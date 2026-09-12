# 洋房大厅 v3 实际验收记录

日期：2026-09-09。`[已实现]` 已接入完整 Godot 场景；负责人视觉终验、人工完整游玩与目标硬件验收仍待执行。模型版本 v3 与剧情内容版本 6 独立。

## 交付入口

- [本机实机评审与新旧对照](../../outputs/manor-v3/review.html)、[带尺寸俯视图](../../outputs/manor-v3/floor-plan.svg)。对照按相同观察主题配对，因布局改变不是相同机位。
- [可编辑 Blend](../../art/blender/dream_manor/dream-manor-hall-v3.blend)、[运行时 GLB](../../assets/generated/dream_escape/3d/dream-manor-hall-v3.glb)、[统计与来源哈希清单](../../assets/generated/dream_escape/3d/dream-manor-hall-v3-manifest.json)。
- [制作与接入说明](../engineering/manor-remodel-v3.md)、[provenance](../../assets/generated/provenance.json) 中的 `dream_manor_hall_runtime_v3`。

含本机 `mc_model` 的截图只供本机非商业查看，不提交、分发或导出。整个本轮本机评审目录已加入本地 Git 排除；环境 Blend/GLB 本身不含人物。没有执行提交、推送或游戏导出。v1/v2 源和历史对照保留，旧外观 v1 不代表当前内部尺寸。

## 执行结果

| 检查 | 实际结果 |
| --- | --- |
| 灰模布局阶段 | 实际胶囊、复合边界、家具/楼梯禁入区与 Orbit 检查通过；保留 18 张灰模截图 |
| Core 编译 | 通过，0 警告、0 错误 |
| Core/内容/移动测试 | **128/128 通过**；保留原 127 项，新增 1 项 v3 实际 GLB、布局哈希、UV 与人物排除检查 |
| Godot Debug 编译 | 通过，0 警告、0 错误；Godot 4.6.3 .NET、net8.0、Compatibility 基线不变 |
| 源文件只读检查 | `MANOR_V3_CHECK_PASS`；临时复导 GLB 哈希一致、缺失外部文件 0、UV 和预算有效，检查不写源 |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31` |
| 四室重复往返 | `MANOR_V3_ROUNDTRIPS_PASS rooms=4 repeats=2 transitions=16 camera=true`；实际 DreamEscape 根场景合成按键换场、模态释放、返厅落点和相对镜头朝向通过 |
| 大厅专项 | `MANOR_V3_PROBE_PASS targets=8 active=5 routes=14 barriers=5 saves=11 companion=true graybox=False` |
| 三档分辨率 | 1280×720、1600×900、1920×1080 各 17 张，另 1 张跟随图，共 **52 张**完整模型实机截图 |

大厅专项在各解锁阶段检查 8 个稳定标记中恰有 5 个有效目标，使用实际角色胶囊检测 14 条双向路线与 5 类阻挡，并通过 11 次实际控制器重建检查墙边、家具、楼梯和内廊收窄处的存档位置修正。另检查 4 条静态障碍绕行到达及实际晓雨跟随环路。原冒烟继续覆盖大门交互无剧情状态副作用、取消/重复开门、门叶复位、输入门禁和序章完整规则路径。所有引擎验证均抑制用户存档写入。

复验命令见[制作说明](../engineering/manor-remodel-v3.md)。记录位于 `outputs/manor-v3-core-build.log`、`manor-v3-core-tests.log`、`manor-v3-build.log`、`manor-v3-source-check.log`、`manor-v3-smoke.log` 与 `manor-v3-probe-rendered.log`。导入日志中曾出现编辑器退出阶段的 AndroidEditorSettings 清理错误，进程退出码为 0；最终运行时冒烟与图形专项均完成通过，不将其描述为零诊断导入。

## 资产结果

| 指标 | 当前 v3 | 上限 |
| --- | ---: | ---: |
| 三角面 | 129,386 | 180,000 |
| 最大同时可见网格 | 62 | 220 |
| 材质 | 15 | 24 |
| GLB | 28,373,164 字节（27.06 MiB） | 60 MiB |
| 使用图像 | 12 | 按实际使用计数 |
| 纹理内存估计 | 238.67 MiB | RGBA8 + mipmaps 估计，不是实际 GPU 占用测量 |

Blend SHA-256：`487e2aef155f91df3352ebd42601f175c9240fde65556587ad263e24f410f714`。

GLB SHA-256：`a8b78a7cdabccf590539992d9d663b2428c30f76e6473554455ce1689eecd8dd`。

当前源包含实体门套、门叶与五金、冷壁炉、软椅、边桌、玄关柜、封闭折返楼梯、长凳、护墙板、画框、壁灯及吊灯；修改器结果已固定，构件木纹按长轴定向。复用已有 CC0 贴图与原样复制的旧离线风景纹理，没有新采购或图像生成。运行时模型中无骨架、人物、动画、相机或灯光；Godot 灯光和碰撞单独维护。

## 实机截图与性能

最终捕获使用 NVIDIA GeForce RTX 2080 Ti、OpenGL 3.3、Compatibility；窗口实际尺寸与 PNG 尺寸逐项匹配。采样每个区域先等待 30 帧，再自动环绕 180 帧。时间为 FramePostDraw 间隔，受系统调度和帧同步影响，不是纯 GPU 时间，也不能外推至 GTX 1660。

| 分辨率 | 区域 | 平均帧时 | P95 | 最大帧时 |
| --- | --- | ---: | ---: | ---: |
| 1280×720 | 前厅 | 16.697 ms | 16.777 ms | 16.845 ms |
| 1280×720 | 内廊 | 16.698 ms | 16.772 ms | 16.929 ms |
| 1600×900 | 前厅 | 16.698 ms | 16.770 ms | 16.820 ms |
| 1600×900 | 内廊 | 16.699 ms | 16.767 ms | 16.798 ms |
| 1920×1080 | 前厅 | 16.977 ms | 16.789 ms | 50.680 ms |
| 1920×1080 | 内廊 | 16.698 ms | 16.769 ms | 16.975 ms |

最终环境资源加载 862.991 ms；随后场景实例就绪（含本机角色与五帧等待）595.129 ms，二者分别计时。最终图像与数据见[metrics.json](../../outputs/manor-v3/probe/metrics.json)。第一次完整模型采样的环境加载为 911.550 ms，含角色就绪为 2151.822 ms，720p 内廊最大帧时 91.515 ms；原数据保留在[metrics-initial.json](../../outputs/manor-v3/probe/metrics-initial.json)，不以最后一次采样掩盖首次尖峰。两轮都出现过尖峰，尚无长时稳定性结论。

前厅与内廊环绕时 SpringArm 最短分别为 2.497 米与 3.4 米；前厅近墙缩臂属于既有碰撞处理。逐图检查了入口全景、前厅反向、楼梯、壁炉、陈设、四室门、单/双门开启和碰撞覆盖。当前采样中通路、家具轮廓和门框在暗部仍可辨认，楼梯封闭区与主通路分离，门洞没有原先的实墙穿插。截图脚本在捕获阶段隔离外部鼠标/摇杆输入，固定机位重新捕获；它不改变游戏正常输入。

## 尚未验收

`[计划]` 负责人视觉终验、至少 3 名剧情 RPG 玩家的完整盲测、真实 Xbox、物理鼠标 raw delta、GTX 1660/中低端集显、长时性能、音频设备和 Windows x64 导出仍待执行。图形检查使用 Dummy 音频驱动，不提供声音验收结论。楼梯仅为封闭景观，没有二层玩法；Core/存档字段/四室解锁顺序/内容版本 6 保持不变。
