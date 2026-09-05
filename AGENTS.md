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
GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=8 items=34
```

人工验收、性能和导出要求见 [测试与发布](doc/production/testing-release.md)。

## 当前阻断项与提交前检查

截至 2026-09-01：梦境密室 v3 的四室规则使用内容版本 5，运行时保持 28 个正式谜题、34 件道具、11 个稳定安装槽和两轮回访；进行中的 v4 梦境存档明确要求重开，已完成 v4 终态和无梦境 Legacy 存档继续兼容。第一室使用电影式半写实 `classroom-art-pass-v5.glb`，12 个交互点；第二室为约 11×8×4 米程序化 PBR 3D 音乐教室，18 个交互点。第三室已替换为约 12×9×4.2 米的 `dream-archive-room-v1.glb` 与独立 3D 状态层，使用新的信号登记题和 15 个唯一交互点；第四室已替换为约 18×13×7.5 米的 `dream-auditorium-v1.glb` 与独立 3D 状态层，使用新的吊杆组配重题和 15 个唯一交互点。四室与洋房全部以 Orbit 3D→3D 换场，答案线索与对应机关至少相距 2 米。礼堂双侧出口的晓雨按压姿态和面板预览只存在于当前表现实例，关闭、离房或重载清零且不写存档；持久外观只从 Core 状态派生。梦境顶部不显示任务目标，只保留紧凑区域名；普通道具自动匹配安装。3D 洋房使用 `dream-manor-hall-v2.glb`。声音均为离线确定性 WAV，复原母带只循环白噪声且有文字替代。当前工作副本的全部 3D 区域默认加载本机 `mc_model` PBR 角色及 `idle/walk/jog`，资源缺失时回退到 2D billboard；该模型仅获非商业本机使用许可，源文件和 Godot 镜像不得提交、推送、再分发或进入 Windows 导出。Dialogue Manager v3.10.4 尚未放入 `addons/dialogue_manager/`；Windows 导出模板缺失；50–60 分钟后半程与完整 90–120 分钟人工通关、真实物理鼠标 raw delta、Xbox、GTX 1660 级硬件和导出尚未验收。

提交或交付前必须确认：

- Core 与内容测试全部通过，测试数量变化有解释。
- Godot 工程可编译；涉及资源或场景时冒烟通过。
- 新路径、ID、本地化键、对白跳转和资源引用有效。
- 低关系、失约、错过时间、撤退和战败仍有继续路径。
- `README.md`、`doc/` 和本文件没有把计划内容写成已实现。
- 无 Web/数据库/API/端口相关变更；无未经授权的依赖、引擎或插件升级。
