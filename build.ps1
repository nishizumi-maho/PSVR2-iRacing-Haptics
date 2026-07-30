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

Copy-Item (Join-Path $root "README.md") $publishDir
Copy-Item (Join-Path $root "CHANGELOG.md") $publishDir
Copy-Item (Join-Path $root "LICENSE") $publishDir
Copy-Item (Join-Path $root "portable.mode") $publishDir
Copy-Item (Join-Path $root "docs") (Join-Path $publishDir "docs") -Recurse

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path $hashPath) {
    Remove-Item -LiteralPath $hashPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -Algorithm SHA256 $zipPath).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $zipPath)" | Set-Content -Path $hashPath -Encoding ascii

Write-Host ""
Write-Host "Package created: $zipPath"
Write-Host "SHA-256: $hash"
