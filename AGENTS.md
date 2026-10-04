# town-rpg 项目代理指南

本文件适用于根目录及全部子目录。任何人或自动化代理修改项目之前都必须先阅读本文件，再从 [doc/README.md](doc/README.md) 进入详细交接文档。

## 任务跟踪、文案与字号（2026-10-04）

- `[已实现]` 后续负责人确认采用舒适 C：正式 UI 默认思源黑体正文 10／对白 11／辅助 8／标题 14，行距 4、按钮最小高 22（640×360 逻辑尺寸）；旧版与 A／B／C 独立对照保留。
- `[固定约束]` 负责人授权将当前全部可提交修改做本机 Git 提交，替代各较早批次“不提交”的阶段限制；不推送、不导出游戏，受限角色源／镜像／动作及含其媒体仍仅本机保留。提交样板不改变其美术或正式采用状态。
- `[已实现]` 正式开场支持可选择事项、分段目标、屏边方向与跟踪保存；Core 推导语义候选，Godot 绑定地点。当前可玩文案已润色，工程和验证见[专项说明](doc/engineering/quest-tracking-typography.md)、[实际记录](doc/production/quest-tracking-validation.md)。
- `[固定约束]` 梦境不加位置跟踪；醒后新任务默认声明阶段目标，无地点时自然说明，不编造坐标或暴露未知信息。SaveEnvelope v1、梦境内容版本 6 保持。原先字号样板待选定的门槛已由本轮 C 档确认解除。

## 正式村庄开场（2026-10-02 后续授权）

- `[已实现]` 两种正式醒来／既有村庄档接入七位功能 NPC、任务板、送凳、三户小件、邀请沈砚、推车米面配送、事项记录、交差领款与保存恢复。正式村庄为 v6，旧 NPC 试点保留；工程与真实验证见[接入说明](doc/engineering/village-opening.md)、[本轮验收](doc/production/village-opening-validation.md)。
- `[固定约束]` 本轮正式开发授权替代之前“只改 Markdown、不改 Core／存档字段”。新增可选 VillageOpening 状态，SaveEnvelope v1、梦境内容版本 6、Godot 4.6.3、Dialogue Manager 3.10.4、net8.0 不变。森林、后续支线、战斗、工具箱和第二章不开放，合作送货后可自由探索但不能跨日。
- `[已实现]` 用户目录 `C:/Users/duwei/.dotnet` 已安装并校验 SDK 10.0.400 与 Runtime 8.0.31，用户 PATH／DOTNET_ROOT 已配置。旧 C:/Program Files/dotnet 示例需改用这个本机位置。本轮 Core／内容 304 项；新增 5 个角色定义和 17 个事件，原日程／梦境物品数保持。
- `[固定约束]` 七人独立低模源不复用受限角色或退役人物；功能验收与人物自然度、真实手柄、GTX 1060 和 30–40 分钟盲测分开。不提交、不推送、不导出游戏，含受限主角的画面仅本机保留。下方较早批次未接入描述为历史状态。

## 按任务找文档

| 要做的工作 | 直接入口 |
| --- | --- |
| 找所有文档、专项工程与验收 | [项目概览](README.md)、[分类索引与交接首页](doc/README.md)、[项目结构](doc/structure.md) |
| 世界观、人物与剧情红线 | [剧情圣经](doc/design/story-bible.md) |
| 第一章任务、场景与章末事件 | [第一章细案](doc/design/chapter-one.md) |
| 村庄赶路、地形与贴图优化 | [分批优化计划](doc/design/village-optimization.md)、[地形样板](doc/engineering/village-terrain-sample.md)、[地形验收](doc/production/village-terrain-validation.md)、[赶路验收](doc/production/village-travel-validation.md) |
| 村庄紧凑布局与 v4 正式采用 | [制作与版本入口](doc/engineering/village-v4.md)、[实际验收](doc/production/village-v4-validation.md) |
| 树木与两铺独立美术样板 | [v3 制作与入口](doc/engineering/village-art-v3.md)、[实际验收](doc/production/village-art-v3-validation.md) |
| 主角家辨识与全村树木样板 | [v6 制作与入口](doc/engineering/village-home-trees-v6.md)、[实际验收](doc/production/village-home-trees-v6-validation.md) |
| 四户住宅贴图、院落摆设与独立走查 | [v5 制作与入口](doc/engineering/village-houses-v5.md)、[实际验收](doc/production/village-houses-v5-validation.md) |
| 村庄建筑、院落与外景建模设计 | [村庄外景细案](doc/design/village-exterior.md)、[第一批建模 v2](doc/engineering/village-v2.md) |
| 章节推进、第二章接口与历史替代 | [剧情流程](doc/design/story-flow.md) |
| 玩法、时间、战斗与成长 | [游戏设计](doc/design/game-design.md) |
| 双向推门、门方向存档与院外道路阻挡 | [修复说明](doc/engineering/home-access.md)、[实际验收](doc/production/home-access-validation.md) |
| 梦境醒来、村庄正式存档与恢复 | [正式村庄探索](doc/engineering/village-wake.md) |
| 当前能运行什么 | [当前实现](doc/engineering/current-implementation.md) |
| 模块边界、状态与存档 | [架构](doc/engineering/architecture.md) |
| 写事件、对白、角色与资源 | [内容制作规范](doc/engineering/content-authoring.md) |
| 构建、测试、人工验收与发布 | [测试与发布](doc/production/testing-release.md) |
| 下一步工作与接入门槛 | [交接路线图](doc/production/roadmap-handoff.md) |

