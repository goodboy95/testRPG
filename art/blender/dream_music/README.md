# 音乐室制作源

`[已实现]` 当前采用独立 `music-room-art-pass-v2.blend`，由 `scripts/tools/remodel_music_v2.py` 离线制作，读取 `MusicRoomLayout3D.cs` 的布局。场景使用对应 GLB，不能用历史程序化实现覆盖。

`history/` 保存本轮开始前的 v1 视觉 C# 和 Godot 场景快照。旧版没有独立 Blender 环境源；这些快照只供历史比较。

采用纹理均复用现有 CC0 来源，不含本机受限角色。具体来源、构建和只读检查见[音乐室 v2](../../../doc/engineering/music-remodel-v2.md)。
