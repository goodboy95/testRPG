# MPFB 正常人脸基线：首轮验收

`[已实现]` 后续更新：负责人已明确确认正常脸，v001 源/配方/确认原话归档于 `art/blender/characters/mpfb-male-v1/approved/v001/`。下文保留首轮待确认时的历史记录；当前 G1 正常脸已确认，新增[头发与便服样板](character-casual-preview.md)，G2–G5 仍未测试。

`[已实现]` 2026-09-20：完成 M0/M1 批处理闭环和独立引擎审图。[工程入口与复现命令](../engineering/character-workflow-mpfb.md)。本记录对应原生男性源 v001、静态导出 **v001-game3**，不对应旧沈砚候选，也不代表 NPC 游戏美术已完成。

## 门槛结论

| 项目 | 实际结果 |
| --- | --- |
| G0 批处理 | 通过：创建、保存退出、另进程重开、渲染与实际读图、原生参数修改/恢复、GLB 重导入及 Godot 显示 |
| Blender Lab MCP | 未通过：本机有 1.0.0 插件，但本会话没有可调用的 Blender 工具；未执行 MCP 对象读写，不宣称已接通 |
| G1 自动技术检查 | 通过：部件、UV、有限坐标、拓扑复现、源保护、静态格式和引用检查 |
| G1 代理视觉判断 | 建议作为正常人脸基线候选；关键结构未见明显崩坏，具体观察见下表 |
| G1 用户最终确认 | 待确认，`visualApproval=false`；尚未产生批准版本 |
| G2–G5 | 未测试；静态导入仅覆盖 G4 的一部分，不等于 G4 通过 |

## 实际环境与交付

[环境报告](../../outputs/character-workflow/environment.json)记录 Blender 5.2.0 LTS、MPFB 2.0.17、RTX 2080 Ti（驱动 591.86），子进程使用独立 Blender 用户目录。Godot 4.6.3 .NET / Compatibility 图形进程实际渲染。官方工具/包哈希和采用部件许可见[来源清单](../../art/sources/cc0/mpfb-male-v1/provenance.json)及[依赖清单](../../art/sources/cc0/mpfb-male-v1/dependencies.json)。本轮付费成本为 0；用户主动操作分钟数无法测量，记录为未知。

- [可编辑基线](../../art/blender/characters/mpfb-male-v1/candidates/v001/character.blend)、[复现配方](../../art/blender/characters/mpfb-male-v1/recipes/v001.json)、[角色规格](../../art/blender/characters/mpfb-male-v1/character_spec.md)。仅使用原生成年男性及东亚宏观参数；没有定制脸、头发网格、服装或骨架。
- [最终静态 GLB](../../assets/generated/characters/mpfb-male-v1/v001-game3/character.glb)、[导出报告](../../assets/generated/characters/mpfb-male-v1/v001-game3/manifest.json)、[独立审图场景](../../scenes/testing/CharacterBaselineReview.tscn)。默认不会替换任何运行时人物。
- 静态资源实际为 **117,192 三角形、6 网格、6 材质面、6 内嵌图像、0 骨架、0 动画**。原生身高约 1.627 米，脚底约高于原点 0.00052 米；未通过缩放或减面强行对齐旧候选规格。

源 SHA-256：`35eb232a7af37be24e57315ad3ceda58e359dd97ac109f3eecb797347eb949f4`。

最终 GLB SHA-256：`2b4299812b1eb9786033c469780614f49cf281a4ad39d7cd01723605a047cf60`。

## 读图记录

实际读取了原生源的五个头部视角及灰模，复核最终 GLB 的正面、45°、侧面和背面，以及 Godot 对应关键 1080p 视角。下列图片都是实际渲染，没有图像生成或美化；不含受限主角。48 张矩阵图已全部产生，不能据此声称每张均逐一人工审过。

| 检查部位 | 代理观察与证据 |
| --- | --- |
| 双眼/眼眶 | 正面双眼位置协调，45° 眼球处于眼眶内；修正后可见棕色虹膜，没有先前不透明外层导致的白眼。[Godot 正面](../../outputs/character-workflow/v001/godot/20260920-173248/1080-material-head-front.png) |
| 鼻翼/颧骨 | 正面及两侧 45° 鼻翼、鼻梁和面颊体积连续，未见独立粘贴或明显塌陷。[Blender 左45°](../../outputs/character-workflow/v001/blender-glb/20260920-173400/material-head-left45.png)、[Godot 右45°](../../outputs/character-workflow/v001/godot/20260920-173248/1080-material-head-right45.png) |
| 唇线/口腔 | 原生嘴部轻微张开，露出窄牙线；没有明显挤压。尚未验证闭口、眨眼、张嘴变形，不把这张静态脸当表情合格。[Blender 正面](../../outputs/character-workflow/v001/blender-glb/20260920-173400/material-head-front.png) |
| 下颌/侧面轮廓 | 额头、鼻、唇、下巴和颈部关系连续，未见极端后缩或尖锥轮廓。[Blender 左侧](../../outputs/character-workflow/v001/blender-glb/20260920-173400/material-head-left90.png)、[Godot 右侧灰模](../../outputs/character-workflow/v001/godot/20260920-173248/1080-gray-head-right90.png) |
| 耳颈连接 | 两侧耳根位置与颈部连接未见明显错位；后方补光下可辨认颈肩和躯干轮廓。[Godot 左侧](../../outputs/character-workflow/v001/godot/20260920-173248/1080-material-head-left90.png)、[Blender 背面](../../outputs/character-workflow/v001/blender-glb/20260920-173400/material-body-back.png) |
| 材质局限 | 皮肤偏通用，头皮短茬来自原生纹理，眉睫仍有模板感、睫毛较长。Godot 与 Blender 光照/高光存在差异，但关键部件与轮廓保持。上述正常脸判断不等于沈砚身份或高质量国风美术通过。 |

