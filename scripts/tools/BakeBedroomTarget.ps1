param(
    [string]$GodotConsole = 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe',
    [switch]$Material
)
$ErrorActionPreference = 'Stop'
$godotVersion = (& $GodotConsole --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $godotVersion -notmatch '^4\.6\.3\.stable\.mono\.') {
    throw "Bedroom baking requires the fixed Godot 4.6.3 Mono: $godotVersion"
}
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$revision = if ($Material) { 'bedroom-material-v1' } else { 'bedroom-target-v2' }
$visualName = if ($Material) { 'BedroomMaterialVisual.tscn' } else { 'BedroomTargetVisual.tscn' }
$bakePath = Join-Path $projectPath "work/$revision-bake"
New-Item -ItemType Directory -Force -Path $bakePath | Out-Null
# Copy only environment assets and the static scene; no characters, autoloads or saves.
$assetPath = "assets/generated/$revision"
$scenePath = 'scenes/testing'
New-Item -ItemType Directory -Force -Path (Join-Path $bakePath $assetPath), (Join-Path $bakePath $scenePath), (Join-Path $bakePath 'addons/bedroom_bake') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectPath $assetPath) -Destination (Join-Path $bakePath 'assets/generated') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectPath "$scenePath/$visualName") -Destination (Join-Path $bakePath $scenePath) -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bedroom_bake_plugin.gd') -Destination (Join-Path $bakePath 'addons/bedroom_bake/plugin.gd') -Force
@'
[plugin]
name="Bedroom offline bake"
description="Disposable offline bake automation"
author="town-rpg"
version="1"
script="plugin.gd"
'@ | Set-Content -LiteralPath (Join-Path $bakePath 'addons/bedroom_bake/plugin.cfg') -Encoding utf8
@'
config_version=5
[application]
config/name="Bedroom offline bake"
[editor_plugins]
enabled=PackedStringArray("res://addons/bedroom_bake/plugin.cfg")
[rendering]
renderer/rendering_method="gl_compatibility"
'@ | Set-Content -LiteralPath (Join-Path $bakePath 'project.godot') -Encoding utf8
# The runtime project.godot is never edited. Vulkan exists only in this work copy.
& $GodotConsole --editor --path $bakePath --rendering-method forward_plus --rendering-driver vulkan --import
if ($LASTEXITCODE -ne 0) { throw "Baking project import failed" }
$bakeArgs = @('--editor','--path',$bakePath,'--rendering-method','forward_plus','--rendering-driver','vulkan','--language','en','--position','3000,1800','--','--bedroom-bake')
if ($Material) { $bakeArgs += '--bedroom-material' }
& $GodotConsole @bakeArgs
if ($LASTEXITCODE -ne 0) { throw "Bedroom lightmap baking failed" }
$bakedAsset = Join-Path $bakePath $assetPath
if (-not (Test-Path (Join-Path $bakedAsset 'bedroom.lmbake'))) { throw 'Missing lightmap data' }
Get-ChildItem -LiteralPath $bakedAsset -File | Where-Object { $_.Extension -in '.lmbake','.exr' -or $_.Name.EndsWith('.exr.import') } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $projectPath $assetPath) -Force
}
Copy-Item -LiteralPath (Join-Path $bakePath "$scenePath/$visualName") -Destination (Join-Path $projectPath "$scenePath/$visualName") -Force
$outputRoot = Join-Path $projectPath $assetPath
$receipt = [ordered]@{
    producedAt = (Get-Date).ToString('o')
    tool = $godotVersion
    bakeRenderer = 'forward_plus/vulkan (isolated work copy)'
    runtimeRenderer = 'gl_compatibility'
    sourceGlbSha256 = (Get-FileHash -LiteralPath (Join-Path $outputRoot "$revision.glb")).Hash
    preparation = Get-Content -LiteralPath (Join-Path $outputRoot 'unwrap-cache.json') -Raw | ConvertFrom-Json
    outputs = @(Get-ChildItem -LiteralPath $outputRoot -File | Where-Object { $_.Extension -in '.exr','.lmbake' } | ForEach-Object {
        [ordered]@{name=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputRoot 'bake-record.json') -Encoding utf8
Write-Output 'BEDROOM_OFFLINE_BAKE_COPIED'
