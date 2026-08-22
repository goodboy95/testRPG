# 主角 3D 美术资产

本目录同时保留第一轮半写实主角的离线技术样模和第二轮原画式静态视觉候选制作源。`protagonist-static-v1.blend` 只用于验证官方人体基底、独立服装外壳、PBR 材质、GLB 导出与教室尺度；它尚未达到视觉候选质量。`exports/protagonist-static-v1.glb` 只是无骨骼、无动画的导出验证件，不应接入游戏场景。

主角 v2 以新的正式身份、脸发和服装参考重做可见网格，目标是普通偏瘦、写实比例但保留原画气质的中国大学生。v2 静态 Blend、GLB 和 manifest 已产生并通过只读技术检查，但仍等待负责人视觉确认；技术检查通过不代表已经绑定、制作动画或进入 Godot。第一室运行时继续使用 2D billboard。

## 来源与许可

- 人体基底：Blender 官方 **Human Base Meshes v1.4.1** 的 `Body Male - Realistic` collection。
- 下载页：https://www.blender.org/download/demo-files/
- 直接来源：https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip
- 官方压缩包 SHA-256：`811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515`
- 许可：CC0 1.0 / Public Domain。
- 仓库只保存采用并重塑后的男性基底，不保存约 49 MB 的完整官方资产包。

角色身份与服装以 `art/concepts/characters/` 下的正式参考为准，以现有像素主角为颜色和远景轮廓辅助。模型为普通中国大学生：短深色头发、砖红短袖衬衫、深蓝长裤和帆布鞋，不包含武器、超自然装饰或英雄化体格。

## 正式视觉参考

| 文件 | 用途 | SHA-256 |
| --- | --- | --- |
| `protagonist-art-direction-v1.png` | 权威身份、配色、服装轮廓与克制气质锚点；由忽略目录中的原始方向图按字节提升到正式概念目录。 | `17842b77ff8b810933e745e9edc6148c41375b4bb83bbaff19b6aad60b0d90c2` |
| `protagonist-face-hair-reference-v2.png` | 同一人物的正面、侧面、背面与 3/4 脸型，以及可建模为实体发束的短发层次。 | `8f5d2139c095375ac1063d7d263955806a4bbf5614868745da09ba702cd39826` |
| `protagonist-garment-reference-v2.png` | 衬衫、长裤与旧帆布鞋的结构、缝线、褶皱和克制磨损；不作为照片贴图直接投射。 | `492c38f9335febf1f2561312f852abb9c2f2dc1d4de22270fc31ce7b32082be3` |
| `protagonist-3d-turnaround-v1.png` | 同尺度正/侧/背 A-pose 比例辅助；身份细节冲突时服从前三项正式参考。 | `59a3191da3c7d7dcbabb8c7cc6bb38c6fdf394025123c16ab7266bf3ac010abc` |

前三项参考由 OpenAI 内置图像生成协助生产或整理，采用日期为 2026-08-22；原提示词、参考关系、尺寸、哈希和后处理记录在 `assets/generated/provenance.json`。所有 PNG 受根目录 `.gitattributes` 的 Git LFS 规则覆盖；未提交前不得宣称已经进入版本历史。

## v2 静态候选门槛

- 权威美术源为 `protagonist-art-pass-v2.blend`；它必须由美术人员在 Blender 中继续编辑，常规导出与检查脚本不得从空场景覆盖手工作品。
- 目标身高为 `1.69±0.02m`，普通偏瘦体格；实体发束、独立可变形衬衫/裤子/鞋，以及删除衣物覆盖下不可见身体面的干净拓扑。
- 静态预算为 30–36k 可见三角面、最多 6 个未来蒙皮网格、最多 5 个材质面、一套 2048² Albedo/Normal/ORM；最终运行时 GLB 目标不超过约 20 MB。
- 首轮只允许输出未绑定静态候选与验收图。负责人确认脸型、发型、服装轮廓、材质和方案 A 教室比例前，不制作骨骼、权重、`idle`、`walk`、`jog` 或 Godot 接入。
- 静态候选检查关注尺寸、拓扑、UV、材质、外链与预算；当前结果为身高 `1.7017m`、`34972` 三角面、6 个可见网格和 1 个材质面，三张 2048² 纹理均已登记，且不存在 Armature 或 Action。manifest 仍明确写入 `runtimeReady=false`。

