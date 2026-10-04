param(
    [string]$GodotConsole = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe',
    [string]$DotnetRoot = 'C:\Users\duwei\.dotnet'
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$env:DOTNET_ROOT = $DotnetRoot
$env:Path = $DotnetRoot + ';' + $env:Path
& (Join-Path $DotnetRoot 'dotnet.exe') build (Join-Path $projectPath 'TownRpg.csproj') --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed; preview was not started.' }
# Interactive native sample. Its session suppresses all persistence.
& $GodotConsole --path $projectPath --scene 'res://scenes/testing/TypographyReview.tscn'
exit $LASTEXITCODE
