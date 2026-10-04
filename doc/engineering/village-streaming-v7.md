# 村庄分区与 LOD v7

2026-09-16。`[已实现]` 从保存的 v6 派生独立流式预览。本轮负责人将目标提升为 **笔记本 GTX 1060、1080p、60 FPS**，v7 默认使用 `laptop-gtx1060` 开发预算；原集显档通过 `--village-budget-integrated` 回归。正式入口与无参数默认村庄预览仍采用 v6；新预算的本机实测见[GTX 1060 预算验证](../production/village-streaming-v7-1060-validation.md)，前一轮集显预算数据保留在[历史记录](../production/village-streaming-v7-validation.md)。不能把 RTX 2080 Ti 测量写成 GTX 1060 笔记本实测通过。

## 使用与编辑

```powershell
# 独立预览；不读写正式存档。HUD 与日志是开发开关，不进入设置或存档。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageStreamingPreview.tscn -- --village-budget-hud --village-budget-log
# 显式回归上一轮集显配置；没有新增用户设置或存档字段。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageStreamingPreview.tscn -- --village-budget-integrated --village-budget-hud
# 在现有入口显式选择 v7，适用于正式恢复/住宅回归；不改变默认采用版本。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . -- --village-v7
# 保存 v7 制作源后导出；不得运行旧构建器覆盖源文件。
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v7.py -- export
# 只读哈希、节点、三角形和材质面复验。
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v7.py -- check
```

源为 `art/blender/village/village-v7.blend`，由保存的 v6 复制而来。`init` 只用于首次创建，已有源时拒绝执行；`export` 仅写 `assets/generated/village/stream-v7/`，不会保存或覆盖 Blender 源。`pack` 只整理导出包的共享图像引用并更新清单哈希，常规导出已包含此步骤。旧 v1～v6、住宅、原生地形高度文件保留。

导出和只读检查同时限制单元 LOD0～3 为 65k／60k／30k／18k 三角形、24／24／2／2 个 surface；检查四档命名全局唯一，以及建筑视觉 ID 与现有碰撞布局的 24 栋覆盖一致。这是单资产制作上限，运行时仍须通过更严格的总预算选择。

所有材料来自已采用的 CC0 资产，没有新下载、图像生成、角色或动画生产。含本机角色的截图与视频只能留在本机 `outputs/`，不得提交或分发。

## 分区、流式生命周期与资产

- 七区：主角家／南部住宅、西侧住宅与活动室、广场两铺、车站、东南住宅、东北住宅、溪流桥区。以现有坐标分组，不移动建筑、道路、桥、院门或碰撞。
- 89 个视觉单元：24 栋建筑及其近旁摆设、58 棵树、7 组分割后的道路／背景。每单元四档显式 LOD；清单记录世界 AABB、区域、类别、优先级、屏幕尺寸阈值、各档三角面、材质名和 surface 数。
- 每区一个 LOD0/1 近景 GLB 与一个 LOD2/3 代理 GLB，共 14 包。低模常驻，近景在距建筑／树木 AABB 58 米内请求异步加载；离开 78 米后持续 4 秒才释放近景节点。每帧至多实例化一个已加载包，未完成时绝不阻塞等待或移除代理。
- 加载失败保留低模并输出明确错误；传送与旧档恢复无需等待近景就绪。住宅内暂停村庄处理，返回后复用外景与原门灯缓存；所有 LOD、请求和遥测均为表现层临时状态。
- 普通树的近景原型继续共享，远景保留完整、分散的叶片卡片并简化木质枝干。LOD2/3 采用几何树冠代理；没有新增运行时生成或自动相机 billboard。
- 同内容贴图引用现有 v6/v5/v4 等已导入图像，不随街区重复提取。低模用顶点色合并不透明表面，叶片保留原 Alpha Scissor 纹理；Compatibility 的顶点色空间在合批时校正。

## LOD 与预算口径

LOD0 保留近景纹理和构件；LOD1 合并共面几何；LOD2/3 逐步移除完整的小构件，主屋顶使用保持檐口／屋脊范围的封闭外壳。禁止用全模型按比例塌缩破坏薄窗、叶片或接坡。道路地表不改变坡面，远景山体另行简化。近距离、投影尺寸与约 10% 滞后共同决定目标档位；对支持的相机实际视锥加 2 米余量，每帧在镜头更新后执行预算选择。GTX 1060 档的 LOD0／1／2 距离为 20／50／100 米，集显档为 16／35／85 米；投影尺寸可进一步保留细节。附近阴影范围内的物件仍纳入候选。

外景模型使用以下选择预算，三列分别为三角形／可见主 pass 实例／surface 提交。这些值统一来自表现层 `VillageRenderProfile`，不靠显卡名称自动推测，也不进入存档。

| 档位 | near | wide |
| --- | --- | --- |
| GTX 1060 笔记本（默认） | 320,000／140／120 | 450,000／180／180 |
| 集显回归 | 100,000／100／24 | 180,000／140／32 |

