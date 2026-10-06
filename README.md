# town-rpg / 《雾山旧轨》

`[已实现]` 2026-10-06：[书桌／衣柜木材贴图专项](doc/engineering/bedroom-material-v1.md)提供同网格的新旧材质对照、七视角和中性光检查。运行 `scripts/tools/PreviewBedroomMaterial.ps1`；[真实截图、性能与待验项](doc/production/bedroom-material-v1-validation.md)。原版完整卧室与正式入口保留。

`[已实现]` 2026-10-05：[卧室专项独立三维样板](doc/engineering/bedroom-target-v2.md)含静态布料床品、结构家具、真实窗外、LightmapGI 和五视角／漫游。运行 `scripts/tools/PreviewBedroomTarget.ps1`；原住宅、三场景样板与正式入口保留，[实际验证及美术待验项](doc/production/bedroom-target-v2-validation.md)。

`[已实现]` 2026-10-02：[正式村庄完整开场](doc/engineering/village-opening.md)已接入两种醒后流程：七位功能 NPC、任务板、送凳、三户订货、邀请沈砚、共同推车配送、结款与存档恢复。正式村庄仍为 v6；[实际验证与待验项](doc/production/village-opening-validation.md)单列。下方旧批次未接入描述是历史边界。

`[可调整草案]` 2026-10-02 文稿轮（早于正式接入）：[村庄开场剧情文稿](doc/design/chapter-one.md)补入七位 NPC、两件独自短活、主动邀请沈砚及首次共同送货的完整对白与劳动操作设计，并附后续人物任务纲要。30–40 分钟为写作目标，尚未接入游戏；[纸面走查与验证边界](doc/production/chapter-one-doc-validation.md)。

`[已实现]` 独立样板，待视觉确认。 2026-09-26：[清新动画村庄 v8](doc/engineering/village-anime-v8.md)提供主角家—广场—两铺及代表树的独立可玩样板；正式与默认仍为 v6，全村推广等待样板确认。[验证与剩余工作](doc/production/village-anime-v8-validation.md)。

`[已实现]` 2026-09-21：[成熟完整角色八项初筛](doc/production/character-mature-source-screening.md)及真实展示比较页。没有合格免费项；一项付费观察仍待造型、来源与许可确认，未采购、未做本机资产验证。MPFB 与正式入口不变。

`[已实现]` 2026-09-20 后续：负责人确认 MPFB v001 正常脸，批准副本已归档；新增 [v007 头发与便服静态样板](doc/production/character-casual-preview.md)，原脸不改。穿戴效果待审，动画/正式 NPC 尚未接入。

`[已实现]` 2026-09-20：[MPFB 人物工作流 M0/M1](doc/engineering/character-workflow-mpfb.md)已完成隔离工具、原生成年男性基线、参数复现、固定视角与独立 Godot 静态审图。批处理闭环通过，MCP 未接通；正常脸等待用户 G1 确认，G2–G5 未测试。正式 v6 和低模 NPC 保持。[实际验收与审图](doc/production/character-workflow-mpfb-validation.md)。

`[已实现]` 2026-09-18：失败的 NPC 美术样板已退役，新建[沈砚静态 v2 候选](doc/engineering/shen-yan-static-v2.md)与独立审图场景，采用独立 CC0 人体来源、原创服装／发型和 PBR 图集。仅完成静态制作与技术验证，美术未批准，未绑定或接入居民；[实际验收](doc/production/shen-yan-static-v2-validation.md)。正式／默认村庄仍为 v6，原低模 NPC 功能试点保留。

`[已实现]` 2026-09-16：新增[村庄 NPC 独立试点](doc/engineering/village-npc-pilot.md)，乔姨定点交谈、沈砚短路线走动与话题选择；采用 v7，正式默认仍为 v6，第一章任务未接入。

