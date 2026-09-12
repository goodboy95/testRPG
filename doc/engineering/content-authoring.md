# 内容制作规范

## 总原则

先设计状态结果，再写对白和表现。每个事件必须回答：玩家如何主动进入、能做什么选择、写入哪些事实/证据/线索/关系、如何防重复、错过或失败后怎样继续。

`[固定约束]` 内容文件不能成为第二套业务逻辑。阈值、路线、结局、承诺状态机和数值截断属于 Core；JSON 和 `.dialogue` 只声明内容与调用白名单。

## ID 规则

- 只使用小写 ASCII、数字、点和下划线，例如 `main.d3.archive_diary`。
- ID 表达语义，不使用数组序号或中文。
- 发布过的 ID 不改含义、不复用；重命名时要考虑旧存档兼容。
- 建议前缀：`main.` 主线、`rel.` 关系、`resident.` 居民、`route.cautious.`、`route.rescue.`、`ending.`。
- 事实、证据、线索、记忆、承诺和 outcome 要有不同命名段，例如 `fact.`、`evidence.`、`lead.`、`memory.`、`promise.`、`outcome.`。
- 本地化键使用大写下划线；每条可显示对白必须有全局唯一静态 `[ID:...]`。

## 角色 catalog

`data/characters.json` 根对象：

```json
{
  "schemaVersion": 1,
  "characters": [
    {
      "id": "doctor_he",
      "nameKey": "CHARACTER_DOCTOR_HE_NAME",
      "roleKey": "ROLE_DOCTOR",
      "colorHex": "#AABBCC",
      "romanceCandidate": false
    }
  ]
}
```

当前强类型运行 DTO 使用 `id`、`nameKey`、`roleKey`、`colorHex` 和 `romanceCandidate`。现有 JSON 中的 `portraitId`、`canJoinParty`、`preferredGifts` 是未来内容元数据，当前解析器会忽略；若要让它们参与规则，必须先扩展 DTO 和测试，不能假设已经生效。

新增核心角色时还要同步：

1. `CharacterIds` 稳定常量及必要集合。
2. 本地化姓名和角色键。
3. 初始有方向关系。
4. 日程和场景位置映射。
5. `WorldSceneController.CharacterName` 等仍在使用的原型 fallback。
6. 立绘/行走/战斗资源与来源清单。

## 日程 catalog

`data/schedules.json` 每项字段：

| 字段 | 含义 |
| --- | --- |
| `npcId` | 必须引用现有角色。 |
| `day` | 指定内容日；省略或 `null` 表示每日兜底。 |
| `startMinute` / `endMinute` | 左闭右开分钟区间；08:00 为 480，23:00 为 1380。 |
| `locationId` | 稳定地点 ID，不等同于场景文件名。 |
| `activityKey` | 手册显示用本地化键。 |
| `requiredFacts` | 所有事实成立才可用；剧情覆盖应比兜底更具体。 |

相同 NPC、同一 `day`、相同前置事实的时间段不得重叠。增加新 `locationId` 时还要添加地图映射、NPC anchor 和手册地点文本。关键 NPC 不可见时，手册必须能从已知日程计算下一时段；关键事实不能只依赖限时会面。

## 事件 catalog

`data/events.json` 每项字段：

| 字段 | 含义 |
| --- | --- |
| `id` | 稳定事件 ID，也必须由对应对白恰好完成一次。 |
| `day` | 可选内容日条件；关键事件不应因玩家晚一天而永久消失。 |
| `locationId` | 玩家主动触发事件的地点。 |
| `startMinute` / `endMinute` | 可用窗口。 |
| `priority` | `ambient`、`relationship`、`investigation`、`dangerWarning`。 |
| `requiredFacts` / `forbiddenFacts` | 前置与排除事实。 |
| `dialoguePath` | `res://dialogue/events/*.dialogue`。 |
| `startTitle` | Dialogue Manager 标题，通常为 `start`。 |
| `displayNameKey` | 事件入口/手册显示文本。 |

