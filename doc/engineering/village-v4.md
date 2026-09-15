# 村庄 v4：紧凑布局与正式接入

> 2026-09-15：[双向推门与院外接坡](home-access.md)更新门方向的可选保存、住宅缓存恢复和 v4 局部地形／路面；[实际验收](../production/home-access-validation.md)记录连续院外往返，保留旧批次的历史结果。

> 2026-09-14 负责人授权。全村适度收紧，主角家、溪流和双桥固定；采用 v3 树木与两铺美术。实际交付状态与测试结果见[验收记录](../production/village-v4-validation.md)。

## 布局与版本

24 栋建筑保留原体量，住宅连同院落、铺面连同工作区一起搬移；不通过缩放房屋或角色压缩地图。主角家及院地、溪流、桥梁、地图外围边界不动。西南住宅组成连续院群，中心设施形成相邻街巷，北侧旧站及铁路景观整体南移，溪东住宅沿双桥之间聚拢。外围景观仍保留，第一章任务、NPC、森林及室内没有新增。

布局正文为[共享布局 JSON](../../assets/generated/village/layout-v4.json)，包含建筑、道路、院地所需尺寸、地标、树木、第一批物件、工作净空及回归路线。`VillageLayoutProfile3D` 为每个外景实例提供布局；旧 `VillageLayout3D`、`VillageExteriorLayout3D` 仍保存原布局。模型变换、碰撞与地表选择属于表现层，不向 Core 增加类型或字段。

参见[旧总平面](../../outputs/village-v4/village-v2-site-plan.png)、[带尺寸总平面](../../outputs/village-v4/village-v4-site-plan.png)、[全部建筑新旧登记](../../outputs/village-v4/layout-register.md)和[院门连通性检查](../../outputs/village-v4/layout-connectivity.json)。主路 4 米、支路 3.5 米、巷道 2.5 米，院门净宽 2.8 米；保留 3×3 米装卸、2.4×2.4 米合作和 8×8 米陪练加四周 1.5 米边带。规划框不是已经接入的任务或战斗。

## 可编辑源与迁移

- [v4 Blender 源](../../art/blender/village/village-v4.blend)从保存的 v3 派生；建筑与道具主要使用原网格，只改变所属组的变换。道路、电杆连线和受影响的铺地按新位置调整。住宅 v1、村庄 v1～v3 原文件保留。
- [v4 GLB](../../assets/generated/village/3d/village-v4.glb)与[资产清单](../../assets/generated/village/3d/village-v4-manifest.json)记录实际导出与保留网格。UV、材质和独立可替换道具沿用最新美术；没有制作章末工具箱。
- 地形从**当前保存的 Terrain3D 原生区域**导出浮点高度、控制图和颜色后派生，原数据不覆盖。新院地与道路周边恢复、平整并回接土坡，搬走地点不保留废弃院地台阶；固定锚点高度保持。广场原有地表与维护细节迁移到新位置。
- [v4 原生地形](../../assets/generated/village/terrain-v4/terrain.tscn)及四个区域为后续编辑源。沿用 Terrain3D 1.0.2、0.5 米间距和四区域；原地形材质与 CC0 运行贴图共享，不修改其内容。高度备份位于 `art/terrain/village-v4/`，使用 float32 和 EXR，不使用 8 位高度图。
- 灰模阶段发现广场连接坡段会阻止移动，已对新道路附近的土坡作平顺处理，保留保护区、建筑地台和物件落地高度。过程备份保存在该目录的 `edits/`。不更改角色坡度上限来绕过地形问题。

素材继续引用[v3 CC0 来源](../../art/sources/cc0/village-art-v3/provenance.json)和[已采用地表来源](../../assets/generated/village/terrain-sample-v1/provenance.json)。本批没有新素材采购、图像生成、插件或引擎升级。

## 编辑与复验

```powershell
# 保存 Blender 手工修改后导出；不能重新 init 覆盖精修
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v4.py -- export
# 临时目录只读复导
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v4.py -- check
# 原生地形与 float 备份一致性、保护区及重开检查
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --headless --path . --script scripts/tools/village_v4_terrain.gd
```

`build_village_v4.py init` 拒绝已有 v4 源，不调用旧构建器。`prepare_village_v4_terrain.py` 也拒绝已完成初始化的数据。`layout_village_v4.py` 是本次落位配方，会写共享布局与平面图；`refine_village_v4_grades.py` 是明确的地形编辑操作，会先备份再修改浮点高度，均不是只读检查工具。后续直接编辑原生地形时，应先导出新的浮点备份，再更新预期检查数据；不可为了通过检查覆盖人工精修。

## 存档与游玩

沿用 SaveEnvelope v1、现实地点标识、世界坐标含义和内容版本 6。恢复时先还原门灯，再依据当前外景验证落点；有效原坐标保留，小幅高度差贴合地表，无效位置恢复至固定家门外安全点。旧档不会被按建筑位移盲目平移，不清空住宅状态、梦境记录或醒来进度。独立预览不读写正式存档。

室外 4.2 米/秒、Shift 慢走 1.4 米/秒，住宅保持 3.1 米/秒；上午光照、镜头、暂停、住宅往返与缺失角色时的 billboard 回退沿用现有机制。

本机角色以及包含该角色的截图、录像继续只限本机非商业使用，禁止提交、推送、分发和游戏导出。负责人视觉与手感评价独立于自动验证。

## 正式与历史入口

正式新游戏醒来和继续存档、默认 `VillagePreview.tscn` 均使用 v4。独立 `VillageCompactPreview.tscn` 明确选择 v4，不读写正式存档。

```powershell
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageCompactPreview.tscn
# 以下选项只用于独立 VillagePreview；正式模式忽略它们
# -- --village-v2                  原 v2 布局 + 已采用的旧 Terrain3D
# -- --village-original-terrain    原 v2 GLB 地形
# -- --village-v1                  原 v1 外景与原地形
```

`VillageArtPreview.tscn` 保留原位置的 v3 样板；`VillageTerrainPreview.tscn` 保留旧布局的地形样板。[VillageCompactAuthoring.tscn](../../scenes/testing/VillageCompactAuthoring.tscn)包含 v4 参考建筑和可编辑原生地形，原 GLB 地面隐藏。该场景只作编辑参考，游玩碰撞由预览控制器创建。

## 贴图加载

v4 的 24 张独立提取图使用 VRAM Compressed 和 mipmaps，Compatibility 下采用 S3TC；旧 v2／v3 导入设置及所有源图保持不变。该格式为有损 GPU 压缩，需与叶片裁切、棚布和木纹的实机画面一起检查，不以降低地形质量换取加载速度。依据 [Godot 4.6 图像导入说明](https://docs.godotengine.org/en/4.6/tutorials/assets_pipeline/importing_images.html)。本机交替加载数据、首次与后续加载的区别见验收记录。


当前完整独立专项命令为 `VillageV1Probe.tscn -- --village-v4`；同输入旧布局对比附加 `--v4-baseline`。历史 v1／v2 检查器主体保留，调用时分别显式使用 `--village-v1`／`--village-v2`，不要把旧碰撞坐标测试用于新的默认布局。
