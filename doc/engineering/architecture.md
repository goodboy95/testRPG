# 技术架构

> 基线：Godot 4.6.3 .NET、C#、Compatibility/OpenGL、Windows x64

## 架构目标

`[固定约束]` 游戏规则必须能够脱离 Godot 场景运行和测试。Godot 负责输入、绘制、场景和 UI；`TownRpg.Core` 负责状态、规则和持久化契约；Dialogue Manager 最终只负责对白流程。这样场景切换、插件升级风险或 UI 重做不会改变世界状态的含义。

```mermaid
flowchart TD
    Input["键盘 / Xbox 输入"] --> Presentation["Godot 表现层\nMainController / World / UI"]
    Content["JSON / .dialogue / 本地化"] --> Repository["ContentRepository"]
    Repository --> Presentation
    Presentation --> Session["GameSession\n组合根与唯一会话"]
    Dialogue["Dialogue Manager 3.10.4\n计划接入"] --> Bridge["NarrativeBridge"]
    Bridge --> Session
    Session --> Services["TownRpg.Core.Services"]
    Services --> State["唯一 GameState"]
    State --> Save["SaveEnvelope v1\nuser://save-slot-1.json"]
```

依赖方向只能从表现层指向 Core。`TownRpg.Core.csproj` 不引用 Godot；根 `TownRpg.csproj` 引用 Core，并显式排除 `src/`、`tests/`、`work/`、`outputs/` 的重复编译。

## 运行期组成

### Autoload

- `[已实现]` `GameSession`：持有唯一 `GameState`、内容仓库、模态门、规则服务和当前战斗；负责新游戏、时间、睡眠、存读档和服务重绑定。
- `[已实现]` `Narrative`：`NarrativeBridge` 的 Autoload 名称。向对白层暴露固定参数数量的 `Evaluate`/`Execute` 重载、玩家姓名和称谓。

`GameSession` 是 composition root，不是领域模型。每次新游戏或读取存档后，它用新的 `GameState` 重建 `TimeService`、`RelationshipService`、`NarrativeCommandService`、`EventOutcomeService` 和 `DreamEscapeService`。

### Core 领域聚合

`GameState` 包含以下可存档部分：

| 部分 | 职责 |
| --- | --- |
| `Player` | 姓名、称谓、当前地图和基础坐标。 |
| `World` | 日期、分钟、剧情阶段、路线、暴露、仪式节点、事实和证据。 |
| `Relationships` | 有方向的好感、信赖、恋爱状态、共同记忆和已生效礼物阶段。 |
| `Promises` | 无期限承诺及其单向状态机。 |
| `Investigation` | 线索、已知日程和按日期记录的时间线。 |
| `Inventory` / `Progression` | 金钱、物品、1–10 级经验。 |
| `DreamEscape`（可选） | 梦境状态、区域坐标、独立物品、谜题、开放区、线索、提示等级和晓雨加入状态；旧存档为 `null`。 |
| 完成集合 | 已完成事件、已应用结果、已读对白。 |
| `Settings` | 文本速度、字号、闪烁、震动、高对比、故事模式、窗口尺寸、全屏和镜头速度。 |

禁止把 `Node`、Signal、`Vector2` 或插件对象加入这些类型。玩家坐标在 DTO 中拆为两个 `float`。

## 状态写入与原子性

### 普通规则

关系、承诺、时间、日程、路线和结局都通过 Core 服务修改或判断。UI 读取阶段文本，不读取内部阈值后自行复制业务规则。

`DreamEscapeService` 同样是唯一谜题判定入口。它先验证当前状态、区域、前置线索与物品，再原子完成一次小操作；`DreamActionResult.Changed=false` 表示错误或重复操作，`GameSession` 不保存。`TargetItemUses` 保存稳定的“目标→可用道具”目录，`UseAvailableItems` 按目录顺序自动应用背包内全部匹配部件；普通物品没有选中状态，批量互动只通知和保存一次。

### 叙事命令

`NarrativeCommandService` 定义对白可用的状态写入白名单。当前允许：

```text
add_fact           1
add_evidence       1
add_lead           1
timeline           1
change_affinity    3
change_trust       3
remember           3
create_promise     4
resolve_promise    2
raise_exposure     1
complete_event     1
grant_xp           1
```

查询限于：`has_fact`、`has_lead`、`event_complete`、`trust_at_least`、`affinity_at_least`、`day_at_least`。查询缺失关系时按 0 处理，不能为了读取条件而创建关系。