危险预警优先于调查、关系和环境事件，但仍应让玩家确认。普通关系事件错过窗口后应次日重现或提供新的入口，不得永久丢失。

## Dialogue Manager 文件

`[已实现]` 插件固定为 v3.10.4，已接入 13 段序章文件；每段只有一个完成命令，在 END 后由 Core 原子提交。新增线性序章内容须通过 PrologueV6Probe 与内容测试。下列逐条命令示例仅描述八个 Legacy 文件的待迁移形态：

```text
~ start
旁白: 场景建立。 [ID:MAIN_D3_EXAMPLE_001]
- 完整描述玩家意图。 [ID:MAIN_D3_EXAMPLE_CHOICE_A]
    你: 回应。 [ID:MAIN_D3_EXAMPLE_002]
    do Narrative.Execute("add_fact", "main.fact.example")
=> result

~ result
旁白: 结果反馈。 [ID:MAIN_D3_EXAMPLE_003]
do Narrative.Execute("complete_event", "main.d3.example")
=> END
```

正式接入时应把选择结果映射成稳定 `outcomeId`，由 C# 查找预定义 mutation 并一次提交；迁移完成前不要把这个示例复制到 Day 3–7 的大量新内容。

固定要求：

- 每条打印行和选项恰好一个唯一静态 ID。
- 选项写完整意图，只对【承诺】【隐瞒】【冒险】等关键性质加标签。
- 每个跳转指向同文件存在的 title 或 `END`/`END!`。
- 每个事件必须恰好一次完成自身 `eventId`。
- 条件只能调用允许的 `Narrative.Evaluate`；修改只能调用允许的 `Narrative.Execute`。
- 插件适配完成后，事件修改必须聚合成一个 outcome 原子提交；现有逐行 `Narrative.Execute` 是待迁移内容，不是推荐的新事件接口。
- 取消或异常时不完成事件，玩家可重新进入。

### 白名单契约

| 命令 | 参数顺序 |
| --- | --- |
| `add_fact` | `factId` |
| `add_evidence` | `evidenceId` |
| `add_lead` | `leadId` |
| `timeline` | `localizationKey` |
| `change_affinity` | `sourceId`, `targetId`, `delta` |
| `change_trust` | `sourceId`, `targetId`, `delta` |
| `remember` | `sourceId`, `targetId`, `memoryId` |
| `create_promise` | `promiseId`, `promisorId`, `promiseeId`, `descriptionKey` |
| `resolve_promise` | `promiseId`, `fulfilled|explained|broken` |
| `raise_exposure` | `steps` |
| `complete_event` | `eventId` |
| `grant_xp` | `amount` |

条件参数：`has_fact(id)`、`has_lead(id)`、`event_complete(id)`、`trust_at_least(source,target,value)`、`affinity_at_least(source,target,value)`、`day_at_least(day)`。

## 本地化

- 源文件是 `localization/zh_CN.csv`，表头必须为 `keys,zh_CN`。
- 即使首版只有简体中文，代码和内容也使用本地化键。
- CSV 中逗号、引号和换行必须正确转义；禁止重复或空键。
- 修改 CSV 后让 Godot 完成重新导入，确认 `zh_CN.zh_CN.translation` 更新且中文无方块。
- 原型中 `JournalPanel` 和部分角色名仍有 C# fallback；新增正式内容时优先移入本地化表，并逐步消除硬编码展示文本。

## 事实、证据与线索

- `Facts`：已经确认，例如“审批章来自已撤销机构”。
- `Evidence`：能向第三方证明，例如告示照片、被篡改日记原件、设施记录。
- `Leads`：下一步方向，例如“核对旧隧道下方结构”。

不要为了方便同时把同一个 ID加入三处。一个选择可同时产生“事实 + 对应实物证据 + 新调查方向”，但三项必须用不同 ID 表达。

## 新事件制作清单

