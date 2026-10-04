# 正式村庄开场接入

`[已实现]` 2026-10-04：[事项与目标跟踪](quest-tracking-typography.md)替换长文本事项页，J／Y、任务板共用列表／详情；支持选择任务、取消跟踪、改选住户，目的地随阶段更新。历史验收数保留，新增验证见[本轮记录](../production/quest-tracking-validation.md)。

本批开发开始于 2026-10-02，最终运行复验延续至 2026-10-03。

`[已实现]` 2026-10-02，按负责人本轮正式开发授权接入。两种梦境醒来后，正式 v6 村庄支持七位功能居民、任务板、送凳、三户小件订货、邀请沈砚、共同推车配送、交差和领款。对白底稿仍统一在[第一章细案](../design/chapter-one.md)，真实检查与待验项见[本轮验收](../production/village-opening-validation.md)。

`[固定约束]` 本轮替代前一轮“只改 Markdown／Core 与存档字段不变”。Godot 4.6.3、Dialogue Manager 3.10.4、net8.0、global.json SDK 10.0.400、SaveEnvelope v1、梦境内容版本 6 保持。森林、支线、战斗、工具箱、第二章和跨日睡眠未开放；结束后继续自由探索、闲聊、查看事项和领取酬金。不提交、推送或导出游戏。

## 环境

`[已实现]` SDK 10.0.400 与 Microsoft.NETCore.App 8.0.31 位于 `C:/Users/duwei/.dotnet`，用户 PATH 与 DOTNET_ROOT 已配置，原安装保留。已启动的终端需重新打开，或在当前 PowerShell 设置下面两行。下载来源为微软发布清单，安装包 SHA-512 与清单匹配。首轮旧缓存引用已不存在的 Visual Studio 路径，用官方 NuGet 源强制恢复后得到真实基线：223 项测试、零警告编译、原九场景冒烟。

```powershell
$env:DOTNET_ROOT = 'C:/Users/duwei/.dotnet'
$env:Path = $env:DOTNET_ROOT + ';' + $env:Path
& "$env:DOTNET_ROOT/dotnet.exe" test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj
& "$env:DOTNET_ROOT/dotnet.exe" build TownRpg.csproj --no-restore
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/VillageOpeningProbe.tscn -- --village-opening-e2e
```

## 状态所有权与恢复

`[已实现]` `GameState.VillageOpening` 可选，独立于 `VillageExploration` 的位置、朝向、镜头、门灯。`VillageOpeningService` 校验接取、逐户收货、邀请、装车、交差、领款和事件确认。各事项经历 Available → Accepted → LaborDone → Reported → Paid；Godot 只请求操作，不自行累计“完成”。取消、重复或不满足前置的请求不会产生第二份货或报酬。

`[已实现]` 送凳状态区分待领、携带、地面暂放、门廊待试坐、住户验收。小件与米面使用独立的已备／已交集合，一户一份，不进入可出售的普通背包。推车另存检查、装载、绑带、逐户卸货与归还标记。语义进度保存，不保存半个劳动动作。携带木凳读档恢复在玩家附近安全地面；推车恢复上次确认停靠处，物理层再核对碰撞。进入住宅前安全放下木凳／停放推车，同行状态保留。

`[已实现]` `SaveGameSerializer` 校验新状态的枚举、集合包含关系、前置、报酬及事件一致性；未知开场状态字段拒绝读取。Core 操作失败回滚新状态、资金和完成事件；既有 `EventOutcomeService` 回滚也补齐了两个现实状态段。

`[已实现]` 旧醒后档加载时仍可缺少开场段，正式进入时初始化未接事项，保留原位置、门灯、资金与梦境结果，不重播已看完的醒来。首次升级写入前保留完整原文件为 `save-slot-1.json.before-village-opening.<UTC时间>.bak`，区别于短暂轮换的 `.bak`。备份失败即停止该次写入；保存失败显示可从暂停菜单重试的提示，已确认内存进度保留。测试只能使用显式 `--village-opening-e2e` 授权的 `outputs/` JSON 路径，不使用正式存档。

