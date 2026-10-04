# MPFB 人物工作流：M0/M1

`[已实现]` 2026-09-20：基于 [工作流纲要](../../character_modeling_workflows_v0.1.md)搭建正常成年男性基线。源码、参数和引擎审图可复现；[实际验收](../production/character-workflow-mpfb-validation.md)分别记录技术、代理视觉和用户确认。后续负责人已确认 v001 正常脸，批准源归档在 `approved/v001/`；G2–G5 未测试。

## 环境与资源

使用本机 Blender 5.2.0 LTS、MPFB 2.0.17、Godot 4.6.3 .NET Compatibility；没有升级原工具。Blender 子进程的 `BLENDER_USER_RESOURCES` 指向项目 `work/character-workflow/blender-user`，以本地 extension repository 加载锁定 MPFB 源；不保存用户全局偏好。

本机安装的 Blender Lab MCP 1.0.0 在本会话没有可调用工具，状态为“未通过/未接通”。所有实际制作由同一 Python 模块的批处理函数完成。以后 MCP 可调用时可以导入这些函数，仍需实测一次读写/渲染闭环；当前不能称为 MCP 已实现。

已授权并取得的官方来源：

| 资源 | 版本/实际下载大小 | SHA-256 |
| --- | --- | --- |
| [MPFB tag 源包](https://codeload.github.com/makehumancommunity/mpfb2/zip/refs/tags/v2.0.17) | 2.0.17 / 44,643,182 字节 | `d08e726c798fdc4eefb02b06b6c4efe37d40b5439777e53cf96dce0e5073297d` |
| [官方基础包](https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip) | 280,737,770 字节 | `b542127a8e25547c7c29c19f2d1d2adb9a664c80396ecd694095dbc8028a0107` |

程序许可证与资产分开：MPFB 代码 GPL-3.0-or-later，不进入游戏；采用的人体及部件来源为 CC0。已复制上游许可文件和各部件版权说明，不能把此结论套用到任意社区资产。[官方资产许可说明](https://static.makehumancommunity.org/mpfb/faq/use_in_closed_source.html)与每个 `.mhclo`/`.mhmat` 的声明共同记录来源。

原始包及插件在被忽略的 `work/character-workflow`；采用的不可修改部件副本、材质依赖、版权和哈希在 `art/sources/cc0/mpfb-male-v1`。`provenance.json` 记录顶层部件，`dependencies.json` 覆盖跨目录引用（例如眼球的 brown 材质/纹理）。制作源已打包图像，可独立查看；重新生成仍需要锁定工具和原始包。

## 源与配方

权威目录 `art/blender/characters/mpfb-male-v1` 包含 `character_spec.md`、`candidates/<version>/character.blend`、`recipes/<version>.json` 和创建报告。v001 是未经身份定制的基线；v002 仅将原生 `nose-volume-incr.target.gz` 设为 0.15；v003 将配方恢复到 0。父版本和源哈希保留。

宏观设定为男性1、青年成人0.5、体重/肌肉/身高/比例0.5，以及原生东亚预设。成年年龄参数的实际含义取自插件定义，不把归一化数值解释为岁数。部件为 `high-poly` 眼球、`eyebrow001`、`eyelashes01`、`teeth_base`、`tongue01`，皮肤为 `young_asian_male`。全部使用官方配准，没有拼接或自行雕刻五官。

一层细分用于结构审查，静态导出共 117,192 三角形、6 网格、6 材质面、6 张内嵌图像。本轮不为满足游戏预算改坏脸；没有骨架、动画或 LOD，不能直接推广至多人场景。

## 命令与重建

统一入口为 `scripts/tools/character_workflow.py`。系统 Python 3.11+ 只负责启动和记录日志，几何工作由 Blender 自带 Python/Numpy 完成。每次创建/导出需要新的版本名，拒绝覆盖。换机仅通过 `--blender` 指定同版本路径。

首次换机先把上表两份原始包分别放在 `work/character-workflow/downloads/mpfb2-2.0.17.zip` 和 `system-assets-verified.zip`；脚本校验哈希后自动解压并在隔离环境加载。校验不符直接失败，不自动换版本或重新消费。

```powershell
python scripts/tools/character_workflow.py doctor
python scripts/tools/character_workflow.py check --version v001
python scripts/tools/character_workflow.py render --version v001
python scripts/tools/character_workflow.py render --version v001 --glb --export-id v001-game3
```

在已交付项目上验证重建，使用新 ID；下列操作不覆盖 v001：

```powershell
python scripts/tools/character_workflow.py baseline --version replay001
python scripts/tools/character_workflow.py check --version replay001
python scripts/tools/character_workflow.py export --version replay001
```

`baseline --parent v001 --version <新ID> --nose-volume 0.15` 为配方重放的单参数实验；改为0是恢复重放。`check` 还会在实际重开的源副本上修改原生形变键、重贴部件、再把同一个形变键归零，检查恢复误差，不能用“重新加载另一个正常文件”冒充参数恢复。

`render` 固定正面、左右45°、左右90°头部和正侧背全身，灰模/基础材质共16图；`--quick` 仅正/右45°/右90°共6图。报告记录相机和源哈希。输出位于带时间戳的 `outputs/character-workflow`，失败日志同样保留。

脚本保持上游源完整；`export` 在独立 Blender 进程的内存副本上去除辅助网格、烘焙修饰器和静态形变，不写回 Blend。导出版本可通过 `--export-id` 与源版本分开。

## Godot 导入与审图

当前静态入口 `CharacterBaselineReview.tscn` 默认读取 `v001-game3` 导出。`v001` 是未经修正的全透明导出，`v001-game` 是不正确地将分层眼球也设成不透明的诊断版本，`v001-game2` 的眉毛裁切出现断裂；均保留为失败证据，不作为当前入口。

MPFB 的 GAMEENGINE 材质最初统一导出为 BLEND，导致皮肤遮住眉睫。最终导出 JSON 按实际结构区分：皮肤/牙齿/舌头 OPAQUE；带透明外层的 high-poly 眼球、依赖柔和透明覆盖的眉睫贴片保留 BLEND。MASK 试验损坏眉毛覆盖，已放弃。纹理和几何不改，静态源材质保留。此规则仅针对已验证的这一部件集，不泛化至其他眼球资产；未来游戏优化仍需重新检查透明成本。

```powershell
dotnet build TownRpg.csproj --configuration Debug --no-restore
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --editor --import
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --audio-driver Dummy --path . --scene res://scenes/testing/CharacterBaselineReview.tscn -- --character-capture
```

不传 `--character-capture` 可交互查看：1–8切换视角，G切换灰模，右键旋转，滚轮缩放，Esc退出。支持 `--character-manifest=res://assets/generated/characters/mpfb-male-v1/<版本>/manifest.json`，只接受独立基线目录。

捕获三分辨率×8视角×2材质模式共48图。审图不实例化主角、NPC演员或村庄；核对GLB哈希、实际网格/面数，并比较正式会话与存档字节。新导出导入后关闭自动LOD和动画导入，保留固定审图几何；不能把空场景计数当作GTX1060性能验收。

## 下一阶段门槛

当前 `runtimeReady=false`、`visualApproval=false`。用户确认 v001 的正常脸基线后才保存批准记录，再选择主参考、少量候选和单问题组调整。G1不代表G2参考相似、G3表情/动作、G4完整游戏交付或G5量产通过。本轮不改正式v6、低模NPC试点、Core、剧情或存档，也不购买其他工具。

## 后续授权：静态头发与便服

负责人确认正常脸后追加穿戴审图；最终候选为 v007-casual，原生脸参数不变。使用 short02 深色短发、male_casualsuit01 衬衫/牛仔裤、shoes05 运动鞋。新素材来源分组独立于原基线清单，旧清单不改。`--casual` 创建穿戴配方；`--hair` 可选 short01/short02/short04，默认 short02；仅已验证的官方部件，不是任意模块的兼容系统。

```powershell
# 重建时选新版本，不覆盖已交付的 v007-casual
python scripts/tools/character_workflow.py baseline --parent v001 --version casual-replay01 --casual
python scripts/tools/character_workflow.py check --version casual-replay01
python scripts/tools/character_workflow.py export --version casual-replay01
python scripts/tools/character_workflow.py render --version casual-replay01 --glb
# 查看本次交付；添加 --character-capture 可重新输出三分辨率矩阵
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/CharacterBaselineReview.tscn -- --character-manifest=res://assets/generated/characters/mpfb-male-v1/v007-casual/manifest.json
```

源内保留原生遮挡组与非破坏性袜筒遮罩；原始部件、人体顶点和脸部形变不删除。配方保存发色乘数、发片细分和袜子遮挡规则。穿戴导出在副本中去除衣内被遮挡人体，眼/眉/睫保留 BLEND，头发 MASK 0.15；基线不含头发，不受该规则影响。灰模隐藏头发与眉睫片，保留服装几何。完整结果、曾失败的发型和成本见[穿戴验收](../production/character-casual-preview.md)。

## 后续授权：自然比例动画风格第2版

`[已实现·候选]` 可选 `--style` 配方支持原生形变路径/权重、独立材质与原创不透明发束，旧配方保持兼容。v008～v010 为三轮独立候选；源及工具快照、只读验证、导出肤色修正、双引擎对照和未完成的发束/口部问题见[实际验收](../production/character-stylized-v2.md)。默认审图入口及正常脸批准不变。

## 后续授权：头肩分阶段精修

`[已实现·美术未通过]` 新 `style.pipeline=head_refine_v1` 保留原生结构及旧配方行为，支持局部纠正数据、最终形变后UV遮罩和真正驱动网格的Bezier截面；导出在副本求值曲线。`check` 核对多边形连接而非随形变改变的临时三角化对角线，并验证纠正开关与曲线编辑恢复。v001/v007/v010回归通过。v014脸部改善保留，v016头发失败后停止；[交付、用法和未解决项](../production/character-head-refinement.md)。
