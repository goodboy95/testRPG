# 当前实现说明

`[已实现]` 2026-10-04：正式开场的 J／Y 事项列表支持跟踪、取消和手选配送住户；阶段目标、室内出口、暂放木凳／推车及交差领款方向随进度更新。可玩文案润色和原生三档字号样板见[说明](quest-tracking-typography.md)，[实际验证](../production/quest-tracking-validation.md)与负责人字号评价分开。

`[已实现]` 2026-10-02：[正式开场](village-opening.md)已接入 Main 两种醒后路线及既有村庄档，包含七位功能 NPC、任务板、三项劳动、同行、交差领款和恢复。正式 v6；森林、支线、工具箱、第二章未开放。见[真实验收](../production/village-opening-validation.md)，下方旧批次边界保留历史。

`[已实现]` 独立样板，待视觉确认。 2026-09-26：[动画村庄 v8](village-anime-v8.md)新增主角家—广场—两铺与四棵代表树的可玩美术样板、独立资源配置及住宅外侧派生。正式和默认入口仍为 v6；其余村庄推广、目标硬件及正式采用未完成。[实际验证](../production/village-anime-v8-validation.md)。

`[已实现]` 2026-09-21：[成熟完整角色来源初筛](../production/character-mature-source-screening.md)仅交付资料与真实展示比较。八项中无合格免费资产，一项付费观察许可和造型未放行；模型下载、GLB/Godot 试验未执行，共享审图入口与正式角色未改。

`[已实现]` 2026-09-20 后续：v001 正常脸获得负责人确认并归档；[v007-casual 静态穿戴样板](../production/character-casual-preview.md)复用通用审图场景，以参数选择，不替换原入口和正式角色。

`[已实现]` 2026-09-20：[MPFB 工作流](character-workflow-mpfb.md)提供 doctor/baseline/render/check/export 五个命令和独立 `CharacterBaselineReview.tscn`。原生成年男性源可保存重开、修改恢复及重建，v001-game3 静态导入完成三分辨率 48 图检查，正式会话/存档未变。批处理通过、MCP 未接通；G1 用户批准和 G2–G5 未完成。Core/内容 219 项、编译及九场景冒烟通过；[实际结果](../production/character-workflow-mpfb-validation.md)。

`[已实现]` 2026-09-18：[沈砚静态 v2](shen-yan-static-v2.md)新增独立 `ShenYanStaticReview.tscn`，仅加载一个无骨架、无动画的静态候选用于审图。旧 `VillageNpcArtPreview.tscn`／旧 GLB／配方／生成器已退役；新的脸部与发型美术尚未批准，原低模 NPC 试点和正式 v6 保持。[实际验收](../production/shen-yan-static-v2-validation.md)。

`[已实现]` 2026-09-16：[村庄 NPC 试点](village-npc-pilot.md)新增独立 `VillageNpcPreview.tscn`，乔姨与沈砚支持闲聊分支及取消，沈砚短路线走动。正式与默认村庄仍为 v6，无 NPC 自动接入；[验收边界](../production/village-npc-pilot-validation.md)。

`[已实现]` 2026-09-16：新增[村庄流式分区与 LOD v7 独立入口](village-streaming-v7.md)，七区域加载、低模回退与运行时预算／遥测。v7 默认已提高为 GTX 1060 笔记本预算，原集显档显式回归。默认村庄版本仍为 v6；独立场景或显式 `--village-v7` 启用，无 Core／存档变化。目标硬件及整帧达标情况见[新档记录](../production/village-streaming-v7-1060-validation.md)。

> 2026-09-15 正式采用：负责人已确认将 v6（四户住宅 v5、主角家暖色外观、全村树木）与配套住宅外观启用。正式游戏和默认村庄预览现用 v6，沿用修复后的 v4 布局／地形；独立预览 `--village-v4` 保留旧外观回归。第一章委托未接入。[采用与验证](../production/village-v6-adoption.md)。下述样板轮“尚未切换”是当轮交付状态。


> 2026-09-15 本轮更新：主角家与全村树木独立样板已可走查，包含四户 v5，正式村庄继续 v4；[入口与边界](village-home-trees-v6.md)。第一章委托尚未接入。

