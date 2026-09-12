# Dream Manor Blender sources

`[已实现]` `dream-manor-v1.blend` 是梦境洋房 v1 的权威美术源。它同时保存室内外完整模型；该版本现为历史资产；当前运行时加载独立 v3 大厅，外观 v1 仅供历史离线对照，不代表 v3 内部尺寸。

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

## 当前 v3

`dream-manor-hall-v3.blend` 是当前可编辑源，`dream-manor-hall-v2.blend` 保留历史。v3 使用 `scripts/tools/remodel_manor_v3.py`，只读复验模式为 `check --root .`；`build` 会重建 v3，不得覆盖后续手工编辑。尺寸与锚点读取 `MansionLayout3D`，导出不含人物、相机或灯光。运行时 GLB、来源、哈希与验证入口见[大厅 v3](../../../doc/engineering/manor-remodel-v3.md)。
