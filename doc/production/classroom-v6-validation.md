# 第一室 v6 验收记录

日期：2026-09-09。`[已实现]` 本轮完成环境建模、布局接入、自动化回归及实际 Godot 图形捕获。`[计划]` 完整人工游玩、负责人视觉终验和目标硬件验证仍待进行。

## 交付与布局

当前权威源为 `art/blender/dream_classroom/classroom-art-pass-v6.blend`，运行时资产和清单为 `assets/generated/dream_escape/3d/classroom-art-pass-v6.glb`、`classroom-art-pass-v6-manifest.json`。来源记录为 `assets/generated/provenance.json` 中的 `dream_classroom_cinematic_runtime_v6`。v5 源保留，制作脚本在运行前后校验其哈希不变。

房间维持 12×9×4 米，东门、西侧三窗、出生点及 11 个交互目标不变。十二组桌椅按四列三排布置，包含椅子的碰撞外缘之间，中央净宽 3.68 米、侧面 1.74 米、后排 1.24 米。维护卡和值日便签贴合左右后排桌面，作息表有独立资料板；纸面线索与机关至少分离 2 米。

家具、门窗五金、时钟、资料板、窗帘、墙裙收口及天花设备在 v6 中制作。桌阵参数集中于 `ClassroomLayout3D`；模型、碰撞和安全落点检查一致。晓雨在第一室使用可见安全点连线路径避开家具。状态外观仍由 Core 派生，Core 规则、谜题答案、内容版本 6 和存档格式没有因本轮改动而改变。

## 资产检查

| 指标 | v5 历史清单 | v6 当前清单 | 上限 |
| --- | ---: | ---: | ---: |
| 三角面 | 114,452 | 84,160 | 150,000 |
| 总网格实例 | 244 | 164 | — |
| 最大同时可见网格 | 178 | 120 | 200 |
| 材质 | 14 | 15 | 24 |
| GLB 字节 | 56,502,720 | 29,908,172（28.52 MiB） | 60 MiB |
| 缺失外链 | 0 | 0 | 0 |

v6 将继承的曲线转为网格后纳入统计，内容检查同时读取实际 GLB 索引数和实例数；历史 v5 三角面口径未补算曲线，不用于精确减面百分比。GLB 文件体积减少约 47.1%。十二组桌椅共用一个网格。导出排除 Lookdev 角色、相机和灯光，不包含本机 `mc_model`。

实际使用九张 2K 图像：已采用的橡木、抹灰、水磨石各含 diffuse、normal、ARM。纹理估算为 **192 MiB**，按 RGBA8 未压缩数据与完整 mipmaps 保守计算，不是显卡实测常驻显存。检查未发现缺失 UV 引用；法线贴图、粗糙度与金属度通道已接入。权威源打包纹理，并固定倒角生成的网格/UV，以消除跨进程复导中的浮点差异。

只读临时复导与交付 GLB 哈希一致：

```text
sourceSha256: 198c33f8559282e3c9f6512ceb89052c2097c2a97d1260f8a0eb7c7593d84b60
runtimeGlbSha256: 312a92588433a97fd32db9a1d6b37d8e72f4cb0d985e5923094cc40797b8e346
ENVIRONMENT_CINEMATIC_CLASSROOM_CHECK_PASS triangles=84160 meshes=164 materials=15 readOnly=true
CLASSROOM_V6_CHECK_PASS desks=12 centralAisle=3.68 sideAisle=1.74 rearAisle=1.24
```

## 实际执行的验证

| 验证 | 结果与范围 | 本机日志 |
| --- | --- | --- |
| 灰盒导入与九场景冒烟 | 通过，精修前验证桌阵与交互布局 | `outputs/classroom-v6-graybox-smoke.log` |
| Core / 内容测试 | **127 通过、0 失败、0 跳过**；比原 126 项新增一项 v6 GLB 内容检查 | `outputs/classroom-v6-core-tests.log` |
| Godot Debug 编译 | 0 警告、0 错误，包含 Core 构建 | `outputs/classroom-v6-dotnet-build.log` |
| Blender 只读复导 | 节点、共享网格、布局、预算、外链与哈希通过 | `outputs/classroom-v6-check.log` |
| 九场景 headless 冒烟 | 场景、机关、回访换场、角色移动、对白与 UI 检查通过 | `outputs/classroom-v6-smoke.log` |
| 第一室专项 headless / 图形检查 | 11 个实际目标焦点、12 组桌椅包围盒、10 条双向胶囊扫掠、12 个家具内落点恢复、晓雨绕行到达及跟随循环通过 | `outputs/classroom-v6-probe-headless.log`、`outputs/classroom-v6-probe-rendered.log` |
| 三档分辨率捕获 | 每档 15 张既有入口截图及 17 张专项截图，另 1 张晓雨返室图，共 97 张 | `outputs/classroom-v6-capture-720.log`、`-900.log`、`-1080.log` |

