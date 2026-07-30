[CmdletBinding()]
param(
    [switch]$Simulator,
    [switch]$FromSource
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root "build\portable\PSVR2iRacingHaptics\PSVR2iRacingHaptics.exe"
$project = Join-Path $root "src\PSVR2iRacingHaptics.App\PSVR2iRacingHaptics.App.csproj"
$appArgs = @("--portable")
if ($Simulator) {
    $appArgs += "--simulator"
}

if ((Test-Path $exe) -and -not $FromSource) {
    Start-Process -FilePath $exe -ArgumentList $appArgs
    exit
}

dotnet run --project $project -c Release -- $appArgs
