# 模块：梦境密室

## 责任边界

- `DreamEscapeState`：唯一可存档梦境业务状态；不保存 Godot 对象或 UI 选中项。
- `DreamEscapeService`：校验区域、前置条件、答案、道具使用、提示与结算；错误操作返回 `Changed=false`。
- `GameSession`：包装服务调用；只有 `Changed=true` 才通知界面并写检查点。
- `DreamEscapeController`：将稳定交互 ID 转成会话操作，重建当前房间、打开通用谜题/背包 UI；不自行判定答案。
- `DreamAreaController`：玩家接近目标时发出稳定 ID，并结合临时选中道具生成操作提示；场景内 `DreamInteractionMarker` 只提供稳定 ID 对应的位置，不持有业务状态。
- `DreamClassroomVisual`：用双 `TileMapLayer`、机关精灵和低频环境动效表现第一室，并由当前 `DreamEscapeState` 刷新状态；`DreamAreaPainter` 继续绘制尚未迁移的其他区域。
- `DreamCharacterFrames` 与 `PlayerController`：将固定 4×4 图集转换为 `SpriteFrames`；未配置精灵资源的 Legacy 玩家继续使用代码绘制回退。

## 关键不变量

1. 梦境物品不进入现实 `InventoryState`。
2. 已开放房间可回访，未开放房间无法通过换场绕过。
3. 错误输入无副作用；物品只在正确目标成功消耗。
4. 提示等级范围为 0–3，林晓雨加入前不可领取；加入后谜题还必须已被发现、仍未解决。
5. `ExitOpened` 不等同于 `CompletedEscape`；第一阶段不发纸条。
6. 刺醒清空全部梦境物品，重载后不能重进或领奖。

## 内容入口

- 物品：`data/items.json`；图标与特写：`assets/generated/dream_escape/`；第一室图块、机关和角色表：`assets/generated/dream_escape/pixel/`。
- 场景：`scenes/dream/`。
- 固定谜题和 UX 规格：[四室梦境密室规格](../design/dream-escape.md)。
- 回归：`DreamEscapeServiceTests`、`ContentIntegrityTests` 和九场景 Godot 冒烟。

## 第一室与界面流程

- 新游戏默认位置是第一室，走廊仍已开放；旧梦境存档继续使用其保存的区域和坐标。
- 黑板只产生观察顺序，窗、时钟和值日表分别产生三条线索；教师桌要求这些线索齐全后才接受 `372`。
- 背包采用“物品/记录/与晓雨商量”分页；最后一页由 `XiaoyuJoined` 控制。选中物品和当前页只属于 UI 临时状态，不写入存档。
- 第一室机关状态只读取已有谜题/物品集合；交互轮廓、当前焦点和角色动画帧均为临时表现，不写入存档。