1. 在剧情流程中写清入口、参与者、选择、状态结果、失败回退和路线影响。
2. 分配稳定 `eventId`、`outcomeId`、事实/证据/线索/记忆 ID。
3. 添加 `events.json` 定义，确保窗口不会造成软锁。
4. 添加独立 `.dialogue` 和全部静态 ID。
5. 添加本地化键与中文文本。
6. 若涉及新 NPC/地点，补角色、日程、anchor、场景和存档地点白名单。
7. 给关键结果添加幂等/原子性或内容完整性测试。
8. 运行 Core 测试和 Godot 冒烟，再人工测试取消、重复进入、错过时段、低关系、撤退与读档。
9. 更新剧情流程的实现标签和当前实现文档。

## AI 资产流程

`[固定约束]` 所有图像从已锁定的色板、比例、视角和角色基准派生；不合格内容优先重生成，只做裁切、尺寸、透明边和色板规范化。音乐/音效可剪循环点、淡入淡出和统一响度，不制作完整语音。

每项采用资产在 provenance 中记录：

- 工具和模型版本；
- 提示词及负面约束；
- 参考图和父资产；
- 生成日期、seed 或可复现参数；
- 裁切、缩放、透明和色板后处理；
- 游戏内用途、许可证或使用限制。

固定视觉规格：UI 与 Legacy 2D 保留 640×360 逻辑坐标、32px 地块和 48px 行走角色，通过 `canvas_items` 分数缩放；3D 按实际窗口分辨率渲染。角色 8 方向移动复用 4 朝向，每向 4 帧。战斗只制作待机、技能、受击、倒地等关键动作；对话使用半身立绘和少量表情差分。

### 梦境像素图集管线

- 构图和角色生成图只作离线参考，不能直接充当最终房间背景或未经校准的整张行走表。
- 最终梦境像素资产由 `scripts/tools/build_dream_pixel_art.py build` 在原始像素尺寸绘制；`check` 模式在内存中重绘并比对尺寸、RGBA、透明边、调色板和哈希，不得改写现有文件。

### 第一室原图实验管线

- `scripts/tools/prepare_original_classroom.py build --import-source` 仅用于首次把离线参考图按原始字节纳入 `experimental/`；之后普通 `build` 从已采用源图生成 640×360 RGBA 背景、四层透明遮挡图集和哈希清单。
- 提交前执行 `py -3 scripts/tools/prepare_original_classroom.py check`。校验模式只在内存重建并比对源图尺寸、PNG 字节和清单，不改写仓库。
- 原图与派生 PNG 受仓库现有 Git LFS 规则管理；运行时不调用 Python 或生成模型。
- 共享图块使用 32px 网格；角色表为 128×192，每格 32×48、四列动作帧、四行方向（下/左/右/上）。最终 PNG 使用最近邻采样且不生成 mipmap。
- 参考图保存在 `work/dream_escape_art_refs/`，不进入导出；运行时图集与区域清单位于 `assets/generated/dream_escape/pixel/`，采用记录统一写入 `assets/generated/provenance.json`。

### 第一室 Blender/GLB 管线

