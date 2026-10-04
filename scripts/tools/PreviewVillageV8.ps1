param(
    [string]$GodotConsole = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe'
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not (Test-Path -LiteralPath $GodotConsole -PathType Leaf)) {
    throw "Godot 4.6.3 .NET executable was not found. Pass -GodotConsole with its local path."
}

# Independent, playable sample; the default/formal entry remains v6.
& $GodotConsole --path $projectPath --scene 'res://scenes/testing/VillageAnimePreview.tscn'
exit $LASTEXITCODE
