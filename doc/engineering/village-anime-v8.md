# 村庄 v8：清新动画乡村样板

2026-09-26。负责人批准完整改造计划，并指定先完成主角家—广场—两铺的可玩样板，确认后推广全村。本授权替代此前相关外观不得调整的阶段边界；正式和默认村庄继续使用 v6。

## 方向与阶段

- `[固定约束]` 清新动画乡村、自然比例、近现代中式日常。24 栋建筑位置和主体占地、主路、溪流、双桥、门口净空与修复后的地形高度保留。无全屏描边或硬切赛璐璐。
- `[已实现]` 第一、二步的首版独立可玩样板、可编辑源与图像证据已制作，尚待负责人视觉确认；技术结果与局限单列于验收。
- `[计划]` 第三步：活动室、house02／03／08／13／15、溪边休息处，再推进其余住宅、公共建筑、旧站及铁路附屋；逐栋保留正面和来路视角。第四步：推广全村树木及道路、桥岸、水面、远山和村界接近段。第五步：重新制作／复核全村四档 LOD、路线与恢复、目标硬件及负责人终验后采用。样板确认前不推进第三步。
- `[固定约束]` 本轮仅外景；住宅场景的外侧同步，室内家具、灯光、房间、门轴不变。不接 NPC、森林、任务或剧情。Core、SaveEnvelope v1、内容版本 6 不变。

## 样板资源与入口

`[已实现]` 新增独立 `scenes/testing/VillageAnimePreview.tscn`。现有独立预览可用 `--village-v8`，正式路由忽略该美术开关。最终运行验证与已知问题见[验收](../production/village-anime-v8-validation.md)。

```powershell
.\scripts\tools\PreviewVillageV8.ps1
# 换机时传入同版本 Godot .NET 控制台程序路径：-GodotConsole '...exe'
```

样板内容：主角家、杂货铺、修理铺、广场摆设；广场树及 Tree_023／028／040 三类代表树；连接路面、独立 Terrain3D 外观和夏日上午光照。其余建筑与 54 棵树仍为原资源外观，不代表全村改造已完成。

主角家与两铺采用原创屋顶外壳、稀疏瓦缝、檐口收边和木构节点；店棚采用下垂曲面及波浪布边。树冠以不规则叶团、分枝和少量外缘叶片构成。材质以低频色彩变化弱化照片颗粒；地形纹理由固定数学配方制作，法线保持平缓。

真实制作方式为 **Blender Python 脚本网格制作与人工参数调整**，不是手工高模雕刻或手绘贴图。没有调用图像生成服务、下载或购买新资产。

## 保存源与安全编辑

- 村庄：`art/blender/village/village-v8.blend`，从保存的 v7 派生。
- 住宅：`art/blender/home/home-anime-v8.blend`，从住宅外观 v2 派生。
- 流式包：`assets/generated/village/stream-v8/`；住宅：`assets/generated/home/anime-v8/`；地形外观：`assets/generated/village/terrain-v8/`。
- 来源及色板：`art/blender/village/v8-provenance.json`；24 栋逐栋状态：`v8-building-checklist.json`；14 个固定审图机位：`v8-review-cameras.json`（均在同一目录）。原始源和运行资产保留，开工快照位于本机 `outputs/village-v8/initial-files.json`。

```powershell
# 仅首次创建；任何一个 v8 保存源已存在就拒绝覆盖。
& D:/ProgramFiles/Blender/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/tools/build_village_v8.py -- init
# 编辑并保存源之后导出；不保存或重建源。
& D:/ProgramFiles/Blender/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/tools/build_village_v8.py -- export
# 只读复验旧源、未改网格、包哈希和实际 GLB 各档成本。
& D:/ProgramFiles/Blender/blender.exe --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/tools/build_village_v8.py -- check
```

`prepare_village_v8_terrain.py` 仅生成独立 v8 外观配方；原生 `terrain-v4/data` 只读引用，贴图控制分布、顶点间距和碰撞保持。手工精修贴图后不得用该配方覆盖。

导出期间关闭使用 v8 的游戏或测试进程；完成 Godot 资源导入后再启动探针。不能在正在流式读取资源的实例旁覆盖同一批 GLB，避免临时包失效和代理回退污染结果。

## 接口、LOD 与验收

表现层 `VillageVisualProfile` 统一指定清单、地形外观、住宅外侧与灯光。流式清单支持版本 7／8，包路径必须匹配版本；加载器使用当前 viewport 实际相机进行预算选择，避免审图相机与剔除相机错位。没有 Core 公共接口或存档变更。

七区与四档 LOD 继续复用 v7。建筑远景删除小构件并保持封闭屋顶范围；样板树保留成组轮廓，远景使用顶点色。沿用 GTX 1060 笔记本预算，不能将当前机器的测试写成目标硬件通过。

```powershell
# 三分辨率无角色实景、连续步行、住宅往返与真实存档不变
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageAnimeProbe.tscn --audio-driver Dummy
# 同机位正式 v6 对照
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageAnimeProbe.tscn --audio-driver Dummy -- --anime-baseline
# 分区、预算、三分辨率性能矩阵
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageStreamingProbe.tscn --audio-driver Dummy -- --village-v8
# 39 路线、净空、桥头与恢复
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageV1Probe.tscn --audio-driver Dummy -- --village-v6 --home-trees-routes --village-v8
```

`[固定约束]` 技术检查、代理视觉意见、负责人确认与目标硬件验收分开。截图或录像写盘时间不能当作游戏帧时。本轮不提交、推送或导出游戏；含受限角色媒体仅留本机。样板审图探针隐藏角色，另提供正常带角色可玩预览。