- 当前管线使用 `D:\ProgramFiles\Blender\blender.exe` 5.2.0 LTS。`scripts/tools/build_classroom_3d.py` 接受兼容别名 `mvp-build`/`mvp-check`（原 `build`/`check` 仍有效）：前者从空场景重建 MVP，后者只读复验灰盒源与 GLB。
- 脚本输入采用 Godot 的 X/Y 向上/Z 逻辑坐标，并统一转换到 Blender 的 X/Y/Z 向上坐标；不要在单个物件中绕过该转换。
- 可编辑源位于 `art/blender/dream_classroom/classroom-mvp.blend`，运行时文件位于 `assets/generated/dream_escape/3d/classroom-mvp.glb`。`art/.gdignore` 阻止 Godot 导入 `.blend`，正式构建只读取 GLB。
- `.blend` 与 `.glb` 都由 Git LFS 规则管理。GLB 只保存视觉网格和状态节点；碰撞继续在 Godot 中以独立 `StaticBody3D` 定义，避免美术重导出静默改变通路。
- 必需节点、房间米制尺寸、十组课桌实例、材质数和源/运行时 SHA-256 写入 `classroom-3d-manifest.json`；缺失节点或导出不确定时校验必须失败。
- `[已实现·待视觉终验]` 精修版以 `classroom-art-pass-v2.blend` 为权威源。`build_classroom_art_pass.py art-bootstrap` 只用于首次从 MVP 建立可编辑结构；之后 Blender MCP 或人工编辑 `.blend`，用 `art-upgrade-third-person` 做幂等门/四墙近景升级，用 `art-export` 生成 GLB/manifest，用 `art-check` 只读打开源文件并导出到临时目录比对节点、贴图、预算和哈希。不要用 bootstrap 覆盖后续手工精修。
- 精修版固定含 `Shell`、`Furniture`、`Props`、`Decals`、`StateVariants`、`Lookdev` 集合；Lookdev 相机和灯光不导出。运行时使用独立的 2048² 建筑与家具 albedo/normal/ORM 图集，以及 1024² 细节和 1024×512 窗外景片；`build_classroom_art_textures.py build` 会清除已废弃的单图集产物。十组课桌必须共享一个网格数据块；四墙、门和墙挂物需在连续环绕与近景下都完整。
- `build_classroom_art_textures.py build` 从四张采用源图离线生成 2048² albedo/normal/ORM、1024² RGBA 细节和 1024×512 窗外景片，并把哈希一致的运行时镜像写入 `assets/generated/dream_escape/3d/textures-v2/`。`check` 在临时目录重建并同时比对美术源输出、运行时镜像和两份 manifest，不写仓库。
- 精确谜题文字由 Pillow 使用本机楷体确定性绘制；生成模型只参与概念图、无文字材质源与窗外景片。AI 不生成整间 3D 教室，采用项必须逐一写入 provenance。

#### v4 扩建历史基线

- `art/blender/dream_classroom/classroom-art-pass-v4.blend` 是 12×9×4 米扩建的历史运行时基线。执行 `build_classroom_art_pass_v4.py art-export` 可重建其 GLB/manifest；执行 `art-check` 时只读打开 Blend 并输出到临时目录，不得改写仓库。当前主流程已切换到下文的 v5，不能用 v4 导出覆盖 v5。
- v4 固定检查 12×9×4 米、完整天花板、18 个共享课桌根、七组状态变体、必需门枢轴、三组窗、可见网格/三角面/GLB 预算和临时导出哈希。Godot 碰撞与交互点继续独立维护。
- `build_classroom_v4_plan.py build` 生成权威俯视图和概念对照板；`check` 在临时目录重建并比较 SHA-256。该脚本只使用 Pillow，不调用生成模型。
- 第一、第二室阶段物品图标由 `build_dream_v2_item_icons.py` 确定性生成。第二室 WAV 由 `build_dream_music_audio.py` 离线确定性生成；`check` 必须验证四个 22.05 kHz PCM 文件，运行时不得合成。新增正式图标或音频时必须同时更新物品 JSON/场景引用、本地化、完整性测试和 provenance。

```powershell
$PythonExe = 'C:\Python314\python.exe'
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
& $PythonExe .\scripts\tools\build_classroom_art_textures.py check --root .
& $BlenderExe --background --python .\scripts\tools\build_classroom_3d.py -- mvp-check --root .
& $BlenderExe --background --python .\scripts\tools\build_classroom_art_pass.py -- art-check --root .
```

### 梦境洋房 Blender/GLB 管线

