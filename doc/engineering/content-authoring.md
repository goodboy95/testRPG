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

`[计划]` 插件固定为 v3.10.4。现有八个文件使用下列逐条调用形态，它便于验证对白内容，但尚未提供整事件原子性：

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

固定视觉规格：640×360 内部分辨率、32px 地块、48px 行走角色；8 方向移动复用 4 朝向，每向 4 帧。战斗只制作待机、技能、受击、倒地等关键动作；对话使用半身立绘和少量表情差分。
