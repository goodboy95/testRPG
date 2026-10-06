# 书桌／衣柜木材贴图 v1 实际验收

2026-10-06，Windows / Godot 4.6.3 Mono / Compatibility / RTX 2080 Ti（驱动 591.86）。`[已实现]` 独立材质样板、保存源、图集、UV2 与离线 GI 已交付；技术检查通过。**负责人美术认可及 GTX 1060 1080p／60 FPS 验收待进行，正式住宅未采用。** [工程与继续编辑](../engineering/bedroom-material-v1.md)。

## 证据及检查结果

- 最终样板：`outputs/bedroom-material-v1/20261006-205658-final/`，39 张实际渲染 PNG 与 `results.json`，专项 109 项通过。720p／1080p 下七视角各有 A/B，中性光 A/B、UV 棋盘格、720p 台灯 A/B 和原有小物件近景。截图隐藏人物及帮助文字，无后期修绘。
- 原版完整卧室回归：`outputs/bedroom-target-v2/20261006-204915-final/`，43 项通过；它使用原版资源及原版 GI，不与材质 A 档混淆。
- .NET 构建：0 警告、0 错误，`outputs/bedroom-material-build.log`。既有 Core／内容 313 项全部通过，`outputs/bedroom-material-core-tests.log`；没有新增无关 Core 测试。
- 项目 Godot 冒烟：9 场景通过，角色动作、16 次房间往返、序章与 UI 专项通过，`outputs/bedroom-material-smoke.log`。
- 保存源重开、两件家具归属、辅助 UV、无角色／运行时 Cloth、packed image 与源 PNG 字节一致：`outputs/bedroom-material-source-check.log`。
- 原版源哈希不变、世界顶点位置双向核对、完整桌面归属、通道／图集像素、压缩配置、GLB／UV2／烘焙哈希及 EXR array 导入一致：`outputs/bedroom-material-asset-audit.json`。最大世界顶点误差 `1.1920928955078125e-7` 米，为导出浮点差。
- Python 与 PowerShell 语法检查、`git diff --check` 通过。

专项实际使用原生移动组件到达床侧、桌前、柜前和门外十处落点；关门阻挡、开门通行、镜头贴墙收缩、门灯状态、V 对照及 R 恢复通过。正式 Session 序列化前后相同，正式 JSON 存档数量与 SHA256 前后相同。

两件家具均加载 2048×2048 颜色、法线和 ORM，运行时法线启用、切线与 UV2 存在，粗糙度取 G 通道，金属通道 B=0、AO R=255；mipmaps 与各向异性过滤启用。新旧材质使用相同网格、30 用户的 GI 数据及固定反射捕获，只切换材质。颜色未加入窗光、高光或投影，N 模式另用中性白光。

## 美术复核与失败修正

`[已实现]` 代理检查最终门口、桌面斜视、柜门、720p 近景、中性光、棋盘格及台灯图：桌面纹理沿长边、柜门纵向、抽屉横向；左右柜门取样不同。原版简单条纹变为疏密变化的木纹，封边仍能区分，没有首轮的黑块、明显拉伸、串色或压缩色块。法线保持低强度，正常视距没有粗重凹凸条纹。

薄漆反光及实际走动的自然度仍需负责人亲自比较。台灯开启有实时亮度与反射变化，但本轮没有调整台灯能量或整个卧室的偏灰照明。床品形状、地砖、窗外树叶和其他模型的既有问题未解决，不能把本次木纹改善写成整间卧室达到参考图。

保留两次实际失败证据及源修订：

1. `20261006-202449-final` 数值检查通过，但桌面未进入新家具组。补入保存的新源并重新排书桌图集，`revisions/before-desktop.blend` 保留修正前源。
2. `20261006-204214-final` 仍出现黑块：Blender 导出沿用了旧 packed image，与新 UV 错位。重新打包保存源的最新 PNG，保留 `revisions/before-repack.blend`；增加源打包字节及导出像素一致性检查，最终图复核消除该问题。

这两批图片不作为最终美术交付。没有重跑旧初始化、覆盖原卧室源或后期涂掉黑块。

## 几何与本机性能

同机、同分辨率、同门口视角，VSync 关闭。帧间隔采样来自引擎 ProcessFrame 墙钟；不是独立 GPU 时间，也不能直接推算目标硬件 FPS。原版与样板分别运行，无并发 Godot 渲染实例。

| 统计口径 | 原版完整 v2 | 木材样板 v1 |
| --- | ---: | ---: |
| 保存源拓扑折算三角面 | 488,922 | 488,922 |
| GLB 导出三角面 | 454,362 | 454,362 |
| GLB 导出顶点 | 386,247 | 386,247 |
| Godot 实际模型三角面 | 454,028 | 454,028 |
| Godot UV2 后顶点 | 410,415 | 410,452（+37） |
| 模型网格对象 | 43 | 45 |
| 1080p 门口整帧 draw calls | 199 | 207（+4.02%） |
| 整帧 primitives（含阴影等重绘） | 2,244,754 | 2,239,302 |
| 门口墙钟帧间隔 median／p95 | 1.441／2.627 ms | 1.432／2.600 ms |
| 引擎 VideoMemUsed | 462,895,958 B | 497,455,114 B |
| 引擎 TextureMemUsed | 430,269,978 B | 466,970,282 B |
| 连续走动采样数 | 3,451 | 3,477 |
| 走动帧间隔 median／p95 | 1.466／2.586 ms | 1.465／2.580 ms |
| 走动纹理／显存峰值 | 430,269,978／462,895,958 B | 466,970,282／497,455,114 B |
| 走动 draw call 峰值 | 522 | 531 |

几何工具复核输出 `outputs/bedroom-material-runtime-geometry.json`；Godot 导入剔除的退化三角面与源／GLB 统计单独记录，未新增几何。UV2 后仅增加 37 个接缝顶点。

十张 A/B 图集含全部 mip 层的压缩数据合计 **33,554,592 B，约 32 MiB**，低于 64 MiB 检查线，不含灯光贴图。颜色／ORM 使用 DXT1、法线 RGTC_RG；没有使用 BC7 高质量选项。独立棋盘格额外计入整进程峰值；TextureMemUsed 增量约 35 MiB，包含图集、诊断资源与导入资源差异，不等于十张图集预算。EXR 灯光贴图保持既有 HDR texture array 导入。RTX 2080 Ti 数据不能替代 GTX 1060 实测。

## 烘焙与日志限制

`bake-record.json` 记录 Godot `4.6.3.stable.mono.official.7d41c59c4`，工作副本 Forward+／Vulkan，主项目 Compatibility。最终烘焙用时 9.41 秒、30 个静态用户；GLB 哈希 `51F22271597B27E29758B4127EEE2884EFA465F6A688AC651BF22397364EA63A` 与 UV2 来源一致，EXR／lmbake 哈希核对通过。

`outputs/bedroom-material-final-bake.log` 仍有既有编辑器 `List.erase`、退出时场景树绝对路径查询及 7 个 Texture RID 释放诊断；headless 导入退出仍有 DummyTexture／CanvasItem／ObjectDB 释放诊断。**不宣称全部日志零错误。** 烘焙资源保存、来源、重新导入和实际 Compatibility 运行通过；最终 109 项运行探针及原版回归日志没有 ERROR／WARNING。

`[固定约束]` 技术通过与美术认可分开。原版卧室、三场景样板、正式住宅、Core、剧情、SaveEnvelope v1 和内容版本 6 保持；不推送、不导出游戏。角色及含角色媒体继续仅本机，最终静态截图没有人物。后续推广需另定范围。
