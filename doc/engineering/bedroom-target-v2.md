# 卧室专项三维重建 v2

2026-10-05。`[已实现]` 独立完整三维卧室、五个固定审图视角与原生移动／镜头组件漫游，采用离线 LightmapGI。`[固定约束]` 原住宅、三场景精修 v1、正式入口、Core、SaveEnvelope v1、内容版本 6 均保留；本轮不代表正式采用。[实际验证与美术边界](../production/bedroom-target-v2-validation.md)。

## 入口与操作

在根目录 PowerShell 执行 `./scripts/tools/PreviewBedroomTarget.ps1`，或用 Godot 4.6.3 Mono 打开 `scenes/testing/BedroomTargetReview.tscn`。

| 操作 | 效果 |
| --- | --- |
| 1／2／3／4／5 | 门口、床边、桌边、窗边、反向固定视角；隐藏角色 |
| Tab | 切换实际走动；沿用 WASD、鼠标与既有镜头组件 |
| F | 房门开关；走动时需靠近门口 |
| L | 实时台灯开关，不改变已烘焙的白天光照 |
| Esc | 走动时暂停并释放鼠标；再次按下恢复 |
| R | 恢复初始位置、门灯与门口视角 |

独立预览不通过正式住宅路由，不执行任务、跨日、自动保存或正式交互。`BedroomTargetReview` 持有自己的门灯状态，复用 `WorldArea3D`、`PlayerController3D` 与 `ThirdPersonCameraRig`。走动人物沿用既有本机外观／缺失时 sprite 回退，没有制作、复制或导出新角色；含角色截图仍只在本机保存。

## 空间与资产

室内净空间为 4.2×4.6×2.9 米。床在左，衣柜在后，右窗下为书桌；门口保留 1.2 米开口和短走廊，床侧至椅子最近主通道约 1.36 米（碰撞边界间），桌椅外侧并非独立穿行通道。碰撞用独立房间／家具盒与角色半径 0.25 米胶囊，镜头用半径 0.12 米球形 SpringArm。

- 床架、衣柜、书桌和椅子分别制作板厚、接缝、抽屉、把手、支撑与倒角。木纹低对比，按各构件长轴排列。背包为独立厚度体积、口袋和悬挂带。
- 被子用 Blender 5.2 Cloth，62×78 网格、55 帧、quality 8、床垫碰撞与自碰撞；应用后加细分、0.029 米厚度及局部大褶皱修形。垂边按实际游戏检查向床架外侧平滑移动，保留上下表面的间距。枕头、坐垫使用封闭鼓胀曲面及缝边形状。运行时没有布料解算。
- 窗框、窗台、窗帘挂杆与布面均有实体厚度。窗外有独立多层房屋、树枝叶和山体，靠窗侧看有真实视差，不以概念图替代窗外。
- 书本、无题面内容的横线作业纸、笔筒、水杯、鞋、收纳盒与盆栽仅表现正常生活，不添剧情线索。表盘 1024×1024、作业本封面 1024×650，复用三场景 v1 已有清晰独立图像；表盘指针独立几何，仅样板显示，不接游戏时钟。纸页用曲面和横线几何，封面文字沿用已有图像，没有生成伪文字。

完整来源记录：`art/blender/bedroom-target-v2/provenance.json`、`cloth-production.json`、`refinement-v1.json`、`review-corrections.json`、`contact-correction.json`、`cover-correction.json`。参考图为 `art/concepts/environment-refinement-v1/bedroom-target.png`，制作方式是脚本原创网格、真实布料求解及局部网格修正，不能写成手工高模雕刻。本轮没有新调用 AI 图像生成或购入素材；复用已有合格 CC0 微弱布料／墙面法线及 v1 图像，其许可和图像来源记录继续保留。

## 保存源和再制作

