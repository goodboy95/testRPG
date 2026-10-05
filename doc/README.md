# town-rpg 交接文档

`[已实现]` 2026-10-05：[三场景环境精修样板](engineering/environment-refinement-v1.md)提供卧室、街段、梦境教室的新旧对照，包含表盘、标牌与纸面图像。正式入口不变，美术待负责人比较；[实际验证与限制](production/environment-refinement-v1-validation.md)。

`[已实现]` 2026-10-04：[任务跟踪、文案与字号](engineering/quest-tracking-typography.md)已接入正式开场的事项选择与分段目标；梦境不加位置跟踪。负责人已确认舒适 C 并统一正式默认，原版与 A／B／C 原生预览保留。[实际验证与人工边界](production/quest-tracking-validation.md)。

`[已实现]` 2026-10-02：[正式村庄开场](engineering/village-opening.md)提供七位功能居民、送凳／订货／合作推车、事项记录与保存恢复；[本轮运行验收](production/village-opening-validation.md)与纸面、美术及盲测分别记录。后半章仍是后续设计，正文统一在第一章细案。

`[可调整草案]` 2026-10-02 文稿轮（早于正式接入）：[第一章开场文稿](design/chapter-one.md)提供七位 NPC 人物卡、六场对白、搬凳／订货／推车操作、结款与再访反馈及后续任务纲要。正文统一留在细案；[本轮文档检查](production/chapter-one-doc-validation.md)与未来可玩、美术、时长验收分开。本轮仅 Markdown，正式村庄仍未接入委托。

`[已实现]` 独立样板，待视觉确认。 2026-09-26：[村庄动画风格 v8](engineering/village-anime-v8.md)先制作主角家、广场、两铺和代表树，正式与默认仍为 v6；[实际验收与未完成项](production/village-anime-v8-validation.md)。负责人确认样板后再推广全村。

`[已实现]` 2026-09-21：[成熟完整角色来源初筛与比较页](production/character-mature-source-screening.md)。八项筛查，没有合格免费项；一项付费观察未放行，无下载／购买／本机模型验证，MPFB 保留为对照。

`[已实现]` 2026-09-20 后续：[正常脸确认后的头发与便服样板](production/character-casual-preview.md)。v001 已获 G1 正常脸确认，v007-casual 增加短发、衬衫牛仔裤与运动鞋，仅独立静态审图。

`[已实现]` 2026-09-20：[MPFB 人物工作流 M0/M1](engineering/character-workflow-mpfb.md)与[实际验收/审图](production/character-workflow-mpfb-validation.md)。原生成年男性基线和独立静态场景可复现，批处理闭环通过，MCP 未接通。G1 由用户最终确认，G2–G5 未测试；新授权允许许可合格的 MPFB 候选，旧模型及受限主角未复用。

`[已实现]` 2026-09-18：[沈砚静态 v2 候选](engineering/shen-yan-static-v2.md)与[本轮验收](production/shen-yan-static-v2-validation.md)。旧模块化样板已退役；新源使用独立 CC0 解剖基体，先验证单人的静态外观。美术未批准、未绑定、未制作 LOD 或批量外观，正式村庄仍无居民；[旧失败记录](engineering/village-npc-art-pilot.md)仅作历史。

`[已实现]` 2026-09-16：[村庄 NPC 首批试点](engineering/village-npc-pilot.md)与[实际验收](production/village-npc-pilot-validation.md)。独立 v7 中的乔姨、沈砚可交谈，正式村庄仍无居民，第一章任务未接入。

`[已实现]` 2026-09-16：[村庄分区与 LOD v7 独立预览](engineering/village-streaming-v7.md) · [GTX 1060 笔记本预算验证](production/village-streaming-v7-1060-validation.md) · [旧集显档历史记录](production/village-streaming-v7-validation.md)。正式／默认村庄入口仍为 v6；外景提交预算与整个渲染器的帧时／draw call 分别验收。

