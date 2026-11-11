param([switch]$SkipScreens)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

if (-not $SkipScreens) {
    dotnet build (Join-Path $here "Screens\Screens.csproj") -c Release -nologo -v q
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    & (Join-Path $here "Screens\bin\Release\net48\nfcehub-screens.exe") (Join-Path $here "img")
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

python (Join-Path $here "build_manual.py")
exit $LASTEXITCODE
