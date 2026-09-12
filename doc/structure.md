# 项目结构

> 文档基线：2026-08-31。仅记录维护者需要追踪的源码、内容与资源；生成目录 `.godot/`、`bin/`、`obj/` 不列入。

```text
town-rpg/
├─ art/
│  ├─ blender/dream_classroom/  第一室 MVP、历史 v2/v3/v4/v5 与当前写实旧教室 v6 权威源；由 .gdignore 排除运行时导入
│  ├─ blender/dream_manor/      洋房室内外 v1、历史内廊 v2、当前宽前厅/短内廊 v3 Blend、PBR 贴图和 manifest 生产源；由 .gdignore 排除运行时导入
│  ├─ blender/dream_late_rooms/ 第三室档案室 v1 与第四室礼堂 v1 权威 Blend 源；由 .gdignore 排除运行时导入
│  ├─ blender/environment-cinematic-samples/ Poly Haven CC0 的项目化 2K 样板材质
│  ├─ blender/characters/       官方 CC0 Human Base 派生源、v1 未绑定技术样模与 v2 静态候选制作源；均不由当前场景加载
│  ├─ concepts/dream_classroom/ 第一室既有概念图、v4 确定性俯视布局、对照板与电影式 v5 目标概念
│  ├─ concepts/dream_manor/     洋房确定性平面、内外景、多角度采用概念板与电影式门厅 v2 目标概念
│  ├─ concepts/environment-cinematic/ CC0 来源板、商业采购前候选板、许可/价格记录与视觉设计哲学
│  ├─ concepts/characters/      主角正式身份锚点、脸发/服装细节板与 3D 三视图
│  └─ sources/cc0/polyhaven/    采用的 Poly Haven 原始 2K 贴图、缩略图、逐文件哈希与许可证清单
├─ assets/generated/
│  ├─ characters/3d/            主角 v2 未绑定静态 GLB/manifest；已过技术检查但 `runtimeReady=false`，当前场景不加载
│  ├─ dream_escape/             31 件目录图标、半身/四向素材、谜题特写与梦境离线 WAV
│  │  ├─ 3d/                    第一室 MVP/v2/v3/v4/v5/当前 v6、洋房 v1/当前内廊 v2、第三室 v1、第四室 v1 GLB 及 manifest；洋房外观 v1 仅离线
│  │  ├─ audio/                 第二室确定性自制环境、72 BPM、音叉和声学墙 WAV；运行时不生成
│  │  ├─ pixel/                 第一室共享图块、机关、环境动效、主角/晓雨 4×4 行走表及清单
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
│  ├─ dream/                    梦境根、3D 洋房公共区与四室，共六个场景
│  ├─ testing/GodotSmokeRunner.tscn
│  └─ world/                    Legacy 旧宅、车站地图
├─ scripts/
│  ├─ application/              GameSession、内容仓库、叙事桥、ModalGate
│  ├─ presentation/             2D/3D 世界与梦境控制器、捕获式第三人称/历史四向相机、碰撞、显示/暂停设置、本机 3D 角色覆盖/精灵回退、背包和谜题 UI
│  ├─ testing/                  九场景冒烟、输入注入、四室及洋房 3D 探针，以及电影式 Lookdev/完整运行时 1080p 捕获
│  └─ tools/                    Pillow 图集/图标/v4 平面图、第二室 WAV、Blender 四室/洋房/完整电影式环境、CC0 来源与评审板、主角候选的离线辅助和只读校验脚本
├─ src/TownRpg.Core/
│  ├─ Content/                  catalog DTO 与严格 JSON
│  ├─ Domain/                   GameState、DreamEscapeState 等可存档状态
│  └─ Services/                 DreamEscapeService 与既有业务规则
├─ tests/TownRpg.Core.Tests/    规则、存档、内容完整性测试
├─ tests/mc-model-test-runtime/ 本机非商业主角 GLB/纹理镜像；由 Git 与 Windows 导出排除，缺失时回退 billboard
└─ doc/                         设计、工程、模块和生产交接文档
   └─ design/dream-escape-v4.md 内容版本 6、12 组谜题、31 道具与一次回访；v2/v3 为历史
```

本项目没有 Web、外部 API、数据库或 SQL schema，因此不建立 `doc/api/`、Web 自测或 `sql/`。

`[已实现]` 音乐室 v2 源位于 `art/blender/dream_music/`，运行时 GLB/manifest 位于 `assets/generated/dream_escape/3d/`；共享布局为 `scripts/presentation/MusicRoomLayout3D.cs`。制作与本机评审脚本为 `scripts/tools/remodel_music_v2.py` 和 `review_music_v2.py`；专项入口为 `scenes/testing/MusicV2Probe.tscn`，字号矩阵复用 `DreamThoughtProbe.tscn -- --music-only`。

`[已实现·仅本机]` `tests/xiaoyu-model-runtime/` 是林晓雨流萤外观镜像；`outputs/character-motion-v3/` 保存双角色制作源/审计/对照。两者均被本地 Git 与导出排除，模型缺失回退 billboard。