| 文件 | 用途 |
| --- | --- |
| `art/blender/bedroom-target-v2/bedroom-gray.blend` | 首阶段灰模原生源 |
| `art/blender/bedroom-target-v2/bedroom-target-v2.blend` | 最终独立保存源，无人物与运行时 Cloth |
| `art/blender/bedroom-target-v2/revisions/` | 初始布料与游戏复核前版本，保留失败过程 |
| `assets/generated/bedroom-target-v2/bedroom-target-v2.glb` | 静态导出与独立纹理 |
| `assets/generated/bedroom-target-v2/meshes/*.res` | Godot xatlas UV2 网格；大文件使用 LFS |
| `assets/generated/bedroom-target-v2/bedroom.lmbake`、`bedroom.exr` | 同版本离线烘焙输出 |
| `assets/generated/bedroom-target-v2/bake-record.json` | 实际引擎版本、GLB／UV2 来源、产出时间与灯光文件哈希 |
| `scenes/testing/BedroomTargetVisual.tscn` | 保存的静态灯光／材质／LightmapGI 场景 |

新源存在时，`gray`／`init` 拒绝覆盖。后续在 Blender 修改并保存最终 `.blend`，只执行 `export`，禁止重跑初始化或修正阶段覆盖已编辑源。`refine`、`finish-review`、`repair-contact`、`repair-cover` 是本轮一次性的记录动作，由 marker 防止重复应用；`repair-graphics` 是纸页法线修复。

```powershell
$BlenderExe = 'D:/ProgramFiles/Blender/blender.exe'
$GodotConsole = 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe'
$env:DOTNET_ROOT = 'C:/Users/duwei/.dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& $BlenderExe --background --python-exit-code 1 --python scripts/tools/author_bedroom_target.py -- export
& $BlenderExe --background --python-exit-code 1 --python scripts/tools/author_bedroom_target.py -- check
& "$env:DOTNET_ROOT/dotnet.exe" build TownRpg.csproj
& $GodotConsole --headless --editor --path . --import
& $GodotConsole --headless --path . --scene scenes/testing/BedroomTargetPrepare.tscn
& scripts/tools/BakeBedroomTarget.ps1
& $GodotConsole --headless --editor --path . --import
& $GodotConsole --path . --scene scenes/testing/BedroomTargetProbe.tscn
```

每步检查退出码再继续。`Prepare` 按最终 GLB 精确 SHA256 判断 UV2 缓存可否复用；源有任何变化则重解包，首次复杂床品约数分钟。写成功场景后才保存缓存标记。UV2 解包会重排顶点，因此保存 full-detail 表面并移除旧导入 LOD 索引，不能把旧 LOD 数据套到新顶点顺序上。将导入 GLB 变成本地拥有节点时清除 `SceneFilePath`，防止同时保存导入实例和复制子节点；探针对照源 manifest 检查每个网格只有一个运行时副本。

## 灯光烘焙与运行边界

先保存灰模，验证窗光方向、漫射补光及 Compatibility 的真实灯光贴图加载，再开始精细资产。最后使用暖色实时太阳、窗外宽漫射静态光、四次间接光反弹、High 烘焙质量、室内探针、反射探针与 Filmic 色调映射。独立预览开启 Godot 4.6 已支持的 Compatibility SSAO，radius 0.48、intensity 1.05，补充细小接触层次；不改正式项目渲染配置。玻璃不投影，不参加 GI；门保持动态 GI。台灯不入烘焙，可实时切换。

依据 [Godot 4.6 LightmapGI 文档](https://docs.godotengine.org/en/4.6/tutorials/3d/global_illumination/using_lightmap_gi.html)，使用同一 Godot 4.6.3 的独立 `work/bedroom-target-v2-bake` 工作副本，离线 Forward+／Vulkan 烘焙，主项目仍为 Compatibility。工作副本仅复制独立环境静态资产和场景，不带角色、autoload 或用户存档；没有改主 `project.godot`，没有升级引擎或引入运行时生成服务。

4.6 的 `LightmapGI.bake()` 未绑定到脚本，工作副本插件调用编辑器已有 Bake Lightmaps 的保存动作，并检查非空 baked users。复制回 `.lmbake`、`.exr` **及 `.exr.import`**；后者必须保留 `2d_array_texture` 导入方式，不能按普通 2D 图片重新导入。编辑器烘焙退出诊断和实际游戏运行检查分开记录。

`BedroomTargetProbe` 保存 720p／1080p 五视角、台灯和精细图像近景，隐藏角色及帮助文字，截图无后期修绘。它实际驱动移动组件，验证走道、关门阻挡、开门出入、镜头回缩，并比较正式状态序列化及用户 JSON 文件 SHA256。性能只代表本机固定视角采样，不替代目标 GPU、连续人工游玩或负责人美术终验。
