# 主角家辨识与全村树木 v6

> 2026-09-15 正式采用：负责人已确认将 v6（四户住宅 v5、主角家暖色外观、全村树木）与配套住宅外观启用。正式游戏和默认村庄预览现用 v6，沿用修复后的 v4 布局／地形；独立预览 `--village-v4` 保留旧外观回归。第一章委托未接入。[采用与验证](../production/village-v6-adoption.md)。下述样板轮“尚未切换”是当轮交付状态。


2026-09-15。`[已实现]` 独立样板包含四户住宅 v5、主角家暖色外观和全村树木，可独立走查。正式游戏／默认预览保持修复院外接坡后的 v4，第一章委托未接入。实际检查与人工确认边界见[本轮验收](../production/village-home-trees-v6-validation.md)。

## 外观与入口

主角家保留原建筑、门廊、院地与开口：暖米白外墙、灰瓦、低饱和赭红敞开院门、木色檐边与柱脚。旧长凳、门廊侧的静态自行车和靠墙收纳提供生活辨识；盆栽沿用占地，改变容器材料。没有父母线索、住户新增背景或骑行功能。

广场大树与 Tree_037／043 保留；55 棵普通树替换为宽展、直立、疏冠三种共享网格。树干变换和碰撞保持，主枝几何搭配现有枝叶图集，Alpha Scissor 0.45、双面、mipmap；仅三个新树冠网格按导入后的资源名启用简化层级，其他村庄网格保持原细节；最高细节面数仍纳入预算。配置依据 [Godot 4.6 场景导入实现](https://github.com/godotengine/godot/blob/4.6-stable/editor/import/3d/resource_importer_scene.cpp)，并用实际导入资源检查层级存在。

```powershell
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageHomeTreesPreview.tscn
```

独立控制器同时选择村庄 v6 与住宅外观 v2，复用 v4 布局及 Terrain3D。正式控制器固定采用 v6 并忽略预览回归开关；默认村庄预览采用 v6，`--village-v4` 回归旧外观，原 v1～v5 和住宅预览入口保持。室外 4.2 米/秒、住宅 3.1、Shift 慢走 1.4；不新增操作或存档。

## 源、材质与编辑

- [村庄 Blender](../../art/blender/village/village-v6.blend)、[GLB](../../assets/generated/village/3d/village-v6.glb)、[清单](../../assets/generated/village/3d/village-v6-manifest.json)。从保存的 v5 派生，包含其四户完整外观。
- [住宅 Blender](../../art/blender/home/home-identity-v2.blend)、[GLB](../../assets/generated/home/3d/home-identity-v2.glb)、[清单](../../assets/generated/home/3d/home-identity-v2.json)。从保存的住宅 v1 派生，室内墙面、家具、门轴与灯光不变；只隔离外侧墙面材料和外景陈列。
- [共同外观／碰撞定义](../../scripts/presentation/HomeIdentityLayout3D.cs)使用住宅局部米制坐标，摆设位置是底部。两个实例使用相同陈列几何和简化碰撞；中央 2.8 米通路保持。
- [来源与加工记录](../../art/blender/village/v6-provenance.json)引用已采用 CC0 树皮、枝叶、抹灰、木材、砖瓦和石材。没有新下载、购买或图像生成。复制源中重复的同内容图像合并，室内原图分辨率不变。

```powershell
# 人工编辑、保存两个 Blender 源后，从保存源导出两份资源。
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v6.py -- export
# 只读打开，临时目录复导；不覆盖编辑成果。
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_v6.py -- check
```

`init` 仅用于第一次从保存基线派生；已有源时拒绝执行。`init-home` 仅用于村庄已保存而住宅派生尚未创建时恢复初始化，不覆盖住宅源。禁止运行旧构建器重建基线。材质／摆设修改应同步两份保存源；临时修正脚本在本轮输出目录留作过程记录，不重复执行。

预算包含地形：120 万三角面、600 可见网格、64 材质、100 MiB GLB、384 MiB 纹理估算。住宅单场景另保留 18 万三角面、220 网格、24 材质、256 MiB 纹理上限；缓存后的实际内存另测，不以两份 GLB 大小推算显存。

## 接入边界

新增版本选择与摆设定义仅属于表现层，无公共游戏 API、Core 类型、存档字段、稳定剧情 ID 或内容版本变化。样板不写真实存档，测试恢复使用隔离快照。双向推门、稳定开启方向、门灯缓存、暂停、角色 billboard 回退与接坡修复继续复用。

`[计划]` 负责人确认家的辨识度、树木外观及手感后，再安排正式采用。第一章、其余建筑、森林和 NPC 另行制作。本机角色及含角色媒体不可提交、推送、分发或导出游戏。
