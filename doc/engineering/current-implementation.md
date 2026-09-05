# 当前实现说明

## 交付边界

`[已实现·待人工终验]` 新游戏进入目标 90–120 分钟的“四室旧校舍梦境”序章第一阶段，用于验证跨房间谜题、道具、记录、提示、刺醒和存档。出口开启后停在机制完成页，不生成正式重要信息或纸条。

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

1. 新游戏出生在第一室前部安全区；当前运行时加载 12×9×4 米 v5 教室，使用方案 A 的 Orbit 第三人称。窗框上不再提供方向答案，后排左课桌维护卡记录“上—下—中”；讲台作息表和值日便签分别提供 `7:00` 与“第 2 项”。三张线索与对应机关均相距至少 2 米，调查后光环隐藏、纸面实体保留。玩家仍可不调查直接尝试，错误无副作用。完整流程为窗扣→粉笔黑板→时钟/值日表→`372` 教师桌，地图柜和气窗盒形成两轮回访。
2. 第二室是约 11×8×4 米 3D 音乐教室。用粉笔拓显 `1—4—2—3` 并打开器材间，救出初次见面的林晓雨；入口排练通知、器材间对照表和纸带柜模板分别与节拍器、音叉架和纸带播放器分离至少 2 米。三条支线逐层换算为“木鱼—钢琴—三角铁—手铃”，由声学墙取得第三室钥匙。地图柜后的点名装置和放映机发放夹钥后的琴凳构成两次回访。
3. 第三室在约 12×9×4.2 米 3D 档案兼广播室中实现 15 个交互点：墙面规则和四份盖章档案→信号登记，入口索引条→抽屉线路，两支线产物→保险丝修复，第一次回访奖励与修复保险丝→晓雨协作录音机，机械停点→剪辑计数器，并保留第二轮高柜回访。答案线索与对应机关至少相距 2 米，门内出生点不会自动聚焦目标。
4. 第四室在约 18×13×7.5 米 3D 礼堂中实现 15 个交互点：磨损尺与配重→`3—1—4—2` 吊杆组，锁后投影轮廓与镜头→放映机，三件回访工具→三段剪带，登记卡→母带拼接与播放器白噪声，最后并行完成灯光、声道、广播控制台和双侧出口。晓雨按住左侧机关和面板预览是仅当前实例有效的临时状态，关闭、离房或重载清零，右侧总闸不会在姿态缺失时调用 Core 结算。
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
| 梦境密室 | 第一室出生、3D 洋房公共区与四室回访、来源＋目标门口出生、自动使用匹配道具、分页背包/记录、晓雨加入后三级提示、通用谜题、刺醒、出口完成页。 | `DreamEscapeService`、`DreamEscapeController` |
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
- `data/items.json`：22 个当前梦境道具定义及图标路径；仍不是完整 34 件目录。
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
- `DreamEscapeController.cs`、`DreamAreaVisual.cs`：梦境表现编排、统一运行时接口和可选瞬时表现接口；答案只由 Core 判定，预览值与晓雨机关姿态不写存档。
- `DreamAreaController3D.cs`、`DreamMusicRoomController3D.cs`、`DreamArchiveRoomController3D.cs`、`DreamAuditoriumController3D.cs` 与 `DreamMansionController3D.cs`：把四室和洋房内廊的稳定 ID、坐标与门转场接入通用 3D 层。旧后两室 2D Controller/Painter/Marker 已移除。
- `WorldArea3D.cs`、`PlayerController3D.cs`、`ThirdPersonCameraRig.cs`、`CameraRelativeMovement.cs`：可供后续梦境和未来小镇复用的 3D 区域、屏幕相对移动、跟随式透视环绕和 SpringArm 防穿模。鼠标 Orbit 在 `_Input` 中且只响应捕获状态；旧 `QuarterTurnCameraRig` 仍保留作实验对照。
- `InteractionMarker3D.cs`、`StatefulObjectVisual3D.cs`、`AreaCoordinateMapper3D.cs`：3D 交互光环、状态变体和二维/三维坐标转换。旧 `WallCutawayController` 与着色器不再由第一室加载。
- `DreamClassroomVisual3D.cs`：第一室 v4 GLB 的七类机关状态绑定、29 组独立碰撞、11.4×8.4 米活动区和旧坐标安全落点。原图、TileMap、v2/v3 视觉资产继续保留作对照。
- `DreamMansionVisual3D.cs`：洋房内廊 GLB、独立碰撞、房门铰链和安全落点；完整外观 GLB 是离线资产，不由场景引用。
- `DreamMusicVisual3D.cs`、`DreamCompanion3D.cs`：约 11×8×4 米音乐教室、16 组碰撞、18 个交互目标，以及跟随/超距回位和后两室短时协作姿态的晓雨 Fixed-Y billboard。
- `DreamLateRoomVisual3D.cs`、`DreamArchiveVisual3D.cs`、`DreamAuditoriumVisual3D.cs`：约 12×9×4.2 米档案室与 18×13×7.5 米礼堂的独立碰撞、安全落点、持久状态和瞬时预览；两室各 15 个交互目标。
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

