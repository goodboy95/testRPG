# 洋房大厅建模与布局 v3

更新：2026-09-09。模型版本 v3 与序章内容版本 6 独立；本轮没有新增剧情、谜题、可探索楼层或存档字段。

## 当前实现

`[已实现]` 洋房公共区仍使用稳定区域 ID `dream.corridor`，布局改为宽前厅、短内廊与封闭楼梯景观。前厅为 10×8 米、净高 6.6 米，X=-5～5、Z=-8～0；内廊为 7×12 米、净高 4.2 米，X=-3.5～3.5、Z=0～12。连接处保留完整墙肩、门套和横梁，中央通路净宽不小于 3 米。

`[已实现]` 南侧双叶大门位于 `(0,0,-8)`；第一、第二室分别位于 `(-3.5,0,2.4)` 与 `(3.5,0,2.4)`；第三室位于 `(-3.5,0,8.8)`，第四室双开门位于 `(0,0,12)`。四室按原剧情顺序解锁；大门永久关闭，两个扩展模数是无交互、没有门型暗示的普通墙段。八个稳定交互标记始终只激活五个目标。

`[已实现]` 前厅西侧为实体冷壁炉、两张木扶手软椅及边桌；东侧为玄关柜与折返木楼梯。楼梯占地 2.4×4.6 米，半平台高 2.1 米、装饰平台高 4.2 米，入口以固定木格屏封闭；整体为禁入区，没有二层导航、剧情状态或交互目标。内廊使用连续护墙板、成组壁灯、长凳和少量画框。边框地毯没有通行碰撞。

## 数据所有权与兼容

- `MansionLayout3D` 是尺寸、门位、家具占地、安全点和碰撞定义的共同来源；Blender 生成器读取其中的常量与锚点。运行时重新定位现有交互标记，禁止让场景、模型和碰撞各自维护不同的布局。
- 当前独立碰撞为 25 组，其中 7 组家具／楼梯、5 组门障碍；楼梯与门后空间不属于玩家活动区。七个门轴保留原稳定名称和开门角度，开门期间碰撞不撤销，仍由已有换场流程完成区域切换。
- `DreamMansionCoordinateMapper3D` 保留存档矩形 `0…640 × 0…360`；存档 X 映射世界 Z `-8…12`，存档 Y 映射世界 X `-5…5`。恢复位置必须再经实际复合空间与障碍检查，不能把外接矩形全部视为可行走区域。
- 旧大厅存档的相对位置映射到新尺寸；落在内廊收窄区、墙边、家具或楼梯中的位置解析到最近安全点，并通过现有会话操作保存修正。普通读档不强制搬到某扇门，不增加布局版本字段，不改变既有旧内容档兼容规则。
- 四个返厅出生点与门前交互区分离，避免入场立即聚焦房门；反向进入四室的既有出生点保持原值。门口面向、相对镜头 yaw 和动画进度仍为临时表现状态。
- 大厅为 `DreamCompanion3D` 配置已有的两个回调，通过当前实例的 `GroundWaypointNavigator3D` 绕过静态障碍。路径只保存在表现层；第一室现有避障实现与其他区域行为不受此接入改变。

## 制作与渲染

`[已实现]` 权威源为 `art/blender/dream_manor/dream-manor-hall-v3.blend`，运行时加载 `assets/generated/dream_escape/3d/dream-manor-hall-v3.glb`。v1/v2 源保留。旧外观 v1 只是历史离线资产，尺寸不代表新内部布局，游戏不加载它；本轮没有重建外观。

- `remodel_manor_v3.py` 的 `graybox` 生成布局验证版，`build` 生成完整 v3，`export` 从 v3 源导出，`check` 只读检查并在临时目录复导。`graybox` 会替换 v3 资产，因此只供制作阶段使用，正式复验使用 `check`，不要在完成细模后误跑灰模。
- 可编辑源保留实体门框、门扇、五金、炉膛、软椅接合、楼梯踏步／栏杆和灯具玻璃轮廓。重复构件在静态对象内部合并；门叶独立归属对应门轴。
- 木纹按各构件长轴定向，每个导出网格统一一套有效 UV，修改器在保存源之前固定结果；纹理打包到源，Lookdev、相机、灯光和人物不导出。
- 复用已采用的 Poly Haven CC0 木作、灰泥、旧石拼花（`old_mosaic_floor`）和织物；画框图像原样复用已有离线确定性风景纹理；未新增采购、生成图或依赖。提示规格、参考图、工具版本、来源与后处理见 provenance 中的 `dream_manor_hall_runtime_v3`。
- 预算为 180,000 三角面、220 个可见网格、24 材质和 60 MiB GLB；实际统计以 v3 manifest 为准。纹理估计采用实际使用图像的 RGBA8 与 mipmaps 上界，不沿用历史固定 64 MiB 数值。
- Godot 明确使用 Color 环境光与零天空混合；单个方向主光负责侧窗投影，暖色壁灯、两盏吊灯的向下投影灯、侧窗补光与中性偏冷填光提高暗部可读性，两个 ReflectionProbe 分别覆盖前厅和内廊。窗玻璃不阻断日光；没有 SSAO、LightmapGI 或屏幕空间反射依赖。

## 验证入口

在项目根目录使用固定版本工具：

```powershell
& 'D:\ProgramFiles\Blender\blender.exe' --background --python-exit-code 1 --python scripts/tools/remodel_manor_v3.py -- check --root .
& 'C:\Program Files\dotnet\dotnet.exe' build src/TownRpg.Core/TownRpg.Core.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' build TownRpg.csproj --configuration Debug --no-restore
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/ManorV3Probe.tscn
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --audio-driver Dummy --path . --scene res://scenes/testing/ManorV3Probe.tscn
& 'C:\Python314\python.exe' scripts/tools/review_manor_v3.py --root .
```

原 `--capture-dream-manor=<目录>` 九张截图入口继续保留，默认 1920×1080；可选 `--manor-size=1280x720` / `1600x900` / `1920x1080`。由于布局改变，新旧对照按观察主题配对，不标成完全相同机位。

当前新增一项内容测试，直接检查实际 GLB 的门根、人物排除、UV、三角面、材质统计和布局哈希，测试总数由 127 增至 128。大厅专项使用实际胶囊检测 14 条双向路线与 5 类阻挡，验证 11 个旧档位置恢复、各解锁阶段的焦点互斥及晓雨绕行到达。图形模式另外采集三档分辨率各 17 张图和跟随截图，记录前厅／内廊环绕帧时。

专项可追加 `-- --manor-behavior-only` 只复验行为并补拍同行图，不重采三档环绕。headless 与该模式写入 `behavior-metrics.json`，保留图形模式的 `metrics.json` 性能数据。九场景冒烟另覆盖四室各两次往返，共 16 次实际换场与相对镜头朝向检查。

实际执行结果、截图与性能见[大厅 v3 验收记录](../production/manor-v3-validation.md)。自动化、headless 或截图检查均不代表负责人视觉终验；完整盲测、真实 Xbox、物理鼠标 raw delta、GTX 1660 等目标硬件及 Windows 导出仍待验收。本机 `mc_model` 与含该角色的截图继续仅限本机非商业，不提交、分发或导出。
