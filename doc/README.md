# town-rpg 交接文档

> 文档基线：2026-09-01

这套文档面向将同时接手程序、内容、剧情、素材和测试的人。它包含完整剧透。当前源码包含四室梦境序章第一阶段及一个 Legacy 两日切片，不是完整七日版本。

## 当前结论

- `[已实现·待人工终验]` 梦境密室 v3 的四室完整规则已进入新游戏主流程：内容版本 5、28 个正式谜题、34 件道具、11 个安装槽、两轮回访和四个协作机关。洋房及四室均为 Orbit 3D；第一室为电影式 v5 GLB，第二室为程序化 PBR 音乐教室，第三室使用档案兼广播室 v1 GLB，第四室使用旧礼堂 v1 GLB。后两室各 15 个唯一交互点。
- `[已实现·待人工终验]` 需要外部信息的机关均使用独立线索交互，配对距离至少 2 米；未调查时仍可直接尝试，但面板不显示关联记录。第三室信号登记按“上午/下午＋北/南”得到合唱—广播—美术—田径，第四室吊杆组配重为 3—1—4—2；复原母带只播放离线稳定白噪声并同步文字。
- `[已实现]` 梦境顶部任务栏已移除，只保留紧凑区域名；普通道具无需选中，检查机关会自动使用背包中的匹配部件。新游戏进入第一室前显示一次 Esc 与 I/Tab/X 操作确认；真实 `I`/`Tab`/Xbox `X` 背包绑定不含 Backspace，游玩时捕获指针，Esc、模态界面、失焦和转场会释放指针。
- `[已实现·仅本机非商业]` 当前工作副本在第一室和洋房默认加载 `tests/mc-model-test-runtime/caelus-pbr-test.glb`，按实际移动方向转身并播放 `idle/walk/jog`；贴墙近镜头会隐藏网格，资源缺失时回退到原 2D billboard。该角色源与派生资产禁止商业使用和二次分发，已由本地 Git 排除且不进入 Windows 导出，不能作为正式可分发主角交付。
- `[计划·v2 静态候选技术检查通过，待视觉确认]` v1 仍是未绑定、无动画且未过视觉门槛的技术样模；原画式半写实 v2 已生成正式静态 GLB/manifest，并以 `1.7017m`、`34972` 三角面、6 个可见网格、1 个材质面、无 Armature/Action 的结果通过只读检查。manifest 仍为 `runtimeReady=false`，尚未获得负责人视觉确认，也未开始骨骼、动画或 Godot 接入；正式可分发运行时仍以 2D billboard 为安全基线，本机 `mc_model` 覆盖不改变 v2 状态。
- `[已实现·Legacy]` 15–30 分钟“两日生活＋首次异象”源码仍存在并供旧存档继续，但新游戏不再进入，其中林晓雨旧友与四人重逢内容待重写。
- `[已实现]` 纯 C# 规则与内容契约测试 104/104 通过；Godot Debug 零警告编译、后两室 Blender 只读复导、24 张 1920×1080 捕获和九场景 `items=34` 冒烟通过。50–60 分钟后半程、完整 90–120 分钟路径和手柄验收仍待执行。
- `[计划]` Dialogue Manager v3.10.4 尚未 vendored；八个 `.dialogue` 文件已经准备，但运行时仍由 `MainController` 的 C# 演示流程呈现。
- `[计划]` 完整版本目标 3–5 小时、约七个内容日、8 名核心 NPC、双路线、终战和三档结局。
- `[计划]` 完整人工游玩、真实物理鼠标 raw delta、手柄、中低端集显和 Windows x64 导出仍待验收；RTX 2080 Ti 上的 720p/原生 1080p 自动性能与截图检查已完成。

## 推荐阅读顺序

1. 根目录 [AGENTS.md](../AGENTS.md)：所有不可越过的边界。
2. [当前实现](engineering/current-implementation.md)：建立“现在哪里能跑”的准确认识。
3. [架构](engineering/architecture.md)：理解状态、场景、Core 和对白边界。
4. [游戏设计](design/game-design.md)：理解系统为何这样组合。
5. [剧情圣经](design/story-bible.md)：完整幕后真相和角色弧线。
6. [当前梦境密室](design/dream-escape.md)、[v3 设计冻结](design/dream-escape-v3.md)、[v2 历史冻结](design/dream-escape-v2.md)及[剧情流程](design/story-flow.md)：区分当前可玩序章、历史规则和 Day 1–7 规格。
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
├─ art/                          Blender 可编辑源、教室/洋房资产、主角 v1 技术样模、v2 静态候选制作源与正式参考（Blender 子目录不进入 Godot 导入扫描）
├─ assets/generated/             AI 标题/梦境资产、第一室与洋房 3D GLB、第二室离线 WAV、风格锚点和来源清单
├─ data/                         角色、日程、事件、物品 JSON
├─ dialogue/events/              八个切片 Dialogue Manager 源文件
├─ localization/                 简体中文 CSV 与 Godot 导入资源
├─ scenes/                       主场景、Legacy 世界、梦境六场景、引擎冒烟
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
2. 执行 [测试与发布](production/testing-release.md)中的 Core 测试和九场景冒烟，记录本机路径差异。
3. 手工完成四室主路径及五个区域的大头针路径，再用旧存档回归 Legacy 两日流程。
4. 对照 [当前实现](engineering/current-implementation.md)记录任何文档与实际行为不一致之处。
5. 优先接入 Dialogue Manager v3.10.4；不要在此之前扩写 Day 3 地图和大量对白。
6. 完成切片盲测后再按[路线图](production/roadmap-handoff.md)扩展完整内容。

## 不适用项

项目没有 Controller、Web 页面、外部 API、数据库或 SQL schema，因此不建立 `doc/api/`、Web 自测或 `sql/schema.sql`。Windows 游戏的等价验收是 Core 测试、Godot 冒烟、人工游玩和导出检查。