`[已实现]` 2026-09-16：[村庄分区与 LOD v7 独立预览](doc/engineering/village-streaming-v7.md)，包含七街区、显式 LOD、预算控制与整帧遥测，现以笔记本 GTX 1060 提升画质预算。正式／默认村庄入口仍为 v6；目标硬件与完整预算状态见[新档验证](doc/production/village-streaming-v7-1060-validation.md)，[旧集显预算记录](doc/production/village-streaming-v7-validation.md)保留历史。

> 2026-09-15 正式采用：负责人已确认将 v6（四户住宅 v5、主角家暖色外观、全村树木）与配套住宅外观启用。正式游戏和默认村庄预览现用 v6，沿用修复后的 v4 布局／地形；独立预览 `--village-v4` 保留旧外观回归。第一章委托未接入。[采用与验证](doc/production/village-v6-adoption.md)。下述样板轮“尚未切换”是当轮交付状态。


`[已实现]` 2026-09-14 后续布局轮：[紧凑村庄 v4](doc/engineering/village-v4.md)已接入正式醒后探索与默认预览，采用 v3 树木／两铺外观和独立派生 Terrain3D。固定主角家、溪流、双桥及 24 栋体量；原布局通过独立预览 `--village-v2` 回归，v1 与 v3 样板入口保留。第一章委托仍未接入。[本轮实际验证](doc/production/village-v4-validation.md)独立记录；下述旧批次的“正式 v2／样板待采用”均为当时状态。

`[已实现]` 2026-09-14 [树木与两间铺面 v3](doc/engineering/village-art-v3.md)提供独立美术样板，正式村庄仍保留当前 v2 外观；本轮 184 项测试及引擎检查见[实际验收](doc/production/village-art-v3-validation.md)。

`[已实现]` 2026-09-14 后续接入：[梦境醒来与村庄自由探索](doc/engineering/village-wake.md)。解谜成功／大头针刺醒后进入周六卧室，住宅和村庄可往返，位置及门灯写入正式存档；第一章委托、居民、森林和第二章尚未接入。此前建模轮的独立预览和验收记录继续保留，不代表本轮验证。


`[已实现]` 村庄第一批外景 v2：广场办事区、杂货铺交货装卸区、修理铺工作区已接入独立漫游，新增物件为静态外观与碰撞。启动与源文件见[村庄 v2](doc/engineering/village-v2.md)，实际检查见[验收](doc/production/village-v2-validation.md)；第一章剧情仍未接入。

`town-rpg` 是一款面向 Windows x64 的单机剧情生活 RPG。当前梦境洋房与四室均已迁移到 3D，Legacy 小镇仍为 2D。`[计划]` 玩家扮演一直生活在村中、就读附近高中的学生，在近现代日常中接触隐藏的奇异现象；村庄及周边是前两章主要舞台，之后走向外地，让地方冒险与长期悬念并进。

新游戏当前进入目标 **60–90 分钟**的四室梦境序章第一阶段；旧存档仍可继续约 15–30 分钟的“两日生活＋首次异象”Legacy 切片。醒后自由探索已接入，第一章委托尚未接入；新故事总时长、总章数与结局未定，旧七日公司/封印方案已转为历史素材。

`[计划]` 2026-09-13 确认：主角是一直生活在村中的高中生，父母两年前外出务工后失踪，独居且由长辈照应，课余帮忙挣钱。第一章周六先独自办事、前中期邀请沈砚，三条必要日常链完成后登记取旧工具箱；正常排查无有效方向，以疑惑收尾，可继续支线或睡觉。第二章周日早上收到下午返校通知，上午续办剩余事项。晓雨第一章现实不出场，父母与工具箱真相有意留白。 本轮仅更新文档，具体任务卡见[第一章细案](doc/design/chapter-one.md)。

`[计划]` 2026-09-14 地点调整：村庄是无敌对遭遇的安全生活区，自愿陪练保留；林缘劳作与两场动物战均在附近森林，章末从森林小屋取箱。先完善村庄外景设计文档，森林及小屋另轮设计；既有铁路附屋只是普通村景。

