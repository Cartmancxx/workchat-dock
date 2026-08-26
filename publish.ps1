$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$localDotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }

& $dotnet publish (Join-Path $root 'WorkChatDock\WorkChatDock.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o (Join-Path $root 'dist\WorkChatDock')

exit $LASTEXITCODE