第一章详细正文只维护在细案中；各入口保留摘要。场景、UI、角色动作、资产和历史资料从交接首页分类索引进入，至多两次点击可达。文档设计不代表已经接入游戏。

## 村庄开场剧情文稿（2026-10-02）

- `[可调整草案]` 负责人批准先交付村内开场文稿：七位 NPC 的人物卡、醒来至首次共同送货的逐场对白、可控搬凳／核货／推车步骤、再访反馈及后续任务纲要。正文统一见[第一章细案](doc/design/chapter-one.md)，30–40 分钟仅为写作目标，待审稿及实际游玩验证。
- `[固定约束]` 本轮仅 Markdown，不创建人物、任务、对白脚本、运行时 ID 或存档字段，不改程序、场景和资产。A1／C1 任意顺序且均交差后主动邀请沈砚；加入后其他委托仍可交错。三链取箱、正常生活无异常及有意留白保持。文档检查见[本轮记录](doc/production/chapter-one-doc-validation.md)，不代表可玩接入或美术通过。

## v6 正式采用与提交（2026-09-15 后续授权）

- `[已实现]` 负责人确认“先这样吧”，授权正式采用 v6 和配套住宅外观，包含四户 v5 与全村树木。正式与默认村庄预览使用新外观，布局及地形继续使用已修复 v4。独立 `--village-v4` 与旧版本入口保留。
- `[固定约束]` 本轮允许 Git 提交已开发内容，替代前述批次“本轮不提交”的阶段边界；本机角色、角色动作及含角色画面仍禁止提交，不推送、不导出游戏。第一章委托未接入。
- [正式采用与验证](doc/production/village-v6-adoption.md)；样板轮验收保留历史，不能当作本次正式入口测试。

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
3. `doc/design/story-bible.md` 决定当前世界观、人物边界和已确认的叙事方向；未定真相不得自行补成固定设定。
4. `doc/engineering/architecture.md` 决定模块边界与状态所有权。
5. `doc/design/story-flow.md` 保存章节概要，`doc/design/chapter-one.md` 保存第一章细案；其中 `[可调整草案]` 是制作建议，不是已实现事实。

文档统一使用四种状态标签：

- `[已实现]`：源码或数据中已经存在，并有相应验证。
- `[计划]`：已经决定要做，但当前未实现。
- `[固定约束]`：未经项目负责人确认不得改变。
- `[可调整草案]`：接手者可调整，但要保持固定约束和前后因果。

禁止用“已经完成”描述只存在于剧情大纲或 Core 枚举中的功能。

## 醒后剧情方向（2026-09-13 确认，09-14 更新地点）

- `[固定约束]` 主角一直在村中生活，在附近读高中；父母两年前外出务工后失踪，独居且由村中长辈照应，课余帮村民做事挣钱。父母真相有意留白，不预定为主线。
- `[计划]` 保留近现代青春奇幻冒险、人物基础与梦境序章；前两章村庄及周边，之后逐渐游历。第一章为周六正常日常委托，第二章周日早上收到下午返校通知，上午可继续剩余事项；学校事件与后续主线留白。
- `[固定约束]` 主角先独自完成两件短活，前中期主动邀请沈砚；晓雨第一章现实不出场、不同行或远程指导。同村但不熟、梦中首次深入合作的关系基础不变。
- `[固定约束]` 广场任务板、村长代管酬金与日常收件箱构成委托入口。三条必要任务链均交差后登记取旧工具箱；支线和领钱不影响解锁。玩家不看到主支线分类、任务链或完成比例；报酬用于装备、治疗物品，无生活费扣款或收入门槛。
- `[计划]` 主路径含两处普通动物情境战，可撤退重试；另有切磋和独立支线链。周六按剧情阶段推进，探索不耗尽当天，关键事项有中断恢复路径。
- `[固定约束]` 工具箱前没有异常。章末取回彭师傅早年遗失在外地的箱子，他否认委托；正常核实后没有有效调查方向，留下疑惑即实质结章。可继续支线或回家睡觉进入周日。不填入求救对象、秘密日记、组织标记或父母线索；来源有意留白。
- `[可调整草案]` 委托、配角、物件、场景和战斗细节见[第一章细案](doc/design/chapter-one.md)。09-12 的暑假返乡、乔姨求助信入口与章末解决新困难已由本轮替代；旧公司、绑架和七日封印终局仍只作[历史素材](doc/design/story-bible-legacy.md)。
- `[固定约束]` 前述剧情设计轮只改 Markdown。程序、数据、存档、梦境机关、稳定 ID、内容版本 6 不变；当前仍止于 `ExitOpened`／`CompletedPin`，第一章和第二章均未接入。详见[剧情流程](doc/design/story-flow.md)。

## 村庄与森林制作方向（2026-09-14 负责人已确认）

- `[固定约束]` 第一章在村庄与附近森林活动；村内没有敌对遭遇，自愿陪练保留。林缘劳作链的送饭、取物与山猴遭遇均在森林入口以内；野猪遭遇、取箱与现场复查均在森林。
- `[计划]` 主角去活动室门前邀请沈砚；森林小屋取代旧北坡铁路附屋的取箱安排。既有铁路附屋保留为普通村景，森林小屋的来源与铁路没有预定联系。
- `[固定约束]` 先完成村庄外景设计文档，模型与游戏暂不修改。保留主角家、溪流、双桥及主路骨架，采用自然生活感，重点地点详设、全村 24 栋分级；普通建筑只设计外观与院落。森林、森林小屋、动物与 NPC 建模另行安排。
- `[计划]` 外景细案统一见[村庄外景设计](doc/design/village-exterior.md)；三批依次为广场／两铺、活动室／任务院落／溪边、一般建筑／旧站／植被与森林出口接近段。没有新增公共 API、类型、存档字段或运行时 ID。

