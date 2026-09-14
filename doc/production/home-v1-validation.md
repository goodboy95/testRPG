# 主角家 3D v1 验收记录

日期：2026-09-12。`[已实现]` 独立住宅漫游；`[计划]` 醒后剧情接入。实现和启动见[住宅 v1](../engineering/home-v1.md)。

`[已实现]` 同日村庄接入后的独立住宅回归再次通过 179 项检查，三分辨率 84 图已更新至 `outputs/home-v1/validation/`，此次已热身工作机加载 687.4 ms。日志 `outputs/village-v1-final-home-regression.log`，最新结果见[村庄验收](village-v1-validation.md)。下文 155 项基线与较早加载耗时为住宅首次交付记录，当前项目测试总数为 162，不能将本次热加载描述为冷启动性能。

## 本轮范围

12×9×3.2 米住宅、15×18 米院地、两卧一厅与独立厨卫；五处门（入口双扇）、六组灯、11 个交互标记。没有新增剧情事件、晓雨、调查秘密、存档字段或正式流程入口，内容版本仍为 6。门灯、暂停和重置只属于当前实例。

## 资产检查

`[已实现]` Blender 5.2.0 LTS 只读重导通过 `HOME_V1_CHECK_PASS rooms=5 doors=5 lights=6 readOnly=true`。检查读取实际 GLB，验证建筑地面世界尺寸、五处门轴、屋瓦法线、必需结构、椅子/瓦面共享网格、有效 UV、8 张嵌入贴图、无角色/动画/相机、源和布局哈希及临时复导一致性。日志：[只读检查](../../outputs/home-v1-final-asset-check.log)；权威统计：[manifest](../../assets/generated/home/3d/home-v1-manifest.json)。

| 项目 | 实际 | 上限 |
| --- | ---: | ---: |
| 三角面 | 76,648 | 180,000 |
| 最大可见网格 | 98 | 220 |
| 共享后唯一网格 | 95 | — |
| 材质 | 17 | 24 |
| GLB | 26,270,220 字节／25.05 MiB | 60 MiB |
| 纹理内存估算 | 170.67 MiB | 256 MiB |

可编辑源为 `art/blender/home/home-v1.blend`，原始采用贴图已打包；GLB 位于 `assets/generated/home/3d/`，Godot `.glb.import` 使用 `gltf/embedded_image_handling=1`，采用的派生贴图随镜像保留。复用现有 CC0 抹灰、木材和 terrazzo 图；灰砖只使用 terrazzo normal/ORM。几何为本次离线制作，无新增付费资产或依赖。灰模位于 `work/home-v1-graybox/`，与正式细模隔离；[平面图](../../outputs/home-v1/home-v1-floor-plan.png)直接读取共享布局。

最终灰模已按当前布局重新生成（18,268 三角面、98 网格、12 材质、无贴图），见[灰模构建日志](../../outputs/home-v1-final-graybox-build.log)。生成后再次确认正式源、GLB、布局哈希匹配，灰模使用同一布局哈希。交付文档的 176 处本地链接无缺失，`git diff --check` 通过；既有 `.blend`／`.glb`／纹理 LFS 属性适用，本轮没有暂存或提交。

## 规则与场景

`[已实现]` Core/内容/表现层测试 **155/155**，0 失败、0 跳过。相对 151 基线新增 4 项：完整住宅布局；门口/扫掠占用与重复输入；半途重置和实例隔离；实际 GLB/共享网格/UV/贴图/本地化契约。没有修改 Core 业务规则。日志：[最终测试](../../outputs/home-v1-final-core-tests.log)。

`[已实现]` Godot 4.6.3 .NET Compatibility Debug 编译 0 警告、0 错误，见[最终构建](../../outputs/home-v1-final-game-build.log)。原九场景回归通过：

```text
CHARACTER_MOTION_PASS jog walk pivot modal focus wall slide legacy fallback
MANOR_V3_ROUNDTRIPS_PASS rooms=4 repeats=2 transitions=16 camera=true
PROLOGUE_V6_PASS groups=12 stories=13 imported=21 cancellation paper photo stage gamepad_undo
UI_REMAKE_PASS scales=3 native_enter native_a mouse_drag no_references focus drag_payload cancel_drag sequence_undo explicit_submit
GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31
```

首次回归出现全部 PASS 后退出时报告 504 个 GodotShape3D RID 未释放；未确认原因，也未改动公共冒烟脚本。停止其他引擎/资产任务后顺序复跑，正常退出且无该错误。保留[首次日志](../../outputs/home-v1-nine-scene-smoke.log)与[最终复跑](../../outputs/home-v1-final-nine-scene-smoke.log)，不能仅凭猜测归因于已有代码或并发。

