# 测试与 Windows 发布

`[已实现]` 2026-09-14 后续接入：[梦境醒来与村庄自由探索](../engineering/village-wake.md)。解谜成功／大头针刺醒后进入周六卧室，住宅和村庄可往返，位置及门灯写入正式存档；第一章委托、居民、森林和第二章尚未接入。此前建模轮的独立预览和验收记录继续保留，不代表本轮验证。


## 2026-09-14 村庄第一批外景 v2

`[已实现]` 广场、杂货铺和修理铺第一批静态建模已接入独立漫游，默认 v2、显式 `--village-v1` 回归。164 项测试（162＋2）、零警告 Debug 构建、九场景冒烟、Blender 只读复验以及 510 项村庄综合检查通过；最终三分辨率 90 图与旧版复拍单独记录。制作与实际结果见[村庄 v2](../engineering/village-v2.md)和[本轮验收](village-v2-validation.md)。

`[固定约束]` 第一章剧情未接入，正式路由、存档及内容版本 6 不变。负责人视觉确认、真实设备和目标硬件、游戏导出仍待验收。以下文档交付及 v1 记录保留其原始时间范围。

## 2026-09-14 村庄外景文档与森林地点调整

`[已实现]` 本轮仅完成 Markdown 文档交付，新增[村庄外景设计](../design/village-exterior.md)，同步第一章村庄／森林分区及导航。现有 Core／内容测试 162 通过、0 失败、0 跳过，未新增或修改测试代码；实际文档、布局映射与增量检查见[记录](village-exterior-doc-validation.md)。

`[计划]` 村内重点地点及全部 24 栋外景分批制作，森林、小屋另轮设计。没有修改模型、场景、程序或内容版本，也未运行 Blender 导出、Godot 构建／冒烟、图形验收或游戏导出。以下旧日期记录保留历史，不代表本轮新外景与剧情已接入。

## 2026-09-13 第一章剧情与文档导航

`[已实现]` 已完成本轮 Markdown 文档整理与检查，第一章叙事设计见[细案](../design/chapter-one.md)，实际检查与纸面走查见[验收记录](chapter-one-doc-validation.md)。现有 Core／内容测试 162 通过、0 失败、0 跳过，数量未变；本轮未新增测试代码。

`[计划]` 周六委托、任务板、工具箱排查、两处动物战及周日承接均尚未接入游戏。本轮没有运行 Godot 构建/冒烟、图形检查、人工游玩、盲测或导出。以下带日期记录保留当时事实；09-12 求助信方向现已被 09-13 方案替代，不作为新剧情约束或验收。

## 2026-09-12 村庄 v1 独立漫游

`[已实现]` 当前 162 项测试（住宅 155 基线＋6 村庄布局/地形＋1 实际 GLB），独立 24 栋村庄与两条回环、住宅往返及三分辨率记录见[村庄 v1](../engineering/village-v1.md)和[验收](village-v1-validation.md)。正式路由、Core/存档/内容版本 6 不变。下方住宅数量与截图为前一次交付历史，村庄回归结果单独记录。

## 2026-09-12 主角家 v1 独立漫游

`[已实现]` 住宅独立场景、Blend/GLB 和共享布局已交付，醒后剧情尚未接入。当前 155 项测试（151＋4）、Godot Debug 零警告编译、原九场景回归和住宅图形专项 179 项检查通过；三档分辨率共 84 张截图。制作/启动见[住宅 v1](../engineering/home-v1.md)，实际日志、首次退出 RID 提示与干净复跑、13–35 秒加载记录和待验收项见[验收记录](home-v1-validation.md)。下方同日叙事文档与提交记录是此前独立工作范围。

## 2026-09-12 醒后剧情文档检查

`[已实现]` 负责人批准的醒后方向已写入[剧情圣经](../design/story-bible.md)、[开篇流程](../design/story-flow.md)及相关入口/交接文档。已复核 19 个 Markdown 变更文件的范围、190 处本地链接引用（无新增失效链接），并确认两份历史剧情正文完整保留、无程序/数据/场景/资源变更。`git diff --check` 通过。

`[已实现]` 本次执行 `dotnet test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj --no-restore`，151 项通过、0 失败、0 跳过，数量未变。因本轮仅文档调整，未重跑 Godot 构建、场景冒烟、人工游玩或导出；此前验证不能作为新第一章已实现或已验收的证据。求助信真相和现实承接仍待设计。

## 2026-09-12 非人物资产提交检查

`[已实现]` 本次提交前复跑 Core 构建与 151 项测试（0 失败、0 跳过，数量沿用双角色动作 v3 基线），Godot Debug/Release 编译均为 0 警告、0 错误；九场景冒烟输出 `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`，并通过角色移动/缺失资源回退、16 次大厅往返、13 段序章对白及 UI 输入专项。