`[已实现]` 村庄 3D v1 独立漫游：24 栋建筑、两座桥、溪岸与旧站双回环，以及可往返的主角家室内。打开 `scenes/testing/VillagePreview.tscn` 后按 F6；WASD 移动、Shift 慢走、F／手柄 A 交互、Esc 暂停与重置。院门开放，住宅正门淡入淡出换场，住宅灯态只在本次漫游保留。见[村庄制作与启动](doc/engineering/village-v1.md)、[验收记录](doc/production/village-v1-validation.md)和[总平面图](outputs/village-v1/village-v1-site-plan.png)。居民、任务尚未接入；醒来与自由探索见上方后续接入说明。

`[已实现]` 主角家原独立入口 `scenes/testing/HomePreview.tscn` 保留：两卧一厅、独立厨卫、小院与完整外观，五处房门、六组灯可操作。独立入口保持原有行为；正式游戏的醒来与存档由后续接入说明维护。见[住宅制作与启动](doc/engineering/home-v1.md)、[验收记录](doc/production/home-v1-validation.md)及[平面图](outputs/home-v1/home-v1-floor-plan.png)。

`[已实现]` 序章 v4（内容版本 6）：四室、12 组主谜题/19 个规则节点、31 件道具、11 个安装槽、一次集中回访；13 段序章对白经 Dialogue Manager v3.10.4 播放。目标 60–90 分钟仍待至少 3 名剧情 RPG 玩家盲测。

新版规则及攻略见 [序章谜题重梳 v4](doc/design/dream-escape-v4.md)。

## 接手入口

- [交接文档首页](doc/README.md)：推荐阅读顺序、当前状态、术语和接手第一周清单。
- [项目代理约束](AGENTS.md)：技术红线、剧情红线、工作流程和提交前检查。
- [当前实现](doc/engineering/current-implementation.md)：现在能运行什么、入口在哪里、哪些尚未完成。
- [第一章细案](doc/design/chapter-one.md)：六项必要委托、五项支线、两场必经战斗、工具箱排查与周日承接。
- [村庄外景设计](doc/design/village-exterior.md)：重点地点尺寸与任务站位、全部 24 栋建筑分级及后续建模批次。
- [醒后剧情流程](doc/design/story-flow.md)：章节概要、已确认方向与有意留白。
- [剧情圣经](doc/design/story-bible.md)：人物基础、背景与叙事约束。
- [旧版剧情流程（历史参考）](doc/design/story-flow-legacy.md)：旧 Day 1–7 与 Legacy 回归材料，不是新现实主线。
- [测试与发布](doc/production/testing-release.md)：打开、构建、测试、冒烟和人工验收命令。
- [UI 重制](doc/engineering/ui-ux.md)：Figma、主题、直接操作机关、输入与截图验收。

## 最短启动方式

使用 **Godot 4.6.3 stable Mono/.NET** 打开根目录的 `project.godot`，等待 C# 编译完成后按 `F6` 运行当前场景或按 `F5` 运行主场景。当前工作机的控制台可执行文件位于：

```text
D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe
```

这个绝对路径只是当前工作机记录；换机后应改为实际的 Godot 4.6.3 Mono 路径，不能换用标准版或其他引擎版本。

## 已验证状态

`[已实现·仅本机角色]` 五个 3D 区域已接入流萤外观的林晓雨与现有玩家双角色动作 v3，包含放松待机、步态、可取消转身、检查/拾取/操作及协作姿态。默认慢跑，按住左右 Shift 慢走，摇杆按推幅调速。见[主角移动与动作](doc/engineering/character-motion.md)。

