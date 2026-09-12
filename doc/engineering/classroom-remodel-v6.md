# 第一室建模与布局 v6

更新：2026-09-09。环境资产版本 v6 与序章内容版本 6 是两套独立版本号；本轮不改变 Core 剧情、谜题、存档格式或内容版本。

## 当前实现

`[已实现]` 第一室使用独立 `classroom-art-pass-v6.blend` / `classroom-art-pass-v6.glb`。从 v5 权威源保留房间、稳定状态根和门窗位置，重制家具、五金、窗帘、门框、灯具与吊扇，采用旧式东亚学校的木面钢架桌椅、灰白抹灰墙、褪色绿墙裙及水磨石地面。v5 保留为历史源，禁止用早期构建器覆盖。

`[已实现]` 房间仍为 12×9×4 米，四墙及天花板保留。十二组课桌按四列三排组织：X 为 -3.55、-2.25、2.25、3.55；Z 为 -1.00、0.65、2.25。桌高 0.75 米，椅面高 0.44 米。碰撞包含椅子；中央净宽 3.68 米、两侧主要通道净宽 1.74 米、后排净宽 1.24 米，均按 11.4×8.4 米活动边界计算。

`ClassroomLayout3D` 集中维护桌阵，Blender 脚本读取同一份坐标；Godot 验证导出桌阵位置和实际碰撞一致。共 24 组碰撞：原有九组建筑/门碰撞、十二组桌椅、教师桌、地图柜和资料板。地图柜碰撞扩至实体宽度。旧存档落入家具时仍通过 `ResolveSpawn` 修正；新游戏 `(448,214)` 和返室 `(592.4,305.8)` 保持不变。

`[已实现]` 维护卡和值日便签分别贴在后排左右外侧课桌上；晨间作息表附在前部左侧独立资料板上。纸面线索与机关保持至少 2 米，11 个交互点保留。黑板拓显使用世界空间本地化文字替代旧三条色块，文字内容与现有谜题一致。机关外观继续只从 Core 派生；退役气窗盒没有恢复交互。

`[已实现]` 第一室为晓雨跟随配置独立的安全位置与移动回调。直达路线畅通时沿直线跟随；障碍时沿已有安全点构成的可见连线绕行，图连接缓存于当前场景。路线和位置只存在于表现层；其他区域默认跟随行为不变。

## 制作与渲染

- `scripts/tools/remodel_classroom_v6.py`：`graybox` 输出布局验证资产；`build` 从保留的 v5 源创建 v6；`export` 导出 v6 并更新清单；`check` 只读检查源、布局、共享网格、预算、哈希及临时复导一致性。
- `scripts/tools/review_classroom_v6.py`：从布局常量生成矢量俯视图及本机 HTML 对照页，不生成或修改位图画作。
- 复用已经采用的 Poly Haven CC0 材质；没有新增采购、生成图或外部依赖。制作规格、工具版本、参考、参数和哈希记录在 `assets/generated/provenance.json` 的 `dream_classroom_cinematic_runtime_v6`。
- 权威源包含打包纹理；GLB 排除 Lookdev 角色、灯光与相机。十二组课桌共用一个网格。上限继续为 150,000 三角面、200 个同时可见网格、24 材质、60 MiB GLB；当前精确统计以 v6 manifest 为准，纹理估计按实际使用图像及 mipmaps 计算。
- 第一室明确使用 Color 环境光，避免引用未配置 Sky；西窗日光与中性补光配合两个 ReflectionProbe。修复旧西墙缺口和东侧门洞覆盖；窗玻璃不遮断日光。Compatibility 不依赖 SSAO、LightmapGI 或屏幕空间效果。

## 复验命令

在项目根目录执行，工具版本保持不变：

```powershell
& 'C:\Python314\python.exe' scripts/tools/review_classroom_v6.py --root .
& 'D:\ProgramFiles\Blender\blender.exe' --background --python-exit-code 1 --python scripts/tools/remodel_classroom_v6.py -- check --root .
& 'C:\Program Files\dotnet\dotnet.exe' build src/TownRpg.Core/TownRpg.Core.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' build TownRpg.csproj --configuration Debug --no-restore
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/ClassroomV6Probe.tscn
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --audio-driver Dummy --path . --scene res://scenes/testing/ClassroomV6Probe.tscn
```

新增一项 Core 内容测试，检查实际 GLB 的共享桌椅、状态根、通道约束和无人物导出；原有测试继续保留，预期总数由 126 增至 127。引擎专项检查 11 个目标的焦点、10 条双向胶囊扫掠路线、家具内安全恢复、模型包围盒和晓雨绕行到达。渲染模式另采集三档分辨率的全景、机关状态、家具、碰撞和环绕帧时。

原 15 张课堂截图入口继续可用，新增可选 `--classroom-size=1280x720` / `1600x900` / `1920x1080`，默认仍为 1920×1080。对照页使用 v5 原截图与 v6 同机位截图；移动了机位的门/黑板近景不标为同机位对照。

## 验收记录与边界

实际执行日志、截图、性能和视觉检查结论记录于 [第一室 v6 验收](../production/classroom-v6-validation.md)。不把结构检查或 headless 冒烟等同人工操作和视觉批准。

`[计划]` 完整玩家盲测、真实 Xbox、物理鼠标 raw delta、GTX 1660 等目标硬件及 Windows 导出仍待验收。渲染验证使用本机 RTX 2080 Ti；音频设备不可用时显式使用 Dummy，不计为音频验收。本机 `mc_model` 及含该角色的截图仍仅限本机非商业，不提交、推送或导出。