提交保留规则、表现层控制代码、场景环境资产、对白、UI、测试与文档；人物建模资产不纳入本次变更。仓库 `.gitignore` 固定排除 `mc_model/`、`assets/流萤建模/`、两个本机角色运行时镜像以及 `outputs/` 下的验证产物（仅保留 `.gdignore`）。受限人物源、动作文件及含人物画面继续只留本机。环境 Blend/GLB、贴图与字体遵守 Git LFS；人工视觉终验、真实输入设备、目标硬件、盲测和导出未在本次执行。

## 环境基线

项目只支持以下开发基线：

- Windows x64；
- Godot 4.6.3 stable Mono/.NET；
- Compatibility/OpenGL；
- .NET SDK 10.0.400 用于命令行，项目目标框架 `net8.0`；
- Dialogue Manager v3.10.4（已 vendored，13 段序章实际使用）；
- 640×360 逻辑坐标、`canvas_items` + `keep` + fractional 缩放；3D 按实际窗口分辨率渲染。

当前工作机路径：

```text
Godot: D:\ProgramFiles\Godot
dotnet: C:\Program Files\dotnet\dotnet.exe
```

路径不是项目契约，版本才是。换机时设置新的 PowerShell 变量，不要把本机绝对路径写进 Godot 资源或业务代码。

## 用编辑器运行

最简单方式是启动 Godot 4.6.3 Mono，导入或打开根目录 `project.godot`。等待资源导入和 C# 编译结束：

- `F5`：运行 `res://scenes/app/Main.tscn`；
- `F6`：运行当前打开场景；
- 出现 C# 构建错误时先查看 Godot 底部 Build/Debugger，而不是更换引擎版本。

也可从 PowerShell 打开编辑器：

```powershell
$GodotRoot = 'D:\ProgramFiles\Godot'
$GodotEditor = Join-Path $GodotRoot 'Godot_v4.6.3-stable_mono_win64.exe'
& $GodotEditor --editor --path .
```

## Core 构建和测试

Core 不依赖 Godot，应先独立验证：

```powershell
$DotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
& $DotnetExe build .\src\TownRpg.Core\TownRpg.Core.csproj --configuration Release
& $DotnetExe test .\tests\TownRpg.Core.Tests\TownRpg.Core.Tests.csproj --configuration Release
```

截至 2026-09-07：Core/内容/移动 **125/125 通过，0 失败、0 跳过**，在原 114 项基线上新增 11 项序章与内容检查。覆盖 v6 分组、唯一解、一次回访全链、装配和部分安装、错误/重复不丢物、旧档重开策略、剧情资格与幂等、本地化/对白契约。

- 关系阶段、有方向关系、截断、礼物和救援同行资格；
- 承诺状态机、不随日期过期、时间推进和模态引用计数；
- 暴露、路线、救援奖励、三档结局；
- 情境战零关系可胜、联携、战败和撤退；
- 存档往返、坏 JSON、版本拒绝和非法状态；
- 事件候选、日程边界、下一可见时段和重叠校验；
- 叙事白名单、原子回滚、结果幂等和纯查询；
- JSON schema、角色/事件引用、对白跳转、静态 ID、本地化和 Godot 资源路径。

测试数量以后可以变化，但交付报告必须解释新增、删除或跳过项，不能机械要求永远等于 71。

## Godot C# 构建

只使用已安装 Godot 4.6.3 随附的离线 NuGet 包源：

```powershell
$GodotRoot = 'D:\ProgramFiles\Godot'
$DotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
$GodotPackages = Join-Path $GodotRoot 'GodotSharp\Tools\nupkgs'

& $DotnetExe restore .\TownRpg.csproj --source $GodotPackages --ignore-failed-sources
& $DotnetExe build .\TownRpg.csproj --configuration Debug --no-restore
& $DotnetExe build .\TownRpg.csproj --configuration Release --no-restore
```

不要增加网络 NuGet 源，不要通过升级 Godot SDK 解决编译错误。`.godot/`、`bin/`、`obj/` 是生成目录，不应手工编辑。

## 引擎冒烟

```powershell
$GodotConsole = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe'

& $GodotConsole --headless --path . --quit-after 30
& $GodotConsole --headless --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn'
```

第二条命令必须输出：

```text
GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31
```

冒烟验证 Autoload、九场景、31 件物品和显示/输入基线，四室为 11/18/13/15 个交互点。保留 GLB/PBR、碰撞、可达性、分离线索及 3D 双向换场检查；新增四步出口错误顺序、离房/重载复位。PrologueV6Probe 另验证 21 份对白导入、13 段序章全播、取消重试、纸带/叠片/舞台操作与模拟手柄撤销。

- `inventory` 同时包含 I、真实 Tab 与 Xbox X，且不含 Backspace；
- Tab 在场景中和背包按钮持有 GUI 焦点时都能开关面板，关闭后不遗留隐藏焦点；普通物品没有“选用/取消选用”，大头针仍可独立对自己使用；
- 捕获状态下注入鼠标水平位移会改变 Orbit yaw，非捕获状态不会；
- 背包、暂停和退出确认会释放指针；Esc 能打开梦境暂停，退出确认不会被快速输入重复创建。