截至 2026-09-02：

- Core、应用规则和内容完整性测试 104/104 通过；覆盖内容版本 5、28 个正式谜题、34 件道具、11 个安装槽、新旧 ID、目标—道具自动匹配、盲猜不阻塞、缺失线索查询、依赖图无环、三条并行支线、两轮回访、四个协作机关、组合产物先入包、错误无副作用、幂等、进行中 v4 存档拒绝和完成终态兼容。
- 根 Godot 项目 Debug/Release 构建通过。
- 主场景、Legacy 两图、梦境根、3D 洋房公共区与四室共九场景实例化通过。
- 内容仓库报告 `characters=5 schedules=21 events=8 items=34`。
- 教室 v4 和洋房内廊 v1 继续通过原有只读复导，作为历史运行时基线；洋房外观 v1 仍是离线验收与未来复用资产，当前关卡不加载。
- `[已实现·当前运行时]` 电影式半写实教室 v5 与洋房内廊 v2 已按负责人确认方向扩展全场景。教室为 114,452 三角面、244 个总网格/178 个最大同时可见网格、14 个材质和 56,502,720 字节；洋房为 88,244 三角面、198 个可见网格、12 个材质和 37,793,392 字节。两份权威 Blend 缺失外链为 0，临时复导哈希一致，均低于 60 MiB 与既定几何预算。
- 教室已补齐 18 组共享课桌五金、非对称书纸、窗扣/窗帘细木作、广播与开关、教室门、地图柜和教师桌状态细节；洋房已补齐七组门细木作和正确铰链原点、全廊护墙板、六组壁灯、壁炉工具、玄关家具、梁架金属箍、画框及两个扩展实墙模数。
- 当前环境使用 Filmic、SSAO、局部冷暖实时灯和每区域两个 ReflectionProbe；没有使用 LightmapGI。代表 1080p 捕获的 `p90` 约 0.25–0.29，较初次完整导入的 0.12–0.16 显著提高且无过曝白斑。捕获设备为 RTX 2080 Ti，仅验证 Compatibility 加载和视觉链路，不代表 GTX 1660 级性能通过。
- 3D 冒烟覆盖正式方案 A、第一室/洋房坐标往返和门口出生；第一室检查 v5 PBR、完整细节节点、两个 ReflectionProbe、十个唯一可达交互点、窗框双目标、29 组碰撞、18 组课桌、七类机关状态和双向门动画；洋房检查 v2 PBR、完整细节节点、七个门枢轴、两个 ReflectionProbe 和门动画。
- 第二室专项冒烟检查 Orbit/SpringArm、15 个唯一可达交互点、16 组碰撞、两个 ReflectionProbe、西南门安全出生和受控门动画；并覆盖器材间关闭/打开、五件拾取物、两种安装状态、三层映射、声学墙/点名装置/琴凳灯态、晓雨加入前位置、加入后跟随与超距回位，以及四个离线 WAV 和非听觉文字替代。
- 本轮 Debug 冒烟还验证了紧凑区域名且无任务栏、普通道具无选用按钮、新游戏操作确认/直接载入不重复、黑板自动用粉笔、时钟一次装入两部件、录音机一次装入三部件并启动；同时继续覆盖真实 I/Tab/X、Backspace 排除、Esc 暂停释放指针和退出确认唯一性。
- Windows 实机短程验证已覆盖 Tab 打开/关闭背包、Esc 打开暂停、继续游戏、打开/取消退出确认及最终正常退出。自动化工具无法产生与真实设备等价的捕获态 raw mouse delta，因此物理鼠标环绕仍需人工复验；Xbox 和中低端集显同样未验收。
- 可自动输出四个典型环绕方向、初始/黑板/教师桌/地图柜状态、碰撞调试，以及门/黑板/课桌近景共十二张原生 1920×1080 图。
- RTX 2080 Ti 的 Compatibility 自动采样在 1280×720、1600×900 与原生无边框 1920×1080 均约为 60 FPS，最低瞬时 59.1/59.5/59.0 FPS，最大环绕帧时 16.92/16.80/16.94 ms，第一室加载 354.67/354.92/371.22 ms。中低端集显与完整人工路径仍未验收。

