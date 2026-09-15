# 树木与两间铺面 v3 样板验收

2026-09-14。`[已实现]` 三棵树与两间铺面的独立美术样板；正式村庄尚未切换。制作源、启动入口、来源与复验命令见[制作说明](../engineering/village-art-v3.md)。下列记录区分程序验证、画面复查与负责人验收。

## 范围与资产

从已保存 v2 派生 v3，只处理 PlazaTree、Tree_037、Tree_043、两铺外壳、雨棚及对应木器材质，新增三组薄型构造。24 栋建筑的原变换、其余树木与道具位置、道路、碰撞、住宅和 Terrain3D 数据保留。独立样板不读写正式存档，不包含第一章委托或森林。

[资产清单](../../assets/generated/village/3d/village-v3-manifest.json)保存源／GLB／基线哈希、替换白名单与保留对象指纹。[来源清单](../../art/sources/cc0/village-art-v3/provenance.json)保存 CC0 原图 URL、尺寸、SHA-256、许可快照、复用原材质与后处理配方。本轮没有图像生成。

| 预算项目 | 本轮实际／保守估算 | 上限 |
| --- | --- | --- |
| GLB 三角面 | 727,333；比 v2 净增 8,762 | 净增 35,000 |
| 含原生地形与草石的保守总三角面 | 1,162,444 | 1,200,000 |
| GLB 网格对象／计入地形后的网格估算 | 250／375 | 600 |
| GLB 材质／含地形及草石 | 50／53 | 64 |
| GLB 文件 | 64.97 MiB；增加 20.09 MiB | 100 MiB |
| GLB 内贴图估算／含 Terrain3D | 268.00／361.34 MiB | 384 MiB |

三角面计算为 GLB 727,333 减去隐藏的旧 Terrain 89,856，加四区域完整采样网格保守上界 524,288 与草石 679；没有降低原地形质量。纹理按实际 GLB 内 24 张图像、RGBA 与完整 mip 链保守计算，再加现有地形 93.34 MiB。详见[资源预算数据](../../outputs/village-art-v3/resource-budget.json)。运行时监控包含角色、缓存住宅和其他引擎资源，与环境资产预算口径不同。

原变换下的树木网格尺寸（宽／高／深，未追加旋转 AABB）：广场树约 7.63／9.88／7.75 米，两棵普通树约 4.28／6.41／4.38 米。它们保留原落点和原碰撞；广场根颈在树池内，冠层改为有间隙的分枝。各树冠的疏密与尺度感觉仍由负责人确认。

## 实际检查