这些注入式检查不能替代真实物理鼠标的 captured/raw delta、Xbox 手感、谜题理解、存档 I/O 或完整流程验收。
`GodotSmokeRunner` 在整个测试进程中持有存档抑制租约，冒烟、性能和截图入口不得写入普通 `user://save-slot-1.json`；涉及真实存档 I/O 的验证必须另用专门临时用户目录执行。

第一室 2D 基线仍可用原有 Pillow 命令复核；当前 v4 另执行 Blender/Pillow 只读校验和十五张原生 1920×1080 截图采集：

```powershell
$PythonExe = 'C:\Python314\python.exe'
& $PythonExe .\scripts\tools\build_dream_pixel_art.py check
& $PythonExe .\scripts\tools\prepare_original_classroom.py check

$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
& $BlenderExe --background --python .\scripts\tools\build_classroom_3d.py -- mvp-check --root (Get-Location).Path
& $PythonExe .\scripts\tools\build_classroom_art_textures.py check --root .
& $BlenderExe --background --python .\scripts\tools\build_classroom_art_pass.py -- art-check --root (Get-Location).Path
& $PythonExe .\scripts\tools\build_dream_v2_item_icons.py check
& $PythonExe .\scripts\tools\build_dream_music_audio.py check --root .
& $PythonExe .\scripts\tools\build_classroom_v4_plan.py check --root .
& $BlenderExe --background --python .\scripts\tools\build_classroom_art_pass_v4.py -- art-check --root .

& $PythonExe .\scripts\tools\build_dream_manor_textures.py check --root .
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_dream_manor_3d.py -- manor-check --root .

$CaptureDir = Join-Path (Get-Location) 'outputs\classroom-3d-art-pass\godot'
& $GodotConsole --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- "--capture-classroom-3d=$CaptureDir"

& $GodotConsole --path . --resolution 1280x720 --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- --profile-classroom-3d
& $GodotConsole --path . --resolution 1920x1080 --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- --profile-classroom-3d
```

第三、第四室执行共同的只读复导与每室 12 张原生捕获：

```powershell
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
$GodotConsole = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe'
& $BlenderExe --background --python .\scripts\tools\build_dream_late_rooms_3d.py -- check
& $GodotConsole --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- '--capture-dream-late-rooms=.\art\review\dream-late-rooms-v1'
```

必须出现 `DREAM_LATE_ROOMS_ASSET_CHECK_PASS rooms=2 ... readOnly=true` 与 `DREAM_LATE_ROOMS_CAPTURE_PASS rooms=2 files=24 size=1920x1080`。捕获隐藏本机受限主角模型，检查入口四向、关键机关前后态、近景、碰撞和出口态，并排除死黑、穿模、假交互装饰和材质重复；它不替代人工通关。

第一室 v4 还必须出现 `CLASSROOM_V4_CHECK_PASS room=12x9x4 desks=18` 与 `CLASSROOM_V4_PLAN_CHECK_PASS`。前者从 Blend 临时复导并检查状态根、32,476 三角面和预算；后者只读重建俯视图/对照板并比较哈希。既有像素、原图、v2/v3、洋房和主角候选检查继续保留作回归，自动结构/截图检查均不等同负责人视觉终验或人工操作。

### 电影式环境完整运行时验收（教室 v5 与洋房 v2 历史）

电影式方向样板已经负责人确认并扩展全场景。当前验证必须覆盖 CC0 下载清单、两份权威 Blend/GLB、完整细节集合、正式梦境场景 1080p 捕获、评审板和九场景冒烟：

```powershell
$PythonExe = 'C:\Python314\python.exe'
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
$GodotConsole = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe'

& $PythonExe .\scripts\tools\build_environment_cinematic_sources.py check
& $BlenderExe --background .\art\blender\dream_classroom\classroom-art-pass-v5.blend --python-exit-code 1 --python .\scripts\tools\expand_environment_cinematic_full.py -- classroom-check --root .
& $BlenderExe --background .\art\blender\dream_manor\dream-manor-hall-v2.blend --python-exit-code 1 --python .\scripts\tools\expand_environment_cinematic_full.py -- manor-check --root .
& $BlenderExe --background --factory-startup --python-exit-code 1 --python .\scripts\tools\export_environment_cinematic_samples.py -- classroom-check --root .
& $BlenderExe --background --factory-startup --python-exit-code 1 --python .\scripts\tools\export_environment_cinematic_samples.py -- manor-check --root .
& $GodotConsole --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- '--capture-classroom-3d=.\outputs\classroom-v5-full-runtime'
& $GodotConsole --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- '--capture-dream-manor=.\outputs\dream-manor-v2-full-runtime'
& $PythonExe .\scripts\tools\build_environment_cinematic_review.py --root .
```

