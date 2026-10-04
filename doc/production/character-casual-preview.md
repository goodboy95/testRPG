# MPFB 头发与便服静态样板

`[已实现]` 2026-09-20：负责人确认“确实像人”后，按追加请求为同一正常脸加入头发与衣服。v001 的 G1 确认、源和配方已归档于 [approved/v001](../../art/blender/characters/mpfb-male-v1/approved/v001/approval.json)。本次交付 **v007-casual**，穿戴造型尚未获用户批准，不代表沈砚身份或正式角色定稿。

## 当前交付

- [可编辑 Blend](../../art/blender/characters/mpfb-male-v1/candidates/v007-casual/character.blend)、[复现配方](../../art/blender/characters/mpfb-male-v1/recipes/v007-casual.json)、[静态 GLB/报告](../../assets/generated/characters/mpfb-male-v1/v007-casual/manifest.json)。[制作及启动命令](../engineering/character-workflow-mpfb.md#后续授权静态头发与便服)。
- 官方 `short02` 短发，材质乘数 `[0.23,0.25,0.29]` 压暗原生棕色；`male_casualsuit01` 蓝衬衫及牛仔裤；`shoes05` 白色运动鞋。来自已下载、已锁定哈希的官方 CC0 基础包；[采用来源](../../art/sources/cc0/mpfb-male-casual-short02/provenance.json)、[38 项依赖/许可检查](../../art/sources/cc0/mpfb-male-casual-short02/dependencies.json)。未新增下载、付费或受限资产。
- 与 v001 使用同一宏观/脸部参数，鼻部实验参数仍为 0。衣下人体采用官方遮挡组；鞋内袜筒在世界高度 0.075 米以上增加非破坏性 Mask，避免穿出裤脚。源的原始人体/部件网格仍在，静态导出才烘焙遮挡与修饰器。
- 当前静态成本 **94,576 三角形、9 网格/材质面、10 张内嵌图像**。低于裸基线面数是因为衣下人体被遮挡，不是面部减面。无骨架/动画/LOD，`runtimeReady=false`、`visualApproval=false`。

源 SHA-256：`3fb1ea533cf251751cae6cf447236434f96b3c17884243ef09ad6917bf9bd6f3`。

GLB SHA-256：`d1d232973bfb4a5c995b88021bbea1ec1371fe1928b52bb542e340a0dd90ee93`。

## 实际读图与局限

实际读取 Blender 正面、45°近景、全身正背，以及 Godot 正面/45°近景、全身正侧背。当前静态姿势中，领口、袖口、腰带和裤脚衔接未见明显皮肤或袜子穿出；头发不遮眼，不改变正常脸结构。图片为实际渲染，不经图像生成或修图。

| 证据 | 观察 |
| --- | --- |
| [Blender 全身](../../outputs/character-workflow/v007-casual/blender-glb/20260920-182102/material-body-front.png) | 衬衫、牛仔裤、腰带与白鞋的完整搭配可见；原生 A 姿态用于静态审查 |
| [Godot 近景](../../outputs/character-workflow/v007-casual/godot/20260920-182232/1080-material-head-right45.png) | 眼部与脸部保持；最终发型没有首轮明显的碎片堆叠，但发际线仍偏硬、发量偏厚，近看有模板/帽状感，不能称为精修发型 |
| [Godot 全身侧面](../../outputs/character-workflow/v007-casual/godot/20260920-182232/1080-material-body-side.png) | 袖口、裤脚与鞋的位置可见，无已发现的袜筒穿透 |
| [Godot 背面](../../outputs/character-workflow/v007-casual/godot/20260920-182232/1080-material-body-back.png) | 后领与后腰接合；布料褶皱偏写实模板，尚未做项目风格处理 |

最终头发在 GLB 使用 MASK 0.15，眼/眉/睫保留先前已验证的 BLEND，实体服装 OPAQUE。灰模隐藏发片与眉睫以便检查人体/衣服结构；不能把灰模隐藏当成材质正常的证据。不同渲染器照明和高光有差别，不做像素一致承诺。鞋底约低于源原点 1.56 厘米，审图场景无地面；未来接入演员时需设置站立高度，本轮不改身体比例或动作。

## 试验与回归

v004-casual 的 short01 头顶发片碎裂和袜筒穿透未放行；v005 的 short04 在混合透明时有黑边，裁切后又损失发际线柔和度；v006 试过 short02 原色。全部源/日志保留。最终 v007 使用 short02 深色材质、原生零细分发片、较低裁切阈值及袜筒 Mask；没有沿用失败的旧 NPC 人体。

| 检查 | 实际结果 |
| --- | --- |
| 新配方与基线回归 | v007-casual、v001 `check` 通过；配方重建及参数恢复最大误差均 0，源哈希保持。[专项报告](../../outputs/character-workflow/v007-casual/check.json) |
| GLB 重导入 | 9 网格、UV/法线及三角面一致，源不覆盖；多网格审图继续可用 |
| Blender | 最终 16 张固定视角/材质图，[相机与哈希](../../outputs/character-workflow/v007-casual/blender-glb/20260920-182102/results.json) |
| Godot Compatibility | 三分辨率共 48 图，正式会话及存档字节不变；`CHARACTER_BASELINE_REVIEW_PASS triangles=94576 surfaces=9 images=48 stateUnchanged=true`。[报告](../../outputs/character-workflow/v007-casual/godot/20260920-182232/results.json) |
| C# 编译 | 0 警告、0 错误。[日志](../../outputs/character-workflow/build-casual-final.log) |
| Core/内容 | 219 项通过，测试数未变，无 Core 修改。[日志](../../outputs/character-workflow/tests-casual.log) |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。[日志](../../outputs/character-workflow/smoke-casual.log) |

`[固定约束]` 正式 v6、低模 NPC 试点和正常脸审图默认入口不替换；穿戴版通过独立 manifest 参数选择。未做动画穿插、表情、批量搭配兼容或目标硬件性能验收。MCP 状态维持未接通。没有提交、推送或导出游戏。
