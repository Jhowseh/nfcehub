param([switch]$SkipTests)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$solution = Join-Path $root "NFCeHub.sln"
$output = Join-Path $root "src\NFCeHub\bin\Release\net48"

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

dotnet build $solution -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not $SkipTests) {
    dotnet test $solution -c Release --no-build -nologo -v q
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $output "NFCeHub.exe") $dist -Force
Copy-Item (Join-Path $root "LEIA-ME.txt") $dist -Force
Copy-Item (Join-Path $root "src\NFCeHub\config.example.ini") $dist -Force
Write-Host "Done: $dist"