必须出现 `ENVIRONMENT_CINEMATIC_SOURCE_CHECK_PASS assets=8 materials=10 readOnly=true`、两条 `ENVIRONMENT_CINEMATIC_FULL_*_CHECK_PASS`、两条 `ENVIRONMENT_CINEMATIC_*_CHECK_PASS`、`CLASSROOM_3D_CAPTURE_PASS files=15 size=1920x1080`、`DREAM_MANOR_GODOT_CAPTURE_PASS files=9 size=1920x1080` 和 `ENVIRONMENT_CINEMATIC_REVIEW_PASS`。人工检查两张目标概念、Cycles Lookdev、完整运行时环绕图、黑板/教师桌/课桌/大门/第一室门/壁灯近景、门打开状态、三栏对比和材质拆解；重点排除死黑、均匀泛白、点光白斑、塑料粗糙度、重复低频纹理、错误门轴和悬浮结构。

这次历史完整运行时检查只证明 RTX 2080 Ti 上的 Compatibility 捕获链可用；没有 GTX 1660 级性能结论。两份 schema v2 manifest 必须保持 `status=cinematic-full-runtime`、`sampleOnly=false`、`runtimeDefault=true`，并用内容测试证明 GLB 路径、字节数、SHA-256 和预算匹配。该历史验收时场景引用教室 v5 与洋房内廊 v2；当前第一室改用 v6、大厅改用 v3，复验命令见下方当前入口；不得因此启用 LightmapGI，也不得把环境验收当作正式主角 v2 的视觉批准。本机非商业 `mc_model` 默认覆盖单独验证。

教室 v3/v4 目前只作离线/历史候选验证；3D 主角 v1 只是明确未通过视觉门槛的技术样模，原画式 v2 已生成静态 GLB/manifest 并通过技术检查，但仍在负责人视觉确认门槛之前。v1/v2 候选不进入运行时冒烟，也不得替代当前教室 v6 或正式可分发的二维角色基线。本机存在 `tests/mc-model-test-runtime/caelus-pbr-test.glb` 时，九场景冒烟额外要求 `PlayerController3D` 选择该 PBR 模型、隐藏 billboard、具备 `idle/walk/jog` 并正确应用世界朝向；资源缺失时验证 billboard 回退。新版还验证起停、Shift、骨骼姿态随步态变化、可取消转身、模态/失焦清零、顶墙与沿墙滑动；混合动画状态不再依赖 `AnimationPlayer.CurrentAnimation`。详见[主角移动与动作](../engineering/character-motion.md)。

```powershell
$PythonExe = 'C:\Python314\python.exe'
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'

& $PythonExe .\scripts\tools\build_classroom_art_textures_v3.py check --root .
& $BlenderExe --background --python .\scripts\tools\build_classroom_art_pass_v3.py -- art-check --root (Get-Location).Path
& $PythonExe .\scripts\tools\build_protagonist_static.py texture-check --root .
& $BlenderExe --background --python .\scripts\tools\build_protagonist_static.py -- check --root (Get-Location).Path

$PreviousGodotUserHome = $env:GODOT_USER_HOME
$FramingUserHome = Join-Path (Get-Location) 'outputs\godot-user-home-camera-framing'
New-Item -ItemType Directory -Path $FramingUserHome -Force | Out-Null
try {
    $env:GODOT_USER_HOME = $FramingUserHome
    $FramingDir = Join-Path (Get-Location) 'outputs\classroom-camera-framing'
    & $GodotConsole --path . --resolution 1920x1080 --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- "--capture-classroom-framing=$FramingDir"
}
finally {
    $env:GODOT_USER_HOME = $PreviousGodotUserHome
}
```

v3 检查仍关注其十组共享课桌、门枢轴、零缺失外部文件、纹理和预算；它与旧镜头对照入口都只作为历史候选回归，不修改第一室当前 v6 运行时引用。主角 v1/v2 检查关注官方 CC0 来源、比例、闭合网格、三角面、材质、GLB 哈希，以及明确不存在运行时骨骼和动画。

主角 v2 的第一阶段只验收静态美术，不复用 v1 的“技术导出通过”结论。必须先核对三项正式参考的哈希与 provenance，再输出同尺度 A-pose 正/侧/背、面部正/3/4/侧、自然站姿四向、服装与鞋近景、方案 A 默认出生构图及四个环绕角度。默认构图中角色屏幕高度目标为 28%–31%；同时报告身高、可见三角面、网格、材质、纹理和缺失外链。负责人确认脸型、头发、服装轮廓、材质和教室比例前，不执行 v2 的绑定、`idle/walk/jog`、`AnimationTree`、Godot 接入或运行时替换；本机 `mc_model` 覆盖不改变这项门槛。