> 2026-09-15 正式采用：负责人已确认将 v6（四户住宅 v5、主角家暖色外观、全村树木）与配套住宅外观启用。正式游戏和默认村庄预览现用 v6，沿用修复后的 v4 布局／地形；独立预览 `--village-v4` 保留旧外观回归。第一章委托未接入。[采用与验证](production/village-v6-adoption.md)。下述样板轮“尚未切换”是当轮交付状态。


> 2026-09-15 本轮更新：[主角家辨识与全村树木 v6](engineering/village-home-trees-v6.md) · [实际验收](production/village-home-trees-v6-validation.md)

`[已实现]` 2026-09-15：[四户住宅美术样板 v5](engineering/village-houses-v5.md)提供独立漫游，四户的贴图、院墙和生活摆设有明确区别；沿用已修复的 v4 地形和布局。正式与默认预览仍为 v4，外观确认后再切换；[实际验收](production/village-houses-v5-validation.md)与人工确认单列，第一章委托尚未接入。

`[已实现]` 2026-09-14 后续布局轮：[紧凑村庄 v4](engineering/village-v4.md)已接入正式醒后探索与默认预览，采用 v3 树木／两铺外观和独立派生 Terrain3D。固定主角家、溪流、双桥及 24 栋体量；原布局通过独立预览 `--village-v2` 回归，v1 与 v3 样板入口保留。第一章委托仍未接入。[本轮实际验证](production/village-v4-validation.md)独立记录；下述旧批次的“正式 v2／样板待采用”均为当时状态。

`[已实现]` 2026-09-14 [树木与两间铺面 v3 样板](engineering/village-art-v3.md)可独立漫游，正式游戏与默认预览尚未切换。三棵树与两铺采用 CC0 材质和独立派生源；[本轮验证](production/village-art-v3-validation.md)与负责人确认单列。

`[已实现]` 2026-09-14 赶路优化：村庄 v2 正式探索及独立预览室外为 4.2 米/秒，住宅／梦境／v1 回归仍为 3.1；见[分批计划](design/village-optimization.md)和[本轮验收](production/village-travel-validation.md)。地形三处独立样板已制作，见[制作](engineering/village-terrain-sample.md)和[验收](production/village-terrain-validation.md)；负责人确认后已接入正式村庄与默认 v2 预览，见[采用验收](production/village-terrain-adoption.md)；全村其余精修仍为后续工作，两铺材质由下述 v3 样板制作，尚未正式采用。

`[已实现]` 2026-09-14 后续接入：[梦境醒来与村庄自由探索](engineering/village-wake.md)。解谜成功／大头针刺醒后进入周六卧室，住宅和村庄可往返，位置及门灯写入正式存档；第一章委托、居民、森林和第二章尚未接入。此前建模轮的独立预览和验收记录继续保留，不代表本轮验证。


> 文档基线：2026-09-14

这套文档面向将同时接手程序、内容、剧情、素材和测试的人。当前源码包含四室梦境序章第一阶段及一个 Legacy 两日切片；醒后自由探索已接入，第一章委托尚未接入。当前剧情方向与旧版完整剧透分开维护。

## 分类文档索引

从 [AGENTS.md](../AGENTS.md) 到本表一次点击，再到专项文档一次点击。剧情细案、通用工程与制作规范也在 AGENTS.md 有直接入口。各项状态以目标文档及其日期为准。

