# Dream Manor v1 Blender source

`[已实现]` `dream-manor-v1.blend` 是梦境洋房 v1 的权威美术源。它同时保存室内外完整模型；运行时只加载门厅/内廊 GLB，外观 GLB用于验收和未来复用。

固定集合为 `InteriorShell`、`ExteriorShell`、`Doors`、`Furniture`、`Props`、`Decals`、`ReservedModules` 和 `Lookdev`。Lookdev 相机与灯光不会导出。四室门后只保留视觉暗门斗，游戏碰撞继续由 Godot 独立维护。

```powershell
$PythonExe = 'C:\Python314\python.exe'
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
& $PythonExe .\scripts\tools\build_dream_manor_textures.py check --root .
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_dream_manor_3d.py -- manor-check --root .
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_dream_manor_3d.py -- manor-capture --root . --output .\outputs\dream-manor-exterior-1080p
```

`manor-build` 从确定性规格重建 Blend 和两套 GLB；`manor-export` 从现有 Blend 重导；`manor-check` 只读打开正式 Blend、在临时目录复导两套 GLB并比对哈希。正式运行和构建不依赖 Blender、Python 或生成模型。

`manor-capture` 只读打开权威 Blend，临时建立验收灯光、地面与相机并输出三张 1920×1080 外观图；不会保存或改写 Blend。Godot 实机的六个内景角度、门口出生、开门状态和碰撞图由九场景冒烟的 `--capture-dream-manor=<目录>` 参数输出。

门动画只旋转以下空节点（Godot Y 轴）：Room 1/3 `-72°`、Room 2 `+72°`、Room 4 左/右 `-62°/+62°`。`Door_Front_*` 永久锁闭。
