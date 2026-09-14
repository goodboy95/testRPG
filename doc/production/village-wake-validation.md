# 梦境醒来与正式村庄探索验收

> 本轮：2026-09-14。实现与启动见[接入说明](../engineering/village-wake.md)。本记录只记录本轮实际执行结果，不复用先前村庄建模或序章验收冒充本轮验证。

## 已完成检查

- Core／内容测试：174 项通过，0 失败、0 跳过；此前 164 项，本轮新增 10 项醒来、存档兼容、时间和门稳定端点测试。日志：`outputs/village-wake-tests.log`。
- Godot Debug 编译：0 警告、0 错误。日志：`outputs/village-wake-build.log`。
- 正式接入专项首轮：`VILLAGE_WAKE_PASS checks=59`，进程 exit 0，日志无错误。结果与 15 张画面（后续核对发现 1920 请求被系统窗口可用区降为 1600，不能作为原生 1080p 验收）：`outputs/village-wake/integration/20260914-071941/`；日志 `outputs/village-wake-probe.log`。
- 上述专项覆盖三轮窗口设置下的醒来、操作恢复、暂停、门灯交互与读档、室内外往返、重复转场、周六时间不推进、非法落点恢复；另覆盖实际已挣得 ExitOpened 档继续、13 个梦境事件保留、短介绍中断、旧终态迁入及 Legacy 原路由。专项使用合成输入与明确的测试状态放置，不宣称全程步行。

## 本轮回归与排查记录

九场景冒烟已通过：`GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`，exit 0，日志 `outputs/village-wake-smoke.log`。还包含角色动作、大厅往返、序章 13 段事件、UI 专项通过标记。

首轮完整输入回归在正常解谜后的最后断言提前结束：原 `Settle` 只等约半秒安静期，短于新增淡出／场景加载／淡入，出现 `Escape wake missing`。当时 Core 已为 CompletedEscape，未等待醒来字幕完成。已将测试改为等待 WakeFade 释放及介绍完成；失败日志 `outputs/village-wake-e2e.log` 保留，不能作为通过记录。第二轮在音乐室自动导航时停住，为避免切房后指针未捕获导致镜头不能转向（`outputs/village-wake-e2e-final.log`）；测试已通过玩家现有的鼠标点击重新捕获路径恢复后再驱动鼠标运动，未修改梦境移动或谜题规则。

最终完整输入回归通过：`PROLOGUE_E2E_PASS walks=76 meters=570.86 clicks=180 reloads=6 pins=4 events=13`，见 `outputs/village-wake-e2e-verified.log`。全程通过输入操作完成四室、四处刺醒取消／确认／终态读档、正常出口到醒来和继续游戏；不注入解谜结果。首两轮失败日志保留为排查记录。

正式接入最终复验同样通过 59 项，exit 0，无错误；该轮 15 图与报告为 `outputs/village-wake/integration/20260914-074323/`，日志为 `outputs/village-wake-probe-final.log`。醒来字幕期间隐藏底部探索提示。独立预览回归通过 420 项检查，exit 0；原有三条路线、新增地点通路与净空、两桥进出、住宅往返、暂停重置及 billboard 回退均通过。日志 `outputs/village-wake-preview.log`，本轮结果另存 `outputs/village-wake/preview-results.json`；已恢复此前 `outputs/village-v2/validation/results.json` 的原始字节，保留建模轮验收。

图像尺寸审计发现：上述两轮 `1920-*.png` 实际为 1600×900（现有窗口适配规则按屏幕可用区域降档）。已在专项中为 1080p 使用正式无边框全屏设置，并为每张图增加实际宽高断言。最终尺寸复验通过：`VILLAGE_WAKE_PASS checks=74`，exit 0，无错误；包含原有 59 项及 15 项实际图像尺寸断言。最终采用 `outputs/village-wake/integration/20260914-074807/` 的 15 张画面与报告，日志 `outputs/village-wake-probe-dimensions.log`。实际尺寸为 1280×720、1600×900、1920×1080 各五张；前两档窗口模式，1080p 为正式无边框全屏。旧图仅保留排查用途。

已由代理查看截图中的床边站位、醒来字幕、住宅出口及暂停菜单，没有发现新增字幕遮挡或失焦菜单问题；这不替代负责人视觉确认。

本地文档检查：527 个链接有效、无错误，AGENTS 到接入文档一次点击、文档最远两次点击。`git diff --check` 通过，仅原有换行转换提醒。工作副本 SHA256 对照未发现 Blender／GLB／建模布局变化，已有角色与资产修改未被覆盖。

## 边界

模型、GLB、建模布局与资产来源未修改；本轮没有建模导出或游戏导出。正式存档在测试中使用显式 `--prologue-e2e` 隔离到 `outputs/`，验证正式存档及备份、临时文件不变。

负责人视觉评价：待确认。本轮自动画面和输入测试不替代真实 Xbox／raw delta、目标硬件、听音、盲测或人工视觉终验。本机角色与包含角色的截图仍禁止提交、分发及导出。没有提交或推送。
