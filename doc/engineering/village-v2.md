# 村庄 v2：第一批外景与独立漫游

`[已实现]` 2026-09-14 后续布局轮：[紧凑村庄 v4](village-v4.md)已接入正式醒后探索与默认预览，采用 v3 树木／两铺外观和独立派生 Terrain3D。固定主角家、溪流、双桥及 24 栋体量；原布局通过独立预览 `--village-v2` 回归，v1 与 v3 样板入口保留。第一章委托仍未接入。[本轮实际验证](../production/village-v4-validation.md)独立记录；下述旧批次的“正式 v2／样板待采用”均为当时状态。

`[已实现]` 后续 [v3 树木与两铺样板](village-art-v3.md)从本版保存源派生，尚未替换正式与默认入口。本版源、GLB 和历史验收保留，样板结果见其独立记录。

> 2026-09-14 负责人确认采用地形：正式村庄与默认 v2 预览已使用 [Terrain3D 地形](village-terrain-sample.md)。显式 `--village-original-terrain` 保留原 v2 地面，`--village-v1` 保留 v1。原始模型与地形数据未重建；本轮结果见[正式采用验收](../production/village-terrain-adoption.md)。仅三处精修，全村其余区域为基础迁入。


> 2026-09-14 后续调速：v2 正式／独立预览室外速度为 4.2 米/秒，住宅和显式 v1 回归仍为 3.1。本文建模资产、几何和历史验收保持原状；见[赶路验收](../production/village-travel-validation.md)和[后续地形／材质计划](../design/village-optimization.md)。

> 2026-09-14 后续更新：[醒来与正式村庄探索](village-wake.md)已接入。两条梦境路线均可进入住宅和村庄，并保存位置与门灯；第一章委托、居民与第二章仍未接入。本文原制作／设计轮的“独立预览、止于 ExitOpened、仅文档、存档不变”等范围描述保留为当轮事实，由该后续接入说明补充现状。


更新：2026-09-14。本版本落实[外景设计](../design/village-exterior.md)的 V01–V03；详细检查见[验收记录](../production/village-v2-validation.md)。资产版本 v2 与剧情内容版本 6 分开管理。

## 当前内容与入口

`[已实现]` 中心广场增加村长办事桌椅、纸笔柜、登记簿、防雨收件箱与局部铺地，公告纸为独立物件。乔姨杂货铺增加交货台、小件包裹、垫高米面、空筐和手推车，调整棚架、冰柜与长凳。彭师傅修理铺增加有厚度的坡顶雨棚、工作台、台钳、挂板、刨子／手锯／锤子／螺丝刀、分类柜、木料与两把木凳。

`[固定约束]` 这是静态外景与碰撞，没有新增任务、居民、对白、购物、拾取、推车操作、开箱或日程。棚柱、家具和车体提供简化碰撞；公告纸、货包和工具可供后续替换，但没有任务状态。章末工具箱不在模型中。任务板仍不能接取委托，第一章、第二章与森林均未接入。

