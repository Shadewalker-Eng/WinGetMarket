param([switch]$Publish, [switch]$SingleFile)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.cli'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $PSScriptRoot '.nuget-cache'
$env:MSBuildEnableWorkloadResolver = 'false'
if ($SingleFile) {
    dotnet publish "$PSScriptRoot/WinGetMarket.csproj" -c Release -p:Platform=x64 -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "$PSScriptRoot/single-exe"
} elseif ($Publish) {
    dotnet publish "$PSScriptRoot/WinGetMarket.csproj" -c Release -p:Platform=x64 -o "$PSScriptRoot/dist"
} else { dotnet build "$PSScriptRoot/WinGetMarket.csproj" -p:Platform=x64 }
if ($LASTEXITCODE -ne 0) { throw 'Build failed; review the output above.' }
if ($SingleFile) {
    $files = @(Get-ChildItem (Join-Path $PSScriptRoot 'single-exe') -File -Recurse)
    if ($files.Count -ne 1 -or $files[0].Name -ne 'WinGetMarket.exe') { throw 'Single-file publish contains unexpected sidecar files.' }
} elseif ($Publish -and !(Test-Path (Join-Path $PSScriptRoot 'dist/WinGetMarket.pri'))) {
    throw 'Publish is incomplete: WinGetMarket.pri is missing. WinUI cannot load the application XAML.'
}
