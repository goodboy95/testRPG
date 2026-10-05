param([string]$GodotConsole = 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe')
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$env:DOTNET_ROOT = 'C:/Users/duwei/.dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& $GodotConsole --path $projectPath --scene 'res://scenes/testing/BedroomTargetReview.tscn'
exit $LASTEXITCODE
