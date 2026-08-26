$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$localDotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }

& $dotnet build (Join-Path $root 'WorkChatDock.sln') -c Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet run --project (Join-Path $root 'WorkChatDock.SmokeTests\WorkChatDock.SmokeTests.csproj') -c Debug --no-build
exit $LASTEXITCODE