| 类别 | 文档地址 |
| --- | --- |
| 当前剧情 | [剧情圣经](design/story-bible.md)、[章节流程](design/story-flow.md)、[第一章细案](design/chapter-one.md)、[剧情速览](../剧情大纲.md) |
| 玩法与现状 | [游戏设计](design/game-design.md)、[当前实现](engineering/current-implementation.md)、[项目结构](structure.md) |
| 工程与内容制作 | [架构](engineering/architecture.md)、[内容制作规范](engineering/content-authoring.md)、[路线图](production/roadmap-handoff.md) |
| 梦境剧情与规则 | [梦境概述](design/dream-escape.md)、[序章 v4](design/dream-escape-v4.md)、[模块说明](modules/dream-escape.md)、[角色提示](engineering/puzzle-feedback.md) |
| 村庄体验与美术优化 | [分批计划](design/village-optimization.md)、[地形样板](engineering/village-terrain-sample.md)、[地形验收](production/village-terrain-validation.md)、[赶路验收](production/village-travel-validation.md) |
| 村庄紧凑布局 | [v4 制作与入口](engineering/village-v4.md)、[v4 实际验收](production/village-v4-validation.md) |
| 树木与铺面样板 | [v3 制作](engineering/village-art-v3.md)、[v3 验收](production/village-art-v3-validation.md) |
| 四户住宅美术样板 | [v5 制作与入口](engineering/village-houses-v5.md)、[v5 实际验收](production/village-houses-v5-validation.md) |
| 门交互与院外通行修复 | [规则、保存与编辑](engineering/home-access.md)、[实际验收](production/home-access-validation.md) |
| 村庄与住宅制作 | [村庄外景设计](design/village-exterior.md)、[村庄 v2](engineering/village-v2.md)、[村庄 v1](engineering/village-v1.md)、[住宅 v1](engineering/home-v1.md) |
| 梦境场景制作 | [第一室 v6](engineering/classroom-remodel-v6.md)、[大厅 v3](engineering/manor-remodel-v3.md)、[音乐室 v2](engineering/music-remodel-v2.md)、[档案室 v2](engineering/archive-remodel-v2.md)、[礼堂 v2](engineering/auditorium-remodel-v2.md) |
| 角色、UI 与资产规范 | [MPFB 工作流](engineering/character-workflow-mpfb.md)、[正常脸基线验收](production/character-workflow-mpfb-validation.md)、[沈砚静态候选](engineering/shen-yan-static-v2.md)、[静态候选验收](production/shen-yan-static-v2-validation.md)、[NPC 功能试点](engineering/village-npc-pilot.md)、[角色动作](engineering/character-motion.md)、[UI 规范](engineering/ui-ux.md)、[电影感 UI 样板](engineering/ui-cinematic-samples.md)、[资产制作与来源](engineering/content-authoring.md) |
| 测试与人工验收 | [测试与发布](production/testing-release.md)、[序章盲测](production/prologue-v6-playtest.md)、[第一章文档检查](production/chapter-one-doc-validation.md)、[村庄外景文档检查](production/village-exterior-doc-validation.md) |
| 正式村庄探索 | [接入与存档](engineering/village-wake.md)、[本轮验收](production/village-wake-validation.md) |
| 场景验收记录 | [村庄 v2](production/village-v2-validation.md)、 [村庄](production/village-v1-validation.md)、[住宅](production/home-v1-validation.md)、[第一室](production/classroom-v6-validation.md)、[大厅](production/manor-v3-validation.md)、[音乐室](production/music-v2-validation.md)、[档案室与礼堂](production/late-rooms-v2-validation.md) |
| 动作与 UI 验收记录 | [双角色动作](production/character-motion-v3-validation.md)、[UI 重制](production/ui-remake-validation.md)、[电影感 UI](production/ui-cinematic-validation.md)、[角色提示](production/dream-hints-validation.md) |
| 历史剧情与旧规格 | [旧剧情圣经](design/story-bible-legacy.md)、[旧流程](design/story-flow-legacy.md)、[梦境 v2](design/dream-escape-v2.md)、[梦境 v3](design/dream-escape-v3.md) |

## 当前结论

- `[已实现]` 2026-09-14 后续建模：广场、杂货铺、修理铺第一批 v2 已接入独立漫游，静态物件与碰撞，不含居民或剧情交互。默认预览 v2，显式 `--village-v1` 回归，基础村庄与住宅源保留；见[制作](engineering/village-v2.md)与[本轮验收](production/village-v2-validation.md)。后面的文档交付说明保留前一阶段范围。

