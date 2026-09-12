# 解谜角色提示验收

日期：2026-09-09。Godot 4.6.3 Mono / .NET 8 / Compatibility，Windows，RTX 2080 Ti。当前实现规范见[角色提示](../engineering/puzzle-feedback.md)。

## 已实现与范围

- 19 个题面改为装置与操作描述；57 条晓雨求助改为自然商量，最高级只提出局部试探建议。
- 214 条情境文案覆盖加入前的已知材料回想、无头绪反应及加入后的简短反馈。同一材料在黑板/谱面、时钟/密码锁等语境下有独立说法。
- 根据实际线索、物品、安装槽和已完成步骤选择候选。没有材料不透露未知来源；部分材料可回想已见部分；失败和轮换不改变存档、求助等级或谜题结果。
- 移除机关关联资料按钮、侧栏、分页和相应焦点逻辑；独立记录、现场检视、合作观察、图形文字替代保留。器材间女孩的编号信息保留为音板面板内的现场对白，互报姓名前仍称“门后的女孩”。
- 时钟、值日表和录音机的部分安装只产生一次反馈。答案、物品消耗、剧情、存档格式与内容版本 6 不变。

## 实际验证

| 检查 | 结果 |
| --- | --- |
| Core / 内容 / 移动测试 | **134/134 通过**，128 基线新增五项情境规则、一项文案完整性检查。 |
| Godot Debug 编译 | 零警告、零错误；Core 随依赖一起编译。 |
| Godot 资源导入 | 完成，最终日志没有 ERROR / WARNING。 |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。保留四室往返、13 段对白、取消与重试、协作复位、Legacy 回归。 |
| UI 输入专项 | 三档字号，Enter/A、手柄方向/B、鼠标拖放与取消、非法拖放拒绝、撤销及明确提交通过。 |
| 实际控制器专项 | `DREAM_THOUGHT_PROBE_PASS`：从机关确认事件提交错误输入，验证姓名、轮换、只回想已知内容、状态不变；真实时钟/录音机部分安装只反馈一次；加入前后、三级求助、独立记录和全部机关无关联资料通过。 |
| 图形矩阵 | 1280×720 / 1600×900 / 1920×1080 × 100% / 125% / 150%；每组 20 张，共 **180 张**。所有图检查实际尺寸，机关动作边界断言通过；九种组合均抽查实际画面。 |

日志：[Core](../../outputs/dream-hints/core-tests.log)、[编译](../../outputs/dream-hints/build.log)、[导入](../../outputs/dream-hints/import.log)、[九场景](../../outputs/dream-hints/smoke.log)、[输入专项](../../outputs/dream-hints/ui-probe.log)、[控制器专项](../../outputs/dream-hints/probe-headless.log)。PNG 尺寸及 SHA-256 清单见[矩阵审核](../../outputs/dream-hints/matrix-audit.json)。

回归期间发现旧冒烟断言仍查找已删除的“按维护卡”题面及关联阅读器，已改为检查操作说明、无关联资料与独立线索/记录仍可用。首次导入尚未结束时启动的专项曾缺少新翻译；等待完整导入后复验通过，未增加静默回退。

## 文案审阅

已逐题检查 19 个题面与各自三条求助，覆盖窗扣、黑板、时钟、值日表、教师桌、地图柜、谱面、器材间、波形校准、纸带、声学墙、合影、档案检索、录音机、工作票、舞台、母带、交接排演和出口。

- 题面不列答案与完整推理。输入按键、拖放说明保留；原始维护卡、便签、编号和侧台条件不删减。
- 求助中的“整点”“第一位可能与窗户有关”“A 先试三档”等仅为局部建议；同题三级累计不拼成完整答案。
- 合影的文字替代改为候选位置与舞台、通道、总闸的客观空间关系，不替玩家宣布谁符合全部要求。
- 合影三级只建议排除挡在通道的候选，不用“侧台那个”等唯一位置描述变相报出单选答案。
- 文案黑名单作为回归补充，不能自动证明所有措辞都没有剧透；本轮没有据自动测试声称难度或自然感已获玩家认可。

## 截图

九组目录均含六种思考状态、未入队时的隔门现场交流、晓雨页面及滚动到求助末段的状态、独立记录、六种图形机关、舞台侧面、排列输入和波形合作观察。

隔门交流在[720p / 150%](../../outputs/dream-hints/matrix/720-150/door-observation.png)、[900p / 125%](../../outputs/dream-hints/matrix/900-125/door-observation.png)、[1080p / 100%](../../outputs/dream-hints/matrix/1080-100/door-observation.png)均可直接阅读，其他字号/分辨率组合也通过了布局检查。

| 分辨率 | 100% | 125% | 150% |
| --- | --- | --- | --- |
| 1280×720 | [独白](../../outputs/dream-hints/matrix/720-100/thought-known.png) | [值日表](../../outputs/dream-hints/matrix/720-125/roster.png) | [晓雨求助](../../outputs/dream-hints/matrix/720-150/xiaoyu-help.png) |
| 1600×900 | [题面](../../outputs/dream-hints/matrix/900-100/sequence.png) | [合作观察](../../outputs/dream-hints/matrix/900-125/calibration-observation.png) | [舞台](../../outputs/dream-hints/matrix/900-150/stage.png) |
| 1920×1080 | [独白](../../outputs/dream-hints/matrix/1080-100/thought-known.png) | [合影](../../outputs/dream-hints/matrix/1080-125/photo.png) | [独立记录](../../outputs/dream-hints/matrix/1080-150/records.png) |

复现：在根目录执行以下命令；图形矩阵依次替换宽度为 1280、1600、1920，字号为 1、1.25、1.5，并为每组指定独立输出目录。

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\TownRpg.Core.Tests\TownRpg.Core.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' build .\TownRpg.csproj --configuration Debug --no-restore
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/DreamThoughtProbe.tscn
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --path . --audio-driver Dummy --scene res://scenes/testing/DreamThoughtProbe.tscn -- --output=outputs/dream-hints/matrix/720-150 --width=1280 --scale=1.5
```

## 未验收与交付边界

专项使用隔离状态、抑制玩家存档写入，部分页面通过设置调查进度构造，不等同首次完整人工通关。真实 Xbox、raw delta、盲测、目标低端硬件及 Windows 导出仍未验收。图形捕获使用 Dummy 音频，没有真实听音验收。

至少三名剧情 RPG 玩家仍需验证：失败后有继续调查方向，不能靠连点失败获得完整答案，角色没有提前知道未见材料，语言不机械，并且去掉关联资料后仍能合理回看线索。当前不据此声称 60–90 分钟目标已达成。

本轮没有修改 Figma；09-08 的关联资料设计属于历史。截图含本机限定角色，`outputs/dream-hints/` 已加入本地 Git 排除，禁止提交、分发和导出。没有提交、推送或导出游戏。
