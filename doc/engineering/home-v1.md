# 主角家 3D v1：独立住宅漫游

> 2026-09-15 正式采用：负责人已确认将 v6（四户住宅 v5、主角家暖色外观、全村树木）与配套住宅外观启用。正式游戏和默认村庄预览现用 v6，沿用修复后的 v4 布局／地形；独立预览 `--village-v4` 保留旧外观回归。第一章委托未接入。[采用与验证](../production/village-v6-adoption.md)。下述样板轮“尚未切换”是当轮交付状态。


> 2026-09-15 本轮更新：主角家暖色外观派生独立版本，原 v1 保留，室内和双向推门规则不变；仅新村庄样板选用匹配住宅外观，详见[v6 制作](village-home-trees-v6.md)。

> 2026-09-14 后续更新：[醒来与正式村庄探索](village-wake.md)已接入。两条梦境路线均可进入住宅和村庄，并保存位置与门灯；第一章委托、居民与第二章仍未接入。本文原制作／设计轮的“独立预览、止于 ExitOpened、仅文档、存档不变”等范围描述保留为当轮事实，由该后续接入说明补充现状。


更新：2026-09-12。住宅模型版本 v1 与序章内容版本 6 分开管理。

## 场景边界

`[已实现]` 近现代乡村自建平房，主体 12×9 米，室内净高 3.2 米，墙厚 0.2 米；两卧一厅、独立厨房与卫生间，完整屋顶和四面外观。15×18 米地块包含 1.5 米门廊、6 米深前院与侧后维护通路。家人卧室也可进入。

`[已实现]` 独立入口为 `scenes/testing/HomePreview.tscn`。启动后在主角卧室床边控制现有主角，支持五处房门、六组灯、暂停与重置。没有晓雨、家人 NPC、睡眠、物品拾取、剧情检视或村庄转场。院门是封闭景观。

`[固定约束]` 本轮是用户批准的住宅模型和独立漫游，不把旧叙事文档中“仅文档修改”的历史范围扩写成已实现第一章。正式标题、新游戏、Legacy `old_house`、梦境结算、稳定剧情 ID、存档 DTO 和内容版本 6 均保持原状。醒来、出门事务及现实章节尚未接入，当前设计见[第一章细案](../design/chapter-one.md)。

## 布局与状态所有权

- `HomeLayout3D.Data` 是嵌入 C# 的共享布局 JSON，坐标单位为米，Godot `+Z` 为南。它提供建筑尺寸、墙段和真实开口、结构盒、家具占地、门轴、开门方向、灯位、开关、安全点及出生点；Blender 脚本读取同一份数据，Godot 从中创建碰撞与交互。
- 中部起居厅净宽 3.6 米；两间卧室约 3.8×4.2 米；厨房约 3.8×5.2 米；卫生间约 3.8×3.2 米。四间侧房独立连接起居厅；主入口净宽 1.6 米，室内门净宽 1.2 米。茶几与电视柜之间留约 1.6 米主通道；前院盆栽避开两侧绕屋通路。
- `HomePreviewController3D` 继承通用 `WorldArea3D`，复用 `PlayerController3D`、`ThirdPersonCameraRig` 与 `InteractionMarker3D`。主角保持现有慢跑、Shift 慢走、摇杆连续调速及本机角色回退，镜头默认臂长 2.6 米，使用完整墙顶和 SpringArm 碰撞，不裁墙。
- 窗洞保留自然光穿透，玻璃另设仅供镜头检测的碰撞层 2，SpringArm mask=3；角色和交互视线 mask=1。贴窗压缩至近镜头阈值时沿用主角隐藏机制，离开窗边后恢复。围墙外 80×80 米背景地面仅用于外观取景，没有碰撞或可游玩范围扩展。
- `HomeDoorMotion` 是纯表现层规则，不属于 Core 或 DTO。每个场景实例独立保存门的目标、进度与开关灯状态；重置/重载回到闭门、关灯和卧室出生点。主入口左右门叶同步反向旋转。
- 2026-09-15 更新：门在 0.45 秒内向玩家对侧转动，关闭沿原方向返回。以实际门叶／胶囊检查运动，受阻停下、让开继续，实体碰撞跟随门叶，不再使用整片矩形拒绝或临时扫掠碰撞。重复输入、暂停和转场不重复操作，不推走或瞬移玩家。正式模式保存稳定方向，见[双向推门](home-access.md)。
- `home.door.*`、`home.light.*` 为预览交互 ID。射线检查从玩家眼高到门/开关；只排除玩家与当前目标自己的门碰撞，墙和其他家具照常阻挡。交互半径 1.65 米，所有提示从 `HOME_*` 本地化键读取。
- 暂停时阻断角色与镜头、释放指针；恢复时释放菜单焦点，避免手柄 A 被旧按钮接收。窗口失焦沿用角色与镜头已有复位逻辑。控制器不调用 GameSession 状态变更或存档接口。

