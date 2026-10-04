# 人物第 2 版：自然比例动画风格试验

`[已实现]` 2026-09-20：从 v007-casual 的配方派生三轮独立静态候选，交付实际 Blend、GLB 和双引擎对照。**造型试验已制作；美术未通过。** 负责人对 v001 正常脸的批准不延伸至此版，角色身份未定，正式村庄及低模 NPC 未替换。

## 可查看的交付

- 编辑源：`art/blender/characters/mpfb-male-v1/candidates/v010-stylized-v2/character.blend`。
- 首轮 v008-stylized-v2、第二轮 v009-stylized-v2 保留在同级候选目录；配方位于 `recipes/`，设计数据为 `style/stylized-v2-r1.json` 至 `r3.json`。
- 当前静态资源：`assets/generated/characters/mpfb-male-v1/v010-stylized-v2-game/character.glb` 及 manifest。没有骨架、动画或 LOD。
- [同条件对照页](../../outputs/character-workflow/stylized-v2-review/comparison.html)：直接引用实际 PNG，无后期修图。左 v007，右 v010。
- 完整来源、脚本快照哈希、原文件保护及限制：`art/blender/characters/mpfb-male-v1/reports/stylized-v2-delivery.json`。

当前源 SHA-256：`b22b11d690588e479a1a79d73c2181ec67d6de009510d331777c5810adbc7b6b`。

当前 GLB SHA-256：`984a6bf5160d14358df22818d554c0d319b39a43a9d031f87e33c35837fe9b23`。

## 实际修改与三轮记录

| 版本 | 修改 | 实际读图结果 |
| --- | --- | --- |
| v008 | 双眼原生 scale-incr 0.12、下巴宽度 decr 0.06，轻微原生口部形变；独立皮肤材质；12 组原创发束及编辑曲线 | 脸部结构可辨；发束后侧明显浮离，嘴仍露牙，不能采用 |
| v009 | 发束按人体头皮射线贴合，衣物纹理低频化，虹膜 UV 小幅调整；继续原生闭嘴参数 | 贴合改善；耳侧出现发根干涉，发帽边界过硬 |
| v010 | 发际边界与耳侧避让、前发束宽度和哑光程度；口部原生形变收整 | 保留自然鼻梁、鼻翼和下巴体积；动画化方向明显，但自然碎发及闭嘴目标未完全达到 |

几何只采用 MPFB 原生形变，不自由拉人体顶点；原生拓扑、UV 和形变键保存在源中。眼球与眉睫通过官方附着规则重新配准。双眼参数值 0.12 **不是实测眼裂增长 12%**，未宣称达到 5–8% 的精确解剖尺寸目标。虹膜使用第二 UV 层在两个虹膜区域缩放至 1.05，原始 UV 保留；这是纹理区域调整，不是眼球几何放大。

新头发为项目脚本制作的不透明网格：12 个主发束顶点组、Scalp 组、12 条 Bézier 编辑导线、薄厚度修饰器及四个共享发色材质。编辑导线是制作参考，改曲线本身不会自动重新生成网格；可直接编辑网格，或改 JSON 后以新版本重建。本轮没有制作独立细发卡，不使用外部或受限角色发束。整体仍偏大块片状，不能称为精修发型。

皮肤采用独立宽幅顶点色分区，去除原皮肤纹理的毛孔、斑点与头皮短茬，粗糙度 0.73。没有手工高模雕刻或法线烘焙。GLB 在导出副本上将这些色区烘焙为原 UV 的 2K Albedo，以修复 Compatibility 顶点色显示偏暗；不是把阴影烘入贴图。衣物使用官方 CC0 纹理派生副本，256 像素低频采样与原图按 80/20 混合，法线强度 0.3、粗糙度 0.88；腰带、口袋和主要褶皱仍保留。睫毛 alpha 乘 0.55。

## 工作流使用

`character_workflow.py --style` 扩展为可选字段，旧配方继续按原行为创建。造型函数集中于 `scripts/tools/character_stylized.py`，本轮工具快照保存在 `style/tool-snapshot-20260920/`。原始官方资源位于独立 `art/sources/cc0/mpfb-male-stylized-v2/`，34 个依赖文件已检查。