- `[计划]` 2026-09-14 确认：第一章为安全村庄与附近森林，村内无敌对遭遇、可自愿陪练；林缘劳作和两场动物战均在森林，最终从森林小屋取箱。当前先交付[村庄外景设计](design/village-exterior.md)：保留路桥和主角家骨架，重点地点详设、24 栋分级，森林及小屋另轮设计；模型、场景、剧情运行时均未改变。

- `[计划]` 2026-09-13 确认：主角是一直生活在村中的高中生，父母两年前外出务工后失踪，独居且由长辈照应，课余帮忙挣钱。第一章周六先独自办事、前中期邀请沈砚，三条必要日常链完成后登记取旧工具箱；正常排查无有效方向，以疑惑收尾，可继续支线或睡觉。第二章周日早上收到下午返校通知，上午续办剩余事项。晓雨第一章现实不出场，父母与工具箱真相有意留白。 见[剧情圣经](design/story-bible.md)、[章节流程](design/story-flow.md)与[第一章细案](design/chapter-one.md)。本轮只改文档，新现实内容尚未接入。

- `[已实现·仅本机非商业]` 林晓雨现使用流萤“春日手信”3D 外观，隐藏剑；两人各 12 段动作、五区平滑跟随、到位后协作及可中断交互已接入。模型缺失继续 billboard 回退，不改变正式角色候选。见[双角色动作](engineering/character-motion.md)与[验收](production/character-motion-v3-validation.md)。

