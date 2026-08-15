# 当前实现说明

## 交付边界

`[已实现]` 当前是预计 15–30 分钟的“两日生活＋首次异象”垂直切片，用于验证探索、时间、关系、承诺、调查、暴露提示、情境战和存档是否形成可理解的因果链。

`[计划]` 完整 3–5 小时版本的五个居民、完整小镇、公司设施、谨慎/救援路线、恋爱确认、终战和结局尚未成为可游玩内容。Core 中已有部分枚举和判定骨架，但这不等于内容已经完成。

## 可玩流程

### 标题与新游戏

- 标题画面使用 `assets/generated/title-town-dusk.png`。
- 可开始新游戏或在存在主档/备份时继续。
- 新游戏允许输入姓名和称谓；空值分别回退为“小岚”和“同学”。
- 设置保留在新游戏之间，剧情状态会重置。

### 第一天

1. 在旧宅醒来，选择关注车站信号或山中回声，写入不同事实。
2. 离开旧宅，在车站与林晓雨、周明、沈砚重逢；问候态度改变关系。
3. 可与乔姨交谈，得知开发公司正在打听旧隧道。
4. 三选一共同活动：河岸录音、学校旧地图、铁路怀表。每条产生不同线索/证据、关系和共同记忆。
5. 与活动伙伴建立明确的【承诺】：次日一起查看工程告示。
6. 回旧宅睡觉，进入下一日 08:00；睡眠本身不推进剧情事实。

### 第二天

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
| 应用导航 | 标题、改名、世界、暂停、设置、完成页。 | `MainController` |
| 世界场景 | 旧宅和车站分区；C# 原型绘制地图与 NPC。 | `Main.tscn`、`WorldSceneController`、`WorldPainter` |
| 移动/交互 | 8 方向移动、对角归一化、邻近目标按距离与朝向选择。 | `PlayerController`、`WorldSceneController` |
| 时间 | 仅实际移动推进，23:00 封顶，睡眠跨日。 | `TimeService`、`GameSession` |
| 日程 | JSON 日程、剧情事实过滤、下一可见时段。 | `ScheduleService`、`data/schedules.json` |
| 关系/承诺 | 有方向好感与信赖、四级标签、无期限承诺。 | `RelationshipService`、`PromiseService` |
| 调查手册 | 事实、证据、线索、承诺、关系、作息、时间线和最近对白。 | `JournalPanel` |
| 对话 | 历史、自动播放、仅跳已读、完整意图选项。当前为 C# 演示层。 | `DialoguePanel`、`MainController` |
| 战斗 | 单场共鸣战，五种情境行动、故事模式、重试和撤退。 | `ResonanceBattle`、`BattlePanel` |
| 存档 | 单槽 JSON、检查点、临时文件和备份恢复、版本拒绝。 | `GameSession`、`SaveGameSerializer` |
| 辅助设置 | 字号、文本速度、减少闪烁、震动、高对比、故事模式、无边框全屏。 | `MainController` / `PlayerSettings` |

## 运行链路

1. Godot 加载 `Main.tscn`，之前先创建 `GameSession` 和 `Narrative` Autoload。
2. `GameSession._Ready` 加载三个 JSON catalog，验证引用并绑定 Core 服务。
3. `MainController._Ready` 创建 HUD 和模态面板，显示标题。
4. 新游戏载入旧宅；世界控制器根据日程创建 NPC，并将交互 ID传给 `MainController`。
5. 切片事件由 `MainController` 运行异步对白；C# 演示流程的具名结果经 `EventOutcomeService` 应用。
6. `StateChanged` 驱动 HUD、NPC 日程和手册刷新。
7. 进入异象时创建战前检查点；战斗对象只活在会话内，不写入存档。

## 内容现状

- `data/characters.json`：5 个定义，主角、林晓雨、周明、沈砚、乔姨。
- `data/schedules.json`：21 条两日和后续兜底日程。
- `data/events.json`：8 个切片事件。
- `dialogue/events/`：8 个对应 `.dialogue` 文件。
- `localization/zh_CN.csv`：角色、日程、事件和对白静态 ID 的简体中文文本。
- Core 预留 8 名核心 NPC ID；何医生、温老师、罗记者、彭师傅目前没有完整角色 catalog、个人事件和可见地图内容。

## 主要代码职责

### `src/TownRpg.Core`

- `Domain/`：存档状态、枚举、角色 ID 和内容模型。
- `Services/`：时间、关系、承诺、日程、事件排序、叙事命令、原子结果、路线/结局、战斗和存档。
- `Content/`：JSON catalog DTO 与严格解析选项。

### `scripts/application`

- `GameSession.cs`：唯一会话与存档 I/O。
- `ContentRepository.cs`：Godot `res://` 内容加载和启动校验。
- `NarrativeBridge.cs`：为 Dialogue Manager 准备的固定签名桥。
- `ModalGate.cs`：引用计数模态锁。

### `scripts/presentation`

- `MainController.cs`：当前最大文件，负责应用 UI、世界切换和全部切片事件演示。
- `WorldSceneController.cs`：NPC 日程、交互目标、提示和地图事件入口。
- `DialoguePanel.cs`、`JournalPanel.cs`、`BattlePanel.cs`：对应模态 UI。
- `PlayerController.cs`、`NpcActor.cs`、`WorldPainter.cs`：移动和原型世界表现。

## 调试入口

- `F3`：显示/隐藏调试状态，含位置、事实数、证据数、暴露和等级。
- 暂停菜单“脱离卡住”：移动到当前场景安全点，不改变剧情。
- 存档位于 Godot `user://save-slot-1.json`，具体绝对路径可在引擎中用 `ProjectSettings.GlobalizePath` 查看。
- 初始化内容错误会记录在 `GameSession.InitializationError` 并输出到 Godot 日志。
- 引擎冒烟入口：`res://scenes/testing/GodotSmokeRunner.tscn`。

## 已完成验证

截至 2026-08-15：

- Core、应用规则和内容完整性测试 71/71 通过。
- 根 Godot 项目 Debug/Release 构建通过。
- 主场景 headless 30 帧启动通过。
- `Main.tscn`、`OldHouse.tscn`、`StationQuarter.tscn` 实例化通过。
- 内容仓库报告 `characters=5 schedules=21 events=8`。
- Compatibility/OpenGL 标题取帧确认标题图、中文字体和 640×360 UI 可显示。

## 已知缺口

- Dialogue Manager v3.10.4 未放入 `addons/`；`.dialogue` 尚不是运行时入口。
- 当前 `NarrativeBridge.Execute` 对单条命令做白名单写入，但没有把一段 `.dialogue` 的多条命令组成原子 outcome；插件接入时必须迁移，不能直接视为完成的事务桥。
- `MainController` 同时负责导航和剧情演示，后续内容扩展前应完成对白接入与职责拆分。
- 当前角色和地图主要是 C# 原型图形，尚无完整像素角色、四向动画、立绘、音频和战斗资产。
- 名称输入的完整 Windows IME、按键重映射、手柄震动和所有辅助设置尚未做实机闭环验证。
- 完整两日流程的各条分支、连续睡多日、窗口缩放、60 FPS 和切图耗时尚未人工验收。
- Windows 导出模板缺失，未生成可分发 ZIP。
- 导出 preset 会排除 `doc/`、`tests/`、`work/` 和 `outputs/`；但实际导出尚未验证。

这些缺口的处理顺序见[交接路线图](../production/roadmap-handoff.md)。