`EventOutcomeService.Apply(outcomeId, mutations)` 先验证全部命令，再序列化快照并逐项执行。任何一项失败都会恢复快照；成功后写入 `AppliedOutcomes`。同一 `outcomeId` 再次应用返回无操作，防止双击、重进和载入重放重复奖励。当前可玩 C# 演示流程通过它提交具名结果。

`[计划]` 现有 `NarrativeBridge.Execute` 直接调用 `NarrativeCommandService.Execute`，因此单条命令有白名单校验，但一段 `.dialogue` 中的多条命令还不是一个原子事务。这是插件接入前必须消除的技术债，不能把“每条命令合法”误写成“整场事件原子”。

```mermaid
sequenceDiagram
    participant UI as 对话/事件 UI
    participant EO as EventOutcomeService
    participant NC as NarrativeCommandService
    participant GS as GameState
    UI->>EO: outcomeId + mutations
    EO->>EO: 验证全部命令和参数
    EO->>GS: 创建序列化快照
    loop 每个 mutation
        EO->>NC: Execute
        NC->>GS: 修改强类型状态
    end
    alt 全部成功
        EO->>GS: 记录 AppliedOutcomes
    else 任一失败
        EO->>GS: 恢复完整快照
    end
```

## 时间、模态与输入

- `[已实现]` 新游戏的世界时钟仍初始化为第 1 日 08:00，但先进入梦境；梦中移动只更新梦境坐标，不推进世界时间。Legacy 世界每现实移动秒推进 0.75 游戏分钟。
- `[已实现]` `PlayerController` 比较移动前后位置，只在实际位移时调用 `AdvanceMovingTime`，顶墙不走时钟。
- `[已实现]` `ModalGate` 按 owner 引用计数。对话、手册、梦境暂停、标题、战斗和完成页持有 lease，阻止玩家移动和时间推进；梦境暂停不依赖 `SceneTree.Paused`。
- `[固定约束]` 不使用全局 `SceneTree.Paused` 冻结业务流程；异步流程必须在 `finally` 或统一关闭方法中释放 lease。
- `[已实现]` `WorldSceneController._UnhandledInput` 处理世界交互，减少 UI 已消费确认键后重复触发的风险。
- `[已实现]` 梦境背包为真实 `I`/`Tab` 或 Xbox `X`，Backspace 不属于该动作。背包在 `DreamEscapeController._Input` 的输入前置阶段统一开关，避免 Tab 被 GUI 焦点导航吞掉；其余移动/交互保持 WASD/方向键、F/空格或左摇杆、A。新游戏进入第一室前显示一次阻塞式 Esc、I/Tab、Menu/X 操作确认，读档和换房不重复。
- `[已实现]` 梦境没有常驻任务目标栏，只保留紧凑区域名；非交互 HUD 使用忽略鼠标的过滤策略。Orbit 3D 区域由 `ThirdPersonCameraRig._Input` 读取鼠标水平位移，并且只有 `InputActive=true` 且指针处于捕获状态时才修改 yaw。右摇杆仍在帧更新中控制水平环绕。
- `[已实现]` 进入第一室或 3D 洋房公共区游玩时捕获指针；Esc 梦境暂停、背包/谜题/确认框、失焦和转场会立即显示指针并锁定角色与镜头，恢复后重新捕获。指针意外可见时，点击游戏画面可请求重捕获。标题和梦境暂停都提供退出入口，并复用单个二次确认框，避免快速输入重复创建或重复触发。

## 场景生命周期

主场景 `Main.tscn` 常驻。新游戏实例化普通 `Node` 根的 `DreamEscape.tscn`，后者在自身 `AreaHost` 中替换洋房公共区/四室；第一至第四室与稳定 ID 仍为 `dream.corridor` 的洋房前厅内廊均是 `Node3D`。Legacy 存档实例化 `OldHouse.tscn` 或 `StationQuarter.tscn`。切换时均断开普通 .NET 事件并释放旧节点，长期状态只保存稳定区域 ID 和基础坐标。