- `[已实现]` 序章 v4（内容版本 6）：四室、12 组主谜题/19 个规则节点、31 件道具、11 个安装槽、一次集中回访；13 段序章对白经 Dialogue Manager v3.10.4 播放。目标 60–90 分钟仍待至少 3 名剧情 RPG 玩家盲测。 四室交互点为 11/18/13/15，采用 Orbit 3D；第一室已重制为 12 组课桌的 v6 环境。
- `[已实现]` 独立线索可重复检视；纸带断口、合影叠片和舞台双视角使用图形面板及文字替代。最后交接按实际四步协作执行，无倒计时，预览/站位不持久化。
- `[已实现]` 梦境顶部任务栏已移除，只保留紧凑区域名；普通道具无需选中，检查机关会自动使用背包中的匹配部件。新游戏进入第一室前显示一次 Esc 与 I/Tab/X 操作确认；真实 `I`/`Tab`/Xbox `X` 背包绑定不含 Backspace，游玩时捕获指针，Esc、模态界面、失焦和转场会释放指针。
- `[已实现·仅本机非商业]` 当前工作副本在四室和洋房默认加载 `tests/mc-model-test-runtime/caelus-pbr-test.glb`，播放新版 `idle/walk/jog` 与左右 90°/180° 转身，连续混合步态，支持 Shift 慢走；贴墙近镜头会隐藏网格，资源缺失时回退到原 2D billboard。该角色源与派生资产禁止商业使用和二次分发，已由本地 Git 排除且不进入 Windows 导出，不能作为正式可分发主角交付。
- `[计划·v2 静态候选技术检查通过，待视觉确认]` v1 仍是未绑定、无动画且未过视觉门槛的技术样模；原画式半写实 v2 已生成正式静态 GLB/manifest，并以 `1.7017m`、`34972` 三角面、6 个可见网格、1 个材质面、无 Armature/Action 的结果通过只读检查。manifest 仍为 `runtimeReady=false`，尚未获得负责人视觉确认，也未开始骨骼、动画或 Godot 接入；正式可分发运行时仍以 2D billboard 为安全基线，本机 `mc_model` 覆盖不改变 v2 状态。
- `[已实现·Legacy]` 15–30 分钟“两日生活＋首次异象”源码仍存在并供旧存档继续，但新游戏不再进入。其中林晓雨旧友、四人重逢与三选一活动仅属历史，不作为新第一章的入口。
- `[已实现]` Core/内容/表现层规则最新 182 项通过（赶路基线 180＋2 项地形输入和保护区检查），Godot 编译、九场景冒烟和 13 段对白专项验证见各次验收记录。新版节奏和完整人工路径仍待盲测。
- `[已实现]` 村庄 3D 独立入口 `scenes/testing/VillagePreview.tscn`（默认 v2，`--village-v1` 回归原版）：24 栋建筑、两座桥、溪岸与旧站回环，院路连续，正门往返已有住宅室内。该独立入口没有居民／任务，也不访问正式存档；正式醒后探索另见[接入说明](engineering/village-wake.md)。见[制作与启动](engineering/village-v1.md)、[路线/转场/72 图验收](production/village-v1-validation.md)。
- `[已实现]` 主角家 3D v1 独立漫游，两卧一厅、独立厨卫、小院和完整外观，五处房门、六组灯可操作。入口 `scenes/testing/HomePreview.tscn`；该独立入口的住宅状态仅属于当前实例，不写正式存档；正式醒后探索另见[接入说明](engineering/village-wake.md)。见[住宅制作与启动](engineering/home-v1.md)与[179 项专项及 84 张截图验收](production/home-v1-validation.md)。
- `[已实现]` 09-08 全 UI 主题作为未重做页面的基线保留，机关支持直接操作；机关关联资料已移除，独立记录和合作观察保留。19 个题面不再直接给解法，加入前失败仅回想已知材料，加入后主动三级商量只给局部建议。规则与当时 180 张矩阵截图见[角色提示](engineering/puzzle-feedback.md)和[验收](production/dream-hints-validation.md)。09-08 的 Figma、288 张图作为历史保留，边界见[UI 规范](engineering/ui-ux.md)。
- `[已实现]` Dialogue Manager v3.10.4 已接入 13 段序章对白，结尾原子提交，取消可重试；8 段 Legacy 演示对白仍待迁移。
- `[已实现]` 更小的电影感 UI 已接入对白、梦境探索、背包和梦中记录；菜单与字幕字号独立。`[计划]` 样板视觉确认后统一其余 UI。见[第一阶段样板](engineering/ui-cinematic-samples.md)和[验收记录](production/ui-cinematic-validation.md)。
- `[可调整草案]` 原 3–5 小时、七个内容日、固定居民总数、双路线与三档结局均属旧稿规模；新故事总章数、总时长、后续地点和终局未定。旧稿见[历史剧情圣经](design/story-bible-legacy.md)与[历史流程](design/story-flow-legacy.md)。
- `[计划]` 完整人工游玩、真实物理鼠标 raw delta、手柄、中低端集显和 Windows x64 导出仍待验收；RTX 2080 Ti 上的 720p/原生 1080p 自动性能与截图检查已完成。

## 推荐阅读顺序

1. 根目录 [AGENTS.md](../AGENTS.md)：所有不可越过的边界。
2. [当前实现](engineering/current-implementation.md)：建立“现在哪里能跑”的准确认识。
3. [架构](engineering/architecture.md)：理解状态、场景、Core 和对白边界。
4. [游戏设计](design/game-design.md)：理解系统为何这样组合。
5. [剧情圣经](design/story-bible.md)：当前世界观、人物边界、已确认方向与未定真相。
6. [当前梦境密室](design/dream-escape.md)、[v4 当前重梳](design/dream-escape-v4.md)及[醒后剧情流程](design/story-flow.md)：区分可玩序章和未实现的现实开篇；[旧剧情](design/story-bible-legacy.md)、[旧流程](design/story-flow-legacy.md)、[v3](design/dream-escape-v3.md)与[v2](design/dream-escape-v2.md)只作历史参考。
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

