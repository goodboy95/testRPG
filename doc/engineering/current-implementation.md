# 当前实现说明

## 交付边界

`[已实现]` 新游戏进入目标 30–45 分钟的“四室旧校舍梦境”序章第一阶段，用于验证跨房间谜题、道具、记录、提示、刺醒和存档。出口开启后停在机制完成页，不生成正式重要信息或纸条。

`[已实现·Legacy]` 原 15–30 分钟“两日生活＋首次异象”切片仍在源码中，旧 v1 存档缺少 `DreamEscape` 字段时可继续该流程；新游戏不再进入。其中林晓雨旧友与四人重逢内容已失去剧情权威，等待重写。

`[计划]` 完整 3–5 小时版本的五个居民、完整小镇、公司设施、谨慎/救援路线、恋爱确认、终战和结局尚未成为可游玩内容。Core 中已有部分枚举和判定骨架，但这不等于内容已经完成。

## 可玩流程

### 标题与新游戏

- 标题画面使用 `assets/generated/title-town-dusk.png`。
- 可开始新游戏或在存在主档/备份时继续。
- 标题页提供二次确认退出；梦境中按 Esc 打开暂停，可继续、调整显示与辅助设置、返回标题或二次确认退出。
- 新游戏允许输入姓名和称谓；空值分别回退为“小岚”和“同学”。
- 设置保留在新游戏之间，剧情状态会重置。
- 姓名确认后直接加载梦境根场景，不显示梦境/醒来/跳过提示。

### 四室梦境序章

