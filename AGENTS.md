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
- 对话插件固定为 **Dialogue Manager v3.10.4**，这是 Godot 4.6 的匹配版本；已 vendored 到 `addons/dialogue_manager/`，本地兼容补丁见其 `TOWN_RPG_VENDOR.md`。
- 目标平台仅为 Windows x64；保留 640×360 逻辑坐标，使用 `canvas_items` + `keep` + fractional 缩放，3D 按实际窗口分辨率渲染。默认 1280×720 窗口，另支持 1600×900、1920×1080 和原生无边框全屏。
- 首版仅简体中文、单人、离线，不做联网、云存档、Mod 或 Steam 接入。

详细数据流和边界见 [架构文档](doc/engineering/architecture.md)。

完整剧透和角色关系以 [剧情圣经](doc/design/story-bible.md) 为准。

## 内容与资产规则

- 稳定 ID 使用小写英文、数字、点和下划线；发布后不得复用旧 ID 表示新含义。
- `Facts` 是已确认事实，`Evidence` 是可用于揭露公司的证据，`Leads` 是可调查方向，三者不得混用。
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

& 'C:\Program Files\dotnet\dotnet.exe' build .\src\TownRpg.Core\TownRpg.Core.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\TownRpg.Core.Tests\TownRpg.Core.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' restore .\TownRpg.csproj --source $GodotPackages --ignore-failed-sources
& 'C:\Program Files\dotnet\dotnet.exe' build .\TownRpg.csproj --configuration Debug --no-restore
& $GodotConsole --headless --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn'
```

换机时只替换本机绝对路径，不得改变版本。冒烟必须出现：

```text
GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31
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