当前独立捕获的实测值为：默认及前侧角度 `36.47%`，且 SpringArm 完整保持 `3.4m`；后侧角度因墙体将 SpringArm 压缩至 `2.430–2.444m`，屏幕高度升至 `49.25%–49.49%`。这说明原定 `28%–31%` 尚未通过，但不是 `1.7017m` 模型或捕获工具的尺度错误：旧二维 sprite 的有效透明边界只对应约 `1.38–1.45m`，不能直接作为真实身高 3D 人物的等价口径。视觉确认后应在“约 `35%–38%` 的真实 3D 构图”与“约 `4.0–4.2m` 臂长或更宽 FOV”之间另行确认；不得缩小角色世界尺寸。后侧 SpringArm 压缩也必须作为独立镜头舒适性问题处理。

v2 候选资产落地后执行以下只读检查；它只能检查静态资产，不会绑定或生成动画：

```powershell
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_protagonist_art_pass_v2.py -- art-check --root .
```

当前已实际得到 `PROTAGONIST_ART_V2_CHECK_PASS meshes=6 triangles=34972 height=1.7017 bytes=1532748 readOnly=true bound=false animated=false`。manifest 同时记录 6 个可见网格、1 个材质面、三张 2048² 纹理、零 Armature/Action，并明确设置 `runtimeReady=false`。这项 PASS 只覆盖静态技术资产；完整视觉图组与负责人视觉确认仍是独立门槛，不得据此开始绑定、动画或运行时接入。

## 四室梦境人工验收

1. 新游戏确认姓名后先显示一次 Esc、I/Tab 与 Xbox Menu/X 操作确认；确认后出生在第一室 `(448, 214)` 安全点，首帧不自动聚焦机关，默认朝向黑板且 SpringArm 实际长度接近 3.4 米。读档和换房不重复提示；顶部没有任务目标栏，只保留紧凑区域名，且不出现“梦境”“醒来”“跳过”。
2. 按[序章 v4 攻略](../design/dream-escape-v4.md)完整走 12 组，确认一次集中回访、材料齐全、三段剪带在礼堂前收齐、四步交接可撤销。盲测者不看攻略，使用[盲测表](prologue-v6-playtest.md)。
3. 每次开新房后返回此前房间；在走廊和四室分别保存/继续，确认区域、坐标、物品、线索和提示等级恢复。
4. 对每个目标尝试至少一个错误道具和错误答案，确认不消耗、不解锁、不自动保存伪进度。
5. 救出晓雨前确认没有主动求助页且 Core 拒绝领取；失败会产生主角思考，只引用实际已知材料。加入后逐级领取 0–3，确认自然对白最多给局部建议、无资源或关系惩罚。
6. 关闭声音或只看屏幕完成辨听；确认文字与波形可用。不得只用颜色区分滤色和控制板状态。
7. 在走廊和四室分别取消一次大头针确认；再于五个区域分别确认刺醒，验证清空梦境物品、无现实纸条、重载不能重进或领奖。
8. 完成出口后确认只显示第一阶段开发完成页，不揭示正式信息、不生成纸条、不标记 `CompletedEscape`。
9. 以 100%/125%/150% 字号检查物品/记录/晓雨分页、数字直输、逐项撤销、确认框的焦点链与取消行为。
10. 第一室默认无悬浮物件标签；12 个目标均只在接近时显示光环与底部提示。检查普通/高对比表现，确认角色和 SpringArm 不能穿过墙、完整天花板、柜、18 组桌椅和关闭的教室门，两条主通道、后排回访通道及所有目标可达。
11. 在洋房与四室分别用鼠标水平移动和手柄右摇杆连续环绕至少一周；确认俯角固定、不会自动回正，移动和交互在转镜时仍可同时进行。五区换场后保持 3D Orbit，并保留镜头相对于门方向的水平夹角。
12. 在至少八个非整 90°镜头角度检查 WASD/左摇杆始终按屏幕方向移动，主角由实际位移转身且停止时保留朝向；晓雨在后三室跟随，异常超距时安全回位，录音机和左机关脚本姿态在关闭、离房或重载后清零，跨房使用安全出生点。
13. 在场景与背包按钮已有焦点时分别用 I/Tab 开关背包，确认 Backspace 不打开，普通物品只读且大头针按钮仍在；背包、谜题、暂停和确认框打开或窗口失焦时鼠标释放、角色与镜头锁定，关闭模态后恢复捕获。若指针意外可见，点击画面可重新捕获。靠近四墙和家具时 SpringArm 拉近但不穿模，镜头不得露出无天花板模型之外。
14. 持有粉笔直接检查黑板，确认无需打开背包；持有时针和上弦钥匙时一次检查时钟应自动装入两件并打开面板。只持有其中一件时安装已有部件并只显示一次成功结果；再检查仍不足时显示相应角色思考，读档后槽位保持。
15. 在第三室持有校准带盘、摇柄和修复保险丝时检查录音机，确认一次安装三件并同次启动重建；错误区域、缺前置、无匹配和重复检查均不消耗物品。
16. 与关闭教室门交互后确认角色、镜头和交互锁定，约 0.35 秒开门并在约 0.15 秒淡出后直接进入洋房；角色不能自行走出门洞。主角应出现在第一室门外；从洋房返回时应出现在教室东门内侧，而不是教室中央。取消/异常时门重新关闭且锁释放。
17. 在洋房依次检查第一至四室门、大门和两个扩展墙段：八个当前交互标记只激活每对锁定/开放状态之一；大门始终关闭且无道具、谜题或 `UnlockedAreas` 副作用；扩展墙段不可交互。确认玩家不能穿过五组门障碍、外墙或走到模型外，中央通路与各门口均可达。
18. 按 Esc 打开梦境暂停，依次检查继续、设置、返回标题和退出；退出二次确认取消后仍停留在暂停，确认后程序正常关闭。标题页的退出同样只显示一个确认框。
19. 首次无提示玩家应在约五分钟内用 `1423` 救出晓雨；三层映射必须能只靠现场图形、音高文字、乐器名和记录页推出。关闭声音后完整完成第二室，并在有声状态确认环境、72 BPM、校准乐器和声学墙声音只增强氛围。
20. 在 720p、900p、1080p 与 150% 字号下完成第二室全部面板；分别测试离房、取消协作、错误输入和中途读档，确认器材间、安装槽、晓雨加入和记录恢复且不软锁。
21. 在礼堂三块控制台完成后先直接操作右侧总闸，确认不会结算；再请晓雨按住左侧机关并离房，返回后确认协作姿态已取消；最后重新执行“左侧按住→右侧总闸”，确认只结算一次且临时姿态不写存档。