如果长视线导致最低代理无法放入，或者会让 18 米内已加载的重要建筑丢失近景材质，切到 wide，并记录 `budgetTier`。GTX 1060 档还会为 24 米内普通建筑失去材质的情况启用 wide，28 米内树冠在远处普通建筑之后降级。仍先降低远景地表细节、远树、普通建筑，近距离主角家、两铺和广场树最后降级；不靠隐藏仍应可见的建筑伪造通过。最低档仍超预算时明确报告 `Fits=false`。不透明远景与叶片按材质合并到常驻 HLOD，近景独立；切换时不会同时显示高低两档。

`[固定约束]` 以下三种数值不能混为一谈：

| 口径 | 作用与边界 |
| --- | --- |
| 导出包各档三角面与材质名 | 资产审计；包括未实例化、未选中的档位，不是帧成本 |
| 选择器 Cost：三角面、实例、surface、独立材质 | 精确记录受控制的外景提交；surface 是主 pass 的绘制预算，独立材质不是 draw call |
| Godot 整帧 primitives、draw calls、GPU/CPU render time | 包含 Terrain3D、角色、UI、阴影等额外成本；不能用选择器通过替代此项 |

`[计划]` GTX 1060 笔记本首版整帧目标：near **600k 三角形／180 可见网格／500 draw calls**；wide **800k／240／700**；过渡 **1,000k／280／900**。GPU 平均 ≤14 ms／p95 ≤16.7 ms 不变，显存目标先控制在 2 GiB 内。它们是项目初始验收值，不是该显卡在所有笔记本、散热、CPU 或驱动组合下的保证。整帧三角面和 draw call 由引擎计数独立验证；渲染对象数不是去重可见网格数，不能用它宣称网格上限通过。原集显 220k／300k／360k 及 24／32／36 draw call 目标只保留为旧配置的历史口径，其未达项不能被新门槛追认通过。

v7 只在地形实例上选择 `mesh_size=16`、`mesh_lods=7`，保留原生高度、0.5 米近处采样及碰撞。GTX 1060 档阳光使用 60 米单阴影视图，40 米内的 LOD0/1 非地表单元启用更完整的 LOD2 阴影代理；草石 MultiMesh 按 16 米格拆分、55 米裁切、4 米滞后、关闭投影。集显回归仍为 40 米阴影视图、25 米内 LOD0 使用 LOD3 阴影代理、38 米草石范围。可见高模不重复参与阴影 pass，原 v6 的 105 米四分区阴影保留在回归入口。

GPU 计时启用后以两秒窗口调整 0～2 级压力；计时器不返回有效值时显示 `N/A`，不记为零毫秒通过。HUD 显示外景 Cost 与整帧计数，JSONL 每 0.5 秒记录区域／类别／LOD、加载耗时、控制器耗时和压力。低模顶点变换／颜色转换在载入时缓存，切换时直接拼接数组；旧的自有合批 GPU 缓冲区立即释放。资源实例化和缓冲区上传仍占主线程时间，必须用包含移动、转镜及冷加载的帧时记录验收。逐帧统计单独标记截图写盘，避免把 PNG 编码开销当作游戏帧时。

## 验证入口与兼容

`VillageRenderPolicy` 不引用 Godot，也不属于 Core 业务状态，通过测试工程链接验证预算、滞后、缺包代理、最低档超额和 575 点保守全村预算。运行矩阵使用：

```powershell
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageStreamingProbe.tscn --audio-driver Dummy
# 同点位 v6 对照；快速诊断可额外添加 --v7-quick。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageStreamingProbe.tscn --audio-driver Dummy -- --v7-baseline
# 复用真实物理路线、旧档安全恢复、桥头、边界和住宅往返检查。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageV1Probe.tscn --audio-driver Dummy -- --village-v6 --home-trees-routes --village-v7
```

矩阵覆盖九个关键位置及可安全站立的 30 米网格，每点 8 个非直角朝向、720p/900p/1080p；检查实际捕获尺寸，保存 GPU、整帧计数、显存和按加载／稳定／移动阶段划分的逐帧数据。住宅往返检查门状态与方向，预览检查正式状态及存档字节不变。完整场景回归仍使用九场景冒烟，不把自动检查写成人工终验。

`python scripts/tools/summarize_village_v7.py <v7/results.json> <baseline/results.json>` 可只读汇总各分辨率的 GPU 平均／p95、整帧图元／调用峰值、预算超限样本以及平均／最低瞬时 FPS。FPS 使用实际逐帧间隔，不使用等待 `FramePostDraw` 的局部耗时；截图阶段单列。探针对每个稳定样本同时断言外景预算和相应 near/wide 整帧三角面目标，无法取得 GPU 计时时保留空值。

没有新增 Core 类型、存档字段、地点 ID、任务、设置项或依赖；SaveEnvelope v1、内容版本 6、Godot 4.6.3 .NET／Compatibility 不变。