- `[已实现·历史内廊/当前外观源]` `art/blender/dream_manor/dream-manor-v1.blend` 是洋房 v1 室内外源。确定性平面决定尺寸、门位和门数，多角度生成图只决定材质、光照和住宅陈设；不得依据单张生成图擅自增删门或改动通路。
- v1 Blend 导出历史内廊 `dream-manor-hall-v1.glb` 与历史离线外观 `dream-manor-exterior-v1.glb`。`DreamCorridor.tscn` 当前只引用 v3 大厅；完整外观用于视觉验收和未来室外复用，不得因文件位于 `assets/generated/` 就加载到当前关卡。
- `build_dream_manor_textures.py` 确定性生成建筑、木材/金属、地毯和细节纹理；`build_dream_manor_3d.py` 负责从已保存 Blend 导出两套 GLB、manifest 和只读临时复导出检查。正式运行与构建不依赖 Blender、Python 或 Blender MCP。
- GLB只保存视觉网格、门枢轴和状态命名；玩家、墙、门及主要家具碰撞继续在 Godot 中独立定义。四个当前房间入口、一扇锁闭外门和两个无交互扩展实墙模数必须同时通过 Blend、manifest 与场景冒烟校验。
- Godot 4.6.3 的两份 `.glb.import` 固定使用 `gltf/embedded_image_handling=1`，并将嵌入的 PBR 图提取到 GLB 同目录；这些派生 PNG 与各自 `.import` 必须一并纳入版本控制。实测设为 `0` 会丢弃嵌入贴图，使材质退化为无纹理表面，不能用作节省仓库空间的方案。
- `.blend`、两套 `.glb`、概念图和大纹理遵守 Git LFS；生成参考、采用关系、提示词、哈希和后处理写入 provenance。检查模式只导出到临时目录并比较结构、纹理和哈希，不改写仓库。

```powershell
$PythonExe = 'C:\Python314\python.exe'
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
& $PythonExe .\scripts\tools\build_dream_manor_textures.py check --root .
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_dream_manor_3d.py -- manor-check --root .
```

截至 2026-08-22 的只读基线为：`DREAM_MANOR_TEXTURE_CHECK_PASS files=10 size=2048 readOnly=true`；内廊 GLB 为 160 个网格实例、79 个共享后唯一网格、14,916 个三角面、11 个材质和 21,789,048 字节，外观 GLB 为 101 个网格实例、37 个唯一网格、11,028 个三角面、7 个材质和 16,715,460 字节。两套导出均低于预算，`manor-check` 验证四个当前房门和两个预留实墙模数。

### 电影式环境完整运行时管线（教室 v5 与洋房 v2 历史）

- `[已实现·历史记录]` 负责人确认样板方向后，教室 v5 已扩展到完整 12×9×4 米房间，洋房内廊 v2 已扩展到完整可玩门厅与内廊。当时 `DreamClassroom.tscn` 引用 `classroom-art-pass-v5.glb`，`DreamCorridor.tscn` 引用 `dream-manor-hall-v2.glb`；v4/v1 保留为历史基线，不提供游戏内切换。
- 两张 2048×1152 目标概念图是布局保持型 paint-over：生成模型只决定电影式材质、色温、磨损密度和光照层次，不得改变门位、谜题目标、房间比例或交互结构。主角 v2 只作为 Lookdev 比例参考，不因此获得视觉批准。
- `build_environment_cinematic_sources.py build/check` 管理 Poly Haven 的八个 CC0 2K 材质族。原始下载、缩略图、逐文件 URL、MD5/SHA-256、色彩空间、获取日期和 CC0 许可证写入 `art/sources/cc0/polyhaven/environment-cinematic-v1/source-manifest.json`；项目派生材质位于 `art/blender/environment-cinematic-samples/textures-v1/`。
- `classroom-art-pass-v5.blend` 是保留的历史源，`dream-manor-hall-v2.blend` 是洋房保留的历史源。`expand_environment_cinematic_full.py classroom-expand/manor-expand` 只维护幂等 `CinematicFullDetails` 集合，不从方盒重建场景；`*-check` 只读验证全场景结构。不得用早期样板构建步骤覆盖这两份 Blend。
- `export_environment_cinematic_samples.py` 负责从权威 Blend 导出 GLB/manifest 和临时复导检查；洋房导出必须同时包含 `CinematicSampleDetails` 与 `CinematicFullDetails`，否则会漏掉独立护墙板。两份 schema v2 manifest 必须保持 `status=cinematic-full-runtime`、`sampleOnly=false`、`runtimeDefault=true`，并记录源/GLB 哈希、纹理来源、缺失外链、三角面、最大可见网格、材质、GLB和纹理显存预算。
- 历史教室 v5 为 114,452 三角面、178 个最大同时可见网格、14 个材质和 56,502,720 字节；洋房 v2 为 88,244 三角面、198 个可见网格、12 个材质和 37,793,392 字节。18 组课桌共享网格；洋房七个门枢轴和四段全廊护墙板必须通过 Godot 冒烟。
- 当前未建立可复验的 Compatibility `LightmapGI` 烘焙链，因此运行时使用扫描 PBR 的 AO/cavity、带阴影实时灯和每区域两个 ReflectionProbe。Lookdev 相机、灯光和静态 3D 人物参考不导出进环境 GLB；Godot 场景内灯光不改变 Core 状态。
- 本轮未购买 Fab 或 BlenderKit 内容。采购前评审记录位于 `art/concepts/environment-cinematic/commercial-candidates-v1.md`，4K 候选板由 `build_environment_commercial_candidate_board.py` 确定性生成；候选缩略图只用于私有内部评审，不属于采用资产。未来商业候选必须先提交价格、许可、三角面和纹理规格；原始商业下载包不进入 Git，项目化派生物仍需记录再分发限制。

