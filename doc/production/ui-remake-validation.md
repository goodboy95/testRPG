# 2026-09-08 UI 重制验收

`[已实现]` Godot UI 与 [Figma 设计](https://www.figma.com/design/lHbRcXTLrfe76nZzKtqR8R)已交付。范围包括全部现有页面、Legacy 手册/战斗、机关直接操作、关联资料与大字号双页；规则、答案、物品链、剧情结果、内容版本和存档字段不变。

## 验证结果

| 验证 | 结果与证据 |
| --- | --- |
| 单元测试 | 126/126，0 失败/跳过。原 125 项＋1 项 UI 本地化引用检查。[日志](../../outputs/ui-remake/unit-tests.log) |
| Godot Debug 编译 | 0 警告、0 错误。[日志](../../outputs/ui-remake/build.log) |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。[日志](../../outputs/ui-remake/smoke.log) |
| 对白与剧情边界 | 21 份对白导入、13 段序章完整遍历、取消重试、结尾原子提交；不写入现实事实/证据/线索。 |
| 输入与编辑 | `UI_REMAKE_PASS`：三档字号、Enter/A、方向键/B、原生鼠标拖放及 Esc 取消、非法来源拒绝、资料编辑值/焦点恢复、撤销、重复规则、明确提交。 |
| 视觉矩阵 | 32 页面/状态 × 3 分辨率 × 3 字号＝288 PNG；保存前断言实际像素尺寸，关键框和操作栏边界检查无越界。[审计](../../outputs/ui-remake/visual-audit.json) |
| Figma | 五部分，24 个完整页面，900p/1080p、150% 双页、空/错误/禁用/失败状态与资料往返原型；可编辑文字、矢量和组件。[清单](../../outputs/ui-remake/figma-manifest.json) |

运行环境：Windows，Godot 4.6.3 Mono，Compatibility/OpenGL，RTX 2080 Ti；捕获采用 Dummy 音频。本机 1080p 使用原生无边框全屏，避免工作区将窗口尺寸降到 900p。完整矩阵日志为 `outputs/ui-remake/capture-*.log`；空背包和确认框最后调整后的定向复查为 `recheck-*.log`。

定向复查期间，900p／125% 的一次进程运行出现 Mono `StyleBoxFlat` 资源终结异常（`0xC000001D`）。同组合单独重跑及随后四个组合均正常退出；最终九个定向日志均有 `UI_UX_CAPTURE_PASS files=2`。根因尚未定位，不能据此宣称异常已修复；长时间人工运行时需留意是否复现。可运行 `python outputs/ui-remake/refresh-visual-audit.py` 重建总览与尺寸/关键容器边界审计。

## 页面与对照

包含标题、命名及错误、探索、字幕、选择、背包空/满、记录空/满、晓雨、窗扣、时钟、值日表、纸带及资料侧栏、合影无层/叠合、舞台正/侧、音板、声学墙、暂停、三组设置、阅读、确认、完成、Legacy 手册/战斗/重试。

- [标题前后](../../outputs/ui-remake/compare-title.png)
- [背包前后](../../outputs/ui-remake/compare-inventory.png)
- [时钟前后](../../outputs/ui-remake/compare-mechanism.png)
- [720p / 100% 总览](../../outputs/ui-remake/contact-720-100.jpg)
- [720p / 125% 总览](../../outputs/ui-remake/contact-720-125.jpg)
- [720p / 150% 总览](../../outputs/ui-remake/contact-720-150.jpg)
- [1080p / 150% 总览](../../outputs/ui-remake/contact-1080-150.jpg)
- [全部矩阵原图](../../outputs/ui-remake/matrix/)

本轮修正了模态 HUD 覆盖、大字号资料抬头占满正文、舞台控件挤压、战斗标题/状态重叠、空背包重复提示、默认菜单缺少手柄映射、拖动 Esc 未取消等问题。焦点与选中分离；确认框默认取消。

## 人工边界

`[计划]` 真实 Xbox、捕获态物理鼠标 raw delta、完整四室与 Legacy 人工游玩、至少三人剧情盲测、GTX 1660 级硬件性能、真实听音与 Windows 导出仍未验收。自动输入、截图和 headless 冒烟不代替这些验收。

`[固定约束]` 本机限定 mc_model 及动作没有上传 Figma、提交或导出。含该角色的游戏截图仅作为本机非商业验收资料。当前工作副本保留用户原有未提交变更，本轮未提交或推送。