至少 3 名偏剧情体验玩家的目标游玩时长 60–90 分钟必须通过真实首次游玩记录确认，自动测试和熟练开发者速通不能替代。

## Legacy 垂直切片人工验收

### 主路径

1. 从标题开始新游戏，输入中文姓名和称谓，也测试空值默认。
2. 确认站立不走时间、实际移动走时间、顶墙不走时间。
3. 完成醒来、四人重逢，并和乔姨交谈。
4. 分三次存档或新游戏覆盖河岸、档案、怀表三项活动。
5. 建立承诺，在手册确认参与者、描述和待履行状态。
6. 睡到第二天，确认 NPC 换位和手册下一可见时段。
7. 阅读工程告示，分别验证线路证据和公司证据；确认承诺变为已履行。
8. 首次异象分别验证【隐瞒】【冒险】和撤离；只有冒险提高暴露。
9. 战斗覆盖基础破坏获胜、观察+安抚联携、战败重试、主动撤退后再挑战。
10. 完成后存档、返回标题、继续游戏，确认世界、关系、承诺、手册和设置一致。

### 软锁与异常

- 第一天立即睡觉、连续睡 3 天后仍可找到主线入口。
- NPC 不在场时能从手册理解下一时段；关键线索仍有世界入口。
- 23:00 后可移动、换图和回旧宅，不强制传送。
- 对话中退出或关闭场景，不应永久持有 ModalGate；事件未提交可重试。
- 连续快速确认不能重复发关系、经验或事件结果。
- 旧/坏存档明确报错；只有 `.bak` 时可恢复；失败不覆盖可用旧档。
- 暂停菜单“脱离卡住”只移动到安全点，不改变剧情事实。

### 输入、显示与辅助

| 项目 | 验收标准 |
| --- | --- |
| 键盘/鼠标 | WASD/方向键、F/空格、I、真实 Tab、J、Esc、F11 均正确，Backspace 不打开背包；3D 第一室物理鼠标 raw delta 可水平环绕，暂停/模态释放指针，UI 确认不重复触发世界。 |
| Xbox 手柄 | 左摇杆、右摇杆、A、X、Y、Menu 正确；提示随最后输入设备切换。 |
| 1280×720 | 默认窗口；HUD 清晰，像素人物最近邻边缘稳定，3D 原生渲染。 |
| 1600×900 | fractional 缩放不裁切 UI；Legacy 像素可非整数缩放，文字与 3D 仍清晰。 |
| 1920×1080 | 窗口与无边框全屏均输出原生 1920×1080，HUD、近景材质和角色边缘正确。 |
| 字号 | 100%/125%/150% 不遮挡关键按钮和选项；电影感样板另测 200% 与菜单／字幕倍率不一致。 |
| 高对比 | 交互提示有不透明深色背景。 |
| 闪烁 | 关闭闪烁后无快速明暗变化；当前切片默认无闪烁。 |
| 故事模式 | 战斗不因来袭失去意志，但仍需完成情境目标。 |
| 中文输入 | Windows IME 可输入；失败时默认姓名仍可继续。 |

### 性能

目标是在 1080p 显示环境和中低端集显上保持 60 FPS，地图切换约 2 秒以内。第一室使用 4× 3D MSAA；记录机器、分辨率、窗口模式、平均/最低 FPS、旋转帧时间和 2D/3D 换场耗时，不要只写“感觉流畅”。

2026-08-21 自动窗口采样（RTX 2080 Ti、Compatibility/OpenGL、60 FPS 上限、每档 240 帧）：