## 村庄第一批外景建模（2026-09-14 后续授权）

- `[已实现]` 第一批广场、杂货铺、木器为主的修理铺已制作为独立村庄 v2 并接入漫游预览；新增物件仅静态外观与碰撞，任务和第一章仍未接入。此前“先文档、暂不改模型”是上一轮边界，本轮已获负责人建模授权。
- `[固定约束]` v1、住宅源、24 栋建筑位置体量、地形路桥、Core、存档与内容版本 6 保留。默认预览使用 v2，`--village-v1` 显式回归。v2 人工修改后用 `export`，禁止用 `build` 覆盖手工精修。制作与实际验证见[村庄 v2](doc/engineering/village-v2.md)与[验收](doc/production/village-v2-validation.md)。

## 醒来与村庄正式接入（2026-09-14 后续授权）

- `[已实现]` 正常解谜和大头针刺醒经床边短过渡进入周六住宅／村庄自由探索；两路分别为 CompletedEscape／CompletedPin。第一章委托、居民、森林及第二章未接入。
- `[固定约束]` 本轮获准修改正式路由、Core 可选现实状态、存档兼容与醒来字幕；此前“止于 ExitOpened／独立预览／存档不变”为当轮边界。SaveEnvelope v1、梦境内容版本 6、旧版未完成梦境重开与 Legacy 路由保留。
- `[已实现]` 正式保存位置、朝向、镜头和门灯，独立预览不访问存档；不新增奖励、关系、现实调查线索、菜单跳过或睡眠。模型资产未重建，说明见[正式村庄探索](doc/engineering/village-wake.md)及[本轮验收](doc/production/village-wake-validation.md)。

## 村庄赶路优化（2026-09-14 后续授权）

- `[已实现]` 村庄 v2 正式探索与独立预览室外速度为 4.2 米/秒；住宅、梦境、显式 v1 回归仍为 3.1，Shift 慢走为 1.4。该调整属于场景表现层，不改动画原始速度基准或存档。
- `[固定约束]` 本轮保留建筑道路、地形、镜头与模型资产，不新增快跑操作、体力、设置项、任务或时间推进。地形插件和贴图只列入[后续计划](doc/design/village-optimization.md)，尚未制作；验证见[本轮记录](doc/production/village-travel-validation.md)。

## Terrain3D 地形样板（2026-09-14 后续授权）

- `[已实现]` Terrain3D 1.0.2 已安装，家门外道路、广场边缘和南桥西岸三处样板进入独立 `VillageTerrainPreview.tscn`；四区域承载基础地面，三处精修。样板制作轮未切换正式入口；负责人随后确认“看着不错，可以覆盖”，当前正式村庄与默认 v2 预览已采用这版地形。
- `[固定约束]` 本轮已获插件安装、地表素材及样板表现层接入授权，先前“未安装、地形不修改”为赶路轮边界。Godot 4.6.3／Compatibility、Core、存档、内容版本 6、旧 Blend/GLB 与建筑路桥保留；不接任务、森林或建筑新材质。
- 原生区域和材质为后续编辑源；初始化工具拒绝覆盖既有文件。制作、备份和实际检查见[地形样板](doc/engineering/village-terrain-sample.md)及[样板验收](doc/production/village-terrain-validation.md)、[正式采用验收](doc/production/village-terrain-adoption.md)。负责人已确认本版外观；真实设备手感与目标硬件验收仍单列。原 v2 地面使用独立预览 `--village-original-terrain` 回归，v1 原入口不变；不覆盖原 Blend/GLB，不改 Core/存档格式。

## 树木与两间铺面样板（2026-09-14 后续授权）

- `[已实现]` 广场大树、Tree_037、Tree_043 和两间铺面已派生为独立 v3；入口 `VillageArtPreview.tscn` 使用已采用的 Terrain3D。该样板制作轮正式游戏和默认预览仍为 v2；后续 v4 已采用其外观，第一章委托尚未接入。
- `[固定约束]` 本轮授权树木、两铺相关几何和 CC0 材质制作，替代上一轮“建筑贴图未制作”的阶段边界；保留位置体量、碰撞通路、旧源、地形、住宅、Core、存档和内容版本 6。本轮不调用图像生成工具，不提交、推送或导出游戏。
- 通过[制作说明](doc/engineering/village-art-v3.md)编辑并导出；v3 已存在时初始化拒绝覆盖。该样板当时等待正式采用；现已随 v4 采用，其余树木、房屋推广另行安排；实际检查和人工验收边界见[本轮记录](doc/production/village-art-v3-validation.md)。

## 村庄布局收紧（2026-09-14 后续授权）

- `[已实现]` 全村适度收紧的 v4 已接入正式探索与默认预览，同时采用 v3 树木与两铺外观；加载、路线及恢复结果见本轮验收。独立预览 `--village-v2` 回归原布局。本轮允许移动其他建筑、院地、道路和局部地形，替代前几轮“全部建筑位置不变”的阶段边界；主角家及院地、溪流、双桥、24 栋建筑体量与外围边界固定。
- `[固定约束]` 从保存的 v3 和已采用 Terrain3D 派生独立源，旧 v1～v3、住宅与旧地形保留；不使用旧构建器覆盖。Core、SaveEnvelope v1、存档字段、内容版本 6、剧情、室内和角色速度不变。新旧布局由外景实例分别持有，旧档无效落点回到固定安全点。
- 制作、入口、编辑方法和当前验证状态见[v4 说明](doc/engineering/village-v4.md)与[本轮验收](doc/production/village-v4-validation.md)。正式切换与负责人视觉、手感确认分别记录，不提交、推送或导出游戏。

