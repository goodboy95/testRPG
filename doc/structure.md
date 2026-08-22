# 项目结构

> 文档基线：2026-08-22。仅记录维护者需要追踪的源码、内容与资源；生成目录 `.godot/`、`bin/`、`obj/` 不列入。

```text
town-rpg/
├─ art/
│  ├─ blender/dream_classroom/  第一室 MVP、当前运行时 v2 与离线候选 v3 Blender 权威源/贴图生产源；由 .gdignore 排除运行时导入
│  ├─ blender/characters/       官方 CC0 Human Base 派生源、v1 未绑定技术样模与 v2 静态候选制作源；均不由当前场景加载
│  ├─ concepts/dream_classroom/ 第一室精修 v2 采用概念图
│  └─ concepts/characters/      主角正式身份锚点、脸发/服装细节板与 3D 三视图
├─ assets/generated/
│  ├─ characters/3d/            主角 v2 未绑定静态 GLB/manifest；已过技术检查但 `runtimeReady=false`，当前场景不加载
│  ├─ dream_escape/             八图标、半身/四向素材、三张谜题特写
│  │  └─ 3d/                    第一室 MVP/当前 v2 GLB 与离线候选 v3 GLB、PBR 纹理及尺寸/节点/哈希 manifest
│  │  └─ pixel/                 第一室共享图块、机关、环境动效、主角/晓雨 4×4 行走表及清单
│  │  └─ experimental/          第一室原图实验源图、运行时背景、四层遮挡图集及清单
│  └─ provenance.json           采用资产的生成与后处理记录
├─ assets/shaders/
│  ├─ pixel_outline.gdshader    旧 2D 第一室接近交互目标轮廓
│  └─ wall_cutaway.gdshader     历史四方向 3D 实验使用的近墙淡入淡出；当前第一室不加载
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
│  ├─ presentation/             2D/3D 世界与梦境控制器、捕获式第三人称/历史四向相机、碰撞、显示/暂停设置、角色精灵、背包和谜题 UI
│  ├─ testing/                  九场景冒烟、输入注入、3D 相机/状态/物理探针及可选十二张原生 1080p 第一室截图采集
│  └─ tools/                    Pillow 图集/原图、PBR 贴图、Blender MVP/v2/v3、主角 v1 技术样模与 v2 静态候选的离线辅助/只读校验脚本
├─ src/TownRpg.Core/
│  ├─ Content/                  catalog DTO 与严格 JSON
│  ├─ Domain/                   GameState、DreamEscapeState 等可存档状态
│  └─ Services/                 DreamEscapeService 与既有业务规则
├─ tests/TownRpg.Core.Tests/    规则、存档、内容完整性测试
└─ doc/                         设计、工程、模块和生产交接文档
```

本项目没有 Web、外部 API、数据库或 SQL schema，因此不建立 `doc/api/`、Web 自测或 `sql/`。