| 请求/实际窗口 | 平均 FPS | 最低瞬时 FPS | 最大旋转帧时 | 第一室场景加载 |
| --- | ---: | ---: | ---: | ---: |
| 1280×720 / 1280×720 窗口 | 60.0 | 59.1 | 16.92 ms | 354.67 ms |
| 1600×900 / 1600×900 窗口 | 60.1 | 59.5 | 16.80 ms | 354.92 ms |
| 1920×1080 / 1920×1080 无边框全屏 | 60.0 | 59.0 | 16.94 ms | 371.22 ms |

该记录对应精修 v2 全室、连续 Orbit 和 `canvas_items` 原生 3D 渲染，证明当前独显达到目标；PBR 资源加载仍低于 2 秒目标。不代表中低端集显、完整四室长时游玩或完整 2D/3D 门转场已经人工验收。

同日完成 Windows 实机短程检查：1280×720 窗口下的显示设置在 100% 与 150% 字号均可完整滚动，F11 可切换到实际 1920×1080 无边框全屏，未发现选项裁切。2026-08-22 又实机验证了 Tab 打开/关闭背包、Esc 打开暂停、继续、退出确认取消与最终正常退出。由于自动桌面输入无法产生与真实设备等价的捕获态 raw mouse delta，物理鼠标环绕仍列为未验收；Xbox 和中低端集显也未验收。

## Dialogue Manager 验收

序章已通过导入、13 段播放、取消重试和 END 后原子完成检查。Legacy 迁移时仍需验证：

- 八个事件都从 `dialoguePath` 和 `startTitle` 实际加载，不再运行硬编码副本；
- 所有 `Narrative.Evaluate`/`Execute` 固定参数重载可被反射调用；
- 自动播放、已读跳过、历史、取消和选项都工作；
- 未知命令、错误参数、缺失 title 和缺失本地化在开始新游戏前失败；
- 取消不完成事件，完成只提交一次；存读档后对白条件与同一 `GameState` 一致。

## Windows x64 导出

`export_presets.cfg` 已有 `Windows x64` preset，输出到 `exports/town-rpg/`，排除文档、测试、工作区和 outputs。当前工作机缺少 Godot 4.6.3 Mono 导出模板，因此截至 2026-08-15 尚未执行发布导出。

模板已由环境负责人提供且版本完全匹配后，执行：

```powershell
$GodotConsole = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe'
& $GodotConsole --headless --path . --export-release 'Windows x64' 'exports/town-rpg/town-rpg.exe'
```

导出成功后不要只验证 exe 存在：

1. 在不打开 Godot 编辑器的环境运行导出版本。
2. 完成标题→新游戏→旧教室→存档→退出→继续的最小路径，并另行回归 Legacy 旧档。
3. 验证 JSON、对白、本地化、标题图和 C# 程序集均被包含。
4. 检查用户目录能创建和恢复存档。
5. 将整个 `exports/town-rpg/` 目录打包为免安装 ZIP，不只单独复制 exe。
6. 解压到包含中文或空格的路径再次运行。

## Legacy 切片盲测通过标准

邀请 2–3 名熟人，不口头解释目标。每人应能完成切片，并能：

- 找到目标 NPC 或从手册判断下一时段；
- 解释【承诺】和暴露征兆；
- 指出至少两项自己的行为改变了后续反馈；
- 在战败或撤退后继续；
- 保存、退出并恢复。

若多数测试者做不到，优先修改切片的因果提示、手册、承诺和风险反馈。以上为 Legacy 标准，不要求新第一章采用暴露/绑架结构；醒后新剧情制作后另按其实际事件验收。2026-09-12 当前仅确认[叙事方向](../design/story-flow.md)，没有新增可供盲测的现实场景。

## 验收记录原则

每次交付记录：提交/源码状态、日期、机器环境、实际命令、通过数、人工覆盖路径、截图或日志位置、未执行项和阻断原因。不得把“代码存在”“自动测试通过”“场景冒烟通过”和“人工完整验收通过”混写成同一状态。

## 2026-09-07 序章 v6 验证

`[已实现]` 125 项 Core/内容测试、Godot 零警告编译、九场景冒烟、13 段正式对白专项验证通过。通过 `res://scenes/testing/PrologueV6Capture.tscn` 在 1280×720 捕获 100%/150% 字体的值日表、纸带、叠片、舞台、波形校准、档案检索及操作区共 24 张图，输出 `outputs/prologue-v6/ui/`。本机 WASAPI 输出设备未能打开，捕获使用 Dummy 音频；没有据此声称真实听音已验收。

`[计划]` 新版全程人工通关、三人盲测、真实 Xbox/raw delta、中低端设备与 Windows 导出仍未验收；旧版性能数字只作历史参考。

## 2026-09-08 UI 重制验证

