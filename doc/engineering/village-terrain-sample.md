# 村庄 Terrain3D 三处地形样板

> 2026-09-14。`[已实现]` 负责人查看样板后确认“看着不错，可以覆盖”，已采用到正式村庄及默认 v2 预览。原样板制作结果见[历史验收](../production/village-terrain-validation.md)，本轮接入见[采用验收](../production/village-terrain-adoption.md)。

## 范围与入口

`[已实现]` 家门外道路、广场边缘、南桥西侧溪岸三处采用“有人维护的乡村”方向。CC0 地表混合、低矮草簇与碎石仅用于环境表现；没有任务、森林、建筑新贴图、水面特效或新交互。室外 4.2 米/秒，Shift 1.4 米/秒；住宅保持 3.1 米/秒。

```powershell
# 独立走查；不访问正式存档。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageTerrainPreview.tscn
```

`VillagePreview.tscn` 和正式醒后探索默认使用村庄 v2 与 Terrain3D 地形。独立预览追加 `-- --village-original-terrain` 回归原 v2 地面，`-- --village-v1` 回归原 v1；`--terrain-baseline` 是原地形测试别名。正式模式忽略这些回归开关。新入口标题为“地形样板 · 夏日上午”。住宅往返、暂停、重置和缺失角色的 billboard 回退沿用共享区域容器。

## 地形与原村庄的关系

`[已实现]` Terrain3D 固定 1.0.2，Godot 保持 4.6.3 .NET／Compatibility。上游插件完整 vendored，Windows 发行包及 DLL 哈希、许可证见[来源记录](../../assets/generated/village/terrain-sample-v1/provenance.json)和[插件说明](../../addons/terrain_3d/TOWN_RPG_VENDOR.md)。没有引擎或渲染器升级。

- 基础地面从 v2 实际 GLB 的 `Terrain` 顶点与原三角剖分采样，非另造一份近似高度公式。0.5 米间距、每区域 256×256 点，四区域覆盖 X/Z -128 至 128 米；活动边界仍为原来的 208×216 米。
- 样板实例隐藏原 `Terrain` 显示网格，省略 `VillageGroundCollision`，由 Terrain3D 提供地面显示、查询和全区域碰撞。原 GLB、住宅源、道路、桥、水面、岸墙、地形外远山及建筑道具不被重建。
- `IVillageSurface3D` 只属于表现层。旧提供者继续使用 `TriangulatedHeight`；样板提供者通过 GDExtension 调用原生高度，桥面由原布局独立覆盖。安全落点使用当前提供者，并拒绝非有限高度与未就绪地面。
- `VillageAreaHost` 默认选择新地形，正式 `EmbeddedInGame` 固定使用已采用地形；只有独立预览的 v1、灰模或原地形开关采用旧提供者。出屋恢复全地形碰撞，进入住宅前停用缓存外景碰撞；不同时保留两套活动地面。
- 建筑院地、硬质道路及其外缘、广场铺地、第一批物件脚下、桥头、树干、电杆及岸墙基础为保护区。43,415 个保护采样点保持原高度；三处共 1,729 点发生局部起伏调整，最大约 0.158 米。

`[固定约束]` Core、SaveEnvelope v1、地点标识、存档字段、梦境内容版本 6、剧情和正式游戏接口不变。恢复测试只使用隔离测试档；正式模式由既有 GameSession 保存，独立预览不读写 GameSession。

## 美术内容与预算

| 样板 | X/Z 制作包络，单位米 | 内容 |
| --- | --- | --- |
| 家门外 | X -74～-39，Z 49～62 | 院门连接及约 30 米道路两侧，压实土、浅排水起伏、碎石路肩与稀疏草簇 |
| 广场边缘 | X -25～3，Z 0～21 | 保留办事区铺地和净空，边缘草土、少量碎石和疏草，过渡回原地势 |
| 南桥西岸 | X 33～49，Z 5～31 | 西桥头到北侧约 20 米岸段，土石混合、局部色差和轻微岸坡变化，岸墙与水位保留 |

采用 ambientCG 的 Grass001、Ground031、Gravel014、Ground037，均为 CC0；源 ZIP、完整 PBR 文件与原始元数据保存在 `art/terrain/village-sample-v1/sources/`。运行纹理为四套 1K，颜色／高度和 OpenGL 法线／粗糙度分别打包，开启 mipmap，关闭会破坏打包通道的自动法线转换。没有调用图像生成工具。

草石为 55 个固定陈列实例，共享两套网格与材质。散布结果保存为可编辑 JSON，不在运行时随机散布，不新增逐颗碰撞、风动或采集。草石避开道路、院地、工作净空和水边阻挡线。

`[已实现]` 样板外的地面高度保留，但四区域统一采用新的低草材质及 Terrain3D LOD，因此全村裸露草地的纹理与远处细节表现也有变化。只有上述三处属于精修；原道路的几何边缘、水面、岸墙和建筑粗模外观仍可见，不能声称全村地形与美术已经完成。

整村资产预算沿用 120 万三角面、600 可见网格、64 材质和 384 MiB 纹理估算。原 GLB 仍受 100 MiB 上限约束，另计地形数据、纹理数组、插件二进制和源文件；实际帧统计包含阴影等多次绘制，不等同于唯一资产三角面。具体估算与同机比较见验收记录。

## 编辑、备份与复验

可编辑原生地形为[terrain.tscn](../../assets/generated/village/terrain-sample-v1/terrain.tscn)及同目录四个 `.res` 区域、外置材质和素材列表。在 Godot 中打开[带村庄参照的编辑场景](../../scenes/testing/VillageTerrainAuthoring.tscn)，选择 Terrain3D 使用插件编辑；原村庄参照锁定，原地面隐藏。该场景用于编辑，玩家走查使用前面的独立入口。

初始高度、颜色、控制位与保护掩码保存在 `art/terrain/village-sample-v1/`，高度为 float32 二进制及 EXR，不使用 8 位 PNG。文件的 X/Z 原点、尺寸、间距、哈希见[manifest](../../assets/generated/village/terrain-sample-v1/manifest.json)。原生区域文件是后续编辑的正文；初始输入用于对照，不应覆盖新编辑。

```powershell
# 只读校验输入与原 GLB；不重建 Blender 资产。
C:/Python314/python.exe scripts/tools/build_village_terrain.py check
# 只读加载原生数据、检查与重开；浮点备份只写 outputs/village-terrain/native-check。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --headless --path . --script scripts/tools/check_village_terrain.gd
```

`init`／`--terrain-init` 只适用于首次空目录制作，已有 manifest 或原生区域时明确拒绝覆盖。手工精修后先复制保存四区域和材质，再通过 Terrain3D 导出 float EXR，更新预期输入及验收记录后复验；不得为通过测试恢复成旧地形。用于初次采样和打包的脚本为[离线制作工具](../../scripts/tools/build_village_terrain.py)与[原生导入工具](../../scripts/tools/import_village_terrain.gd)。

`[已实现]` 负责人已确认本版外观并授权正式采用。正式探索和旧档安全落点已接入；`[计划]` 全村其余区域精修另行安排；杂货铺／修理铺建筑材质仍是后续独立批次。人工手感、真实 Xbox/raw delta、目标硬件和游戏导出未由自动测试替代。