`[已实现]` 2026-09-15：[四户住宅美术 v5](village-houses-v5.md)仅在独立入口走查，沿用已修复 v4 地形。正式游戏及默认预览继续 v4；第一章委托未接入。验证见[本轮记录](../production/village-houses-v5-validation.md)。

> 2026-09-15：[双向推门与院外接坡](home-access.md)更新门方向的可选保存、住宅缓存恢复和 v4 局部地形／路面；[实际验收](../production/home-access-validation.md)记录连续院外往返，保留旧批次的历史结果。

`[已实现]` 2026-09-14 后续布局轮：[紧凑村庄 v4](village-v4.md)已接入正式醒后探索与默认预览，采用 v3 树木／两铺外观和独立派生 Terrain3D。固定主角家、溪流、双桥及 24 栋体量；原布局通过独立预览 `--village-v2` 回归，v1 与 v3 样板入口保留。第一章委托仍未接入。[本轮实际验证](../production/village-v4-validation.md)独立记录；下述旧批次的“正式 v2／样板待采用”均为当时状态。

`[已实现]` 2026-09-14 [树木与两间铺面 v3](village-art-v3.md)可通过独立 `VillageArtPreview.tscn` 走查。正式与默认入口仍加载 v2 + 已采用 Terrain3D。该美术轮测试 184 项（182＋2 项美术导出与隔离契约），具体结果见[本轮验收](../production/village-art-v3-validation.md)。

> 2026-09-14 负责人确认采用地形：正式村庄与默认 v2 预览已使用 [Terrain3D 地形](village-terrain-sample.md)。显式 `--village-original-terrain` 保留原 v2 地面，`--village-v1` 保留 v1。原始模型与地形数据未重建；本轮结果见[正式采用验收](../production/village-terrain-adoption.md)。仅三处精修，全村其余区域为基础迁入。


`[已实现]` 2026-09-14 村庄赶路优化：正式醒后与独立 v2 预览室外为 4.2 米/秒；住宅、梦境和显式 v1 回归为 3.1，Shift 慢走 1.4。存档与模型不改，第一章委托未接入。见[优化计划](../design/village-optimization.md)与[本轮验收](../production/village-travel-validation.md)。

`[已实现]` 2026-09-14 后续接入：[梦境醒来与村庄自由探索](village-wake.md)。解谜成功／大头针刺醒后进入周六卧室，住宅和村庄可往返，位置及门灯写入正式存档；第一章委托、居民、森林和第二章尚未接入。此前建模轮的独立预览和验收记录继续保留，不代表本轮验证。


`[已实现]` 2026-09-14：村庄独立预览默认采用第一批外景 v2，新增广场办事、杂货装卸与修理铺工作空间；仅静态模型与碰撞。详情见[村庄 v2](village-v2.md)与[验收](../production/village-v2-validation.md)。正式路由、第一章接入状态、Core／存档和内容版本 6 不变。

当前 Core／内容测试为 174 项（原 164＋本轮 10），本轮回归见[记录](../production/village-wake-validation.md)。

## 交付边界

`[已实现·待人工终验]` 新游戏进入目标 60–90 分钟的“四室旧校舍梦境”序章第一阶段，用于验证跨房间谜题、道具、记录、提示、刺醒和存档。出口开启并播完最后对白后醒来，不生成现实调查线索或纸条。

`[已实现·Legacy]` 原 15–30 分钟“两日生活＋首次异象”切片仍在源码中，旧 v1 存档缺少 `DreamEscape` 字段时可继续该流程；新游戏不再进入。其中林晓雨旧友、四人重逢与三选一活动已失去新剧情权威，仅作历史和兼容回归。

`[计划]` 2026-09-13 确认：主角是一直生活在村中的高中生，父母两年前外出务工后失踪，独居且由长辈照应，课余帮忙挣钱。第一章周六先独自办事、前中期邀请沈砚，三条必要日常链完成后登记取旧工具箱；正常排查无有效方向，以疑惑收尾，可继续支线或睡觉。第二章周日早上收到下午返校通知，上午续办剩余事项。晓雨第一章现实不出场，父母与工具箱真相有意留白。 [第一章细案](../design/chapter-one.md)仅是叙事设计；新任务板、收件箱、阶段时间、结款、动物战和跨章支线尚未接入。住宅及村庄也已通过正式醒来入口接入。

