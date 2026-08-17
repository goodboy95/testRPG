# 项目结构

> 文档基线：2026-08-17。仅记录维护者需要追踪的源码、内容与资源；生成目录 `.godot/`、`bin/`、`obj/` 不列入。

```text
town-rpg/
├─ assets/generated/
│  ├─ dream_escape/             八图标、半身/四向素材、三张谜题特写
│  │  └─ pixel/                 第一室共享图块、机关、环境动效、主角/晓雨 4×4 行走表及清单
│  │  └─ experimental/          第一室原图实验源图、运行时背景、四层遮挡图集及清单
│  └─ provenance.json           采用资产的生成与后处理记录
├─ assets/shaders/
│  └─ pixel_outline.gdshader    第一室接近交互目标时的一像素轮廓
├─ data/
│  ├─ characters.json
│  ├─ events.json
│  ├─ items.json                梦境道具 catalog
│  └─ schedules.json
├─ dialogue/events/             Legacy 两日切片对白源；梦境暂不使用 Dialogue Manager
├─ localization/zh_CN.csv
├─ scenes/
│  ├─ app/Main.tscn
│  ├─ dream/                    梦境根、走廊与四室共六个场景
│  ├─ testing/GodotSmokeRunner.tscn
│  └─ world/                    Legacy 旧宅、车站地图
├─ scripts/
│  ├─ application/              GameSession、内容仓库、叙事桥、ModalGate
│  ├─ presentation/             世界/梦境控制器、可替换第一室视觉、碰撞布局、角色精灵、分页背包和谜题 UI
│  ├─ testing/                  九场景冒烟、原图物理探针及可选第一室五状态截图采集
│  └─ tools/                    梦境像素图集与第一室原图遮挡层的确定性构建/校验脚本
├─ src/TownRpg.Core/
│  ├─ Content/                  catalog DTO 与严格 JSON
│  ├─ Domain/                   GameState、DreamEscapeState 等可存档状态
│  └─ Services/                 DreamEscapeService 与既有业务规则
├─ tests/TownRpg.Core.Tests/    规则、存档、内容完整性测试
└─ doc/                         设计、工程、模块和生产交接文档
```

本项目没有 Web、外部 API、数据库或 SQL schema，因此不建立 `doc/api/`、Web 自测或 `sql/`。