## 模型与渲染

`[已实现]` 可编辑源为 `art/blender/home/home-v1.blend`，Godot 镜像为 `assets/generated/home/3d/home-v1.glb`；同目录 manifest 标记为 `independent-preview`、`runtimeDefault=false`，不能据此声称已经进入正式剧情。

- 浅色抹灰、局部旧瓷砖墙裙、铝窗、深灰瓦屋顶和水泥院地；室内包含床框/床品、书桌与书籍背包、柜体、沙发电视、餐桌椅、厨具、卫浴洗衣设施。门框、门叶、把手、开关、窗台、雨水管等有独立结构。
- 使用项目已采用的 Poly Haven CC0 `oak_veneer_01`、`painted_plaster_wall`、`terrazzo_tiles` PBR 图。新增几何由离线 Blender Python 制作，没有新增生成图、付费资产、依赖或运行时生成服务。来源、工具版本、制作说明、参数、后处理与 SHA-256 记录在 manifest 和 `assets/generated/provenance.json`。
- 室内地砖采用中性灰底色，复用 terrazzo 的 normal/ORM（未采用其棕色 albedo），增加实际灰缝几何；木材和抹灰保留原扫描图。屋瓦法线从实际 GLB 二进制检查朝外朝上。
- 原始材质图打包进 Blend 和 GLB；运行时仅加载 GLB 与 Godot 派生资源。重复餐椅与书桌椅共享网格，南北瓦面共享同一瓦片网格。导出不包含相机、灯光、角色骨架或动画。
- 预算上限为 180,000 三角面、220 可见网格、24 材质、60 MiB GLB、256 MiB 纹理内存估算；当前实际值以 manifest 和[验收记录](../production/home-v1-validation.md)为准。
- 固定清晨侧向日光，Color 环境光、局部反射探针和六组可切换实际灯光。Compatibility/OpenGL 下不依赖 SSAO、LightmapGI 或屏幕空间反射。默认关灯也保留通路可读性。

## 制作与启动

在项目根目录用 PowerShell 执行：

```powershell
& 'D:\ProgramFiles\Blender\blender.exe' --background --python-exit-code 1 --python scripts/tools/build_home_v1.py -- check --root .
& 'C:\Program Files\dotnet\dotnet.exe' build TownRpg.csproj --configuration Debug --no-restore
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/HomePreview.tscn
```

- `graybox`：输出到 `work/home-v1-graybox/`，不覆盖正式 Blend/GLB。引擎加 `-- --home-graybox` 时用同布局的可见碰撞灰盒检查通路，无需导入工作目录。
- `build`：重建正式 v1 源、GLB 和 manifest，会覆盖本版本程序生成的源；人工编辑后应使用 `export` 从当前 Blend 导出。
- `export`：打开现有 Blend 后导出，不重新建模；适合保留人工修改。
- `check`：只读打开源，在临时目录重新导出并比较哈希、实际 GLB 节点、UV、共享网格和预算，不改写工作副本。
- `C:\Python314\python.exe scripts/tools/build_home_review.py plan`：从共享布局重绘平面图。

`[固定约束]` 本机 mc_model 与含角色的画面继续仅限本机非商业，不得提交、推送、分发或导出。住宅环境不包含角色；缺失本机模型时仍使用现有 billboard。人工视觉终验、真实 Xbox/raw delta、目标硬件与正式导出见验收记录中的未完成项。
