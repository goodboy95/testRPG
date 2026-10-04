# 自然青年头肩修订：实际结果

2026-09-20。`[已实现·静态试验]` 脸部纠正、局部皮肤处理和可编辑曲线管线已实现；**头发美术未通过，整套头肩样板不放行**。按负责人指定的阶段停止条件，头发一次集中修正后仍存在明显缺陷，本轮停止造型迭代，没有进入全身精修。

## 可检查的交付

- [v014 无发脸部源](../../art/blender/characters/mpfb-male-v1/candidates/v014-head-material/character.blend)、[GLB](../../assets/generated/characters/mpfb-male-v1/v014-head-material/character.glb)：保留已改善的嘴部和肤色，完整身体及原蓝衬衫仍在。
- [v016 带发失败候选源](../../art/blender/characters/mpfb-male-v1/candidates/v016-head-shoulders/character.blend)、[GLB](../../assets/generated/characters/mpfb-male-v1/v016-head-shoulders/character.glb)、[配方](../../art/blender/characters/mpfb-male-v1/recipes/v016-head-shoulders.json)。保留以便定位问题，不推荐采用其头发。
- [同条件对照页](../../outputs/character-workflow/head-refine/review.html)：v007、v010、v014、v016 的实际渲染，无生成图修饰。
- [技术与来源记录](../../art/blender/characters/mpfb-male-v1/reports/head-refinement-delivery.json)、[源完整性检查](../../outputs/character-workflow/head-refine/source-integrity.json)。

正式 v6、低模 NPC 试点、默认正常脸审图、Core、剧情、存档和已批准 v001 均未切换。用户批准仍待确认；原 G1 批准不延伸至本轮，G2–G5 未测试。

## 阶段记录与代理读图

| 阶段 | 初稿 | 一次集中修正 | 实际结果 |
| --- | --- | --- | --- |
| 脸部结构 | v011：中部闭合，但嘴角仍露牙 | v012：扩展唇缘纠正范围 | 正面、左右45°没有观察到露牙；侧面保留唇厚、鼻唇下巴关系 |
| 脸部材质 | v013：局部皮肤改善；睫毛形变在 MPFB 重贴后不能恢复 | v014：改用独立 Shrinkwrap 修饰器缩短外伸睫毛 | 重贴与恢复通过；唇色、眼周和面颊变化保留，停止继续美化 |
| 头发整合 | v015：整体体积过于帽状 | v016：收紧体积、12条真实曲线发束 | 仍像底层头套加凸条，后脑发束折转，**失败并停止** |

口部诊断将牙齿临时显示为红色、舌头蓝色，皮肤灰色，在只读审图副本中观察。v007 中央唇缘间隔约2.65毫米；v012 不再看到红色露牙。诊断图为 `outputs/character-workflow/head-refine/baseline-mouth.png` 与 `closed-mouth.png`。没有隐藏牙齿、涂黑口缝或使用负向张嘴参数。

新配方从 v007 的原生中性结构创建，只保留左右眼 `eye-scale-incr` 各0.12和虹膜UV约1.05的既有小幅调整，没有继续放大眼睛。`Neutral_Lip_Closure` 是单独可关闭的加法纠正层；数据带基体顶点数、拓扑哈希、逐顶点偏移及权重。原始基体、拓扑、UV和原生形变仍保留。

皮肤使用官方原图，通过最终形变后坐标生成解剖区域遮罩并烘焙到原UV，在遮罩内进行局部频率平滑，保护唇和眼部；没有用顶点色作为运行时肤色或整图缩放处理。派生 Albedo、遮罩和区域 Roughness 均打包在 Blend。普通 Principled/glTF PBR，无皮肤专属着色、无光照烘焙。眉毛保留原毛流，只调粗糙度，**未完成单独的眉尾重画**。

已实际读取 v016 的正面、左右45°、左右90°无遮发灰模，以及对应材质图、后脑和双耳后图；带发灰模检查了正面、侧面、45°和耳后。Godot 实际读取1080p正面/45°、900p侧面、720p后脑。结论：

- 眼睑及眼白未见新明显结构退步；闭唇比 v007 的张口及 v010 的亮缝改善。
- 肤色保留柔和变化，比 v010 的大片平色更自然；头皮和后颈短茬仍明显，局部减弱不足。
- 头发根部没有融入整体，前额有可见底层边界，侧面发束形状重复；后脑宽度/扭转突变。曲线可编辑不代表造型好看。
- Blender 颈侧/耳后仍有多边形阴影边界，灰模和材质图中均可见；原基线也有类似现象。源无自定义法线、全部面平滑，基体几何未改变；Godot 当前无阴影审图未见同样的折面。**尚未完成带阴影条件下根因隔离与修复，不能写成法线问题已解决。**

