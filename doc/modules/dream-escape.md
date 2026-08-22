# 模块：梦境密室

## 责任边界

- `DreamEscapeState`：唯一可存档梦境业务状态；不保存 Godot 对象或 UI 选中项。
- `DreamEscapeService`：校验区域、前置条件、答案、道具使用、提示与结算；错误操作返回 `Changed=false`。
- `GameSession`：包装服务调用；只有 `Changed=true` 才通知界面并写检查点。
- `DreamEscapeController`：将稳定交互 ID 转成会话操作，重建当前房间、打开通用谜题/背包 UI；在 `_Input` 前置阶段统一处理真实 I/Tab/X 背包开关，避免 GUI 焦点吞掉 Tab；不自行判定答案。
- `IDreamAreaRuntime`：统一 2D/3D 区域的交互请求、提示、临时选中道具和辅助设置；不暴露 Godot 节点给 Core。
- `DreamAreaController`：走廊与后三室的 2D 运行时；`DreamInteractionMarker` 只提供稳定 ID 和位置。
- `WorldArea3D` / `DreamAreaController3D`：前者提供可复用的 3D 移动、焦点扫描、Orbit 输入门禁和屏幕相对移动，后者只接入第一室稳定 ID、Core 状态、受控门转场和旧坐标映射。
- `PlayerController3D`：`CharacterBody3D` 的 X/Z 碰撞移动，并将现有 4×4 角色表作为 Fixed-Y `AnimatedSprite3D` billboard；动画方向按实际移动投影到屏幕后选择。
- `ThirdPersonCameraRig`：正式采用方案 A 的 FOV 60°、固定 21°俯角、1.30 米跟随高度、0.22 米肩偏、0.1 秒位置平滑和 3.4 米 SpringArm；鼠标 Orbit 在 `_Input` 中仅响应捕获状态，右摇杆连续水平环绕。模态、失焦或转场时释放鼠标，恢复后重新捕获，点击可见游戏画面可请求重捕获。`QuarterTurnCameraRig` / `WallCutawayController` 只保留历史实验代码。
- `DreamClassroomVisual3D`：从 GLB 查找状态节点，只由 `DreamEscapeState` 派生粉笔、黑板、教师桌和地图柜外观；含关闭门障碍在内的 20 组 `StaticBody3D` 碰撞独立于 GLB。
- `AreaCoordinateMapper3D`：旧二维存档坐标与 3D X/Z 坐标往返；不改变存档格式。
- 旧 `DreamClassroomVisual`、`DreamOriginalClassroomVisual` 和 2D 碰撞脚本保留作分支对比，但当前运行时不提供切换入口。

## 关键不变量

1. 梦境物品不进入现实 `InventoryState`。
2. 已开放房间可回访，未开放房间无法通过换场绕过。
3. 错误输入无副作用；物品只在正确目标成功消耗。
4. 提示等级范围为 0–3，林晓雨加入前不可领取；加入后谜题还必须已被发现、仍未解决。
5. `ExitOpened` 不等同于 `CompletedEscape`；第一阶段不发纸条。
6. 刺醒清空全部梦境物品，重载后不能重进或领奖。

## 内容入口

- 物品：`data/items.json`；图标与特写：`assets/generated/dream_escape/`；当前第一室运行时 GLB 是 `3d/` 下的精修 v2，Blender 源文件位于 `art/blender/dream_classroom/`。同目录的 v3 Blend/GLB 只是离线候选，负责人视觉确认前不切换场景。像素与原图实验基线继续位于 `pixel/` 和 `experimental/`。
- 3D 主角 v1 技术样模、v2 静态候选制作源及官方 CC0 派生源位于 `art/blender/characters/`；v2 静态 GLB/manifest 位于 `assets/generated/characters/3d/`，正式身份锚点、脸发/服装细节板和三视图位于 `art/concepts/characters/`。v1 明确未过视觉门槛；v2 已通过静态技术检查，但仍为 `runtimeReady=false` 并等待负责人视觉确认。两者均未绑定、无动画、未接入运行时，第一室仍使用 2D billboard。
- 场景：`scenes/dream/`。
- 固定谜题和 UX 规格：[四室梦境密室规格](../design/dream-escape.md)。
- 回归：`DreamEscapeServiceTests`、`ContentIntegrityTests` 和九场景 Godot 冒烟。

## 第一室与界面流程

- 新游戏默认位置是第一室前部的安全开阔区，存档坐标为 `(448, 214)`；此处不会开场自动聚焦机关，并允许 SpringArm 完整伸展。走廊仍已开放；已有梦境存档继续使用其保存的区域和坐标，缺失坐标字段的旧 v1 存档仍按旧默认值恢复。
- 黑板只产生观察顺序，窗、时钟和值日表分别产生三条线索；教师桌要求这些线索齐全后才接受 `372`。
- 背包采用“物品/记录/与晓雨商量”分页；最后一页由 `XiaoyuJoined` 控制。选中物品和当前页只属于 UI 临时状态，不写入存档。
- 非交互 HUD 不接收鼠标；游玩时捕获指针，Esc 梦境暂停、背包/谜题/确认框与失焦时释放。标题与暂停的退出操作均使用二次确认，不改变 Core 梦境业务状态。
- 第一室机关状态只读取已有谜题/物品集合；交互光环、当前焦点、镜头 yaw、SpringArm 长度、门动画进度和角色动画帧均为临时表现，不写入存档。
