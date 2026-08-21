# town-rpg 项目代理指南

本文件适用于根目录及全部子目录。任何人或自动化代理修改项目之前都必须先阅读本文件，再从 [doc/README.md](doc/README.md) 进入详细交接文档。

## 项目约束

- 项目名：town-rpg
- 项目类型：Windows 单机剧情生活 RPG 原型，当前为 2D/3D 混合表现并逐步迁移到 3D
- 前端技术栈：N/A
- 后端技术栈：N/A
- 测试服域名：N/A
- 正式服域名：N/A
- 前端对外端口：N/A
- 后端对外端口：N/A

Web、数据库、外部微服务、HTTP/gRPC、域名、监听端口、反向代理和服务部署均不适用。不要为本项目新增这些设施或相应文档。

## 文档与事实优先级

发生冲突时按以下顺序处理，并在同一次变更中修正文档：

1. 可执行测试和当前源码决定“现在实际怎样运行”。
2. 本文件决定技术、范围和剧情红线。
3. `doc/design/story-bible.md` 决定既定世界观、人物动机和幕后真相。
4. `doc/engineering/architecture.md` 决定模块边界与状态所有权。
5. `doc/design/story-flow.md` 中的 `[可调整草案]` 是制作建议，不是已实现事实。

文档统一使用四种状态标签：

- `[已实现]`：源码或数据中已经存在，并有相应验证。
- `[计划]`：已经决定要做，但当前未实现。
- `[固定约束]`：未经项目负责人确认不得改变。
- `[可调整草案]`：接手者可调整，但要保持固定约束和前后因果。

禁止用“已经完成”描述只存在于剧情大纲或 Core 枚举中的功能。

## 技术基线

- 引擎固定为 **Godot 4.6.3 .NET/Mono**，开发期不升级。
- 语言为 C#；游戏项目和纯核心库目标框架均为 `net8.0`。
- 当前命令行 SDK 由 `global.json` 固定为 .NET SDK 10.0.400。
- 渲染器固定为 Compatibility / OpenGL。
- 对话插件固定为 **Dialogue Manager v3.10.4**，这是 Godot 4.6 的匹配版本；插件尚未 vendored。
- 目标平台仅为 Windows x64；内部分辨率 640×360，Viewport 整数缩放，默认窗口 1280×720。
- 首版仅简体中文、单人、离线，不做联网、云存档、Mod 或 Steam 接入。

不得执行依赖安装、服务部署或容器编排。Godot 工程恢复只允许使用已经安装的 Godot 4.6.3 自带离线 NuGet 包源，不得为了构建临时升级引擎、SDK 或插件。

## 架构红线

- `TownRpg.Core` 必须保持纯 C#，不得引用 Godot、Dialogue Manager、场景节点或平台 UI。
- `GameSession` 是运行期组合根，`GameState` 是唯一业务状态；场景节点、对白插件和 UI 不得维护第二份世界状态。
- 场景之间只能长期保存稳定 ID 和基础 DTO，不得在状态或存档中保存 `Node`、`Vector2`、Signal 连接或其他 Godot 对象。
- Dialogue Manager 只负责对白流程。查询只能经 `NarrativeBridge.Evaluate`，修改只能经白名单 `NarrativeBridge.Execute` 进入 Core。
- 事件结果必须先完整校验、再原子应用；`AppliedOutcomes` 保证幂等。当前 `NarrativeBridge.Execute` 仍是逐条直写的过渡接口，Dialogue Manager 正式接入时必须改为提交预定义 outcome 或缓冲后一次提交，不能把逐行命令当成最终事务方案。
- 关系必须显式使用 `SourceId -> TargetId`，不得把有方向信赖简化成单个“好感度”。
- 承诺无隐藏期限；睡眠和长期不互动不能自动推动主线、降低关系或令承诺违约。
- 时间只因玩家实际移动推进；对话、菜单、手册、事件和战斗打开时暂停，23:00 封顶但不强制传送。
- 存档格式不兼容时必须明确拒绝；禁止静默丢字段、自动覆盖损坏存档或在半场战斗中恢复。
- 所有 Godot `Node`/`Resource` 派生 C# 类型必须是 `partial`；跨层事件只传 Variant 兼容的基础值或稳定 ID。

详细数据流和边界见 [架构文档](doc/engineering/architecture.md)。

## 剧情红线