## 四户住宅美术样板（2026-09-15 后续授权）

- `[已实现]` house02、house03、house13、house15 采用新旧混居、有人维护的外墙／屋顶／门窗／院墙与不同生活摆设；从包含院外接坡修复的保存 v4 派生独立 v5。入口为 `VillageHouseArtPreview.tscn`，正式游戏与默认预览仍为 v4，负责人确认后再切换。
- `[固定约束]` 负责人批准这四户的材质、浅构件与门前静态摆设，替代此前该批未制作的阶段边界；保留全部建筑位置体量、道路、已修复 Terrain3D、住宅、两铺和其他树木。只复用已采用 CC0 素材，不调用图像生成，不改 Core、存档和内容版本，不接第一章委托。
- 制作和只读复验见[v5 说明](doc/engineering/village-houses-v5.md)。保留旧源；禁止重跑 `init` 覆盖手工编辑。负责人视觉／手感确认与自动验证分别记录，不提交、推送或导出游戏。

## 主角家与全村树木样板（2026-09-15 后续授权）

- `[已实现]` 从保存 v5 派生独立 v6，保留四户样板，采用主角家暖色外观并将三种共享阔叶树推广至其余 55 棵普通树；广场大树与两棵已美化普通树保留，样板可独立走查。正式村庄与默认预览仍为 v4，负责人确认后再安排采用。
- `[固定约束]` 本轮授权主角家外侧材料、门廊浅构件、静态自行车／收纳与全村普通树枝叶替换，替代前轮相应“不变”边界。位置体量、室内、门轴、院门净宽、双向推门、修复地形、四户和两铺保持；无 Core／存档／剧情改动。
- [制作与独立入口](doc/engineering/village-home-trees-v6.md)、[实际验收](doc/production/village-home-trees-v6-validation.md)。只复用已采用 CC0；旧源保留，不运行旧构建器覆盖，不提交、推送或导出游戏。

## 村庄分区与 LOD v7（2026-09-16）

- `[固定约束]` 负责人随后允许以笔记本 GTX 1060 的处理能力提高预算，1080p／60 FPS 目标保留。v7 默认 `laptop-gtx1060`，集显旧档通过 `--village-budget-integrated` 回归；这是表现层配置，不改正式采用版本或存档。新档目标硬件实测与本机独显数据分开，见[新预算验证](doc/production/village-streaming-v7-1060-validation.md)。

- `[已实现]` 从保存 v6 派生 v7 独立源与七区流式预览，含显式 LOD、常驻代理、共享贴图及预算／整帧遥测。入口 `VillageStreamingPreview.tscn`，现有路由可显式加 `--village-v7`；正式和无参数默认仍为 v6。
- `[固定约束]` 本轮获准表现层分区与 LOD 改造，旧源、布局、碰撞、住宅、Core、存档和内容版本保留；v7 地形实例的 clipmap 配置及近景阴影预算独立于 v6，不修改原生地形高度。禁止用旧构建器覆盖 v7；编辑保存源后用 `build_village_v7.py export`，`init` 拒绝覆盖。
- `[固定约束]` 外景三角面／surface 预算、整帧 primitives／draw calls 和目标硬件帧时分别记录，不得把一项通过写成全部达标。真实目标硬件和负责人视觉终验待验收。见[工程入口](doc/engineering/village-streaming-v7.md)与[首轮集显预算记录](doc/production/village-streaming-v7-validation.md)。不推送、不导出游戏，本机角色及含角色媒体限制继续有效。

## 村庄 NPC 首批独立试点（2026-09-16）

- `[已实现]` 负责人批准独立 v7 内的乔姨和沈砚、简易 3D 人物与闲聊分支；乔姨定点，沈砚短路线走动。入口 `VillageNpcPreview.tscn`，正式与默认仍为 v6。
- `[固定约束]` 本轮取代此前 NPC 暂不制作的阶段边界；不接第一章委托、交易、关系、奖励、入队或日程，不改布局、Core、存档与内容版本。试点闲聊不注册剧情事件，不调用 `complete_event`；对白上下文与已读记录仅属于预览实例。
- [工程与来源](doc/engineering/village-npc-pilot.md)、[验收与推广门槛](doc/production/village-npc-pilot-validation.md)。试点人物不是正式美术定稿，目标硬件及负责人视觉确认单列；本机主角与含其画面禁止提交、分发和导出的约束继续有效。

## NPC 美术重新设计与沈砚静态候选（2026-09-18）

- `[已实现]` 09-17 失败的模块化样板已退出活动资源、配置、构建工具和场景入口；旧文件及旋转备份隔离于被 Git/Godot 忽略的 `work/shen-yan-v2/retired/`，不得复用为新模型。原低模 `VillageNpcPreview.tscn` 保留功能对照；[失败记录](doc/engineering/village-npc-art-pilot.md)仅为历史。
- `[固定约束]` 本轮按负责人批准的新计划，从独立 CC0 人体基模重新制作一个自然比例、半写实国风动画方向的沈砚静态候选；该授权替代上轮“所有人体基底必须项目自制”的限制。允许离线生成原创设计参考，但禁止复用主角 v2、本机角色或旧失败样板的模型、脸、骨架、纹理。MB-Lab／MakeHuman 不进入正式资产。
- `[已实现]` 新源 `art/blender/characters/shen-yan-v2/shen-yan-static-v2.blend`、静态 GLB、来源记录、PBR 图集与独立 `ShenYanStaticReview.tscn` 可审查；实际制作方式是 CC0 解剖基体改形和脚本网格制作，不能描述为手工高模雕刻。面数／格式通过不代表美术通过，`runtimeReady=false`、`visualApproval=false`。
- `[固定约束]` 本轮仅静态候选与审图。正式和默认仍为 v6，低模 NPC 试点不替换；先完成近景视觉复核，再决定骨骼、动作、LOD、女性基体、乔姨与居民批量制作。Core、剧情和存档格式不变，受限角色与含其媒体的提交／分发／导出限制继续有效。
- 制作、入口与下一阶段门槛见[沈砚静态 v2](doc/engineering/shen-yan-static-v2.md)；真实结果和未通过项见[本轮验收](doc/production/shen-yan-static-v2-validation.md)。

