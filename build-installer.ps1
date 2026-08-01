[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$PublishDirectory,
    [string]$IsccPath,
    [switch]$SkipInstallSmokeTest
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$propertiesPath = Join-Path $root "Directory.Build.props"
$installerScript = Join-Path $root "installer\PSVR2iRacingHaptics.iss"
$buildProperties = [xml](Get-Content -LiteralPath $propertiesPath)
$version = [string]$buildProperties.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "The application version is missing from Directory.Build.props."
}
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "The Inno installer requires a three-part numeric version. Found: $version"
}

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $root "build\portable\PSVR2iRacingHaptics"
}
$PublishDirectory = [System.IO.Path]::GetFullPath($PublishDirectory)
if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
    throw "The published application directory does not exist: $PublishDirectory"
}
$publishedExecutable = Join-Path $PublishDirectory "PSVR2iRacingHaptics.exe"
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "The published application executable is missing: $publishedExecutable"
}

function Resolve-IsccPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path -LiteralPath $RequestedPath -PathType Leaf)) {
            throw "The requested Inno Setup compiler does not exist: $RequestedPath"
        }
        return [System.IO.Path]::GetFullPath($RequestedPath)
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @()
    if (${env:ProgramFiles(x86)}) {
        $candidates += Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe"
        $candidates += Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
    }
    if ($env:ProgramFiles) {
        $candidates += Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe"
        $candidates += Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw "Inno Setup 6.3 or later was not found. Install it or pass -IsccPath."
}

$iscc = Resolve-IsccPath $IsccPath
$outputDirectory = Join-Path $root "build\installer"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$setupName = "PSVR2-iRacing-Haptics-v$version-$Runtime-setup.exe"
$setupPath = Join-Path $outputDirectory $setupName
$hashPath = "$setupPath.sha256"
if (Test-Path -LiteralPath $setupPath) {
    Remove-Item -LiteralPath $setupPath -Force
}
if (Test-Path -LiteralPath $hashPath) {
    Remove-Item -LiteralPath $hashPath -Force
}

Write-Host "Building Inno Setup installer with: $iscc"
$compilerArguments = @(
    "/Qp",
    "/DAppVersion=$version",
    "/DSourceDir=$PublishDirectory",
    "/DOutputDir=$outputDirectory",
    $installerScript
)
& $iscc @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) {
    throw "The Inno Setup installer was not created: $setupPath"
}

if (-not $SkipInstallSmokeTest) {
    $smokeRoot = Join-Path (
        [System.IO.Path]::GetTempPath()) (
        "PSVR2Haptics-InstallerSmoke-" + [Guid]::NewGuid().ToString("N"))
    $installDirectory = Join-Path $smokeRoot "installed app"
    $installLog = Join-Path $smokeRoot "install.log"
    New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
    try {
        Write-Host "Running silent installer smoke test..."
        $installArguments = `
            '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="{0}" /LOG="{1}"' `
            -f $installDirectory, $installLog
        $installProcess = Start-Process `
            -FilePath $setupPath `
            -ArgumentList $installArguments `
            -Wait `
            -PassThru
        if ($installProcess.ExitCode -ne 0) {
            throw "The installer smoke test failed with exit code $($installProcess.ExitCode)."
        }

        $installedExecutable = Join-Path $installDirectory "PSVR2iRacingHaptics.exe"
        if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) {
            if (Test-Path -LiteralPath $installLog -PathType Leaf) {
                Write-Host "Inno Setup install log:"
                Get-Content -LiteralPath $installLog |
                    ForEach-Object { Write-Host $_ }
            }
            throw "The installer did not deploy the application executable."
        }
        if (Test-Path -LiteralPath (Join-Path $installDirectory "portable.mode")) {
            throw "The installed package must not contain portable.mode."
        }
        if (-not (Test-Path -LiteralPath (Join-Path $installDirectory "README.md"))) {
            throw "The installed package is missing README.md."
        }

        $uninstaller = Join-Path $installDirectory "unins000.exe"
        if (-not (Test-Path -LiteralPath $uninstaller -PathType Leaf)) {
            throw "The installer did not create an uninstaller."
        }
        $uninstallProcess = Start-Process `
            -FilePath $uninstaller `
            -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" `
            -Wait `
            -PassThru
        if ($uninstallProcess.ExitCode -ne 0) {
            throw "The silent uninstaller smoke test failed with exit code $($uninstallProcess.ExitCode)."
        }
    }
    finally {
        if (Test-Path -LiteralPath $smokeRoot) {
            Remove-Item -LiteralPath $smokeRoot -Recurse -Force
        }
    }
}

$hash = (Get-FileHash -Algorithm SHA256 $setupPath).Hash.ToLowerInvariant()
"$hash  $setupName" | Set-Content -LiteralPath $hashPath -Encoding ascii

Write-Host ""
Write-Host "Installer created: $setupPath"
Write-Host "Per-user install: yes"
Write-Host "Administrator rights required: no"
Write-Host "Portable marker bundled: no"
Write-Host "Silent install/uninstall smoke test: $(-not $SkipInstallSmokeTest)"
Write-Host "SHA-256: $hash"