`[可调整草案]` 旧 3–5 小时/七日公司与封印主线退为[历史材料](../design/story-bible-legacy.md)。Core 中已有路线、暴露和结局枚举/判定骨架继续保留，不能据此声称内容已完成或要求新故事沿用这些路线。

## 可玩流程

`[计划]` 2026-09-14 [村庄外景设计](../design/village-exterior.md)登记 24 栋建筑与重点任务空间，属于文档；村内安全、森林动物遭遇／小屋取箱、活动室邀请沈砚均未接入。东北森林出口仅为接口预留，不是新增可通行区域。

### 独立村庄漫游

`[已实现]` `scenes/testing/VillagePreview.tscn` 提供 24 栋建筑、18 户住宅、两座桥、溪岸与旧站双回环的可行走外景。主角家院门开放，正门连接复用住宅室内；地形重力/贴地仅在户外开启，门灯临时状态可往返保持与重置。公共建筑只做外部，不增加居民、任务或正式存档路由。当前默认加载第一批外景 v2；测试 164（v1 的 162＋2）。源文件、启动与实测见[村庄 v2](village-v2.md)和[验收](../production/village-v2-validation.md)，基础村庄历史见[村庄 v1](village-v1.md)。

### 独立住宅漫游

`[已实现]` `scenes/testing/HomePreview.tscn` 是主角家 3D v1 的 F6 独立入口：12×9 米平房、两卧一厅及独立厨卫，15×18 米院地与完整外观；全房间和绕屋通路可步行，五处门/六组灯、遮挡交互、防夹、暂停和重置由表现层管理。可编辑 Blend、GLB、共享布局和制作脚本见[住宅 v1](home-v1.md)。没有晓雨或剧情调查物；独立入口不访问存档；正式路由另由醒来接入维护。155 项测试（151＋4）、179 项引擎检查与三分辨率 84 张截图见[验收](../production/home-v1-validation.md)；自动检查不替代负责人视觉确认。

### 标题与新游戏

- 标题画面使用 `assets/generated/title-town-dusk.png`。
- 可开始新游戏或在存在主档/备份时继续。
- 标题页提供二次确认退出；梦境中按 Esc 打开暂停，可继续、调整显示与辅助设置、返回标题或二次确认退出。
- 新游戏允许输入姓名和称谓；空值分别回退为“小岚”和“同学”。
- 设置保留在新游戏之间，剧情状态会重置。
- 姓名确认后直接加载梦境根场景，不显示梦境/醒来/跳过提示。

### 四室梦境序章

1. 第一室使用 12×9×4 米 v6 教室和 11 个交互点。维护卡教上—下—中；7:15 开排与提前一刻钟推得 7:00；透明滑片破边对齐后读第 2 项。粉笔拓显取数顺序，372 教师桌给第二室钥匙、节目单与未署名准备清单。
2. 第二室保持 18 个交互点。谱面提供图形顺序，门后女孩提供编号，推得 1423，开门后正式互报姓名。节拍器改为装配；正面波形与晓雨第四条观测建立图形—乐器对应；按断口和孔轨拼 2413，再操作声学墙取得第三室钥匙。
3. 第三室有 13 个交互点。时段/方位排序结合索引得 C1、A4、D2、B3，给维护材料和档案剪带。集中回访教室地图柜与音乐室灯箱，C4 是满足三条件的侧台工作位，同时收齐其余剪带和录音机部件。返回档案修复并启动录音机，按票据还原开门→校准→传谱→侧台收尾，得到第四室钥匙。
4. 第四室保持 15 个交互点。舞台配重与影盘合为双视角题：正面两种候选由晓雨侧台条件排除为 3142，窗格—人物—校铃接合。三剪带依工作时间线复原母带并播放白噪声。排演后实际完成侧台就位→返听→收光→放行；关闭、进入其他模态、离房和重载清空临时分工，机关终点为 ExitOpened，播完收尾对白后进入 CompletedEscape 与现实探索。
5. 任意进行中区域可从背包对自己使用大头针，二次确认后清包、标记 `CompletedPin` 并进入现实卧室。