1. 新游戏出生在第一室前部的安全开阔区；当前运行时以自由水平环绕、固定俯角的透视第三人称表现精修 v2 教室，正式镜头采用方案 A（FOV 60°、21°俯角、3.4 米 SpringArm、1.30 米跟随高度、0.22 米肩偏），默认朝向黑板。出生点不会自动聚焦机关，镜头可完整伸展；主角暂以二维四向精灵 billboard 行走。自由镜头可见的四墙、关闭教室门、门窗五金和课桌近景已补齐，尚待负责人视觉终验。粉笔显出观察顺序，分别调查窗、时钟和值日表后以 `372` 取得第二室钥匙，地图柜留待回访。
2. 第二室输入 `1—4—2—3` 救出初次见面的林晓雨，完成三处辨听后开放第三室。
3. 第三室档案顺序取得维护钥匙与红尺；依次回第一、第二、第三室取得并修复录音机。
4. 广播序列开放第四室；三块控制板复现旧答案，晓雨与主角同时操作机关后开启出口。
5. 任意进行中区域可从背包对自己使用大头针，二次确认后清包、标记 `CompletedPin` 并揭示梦境。

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
| 梦境密室 | 第一室出生、走廊与四室回访、分页背包/记录、晓雨加入后三级提示、通用谜题、刺醒、出口完成页。 | `DreamEscapeService`、`DreamEscapeController` |
| 世界场景 | 旧宅和车站分区；C# 原型绘制地图与 NPC。 | `Main.tscn`、`WorldSceneController`、`WorldPainter` |
| 移动/交互 | 2D 区域保持 8 方向移动；第一室使用任意水平镜头角度下的屏幕相对 X/Z 移动和 3D 邻近交互，鼠标/右摇杆水平环绕。 | `PlayerController`、`PlayerController3D`、`ThirdPersonCameraRig`、`WorldArea3D` |
| 输入与指针 | 背包使用真实 I/Tab/X 且不含 Backspace；非交互 HUD 忽略鼠标。3D 游玩时捕获指针，Esc、模态、失焦和转场时释放，点击画面可重捕获。 | `DreamEscapeController`、`DreamInventoryPanel`、`MainController`、`ThirdPersonCameraRig` |
| 时间 | 仅实际移动推进，23:00 封顶，睡眠跨日。 | `TimeService`、`GameSession` |
| 日程 | JSON 日程、剧情事实过滤、下一可见时段。 | `ScheduleService`、`data/schedules.json` |
| 关系/承诺 | 有方向好感与信赖、四级标签、无期限承诺。 | `RelationshipService`、`PromiseService` |
| 调查手册 | 事实、证据、线索、承诺、关系、作息、时间线和最近对白。 | `JournalPanel` |
| 对话 | 历史、自动播放、仅跳已读、完整意图选项。当前为 C# 演示层。 | `DialoguePanel`、`MainController` |
| 战斗 | 单场共鸣战，五种情境行动、故事模式、重试和撤退。 | `ResonanceBattle`、`BattlePanel` |
| 存档 | 单槽 JSON、检查点、临时文件和备份恢复、版本拒绝。 | `GameSession`、`SaveGameSerializer` |
| 辅助设置 | 字号、文本速度、减少闪烁、震动、高对比、故事模式、1280×720/1600×900/1920×1080 窗口、无边框全屏、镜头速度和 F11 快切。 | `MainController` / `PlayerSettings` / `DisplaySettingsController` |

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
- `data/events.json`：8 个切片事件。
- `data/items.json`：8 个梦境道具定义及图标路径。
- `dialogue/events/`：8 个对应 `.dialogue` 文件。
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
- `DreamEscapeController.cs`、`DreamAreaVisual.cs`：梦境表现编排与 2D/3D 区域统一运行时接口；答案只由 Core 判定。
- `DreamAreaController.cs`：走廊和后三室的 2D 场景交互；`DreamAreaController3D.cs` 只把第一室稳定 ID 与坐标接入通用 3D 层。
- `WorldArea3D.cs`、`PlayerController3D.cs`、`ThirdPersonCameraRig.cs`、`CameraRelativeMovement.cs`：可供后续梦境和未来小镇复用的 3D 区域、屏幕相对移动、跟随式透视环绕和 SpringArm 防穿模。鼠标 Orbit 在 `_Input` 中且只响应捕获状态；旧 `QuarterTurnCameraRig` 仍保留作实验对照。
- `InteractionMarker3D.cs`、`StatefulObjectVisual3D.cs`、`AreaCoordinateMapper3D.cs`：3D 交互光环、状态变体和二维/三维坐标转换。旧 `WallCutawayController` 与着色器不再由第一室加载。
- `DreamClassroomVisual3D.cs`：第一室 GLB 状态绑定、含关闭门在内的 20 组独立碰撞与旧坐标安全落点。原图和 TileMap 视觉脚本继续保留作分支对比。
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

截至 2026-08-22：

- Core、应用规则和内容完整性测试 89/89 通过；在此前 88 项基础上新增 1 项，验证新游戏显式安全出生点不会改变缺失梦境坐标字段的旧 v1 存档回退语义。
- 根 Godot 项目 Debug/Release 构建通过。
- 主场景、Legacy 两图、梦境根、走廊与四室共九场景实例化通过。
- 内容仓库报告 `characters=5 schedules=21 events=8 items=8`。
- 第一室 MVP 与精修 v2 的 `.blend`/GLB 均通过 Blender 5.2.0 后台结构、节点、十组共享课桌、八张纹理哈希、预算和确定性导出校验；精修版为 133 个可见网格实例、19,082 个三角面。
- 3D 冒烟覆盖正式方案 A 的透视 Orbit 常量、连续 360°水平旋转、任意角度屏幕相对移动、模态输入恢复、坐标往返、新游戏出生点无碰撞/无初始目标聚焦/完整 3.4 米镜头距离、旧坐标安全落点、九个稳定交互点、四类视觉状态、20 组真实碰撞、SpringArm 和门动画物理探针。
- 本轮 Debug 冒烟还注入并验证了捕获状态下的鼠标水平位移、真实 I/Tab/X 与 Backspace 排除、Tab 在 GUI 持有焦点时仍能开关背包、Esc 暂停释放指针，以及退出确认只创建和显示一次。
- Windows 实机短程验证已覆盖 Tab 打开/关闭背包、Esc 打开暂停、继续游戏、打开/取消退出确认及最终正常退出。自动化工具无法产生与真实设备等价的捕获态 raw mouse delta，因此物理鼠标环绕仍需人工复验；Xbox 和中低端集显同样未验收。
- 可自动输出四个典型环绕方向、初始/黑板/教师桌/地图柜状态、碰撞调试，以及门/黑板/课桌近景共十二张原生 1920×1080 图。
- RTX 2080 Ti 的 Compatibility 自动采样在 1280×720、1600×900 与原生无边框 1920×1080 均约为 60 FPS，最低瞬时 59.1/59.5/59.0 FPS，最大环绕帧时 16.92/16.80/16.94 ms，第一室加载 354.67/354.92/371.22 ms。中低端集显与完整人工路径仍未验收。