## 已知缺口

- Dialogue Manager v3.10.4 未放入 `addons/`；`.dialogue` 尚不是运行时入口。
- 当前 `NarrativeBridge.Execute` 对单条命令做白名单写入，但没有把一段 `.dialogue` 的多条命令组成原子 outcome；插件接入时必须迁移，不能直接视为完成的事务桥。
- `MainController` 同时负责导航和剧情演示，后续内容扩展前应完成对白接入与职责拆分。
- 洋房与四室均使用 3D 自由镜头；Legacy 小镇仍为 2D。第三室已使用信号登记新 ID，第四室已使用吊杆组配重新 ID，旧社团排序、预约表和幕布顺序 ID 只存在于退役目录。当前目录为 34 件，三段剪带可组合为复原母带并安装；播放器只循环离线白噪声。50–60 分钟后半程、完整通关、Xbox、物理鼠标和性能仍待人工验收。教室 v2/v3/v4 和原图/TileMap 版本保留作对照，v5 是当前场景引用。
- 环境全场景扩展已完成。当前工作副本以本机非商业 `mc_model` 补上 3D 区域角色表现，但该资产禁止再分发并从 Git/Windows 导出排除；干净检出和发布包仍回退到 2D billboard，正式可分发 3D 主角仍是已知缺口。英雄物件可继续独立重拓扑，但不得改变碰撞、交互锚点或稳定 ID。
- 半写实 3D 主角的 v1 技术样模验证了 Blender 官方 Human Base Meshes v1.4.1 CC0 男性人体、1.694 米尺度、闭合独立服装/鞋/发帽网格与静态 GLB 管线，但仍明确不是视觉候选。原始人物方向图已提升为正式身份锚点，并新增同一人物的脸发和服装细节板；原画式 v2 已生成静态 GLB/manifest 并通过只读复导出检查，当前指标为 `1.7017m`、`34972` 三角面、6 个可见网格、1 个材质面、无 Armature/Action，manifest 明确为 `runtimeReady=false`。独立 Godot 捕获没有替换正式可分发基线，且发现方案 A 默认构图为 `36.47%`、后侧受墙体压缩后约为 `49.25%–49.49%`，原定 `28%–31%` 仍未满足；这是需要在视觉确认后处理的 3D 镜头口径与 SpringArm 构图问题，不是缩小人物世界尺寸的理由。v2 仍等待负责人视觉确认，v1/v2 均未绑定、无动画、未进入 Godot；本机 `mc_model` 默认替换是独立的非商业运行时覆盖，不代表 v2 获批或固定外观被改写。第二室已有离线自制音频和晓雨 3D billboard 同行，但正式 3D 晓雨模型仍未制作。
- 名称输入的完整 Windows IME、按键重映射、真实物理鼠标 raw delta、手柄震动和所有辅助设置尚未做实机闭环验证。
- 梦境 90–120 分钟完整路径、键盘/Xbox、150% 字号和门转场尚未完成人工闭环；720p/1080p 自动截图与当前独显性能已验收，Legacy 两日流程仍需旧存档回归。
- Windows 导出模板缺失，未生成可分发 ZIP。
- 导出 preset 会排除 `doc/`、`tests/`、`work/` 和 `outputs/`；但实际导出尚未验证。

这些缺口的处理顺序见[交接路线图](../production/roadmap-handoff.md)。