```powershell
$PythonExe = 'C:\Python314\python.exe'
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
& $PythonExe .\scripts\tools\build_environment_cinematic_sources.py check
& $BlenderExe --background .\art\blender\dream_classroom\classroom-art-pass-v5.blend --python-exit-code 1 --python .\scripts\tools\expand_environment_cinematic_full.py -- classroom-check --root .
& $BlenderExe --background .\art\blender\dream_manor\dream-manor-hall-v2.blend --python-exit-code 1 --python .\scripts\tools\expand_environment_cinematic_full.py -- manor-check --root .
& $BlenderExe --background --factory-startup --python-exit-code 1 --python .\scripts\tools\export_environment_cinematic_samples.py -- classroom-check --root .
& $BlenderExe --background --factory-startup --python-exit-code 1 --python .\scripts\tools\export_environment_cinematic_samples.py -- manor-check --root .
& $PythonExe .\scripts\tools\build_environment_commercial_candidate_board.py --root .
```

### 主角 v2 静态候选管线

- `[已实现·静态技术检查通过，待负责人视觉确认]` `art/blender/characters/protagonist-art-pass-v2.blend` 是原画式主角 v2 的权威手工美术源；正式静态 GLB/manifest 已生成并通过只读复导出检查，但 manifest 明确为 `runtimeReady=false`。不得用 v1 的程序化重建流程覆盖；v1 技术样模继续单独保留为尺度与导出基线。
- 第一阶段只允许生产静态 Blend、静态 GLB、manifest 与验收截图。静态候选即使通过尺寸、拓扑、UV、材质、预算和确定性导出检查，也仍不代表视觉已批准，更不代表骨骼、权重、动画、`AnimationTree` 或 Godot 运行时接入已经完成。
- 采用参考固定为正式身份锚点、脸发细节板、服装细节板与比例三视图；生成图只用于建模参考，不直接投射为皮肤或服装纹理。权威来源、提示词、参考关系和哈希统一记录在 `assets/generated/provenance.json`。
- v2 静态候选的 provenance 必须使用“待负责人视觉确认、非运行时”状态，并记录最终 Blend/GLB/manifest 哈希、GLB 字节数、身高、可见三角面、网格、材质与 2048² Albedo/Normal/ORM。还必须显式记录 `bound=false`、`animated=false` 和未接入当前场景；禁止把输出文件位于 `assets/generated/` 解释为已被场景采用。

