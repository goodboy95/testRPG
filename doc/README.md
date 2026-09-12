# town-rpg 交接文档

> 文档基线：2026-09-12

这套文档面向将同时接手程序、内容、剧情、素材和测试的人。它包含完整剧透。当前源码包含四室梦境序章第一阶段及一个 Legacy 两日切片，不是完整七日版本。

## 当前结论

- `[已实现·仅本机非商业]` 林晓雨现使用流萤“春日手信”3D 外观，隐藏剑；两人各 12 段动作、五区平滑跟随、到位后协作及可中断交互已接入。模型缺失继续 billboard 回退，不改变正式角色候选。见[双角色动作](engineering/character-motion.md)与[验收](production/character-motion-v3-validation.md)。

- `[已实现]` 序章 v4（内容版本 6）：四室、12 组主谜题/19 个规则节点、31 件道具、11 个安装槽、一次集中回访；13 段序章对白经 Dialogue Manager v3.10.4 播放。目标 60–90 分钟仍待至少 3 名剧情 RPG 玩家盲测。 四室交互点为 11/18/13/15，采用 Orbit 3D；第一室已重制为 12 组课桌的 v6 环境。
- `[已实现]` 独立线索可重复检视；纸带断口、合影叠片和舞台双视角使用图形面板及文字替代。最后交接按实际四步协作执行，无倒计时，预览/站位不持久化。
- `[已实现]` 梦境顶部任务栏已移除，只保留紧凑区域名；普通道具无需选中，检查机关会自动使用背包中的匹配部件。新游戏进入第一室前显示一次 Esc 与 I/Tab/X 操作确认；真实 `I`/`Tab`/Xbox `X` 背包绑定不含 Backspace，游玩时捕获指针，Esc、模态界面、失焦和转场会释放指针。
- `[已实现·仅本机非商业]` 当前工作副本在四室和洋房默认加载 `tests/mc-model-test-runtime/caelus-pbr-test.glb`，播放新版 `idle/walk/jog` 与左右 90°/180° 转身，连续混合步态，支持 Shift 慢走；贴墙近镜头会隐藏网格，资源缺失时回退到原 2D billboard。该角色源与派生资产禁止商业使用和二次分发，已由本地 Git 排除且不进入 Windows 导出，不能作为正式可分发主角交付。
- `[计划·v2 静态候选技术检查通过，待视觉确认]` v1 仍是未绑定、无动画且未过视觉门槛的技术样模；原画式半写实 v2 已生成正式静态 GLB/manifest，并以 `1.7017m`、`34972` 三角面、6 个可见网格、1 个材质面、无 Armature/Action 的结果通过只读检查。manifest 仍为 `runtimeReady=false`，尚未获得负责人视觉确认，也未开始骨骼、动画或 Godot 接入；正式可分发运行时仍以 2D billboard 为安全基线，本机 `mc_model` 覆盖不改变 v2 状态。
- `[已实现·Legacy]` 15–30 分钟“两日生活＋首次异象”源码仍存在并供旧存档继续，但新游戏不再进入，其中林晓雨旧友与四人重逢内容待重写。
- `[已实现]` Core/内容/移动及 UI 规则 151 项通过（145 基线＋6 项跟随/协作计时用例），Godot 编译、九场景冒烟和 13 段对白专项验证见各次验收记录。新版节奏和完整人工路径仍待盲测。
- `[已实现]` 09-08 全 UI 主题作为未重做页面的基线保留，机关支持直接操作；机关关联资料已移除，独立记录和合作观察保留。19 个题面不再直接给解法，加入前失败仅回想已知材料，加入后主动三级商量只给局部建议。规则与当时 180 张矩阵截图见[角色提示](engineering/puzzle-feedback.md)和[验收](production/dream-hints-validation.md)。09-08 的 Figma、288 张图作为历史保留，边界见[UI 规范](engineering/ui-ux.md)。
- `[已实现]` Dialogue Manager v3.10.4 已接入 13 段序章对白，结尾原子提交，取消可重试；8 段 Legacy 演示对白仍待迁移。
- `[已实现]` 更小的电影感 UI 已接入对白、梦境探索、背包和梦中记录；菜单与字幕字号独立。`[计划]` 样板视觉确认后统一其余 UI。见[第一阶段样板](engineering/ui-cinematic-samples.md)和[验收记录](production/ui-cinematic-validation.md)。
- `[计划]` 完整版本目标 3–5 小时、约七个内容日、8 名核心 NPC、双路线、终战和三档结局。
- `[计划]` 完整人工游玩、真实物理鼠标 raw delta、手柄、中低端集显和 Windows x64 导出仍待验收；RTX 2080 Ti 上的 720p/原生 1080p 自动性能与截图检查已完成。

## 推荐阅读顺序

1. 根目录 [AGENTS.md](../AGENTS.md)：所有不可越过的边界。
2. [当前实现](engineering/current-implementation.md)：建立“现在哪里能跑”的准确认识。
3. [架构](engineering/architecture.md)：理解状态、场景、Core 和对白边界。
4. [游戏设计](design/game-design.md)：理解系统为何这样组合。
5. [剧情圣经](design/story-bible.md)：完整幕后真相和角色弧线。
6. [当前梦境密室](design/dream-escape.md)、[v4 当前重梳](design/dream-escape-v4.md)、[v3 历史冻结](design/dream-escape-v3.md)、[v2 历史冻结](design/dream-escape-v2.md)及[剧情流程](design/story-flow.md)：区分当前可玩序章、历史规则和 Day 1–7 规格。
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
├─ dialogue/events/              13 个序章与 8 个 Legacy Dialogue Manager 源文件
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
5. 先做至少 3 人的序章盲测，再迁移 Legacy 演示对白；不要据自动化结果扩写大量 Day 3 内容。
6. 完成切片盲测后再按[路线图](production/roadmap-handoff.md)扩展完整内容。

## 不适用项

项目没有 Controller、Web 页面、外部 API、数据库或 SQL schema，因此不建立 `doc/api/`、Web 自测或 `sql/schema.sql`。Windows 游戏的等价验收是 Core 测试、Godot 冒烟、人工游玩和导出检查。

`[已实现]` 第一室 v6 建模、宽通道与回访避障，详见[第一室建模与布局 v6](engineering/classroom-remodel-v6.md)和[实际验收记录](production/classroom-v6-validation.md)。

`[已实现]` 洋房宽前厅、短内廊与封闭楼梯景观见[大厅 v3](engineering/manor-remodel-v3.md)和[验收记录](production/manor-v3-validation.md)。

`[已实现]` 第二室已采用独立音乐教室 v2 GLB：12×10×4 米、可进入器材间、六把椅子、分阶段门碰撞和晓雨连续绕行。见[实现](engineering/music-remodel-v2.md)与[验收](production/music-v2-validation.md)。

`[已实现]` 后两室已扩大重制：档案室 14×12×4.2 米，礼堂 22×16×7.5 米、32 座共享网格；13／15 个交互 ID、答案、一次集中回访与内容版本 6 保持。见[档案室](engineering/archive-remodel-v2.md)、[礼堂](engineering/auditorium-remodel-v2.md)及[完整流程与两室验收](production/late-rooms-v2-validation.md)。