## MPFB 人物工作流首轮（2026-09-20）

- `[固定约束]` 负责人批准按 `character_modeling_workflows_v0.1.md` 完成 M0/M1：隔离安装 MPFB 2.0.17 与官方基础资产包，取得未经个性化改形的成年男性正常脸基线。许可合格、验收通过的 MPFB/MakeHuman 体系产物允许成为项目候选，替代 09-18 对此来源的禁令；MB-Lab 与受限角色限制不变。
- `[已实现]` 独立 Python 创建、保存重开、配方重现、小幅参数修改与恢复、灰模/材质多视图和 Godot 静态审图。入口 `CharacterBaselineReview.tscn`；本轮使用批处理，当前会话没有可调用的 Blender MCP 工具，不宣称 MCP 闭环通过。
- `[固定约束]` 保留原生基线、全部试验版本和失败记录，不复用旧失败 NPC。G1 的代理视觉意见与负责人批准分开；G2–G5 未验，不进入参考定制、动画、LOD、批量人物或正式替换。正式 v6、低模 NPC 功能试点、Core、剧情、存档不变。
- [制作与复现](doc/engineering/character-workflow-mpfb.md)、[验收与图像证据](doc/production/character-workflow-mpfb-validation.md)。

## MPFB 正常脸确认与穿戴样板（2026-09-20 后续）

- `[已实现]` 负责人明确“确实像人”，G1 正常脸基线 v001 的源、配方和确认记录归档于 `art/blender/characters/mpfb-male-v1/approved/v001/`；此前 G1 待确认已由本条替代。G2 身份、G3 动态、G4 游戏交付及 G5 量产仍未验收。
- `[固定约束]` 负责人追加授权为同一人加头发与衣服供审看，取代首轮暂不制作穿戴的范围限制；使用已取得的官方 CC0 资产，保留脸部参数和原始/批准版本。不是正式沈砚造型批准，不接 NPC 逻辑或动画。
- `[已实现]` 新候选 v007-casual 使用短发、衬衫/牛仔裤和运动鞋，脚袜遮挡与头发材质适配均记录于配方/导出；[制作与实际审图](doc/production/character-casual-preview.md)。正式 v6、低模 NPC、旧审图、Core、存档和受限资产规则不变。

## 自然青年头肩修订（2026-09-20 后续授权）

- `[固定约束]` 负责人允许在 v007 基础上做可关闭的局部唇缘/眼睑纠正层，保留原拓扑、UV及MPFB形变；本轮仅头肩样板，按脸部结构、材质、头发三阶段各一次集中修正。明显缺陷仍在则停止，不自动进入全身、动作、LOD或正式NPC。
- `[已实现]` v012 闭唇纠正与 v014 局部皮肤/睫毛处理通过技术检查；v016 新曲线确实驱动头发网格，但头套边界、凸条感与后脑折转仍明显，**头发美术失败，整体样板未放行**。保存全部阶段源与失败证据，默认基线与正式 v6 不变。见[实际交付、验证及停止记录](doc/production/character-head-refinement.md)。

## 成熟完整角色来源初筛（2026-09-21）

- `[固定约束]` 负责人批准改用“成熟完整角色 → 小幅定制 → Godot 验证”；MPFB 保留对照，本轮不继续改形。最多八项来源，免费优先、付费仅评估，不自动购买。
- `[已实现]` 八项初筛完成，无合格免费项；高领便服男性仅列付费观察，造型、来源及修改许可未核清，不能入库。没有下载模型或开始本机静态验证，未新增外部角色运行时适配。
- [筛选证据、费用与未验证项](doc/production/character-mature-source-screening.md)。正式 v6、低模 NPC、Core、剧情、存档与旧批准资产保持；本轮不是新模型交付或美术批准。

## 村庄清新动画风格 v8（2026-09-26）

- `[固定约束]` 负责人批准全村动画乡村改造，当前执行独立源与主角家—广场—两铺样板；样板确认后才推广全村，完整终验后才采用。允许重做外形、材质与光照，保留 24 栋位置/主体占地、路网、溪流、双桥、门口净空与修复后的地形高度。
- `[已实现]` v8 独立源、资源配置和 `VillageAnimePreview.tscn`／独立预览 `--village-v8`。正式与无参数默认继续 v6，详见[工程入口](doc/engineering/village-anime-v8.md)与[实际验证](doc/production/village-anime-v8-validation.md)。三栋重点建筑和四棵代表树不代表全村已改造。
- `[固定约束]` 仅外景，住宅场景外侧同步；室内、人物、NPC、森林、Core、任务、SaveEnvelope 和内容版本不改。旧源保留，不用旧构建器覆盖；不购买、不调用图像生成、不升级、不提交、不推送、不导出游戏，受限角色和媒体限制继续有效。

## 技术基线