详细固定答案、提示与结算边界见[四室梦境密室规格](../design/dream-escape.md)。

### Legacy 第一天

1. 在旧宅醒来，选择关注车站信号或山中回声，写入不同事实。
2. 离开旧宅，在车站与林晓雨、周明、沈砚重逢；问候态度改变关系。
3. 可与乔姨交谈，得知开发公司正在打听旧隧道。
4. 三选一共同活动：河岸录音、学校旧地图、铁路怀表。每条产生不同线索/证据、关系和共同记忆。
5. 与活动伙伴建立明确的【承诺】：次日一起查看工程告示。
6. 回旧宅睡觉，进入下一日 08:00；睡眠本身不推进剧情事实。

### Legacy 第二天

1. 四名 NPC 改变位置，手册可显示已知作息和下一可见时段。
2. 与承诺对象查看“北山旧铁路安全整修与遗迹勘察工程”告示。
3. 选择核对施工线路或公司/审批信息，取得不同证据并履行承诺。
4. 主动前往废弃铁路交互点触发异象。
5. 选择【隐瞒】能力、【冒险】延长共鸣或先撤离；只有明确冒险会提高暴露。
6. 进入情境战，使用观察、保护、安抚、破坏或撤退。
7. 胜利写入异象事实、时间线、完成事件和经验；失败可重试，撤退后可再次挑战。

## 已实现系统

| 系统 | 当前行为 | 主要入口 |
| --- | --- | --- |
| 应用导航 | 标题、改名、世界、梦境暂停、设置、退出确认、完成页。 | `MainController` |
| 梦境密室 | 第一室出生、3D 洋房与四室回访、自动使用道具、独立背包/记录、加入前基于已知材料的失败独白、加入后三级局部建议、无关联资料的机关、刺醒与出口完成页。 | `DreamEscapeService`、`DreamThoughtRules`、`DreamEscapeController` |
| 世界场景 | 旧宅和车站分区；C# 原型绘制地图与 NPC。 | `Main.tscn`、`WorldSceneController`、`WorldPainter` |
| 移动/交互 | 洋房与四室均使用任意水平镜头角度下的屏幕相对 X/Z 移动和 3D 邻近交互，鼠标/右摇杆水平环绕；Legacy 小镇保持 2D。 | `PlayerController`、`PlayerController3D`、`ThirdPersonCameraRig`、`WorldArea3D` |
| 输入与指针 | 背包使用真实 I/Tab/X 且不含 Backspace；新游戏进入第一室前显示一次操作确认，读档不重复。梦境顶部只保留紧凑区域名，3D 游玩时捕获指针，Esc、模态、失焦和转场时释放，点击画面可重捕获。 | `DreamEscapeController`、`DreamInventoryPanel`、`MainController`、`ThirdPersonCameraRig` |
| 时间 | 仅实际移动推进，23:00 封顶，睡眠跨日。 | `TimeService`、`GameSession` |
| 日程 | JSON 日程、剧情事实过滤、下一可见时段。 | `ScheduleService`、`data/schedules.json` |
| 关系/承诺 | 有方向好感与信赖、四级标签、无期限承诺。 | `RelationshipService`、`PromiseService` |
| 调查手册 | 事实、证据、线索、承诺、关系、作息、时间线和最近对白。 | `JournalPanel` |
| 对话 | 历史、自动播放、仅跳已读、完整意图选项。当前为 C# 演示层。 | `DialoguePanel`、`MainController` |
| 战斗 | 单场共鸣战，五种情境行动、故事模式、重试和撤退。 | `ResonanceBattle`、`BattlePanel` |
| 存档 | 单槽 JSON、检查点、临时文件和备份恢复、版本拒绝。 | `GameSession`、`SaveGameSerializer` |
| 辅助设置 | 菜单／字幕独立字号、字幕背景与预览、文本速度、减少闪烁、震动、高对比、故事模式、1280×720/1600×900/1920×1080 窗口、无边框全屏、镜头速度和 F11 快切。 | `MainController` / `PlayerSettings` / `DisplaySettingsController` |