`[已实现]` 当前单元测试 126/126（125 项原有测试＋1 项 UI 本地化完整性检查），Godot 编译与九场景冒烟通过。`UiRemakeProbe` 额外验证三档字号下的键盘/合成手柄导航确认取消、鼠标拖放及取消、资料往返焦点与编辑值、重复规则、撤销和明确提交。

32 个页面/典型状态 × 三档实际分辨率 × 三档字号，共 288 张图；1920 档使用本机原生全屏并在保存前校验像素尺寸。日志、矩阵、前后对照、Figma 与未验收项见 [UI 重制验收](ui-remake-validation.md)。自动化输入和截图不替代真实 Xbox、完整人工游玩或目标硬件验收。

### 第一室 v6 当前验收入口

当前第一室使用独立重制 v6。执行 `remodel_classroom_v6.py check`、完整 Core/内容测试、Godot Debug 编译、九场景冒烟与 `res://scenes/testing/ClassroomV6Probe.tscn`。图形模式采集三档分辨率；无音频输出设备时使用 `--audio-driver Dummy`，不得据此声称音频通过。具体命令见[第一室 v6](../engineering/classroom-remodel-v6.md)，实际执行、截图和硬件边界见[验收记录](classroom-v6-validation.md)。

### 大厅 v3 当前验收

`[已实现]` 当前大厅加载独立 v3；`remodel_manor_v3.py check` 检查源/临时复导哈希、UV、节点和预算，新增实际 GLB 内容检查后测试总数为 128。九场景冒烟增加四室各两次双向往返、相对镜头朝向与安全落点检查。`ManorV3Probe.tscn` 检查 14 条双向物理路线、5 类阻挡、11 个旧档落点及晓雨绕行；图形模式采集三档分辨率各 17 张图和跟随截图。命令见[大厅 v3](../engineering/manor-remodel-v3.md)，加载、帧时尖峰及人工验收边界见[实际验收记录](manor-v3-validation.md)。

## 09-09 解谜角色提示验收

`[已实现]` 当时 134 项测试、Godot 零警告编译、九场景冒烟与 `DreamThoughtProbe.tscn` 通过。专项包含真实失败提交、材料未齐/齐全、安装后的知识、同次单条反馈、重复轮换、加入前后、三级求助和移除机关关联资料；`UiRemakeProbe` 继续检查键鼠与合成手柄输入。三档分辨率×三档字号各 20 张图，共 180 张；实际日志、尺寸审核与人工边界见[验收记录](dream-hints-validation.md)。

## 第二室音乐教室 v2 专项

`[已实现]` 运行 `remodel_music_v2.py check`、135 项 Core/内容测试、Godot Debug 编译和九场景冒烟。`MusicV2Probe.tscn` 检查真实胶囊通路、门两阶段、旧档、状态节点与晓雨路径，并采集三档分辨率和环绕帧时。`DreamThoughtProbe.tscn -- --music-only` 采集三分辨率×三字号音乐室界面。实际结果、复现命令和未执行项见[音乐室 v2 验收](music-v2-validation.md)。

## 电影感 UI 第一阶段

`[已实现·09-09 样板记录]` 当时 143 项测试、Godot 零警告编译、九场景冒烟及电影感 UI 输入专项通过。三档分辨率×四档字号，另加两种独立菜单／字幕倍率，共 238 张矩阵图与 8 张前后对照。具体日志、复现、焦点修复和人工验收边界见[当前验收](ui-cinematic-validation.md)。其余页面统一待四类样板视觉确认。

## 后两室 v2 与完整输入通关

`[已实现·2026-09-10]` 当前 145 项 Core／内容测试通过（143＋两项实际 GLB／共享布局检查），九场景、13 段对白、UI 与 Legacy 既有回归通过。档案室／礼堂专项验证全部标记、胶囊通行、家具与舞台阻挡、旧位置恢复和晓雨绕行。三分辨率最终场景图 117 张、四档字号及独立字幕图 144 张。

`PrologueE2EProbe.tscn -- --prologue-e2e` 通过正常物理行走和合成输入从标题完成四室：76 段路径、约 571 米、6 处实际存读档、四室大头针取消/确认/终态重载、13 个事件与出口完成页。测试存档只注入到项目 `outputs/`，正式主档、备份与临时文件不变。执行命令、资产数据、完整日志、存档文件、帧时及加载等待见[两室验收](late-rooms-v2-validation.md)。不能用状态夹具的截图专项代替这个完整输入入口，也不能将合成输入通过写成真实设备或盲测通过。

### 双角色动作 v3

2026-09-12：`[已实现]` Core/内容及表现层规则基线为 151 项（145＋6）。九场景 `CharacterMotionProbe` 增加锁输入期间呼吸、交互动画、实例独立与可选姿态回退；独立 `DualCharacterProbe.tscn` 验证五区寻路/近镜头/协作/失焦。完整输入序章仍使用 `PrologueE2EProbe.tscn -- --prologue-e2e`，不写正式存档。图形矩阵、脚部 GLB 审计与实际结果见[双角色验收](character-motion-v3-validation.md)。
