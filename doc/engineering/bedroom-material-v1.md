# 卧室书桌／衣柜材质对照 v1

2026-10-06。`[已实现]` 从保存的卧室 v2 派生独立环境源，仅分离并精修书桌、衣柜的木质表面。新旧材质共用同一网格、UV2、烘焙间接光与固定反射捕获。原版完整卧室、三场景样板和正式住宅保留；[实际验收](../production/bedroom-material-v1-validation.md)与负责人美术批准分开。

## 入口和操作

执行 `scripts/tools/PreviewBedroomMaterial.ps1`，或运行 `scenes/testing/BedroomMaterialReview.tscn`。

| 操作 | 效果 |
| --- | --- |
| V | 新／旧家具材质；默认新材质 |
| N | 中性白光材质检查／返回完整卧室 |
| 1–5 | 原有门口、床边、桌边、窗边与反向 |
| 6／7 | 桌面斜视／柜门近景 |
| Tab | 原生走动；进入走动自动退出中性光检查 |
| F／L | 门／实时台灯 |
| R | 回到新材质、卧室、初始门灯及位置 |
| Esc | 沿用走动暂停及鼠标释放 |

截图隐藏人物和帮助文字，不进行后期修绘。中性检查单独显示两件木家具，不表示替换卧室白天照明。

## 美术与源文件

- 保存源：`art/blender/bedroom-material-v1/bedroom-material-v1.blend`。房间结构、顶点位置、板厚、倒角、平滑／加权角点法线及总三角面保留。
- 书桌／衣柜由原 `Furniture_Wood`、`Furniture_WoodEdge`、`Furniture_WoodDark` 的连通木板按空间位置分离，加入 `Furniture_Desk`／`Furniture_Wardrobe`。源共 45 网格，保存源拓扑折算 488,922 三角面、GLB 导出 454,362 三角面；两项均与原版相同，原版为 43 网格。原有床架、椅子、门及其他物件的几何和材质保留。
- `textures/` 保存每件家具的 2K 颜色、GL 切线法线与 ORM，并保存旧颜色、旧粗糙度和旧 ORM。`inputs/` 保存加工后的 CC0 橡木原料及 UV 棋盘格；`provenance.json` 保存基础源哈希、木板包围盒、图集排布与实际密度。
- 使用已取得的 Poly Haven `oak_veneer_01`，1.8 米物理尺寸，CC0 来源仍由原 `source-manifest.json` 管理。Pillow／NumPy 将 sRGB 颜色对比降为约一半、均值调为暖棕色；粗糙度从独立原 ARM 的 G 通道加工，书桌约 .45、衣柜约 .55，幅度不超过 ±.07。原 GL 法线在 Blender 强度 .15 下烘焙；不是由颜色灰度伪造全部通道。
- UV 图集采用确定性 MaxRects。书桌主要表面 512 px/m，背／底面 256；衣柜主要正面和大侧板 448、其余 224，在约 512／256 的目标附近降低 12.5% 以容纳 2K 图集。岛间扩展 16 像素。图集可旋转岛，独立 `WoodSurface` UV 始终沿木板长轴；旋转不改变家具上的纹理方向。
- Blender 的 `OriginalUV` 保留原纹理位置，旧颜色仅烘焙 Diffuse Color，关闭直接／间接光；旧粗糙度独立烘焙。新旧均在相同图集 UV 上运行。源保留辅助 UV，GLB 导出移除辅助 UV，Godot UV2 专用于 GI。
- ORM：R=255（不新增 AO），G=粗糙度，B=0（非金属）；颜色 sRGB，法线／ORM Non-Color。桌面和柜门采用克制薄漆反光，不画入固定光影、污渍、破损、文字或剧情线索。本轮没有新 AI 图像、购买或插件。

`revisions/before-desktop.blend` 保留首轮漏选桌面的版本，`before-repack.blend` 保留更新图集尚未重新打包的版本。实际游戏截图发现桌面未进入木家具组后，编辑**保存的新源**补入桌面并重做书桌图集；没有重跑旧卧室初始化或从旧源覆盖精修源。后续截图发现 Blender 沿用旧 packed image 导致图集与新 UV 错位，重新打包保存源的 PNG 后修复；源检查核对 packed bytes，资产审计逐像素核对源 PNG 与导出图集。

