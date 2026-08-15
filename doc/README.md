# town-rpg 交接文档

> 文档基线：2026-08-15

这套文档面向将同时接手程序、内容、剧情、素材和测试的人。它包含完整剧透。当前源码是一个可编译并通过引擎冒烟的垂直切片，不是完整七日版本。

## 当前结论

- `[已实现]` 15–30 分钟“两日生活＋首次异象”源码：标题、改名、两张地图、四名可见 NPC、日程、关系、承诺、手册、工程告示、风险选择、情境战和单槽存档。
- `[已实现]` 纯 C# 规则与内容契约测试 71/71 通过；Godot Debug/Release 编译、主场景启动、三场景冒烟和 Compatibility 标题取帧通过。
- `[计划]` Dialogue Manager v3.10.4 尚未 vendored；八个 `.dialogue` 文件已经准备，但运行时仍由 `MainController` 的 C# 演示流程呈现。
- `[计划]` 完整版本目标 3–5 小时、约七个内容日、8 名核心 NPC、双路线、终战和三档结局。
- `[计划]` 完整人工游玩、手柄、1080p 缩放、性能和 Windows x64 导出仍待验收。

## 推荐阅读顺序

1. 根目录 [AGENTS.md](../AGENTS.md)：所有不可越过的边界。
2. [当前实现](engineering/current-implementation.md)：建立“现在哪里能跑”的准确认识。
3. [架构](engineering/architecture.md)：理解状态、场景、Core 和对白边界。
4. [游戏设计](design/game-design.md)：理解系统为何这样组合。
5. [剧情圣经](design/story-bible.md)：完整幕后真相和角色弧线。
6. [剧情流程](design/story-flow.md)：Day 1–7 场景级规格。
7. [内容制作](engineering/content-authoring.md)：新增内容时使用。
8. [测试与发布](production/testing-release.md)及[路线图](production/roadmap-handoff.md)：选择下一项工作并交付。

## 权威关系

文档中的标签含义：

| 标签 | 含义 |
| --- | --- |
| `[已实现]` | 当前源码或数据已经具备；仍要参考验证状态。 |
| `[计划]` | 已决定方向，但当前不可游玩或只存在规则骨架。 |
| `[固定约束]` | 技术、主题或因果红线，修改前必须获得负责人确认。 |
| `[可调整草案]` | 为接手者补足的制作方案，可以调整具体场景、名称和节奏。 |

判断“现在如何运行”时，以测试和源码为准；判断“故事真相是什么”时，以剧情圣经的固定约束为准。发现冲突不能私下择一，应修复冲突并注明影响。

## 项目结构速览

```text
town-rpg/
├─ project.godot / TownRpg.csproj / TownRpg.sln
├─ assets/generated/             AI 标题资产、风格锚点和来源清单
├─ data/                         角色、日程、事件 JSON
├─ dialogue/events/              八个切片 Dialogue Manager 源文件
├─ localization/                 简体中文 CSV 与 Godot 导入资源
├─ scenes/                       主场景、两张世界场景、引擎冒烟场景
├─ scripts/application/          会话、状态桥、内容仓库、模态门
├─ scripts/presentation/         世界、UI、对白演示、手册和战斗
├─ src/TownRpg.Core/             不依赖 Godot 的领域状态与规则
├─ tests/TownRpg.Core.Tests/     规则、存档和内容完整性测试
├─ doc/                          本交接文档
└─ outputs/ / work/              交付物与工作区，不属于游戏程序集
```

自动生成的 `.godot/`、`bin/`、`obj/` 和 `.uid` 不作为架构文档的维护对象。

## 关键术语

- **事实 Facts**：玩家已经确认的世界事实，用于剧情条件。
- **证据 Evidence**：可向居民或公司成员证明说法的材料，用于救援、倒戈和结局。
- **线索 Leads**：尚待调查的方向，不代表已经证实。
- **结果 Outcome**：一次事件提交的原子状态变更集合，使用稳定 ID 防重复应用。
- **暴露 Exposure**：林晓雨身份从无人注意到确认的四阶段风险。
- **谨慎路线**：身份未确认时秘密潜入并破坏一个仪式节点。
- **救援路线**：身份确认后林晓雨被绑架，玩家依赖既有信赖组织救援。
- **情境战**：以调查、保护、安抚、破坏和制服为目标的非致命战斗。

## 接手第一周

1. 用 Godot 4.6.3 Mono 打开 `project.godot`，按 `F5` 确认标题和新游戏可启动。
2. 执行 [测试与发布](production/testing-release.md)中的 Core 测试和三场景冒烟，记录本机路径差异。
3. 手工完成两日流程，至少覆盖隐藏能力、冒险共鸣、战败重试、撤退再挑战和存读档。
4. 对照 [当前实现](engineering/current-implementation.md)记录任何文档与实际行为不一致之处。
5. 优先接入 Dialogue Manager v3.10.4；不要在此之前扩写 Day 3 地图和大量对白。
6. 完成切片盲测后再按[路线图](production/roadmap-handoff.md)扩展完整内容。

## 不适用项

项目没有 Controller、Web 页面、外部 API、数据库或 SQL schema，因此不建立 `doc/api/`、Web 自测或 `sql/schema.sql`。Windows 游戏的等价验收是 Core 测试、Godot 冒烟、人工游玩和导出检查。
