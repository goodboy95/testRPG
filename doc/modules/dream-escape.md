# 模块：梦境密室

## 责任边界

- `DreamEscapeState`：唯一可存档梦境业务状态；不保存 Godot 对象或 UI 选中项。
- `DreamEscapeService`：校验区域、前置条件、答案、道具使用、提示与结算；错误操作返回 `Changed=false`。
- `GameSession`：包装服务调用；只有 `Changed=true` 才通知界面并写检查点。
- `DreamEscapeController`：将稳定交互 ID 转成自动道具使用或会话操作，重建当前房间、打开通用谜题/背包 UI；在 `_Input` 前置阶段统一处理真实 I/Tab/X 背包开关，避免 GUI 焦点吞掉 Tab；不自行判定答案。
- `IDreamAreaRuntime`：统一 2D/3D 区域的交互请求、提示和辅助设置；不传递临时道具状态，也不暴露 Godot 节点给 Core。
- `WorldArea3D` / `DreamAreaController3D` / `DreamMusicRoomController3D` / `DreamArchiveRoomController3D` / `DreamAuditoriumController3D` / `DreamMansionController3D`：通用层提供可复用的 3D 移动、焦点扫描、Orbit 输入门禁和屏幕相对移动；各适配器接入对应稳定 ID、Core 状态、门转场与坐标映射。
- `DreamLateRoomVisual3D` / `DreamArchiveVisual3D` / `DreamAuditoriumVisual3D`：第三、第四室共享独立碰撞、安全落点和程序化状态表现；持久外观从 Core 派生，面板预览和晓雨机关姿态通过可选瞬时接口传递。
- `PlayerController3D`：`CharacterBody3D` 的 X/Z 碰撞移动；当前工作副本优先加载本机 `mc_model` PBR GLB，按实际位移旋转并切换 `idle/walk/jog`，SpringArm 过近时隐藏网格。资源缺失或动画结构不完整时回退到现有 4×4 Fixed-Y `AnimatedSprite3D` billboard，回退动画方向按实际移动投影到屏幕后选择。
- `ThirdPersonCameraRig`：正式采用方案 A 的 FOV 60°、固定 21°俯角、1.30 米跟随高度、0.22 米肩偏、0.1 秒位置平滑和 3.4 米 SpringArm；鼠标 Orbit 在 `_Input` 中仅响应捕获状态，右摇杆连续水平环绕。模态、失焦或转场时释放鼠标，恢复后重新捕获，点击可见游戏画面可请求重捕获。`QuarterTurnCameraRig` / `WallCutawayController` 只保留历史实验代码。
- `DreamClassroomVisual3D`：从 GLB 查找状态节点，只由 `DreamEscapeState` 派生粉笔、黑板、教师桌和地图柜外观；29 组 `StaticBody3D` 碰撞独立于 GLB。
- `DreamMusicVisual3D` / `DreamCompanion3D`：程序化 PBR 音乐教室的 16 组碰撞与 Core 派生机关状态，以及不写存档的晓雨 Fixed-Y billboard 跟随、超距回位和动画。
- `DreamMansionVisual3D`：加载洋房内廊 GLB并维护独立简化碰撞、房门铰链与安全落点；完整外观 GLB不由运行时场景加载。
- `AreaCoordinateMapper3D`：旧二维存档坐标与 3D X/Z 坐标往返；不改变存档格式。
- `DreamAreaSpawnCatalog`：按来源＋目标区域解析门口出生位置和默认朝向；普通读档仍优先恢复实际保存位置，镜头连续性上下文只存在于表现层。
- 旧 `DreamClassroomVisual`、`DreamOriginalClassroomVisual` 和 2D 碰撞脚本保留作分支对比，但当前运行时不提供切换入口。

## 关键不变量

1. 梦境物品不进入现实 `InventoryState`。
2. 已开放房间可回访，未开放房间无法通过换场绕过。
3. 错误输入无副作用；物品只在正确目标成功消耗。
4. 提示等级范围为 0–3，林晓雨加入前不可领取；加入后谜题还必须已被发现、仍未解决。
5. `ExitOpened` 不等同于 `CompletedEscape`；第一阶段不发纸条。
6. 刺醒清空全部梦境物品，重载后不能重进或领奖。