## 运行链路

1. Godot 加载 `Main.tscn`，之前先创建 `GameSession` 和 `Narrative` Autoload。
2. `GameSession._Ready` 加载四个 JSON catalog，验证引用并绑定 Core 服务。
3. `MainController._Ready` 创建 HUD 和模态面板，显示标题。
4. 新游戏创建 `DreamEscapeState` 并载入梦境根场景；旧存档没有该字段时载入 Legacy 世界。
5. 切片事件由 `MainController` 运行异步对白；C# 演示流程的具名结果经 `EventOutcomeService` 应用。
6. `StateChanged` 驱动 HUD、NPC 日程和手册刷新。
7. 进入异象时创建战前检查点；战斗对象只活在会话内，不写入存档。

## 内容现状

- `data/characters.json`：5 个定义，主角、林晓雨、周明、沈砚、乔姨。
- `data/schedules.json`：21 条两日和后续兜底日程。
- `data/events.json`：21 个事件（13 段序章、8 段 Legacy）。
- `data/items.json`：31 个当前梦境道具及图标引用，退役工具不再可获得。
- `dialogue/events/`：21 个对应 `.dialogue` 文件，序章由插件实际播放。
- `localization/zh_CN.csv`：角色、日程、事件和对白静态 ID 的简体中文文本。
- Core 预留 8 名核心 NPC ID；何医生、温老师、罗记者、彭师傅目前没有完整角色 catalog、个人事件和可见地图内容。

## 主要代码职责

### `src/TownRpg.Core`

- `Domain/`：存档状态、枚举、角色 ID 和内容模型。
- `Services/`：梦境密室、时间、关系、承诺、日程、事件排序、叙事命令、原子结果、路线/结局、战斗和存档。
- `Content/`：JSON catalog DTO 与严格解析选项。

### `scripts/application`

- `GameSession.cs`：唯一会话与存档 I/O。
- `ContentRepository.cs`：Godot `res://` 内容加载和启动校验。
- `NarrativeBridge.cs`：为 Dialogue Manager 准备的固定签名桥。
- `ModalGate.cs`：引用计数模态锁。

### `scripts/presentation`

- `MainController.cs`：当前最大文件，负责应用 UI、世界切换、梦境暂停/退出确认和全部切片事件演示。
- `DreamEscapeController.cs`、`DreamAreaVisual.cs`：梦境表现编排、统一运行时接口和可选瞬时表现接口；答案只由 Core 判定，预览值与晓雨机关姿态不写存档。
- `DreamAreaController3D.cs`、`DreamMusicRoomController3D.cs`、`DreamArchiveRoomController3D.cs`、`DreamAuditoriumController3D.cs` 与 `DreamMansionController3D.cs`：把四室和洋房内廊的稳定 ID、坐标与门转场接入通用 3D 层。旧后两室 2D Controller/Painter/Marker 已移除。
- `WorldArea3D.cs`、`PlayerController3D.cs`、`ThirdPersonCameraRig.cs`、`CameraRelativeMovement.cs`：可供后续梦境和未来小镇复用的 3D 区域、屏幕相对移动、跟随式透视环绕和 SpringArm 防穿模。鼠标 Orbit 在 `_Input` 中且只响应捕获状态；旧 `QuarterTurnCameraRig` 仍保留作实验对照。
- `InteractionMarker3D.cs`、`StatefulObjectVisual3D.cs`、`AreaCoordinateMapper3D.cs`：3D 交互光环、状态变体和二维/三维坐标转换。旧 `WallCutawayController` 与着色器不再由第一室加载。
- `DreamClassroomVisual3D.cs`：第一室 v6 GLB 的机关状态绑定、24 组独立碰撞、11.4×8.4 米活动区和旧坐标安全落点。原图、TileMap、v2/v3 视觉资产继续保留作对照。
- `DreamMansionVisual3D.cs`：洋房内廊 GLB、独立碰撞、房门铰链和安全落点；完整外观 GLB 是离线资产，不由场景引用。
- `DreamMusicVisual3D.cs`、`DreamCompanion3D.cs`：12×10×4 米音乐教室 v2 GLB、31 个碰撞定义（开关门扇互斥，家具 16 个）和 18 个交互目标；音乐室启用连续绕行与校准往返，后两室也已启用连续路径。
- `DreamLateRoomVisual3D.cs`、`DreamArchiveVisual3D.cs`、`DreamAuditoriumVisual3D.cs`：14×12×4.2 米档案室 v2 与 22×16×7.5 米礼堂 v2，20／56 组碰撞、13／15 标记、实体安装和瞬时预览。`ArchiveRoomLayout3D`、`AuditoriumLayout3D` 统一生产、碰撞、标记、门轴、安全点与晓雨路径；`LateRoomLayout` 和 `GroundWaypointNavigator3D` 提供恢复及连续绕行。礼堂 32 座共享网格，抬高舞台为景观。
- `DreamAreaSpawnCatalog.cs`：按来源与目标区域解析洋房与四室门内安全点；全部为 3D→3D，临时镜头入口姿态不进入存档。
- `DisplaySettingsController.cs`：三档 16:9 窗口预设、显示器工作区降级、无边框全屏切换、窗口尺寸恢复和居中。
- `DreamCharacterFrames.cs`：将 4×4 角色表转换为 2D/3D 共用的 `SpriteFrames`。
- `DreamInventoryPanel.cs`、`DreamPuzzlePanel.cs`：分页背包/记录/晓雨提示，以及支持数字直输和逐项撤销的通用排列输入。
- `WorldSceneController.cs`：NPC 日程、交互目标、提示和地图事件入口。
- `DialoguePanel.cs`、`JournalPanel.cs`、`BattlePanel.cs`：对应模态 UI。
- `PlayerController.cs`、`NpcActor.cs`、`WorldPainter.cs`：移动、可选角色精灵动画和原型世界表现。