## 内容与人物

`[已实现]` `data/village-opening-npcs.json` 定义七位居民的角色标识、建筑局部站位和独立模型；`data/village-opening-goods.json` 定义三户小件的姓名、数量、本地化与归属。`VillageOpeningContent` 校验角色／模型／建筑与事项引用，不增加 Legacy 日程记录。正式事件新增 17 个，每个独立 `.dialogue`、唯一静态文本 ID、恰好一个匹配的 `complete_event`。表现层以 Skip 播放命令，完整结束后调用 Core 原子确认；取消可重播，生活问答与操作菜单不注册故事事件。

`[已实现]` 报酬集中在 `VillageOpeningIds`：送凳 20 元，小件 30 元，共同配送玩家 40 元、沈砚 40 元。交差才形成应付款，向村长领取才增加玩家余额；沈砚有独立登记和已领标记，不进入玩家的 90 元合计。不改变进入开场前的余额，不把领钱作为后续条件。

`[已实现]` 七人源为 `art/blender/village/opening-npcs.blend`，运行资源在 `assets/generated/village/opening-npcs/`。沿用原始低模试点的原创程序网格方法，七人分别制作体型、发色、衣色、围裙／帽子／口袋；每人两个 LOD、六骨刚性权重，运行时待机、步行、交谈、搬运和试坐姿态。角色称谓可见。它们是功能人物，不代表自然人物美术验收通过。

```powershell
# 已有源只 export；init 拒绝覆盖，check 只读复验。
& 'D:/ProgramFiles/Blender/blender.exe' --background --python scripts/tools/build_village_opening_npcs.py -- check
```

`[固定约束]` 不复用退役失败人物、受限主角或 MPFB 候选。原 NPC 试点独立保留。主角沿用本机许可限制和 billboard 回退；任务规则不引用主角模型。正式实例仅隐藏 v6 的静态待领凳与装饰推车，换成同用途交互物；v6 源、地形、路网和既有建筑保持。

## 操作与场景边界

`[已实现]` 任务板只列当前可接事项；J／手柄 Y 打开“事项”，列实际下一步、已收住户与应付款。上下键／左摇杆可滚动，Esc／B 返回。住宅早餐和洗手可选，既不加奖励也不拦出门。

- 木凳：指定待领凳上 F／A 拿取；速度 2.4 m/s，携带范围参加碰撞。F／A 进入放置，左右调整朝向，再确认；Esc／B 返回携带。门廊辅助落位仍须找许婆婆试坐，再回彭师傅交差。凳子留在院中，许婆婆可坐着回应。
- 订货：交货台选择货包、核对订单住户。三份能一起带走；到户再选对应包，错误提醒不扣货。收货确认和闲聊分开，重复访问不重复交付。
- 合作：两项短活都交差才可邀请。沈砚在活动室门前，可追问、暂缓；接受后跟随、避障，在装卸时移到车旁协助。被阻挡时寻找侧路，持续掉队后回到经碰撞验证的近处，避免失联。
- 推车：控制式 CharacterBody3D，前进／倒退 2.4 m/s，左右转向，镜头独立。F／A 停靠或接手；接近收货点时辅助停稳，确认卸货，归还空车后交差。没有刚体翻车、损坏、平衡条、计时或体力。失焦、暂停及对白均停止移动。

`[已实现]` 共同送货报告后是歇脚对白；取消歇脚可再找沈砚。玩家仍可探索、闲聊和领钱，不触发第一章完成、工具箱或睡眠。纸面后半章保留为后续制作。

`[计划]` 人物自然度、真实手柄、GTX 1060 帧时和完整 30–40 分钟体验另行人工验收；专项中的模拟输入与端点夹具不替代完整人工连走全路线。