`[已实现]` 有图形住宅专项通过 **179 项检查**：闭门阻挡/开门通行、五门反复开合、入口双扇、门口防夹与重复输入、隔墙阻断、六组灯、20 段双向胶囊通路检查，以及实际输入连续步行经过全部房间、门廊、院子和侧后通路。验证慢走/慢跑、环绕、贴窗镜头收缩/角色隐藏与恢复、暂停/失焦清零、F/A 操作、Esc/B 恢复、菜单键盘重置、场景重载及缺失模型 billboard。会话序列化和正式存档字节前后相同。输入通过引擎注入，不等于真实设备操作。详情：[结果 JSON](../../outputs/home-v1/validation/results.json)、[引擎日志](../../outputs/home-v1-final-rendered-probe.log)。

复现（项目根目录 PowerShell）：

```powershell
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/HomeV1Probe.tscn
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/HomeV1Probe.tscn -- --home-capture
```

`--home-capture` 使用窗口图形渲染并写入 `outputs/home-v1/validation/`。常规 headless 专项不含图形捕获项，检查计数相应较少；在末尾加 `-- --home-graybox` 可验证同布局的引擎灰盒。

## 图形与性能记录

`[已实现]` 1280×720、1600×900、1920×1080 各 28 张，共 **84 张 PNG**：每档 12 个玩家视角的关灯/开灯对照，加四面建筑全貌。覆盖两卧、厨卫、起居/餐区、门口、门廊、院子和侧后通路。全部九张联系表已逐张复核，重点查看材质尺度、暗部、门窗、屋顶、家具结构和镜头遮挡。

- [720p 联系表 1](../../outputs/home-v1/contact-1280x720-1.jpg)、[2](../../outputs/home-v1/contact-1280x720-2.jpg)、[3](../../outputs/home-v1/contact-1280x720-3.jpg)
- [900p 联系表 1](../../outputs/home-v1/contact-1600x900-1.jpg)、[2](../../outputs/home-v1/contact-1600x900-2.jpg)、[3](../../outputs/home-v1/contact-1600x900-3.jpg)
- [1080p 联系表 1](../../outputs/home-v1/contact-1920x1080-1.jpg)、[2](../../outputs/home-v1/contact-1920x1080-2.jpg)、[3](../../outputs/home-v1/contact-1920x1080-3.jpg)
- [1080p 正面](../../outputs/home-v1/validation/1920x1080/exterior-front.png)、[平面布局](../../outputs/home-v1/home-v1-floor-plan.png)

本轮根据截图修正了屋瓦朝内法线、棕色地面/过曝灯光、部分柜门与床头朝向，以及窗洞导致 SpringArm 穿到室外的构图。玻璃增加镜头专用碰撞，捕获继续运行近镜头隐藏，避免大面积角色遮住画面；围墙外增加纯景观背景地面。关闭灯光时较暗的家人卧室、浴室和后通路仍可辨认结构，最终明暗和镜头舒适性等待负责人游玩确认。

本机 GPU 为 NVIDIA GeForce RTX 2080 Ti，每档记录 180 个渲染帧，包含垂直同步/帧率节奏的墙钟采样：

| 分辨率 | 平均 ms | P95 ms | 最大 ms |
| --- | ---: | ---: | ---: |
| 1280×720 | 16.690 | 16.759 | 17.959 |
| 1600×900 | 16.692 | 16.787 | 16.868 |
| 1920×1080 | 16.693 | 16.775 | 16.979 |

原始记录位于各分辨率子目录的 `frame-times.json`。这些数据仅说明本次静止捕获点的本机帧节奏，不代表 GPU 极限、全程移动帧率或目标硬件认证。

首轮场景初始化为 **35,119.522 ms**，最终为 **13,122.8 ms**。测量包含场景加载、实例化、本机主角加载及五个物理帧，不包含控制台进程启动，也没有控制冷/热缓存条件；不能据此宣称已优化冷启动。首轮数据保存于 `outputs/home-v1/before/results-first-pass.json`，最终见专项结果 JSON。

## 待验收与交付限制

- `[计划]` 负责人完整漫游和视觉确认，尤其近墙镜头、家具自然度、清晨暗部与灯光舒适性；本轮代理截图复核不代替该确认。
- `[计划]` 真实 Xbox、物理鼠标 raw delta、目标硬件冷启动与长时间移动性能、正式 Windows x64 导出。本轮未执行 Release/导出，不沿用此前其他任务的 Release 结果。
- `[计划]` 醒后剧情、村庄连接、家人角色和任何调查内容另行设计接入，独立漫游不表示第一章已完成。
- `[固定约束]` 环境 GLB 不含角色。本机 mc_model、动作及含角色截图只限本机非商业，不得提交、推送、分发或导出；没有本机模型时保留 billboard。没有为本轮执行提交或推送。