## 调试入口

- `F3`：显示/隐藏调试状态，含位置、事实数、证据数、暴露和等级。
- 暂停菜单“脱离卡住”：移动到当前场景安全点，不改变剧情。
- 存档位于 Godot `user://save-slot-1.json`，具体绝对路径可在引擎中用 `ProjectSettings.GlobalizePath` 查看。
- 初始化内容错误会记录在 `GameSession.InitializationError` 并输出到 Godot 日志。
- 引擎冒烟入口：`res://scenes/testing/GodotSmokeRunner.tscn`。

## 已完成验证

截至 2026-09-07：Core、对白、机关面板与场景冒烟为本轮复验；下列美术复导和性能数字保留为历史记录。

- Core/内容/移动 125/125 通过；增加的 11 项覆盖分组、双视角唯一解、旧档兼容、隔门观察、退役拒绝、剧情幂等和本地化/事件内容契约。既有完整链、错误、重复、安装和回访测试已迁移到 v6。
- 根 Godot 项目 Debug 零警告构建通过；本轮未执行发布导出。
- 主场景、Legacy 两图、梦境根、3D 洋房公共区与四室共九场景实例化通过。
- 内容仓库报告 `characters=5 schedules=21 events=21 items=31`。
- 教室 v4 和洋房内廊 v1 继续通过原有只读复导，作为历史运行时基线；洋房外观 v1 仍是离线验收与未来复用资产，当前关卡不加载。
- `[已实现·历史 v5 基线]` 下述教室 v5 数据已由 v6 重制替代；洋房 v2 也已由大厅 v3 替代，下述统计保留为历史。教室为 114,452 三角面、244 个总网格/178 个最大同时可见网格、14 个材质和 56,502,720 字节；洋房为 88,244 三角面、198 个可见网格、12 个材质和 37,793,392 字节。两份权威 Blend 缺失外链为 0，临时复导哈希一致，均低于 60 MiB 与既定几何预算。
- 历史 v5 教室补齐过 18 组共享课桌五金、非对称书纸、窗扣/窗帘细木作、广播与开关、教室门、地图柜和教师桌状态细节；洋房已补齐七组门细木作和正确铰链原点、全廊护墙板、六组壁灯、壁炉工具、玄关家具、梁架金属箍、画框及两个扩展实墙模数。
- 历史 v5 捕获使用 Filmic、局部冷暖实时灯和每区域两个 ReflectionProbe；没有使用 LightmapGI。代表 1080p 捕获的 `p90` 约 0.25–0.29，较初次完整导入的 0.12–0.16 显著提高且无过曝白斑。捕获设备为 RTX 2080 Ti，仅验证 Compatibility 加载和视觉链路，不代表 GTX 1660 级性能通过。
- 3D 冒烟覆盖正式方案 A、第一室/洋房坐标往返和门口出生；第一室检查 v6 PBR、完整细节节点、两个 ReflectionProbe、11 个唯一可达交互点、独立窗扣线索、24 组碰撞、12 组课桌、七类机关状态和双向门动画；洋房检查 v3 PBR、完整细节节点、七个门枢轴、两个 ReflectionProbe 和门动画。
- 第二室专项冒烟检查 Orbit/SpringArm、15 个唯一可达交互点、16 组碰撞、两个 ReflectionProbe、西南门安全出生和受控门动画；并覆盖器材间关闭/打开、五件拾取物、两种安装状态、三层映射、声学墙/点名装置/琴凳灯态、晓雨加入前位置、加入后跟随与超距回位，以及四个离线 WAV 和非听觉文字替代。
- 本轮 Debug 冒烟还验证了紧凑区域名且无任务栏、普通道具无选用按钮、新游戏操作确认/直接载入不重复、黑板自动用粉笔、时钟一次装入两部件、录音机一次装入三部件并启动；同时继续覆盖真实 I/Tab/X、Backspace 排除、Esc 暂停释放指针和退出确认唯一性。
- Windows 实机短程验证已覆盖 Tab 打开/关闭背包、Esc 打开暂停、继续游戏、打开/取消退出确认及最终正常退出。自动化工具无法产生与真实设备等价的捕获态 raw mouse delta，因此物理鼠标环绕仍需人工复验；Xbox 和中低端集显同样未验收。
- 可自动输出四个典型环绕方向、初始/黑板/教师桌/地图柜状态、碰撞调试，以及门/黑板/课桌近景共十五张原生截图，支持 1280×720、1600×900 与 1920×1080。
- 历史版本 RTX 2080 Ti 的 Compatibility 自动采样在 1280×720、1600×900 与原生无边框 1920×1080 均约为 60 FPS，最低瞬时 59.1/59.5/59.0 FPS，最大环绕帧时 16.92/16.80/16.94 ms，第一室加载 354.67/354.92/371.22 ms。中低端集显与完整人工路径仍未验收。