- 主题固定为“信任先于证据，真相修复信任”；主角从旁观者成长为愿意承担误解和责任的人。
- 基调是青春奇幻冒险，保留民俗悬疑，不写成持续压迫的心理惊悚。
- 古老存在没有人类意义上的善恶，只通过声音、梦境、地层和情绪共鸣表达，不进行人格化对话。
- 主角与林晓雨在现实中此前从未见过；两人在旧校舍梦境中初次相遇。主角醒后只记得她的姓名与尚未确定的重要信息，无法清楚回忆她的面容。不得恢复“少年时否定她听见声音”或“四人旧友重逢”的设定。
- 小镇居民本质善良，不设置居民内鬼；被误导时表现为恐惧、犹疑或劝阻，不主动迫害林晓雨。
- 组织普通成员不知道唤醒灾厄的真实计划，必须保留通过证据策反部分成员的空间。
- 舆论操控只是阻止主角救出林晓雨的手段，不是组织或领袖的最终目的。
- 领袖的家族创伤可以被理解，但不能免除责任；其是否劝停由证据、倒戈者和玩家行动决定。
- 两条恋爱线含蓄且可选；确认林晓雨或沈砚后另一人自然转为友情，不设置惩罚。
- 主线关键事实不能只挂在关系阈值、礼物、承诺或某个限时 NPC 身上；低关系、失约、撤退或连续睡眠都不得软锁。

完整剧透和角色关系以 [剧情圣经](doc/design/story-bible.md) 为准。

## 内容与资产规则

- 稳定 ID 使用小写英文、数字、点和下划线；发布后不得复用旧 ID 表示新含义。
- `Facts` 是已确认事实，`Evidence` 是可用于揭露公司的证据，`Leads` 是可调查方向，三者不得混用。
- 新角色、日程或事件必须同步 JSON、本地化、对白、资源引用和内容完整性测试。
- 每个事件使用独立 `.dialogue` 文件、唯一静态文本 ID，并且恰好执行一次对应 `complete_event`。
- 未知状态键、对白命令、参数数量、跳转、角色引用或本地化键必须在构建检查中失败，不能静默忽略。
- AI 图像、音乐和音效只用于离线生产。每项采用资产都要记录工具、模型版本、提示词、参考图、日期、可用参数和后处理；运行时不得调用生成模型。
- 大文件按 Git LFS 约定管理；当前目录不是 Git 仓库时，不得声称已经提交或已受 LFS 追踪。

具体步骤见 [内容制作规范](doc/engineering/content-authoring.md)。

## 工作流程

1. 阅读 `doc/README.md` 和与任务相关的设计/工程文档。
2. 用 `rg` 搜索现有 ID、类型、事件和资源；不要从文档猜测源码位置。
3. 先修改纯 Core 规则和测试，再接 Godot 表现层；内容变更先确定状态结果，再写对白。
4. 保持修改范围最小，保留用户已有变更；不要使用破坏性 Git 命令。
5. 业务逻辑变更必须补单元测试；内容变更必须通过内容完整性测试；场景变更必须跑 Godot 冒烟。
6. 同步更新 `doc/`。若改变固定剧情或架构决策，先获得项目负责人确认，并在文档中记录影响。
7. 报告实际执行的验证和未执行项，不得把 headless 冒烟当成完整人工验收。

## 验证命令

在项目根目录使用 PowerShell。当前工作机示例：

```powershell
$GodotRoot = 'D:\ProgramFiles\Godot'
$GodotConsole = Join-Path $GodotRoot 'Godot_v4.6.3-stable_mono_win64_console.exe'
$GodotPackages = Join-Path $GodotRoot 'GodotSharp\Tools\nupkgs'

& 'C:\Program Files\dotnet\dotnet.exe' build .\src\TownRpg.Core\TownRpg.Core.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\TownRpg.Core.Tests\TownRpg.Core.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' restore .\TownRpg.csproj --source $GodotPackages --ignore-failed-sources
& 'C:\Program Files\dotnet\dotnet.exe' build .\TownRpg.csproj --configuration Debug --no-restore
& $GodotConsole --headless --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn'
```

换机时只替换本机绝对路径，不得改变版本。冒烟必须出现：

```text
GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=8 items=8
```

人工验收、性能和导出要求见 [测试与发布](doc/production/testing-release.md)。

## 明确不做

- 不做通用犯罪、农耕、制作、经营、开放世界、随机战斗、刷怪、随机装备或永久死亡。
- 不做大学校园大地图、完整生活职业、自主生成式 NPC 或完整语音。
- 不为原型引入技能树、复杂装备经济或与核心体验无关的新系统。
- 垂直切片盲测无法理解“行为改变后续反馈”时，暂停扩图，优先修复关系、风险提示和事实手册。

## 当前阻断项与提交前检查

截至 2026-08-21：梦境第一室已迁移为 3D MVP，走廊、后三室与 Legacy 小镇仍为 2D；Dialogue Manager v3.10.4 尚未放入 `addons/dialogue_manager/`；Windows 导出模板缺失；梦境密室 30–45 分钟完整人工游玩、手柄、性能和导出尚未验收。旧两日切片仍保留给旧存档与回归，但新游戏不再进入。

提交或交付前必须确认：

- Core 与内容测试全部通过，测试数量变化有解释。
- Godot 工程可编译；涉及资源或场景时冒烟通过。
- 新路径、ID、本地化键、对白跳转和资源引用有效。
- 低关系、失约、错过时间、撤退和战败仍有继续路径。
- `README.md`、`doc/` 和本文件没有把计划内容写成已实现。
- 无 Web/数据库/API/端口相关变更；无未经授权的依赖、引擎或插件升级。