## 内容入口

- 物品：`data/items.json`，当前 34 件；图标、特写与离线 WAV：`assets/generated/dream_escape/`。第一室加载 `classroom-art-pass-v5.glb` 并补充程序化纸面线索；第二室为程序化 3D 表现；第三、第四室分别加载 `dream-archive-room-v1.glb` 与 `dream-auditorium-v1.glb` 并叠加 Core 派生状态层；洋房加载 `dream-manor-hall-v2.glb`。
- 3D 主角 v1 技术样模、v2 静态候选制作源及官方 CC0 派生源位于 `art/blender/characters/`；v2 静态 GLB/manifest 位于 `assets/generated/characters/3d/`，正式身份锚点、脸发/服装细节板和三视图位于 `art/concepts/characters/`。v1 明确未过视觉门槛；v2 已通过静态技术检查，但仍为 `runtimeReady=false` 并等待负责人视觉确认。两者均未绑定、无动画、未接入运行时。本机 `mc_model` 是另一路仅限非商业、禁止再分发的默认覆盖，位于被 Git 和导出排除的 `tests/mc-model-test-runtime/`；缺少该目录时第一室、第二室与洋房仍使用 2D billboard。
- 场景：`scenes/dream/`。
- 固定谜题和 UX 规格：[四室梦境密室规格](../design/dream-escape.md)。
- 设计冻结：[四室梦境密室 v2](../design/dream-escape-v2.md)。34 道具/28 谜题全链、第三/第四室正式机关、两轮回访和白噪声母带均已实现并待人工终验；成功后的正式封存载体仍属于后续阶段。
- 回归：`DreamEscapeServiceTests`、`ContentIntegrityTests` 和九场景 Godot 冒烟。

## 第一室与界面流程

- 新游戏默认位置是第一室前部的安全开阔区，存档坐标为 `(448, 214)`；此处不会开场自动聚焦机关，并允许 SpringArm 完整伸展。洋房公共区仍以稳定 ID `dream.corridor` 从开始开放；已有梦境存档继续使用其保存的区域和坐标，缺失坐标字段的旧 v1 存档仍按旧默认值恢复。
- 黑板产生取数顺序，维护卡、作息表和值日便签分别提供窗扣方向、`7:00` 和第 2 项记录。未调查记录时面板不泄露内容，但正确答案仍可提交；教师桌只保留黑板、时钟和值日表已完成的结构前置。
- 背包采用“物品/记录/与晓雨商量”分页；最后一页由 `XiaoyuJoined` 控制。普通物品没有选用按钮，检查目标时由 `TargetItemUses` 自动应用匹配道具；当前页只属于 UI 临时状态，不写入存档。
- 梦境顶部只保留紧凑区域名，非交互 HUD 不接收鼠标；新游戏首次进入第一室前显示 Esc 与 I/Tab/X 操作确认。游玩时捕获指针，Esc 梦境暂停、背包/谜题/确认框与失焦时释放。标题与暂停的退出操作均使用二次确认，不改变 Core 梦境业务状态。
- 第一室机关状态只读取 `SolvedPuzzles`、`InstalledParts` 与物品集合；窗台暗格、时针拾取、时钟五态、值日表三态、黑板、教师桌、地图柜和气窗盒均不保存第二份表现状态。交互光环、焦点、镜头和动画仍为临时状态。
- 第一室与洋房双向换场按来源＋目标选择门口安全点：离开教室后出现在第一室门外，从洋房返回后出现在教室东门内侧。锁闭大门只提供无状态副作用的表现层反馈，不进入 `UnlockedAreas`，两个预留扩展墙段不提供交互。
- 第一室七题加三张分散纸面线索、时针和出口，共 12 个运行时交互目标；第二室八题、五个拾取点、三张分散记录、门口和晓雨提示，共 18 个目标；第三、第四室各 15 个唯一 3D 目标。自动冒烟校验所有答案线索与对应机关至少相距 2 米，并检查安全出生、碰撞和目标可达性。
