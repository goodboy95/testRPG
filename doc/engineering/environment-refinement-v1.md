# 三场景环境精修独立样板

2026-10-05。`[已实现]` 主角卧室、家门口至杂货铺街段、梦境教室的独立审看入口；`[可调整草案]` 当前外观等待负责人比较。真实验证见[验收记录](../production/environment-refinement-v1-validation.md)。

## 范围与入口

`[固定约束]` 负责人确认“按这三个代表场景做样板”，并要求房间模型上的钟表等图像一并精修。本轮允许独立室内／室外样板及离线图像生产，替代先前 v8 制作轮“不改室内、不调用图像生成”的阶段限制。正式村庄仍为 v6，正式梦境场景、Core、SaveEnvelope v1、内容版本 6、引擎版本与 Compatibility 渲染器保持。

在项目根目录 PowerShell 运行：

```powershell
& ./scripts/tools/PreviewEnvironmentRefinement.ps1 -Area home
```

- `1` 卧室、`2` 杂货铺街景、`3` 梦境教室；沿用移动与转镜操作。
- `B` 新旧外观切换，保留当前人物位置及镜头朝向；重新创建场景，样板门灯局部状态不跨新旧切换持久化。
- 教室 `T` 循环五种原有钟表视觉状态；只是审图，无解谜进度、拾取或奖励。`Esc` 释放／捕获鼠标。
- 单独选择街景／教室可用 `-Area village`／`-Area classroom`。原版命令行参数为 `--refinement-baseline`。

场景入口为 `scenes/testing/EnvironmentRefinementPreview.tscn`。独立课堂复制现有空间、碰撞与交互定位，使用不访问剧情会话的审图控制器。住宅和街景复用已有独立漫游控制器。`VillageAreaHost.IndependentVisualProfile` 仅在非正式实例启用；正常正式路由仍由既有选择器决定。

## 实际制作内容

| 场景 | 本轮新增／调整 |
| --- | --- |
| 卧室 | 木材／墙面／布料配色、圆角床垫、枕头和连续下垂被褥、窗帘、窗侧补光、练习簿封面；门轴和室内通路保留 |
| 街段 | 主角家／杂货铺材质、门窗边框与屋檐构件、四棵代表树的枝叶、杂货铺高分辨率招牌；沿用 v4 地形及 v8 已有边缘摆设 |
| 教室 | 与住宅共用木材及材质精度、冷色照明、五状态统一表盘、三张纸面提示；原几何位置及指针保持 |

这是基于已有模型的首轮样板。没有声称重建整个村庄、完整复现概念图、完成角色动画或达到最终美术标准。树冠自然度、床品形态与柔和间接照明仍需负责人评估。

## 源文件、图像与复现

- 保存源：`art/blender/environment-refinement-v1/{home,classroom,village}.blend`，分别派生自保存的 home-anime-v8、classroom-art-pass-v6、village-v8；旧源哈希记录在运行资源 manifest 中。
- 运行模型：`assets/generated/environment-refinement-v1/`，新增共享纹理位于同目录 `textures/`；旧 CC0 材质按哈希引用已有素材。本轮导出为完整审图模型，**没有制作 v7 流式／LOD 包，不能据此宣称通过目标硬件预算**。
- 高精度图像：`graphics/`，表盘 1024×1024、招牌 1024×320、练习簿／纸面 1024×650；启用 mipmap 和各向异性过滤，文字图保持无损导入。表盘不烘入指针，五种原有 3D 状态独立显示。
- 中文字形来自已采用思源黑体。三张纸面从 `DREAM_V6_WORLD_WINDOW`／`DREAM_V6_WORLD_SCHEDULE`／`DREAM_V6_WORLD_DUTY` 原文绘制，不添加剧情线索或推理答案。
- 三张方向图位于 `art/concepts/environment-refinement-v1/`；AI 木纹为源目录的 `oak-albedo.png`。两处 `provenance.json` 记录实际工具、提示词、日期、参考和后处理。工具未返回具体模型版本，记录为未披露；不猜测型号。方向图不是游戏截图。

```powershell
$env:DOTNET_ROOT = 'C:/Users/duwei/.dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& 'D:/ProgramFiles/Blender/blender.exe' --background --python-exit-code 1 --python scripts/tools/author_environment_refinement.py -- export
& 'D:/ProgramFiles/Blender/blender.exe' --background --python-exit-code 1 --python scripts/tools/author_environment_refinement.py -- check
& 'C:/Users/duwei/.dotnet/dotnet.exe' build TownRpg.csproj --configuration Debug --no-restore
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/RefinementGraphicBake.tscn
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --headless --editor --path . --import
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/EnvironmentRefinementProbe.tscn
```

`init` 拒绝覆盖已存在样板；后续编辑保存源并 `export`。`refine` 是首轮网格修复迁移，带执行标记；不应充当反复重建命令。`audit_refinement_sources.py` 清除新源中的 Lookdev／人物辅助物与孤立数据；旧源不变，清理记录在 `source-audit.json`。导出排除人物、动画、相机和光源。受限人物及含其画面的媒体继续仅本机保留。