```powershell
$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_protagonist_art_pass_v2.py -- art-export --root .
& $BlenderExe --background --python-exit-code 1 --python .\scripts\tools\build_protagonist_art_pass_v2.py -- art-check --root .
```

负责人确认脸型、发型、服装轮廓、材质和方案 A 教室比例前，后续绑定/动画阶段保持阻断。完整静态验收图与预算要求见 `doc/production/testing-release.md`，资产的当前真实状态见 `art/blender/characters/README.md`。

截至 2026-08-22 的技术基线为：`PROTAGONIST_ART_V2_CHECK_PASS meshes=6 triangles=34972 height=1.7017 bytes=1532748 readOnly=true bound=false animated=false`；三张纹理均为 2048²，材质面为 1，GLB 不含 skin、animation 或 camera。该基线不得改写为视觉已批准或运行时已接入。

### 第一室 v6 当前管线

`[已实现]` 当前教室源和 GLB 分别为 `classroom-art-pass-v6.blend` / `classroom-art-pass-v6.glb`。使用 `scripts/tools/remodel_classroom_v6.py build/export/check`；该脚本保留 v5，维护新源、共享桌阵、状态根、布局约束和 provenance。旧 `expand_environment_cinematic_full.py classroom-expand` 与电影式 exporter 的默认 classroom 模式只服务历史 v5，不得用于覆盖 v6。完整步骤及状态见[第一室建模与布局 v6](classroom-remodel-v6.md)。

### 洋房大厅 v3 当前管线

`[已实现]` 当前权威源为 `dream-manor-hall-v3.blend`，运行时为同名 GLB。用 `remodel_manor_v3.py check --root .` 只读复验；`build` 会重建 v3，不能覆盖后续手工修改。v1/v2 及旧外观保留历史用途，旧外观尺寸不代表当前内部。布局读取 `MansionLayout3D`，禁止分别调整模型、门位和碰撞。实际 UV、贴图内存、复导哈希与 provenance 见[大厅 v3](manor-remodel-v3.md)。

## 解谜题面与角色帮助

`[已实现]` 当前序章遵循[角色提示规则](puzzle-feedback.md)。新谜题必须遵循：题面只描述装置和操作；完整答案只留在制作/回归文档；原始纸面、图形、合作观察须足以解题。不要将解法搬进道具不足提示或机关文字替代。

主角失败独白只能引用实际已知材料，部分收集时也可以回想已见部分；无材料时表达疑惑。每个适用情境至少写两句，按对象变换句式，不用统一模板反复替换名词。安装、消耗后仍能证明曾取得的材料需要同步 `DreamThoughtRules`。加入后的主动求助保持三级自然对话，最后一级只给局部、试探建议，三个级别合计仍留出推理空间。技术错误不伪装成角色台词。

新增文案需检查本地化、材料证明、失败不改变状态及同次操作单条反馈，并逐题人工审核剧透边界。机关不再提供“关联资料”；独立记录、现场检视、必要合作观察和图形文字替代仍必须可用。

## 音乐室 v2 环境资产

`[已实现]` 当前使用 `music-room-art-pass-v2.blend/glb`，制作脚本读取 `MusicRoomLayout3D.cs`。新增/移动机关需同步共享布局、状态根、纸面 Label3D、manifest/provenance 和实际 GLB 检查。日常用 `remodel_music_v2.py check` 只读验证；不要运行 `graybox` 覆写细模。复用现有三套 CC0 材质、四个离线 WAV，环境名称调整不改变谜题答案。见[制作说明](music-remodel-v2.md)。

### 双角色动作 v3（本机限定）

`[已实现]` 流萤 PMX 是晓雨本机外观源；受限原件保留，独立制作源、12 段动作、GLB 和脚部复导审计在 `outputs/character-motion-v3/`。禁止把受限资产写入正式可分发角色目录，也不得通过旧 v2 动作构建器覆盖新版镜像。详细流程、来源、回退与限制见[双角色动作](character-motion.md)。
