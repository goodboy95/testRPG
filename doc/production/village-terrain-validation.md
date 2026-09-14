# Terrain3D 三处地形样板验收记录

> 后续状态：负责人已确认外观并授权正式采用，当前正式村庄与默认 v2 入口已切换；见[正式采用验收](village-terrain-adoption.md)。下文保持样板制作轮的实际检查和当时边界，不作为正式采用轮的检查结果。

> 2026-09-14，本轮实际结果。`[已实现]` 三处样板可在独立入口走查，正式村庄未切换。制作与启动见[说明](../engineering/village-terrain-sample.md)；本记录不代表负责人视觉验收。

## 已完成的验证

| 项目 | 本轮结果 | 记录 |
| --- | --- | --- |
| Core／内容／表现层规则 | 182 通过，0 失败，0 跳过；赶路基线 180＋2 项高度迁入／保护区及资产来源检查 | [最终测试日志](../../outputs/village-terrain/tests-final.log) |
| Godot Debug 构建 | 0 警告、0 错误 | [构建](../../outputs/village-terrain/build-final.log) |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31` | [日志](../../outputs/village-terrain/smoke.log) |
| 样板完整引擎专项 | 398 项通过，25 条路线记录，18 张三分辨率截图；退出码 0 | [日志](../../outputs/village-terrain/sample-probe.log)、[结果](../../outputs/village-terrain/20260914-175211-sample/results.json) |
| 原村庄 v2 对照 | 375 项通过，25 条路线记录，18 张同机位截图；退出码 0 | [日志](../../outputs/village-terrain/baseline-probe.log)、[结果](../../outputs/village-terrain/20260914-175604-baseline/results.json) |
| 正式住宅／村庄回归 | `VILLAGE_WAKE_PASS checks=83`，保留正式存档隔离、门灯与室内外恢复、Legacy 和旧梦境迁入检查 | [日志](../../outputs/village-terrain/wake-probe.log) |
| 原生数据与浮点备份 | 262,144 点一致，保护点误差 0；EXR 浮点导出、重载和四区域重开通过 | [日志](../../outputs/village-terrain/native-check-final.log)、[报告](../../outputs/village-terrain/native-check/report.json) |
| 编辑场景 | 原村庄参照的旧地面隐藏，新原生地形引用有效 | [实际加载检查](../../outputs/village-terrain/authoring-check-final.log) |
| 视频 | 3 地点×3 分辨率＝9 段，每段 120 帧、30 FPS、4 秒；全部逐帧解码并核对尺寸 | [录像清单](../../outputs/village-terrain/20260914-180104-sample/encoded-videos.json)、[引擎日志](../../outputs/village-terrain/video-final.log) |

引擎专项覆盖 WASD／慢走／暂停／失焦、原三条村庄回环、两桥双向与偏移通行、第一批工作净空和物件碰撞、住宅门灯往返、重复转场、重置、缺失角色 billboard。独立样板检查正式会话和存档未改变。

新增样板检查覆盖当前地表提供者、高度查询、旧地面碰撞不重复存在、六处实际地面射线、隔离位置快照恢复、高度不匹配／越界／建筑内落点退回安全点，以及缓存住宅时停用外景碰撞、回村后恢复。20,155 个引擎高度采样点与输入一致；六处射线最大高度差为 0.000834 米。详见[空间报告](../../outputs/village-terrain/20260914-175211-sample/terrain-contract.json)。

未重复完整序章输入通关，没有运行 Blender 重建、模型导出或游戏导出。本轮未单独重跑显式 v1 图形路线；v1 资源与入口保留，原资产测试仍覆盖 v1，实际路线对照使用当前 v2。

## 图形与资源结果

- [三处样板总览](../../outputs/village-terrain/terrain-sample-preview.jpg)
- [带尺寸的样板范围图](../../outputs/village-terrain/terrain-sample-plan.png)
- [家门前对照](../../outputs/village-terrain/compare-home-1280.jpg)、[广场对照](../../outputs/village-terrain/compare-plaza-1280.jpg)、[溪岸对照](../../outputs/village-terrain/compare-bank-1280.jpg)
- [720p 动作抽帧](../../outputs/village-terrain/20260914-180104-sample/motion-contact-1280.jpg)、[900p](../../outputs/village-terrain/20260914-180104-sample/motion-contact-1600.jpg)、[1080p](../../outputs/village-terrain/20260914-180104-sample/motion-contact-1920.jpg)
- [家门前录像](../../outputs/village-terrain/20260914-180104-sample/home-1280x720.mp4)、[广场录像](../../outputs/village-terrain/20260914-180104-sample/plaza-1280x720.mp4)、[溪岸录像](../../outputs/village-terrain/20260914-180104-sample/bank-1280x720.mp4)

代理检查了正常视角、细节图及动作抽帧：地表纹理、路肩与稀疏草石可见；原有建筑粗模、硬质道路几何边缘、规则岸墙与平水面保留。溪岸精修部分受原岸墙遮挡，近景记录显示墙内外衔接，后续若改岸墙／水面必须另行确定范围。不能把地表样板描述为全村美术终稿。

| 预算项目 | 本轮记录 |
| --- | --- |
| 地面全分辨率保守三角面 | 原资产 718,571－隐藏原地面 89,856＋四区域全分辨率 524,288＋草石 679＝1,153,682，低于 120 万；实际可见 Terrain3D 采用 LOD |
| 场景网格实例上界 | 原可见网格 246＋Terrain3D 6 层最多 124 块＋2 组草石＝372，低于 600；不把阴影多次绘制当作新网格 |
| 材质 | 保守按原 41＋地形 1＋草石 2＝44，低于 64；地形内部四套纹理不是四个重复场景材质 |
| 原 GLB | 47,062,464 字节，哈希与开工前一致 |
| 新区域数据 | 四份 `.res` 共 281,852 字节 |
| 新运行 PNG | 23,494,537 字节；四套 1K 打包纹理，含 mipmap 的单份估算 42.67 MiB |
| 纹理保守估算 | 原 172 MiB＋原始纹理及纹理数组双份 85.33 MiB＋区域图及辅助预留 8 MiB＝约 265.33 MiB，低于 384 MiB；本机角色和缓存住宅另列在全进程监测中 |

Terrain3D 网格上界按固定 1.0.2 的 mesher 生成规则核对：LOD0 为 16 tile＋4 edge＋4 trim，后五层各 12 tile＋4 edge＋4 fill，共 124 块。这里只计单次场景几何；含阴影、UI 和角色的渲染器 primitive/draw-call 总数另存性能报告，不与资产三角面混用。

同机 RTX 2080 Ti、Compatibility/OpenGL，保留夏日上午光照与当前相机；未使用固定步长录像的数据作为性能结果。

| 分辨率 | 原 v2 平均／P95，毫秒 | 样板平均／P95，毫秒 |
| --- | --- | --- |
| 1280×720 | 16.6967／16.795 | 16.6971／16.791 |
| 1600×900 | 16.6978／16.780 | 16.6969／16.788 |
| 1920×1080 | 16.6978／16.800 | 16.6967／16.783 |

加载记录为原 v2 1,454.5 毫秒、样板 4,059.9 毫秒。样板引擎纹理显存读数比对照高约 90.79 MiB；全进程包含本机角色和已缓存住宅，不能直接当作村庄独立纹理预算。帧时间包含 VSync／帧调度，不能据此宣称纯 GPU 性能提升、目标硬件 60 FPS 或冷启动已验收。原始数据见[汇总](../../outputs/village-terrain/summary.json)。

## 中间失败、兼容提示与限制

以下均保留原日志，不把失败轮冒充成功：

1. 初次发行包下载省略了文件名中的 `-stable`，返回 404；核对官方附件后使用正确的固定版本包。Python 下载材质返回 403，PowerShell 从同一官方地址成功取得原始素材。
2. 初始原生重开测试对无路径临时材质启用了 `free_editor_textures`，产生空资源加载错误；样板固定关闭该可选释放，后续重开与缓存往返通过。
3. [首轮视图](../../outputs/village-terrain/first-views.log)虽然输出完成标记，但快速测试往返在住宅反射捕获完成前移出了住宅，产生 `scenario is null`。专项增加了住宅渲染稳定帧；完整样板／v2 对照及正式回归日志无此错误。该失败不记为干净通过，也不声称修复了 Godot 的反射捕获实现。
4. 初次原生备份检查错误地要求 EXR 重载为单通道 RF；Godot 展开为 RGBF，数据仍为浮点。检查改为验证浮点格式、尺寸及通道数据，最终全量／抽样往返通过。
5. 初次固定 30 FPS 录像在回村后仅等六个物理帧便判定落地，检查失败；测试补齐焦点恢复并在最多 30 帧内等待真实落地，最终录像专项通过。没有放宽高度／碰撞误差或修改移动规则。
6. 编辑参照场景最初漏掉 GLB 导入根 `VillageV1`，原地面隐藏路径无效；已改为实际实例路径，重新加载检查通过。
7. Terrain3D 原生二进制在当前引擎首次建立网格时发出 `instance_reset_physics_interpolation()` 弃用警告，最终样板和原生检查仍保留该警告。插件启用后的快速 headless 编辑器导入退出曾报告一项 CanvasItem／纹理资源清理提示，见[编辑器导入日志](../../outputs/village-terrain/authoring-import.log)；它不属于运行时检查的零错误声明。未修改上游二进制或更换引擎。

`[计划]` 负责人视觉、自然度与手感终验仍待实际游玩；真实 Xbox/raw delta、GTX 1660／目标硬件、正式地形接入和游戏导出均未验收。本机角色及含角色图像／录像继续仅限本机非商业，不提交、推送或分发。

## 复现与增量审查

```powershell
# 样板路线、碰撞、恢复、住宅往返与三分辨率记录。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --terrain-check --village-capture
# 同机原 v2 对照：将 --terrain-check 换为 --terrain-baseline。
# 固定时间步录像：加入 --fixed-fps 30，用户参数改为 --terrain-check --terrain-views --terrain-video。
```

图形测试串行运行；纹理、数据导入和编译在图形运行之外进行。只读输入与原生复验命令见[制作说明](../engineering/village-terrain-sample.md)。旧预览的输出目录与历史报告不被本轮覆盖。

开工前哈希记录为本机临时目录 `village-terrain-start.json`，用于区别本轮与已有未提交工作。增量、受保护文件与文档链接检查见[增量报告](../../outputs/village-terrain/increment.json)、[链接报告](../../outputs/village-terrain/links.json)。新增 LFS 规则只声明 native 插件与地形源数据管理方式，不代表已提交、推送或实际上传。

本轮审查结果：12 个已有文件改变、254 个新增、0 删除；受保护的 Core、剧情数据、本地化、原 Blend／GLB、基础村庄布局、移动控制器和 GameSession 无本轮修改。597 个本地链接有效，54 份被检查的文档均可从 AGENTS 在两次点击内到达；`git diff --check` 通过，只有既有换行转换提示。没有提交、推送或游戏导出。