`IDreamAreaRuntime` 为梦境区域统一提供交互请求、提示变化、辅助设置和 `CameraControlMode`，不再传递临时选中道具。通用 3D 表现层由 `WorldArea3D`、`PlayerController3D`、`ThirdPersonCameraRig`、`CameraRelativeMovement`、`InteractionMarker3D`、`StatefulObjectVisual3D` 与 `AreaCoordinateMapper3D` 组成；这些类型不引用梦境谜题答案。洋房与四室均使用透视 Orbit 跟随和 SpringArm；旧 `QuarterTurnCameraRig`、`WallCutawayController` 与裁墙着色器仅保留作历史实验对照，不由当前场景加载。`IDreamAreaExitTransition` 允许区域在 Core 切换前播放可取消的门动画并提供统一复位；五个 3D 区域控制器只负责把各自稳定 ID、门表现与 `DreamEscapeState` 接入通用层。`DreamAreaTransientState` 与可选 `IDreamAreaTransientPresentation` 只传递谜题面板预览和晓雨当前协作姿态，面板关闭、换房或重载即清除；DTO 不包含这些字段。四室机关、拾取物、安装件和灯态只从 Core 集合派生，`DreamCompanion3D` 的跟随位置和动画是临时表现状态。锁闭洋房大门只产生表现反馈，不调用 Core 道具/谜题状态。

`DreamAreaSpawnCatalog` 按来源区域与目标区域选择换场出生点和默认面向。新游戏和普通读档不使用这条覆盖规则：前者仍采用第一室 `(448, 214)`，后者恢复实际保存位置。第一室、第二室与洋房之间的 3D→3D 换场把镜头相对于出口门的水平夹角转换到目标门，2D↔3D 使用目标门默认朝向；第三、第四室门内安全点与最近交互焦点保持超过 48 像素，避免入场自动选中。该入口姿态、镜头 yaw 和门动画进度全部是临时表现状态，不进入存档。

当前第一室场景引用 12×9×4 米的电影式 v5 教室 GLB，洋房场景引用电影式 v2 完整可玩内廊 GLB；第二室使用约 11×8×4 米的程序化 PBR 音乐教室。三个区域在 Godot 场景层使用 Filmic、实时冷暖灯和两个 ReflectionProbe，但光照与反射不进入 Core。第二室四个 WAV 由确定性离线脚本生成并记录 provenance，运行时只加载文件，不合成音频；声音关闭时仍可依赖图形、音高与乐器名完成全部机关。`PlayerController3D` 优先动态加载本机非商业 `tests/mc-model-test-runtime/caelus-pbr-test.glb`，驱动 `idle/walk/jog`、实际位移朝向和近镜头隐藏；资源不存在或结构无效时使用 Fixed-Y 二维角色 billboard。该本机目录由 Git 排除，`export_presets.cfg` 也排除 `tests/*`，因此发布包只保留安全回退。v2/v3/v4 教室与 v1 洋房内廊继续作为历史/离线对照，完整洋房外观 v1 GLB不进入场景树。基于 Blender 官方 CC0 Human Base 的主角 v1 静态 GLB 只是未过视觉门槛的技术样模。原画式主角 v2 与 v1 分开保留，其静态 GLB/manifest 已通过尺寸、预算、结构和只读复导出检查，但 manifest 明确为 `runtimeReady=false`，仍等待负责人视觉确认；本机 `mc_model` 覆盖不改变 v2 的审批状态，也不替代正式可分发主角资产。

梦境存档继续只保存旧二维 `PositionX/Y`。第一室在表现层把固定存档矩形 `Rect2(16,178,608,152)` 映射到 11.4×8.4 米 X/Z 活动区；第二室把 `Rect2(16,54,608,272)` 映射到 10.5×7.5 米活动区，从洋房进入使用 `(50.7,288.7)` 的西南门内点；洋房把存档 X `0…640` 映射到世界 Z `-6.8…18`，存档 Y `0…360` 映射到世界 X `-3.8…3.8`。`CreateDreamPrologue()` 只为新游戏显式使用第一室 `(448, 214)` 的安全出生点；从洋房返回第一室使用 `(592.4,305.8)`。若恢复坐标位于新家具或门障碍内，则解析最近安全点并保存修正。镜头 yaw、SpringArm 长度、当前焦点、门动画进度、晓雨位置和角色动画帧均为临时表现状态。

`DreamEscapeState.ContentVersion=4` 区分完整四室规则；`DiscoveredPuzzles` 控制晓雨加入后的提示可见性，`InstalledParts` 保存 11 个稳定槽位 ID。Core 的 `PuzzleInputClues` 目录提供“谜题→外部输入线索”映射及缺失查询，只控制面板说明和三级提示，不阻止正确答案提交；结构前置、安装件、区域开放和协作要求仍由服务判定。机关视觉只由这些 Core 集合派生，Godot 不持有第二份安装或开关状态。缺少内容版本或版本低于 4 的进行中梦境存档明确拒绝；已完成且梦境背包为空的终态和无梦境 Legacy 存档不受影响。

