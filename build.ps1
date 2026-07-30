[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root "PSVR2iRacingHaptics.sln"
$testProject = Join-Path $root "tests\PSVR2iRacingHaptics.Tests\PSVR2iRacingHaptics.Tests.csproj"
$appProject = Join-Path $root "src\PSVR2iRacingHaptics.App\PSVR2iRacingHaptics.App.csproj"
$buildProperties = [xml](Get-Content (Join-Path $root "Directory.Build.props"))
$version = [string]$buildProperties.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "The application version is missing from Directory.Build.props."
}
$publishDir = Join-Path $root "build\portable\PSVR2iRacingHaptics"
$zipPath = Join-Path $root "build\PSVR2-iRacing-Haptics-v$version-$Runtime-portable.zip"
$hashPath = "$zipPath.sha256"

$sdkVersion = dotnet --version
if (-not $sdkVersion.StartsWith("8.")) {
    throw ".NET 8 SDK is required. Found version: $sdkVersion"
}

Write-Host "Restoring..."
dotnet restore $solution --disable-parallel -m:1

$selfContained = -not $FrameworkDependent
if ($selfContained) {
    Write-Host "Restoring the $Runtime runtime pack..."
    dotnet restore $appProject -r $Runtime --disable-parallel -m:1
}

Write-Host "Building..."
dotnet build $solution -c $Configuration --no-restore -m:1

Write-Host "Running tests..."
dotnet run --project $testProject -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed."
}

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "Publishing for $Runtime (self-contained=$selfContained)..."
dotnet publish $appProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContained `
    --no-restore `
    -m:1 `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o $publishDir

$executablePath = Join-Path $publishDir "PSVR2iRacingHaptics.exe"
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Published executable was not created: $executablePath"
}

$executableBytes = [System.IO.File]::ReadAllBytes($executablePath)
if ($executableBytes.Length -lt 64 -or
    $executableBytes[0] -ne 0x4d -or
    $executableBytes[1] -ne 0x5a) {
    throw "Published executable does not have a valid Windows PE header."
}
$peOffset = [BitConverter]::ToInt32($executableBytes, 0x3c)
if ($peOffset -lt 0 -or $peOffset + 6 -gt $executableBytes.Length) {
    throw "Published executable contains an invalid PE offset."
}
$machine = [BitConverter]::ToUInt16($executableBytes, $peOffset + 4)
if ($machine -ne 0x8664) {
    throw ("Published executable is not x86-64 (PE machine=0x{0:x4})." -f $machine)
}

Copy-Item (Join-Path $root "README.md") $publishDir
Copy-Item (Join-Path $root "CHANGELOG.md") $publishDir
Copy-Item (Join-Path $root "LICENSE") $publishDir
Copy-Item (Join-Path $root "portable.mode") $publishDir
Copy-Item (Join-Path $root "docs") (Join-Path $publishDir "docs") -Recurse

$forbiddenToolkitFiles = Get-ChildItem -LiteralPath $publishDir -Recurse -File |
    Where-Object {
        $_.Name -ieq "psvr2_toolkit_capi.dll" -or
        $_.Name -like "PSVR2Toolkit*.exe"
    }
if ($forbiddenToolkitFiles) {
    throw "A PSVR2 Toolkit binary was found in the portable publication."
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path $hashPath) {
    Remove-Item -LiteralPath $hashPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    if (-not ($entryNames -contains "PSVR2iRacingHaptics.exe") -or
        -not ($entryNames -contains "portable.mode") -or
        -not ($entryNames -contains "README.md")) {
        throw "Portable ZIP is missing a required file."
    }
    if ($entryNames | Where-Object {
            $_ -match "(?i)psvr2_toolkit_capi\.dll$" -or
            $_ -match "(?i)PSVR2Toolkit.*\.exe$"
        }) {
        throw "A PSVR2 Toolkit binary was found in the portable ZIP."
    }

    foreach ($entry in $archive.Entries) {
        if ($entry.Length -eq 0) {
            continue
        }
        $inputStream = $entry.Open()
        try {
            $inputStream.CopyTo([System.IO.Stream]::Null)
        }
        finally {
            $inputStream.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}

$hash = (Get-FileHash -Algorithm SHA256 $zipPath).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $zipPath)" | Set-Content -Path $hashPath -Encoding ascii

Write-Host ""
Write-Host "Package created: $zipPath"
Write-Host "Portable entries: $($entryNames.Count)"
Write-Host "PE machine: x86-64 (0x8664)"
Write-Host "Toolkit binaries bundled: no"
Write-Host "SHA-256: $hash"
