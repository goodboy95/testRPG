# 沈砚静态 v2：重新设计与制作入口

`[已实现]` 2026-09-18：退役失败的模块化模型，从独立 CC0 解剖基体制作一个新的静态沈砚候选。可编辑 Blend、静态 GLB、真实烘焙图集、来源清单、格式校验和独立引擎审图场景已存在。**美术未批准，`runtimeReady=false`、`visualApproval=false`；这不是可走动或可对话的新人物资源。** 实际检查与不足见[本轮验收](../production/shen-yan-static-v2-validation.md)。

## 设计方向

`[固定约束]` 先验证一个自然比例、半写实国风动画方向的青年男性。外观视觉年龄约 18–20 岁，身高约 1.73 米，偏瘦而健康；这些是美术制作基准，不增加正式剧情事实。保留 `shen_yan` 角色 ID，新资产 ID 为 `shen_yan.static_v2`。

服装组合为灰蓝开襟轻衬衫、卷袖、米白圆领内搭、炭灰直筒裤、旧白帆布鞋。短深色侧分发型、自然大小的眼睛、可读的眼睑/鼻翼/唇线是近景重点。当前源已具备人体、五官、手指和连续衬衫，但还没有达到原画的脸部和服装完成度。

原创概念参考位于 `art/concepts/characters/shen-yan-v2/design-sheet.png`。`generation.json` 保存工具、完整提示词、日期和处理记录；工具未提供具体模型版本，记录为未公开。概念图只定义方向，没有投影到模型，也不能代替实际 3D 截图。

## 来源与实际制作方法

从 [Blender 官方资产包页面](https://www.blender.org/download/demo-files/)对应的 [Human Base Meshes v1.4.1](https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip) 独立选取 `Body Male - Realistic`，作者 Dan Ulrich。选择的 collection 元数据和包内 README 均明确 CC0；包内另一个 Rain Rig 的许可文本没有套用到此基体。

- 独立来源：`art/sources/cc0/npc-human-base-v1/body-male-realistic.blend`。
- 来源说明：同目录 `UPSTREAM_README.txt`、`provenance.json`，包含原包地址和 SHA-256。
- 权威制作源：`art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend`。
- 采用方法：CC0 人体改形、脚本编辑连续网格、独立服装/发束/眼部制作、共享材质和 UV 烘焙。不是手工高模雕刻或高低模重拓扑烘焙。
- 未采用：旧失败 NPC、主角 v2、mc_model、晓雨、本机受限动作、MB-Lab、MakeHuman。没有运行时生成模型调用。

制作源包含 `ShenYan_Artwork` 中的可编辑部件和隐藏的解剖参考。只有带 `export_static=true` 的美术网格进入静态 GLB。来源中不包含主角资源，独立审图也不会实例化主角。

## 资产与贴图

导出目录为 `assets/generated/characters/shen-yan-v2/`，包含 `shen-yan-static-v2.glb` 和 `manifest.json`。manifest 根据实际文件计算源/GLB/纹理哈希、面数、材质、高度、图像尺寸和再导入结果，不用目标预算冒充实际统计。

最多 32k 三角形、一个合并网格、三个材质面：皮肤/眼睛、衣物/鞋、头发/眉毛。源保持分开的编辑部件，只有导出副本合并。没有 skeleton、skin、动画、LOD 或外观配方；Godot 自动 LOD 和动画导入也关闭。

皮肤和衣物各使用 2K Albedo/Normal/ORM，头发使用 1K，共九张图。Albedo 烘焙真实顶点色；Normal 来自现有表面和程序微表面，ORM 为 AO、部位粗糙度、零金属度。`bake-record.json` 明确记录 `highPolySculpt=false` 和 `generatedConceptProjected=false`。这些图集属于当前候选，不代表已经实现多人共享纹理库。

## 修改、导出与只读检查

优先编辑并保存权威 Blend。`author_shen_yan_v2.py` 是一次性初建记录，源存在时拒绝覆盖；`refine_*`、`fit_*`、`polish_*`、`finish_*`、`clearance_*` 和衣领修正脚本是带版本保护的本轮制作记录，不能当作每次导出前必跑的构建链。

只改几何并保持现有贴图有效时，保存后直接导出。改变 UV/部件/顶点色/粗糙度时应重新烘焙；下面的显式 `--rebake` 会重建三组制作期材质节点、重新展 UV，并保存源，**不适用于已另行手绘精修的贴图**。以后若使用手绘贴图，应继续直接编辑保存，不用这条命令覆盖。

```powershell
& 'D:/ProgramFiles/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python scripts/tools/bake_shen_yan_v2.py -- --rebake
& 'D:/ProgramFiles/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python scripts/tools/export_shen_yan_v2.py -- export
& 'D:/ProgramFiles/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python scripts/tools/export_shen_yan_v2.py -- check
& 'D:/ProgramFiles/Blender/blender.exe' --background --factory-startup --python-exit-code 1 --python scripts/tools/review_shen_yan_v2.py -- exported --glb
```

`export`/`check` 不重建几何，也不保存源；二者校验源前后哈希一致。检查覆盖来源、有限数值、单位法线、有效 UV/切线、纹理非空、三个完整 PBR 材质、32k 上限、无 rig/animation、内嵌图像和真实 GLB 再导入。此类检查不能证明拓扑适合动画、全部 UV 无重叠或美术合格。

## 独立引擎审图

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' build TownRpg.csproj --configuration Debug --no-restore
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --editor --import
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --audio-driver Dummy --path . --scene res://scenes/testing/ShenYanStaticReview.tscn
```

数字 1–7 切换正、三分之四、侧、背、脸、脸三分之四、第三人称距离；L 切换中性、日间、阴影区、夜间光照；右键旋转、滚轮缩放、Esc 退出。`Dummy` 只禁用审图场景不需要的音频设备，与画面无关。

追加 `-- --shen-review-capture` 会捕获三个分辨率各 13 个视角/光照组合，共 39 图，并写入 `outputs/shen-yan-v2/godot/results.json`。正侧背/头部使用中性光，日/阴影/夜分别包含全身与头部；另含第三人称距离。头部隐藏审图地面，日/夜开启模型阴影，中性/阴影区用于漫射表面复核。

该场景不经过 `VillageNpcActor3D`，只检查静态 GLB、PBR 导入、实际整帧 primitives/draw calls，以及正式会话与存档字节未变。审图房间的计数不是村庄性能，RTX 2080 Ti 的结果不是 GTX 1060 笔记本验收。

## 当前停止线与后续模块化

`[固定约束]` 当前未过视觉门槛，不进行绑定、动画、女性基体、居民组合或正式替换。先修脸部表情与平面、自然发束和发际线、布料褶皱、鞋型及衣领/袖口近景，再用同一组真实截图复核；不能通过提高面数、换配色、柔化灯光或概念图来宣布完成。

`[计划]` 一个男性样板通过后，才确定统一骨架和四组动作；以弯肘、抬臂、迈步验证服装拓扑，再拆出可交换头、发型、上装、下装、鞋和少量配饰。先做少数组合，覆盖区域互斥并离线合并；随后制作女性基体，必要时再增加第三/第四体型，避免先铺量。

`[计划]` LOD 和 12 人性能在实际资产接入后测量。旧四名 32k＋四名 10k＋四名 2.5k 合计 178k，不能直接承诺 170k；必须按真实三角面与材质成本分配，并维持既定 v7 整帧上限。正式 v6、低模试点、Core、存档和剧情本轮均不替换。
