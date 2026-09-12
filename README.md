# town-rpg / 《雾山旧轨》

`town-rpg` 是一款面向 Windows x64 的单机剧情生活 RPG。当前梦境洋房与四室均已迁移到 3D，Legacy 小镇仍为 2D。玩家扮演暑假回到故乡的大学生，在山间铁路小镇安排日常、建立关系，并调查开发工程背后的封印真相。

新游戏当前进入目标 **60–90 分钟**的四室梦境序章第一阶段；旧存档仍可继续约 15–30 分钟的“两日生活＋首次异象”Legacy 切片。计划中的完整版本为 3–5 小时、约七个内容日，不能把剧情大纲写成已实现功能。

`[已实现]` 序章 v4（内容版本 6）：四室、12 组主谜题/19 个规则节点、31 件道具、11 个安装槽、一次集中回访；13 段序章对白经 Dialogue Manager v3.10.4 播放。目标 60–90 分钟仍待至少 3 名剧情 RPG 玩家盲测。

新版规则及攻略见 [序章谜题重梳 v4](doc/design/dream-escape-v4.md)。

## 接手入口

- [交接文档首页](doc/README.md)：推荐阅读顺序、当前状态、术语和接手第一周清单。
- [项目代理约束](AGENTS.md)：技术红线、剧情红线、工作流程和提交前检查。
- [当前实现](doc/engineering/current-implementation.md)：现在能运行什么、入口在哪里、哪些尚未完成。
- [完整剧情流程（剧透）](doc/design/story-flow.md)：Day 1–7 的共享主线、双路线和终局场景规格。
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
- Core、应用规则和内容契约测试为 **151/151 通过**（145 基线＋6 项双角色跟随与协作计时用例）。
- Godot Debug 构建与九场景 Compatibility/OpenGL 冒烟通过；第一室采用重制 v6、洋房采用大厅 v3、第二室采用音乐教室 v2。冒烟覆盖交互点、线索分离、碰撞、换场、临时协作和白噪声；新增正式序章对白、取消重试及机关面板专项验证。旧版 24 张 1080p 房间捕获保留作历史，新版 UI 捕获见测试文档。
- 引擎冒烟成功标志：`GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。
- `[已实现]` 全 UI 重制、机关直接操作与明确提交；09-09 已移除机关关联资料，加入基于已知材料的主角思考，改写 19 个题面和 57 条晓雨求助。三档分辨率×三档字号新增 180 张截图，见[角色提示验收](doc/production/dream-hints-validation.md)。09-08 的 288 张图保留在[历史 UI 验收](doc/production/ui-remake-validation.md)。
- `[已实现]` 电影感 UI 第一阶段：下方居中字幕、探索提示、背包、梦中记录及独立字幕设置。其余 UI 待样板视觉确认后统一，见[样板说明](doc/engineering/ui-cinematic-samples.md)和[本机预览](outputs/ui-cinematic/review.html)。

当前工作副本的 3D 区域默认使用本机非商业 `mc_model` 玩家与流萤外观的林晓雨；该资源不进入 Git 或导出，缺失时安全回退到 2D billboard。尚未完成：新版 60–90 分钟完整四室盲测、Xbox 手柄、物理鼠标 raw delta、GTX 1660 级硬件性能、正式可分发 3D 主角视觉确认/绑定/接入、Windows x64 导出。具体优先级见[交接路线图](doc/production/roadmap-handoff.md)。

`[已实现]` 第一室重制为 12 组课桌的写实旧教室，中央净宽 3.68 米、侧通道 1.74 米、后排 1.24 米；模型、线索承托、碰撞及晓雨避障同步接入。见[第一室建模与布局 v6](doc/engineering/classroom-remodel-v6.md)和[验收记录](doc/production/classroom-v6-validation.md)。

`[已实现]` 洋房大厅 v3 已接入：10×8 米前厅、7×12 米内廊、封闭楼梯景观、共享布局碰撞与旧档安全落点。四室顺序及内容版本 6 不变。见[建模说明](doc/engineering/manor-remodel-v3.md)、[验收记录](doc/production/manor-v3-validation.md)及[本机实机评审](outputs/manor-v3/review.html)。

`[已实现]` 第二室音乐教室 v2：12×10×4 米、可进入器材间、真实乐器、六把排练椅与晓雨绕行。见[实现](doc/engineering/music-remodel-v2.md)、[验收](doc/production/music-v2-validation.md)和[本机对照页](outputs/music-v2/review.html)。

`[已实现]` 第三、第四室 v2：14×12×4.2 米档案室与 22×16×7.5 米礼堂，共享布局、实体机关、连续绕行与旧档落点恢复。礼堂 32 座共享网格，抬高舞台为不可进入景观。完整输入主线、隔离存读档和四室大头针分支见[验收记录](doc/production/late-rooms-v2-validation.md)；[本机新旧对照](outputs/late-rooms-v2/review.md)与俯视布局供视觉复核。人工视觉确认和真实设备盲测仍待完成。