- Godot 4.6.3 Debug C# 编译通过；本轮未执行 Windows 导出。
- Core、应用规则和内容契约测试为 **162/162 通过**（155 基线＋6 项村庄布局/地形规则＋1 项实际 GLB 契约）；住宅原 179 项专项及历史 84 图见住宅验收，村庄路线、转场和三分辨率 72 图见村庄验收。
- Godot Debug 构建与九场景 Compatibility/OpenGL 冒烟通过；第一室采用重制 v6、洋房采用大厅 v3、第二室采用音乐教室 v2。冒烟覆盖交互点、线索分离、碰撞、换场、临时协作和白噪声；新增正式序章对白、取消重试及机关面板专项验证。旧版 24 张 1080p 房间捕获保留作历史，新版 UI 捕获见测试文档。
- 引擎冒烟成功标志：`GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。
- `[已实现]` 全 UI 重制、机关直接操作与明确提交；09-09 已移除机关关联资料，加入基于已知材料的主角思考，改写 19 个题面和 57 条晓雨求助。三档分辨率×三档字号新增 180 张截图，见[角色提示验收](doc/production/dream-hints-validation.md)。09-08 的 288 张图保留在[历史 UI 验收](doc/production/ui-remake-validation.md)。
- `[已实现]` 电影感 UI 第一阶段：下方居中字幕、探索提示、背包、梦中记录及独立字幕设置。其余 UI 待样板视觉确认后统一，见[样板说明](doc/engineering/ui-cinematic-samples.md)和[本机预览](outputs/ui-cinematic/review.html)。

当前工作副本的 3D 区域默认使用本机非商业 `mc_model` 玩家与流萤外观的林晓雨；该资源不进入 Git 或导出，缺失时安全回退到 2D billboard。尚未完成：新版 60–90 分钟完整四室盲测、Xbox 手柄、物理鼠标 raw delta、GTX 1660 级硬件性能、正式可分发 3D 主角视觉确认/绑定/接入、Windows x64 导出。具体优先级见[交接路线图](doc/production/roadmap-handoff.md)。

`[已实现]` 第一室重制为 12 组课桌的写实旧教室，中央净宽 3.68 米、侧通道 1.74 米、后排 1.24 米；模型、线索承托、碰撞及晓雨避障同步接入。见[第一室建模与布局 v6](doc/engineering/classroom-remodel-v6.md)和[验收记录](doc/production/classroom-v6-validation.md)。

`[已实现]` 洋房大厅 v3 已接入：10×8 米前厅、7×12 米内廊、封闭楼梯景观、共享布局碰撞与旧档安全落点。四室顺序及内容版本 6 不变。见[建模说明](doc/engineering/manor-remodel-v3.md)、[验收记录](doc/production/manor-v3-validation.md)及[本机实机评审](outputs/manor-v3/review.html)。

`[已实现]` 第二室音乐教室 v2：12×10×4 米、可进入器材间、真实乐器、六把排练椅与晓雨绕行。见[实现](doc/engineering/music-remodel-v2.md)、[验收](doc/production/music-v2-validation.md)和[本机对照页](outputs/music-v2/review.html)。

`[已实现]` 第三、第四室 v2：14×12×4.2 米档案室与 22×16×7.5 米礼堂，共享布局、实体机关、连续绕行与旧档落点恢复。礼堂 32 座共享网格，抬高舞台为不可进入景观。完整输入主线、隔离存读档和四室大头针分支见[验收记录](doc/production/late-rooms-v2-validation.md)；[本机新旧对照](outputs/late-rooms-v2/review.md)与俯视布局供视觉复核。人工视觉确认和真实设备盲测仍待完成。

`[已实现·静态试验，未过美术验收]` [人物第2版：自然比例动画风格样板](doc/production/character-stylized-v2.md)包含三轮可编辑源、静态 GLB 和实际多视角对照；发束与闭嘴仍需精修，未采用为正式 NPC。

`[已实现·未过美术验收]` [自然青年头肩修订](doc/production/character-head-refinement.md)：v014 保留闭唇与局部肤色改善，v016 头发仍失败，按阶段门槛停止。可编辑源、静态GLB、真实对照与回归结果已记录，未进入全身精修。
