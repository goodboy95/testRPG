# 村庄 NPC 首批试点

`[已实现]` 2026-09-16：独立 v7 试点加入乔姨和沈砚，支持接近提示、第三人称原位交谈、两个重复话题、告别及取消。乔姨定点，沈砚在活动室东侧往返。正式村庄和默认预览仍为 v6；原 v7 无 NPC 入口保留。实际结果与限制见[验收记录](../production/village-npc-pilot-validation.md)。

`[固定约束]` 本轮负责人授权两位简易 3D 试点人物、对白和独立预览，替代以前“NPC 建模另行安排”的阶段边界。不接第一章任务、交易、奖励、关系、入队、日程或森林，不改 Core、SaveEnvelope v1、内容版本 6、旧角色 ID、布局与地形。晓雨不在现实出场。人物试点不等于正式角色美术定稿。

## 启动与内容入口

```powershell
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageNpcPreview.tscn -- --village-budget-hud --village-budget-log
```

- `data/previews/village-npcs.json`：引用既有 `aunt_qiao`、`shen_yan`，包含建筑 ID、建筑局部二维活动点、默认朝向、模型、对白和提示键。Godot 使用当前布局的 `ToWorld` 和地形定位；不复制世界布局。
- `dialogue/previews/village_npcs/`：每人一个只含文字、选项、标题与跳转的 `.dialogue`，文本 ID 同步 `localization/zh_CN.csv`。闲聊不是剧情事件，不加入 `events.json`、Legacy 日程或 `complete_event`。
- 乔姨位于杂货铺交货台侧面；沈砚三个点在活动室门廊旁，速度 1 米/秒、每点停留 3 秒。启动检查站位胶囊及沿地形分段路径；10 Hz 前向预判，实际碰撞逐物理帧处理，玩家挡路时停下，每 0.5 秒重试。

## 行为与状态归属

`VillageNpcPreviewController` 继承现有 v7 主机；预加载两套模型与对白，每帧至多实例化一个 NPC。人物是外景下的独立节点，建筑流式加载不负责它们的生命周期。住宅往返缓存同一组人物，暂停、失焦和进屋停用动作；预览重置取消对白、复位巡逻并清空临时已读记录。

`VillageNpcActor3D` 使用六骨骼低模及独立胶囊。`MoveAndSlide` 处理地面支撑，前向检测区分可行走坡面与墙／人物；不通过写位置穿过阻挡。骨骼动作叠加导入初始姿态，覆盖待机、行走、转向和交谈手势。当前只有短路线，不宣称具备全村寻路或生活日程。

`WorldArea3D.RegisterInteractionMarker/UnregisterInteractionMarker` 维护动态目标，拒绝空／重复 ID，注销当前目标时清除提示。触发时重新选择有效目标；NPC 保持 2 米范围、0.85 米高度差与上身间遮挡检查，门继续使用原可达规则。F／手柄 A 发起，双方原地转向，镜头保持；当前会话独占本地 `ModalGate` 和主机输入锁。

`DialoguePanel.Context` 是可选的实例上下文，含姓名、设置与已读集合；未提供时继续访问原 GameSession，序章和 Legacy 不迁移。试点提供独立 `DialogueContext`，没有任何正式会话或存档写入。Dialogue Manager 仍负责选项跳转；脚本文字先通过严格语法白名单，再以 `MutationBehaviour.Skip` 执行。不允许 `do`、条件表达式、插值或动态函数。一次按键释放后才展示下一节点，避免同一次输入选择下一页首项。

Esc／B 遵循对白工具／历史层级返回；对白尚未显示时也可取消。关闭、异常和节点退出释放锁。失焦停止自动推进和对白输入。未知资源或不合法配置给出开发错误，不伪装成剧情台词。历史面板同样使用上下文的本地模态锁，不占用正式会话的模态门。

## 美术源与预算

源：`art/blender/village/npc-pilot.blend`；运行时：`assets/generated/village/npc-pilot/`。两位人物均由原始程序化几何、顶点色、刚性骨骼权重构成，没有外部角色素材或 AI 图像。乔姨束发／围裙，沈砚短发／青年便装。来源、哈希、真实面数见同目录 manifest。

| 人物 | LOD0 三角形 | LOD1 三角形 | 每级材质面 |
| --- | ---: | ---: | ---: |
| 乔姨 | 1,764 | 564 | 1 |
| 沈砚 | 1,584 | 504 | 1 |

运行时两人复用同一顶点色材质。25 米内完整动作；25–55 米简化模型、10 Hz 骨骼更新、无阴影；55 米外隐藏，回到 50 米内恢复。当前对话对象强制 LOD0；外观切换不重置巡逻和对话。NPC 不加入建筑代理合批。

```powershell
# init 仅首次，存在源时拒绝；编辑保存源后用 export。
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_npc_pilot.py -- export
& D:/ProgramFiles/Blender/blender.exe --background --python-exit-code 1 --python scripts/tools/build_village_npc_pilot.py -- check
```

`check` 校验源／GLB 哈希，实际导入 GLB 检查两级网格、面数、材质面和六骨骼蒙皮，不写资产。不得重跑初始化覆盖人工修改。Godot 自动导入文件与 GLB 一同维护。

HUD／JSONL 在原 v7 预算外附加 NPC 当前启用模型面数、surface 数、更新和加载耗时。该面数是启用模型的保守成本，包含镜头外的近邻 NPC，不冒充精确遮挡后的可见面数或真实 draw calls。整帧计数仍以渲染器为准，GTX 1060 档的整帧门槛不变。

## 验证与推广

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj --no-restore
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageNpcProbe.tscn --audio-driver Dummy
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillageStreamingProbe.tscn --audio-driver Dummy -- --v7-npc
# 去掉 --v7-npc：相同完整矩阵的无 NPC v7 对照。
```

专项用真实引擎中的合成键鼠／手柄输入及物理，记录重复交谈、分支、取消、失焦、阻挡、巡逻恢复、LOD、住宅往返、正式状态与存档字节比较。完整矩阵保留原网格点、八个非直角朝向、三档分辨率及预算断言。两项性能探针单独运行，不与重型构建或其他 Godot 实例并行。

`[计划]` 负责人确认人物辨识、动作、字幕和通行后再推广其他居民；任务和正式采用另行安排。RTX 2080 Ti 结果不替代笔记本 GTX 1060 实测。现有启动／转场卡顿须与新增 NPC 成本分开。本机主角与含其画面继续禁止提交、分发或导出。

汇总命令：`python scripts/tools/summarize_village_npc.py <NPC专项/results.json> <NPC矩阵/results.json> <无NPC矩阵/results.json>`。结果包含 NPC 更新门槛、整帧对照、FPS／最低瞬时 FPS／p95 和目标设备待验收标记。
