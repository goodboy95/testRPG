# 沈砚静态 v2 制作与自检记录

2026-09-18，Godot 4.6.3 .NET / Compatibility、Blender 5.2.0 LTS、RTX 2080 Ti。

`[已实现]` 旧失败模型已退出项目活动路径；新的独立 CC0 来源、原创设计参考、可编辑候选、PBR 烘焙、静态 GLB 与审图入口已制作并验证。**自检结论：技术可导入，美术未通过高质量样板门槛。** `runtimeReady=false`、`visualApproval=false`；没有继续绑定、动画、LOD、女性基体或批量制作。

## 实际验证

| 项目 | 结果 |
| --- | --- |
| 静态几何 | 31,439 个三角形，一个导出网格、三个材质面；源保留 36 个编辑部件 |
| 尺寸 | 实际包围盒高度 1.721660 米，约 1.73 米设计目标；未做最终脚底原点/绑定标定 |
| 贴图 | 九张真实 Albedo/Normal/ORM；皮肤/衣服 2K、头发 1K；源使用相对路径，GLB 内嵌 |
| 来源和哈希 | 独立 CC0 collection、上游 README、包哈希、独立基体哈希；源/纹理/GLB 哈希由导出工具记录 |
| 导出与只读复验 | `SHEN_STATIC_EXPORT_PASS`、`SHEN_STATIC_CHECK_PASS`；源前后哈希相同，GLB 再导入成功 |
| 骨架/动作 | 0 skin、0 skeleton、0 animation；Godot 自动 LOD 与动画导入关闭 |
| 引擎静态专项 | `SHEN_STATIC_REVIEW_PASS triangles=31439 surfaces=3 images=39 stateUnchanged=true` |
| Core/内容/表现层规则 | 219 项通过，原 217＋2 项禁止退役美术/未批准静态候选进入功能预览的测试 |
| Godot 编译 | Debug 成功，0 warnings、0 errors |
| 九场景回归 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31` |
| 原低模 NPC 回归 | `VILLAGE_NPC_PROBE_PASS checks=363 samples=1080`；两人交谈/重复取消、巡逻、住宅往返和状态隔离 |
| 正式状态隔离 | 静态专项与原 NPC 专项均比较会话序列化值和正式存档字节，未发生变化 |

日志位于 `outputs/shen-yan-v2/`：`build.log`、`check.log`、`godot-review.log`、`smoke.log`、`npc-regression.log` 和 `test-results/shen-yan-v2-regression.trx`。Godot 矩阵结果是 `godot/results.json`，含本次 GLB SHA-256。原低模 NPC 专项完整结果位于 `outputs/village-npc/20260918-082632-probe/results.json`；其含受限主角的游戏截图不属于本次美术交付物，禁止提交、分发、导出或用作对外证据。

引擎导入进程结束时仍有编辑器资源释放提示；原 NPC 专项仍出现 Terrain3D 的 `instance_reset_physics_interpolation` 弃用提示。最终静态图形专项没有脚本错误。初次静态专项遇到音频设备不可用，后续使用与画面无关的 Dummy 音频驱动运行。

## 图形与自检

实际检查包括六张 Blender 导出后重导入的图、引擎正侧背/头部/第三人称距离和日/阴影/夜光照。自动生成 720p、900p、1080p 共 39 张引擎图；并非每张都进行了逐像素人工评审。

可直接复查的无受限资源图：

- `outputs/shen-yan-v2/godot/1080-neutral-face.png`：近景五官与发型。
- `outputs/shen-yan-v2/godot/1080-day-threequarter.png`：日间全身与服装轮廓。
- `outputs/shen-yan-v2/godot/1080-neutral-back.png`：后领、后发际、背部和袖口。
- `outputs/shen-yan-v2/godot/1080-night-threequarter.png`：夜间轮廓和配色。
- `outputs/shen-yan-v2/exported/`：六张 Blender GLB 重导入视图。

已经修正了早期的眼球位置、袖口和后领大面积穿插、断裂颈部、过量网格及衣领矩形 AO 色斑。最后一项用实际 AO 诊断图确认，重做衣领三角划分和烘焙 UV 边距后，在 AO 图与 Godot 实拍中均已消失；没有用修改截图的方式掩盖。

**仍未通过的美术项：**

1. 眼睑、鼻翼、唇部和脸部平面仍显生硬，表情与人物辨识不足，未达到设计图里的自然青年感。
2. 发型仍有整块外壳感，侧分和发束连接不够自然，近景可见规则接缝。
3. 衬衫和裤子过于平滑，肩袖、领口及内搭边缘需要进一步修型；鞋子缺少可信的帆布与鞋底构造。
4. 皮肤/衣物颜色层次和细节仍较弱，硬光会放大面部平面和衣服简化形态。已有贴图格式不等于已经完成高质量 PBR 美术。

因此该候选只证明新资产的来源、可编辑性、静态制作和引擎检查链可用。脚本网格制作目前尚未达到已确认的视觉目标；不能把“长得像人”“比旧版好”或测试通过作为采用依据。后续先集中修头部和发型，真实近景通过后再精修整套服装，仍不扩展 NPC 数量。

## 性能口径与未执行项

静态审图房间实际记录为 31,439–62,880 primitives、3–7 draw calls，差异来自地面及日/夜的模型阴影。**三个材质面不等于整帧只有三个 draw call。** 这些不是村庄场景性能数据，也没有进行 GPU 均值/p95 或动画主线程开销验收。

真实 GTX 1060 笔记本、绑定变形、行走/交谈、12 人矩阵、模块组合和负责人视觉确认均未完成。本轮主动停在静态美术阶段；正式 v6、低模 NPC 试点、Core 业务和存档格式未替换。

## 旧模型清理

批量删除旧模型的操作曾被工具自动审批拒绝，返回原因仅为 `blocked by policy`。随后改用可逆隔离，旧源、旋转备份、贴图、GLB、目录、构建器和专用场景/控制器转入 `work/shen-yan-v2/retired/`，该目录被 Git、Godot 和 C# 编译排除。项目活动路径已移除旧入口；没有声称旧文件被永久物理删除。