第一章详细正文统一维护在[细案](design/chapter-one.md)；[剧情圣经](design/story-bible.md)保存约束，[流程](design/story-flow.md)保存章节关系。**有意留白**使用 `[固定约束]` 加说明，禁止自行补全，不新增状态标签。

判断“现在如何运行”时，以测试和源码为准；当前故事以剧情圣经的已确认方向和固定约束为准。未定真相不得从旧稿自动补齐，历史文件中的旧标签也不覆盖现行决策。发现冲突应修复并注明影响。

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
- **证据 Evidence**：可向他人出示以支持说法的材料；公司取证、救援、倒戈和结局判定是旧版用途，不限定新剧情。
- **线索 Leads**：尚待调查的方向，不代表已经证实。
- **结果 Outcome**：一次事件提交的原子状态变更集合，使用稳定 ID 防重复应用。
- **暴露 Exposure（旧版规则）**：林晓雨身份从无人注意到确认的四阶段风险；现有状态/规则保留，不预定新剧情使用。
- **谨慎路线（旧稿）**：身份未确认时秘密潜入并破坏一个仪式节点。
- **救援路线（旧稿）**：身份确认后林晓雨被绑架，玩家依赖既有信赖组织救援；新主线没有预定这一分线。
- **情境战**：以调查、保护、安抚、破坏和制服为目标的非致命战斗。

## 接手第一周

1. 用 Godot 4.6.3 Mono 打开 `project.godot`，按 `F5` 确认标题和新游戏可启动。
2. 执行 [测试与发布](production/testing-release.md)中的 Core 测试和九场景冒烟，记录本机路径差异。
3. 手工完成四室主路径及五个区域的大头针路径，再用旧存档回归 Legacy 两日流程。
4. 对照 [当前实现](engineering/current-implementation.md)记录任何文档与实际行为不一致之处。
5. 继续至少 3 人的序章盲测；新现实剧情依[第一章细案](design/chapter-one.md)写场景与对白草稿，来源真相继续留白，不从 Legacy 或旧 Day 3 内容直接扩写。
6. 按[路线图](production/roadmap-handoff.md)区分叙事设计、已有内容维护和未来运行时接入。

## 不适用项

项目没有 Controller、Web 页面、外部 API、数据库或 SQL schema，因此不建立 `doc/api/`、Web 自测或 `sql/schema.sql`。Windows 游戏的等价验收是 Core 测试、Godot 冒烟、人工游玩和导出检查。

`[已实现]` 第一室 v6 建模、宽通道与回访避障，详见[第一室建模与布局 v6](engineering/classroom-remodel-v6.md)和[实际验收记录](production/classroom-v6-validation.md)。

`[已实现]` 洋房宽前厅、短内廊与封闭楼梯景观见[大厅 v3](engineering/manor-remodel-v3.md)和[验收记录](production/manor-v3-validation.md)。

`[已实现]` 第二室已采用独立音乐教室 v2 GLB：12×10×4 米、可进入器材间、六把椅子、分阶段门碰撞和晓雨连续绕行。见[实现](engineering/music-remodel-v2.md)与[验收](production/music-v2-validation.md)。

`[已实现]` 后两室已扩大重制：档案室 14×12×4.2 米，礼堂 22×16×7.5 米、32 座共享网格；13／15 个交互 ID、答案、一次集中回访与内容版本 6 保持。见[档案室](engineering/archive-remodel-v2.md)、[礼堂](engineering/auditorium-remodel-v2.md)及[完整流程与两室验收](production/late-rooms-v2-validation.md)。

`[已实现·静态试验，未过美术验收]` [人物第2版](production/character-stylized-v2.md)：v007 派生三轮造型，交付源为 v010；双引擎实际渲染与未通过项分别记录。

`[已实现·未过美术验收]` [自然青年头肩修订](production/character-head-refinement.md)：v012/v014 脸部检查通过，v016 头发仍有头套边界及折转，整体未放行；技术、代理判断与用户批准分开记录。
