# town-rpg / 《雾山旧轨》

`town-rpg` 是一款面向 Windows x64 的单机 2D 剧情生活 RPG。玩家扮演暑假回到故乡的大学生，在山间铁路小镇安排日常、建立关系，并调查开发工程背后的封印真相。

当前仓库交付的是约 **15–30 分钟**的“两日生活＋首次异象”垂直切片；计划中的完整版本为 3–5 小时、约七个内容日。两者必须明确区分，不能把剧情大纲写成已实现功能。

## 接手入口

- [交接文档首页](doc/README.md)：推荐阅读顺序、当前状态、术语和接手第一周清单。
- [项目代理约束](AGENTS.md)：技术红线、剧情红线、工作流程和提交前检查。
- [当前实现](doc/engineering/current-implementation.md)：现在能运行什么、入口在哪里、哪些尚未完成。
- [完整剧情流程（剧透）](doc/design/story-flow.md)：Day 1–7 的共享主线、双路线和终局场景规格。
- [测试与发布](doc/production/testing-release.md)：打开、构建、测试、冒烟和人工验收命令。

## 最短启动方式

使用 **Godot 4.6.3 stable Mono/.NET** 打开根目录的 `project.godot`，等待 C# 编译完成后按 `F6` 运行当前场景或按 `F5` 运行主场景。当前工作机的控制台可执行文件位于：

```text
D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe
```

这个绝对路径只是当前工作机记录；换机后应改为实际的 Godot 4.6.3 Mono 路径，不能换用标准版或其他引擎版本。

## 已验证状态

- Godot 4.6.3 Debug/Release C# 编译通过。
- Core、应用规则和内容契约测试 **71/71 通过**。
- 主场景 headless 启动、三场景实例化和 Compatibility/OpenGL 标题取帧通过。
- 引擎冒烟成功标志：`GODOT_SMOKE_PASS scenes=3 characters=5 schedules=21 events=8`。

尚未完成：两日流程全程人工验收、键鼠与 Xbox 手柄手感、1080p 整数缩放、性能计时、Dialogue Manager v3.10.4 实际接入，以及 Windows x64 导出。具体优先级见[交接路线图](doc/production/roadmap-handoff.md)。