```text
GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31
CLASSROOM_V6_PROBE_PASS targets=11 desks=12 routes=10 saves=12 companion=true
```

原有 Core 回归覆盖第一室解谜、部分安装与存档序列化、地图柜集中回访及失败/重复操作；引擎冒烟覆盖状态变体、家具内旧坐标安全修正和第一室—洋房双向换场。专项通行使用真实引擎碰撞扫掠和合成移动，不代表完整玩家操作。图形捕获直接设置相应测试状态，不把截图本身作为解谜链完成证据。测试入口抑制持久化，不覆盖玩家存档。

## 实际画面与性能

本机输出：[`review.html`](../../outputs/classroom-v6/review.html)、[`floor-plan.svg`](../../outputs/classroom-v6/floor-plan.svg)。同机位对照使用历史 v5 的四向视图及课桌近景；环境机位一致，历史人物表现不同，因此不比较人物质量。门与黑板近景因安全站位调整，不标为同机位。

截图目录为 `outputs/classroom-v6/{1280x720,1600x900,1920x1080}/` 和 `outputs/classroom-v6/probe/`。代表性原生截图复查覆盖三档分辨率的教学区、西窗、桌椅、纸面线索、资料板、抽屉、地图柜及开门状态：未见缺失贴图、纸张悬浮或门洞被墙体覆盖；墙裙、木面与阴影区域可辨认。修复了旧西墙缺口及门洞墙体重叠，明确 Color 环境光且 sky contribution 为 0，窗玻璃不再挡住日光。Compatibility 保持两个 ReflectionProbe，不依赖 SSAO 或 LightmapGI。

以下为最终一次本机 RTX 2080 Ti、Godot 4.6.3 .NET、Compatibility/OpenGL、默认帧同步条件下的 180 帧中央通道 360° 环绕采样。采样期间未同时运行其他图形验收进程。

| 分辨率 | 平均帧时 ms | P95 ms | 最大 ms | 最短镜头臂 m |
| --- | ---: | ---: | ---: | ---: |
| 1280×720 | 16.695 | 16.815 | 17.087 | 3.40 |
| 1600×900 | 16.697 | 16.840 | 17.488 | 3.40 |
| 1920×1080 | 16.696 | 16.813 | 17.639 | 3.40 |

原始数据：[`probe/metrics.json`](../../outputs/classroom-v6/probe/metrics.json)。环境 PackedScene 资源加载 596.469 ms；预加载环境后的房间实例化、角色接入及等待五帧合计 295.555 ms。此为重复运行后的缓存条件，未测清冷缓存启动；这两个计时范围不同，不能与历史单独资源加载数字混用。帧同步下接近 60 FPS 不代表 GPU 满载上限。中央通道环绕未压缩镜头臂，侧面近墙允许现有 SpringArm 正常缩短。

## 未验收与使用边界

- `[计划]` 负责人视觉终验、完整人工键鼠游玩、三名玩家 60–90 分钟盲测、真实 raw mouse delta、Xbox、GTX 1660 等目标硬件和 Windows 导出。
- `[计划]` 人工确认所有贴墙转身、临近家具绕行和连续交互的镜头舒适度；自动化焦点与通道检查不能替代此项。
- 本机音频输出设备不可用，图形捕获显式使用 Dummy；本轮不据此报告真实听音通过。
- `[固定约束]` 本机 `mc_model` 及含该角色的截图仅限本机非商业，不提交、推送、再分发或导出。截图与对照页位于被 Git 排除的 `outputs/`；本轮未提交、推送或生成发布包。

复验命令和实现边界见[第一室建模与布局 v6](../engineering/classroom-remodel-v6.md)。