默认独立入口仍是 `scenes/testing/VillagePreview.tscn`，现在加载 v2。加 `--village-v1` 回到保存的 v1 外景；加 `--village-graybox` 显示当前版本的碰撞灰模，两者可组合查看 v1 基线。正式 F5、新游戏、Legacy、梦境结算、Core、正式存档及内容版本 6 不变。

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build TownRpg.csproj --configuration Debug --no-restore
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/VillagePreview.tscn
# 显式旧版回归：
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/VillagePreview.tscn -- --village-v1
```

沿用 WASD／左摇杆、Shift 慢走、鼠标／右摇杆环绕与 F／A 进出自家正门。暂停、重置、住宅门灯临时状态及角色缺失时的 billboard 回退不变。

## 空间与状态所有权

`[已实现]` [基础布局](../../scripts/presentation/VillageLayout3D.cs)保持原文件不变；新增[第一批外景布局](../../scripts/presentation/VillageExteriorLayout3D.cs)提供 25 项摆设／棚顶定义、五处净空和五条接近路线。尺寸单位为米、Godot +Z 为南，位置为物件底部。

| 空间 | 实际布局要求 |
| --- | --- |
| 公告牌前 | 3×2.4 米读牌区；保留公告牌与大树的位置 |
| 办事桌前 | 2.4×2.4 米访客净空，约 4×4 米办事区域 |
| 杂货铺 | 3×3 米装卸净空、约 2.8 米中央通道；推车位于原回环道路外，长凳移到门前侧方 |
| 修理铺 | 台前 2.4×2.4 米合作净空；工作台与待修物置于中央进出路线两侧 |
| 雨棚 | 下沿至少 2.6 米，棚顶与立柱分别提供简化碰撞；保留夏日上午光照 |

`VillageExterior3D` 根据版本选择移除旧公告牌、长凳与箱筐碰撞，再加载对应新定义，避免旧位置留下隐形障碍。基础道路、建筑体量与位置、桥、水岸、地形以及住宅转场不修改。全部新增内容属于表现层，预览没有调用任务或存档接口。每帧入口射线查询及时释放；预览控制器在住宅／村庄往返期间持有角色 PackedScene，避免缓存包装对象过早回收。验收结束先释放预览，再等待托管终结器完成，最后退出引擎。

灰模发现推车初始位置阻挡旧站回环，已移出原路线；长凳原候选位置与店壳相交，已调整。棚顶从单面面片修正为有厚度构件；办事区新铺地避开既有地面的重叠闪烁。具体复跑结果以本轮验收记录为准。

## Blender 源、导出与复验

`[已实现]` 可编辑源为 [village-v2.blend](../../art/blender/village/village-v2.blend)，运行镜像为 [village-v2.glb](../../assets/generated/village/3d/village-v2.glb)，同目录保存 manifest。新构建器从**已保存**的 v1 打开派生，只替换两间铺面与三组原地标几何，其他对象保持原状；不调用旧版 `build`。v1 与住宅 v1 源及运行资源继续保留。

```powershell
$VillageBlender = 'D:\ProgramFiles\Blender\blender.exe'
# 首次生成／明确要求重建 v2 时使用；会重建 v2，不能用于保留后续手工修改。
& $VillageBlender --background --python-exit-code 1 --python scripts/tools/build_village_v2.py -- build --root .
# 保存手工修改后导出：
& $VillageBlender --background --python-exit-code 1 --python scripts/tools/build_village_v2.py -- export --root .
# 只读检查，在临时目录重新导出：
& $VillageBlender --background --python-exit-code 1 --python scripts/tools/build_village_v2.py -- check --root .
# 灰模只写工作目录：
& $VillageBlender --background --python-exit-code 1 --python scripts/tools/build_village_v2.py -- graybox --root .
```

`graybox` 仅写 `work/village-v2-graybox/`。`export` 不改写 Blend；`check` 验证源、v1／住宅源、布局哈希、实际 UV／法线／地形顶点与确定性复导。还逐项比较 202 个未替换网格的实际 glTF 顶点、索引、UV 和材质引用，验证源中保留的 229 个对象。改动批准范围之外的源对象会使复验失败。

`[已实现]` 复用此前采用的 Poly Haven CC0 木材、抹灰、石材纹理，新增普通颜色材质与浅色胶合板材质。木器边缘使用小倒角，袋装物增加缝线；没有购买资产、调用图像生成模型或引入运行时服务。来源、Blender 5.2.0、制作脚本、参数与哈希见 manifest 和全局 provenance。素材引用的旧截图只作本机参考。

Godot 导入继续使用 `gltf/embedded_image_handling=1`，保留派生贴图，关闭自动 LOD，避免村庄地面被简化后互穿。源、GLB 与纹理遵守现有 LFS 规则。整村预算保持 120 万三角面、600 可见网格、64 材质、100 MiB GLB 与 384 MiB 纹理估算。

## 后续制作

`[计划]` 第二批为活动室、配送／禽栏院落与溪边休息处；第三批为其他建筑和环境收边。森林、工具箱小屋、NPC、动物及实际任务制作仍另行安排。

`[固定约束]` 负责人视觉确认、真实输入设备、目标硬件与游戏导出状态单独记录。自动走查不代替人工视觉评价；本机角色及含角色截图仍禁止提交、推送、分发和导出。