## 已知缺口

- Dialogue Manager v3.10.4 未放入 `addons/`；`.dialogue` 尚不是运行时入口。
- 当前 `NarrativeBridge.Execute` 对单条命令做白名单写入，但没有把一段 `.dialogue` 的多条命令组成原子 outcome；插件接入时必须迁移，不能直接视为完成的事务桥。
- `MainController` 同时负责导航和剧情演示，后续内容扩展前应完成对白接入与职责拆分。
- 当前只有第一室完成 3D 与精修 v2 自由镜头实现，仍待负责人视觉终验；原图＋2D 碰撞版本保留在 `codex/classroom-original-image-collision`，模块化 TileMap 基线保留在更早提交。走廊、音乐教室、档案室、礼堂和 Legacy 小镇仍为 2D。已有八个道具图标、晓雨中性半身、主角/晓雨 4×4 四向行走表、合影滤色/录音机特写和礼堂控制板底图。教室 v3 已生成离线 Blend/GLB 候选但尚未切换运行时；当前 v2 仍是场景引用。
- 半写实 3D 主角的 v1 技术样模验证了 Blender 官方 Human Base Meshes v1.4.1 CC0 男性人体、1.694 米尺度、闭合独立服装/鞋/发帽网格与静态 GLB 管线，但仍明确不是视觉候选。原始人物方向图已提升为正式身份锚点，并新增同一人物的脸发和服装细节板；原画式 v2 已生成静态 GLB/manifest 并通过只读复导出检查，当前指标为 `1.7017m`、`34972` 三角面、6 个可见网格、1 个材质面、无 Armature/Action，manifest 明确为 `runtimeReady=false`。独立 Godot 捕获没有替换运行时，且发现方案 A 默认构图为 `36.47%`、后侧受墙体压缩后约为 `49.25%–49.49%`，原定 `28%–31%` 仍未满足；这是需要在视觉确认后处理的 3D 镜头口径与 SpringArm 构图问题，不是缩小人物世界尺寸的理由。v2 仍等待负责人视觉确认，v1/v2 均未绑定、无动画、未进入 Godot，运行时仍为二维 billboard；静态视觉确认前不得开始骨骼、`idle/walk/jog` 或游戏接入。音频和 3D 晓雨同行也尚未制作。
- 名称输入的完整 Windows IME、按键重映射、真实物理鼠标 raw delta、手柄震动和所有辅助设置尚未做实机闭环验证。
- 梦境 30–45 分钟完整路径、键盘/Xbox、150% 字号和门转场尚未完成人工闭环；720p/1080p 自动截图与当前独显性能已验收，Legacy 两日流程仍需旧存档回归。
- Windows 导出模板缺失，未生成可分发 ZIP。
- 导出 preset 会排除 `doc/`、`tests/`、`work/` 和 `outputs/`；但实际导出尚未验证。

这些缺口的处理顺序见[交接路线图](../production/roadmap-handoff.md)。
