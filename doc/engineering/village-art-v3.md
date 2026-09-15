# 树木与两间铺面美术样板 v3

`[已实现]` 2026-09-14 后续布局轮：[紧凑村庄 v4](village-v4.md)已接入正式醒后探索与默认预览，采用 v3 树木／两铺外观和独立派生 Terrain3D。固定主角家、溪流、双桥及 24 栋体量；原布局通过独立预览 `--village-v2` 回归，v1 与 v3 样板入口保留。第一章委托仍未接入。[本轮实际验证](../production/village-v4-validation.md)独立记录；下述旧批次的“正式 v2／样板待采用”均为当时状态。

2026-09-14。`[已实现]` 树木与两间铺面样板可独立走查，正式村庄尚未切换。沿用已采用的 Terrain3D 数据、夏日上午光照、Orbit 镜头和室外 4.2 米/秒；第一章委托尚未接入。实际验证与画面对照见[本轮记录](../production/village-art-v3-validation.md)。

## 入口与版本边界

```powershell
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageArtPreview.tscn
```

[独立场景](../../scenes/testing/VillageArtPreview.tscn)通过 `VillageArtPreviewController` 复用区域切换、住宅门灯、暂停与 billboard 回退。`VillageAreaHost` 只允许非正式、非 v1、非灰模的专用控制器设置 `UseArtSample`；正式控制器不读取美术样板开关。默认预览与正式游戏仍加载 v2，住宅仍为 v1；旧地形和 v1 回归入口不变。

只复用现有树干、建筑、道具简化碰撞。叶片、窗框和棚布缝边没有新碰撞；Terrain3D 继续隐藏 GLB 原地面、替代旧地面碰撞，住宅往返按原机制停用／恢复地表。不改 Core、正式存档、稳定内容 ID 或内容版本 6。

## 制作内容

| 对象 | 本批处理 | 保留边界 |
| --- | --- | --- |
| PlazaTree | 渐细弯曲树干、根颈、分叉、宽展枝冠和带透光空隙的枝叶面片 | 原变换、树池、座椅与树干阻挡范围 |
| Tree_037、Tree_043 | 共享普通阔叶树原型，沿用原朝向与尺寸形成不同呈现 | 原落点，其余 55 棵普通树不变 |
| 乔姨杂货铺 | 浅抹灰、砖石基座、灰瓦、绿白布纹和缝边；窗框窗台、檐口构件、交货台木材 | 建筑体量、货物与推车位置、中央通路和装卸空地 |
| 彭师傅修理铺 | 区分砖石、旧木、瓦面、金属卷帘和雨棚；窗台、檐口、瓦脊与工具区材质 | 门口、工作台净空、可替换物件和既有排水构造 |

新增构造合并为 `V3_shop_Joinery`、`V3_workshop_Joinery`、`V3_ShopCanopySeams` 三个网格对象。原公告纸、货包、推车、木凳和待修物件仍独立；不制作章末工具箱、室内、NPC、森林、风动或季节系统。

主枝采用几何，细枝叶采用四三角折面图集卡片；叶片法线按树冠方向整理。GLB 写入 MASK、阈值 0.45、双面；Godot 显式采用 Alpha Scissor、各向异性 mipmap 和低强度背光。该透明模式可保留叶片阴影，依据 [Godot 4.6 材质说明](https://docs.godotengine.org/en/4.6/tutorials/3d/standard_material_3d.html)。没有改变全场景灯光来补偿素材明暗。

## 素材与编辑源

- [Blender 源](../../art/blender/village/village-v3.blend)、[GLB](../../assets/generated/village/3d/village-v3.glb)、[资产清单](../../assets/generated/village/3d/village-v3-manifest.json)。
- [新增原始素材与许可记录](../../art/sources/cc0/village-art-v3/provenance.json)、[运行纹理制作目录](../../art/blender/village/v3-textures)、[全项目来源索引](../../assets/generated/provenance.json)。
- Poly Haven 的 `chinese_hackberry_bark`、`brick_wall_10`、`grey_roof_tiles`、`denim_fabric`；`island_tree_01` 只采用枝叶图集原图，不采用整树模型。素材采用 CC0，依据[官方许可](https://polyhaven.com/license)。此前采用的抹灰、木材与石材继续复用并记录输入哈希。
- 新表面颜色／OpenGL 法线／粗糙度以 1K 为主，枝叶颜色与透明图集为 2K。旧复用材质保留原分辨率，计入总预算。照片降饱和、压缩色差，统一尺度；侧墙砖纹 UV 沿墙面水平排列。没有固定太阳阴影或图像生成素材。
- `branches-color.png` 为实际枝叶图集：从 CC0 扫描叶片裁切，离线排成四种细枝组合，透明通道保留。`leaves-color.png` 与 leaf normal/rough 是处理中间件，未挂入运行材质。处理配方、原图 URL、尺寸、哈希与日期见来源记录及脚本。

## 保存、导出与只读复验

```powershell
# 在 Blender 编辑并保存 village-v3.blend 后导出
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v3.py -- export
# 临时目录复导比对，不覆盖源和运行资源
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v3.py -- check
```

`graybox` 只写入 `work/village-art-v3-graybox/`。`init` 从已保存 v2 派生，已有 v3 时拒绝执行；不能删除源来绕过保护。人工精修后使用 `export`，禁止重新初始化。脚本只导入旧工具的辅助函数，不调用 v1／v2 构建入口。修改纹理前备份 v3 源与 `v3-textures/`；`prepare_village_art.py` 是离线原料处理配方，会重写派生纹理，不是只读检查命令。

`check` 校验源与 GLB 哈希、外链、UV、法线、共享网格、233 个非目标网格的实际导出数据，并在系统临时目录重导验证一致性。旧 v1／v2、住宅与 Terrain3D 数据由开工快照复核未变。完整范围与预算记录在 manifest，未来推广不能只检查 GLB 文件大小。

## 后续

`[计划]` 负责人确认视觉与操作体验后，再安排正式采用和其他树木、房屋推广；不自动切换。当前帧时间记录包含帧同步，不代表目标硬件认证。角色、含角色截图和视频仅限本机非商业，禁止提交、推送、分发或导出游戏。
