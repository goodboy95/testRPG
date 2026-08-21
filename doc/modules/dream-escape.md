# 模块：梦境密室

## 责任边界

- `DreamEscapeState`：唯一可存档梦境业务状态；不保存 Godot 对象或 UI 选中项。
- `DreamEscapeService`：校验区域、前置条件、答案、道具使用、提示与结算；错误操作返回 `Changed=false`。
- `GameSession`：包装服务调用；只有 `Changed=true` 才通知界面并写检查点。
- `DreamEscapeController`：将稳定交互 ID 转成会话操作，重建当前房间、打开通用谜题/背包 UI；不自行判定答案。
- `IDreamAreaRuntime`：统一 2D/3D 区域的交互请求、提示、临时选中道具和辅助设置；不暴露 Godot 节点给 Core。
- `DreamAreaController`：走廊与后三室的 2D 运行时；`DreamInteractionMarker` 只提供稳定 ID 和位置。
- `WorldArea3D` / `DreamAreaController3D`：前者提供可复用的 3D 移动、焦点扫描和四方向镜头输入，后者只接入第一室稳定 ID、Core 状态和旧坐标映射。
- `PlayerController3D`：`CharacterBody3D` 的 X/Z 碰撞移动，并将现有 4×4 角色表作为 Fixed-Y `AnimatedSprite3D` billboard；动画方向按实际移动投影到屏幕后选择。
- `QuarterTurnCameraRig` / `WallCutawayController`：0.3 秒正交四向旋转和两近墙裁切；减少闪烁时使用二进制切换，普通模式使用 Compatibility 空间着色器淡变。
- `DreamClassroomVisual3D`：从 GLB 查找状态节点，只由 `DreamEscapeState` 派生粉笔、黑板、教师桌和地图柜外观；19 组 `StaticBody3D` 碰撞独立于 GLB。
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

- 物品：`data/items.json`；图标与特写：`assets/generated/dream_escape/`；第一室 GLB 位于 `3d/`，Blender 源文件位于 `art/blender/dream_classroom/`；像素与原图实验基线继续位于 `pixel/` 和 `experimental/`。
- 场景：`scenes/dream/`。
- 固定谜题和 UX 规格：[四室梦境密室规格](../design/dream-escape.md)。
- 回归：`DreamEscapeServiceTests`、`ContentIntegrityTests` 和九场景 Godot 冒烟。

## 第一室与界面流程

- 新游戏默认位置是第一室，走廊仍已开放；旧梦境存档继续使用其保存的区域和坐标。
- 黑板只产生观察顺序，窗、时钟和值日表分别产生三条线索；教师桌要求这些线索齐全后才接受 `372`。
- 背包采用“物品/记录/与晓雨商量”分页；最后一页由 `XiaoyuJoined` 控制。选中物品和当前页只属于 UI 临时状态，不写入存档。
- 第一室机关状态只读取已有谜题/物品集合；交互光环、当前焦点、镜头角位、裁墙 alpha 和角色动画帧均为临时表现，不写入存档。