## 已知缺口

- `[已实现]` Dialogue Manager v3.10.4 已 vendored，序章 13 段由插件解析播放；Legacy 8 段仍保留旧演示编排。
- 当前 `NarrativeBridge.Execute` 对单条命令做白名单写入，但没有把一段 `.dialogue` 的多条命令组成原子 outcome；插件接入时必须迁移，不能直接视为完成的事务桥。
- `MainController` 同时负责导航和剧情演示，后续内容扩展前应完成对白接入与职责拆分。
- 洋房与四室仍为 3D 自由镜头；当前采用 v6 规则和新稳定 ID，三剪带在礼堂前收齐，取消礼堂后的专程取物。关卡时长、全程键鼠/真实 Xbox 和硬件性能仍需新版人工验收。
- 环境全场景扩展已完成。当前工作副本以本机非商业 `mc_model` 补上 3D 区域角色表现，但该资产禁止再分发并从 Git/Windows 导出排除；干净检出和发布包仍回退到 2D billboard，正式可分发 3D 主角仍是已知缺口。英雄物件可继续独立重拓扑，但不得改变碰撞、交互锚点或稳定 ID。
- 半写实 3D 主角的 v1 技术样模验证了 Blender 官方 Human Base Meshes v1.4.1 CC0 男性人体、1.694 米尺度、闭合独立服装/鞋/发帽网格与静态 GLB 管线，但仍明确不是视觉候选。原始人物方向图已提升为正式身份锚点，并新增同一人物的脸发和服装细节板；原画式 v2 已生成静态 GLB/manifest 并通过只读复导出检查，当前指标为 `1.7017m`、`34972` 三角面、6 个可见网格、1 个材质面、无 Armature/Action，manifest 明确为 `runtimeReady=false`。独立 Godot 捕获没有替换正式可分发基线，且发现方案 A 默认构图为 `36.47%`、后侧受墙体压缩后约为 `49.25%–49.49%`，原定 `28%–31%` 仍未满足；这是需要在视觉确认后处理的 3D 镜头口径与 SpringArm 构图问题，不是缩小人物世界尺寸的理由。v2 仍等待负责人视觉确认，v1/v2 均未绑定、无动画、未进入 Godot；本机 `mc_model` 默认替换是独立的非商业运行时覆盖，不代表 v2 获批或固定外观被改写。第二室已有离线自制音频；2026-09-12 起本机晓雨使用流萤 PMX 派生的 3D 模型与双角色动作 v3，缺失时回退 billboard，正式可分发晓雨模型仍未制作。
- 名称输入的完整 Windows IME、按键重映射、真实物理鼠标 raw delta、手柄震动和所有辅助设置尚未做实机闭环验证。
- 梦境 60–90 分钟完整路径、键盘/Xbox、150% 字号和门转场尚未完成人工闭环；720p/1080p 自动截图与当前独显性能已验收，Legacy 两日流程仍需旧存档回归。
- Windows 导出模板缺失，未生成可分发 ZIP。
- 导出 preset 会排除 `doc/`、`tests/`、`work/` 和 `outputs/`；但实际导出尚未验证。