- 引擎固定为 **Godot 4.6.3 .NET/Mono**，开发期不升级。
- 语言为 C#；游戏项目和纯核心库目标框架均为 `net8.0`。
- 当前命令行 SDK 由 `global.json` 固定为 .NET SDK 10.0.400。
- 渲染器固定为 Compatibility / OpenGL。
- 对话插件固定为 **Dialogue Manager v3.10.4**，这是 Godot 4.6 的匹配版本；已 vendored 到 `addons/dialogue_manager/`，本地兼容补丁见其 `TOWN_RPG_VENDOR.md`。
- 目标平台仅为 Windows x64；保留 640×360 逻辑坐标，使用 `canvas_items` + `keep` + fractional 缩放，3D 按实际窗口分辨率渲染。默认 1280×720 窗口，另支持 1600×900、1920×1080 和原生无边框全屏。
- 首版仅简体中文、单人、离线，不做联网、云存档、Mod 或 Steam 接入。

详细数据流和边界见 [架构文档](doc/engineering/architecture.md)。

当前叙事方向与角色关系以 [剧情圣经](doc/design/story-bible.md) 为准；历史稿不覆盖现行决策。

## 内容与资产规则

- 稳定 ID 使用小写英文、数字、点和下划线；发布后不得复用旧 ID 表示新含义。
- `Facts` 是已确认事实，`Evidence` 是可向他人出示以支持说法的证据，`Leads` 是可调查方向，三者不得混用；揭露公司是旧版证据用途，不限定新剧情。
- 新角色、日程或事件必须同步 JSON、本地化、对白、资源引用和内容完整性测试。
- 每个事件使用独立 `.dialogue` 文件、唯一静态文本 ID，并且恰好执行一次对应 `complete_event`。
- 未知状态键、对白命令、参数数量、跳转、角色引用或本地化键必须在构建检查中失败，不能静默忽略。
- 解谜题面不直接写完整答案或推理步骤；失败反馈使用自然角色表达，主角只能回想实际已知材料。晓雨主动三级求助最终只给局部试探建议，不能把完整答案拆成三次说完。机关无关联资料，独立记录、现场线索和必要合作观察必须保留。详见[角色提示规范](doc/engineering/puzzle-feedback.md)。
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