灰模不使用皮肤纹理/细节法线，并隐藏眉睫透明片以免其变成实心几何遮挡结构；这个显示规则同时记录于渲染报告。

最终固定视角批次：[Blender GLB 16 图及相机报告](../../outputs/character-workflow/v001/blender-glb/20260920-173400/results.json)、[Godot 三分辨率 48 图及状态报告](../../outputs/character-workflow/v001/godot/20260920-173248/results.json)。Blender 原始源固定图保留于 `outputs/character-workflow/v001/blender/20260920-171413/`。两种渲染器分别使用固定灯光，并不保证像素级颜色相同。

## 可复现性与异常处理

- v001、v002、v003 的 `check` 均实际重新创建相同配方，与重开的源比较部件、三角形索引和顶点；最大坐标差均 **0 米**，检查容差为 1e-6 米。[v001](../../outputs/character-workflow/v001/check.json)、[v002](../../outputs/character-workflow/v002/check.json)、[v003](../../outputs/character-workflow/v003/check.json)。这是几何复现，不要求 Blend 文件字节相同。
- v002 只修改原生 `nose-volume-incr.target.gz` 为 0.15，v003 恢复为 0；检查中还在重开源的内存副本上修改同一形变键并恢复，最大修改约 **0.661 毫米**、恢复误差 **0 米**。已读取[修改侧面](../../outputs/character-workflow/v002/blender/20260920-171757/gray-head-right90.png)和[恢复侧面](../../outputs/character-workflow/v003/blender/20260920-171956/gray-head-right90.png)，变化轻微，不用夸大变形证明工具能力。
- 最初恢复实现重复加载 target 会新增形变键，已改成恢复同一个键；失败日志保留，后续三个版本均通过。
- 静态材质适配曾失败：全 BLEND 造成皮肤/眉毛排序异常；不透明分层眼球造成白眼；眉毛 MASK 造成断裂。当前 v001-game3 仅使皮肤/牙齿/舌头 OPAQUE，眼球和眉睫保持原生 BLEND。旧导出留作诊断，默认入口已指向修正版，源不改。
- 再次创建 v001 被拒绝，源哈希保持；[覆盖拒绝日志](../../outputs/character-workflow/overwrite-rejection.log)。创建、检查、导出、审图均另进程执行，未覆盖基线。
- 上游基础包有跨目录的眼材质引用，现已纳入 25 项依赖及许可检查。GitHub API 限流后使用官方固定 tag；慢镜像部分下载未采用；已采用原始包做完整 SHA-256 校验。没有升级插件或改用其他生成器。

各次机器耗时、退出码与命令在 `outputs/character-workflow/logs/*.json`，异常堆栈在 `outputs/character-workflow/failures/`。用户主动操作时间未知；机器等待时间不作为人工时间。非致命警告包括 MPFB 材质字段规范化、Blender 未来版本 API 弃用与 glTF 采样器提示；headless 导入退出有资源清理提示。最终图形审图无运行错误。

## 工程回归与剩余工作

| 实际执行 | 结果/证据 |
| --- | --- |
| 工作流专项 | doctor 创建全部部件；三个配方复现/恢复；来源依赖；禁止覆盖；GLB 重导入与源哈希通过 |
| Core/内容 | **219 项通过**，沿用本轮前测试数量，没有修改 Core 或新增业务测试。[日志](../../outputs/character-workflow/tests.log) |
| Godot C# 编译 | **0 警告、0 错误**。[日志](../../outputs/character-workflow/build.log) |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。[日志](../../outputs/character-workflow/smoke.log) |
| 图形审图专项 | `CHARACTER_BASELINE_REVIEW_PASS triangles=117192 surfaces=6 images=48 stateUnchanged=true`；720p、900p、1080p，正式会话及存档字节未变。[日志](../../outputs/character-workflow/godot-review-game3.log) |

`[固定约束]` 正式 v6、低模 NPC 试点、旧沈砚审图、Core、剧情和存档格式保持现状。未提交、推送或导出游戏。

`[计划]` 下一步首先由用户确认 G1 正常脸，再选择主参考进入 A 路线定制。MCP 接通、G2 身份、G3 动态、G4 完整游戏交付、G5 量产、真实 GTX 1060 性能均尚未验收。本轮没有把透明开销、117k 面数或原生身高批准为正式 NPC 规格。