## 继续编辑与导出

`[固定约束]` 后续修改保存的 `.blend`，只执行 `export`。`derive` 是初次派生入口，已有保存源时拒绝覆盖；`edit-desk`／`refresh-packed` 是有备份／防重跑保护的本轮生产修正记录，不能用于日后重建。不要重跑 v2 的 `init`、`refine` 或任何一次性修正。

```powershell
$BlenderExe = 'D:/ProgramFiles/Blender/blender.exe'
$GodotConsole = 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe'
$DotnetExe = 'C:/Users/duwei/.dotnet/dotnet.exe'
$PythonExe = 'C:/Python314/python.exe'
& $BlenderExe --factory-startup --background --python-exit-code 1 --python scripts/tools/author_bedroom_material.py -- export
& $BlenderExe --factory-startup --background --python-exit-code 1 --python scripts/tools/author_bedroom_material.py -- check
& $DotnetExe build TownRpg.csproj --no-restore
& $GodotConsole --headless --editor --path . --import
& $PythonExe scripts/tools/configure_bedroom_material_imports.py
& $GodotConsole --headless --editor --path . --import
& $GodotConsole --headless --path . --scene scenes/testing/BedroomTargetPrepare.tscn -- --bedroom-material
& scripts/tools/BakeBedroomTarget.ps1 -Material
& $GodotConsole --headless --editor --path . --import
& $PythonExe scripts/tools/audit_bedroom_material.py outputs/bedroom-material-asset-audit.json
& $GodotConsole --headless --path . --script scripts/tools/audit_bedroom_material_geometry.gd
& $GodotConsole --path . --scene scenes/testing/BedroomTargetProbe.tscn -- --bedroom-material
```

每步检查退出码再继续。Python 工具使用 Pillow／NumPy，Blender 烘焙使用随附 NumPy／Cycles；不需下载新软件或素材。`prepare_bedroom_wood.py` 只重建加工原料，不修改已保存模型或最终图集，不能替代在 Blender 中编辑、烘焙、保存。

## 游戏与照明边界

`BedroomTargetPrepare` 新增显式材质样板参数；没有参数时仍写原 v2 路径。`BedroomTargetReview` 的视觉资源选择及虚方法仅供审图继承；原入口默认不变。新入口复用碰撞、移动、镜头、门灯，没有游戏公共 API、Core、剧情、SaveEnvelope v1、内容版本 6 或正式路由变化。

烘焙使用 Godot 4.6.3 Mono 的 `work/bedroom-material-v1-bake` 工作副本，Forward+／Vulkan 离线烘焙；主项目保持 Compatibility。窗光、天空、补光、曝光、色调映射、GI 参数及反射探针沿用原版。新网格、30 个烘焙用户、`.lmbake`、EXR 及 `2d_array_texture` 导入设置一同更新，来源由 `bake-record.json` 与 GLB 哈希关联。

**A/B 共用新材质烘焙后的间接光，不能把 A 档称为原版完整光照。** 原版完整 v2 入口保留做回归；材质切换不重新捕获反射。N 模式另用中性环境和白色侧光，不修改保存的卧室灯光。

十张 A/B 图集采用 Compatibility 支持的 DXT1／RGTC_RG、mipmaps 与各向异性过滤；高质量压缩关闭，不依赖 BC7。独立 UV 棋盘格仅由探针加载。纹理预算、整帧绘制调用、引擎内存统计、墙钟帧间隔及目标硬件验收分别记录。

`[固定约束]` 不推广到床品、地砖、墙面、全村，不正式采用、不推送、不导出游戏。源没有人物及运行时 Cloth；漫游仍可沿用本机角色，含人物媒体继续仅本机。允许符合既有授权的本机 Git 提交，不将技术通过写成负责人美术认可。