| 项目 | 实际结果与日志 |
| --- | --- |
| Core／内容／表现层 | 184 通过，0 失败、0 跳过；182 基线上增加 2 项实际 GLB 范围、变换、共享网格、材质隔离和预算契约。[日志](../../outputs/village-art-v3/tests-final.log) |
| Godot Debug 构建 | 0 警告、0 错误。[日志](../../outputs/village-art-v3/build-final.log) |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`，退出码 0。[日志](../../outputs/village-art-v3/smoke.log) |
| Blender 只读复导 | `VILLAGE_V3_CHECK_PASS readOnly=true 233`，退出码 0；源外链、UV、法线、共享树网格、实际 GLB 与临时复导一致，233 个非目标网格 payload 保持。[日志](../../outputs/village-art-v3/asset-check-final.log) |
| 完整样板漫游 | 正常时间倍率下 808 项检查、25 条路线、33 图，退出码 0。包含三条主路线、净空、桥头、棚下镜头、门灯与住宅往返、暂停／失焦和 billboard 回退，正式存档未被改写。[报告](../../outputs/village-art-v3/20260914-204731-sample/results.json)、[日志](../../outputs/village-art-v3/sample-realtime.log) |

完整路线之后仅平滑树干相邻面的法线，没有改变 Blender 源的顶点位置、面结构、材质绑定、碰撞或输入规则；再次导出后重跑资产契约和只读复导，并重新拍摄画面。上述路线报告保留其实际执行顺序，不冒充平滑法线之后再次走完全村。

## 正式隔离、图像和短片

- 正式醒后与存档专项 **105 项通过、退出码 0**：包含三分辨率醒来／卧室／门口／村庄／暂停共 15 图，门灯、住宅往返、旧完成档、ExitOpened 收尾、Legacy、落点恢复和正式存档保护。新断言验证带 `--village-art` 也不会把正式场景切到 v3。[日志](../../outputs/village-art-v3/formal.log)、[隔离输出](../../outputs/village-art-v3/formal/20260914-210628)。这不是完整序章输入通关。
- 最终法线修正后，v3 与默认 v2 各 **33 张三分辨率同机位图**，两次均退出码 0；检验样板三棵树、双面裁切、共用网格及基线不含 v3 装饰。[样板图](../../outputs/village-art-v3/20260914-210100-sample)、[v2 图](../../outputs/village-art-v3/20260914-210217-baseline)、[样板日志](../../outputs/village-art-v3/views-final.log)、[基线日志](../../outputs/village-art-v3/baseline-views.log)。
- [汇总预览](../../outputs/village-art-v3/village-art-preview.jpg)；三档分辨率各有广场树、杂货铺、修理铺三张前后对照，共 9 张。1080p 入口：[树木](../../outputs/village-art-v3/compare-tree-profile-1920.jpg)、[杂货铺](../../outputs/village-art-v3/compare-shop-side-1920.jpg)、[修理铺](../../outputs/village-art-v3/compare-workshop-side-1920.jpg)。
- [720p 接触表](../../outputs/village-art-v3/contact-1280.jpg)、[900p](../../outputs/village-art-v3/contact-1600.jpg)、[1080p](../../outputs/village-art-v3/contact-1920.jpg)包含正常角色镜头、树下仰视、两棵周边树、铺面侧面、棚下和工作台。抽查三个分辨率的源图，树池和树脚未见新增悬浮、通路和门口未被新装饰占用，侧墙砖纹与树干明暗修正生效。
- **12 段短片**：三处正常移动×三分辨率共 9 段，另加完整树冠环视×三分辨率 3 段。每段 120 帧、30 FPS、4 秒；编码后逐帧解码确认帧数与尺寸。普通移动有完整[列表](../../outputs/village-art-v3/20260914-210410-sample/encoded-videos.json)，另有[树冠列表](../../outputs/village-art-v3/20260914-210950-sample/encoded-videos.json)。1080p：[广场行走](../../outputs/village-art-v3/20260914-210410-sample/plaza-1920x1080.mp4)、[杂货铺](../../outputs/village-art-v3/20260914-210410-sample/shop-1920x1080.mp4)、[修理铺](../../outputs/village-art-v3/20260914-210410-sample/workshop-1920x1080.mp4)、[树冠环视](../../outputs/village-art-v3/20260914-210950-sample/plaza-1920x1080.mp4)。

帧数校验和[移动抽帧](../../outputs/village-art-v3/movement-review.jpg)不等于人工完整播放确认；叶片远处硬边、轻微闪动、细枝交叠的自然度和贴图重复感继续列入负责人视觉验收。已有硬质道路边缘、其他建筑与球团树冠未在本批调整。

## 同机性能记录

RTX 2080 Ti、Godot 4.6.3 Compatibility／OpenGL 3.3、上午光照。最终两次观测都使用 `--fixed-fps 30` 统一模拟步长，计时读取真实墙钟的 180 个渲染帧；均包含帧同步。每个分辨率在同一广场位置和相同镜头转动序列采样，先后独立运行，没有同时跑另一份图形测试。原始数据见[对照 JSON](../../outputs/village-art-v3/performance-comparison.json)。

| 分辨率 | v2 平均／P95 ms | v3 平均／P95 ms | 引擎显存估算 MiB：v2／v3 | 进程工作集 MiB：v2／v3 |
| --- | --- | --- | --- | --- |
| 1280×720 | 16.695／16.767 | 16.679／16.777 | 305.1／383.1 | 582.9／592.4 |
| 1600×900 | 16.664／16.808 | 16.648／16.800 | 325.0／403.0 | 608.5／604.7 |
| 1920×1080 | 16.650／16.781 | 16.597／16.903 | 349.9／427.9 | 707.5／711.6 |

相同观测角度绘制调用为 v2 **1506**、v3 **1522**，包括地形与阴影等多个绘制过程，不能与 600 个网格对象预算混用。引擎纹理占用增加约 **77.33 MiB**；表中显存指标还包括顶点及其他引擎分配，是 Godot 估算，不等于驱动整卡占用。系统 `nvidia-smi` 另记录活动样板约 602 MiB、活动 v2 约 567 MiB，包含其他进程且不是同一帧，不能拿它们相减作为资产成本。旧 `gpu-baseline.csv` 的 52 MiB 采样发生在退出后，不用于运行对比。

最终两次启动观测：v3 **8403.9 ms**、v2 **3484.9 ms**。最终资源重导后的首次样板加载 **25889.4 ms**，随后另次启动 **11255.8 ms**；全部保留，没有选取最小值冒充稳定冷启动。加载比 v2 慢且有波动；本轮未定位为单一原因，文件缓存、纹理解码和首次材质准备均可能影响结果。**正式采用前应复测并优化加载过程**。常态帧时间包含帧同步，不能由本组结果声称透明枝叶零成本、目标硬件认证或纯 GPU 性能通过。

## 复现命令

```powershell
# 样板三条路线、工作净空、桥头与住宅往返
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --village-art --village-capture
# 三分辨率图像；基线改为 --village-art-baseline
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --village-art --art-views
# 正常移动短片；仅树冠环视另加 --art-leaf-video
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --fixed-fps 30 --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --village-art --art-views --art-video
# 正式回归：测试参数只选输出目录，不改变正式场景版本
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageWakeProbe.tscn -- --prologue-e2e --village-art
```

本轮新日志和时间戳目录均在 `outputs/village-art-v3/`，历史验收不覆写。开工工作副本对比、保护区文件哈希与文档导航检查见[增量](../../outputs/village-art-v3/increment.json)、[本地链接](../../outputs/village-art-v3/links.json)；`git diff --check` 通过，仅保留既有换行转换提示。AGENTS 直接进入本样板，分类文档至多两次点击可达。

## 修正与已知提示

- 首轮近景发现枝叶背光偏暗、侧墙砖纹方向不对：调整枝叶色阶、低强度背光和 UV；没有改变全局光照。随后平滑树干相邻角点法线，改善分段明暗。
- 首次沿用三倍测试时间倍率，溪岸路线在 `(73.52, -22.60)` 附近未到达下一路点，退出码 1；[原始失败日志](../../outputs/village-art-v3/sample-final.log)保留。改为本批正常倍率进行实际输入走查后通过；没有通过移动建筑或修改碰撞规避失败。加速测试与正常输入的差异可能放大偶发帧停顿，未将原因认定为已修复的游戏碰撞缺陷。
- Godot headless 编辑器导入完成后报告退出时 CanvasItem／ObjectDB／资源仍在使用；该导入日志不标为无错误的游戏验证。[导入记录](../../outputs/village-art-v3/import-smooth.log)。运行专项的退出结果另外检查。
- Terrain3D 保留上游 `instance_reset_physics_interpolation()` 弃用警告。Blender 保留多图像节点共用纹理时采用首个 sampler 的提示；现有采样方式一致，复导检查通过。未修改上游或屏蔽日志。

## 负责人验收与后续

`[计划]` 负责人视觉与操作确认尚未完成：重点确认冠层疏密、树干自然度、叶片在移动中的边缘表现、铺面材料色彩及正常镜头。自动路线与截图复查不能代替该确认；真实 Xbox／raw delta、目标硬件性能也未认证。

独立入口复用现有 Terrain3D，但这不是新的全村地形精修或正式美术切换。确认后再安排正式采用及其余树木、房屋推广。本轮不重复完整序章输入通关，不提交、推送或导出游戏；本机角色及其画面、视频继续仅限本机非商业。