这些缺口的处理顺序见[交接路线图](../production/roadmap-handoff.md)。

`[已实现]` 当前第一室 v6 重制与三档分辨率专项检查见[建模文档](classroom-remodel-v6.md)和[验收记录](../production/classroom-v6-validation.md)。精确资产统计以 v6 manifest 为准；该次新增内容检查后测试总数为 127，未扩展剧情及内容版本。

`[已实现]` 大厅 v3 已替换为 10×8×6.6 米前厅与 7×12×4.2 米内廊，加入整体禁入的折返楼梯景观。25 组碰撞与 8 个标记共用 `MansionLayout3D`；旧档位置按复合区域修正，晓雨接入静态障碍绕行。当时 128 项测试、四室各两次往返与三档分辨率检查见[建模说明](manor-remodel-v3.md)和[验收记录](../production/manor-v3-validation.md)。

`[已实现]` 09-09 解谜反馈重写：19 个题面、57 条晓雨求助和 214 条情境独白；部分材料可回想已见部分，安装/消耗后仍保留知识，重复失败不升级为答案。机关关联资料已移除，独立记录与必要合作观察保留。当时 134 项测试和 180 张矩阵截图见[角色提示](puzzle-feedback.md)与[本轮验收](../production/dream-hints-validation.md)。

`[已实现]` 音乐室 v2 的独立 Blender/GLB、分阶段门碰撞、校准实物、旧档与晓雨路径详见[实现](music-remodel-v2.md)。当时 135 项测试（134＋1 项实际 GLB 检查）及三分辨率/字号专项见[验收](../production/music-v2-validation.md)。

`[已实现]` 电影感 UI 第一阶段样板已接入对白、探索、背包与记录，含独立字幕设置。该阶段 143 项测试（135＋5 项设置兼容＋3 项分页）；后两室 v2 增至 145 项，样板视觉确认及其余页面统一仍待完成，见[说明](ui-cinematic-samples.md)和[验收](../production/ui-cinematic-validation.md)。

截至 2026-09-12：`[已实现·仅本机非商业]` 双角色动作 v3 覆盖五区，林晓雨使用 1.6 米流萤外观；两人各 12 段动画，加入按实际速度调频的平滑跟随、到位后协作、模态呼吸与交互姿态。151 项测试、九场景冒烟、五区专项及完整输入通关结果见[双角色动作](character-motion.md)与[验收](../production/character-motion-v3-validation.md)。

`[已实现·静态试验]` [头肩修订](../production/character-head-refinement.md)新增可开关闭唇纠正、局部UV皮肤处理、真实曲线发束及后脑/耳后捕获。v014 脸部改善可保留，v016 头发美术失败，阶段停止。正式入口、NPC与存档未改变。