候选 Blend 准备好后，先显式生成静态 GLB/manifest，再使用只读检查复导出比对；两种模式都只读取权威 Blend，不调用 v1 的程序化重建流程：

```powershell
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_protagonist_art_pass_v2.py -- art-export --root .
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_protagonist_art_pass_v2.py -- art-check --root .
```

当前只读复导出检查已经输出 `PROTAGONIST_ART_V2_CHECK_PASS meshes=6 triangles=34972 height=1.7017 bytes=1532748 readOnly=true bound=false animated=false`，正式静态 GLB 与 manifest 位于 `assets/generated/characters/3d/`。这只证明静态文件满足当前技术预算、导出内容与仓库文件一致；视觉验收仍未通过，资产仍是 `runtimeReady=false`，不得接入当前场景或开始绑定与动画阶段。

验收图由权威 Blend 派生的离线渲染和独立 Godot 捕获场景生成，输出只进入忽略的 `outputs/protagonist-art-pass-v2/`。对照板可在这些图已经存在后重建：

```powershell
python .\scripts\tools\build_protagonist_review_sheet.py --root .
```

当前方案 A 捕获的默认与前侧角度角色高度为画面的 `36.47%`，后侧角度因 SpringArm 被墙体压缩到约 `2.43–2.44m` 而达到 `49.25%–49.49%`；因此原定 `28%–31%` 构图门槛尚未满足。模型自身 `1.7017m` 的世界尺度正确，不能用缩小人物来掩盖镜头问题。若视觉方向获批，绑定前需另行决定将真实 3D 人物验收范围调整到约 `35%–38%`，或为 3D 人物增加约 `4.0–4.2m` 的镜头距离/更宽 FOV，并单独处理后侧碰撞构图。

## 构建与校验

```powershell
$PythonExe = 'C:\Python314\python.exe'
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'

& $PythonExe .\scripts\tools\build_protagonist_static.py prepare-source --root .
& $BlenderExe --background --python .\scripts\tools\build_protagonist_static.py -- adopt-source --root .
& $PythonExe .\scripts\tools\build_protagonist_static.py texture-build --root .
& $BlenderExe --background --python .\scripts\tools\build_protagonist_static.py -- build --root .
& $BlenderExe --background --python .\scripts\tools\build_protagonist_static.py -- render --root .
& $PythonExe .\scripts\tools\build_protagonist_static.py texture-check --root .
& $BlenderExe --background --python .\scripts\tools\build_protagonist_static.py -- check --root .
```

`prepare-source` 只从已下载到 `work/` 的官方压缩包中提取源 Blend；不会下载文件。`adopt-source` 进一步只保存采用的男性身体与双眼至 `source-v1/`，不会把完整资产包加入仓库。后续角色构建读取该精简来源。`check` 在临时目录重新导出 GLB并比对哈希，不改写仓库文件。

## 当前边界

- v1 当前资产明确为 `technical-modeling-sample`，不是静态视觉候选；没有 Armature、Skeleton、权重、`idle` 或 `walk`。
- 程序化衬衫、直筒裤、发帽和帆布鞋已经替换最初的贴体/破面方案，但仍需要美术人员在 Blender 中进行服装重拓扑、脸部相似度雕刻、发束与皮肤贴图精修后再送视觉验收。
- 自然站姿由离线网格变形烘焙，不代表最终骨骼变形质量。
- v2 静态候选已独立完成本轮技术导出，不能通过替换 v1 的文件或 manifest 冒充升级；v1 必须保留作尺度与导出基线。
- v2 视觉确认后才建立 Godot Humanoid 对应骨架、绑定和动画；那一阶段必须另行验收肩、胯、袖口、脚底和近墙镜头处理。