## 工具与编辑

统一入口继续是 `scripts/tools/character_workflow.py`，新实现位于 `character_head_refine.py`，旧 `character_stylized.py` 行为保留。新流水线由 `style.pipeline=head_refine_v1` 显式开启；默认基线与旧配方回归通过。

v016 源中 `Hair_Live_Curve_Locks` 为真实 Bezier 曲线，`Editable_Hair_Cross_Section` 为椭圆截面；修改曲线控制点会立即改变求值网格。截面辅助物体默认隐藏，可在 Outliner 取消隐藏进行编辑。`Hair_Volume` 为独立可编辑覆盖网格；没有使用旧发型网格或受限角色。当前曲线截面朝向不能稳定贴合后脑，是待重新设计的问题之一。

导出只在临时进程中求值并转换曲线、清理辅助对象，不重建或写回保存源。曲线网格若无UV，则只在求值/导出副本添加确定性投影UV；当前头发为不透明单色PBR，未制作方向性纹理或次级碎发。主体与发束分为两个网格，共享一个材质。

```powershell
python scripts/tools/character_workflow.py check --version v014-head-material
python scripts/tools/character_workflow.py check --version v016-head-shoulders
python scripts/tools/character_workflow.py render --version v016-head-shoulders --glb
# 手动编辑另存新版本之后，仅 export，不重跑 baseline 覆盖
# 以下复现必须用未占用的新 ID
python scripts/tools/character_workflow.py baseline --parent v014-head-material --version head-replay01 --style art/blender/characters/mpfb-male-v1/refinement/hair-integrated-r2.json
```

Godot 独立审图：

```powershell
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/CharacterBaselineReview.tscn -- --character-manifest=res://assets/generated/characters/mpfb-male-v1/v016-head-shoulders/manifest.json
```

1–8保留原视角；G切换材质/无遮发灰模/带发灰模，右键旋转可看耳后。新配方的 `reviewRearViews` 为自动捕获增加后脑及双耳后，不改旧基线的8视角行为。

## 实际验证

- 保存重开、独立配方重建、原生鼻形变及部件重贴后恢复：v001、v007、v010、v012、v014、v016 均通过；v016 几何重建及恢复最大误差为0。
- v016 闭嘴纠正开关：最大变化0.00185037米，恢复误差0；曲线控制点编辑：求值网格最大变化0.00212705米，恢复误差0。
- v012/v014/v016 的基体 Basis、多边形连接和原UV与 v007 哈希一致；v001/批准副本、v007、v010源哈希保持原值。34个采用依赖文件校验通过。
- GLB重新导入、UV/法线/引用/角色映射通过。v014为91,232三角形、8网格/材质面；v016为130,160三角形、10网格/材质面，其中新头发38,928三角形。没有套旧预算减面，也不能视为多人性能合格。
- v016 Blender GLB输出33图；Godot Compatibility输出720p/900p/1080p×11视角×3模式共99图，正式会话和存档字节均未改变。v014对应Godot72图；默认 v001 回归48图通过。
- `dotnet test ... --no-restore`：219/219，未新增业务测试，业务规则未修改。
- Godot C# Debug：0警告、0错误。
- 九场景冒烟实际出现 `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`，日志 `outputs/character-workflow/head-refine-smoke.log`。

本机显卡 RTX 2080 Ti。静态审图不是目标硬件、动态、表情、发束运动或多人预算验证。没有正式游戏导出、提交或推送。

## 失败与停止

v013 的睫毛形变恢复失败保留原源和日志，该失败配方不是当前推荐重建入口。v014 初次新增纠正开关测试误将随形变改变的四边形三角化对角线判为拓扑变化；检查已改为核对多边形连接，仍严格比较顶点数量和恢复坐标。v016 第一次建模进程因末端超出覆盖网格导致投影中断，未写入候选；补充末端贴合原头皮的处理后成功创建，失败日志保留。

**整体美术门槛未通过。** 可保留脸部阶段作为后续来源；头发需要重审主体与发束融合的建模方法，不能继续把当前椭圆条束堆密来冒充自然发型。本轮没有进行第三次头发造型调整，也没有自动进入全身、动画或正式NPC接入。