礼堂双侧出口的“晓雨正在按住左侧机关”只属于当前 `DreamEscapeController` 的临时协作姿态：离开礼堂、重建场景或完成出口时清零，不写入 `DreamEscapeState`，也不复用退役的 `dream.auditorium.left_lever`。Core 仍负责验证晓雨加入和三块控制台完成；表现层只有在临时姿态成立后才调用出口结算。

当前独立世界场景只有旧宅和车站分区；河岸、学校档案室、商店活动和废弃铁路主要通过事件段落或同一车站地图上的交互点表现。完整版本扩图时继续保持“常驻应用壳 + 可替换世界子场景”。

所有订阅普通 .NET event 的场景节点必须在 `_ExitTree` 解除订阅；Godot 自动断开 Signal 不会处理普通 .NET 事件。异步等待在节点退出时必须取消，避免 await 永久悬挂或访问已释放节点。

## 内容加载

`ContentRepository` 启动时从 `res://data/` 读取 `characters.json`、`schedules.json`、`events.json` 和 `items.json`：

- 四个 catalog 必须使用 `schemaVersion: 1`。
- 角色 ID 必须唯一且包含主角。
- 日程 NPC 必须存在，等价条件的时间段不得重叠。
- 每个事件的 `dialoguePath` 必须存在。
- 34 个当前梦境道具 ID 必须完整唯一，本地化键和图标路径必须存在。
- 验证失败写入 `InitializationError`，标题页应阻止新游戏而不是带着残缺内容继续。

更完整的跳转、命令、本地化和资源检查由 `ContentIntegrityTests` 完成。

## 存档

- `[已实现]` 单槽路径为 `user://save-slot-1.json`，外层 `SaveEnvelope.formatVersion` 当前为 1。
- `[已实现]` 写入先落到 `.tmp`，旧档旋转为 `.bak`，新档成功换入后删除备份；提交失败尝试恢复旧档。
- `[已实现]` 正式档不存在但 `.bak` 存在时，读取会先尝试恢复备份。
- `[已实现]` 空文件、坏 JSON、缺少必要分区、非法枚举/范围、重复关系/承诺和版本不匹配都会明确拒绝。
- `[固定约束]` 手动保存只允许稳定状态；对话或战斗前使用检查点。退出战斗后应回到战前状态，而非序列化半场对象。

`ValidPlayerLocations` 包含 `old_house`、`station_quarter` 和 `dream_school`。`DreamEscape` 是 v1 内可选分区：缺失时按 Legacy 流程；存在时会严格拒绝未知区域/物品/谜题/线索、越界提示等级和不一致状态，因此本次未提升格式版本。

## Dialogue Manager 边界

`[计划]` 正式集成固定为 Dialogue Manager v3.10.4，并放在 `res://addons/dialogue_manager/`。当前八个 `.dialogue` 文件已经按 `Narrative.Execute("command", "arg"...)` 和 `Narrative.Evaluate(...)` 编写，但插件目录不存在，实际游戏仍运行 `MainController` 中的硬编码演示对白。

接入时：

1. 保留 Autoload 名 `Narrative`；若继续暴露反射方法，使用固定参数重载，避免 C# `params string[]` 无法被插件正确展开。
2. 用插件加载 `EventDefinition.dialoguePath` 和 `startTitle`，替换演示编排。
3. 插件不得保存事实、关系、承诺、暴露或路线变量。
4. 用 `eventId + outcomeId` 查询 C# 预定义 mutation，或在桥内缓冲并一次调用 `EventOutcomeService.Apply`；提交成功后才完成事件。
5. 迁移现有逐条 `Narrative.Execute` 调用，不能保留“执行到一半即可存档”的状态。
6. 插件异常或取消时不标记事件完成，允许玩家重新交互。

## 扩展点与高风险区

- 新地图：同步 `SaveGameSerializer.ValidPlayerLocations`、场景冒烟、世界转场和安全点。
- 新状态字段：同步 `GameState`、存档验证、往返测试，并决定是否提升格式版本。
- 新对白命令：同步允许集合、参数契约、执行、原子回滚测试、内容解析测试和本文档。
- 新路线条件：集中放入 `StoryRuleService`，不得散落在 UI。
- `MainController` 当前同时承担应用导航和切片事件演示，接入插件后应拆出事件呈现，但不要在插件中复制 Core。

## 不适用架构

项目没有前后端、Controller、数据库、远程 API 或运行时生成服务。`doc/api/`、`sql/schema.sql`、服务发现和 Web E2E 流程均不适用。
