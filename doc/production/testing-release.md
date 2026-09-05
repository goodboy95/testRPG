# 测试与 Windows 发布

## 环境基线

项目只支持以下开发基线：

- Windows x64；
- Godot 4.6.3 stable Mono/.NET；
- Compatibility/OpenGL；
- .NET SDK 10.0.400 用于命令行，项目目标框架 `net8.0`；
- Dialogue Manager v3.10.4（当前未 vendored）；
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

截至 2026-09-02 的基线是 **104/104 通过，0 失败，0 跳过**。覆盖内容版本 5、28 个正式谜题、34 件道具、11 个安装槽、新旧稳定 ID、线索映射与缺失查询、目标—道具目录、单件/多件/部分自动安装、盲猜不阻塞、依赖图无环、三条音乐支线、两轮回访、四个协作机关、组合产物先入包、错误无副作用、重复幂等、进行中 v4 梦境存档拒绝和完成 v4 终态兼容。

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
GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=8 items=34
```

冒烟验证 Autoload、九场景、34 件物品目录、`canvas_items` 显示策略和窗口预设。第一室检查 v5 GLB/PBR、新游戏安全点、12 个交互点、三张分散纸面线索、29 组碰撞和门动画；第二室检查 18 个唯一可达交互点、三张分散记录、16 组碰撞、两个 ReflectionProbe、晓雨跟随、五个离线 WAV 和静音文字替代；第三、第四室分别检查 Orbit、15 个唯一目标、v1 GLB、18/21 组碰撞、2/3 枚 ReflectionProbe、坐标往返、非法旧坐标修正和全部目标可达。自动化统一校验线索—机关距离至少 2 米、门内安全点不自动聚焦、锁后线索显隐、洋房门状态和 3D→3D 双向转场；礼堂还验证母带白噪声、右总闸必须经过当前实例的左机关姿态，以及离房/重载清零。

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

### 电影式环境完整运行时验收

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

当前完整运行时只证明 RTX 2080 Ti 上的 Compatibility 捕获链可用；没有 GTX 1660 级性能结论。两份 schema v2 manifest 必须保持 `status=cinematic-full-runtime`、`sampleOnly=false`、`runtimeDefault=true`，并用内容测试证明 GLB 路径、字节数、SHA-256 和预算匹配。场景必须分别引用教室 v5 与洋房内廊 v2；不得因此启用 LightmapGI，也不得把环境验收当作正式主角 v2 的视觉批准。本机非商业 `mc_model` 默认覆盖单独验证。

教室 v3/v4 目前只作离线/历史候选验证；3D 主角 v1 只是明确未通过视觉门槛的技术样模，原画式 v2 已生成静态 GLB/manifest 并通过技术检查，但仍在负责人视觉确认门槛之前。v1/v2 候选不进入运行时冒烟，也不得替代当前教室 v5 或正式可分发的二维角色基线。本机存在 `tests/mc-model-test-runtime/caelus-pbr-test.glb` 时，九场景冒烟额外要求 `PlayerController3D` 选择该 PBR 模型、隐藏 billboard、具备 `idle/walk/jog` 并正确应用世界朝向；资源缺失时验证 billboard 回退。

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

v3 检查仍关注其十组共享课桌、门枢轴、零缺失外部文件、纹理和预算；它与旧镜头对照入口都只作为历史候选回归，不修改第一室当前 v4 运行时引用。主角 v1/v2 检查关注官方 CC0 来源、比例、闭合网格、三角面、材质、GLB 哈希，以及明确不存在运行时骨骼和动画。

主角 v2 的第一阶段只验收静态美术，不复用 v1 的“技术导出通过”结论。必须先核对三项正式参考的哈希与 provenance，再输出同尺度 A-pose 正/侧/背、面部正/3/4/侧、自然站姿四向、服装与鞋近景、方案 A 默认出生构图及四个环绕角度。默认构图中角色屏幕高度目标为 28%–31%；同时报告身高、可见三角面、网格、材质、纹理和缺失外链。负责人确认脸型、头发、服装轮廓、材质和教室比例前，不执行 v2 的绑定、`idle/walk/jog`、`AnimationTree`、Godot 接入或运行时替换；本机 `mc_model` 覆盖不改变这项门槛。

当前独立捕获的实测值为：默认及前侧角度 `36.47%`，且 SpringArm 完整保持 `3.4m`；后侧角度因墙体将 SpringArm 压缩至 `2.430–2.444m`，屏幕高度升至 `49.25%–49.49%`。这说明原定 `28%–31%` 尚未通过，但不是 `1.7017m` 模型或捕获工具的尺度错误：旧二维 sprite 的有效透明边界只对应约 `1.38–1.45m`，不能直接作为真实身高 3D 人物的等价口径。视觉确认后应在“约 `35%–38%` 的真实 3D 构图”与“约 `4.0–4.2m` 臂长或更宽 FOV”之间另行确认；不得缩小角色世界尺寸。后侧 SpringArm 压缩也必须作为独立镜头舒适性问题处理。

v2 候选资产落地后执行以下只读检查；它只能检查静态资产，不会绑定或生成动画：

```powershell
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_protagonist_art_pass_v2.py -- art-check --root .
```

当前已实际得到 `PROTAGONIST_ART_V2_CHECK_PASS meshes=6 triangles=34972 height=1.7017 bytes=1532748 readOnly=true bound=false animated=false`。manifest 同时记录 6 个可见网格、1 个材质面、三张 2048² 纹理、零 Armature/Action，并明确设置 `runtimeReady=false`。这项 PASS 只覆盖静态技术资产；完整视觉图组与负责人视觉确认仍是独立门槛，不得据此开始绑定、动画或运行时接入。

## 四室梦境人工验收

1. 新游戏确认姓名后先显示一次 Esc、I/Tab 与 Xbox Menu/X 操作确认；确认后出生在第一室 `(448, 214)` 安全点，首帧不自动聚焦机关，默认朝向黑板且 SpringArm 实际长度接近 3.4 米。读档和换房不重复提示；顶部没有任务目标栏，只保留紧凑区域名，且不出现“梦境”“醒来”“跳过”。
2. 在后排左课桌调查窗扣维护卡，再走到西墙窗扣，确认两处不会抢焦点且记录页显示“上—下—中”；继续完成讲台作息表→时钟 `7:00`、后排右便签→值日表第 2 项、`372` 教师桌。第二室检查三张分散记录与对应机关；第三室完成五份登记档案→“合唱—广播—美术—田径”、录音机与计数器；第四室完成 `3—1—4—2` 吊杆组、放映机、母带、三控制台和两轮回访。
3. 每次开新房后返回此前房间；在走廊和四室分别保存/继续，确认区域、坐标、物品、线索和提示等级恢复。
4. 对每个目标尝试至少一个错误道具和错误答案，确认不消耗、不解锁、不自动保存伪进度。
5. 救出晓雨前确认没有提示页且 Core 拒绝领取；加入后逐级领取 0–3，确认无资源或关系惩罚。
6. 关闭声音或只看屏幕完成辨听；确认文字与波形可用。不得只用颜色区分滤色和控制板状态。
7. 在走廊和四室分别取消一次大头针确认；再于五个区域分别确认刺醒，验证清空梦境物品、无现实纸条、重载不能重进或领奖。
8. 完成出口后确认只显示第一阶段开发完成页，不揭示正式信息、不生成纸条、不标记 `CompletedEscape`。
9. 以 100%/125%/150% 字号检查物品/记录/晓雨分页、数字直输、逐项撤销、确认框的焦点链与取消行为。
10. 第一室默认无悬浮物件标签；12 个目标均只在接近时显示光环与底部提示。检查普通/高对比表现，确认角色和 SpringArm 不能穿过墙、完整天花板、柜、18 组桌椅和关闭的教室门，两条主通道、后排回访通道及所有目标可达。
11. 在洋房与四室分别用鼠标水平移动和手柄右摇杆连续环绕至少一周；确认俯角固定、不会自动回正，移动和交互在转镜时仍可同时进行。五区换场后保持 3D Orbit，并保留镜头相对于门方向的水平夹角。
12. 在至少八个非整 90°镜头角度检查 WASD/左摇杆始终按屏幕方向移动，主角由实际位移转身且停止时保留朝向；晓雨在后三室跟随，异常超距时安全回位，录音机和左机关脚本姿态在关闭、离房或重载后清零，跨房使用安全出生点。
13. 在场景与背包按钮已有焦点时分别用 I/Tab 开关背包，确认 Backspace 不打开，普通物品只读且大头针按钮仍在；背包、谜题、暂停和确认框打开或窗口失焦时鼠标释放、角色与镜头锁定，关闭模态后恢复捕获。若指针意外可见，点击画面可重新捕获。靠近四墙和家具时 SpringArm 拉近但不穿模，镜头不得露出无天花板模型之外。
14. 持有粉笔直接检查黑板，确认无需打开背包；持有时针和上弦钥匙时一次检查时钟应自动装入两件并打开面板。只持有其中一件时先安装已有部件、提示仍缺部件，读档后槽位保持。
15. 在第三室持有校准带盘、摇柄和修复保险丝时检查录音机，确认一次安装三件并同次启动重建；错误区域、缺前置、无匹配和重复检查均不消耗物品。
16. 与关闭教室门交互后确认角色、镜头和交互锁定，约 0.35 秒开门并在约 0.15 秒淡出后直接进入洋房；角色不能自行走出门洞。主角应出现在第一室门外；从洋房返回时应出现在教室东门内侧，而不是教室中央。取消/异常时门重新关闭且锁释放。
17. 在洋房依次检查第一至四室门、大门和两个扩展墙段：八个当前交互标记只激活每对锁定/开放状态之一；大门始终关闭且无道具、谜题或 `UnlockedAreas` 副作用；扩展墙段不可交互。确认玩家不能穿过五组门障碍、外墙或走到模型外，中央通路与各门口均可达。
18. 按 Esc 打开梦境暂停，依次检查继续、设置、返回标题和退出；退出二次确认取消后仍停留在暂停，确认后程序正常关闭。标题页的退出同样只显示一个确认框。
19. 首次无提示玩家应在约五分钟内用 `1423` 救出晓雨；三层映射必须能只靠现场图形、音高文字、乐器名和记录页推出。关闭声音后完整完成第二室，并在有声状态确认环境、72 BPM、音叉和声学墙声音只增强氛围。
20. 在 720p、900p、1080p 与 150% 字号下完成第二室全部面板；分别测试离房、取消协作、错误输入和中途读档，确认器材间、安装槽、晓雨加入和记录恢复且不软锁。
21. 在礼堂三块控制台完成后先直接操作右侧总闸，确认不会结算；再请晓雨按住左侧机关并离房，返回后确认协作姿态已取消；最后重新执行“左侧按住→右侧总闸”，确认只结算一次且临时姿态不写存档。

目标游玩时长 90–120 分钟必须通过真实首次游玩记录确认，自动测试和熟练开发者速通不能替代。

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
| 字号 | 100%/125%/150% 不遮挡关键按钮和选项。 |
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

插件接入后至少验证：

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
2. 完成标题→新游戏→旧宅→存档→退出→继续的最小路径。
3. 验证 JSON、对白、本地化、标题图和 C# 程序集均被包含。
4. 检查用户目录能创建和恢复存档。
5. 将整个 `exports/town-rpg/` 目录打包为免安装 ZIP，不只单独复制 exe。
6. 解压到包含中文或空格的路径再次运行。

## 盲测通过标准

邀请 2–3 名熟人，不口头解释目标。每人应能完成切片，并能：

- 找到目标 NPC 或从手册判断下一时段；
- 解释【承诺】和暴露征兆；
- 指出至少两项自己的行为改变了后续反馈；
- 在战败或撤退后继续；
- 保存、退出并恢复。

若多数测试者做不到，暂停 Day 3 以后内容，优先修改因果提示、手册、承诺和风险反馈。

## 验收记录原则

每次交付记录：提交/源码状态、日期、机器环境、实际命令、通过数、人工覆盖路径、截图或日志位置、未执行项和阻断原因。不得把“代码存在”“自动测试通过”“场景冒烟通过”和“人工完整验收通过”混写成同一状态。