& 'C:\Users\duwei\.dotnet\dotnet.exe' build .\src\TownRpg.Core\TownRpg.Core.csproj
& 'C:\Users\duwei\.dotnet\dotnet.exe' test .\tests\TownRpg.Core.Tests\TownRpg.Core.Tests.csproj
& 'C:\Users\duwei\.dotnet\dotnet.exe' restore .\TownRpg.csproj --source $GodotPackages --ignore-failed-sources
& 'C:\Users\duwei\.dotnet\dotnet.exe' build .\TownRpg.csproj --configuration Debug --no-restore
& $GodotConsole --headless --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn'
```

换机时只替换本机绝对路径，不得改变版本。冒烟必须出现：

```text
GODOT_SMOKE_PASS scenes=9 characters=10 schedules=21 events=38 items=31
```

人工验收、性能和导出要求见 [测试与发布](doc/production/testing-release.md)。

## 当前阻断项与提交前检查

截至 2026-09-07：`[已实现]` 序章 v4（内容版本 6）：四室、12 组主谜题/19 个规则节点、31 件道具、11 个安装槽、一次集中回访；13 段序章对白经 Dialogue Manager v3.10.4 播放。目标 60–90 分钟仍待至少 3 名剧情 RPG 玩家盲测。 第一室、音乐室、档案室、礼堂分别为 11/18/13/15 个交互点；沿用现有四室与洋房 GLB/PBR 资产、Orbit 换场和 2 米线索分离。双侧出口在持久排演之后还需当前实例完成返听和灯光；预览、晓雨站位和按压不存档，关闭、其他模态、离房和重载复位。旧版进行中及 ExitOpened 梦境档要求重开，真正完成且背包清空的终态与无梦境 Legacy 档继续兼容。母带只循环离线白噪声并提供文字。机制仍止于 ExitOpened，局部旧事只进入梦中记录。Windows 导出模板、完整人工盲测、真实 raw delta/Xbox/GTX 1660 和导出仍待验收。详见 [序章 v4](doc/design/dream-escape-v4.md)。

当前工作副本全部 3D 区域的 mc_model 角色与动作仅限本机非商业；源文件、Godot 镜像及动作均禁止提交、推送、再分发和导出，缺失时回退 2D billboard。

截至 2026-09-05：本机 mc_model 动作 v2 已接入五个 3D 区域，默认慢跑 3.1 米/秒，左右 Shift 慢走 1.4 米/秒，摇杆连续调速；新增左右 90°/180° 转身、AnimationTree 相位同步、起停/碰撞调速和失焦清零。模型、动作、制作源与预览继续仅限本机非商业，禁止提交和导出。移动状态属于表现层，不修改 Core/存档；当时测试基线增至 114（新增 10 项移动测试），序章再增加 11 项至 125。实现、复现与人工验收边界见 [主角移动与动作](doc/engineering/character-motion.md)。

截至 2026-09-08：`[已实现]` 全部现有 UI 重制，含 Legacy。默认正文 14、辅助文字不小于 11、三档字号；机关直接操作、显式 A/B 菜单映射。当时的机关资料侧栏及大字号资料双页已于 09-09 移除。Core/存档/剧情结果和内容版本不变。当时新增 1 项 UI 本地化完整性测试至 126 项；九场景冒烟与引擎 UI 交互验证通过。09-08 的 288 张页面/状态截图与 Figma 保留历史，当前边界见 [UI 规范](doc/engineering/ui-ux.md)。真实 Xbox、raw delta、完整人工盲测与导出仍待验收。

提交或交付前必须确认：

- Core 与内容测试全部通过，测试数量变化有解释。
- Godot 工程可编译；涉及资源或场景时冒烟通过。
- 新路径、ID、本地化键、对白跳转和资源引用有效。
- 低关系、失约、错过时间、撤退和战败仍有继续路径。
- `README.md`、`doc/` 和本文件没有把计划内容写成已实现。
- 无 Web/数据库/API/端口相关变更；无未经授权的依赖、引擎或插件升级。

截至 2026-09-09：`[已实现]` 第一室环境重制 v6：12×9×4 米、12 组共享课桌、24 组碰撞、11 个交互点；中央/侧面/后排净通道为 3.68/1.74/1.24 米。新增实体作息资料板、纸面承托、窗墙和门洞修复、Color 环境光以及仅第一室采用的晓雨避障回调。Core/存档/剧情与内容版本 6 不变；当前测试数 127（新增 1 项实际 GLB 共享网格与状态根检查）。v5 为历史源；当前使用独立 `classroom-art-pass-v6.blend/glb`，通过 `remodel_classroom_v6.py check` 复验，禁止用旧构建器覆盖。验证、截图和未验收项见 [第一室 v6](doc/engineering/classroom-remodel-v6.md)与[验收记录](doc/production/classroom-v6-validation.md)。本机角色及含该角色的截图继续受原有禁止提交/分发/导出约束。

截至 2026-09-09：`[已实现]` 洋房大厅 v3：10×8×6.6 米前厅、7×12×4.2 米内廊、2.4×4.6 米封闭折返楼梯景观和至少 3 米中央通路。`MansionLayout3D` 为 Blender 与 Godot 的共同布局来源；25 组碰撞、8 标记/5 有效目标，新增旧档落点修正与大厅晓雨绕行。Core/存档字段/四室顺序/内容版本 6 不变。当时测试 128（在 127 基线上新增 1 项实际 GLB 内容检查）；编译、九场景冒烟及三档分辨率专项结果见[大厅 v3](doc/engineering/manor-remodel-v3.md)和[验收记录](doc/production/manor-v3-validation.md)。v1/v2 为历史源，当前使用独立 `dream-manor-hall-v3.blend/glb`，用 `remodel_manor_v3.py check` 只读复验。旧外观 v1 不代表新内部尺寸。原有本机角色与截图限制继续有效；人工视觉终验、盲测、真实输入设备、目标硬件和导出仍待验收。

截至 2026-09-09：`[已实现]` 解谜角色提示：19 个题面、57 条晓雨求助和 214 条情境文案；加入前仅回想已知材料，加入后主动三级商量只给局部建议。失败不改变进度，安装/消耗后仍保留材料知识，同次交互只显示一条结果。机关关联资料已移除，独立记录和合作观察保留。当前测试 134（128 基线＋5 项规则＋1 项文案检查），编译、九场景冒烟、真实控制器专项及三分辨率×三字号共 180 张截图通过；详见[规则](doc/engineering/puzzle-feedback.md)与[验收](doc/production/dream-hints-validation.md)。剧情、答案、存档格式与内容版本 6 不变。语气和难度仍待玩家盲测。

截至 2026-09-09：`[已实现]` 第二室音乐教室 v2 为 12×10×4 米，东北 3.2×3.2 米器材间解锁可进入；六把排练椅、真实乐器、实体资料承托和西窗开洞。`MusicRoomLayout3D` 共享 18 个标记、家具、门轴及安全点；31 个分阶段碰撞定义、晓雨介绍后出门与校准绕行、旧档安全恢复。Core/存档/答案/剧情/内容版本 6 不变。当时测试 135（134 基线＋1 项实际 GLB/布局/状态根检查）。细节与实际结果见[音乐室 v2](doc/engineering/music-remodel-v2.md)和[验收](doc/production/music-v2-validation.md)。本机角色及截图限制继续有效，人工视觉终验、盲测、真实输入设备、目标硬件和导出仍待验收。

截至 2026-09-09：`[已实现]` 电影感 UI 第一阶段样板：对白、梦境探索、背包与梦中记录。样板默认正文/字幕/辅助/标题为 8/9/7/12 逻辑字号，随包思源黑体 2.005R；菜单与字幕独立提供四档倍率及字幕背景。负责人批准新增兼容旧档的可选字幕设置，剧情字段、事件提交、答案与内容版本 6 不变。当时 143 项测试（135＋5 项设置兼容＋3 项分页）、零警告编译、九场景冒烟和输入专项通过；238 张矩阵图及 8 张前后对照见[样板](doc/engineering/ui-cinematic-samples.md)与[验收](doc/production/ui-cinematic-validation.md)。`[计划]` 按已确认的分阶段方案，先由负责人确认样板视觉，再统一其余 UI，不能声称第二次全 UI 重制完成。本机角色与截图的提交/分发/导出限制不变。

截至 2026-09-10：`[已实现]` 第三、第四室 v2：档案室 14×12×4.2 米、13 标记／20 碰撞；礼堂 22×16×7.5 米、15 标记／56 碰撞、32 座共享网格，抬高舞台仅景观。两份共享布局统一建模、碰撞、门轴、安全落点与晓雨连续绕行；使用独立 v2 Blend／GLB，v1 保留历史，禁止旧构建器覆盖。只读复验分别使用 `remodel_archive_v2.py check`、`remodel_auditorium_v2.py check`。Core／存档格式／答案／剧情／内容版本 6 不变，当前 145 项测试（143＋2 项实际 GLB／布局检查）。完整输入序章到 `ExitOpened` 完成页通过，含 6 处隔离真实存读档、四室大头针分支、13 个事件与中断恢复；九场景及 UI 回归、三分辨率场景 117 图与字号/独立字幕 144 图通过。测试存档仅在显式 `--prologue-e2e` 下注入项目 `outputs/`，正式路径不变。详情见[档案室](doc/engineering/archive-remodel-v2.md)、[礼堂](doc/engineering/auditorium-remodel-v2.md)和[验收](doc/production/late-rooms-v2-validation.md)。档案室加载曾记录约 16 秒，独立复测约 0.99 秒，原始数据保留，目标硬件冷启动与性能仍待验收；自动通关不代表负责人视觉确认、真实 Xbox/raw delta、听音、盲测或导出通过。本机角色及截图限制不变。

截至 2026-09-12：`[已实现·仅本机非商业]` 双角色动作 v3：林晓雨使用 `assets/流萤建模` 的流萤“春日手信”外观（隐藏剑、1.6 米），玩家保留现有 mc_model；每人 12 段动画，五区平滑跟随、实际位移步频、到位后协作与模态待机/交互已接入。Core/存档/剧情/答案/内容版本 6 不变，测试 151（145＋6 项表现层规则）。九场景、五区专项、完整输入通关与图形记录见[双角色动作](doc/engineering/character-motion.md)和[验收](doc/production/character-motion-v3-validation.md)。原 PMX、制作源、镜像及含角色画面均只限本机，不得提交、推送、分发或导出；缺失时保留 billboard。玩家静态候选 v2 未继续开发；人工自然度、真实输入设备、目标硬件和导出仍待验收。

截至 2026-09-12：`[已实现]` 主角家 3D v1 独立漫游：12×9×3.2 米平房、15×18 米院地、两卧一厅与独立厨卫，五处房门和六组灯可操作；`HomeLayout3D` 统一 Blender 几何与 Godot 碰撞/门轴/安全点。入口为 `scenes/testing/HomePreview.tscn`，状态仅属于当前实例，不写正式存档，没有晓雨或剧情线索；醒后剧情尚未接入，正式新游戏、Legacy、梦境结算和内容版本 6 不变。独立源为 `art/blender/home/home-v1.blend`，GLB/manifest 位于 `assets/generated/home/3d/`；用 `build_home_v1.py check` 只读复验，灰模仅写入 `work/home-v1-graybox/`。155 项测试（151＋4）、零警告 Debug 构建、九场景回归及住宅 179 项图形专项通过，三分辨率 84 张截图与加载耗时见[实现](doc/engineering/home-v1.md)和[验收](doc/production/home-v1-validation.md)。负责人视觉确认、真实 Xbox/raw delta、目标硬件冷启动和导出仍待验收；本机角色与含角色截图的限制不变。

截至 2026-09-12：`[已实现]` 村庄 3D v1 独立漫游：24 栋建筑（18 住宅＋6 公用/铁路建筑）、两桥、溪岸与旧站回环，院路连续，住宅正门淡入淡出往返既有室内。`VillageLayout3D` 统一地形网格、建筑、道路、桥面和碰撞，角色可选地形移动默认关闭，仅村庄开启；预览缓存住宅临时门灯状态，重置释放，不写正式存档，不接入居民/对白/任务或醒后剧情。独立源 `art/blender/village/village-v1.blend`，运行资源 `assets/generated/village/3d/`；用 `build_village_v1.py check` 只读复验，不能覆盖住宅 v1 源。当前 162 项测试（155＋6 布局/地形＋1 实际 GLB），零警告构建、九场景与住宅回归、村庄路线/转场和三分辨率专项见[制作](doc/engineering/village-v1.md)与[验收](doc/production/village-v1-validation.md)。内容版本 6 与正式路由不变。负责人视觉确认、真实 Xbox/raw delta、目标硬件与导出仍待验收；本机角色及截图限制不变。

## 双向推门与院外接坡（2026-09-15 后续授权）

- `[已实现]` 房门向玩家对侧开启，真实门叶运动受阻暂停、让开继续；正式正门仍转场。取消原整片扫掠拒绝与临时碰撞，保存稳定开闭及方向。
- `[固定约束]` 本轮负责人明确批准 Core 可选 `DoorOpenSides` 字典，SaveEnvelope v1／内容版本 6 保留。旧档缺字段先用原方向，必要时选择安全另一侧；住宅缓存不覆盖载入快照。
- `[已实现]` 连续走到院外低处后回路面复现 Terrain3D 接坡阻挡，局部修改 276 个样本及三处道路网格；住宅、院门宽度与移动参数不变。此前 v4 小段道路高度保护约束由本次修复授权局部替代。详见[修复说明](doc/engineering/home-access.md)与[实际验收](doc/production/home-access-validation.md)，旧测试没有覆盖完整院外往返，不代表这段路线此前顺畅。

## 自然比例动画风格人物第2版（2026-09-20）

- `[固定约束]` 负责人授权从 v007-casual 派生独立动画风格静态样板，使用 MPFB 原生形变、原创侧分发束和派生材质；不另设概念图审批。原正常脸、穿戴版、正式 v6、低模 NPC、Core、剧情和存档保持。身份未定，不复用受限人物。
- `[已实现]` v008/v009/v010 三轮候选与双引擎对照已制作，当前源 v010-stylized-v2，导出 v010-stylized-v2-game；技术检查通过但发束、口部及局部阴影仍有不足，**美术未通过**。默认审图仍为原基线；原 G1 批准不延伸为此造型或动态批准。见[实际验收与入口](doc/production/character-stylized-v2.md)。