```powershell
# 使用未占用的新 ID 重建，不覆盖交付源
python scripts/tools/character_workflow.py baseline --parent v007-casual --version style-replay01 --style art/blender/characters/mpfb-male-v1/style/stylized-v2-r3.json
python scripts/tools/character_workflow.py check --version style-replay01
python scripts/tools/character_workflow.py export --version style-replay01
python scripts/tools/character_workflow.py render --version style-replay01 --glb
# 交付模型审图；加 --character-capture 输出三分辨率矩阵
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/CharacterBaselineReview.tscn -- --character-manifest=res://assets/generated/characters/mpfb-male-v1/v010-stylized-v2-game/manifest.json
```

审图场景默认仍为正常脸基线。1–8 选择视角、G 切换材质／无发灰模／带发灰模、右键旋转、滚轮缩放、Esc 退出。新资源以角色标记及 manifest 网格角色识别部件，旧资源从自身配方解析，不再硬编码 short01/02/04。静态导入关闭自动 LOD 和动画导入。

## 实际验证

| 项目 | 结果 |
| --- | --- |
| 保存重开、同配方独立重建 | v010 最大坐标误差 0 米，拓扑与部件一致；源未改写 |
| 原生参数修改和恢复 | 最大可观察变化 0.0006608516 米，恢复误差 0 米 |
| 回归原基线与穿戴配方 | v001、v007、首轮 v008 的 check 均通过 |
| 原文件保护 | v001、approved/v001、v007 Blend 哈希与本轮前记录相同 |
| GLB 导出／重导入 | 111,680 三角形、9 网格、12 材质面、9 内嵌图像，UV／法线存在，无动态资源 |
| Blender 对照 | v007 16 图，v010 24 图；相同相机、曝光、显示尺寸、灯光，增加实体发束灰模 |
| Godot 对照 | v007 48 图，v010 72 图；720p／900p／1080p，Compatibility，正式状态／存档字节不变 |
| Core／内容 | 219 通过，数量未变，本轮不新增业务逻辑 |
| C# 编译 | 0 警告、0 错误 |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31` |

最终 Blender 图目录：`outputs/character-workflow/v010-stylized-v2/blender-glb/20260920-202535/`。

最终 Godot 图目录：`outputs/character-workflow/v010-stylized-v2/godot/20260920-202715/`。日志在 `outputs/character-workflow/` 的 `stylized-v2-*` 文件；命令及机器耗时在 `logs/*.json`。用户主动操作时间未知，MCP 仍未接通。编辑器导入退出有既有资源释放告警，不能称为无警告引擎导入；运行审图与冒烟通过。

## 代理视觉判断与未完成项

实际逐图读取了三轮正面／45°／侧面，最终无遮发灰模正面与侧面、实体发束灰模、全身正侧背，以及 Godot 三种分辨率的关键角度，并与 v007 对照。

- 眼眶、眼球和鼻翼在所读图中未发现明显错位；眼白比例仍自然，未做夸张大眼。侧面鼻唇下巴轮廓保持人形。
- 皮肤和衬衫碎纹理减少，普通 PBR 保留柔和体积；Godot 的光照比 Blender 更平，不能称为像素一致。
- **发束有明显条片感，顶部根部接缝和侧面大块发帽边界仍可见。** 证据为材质和带发灰模 head-right90、head-left45。
- **嘴部未完全自然闭合，近景仍有细亮口缝，唇色偏弱。** 正面及左右45°均可见；不能标记为闭嘴验收通过。
- 颈部有轻微多边形阴影转折，皮肤略显塑料；衣物主要褶皱保留但局部纹理简化偏糊。所读全身图未见新的明显衣物穿插。

按最多三轮造型迭代的边界停在 v010；不以材质掩盖这些不足，不自动放行。新造型 G1 **未通过／待用户看图**；G2–G5、动画、LOD、量产、正式 NPC 接入和目标硬件性能均未测试。

## 失败记录

v008 初次保存时，脚本变量名复用使 Blend 被写到隔离插件的 `mouth-compression.target.gz` 路径。已经将 Blend 恢复到正确候选目录，并从锁定官方 ZIP 恢复该原生形变文件，与原备份逐字节相同；加入候选路径断言。批准基线未受影响，失败日志与修复记录保留，随后 v008 重建检查通过。

首个 Godot v010 导出因顶点肤色偏暗未采用，保留历史。当前 `v010-stylized-v2-game` 只修正导出材质表示，源造型没有第四轮修改。Godot 导入器会给网格资源名添加前缀，首次角色映射诊断失败后已修正并重新捕获完整矩阵。
