# 村庄 v6 正式采用与提交

2026-09-15，负责人确认现有外观并授权正式启用、Git 提交。此前 v5／v6 的“独立样板、暂不提交”是制作阶段边界，由此次授权替代。

## 当前入口

正式梦境醒来、住宅／村庄读档和默认 `VillagePreview.tscn` 使用 village-v6 与 home-identity-v2，包括四户 v5、主角家暖色外观和全村 58 棵树。双向推门、门方向保存及院外接坡修复保留。使用当前 v4 紧凑布局与已修复 Terrain3D；第一章委托仍未接入。

正式模式忽略预览回归参数。独立 `VillagePreview.tscn -- --village-v4` 或 `VillageCompactPreview.tscn` 可回归 v4；v1／v2／v3／v5 与 v6 样板入口继续保留。无新存档字段、内容版本或交互。

本轮只切换资源选择与对应住宅、摆设碰撞，更新导出元数据及文档，不重建 Blender、GLB 或地形。源与编辑方法见[v6 制作](../engineering/village-home-trees-v6.md)；[样板验收](village-home-trees-v6-validation.md)保留当轮预算、性能和图形记录，不冒充本轮正式入口验证。

## 本轮验证

Core／内容测试 198 通过，0 失败、0 跳过；Debug 构建零警告、零错误。测试数不变，更新已有导出元数据断言，并在引擎住宅专项增加正式村庄与住宅版本断言。

正式住宅专项通过 121 项检查，包含正式 v6／配套住宅选择断言、隔离卧室读档、双向推门、穿过院门到院外低处、村路返回卧室、门灯恢复及院门中央／左右线。实际输入连续行走，不靠测试瞬移跨过院外阻挡。见[本轮住宅日志](../../outputs/village-v6-adoption/home.log)。

默认 `VillagePreview.tscn` 通过 751 项检查、39 条路线记录，包含默认版本断言、三条既有路线、住宅院门、工作净空、桥头、恢复、住宅往返和 billboard 回退。沿用布局专项工具，所以日志结尾仍为 `VILLAGE_V4_PROBE_PASS`，测试实际加载的是默认 v6。见[本轮路线日志](../../outputs/village-v6-adoption/routes.log)。

[九场景冒烟](../../outputs/village-v6-adoption/smoke.log)通过：`GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。三个引擎进程均退出 0，无 ERROR／FAIL；保留 Terrain3D 既有插值弃用提示。没有重跑完整序章通关、重新建模或重复样板性能对比。

真实玩家存档 SHA-256 [保持一致](../../outputs/village-v6-adoption/save-check.json)。[本轮编译](../../outputs/village-v6-adoption/build.log)与[全部测试](../../outputs/village-v6-adoption/tests.log)独立保存；样板轮日志未覆盖。

## Git 范围

提交包含此前未入库的村庄 v1～v6、住宅源与运行资源、CC0 来源、Terrain3D 数据、布局和门通行修复、对应代码、测试及文档。大文件按 LFS 管理。真实存档、outputs／work、mc_model、流萤源与两个本机角色运行镜像不纳入提交；不推送、不导出游戏。

暂存范围为 650 个文件，其中 310 个二进制文件校验为 LFS 指针；[逐项清单](../../outputs/village-v6-adoption/staged-review.json)未发现受限路径。补齐新地形、原始素材和笔刷的 LFS 规则；上游 SVG 与原始许可网页保留原字节，仅为这些文件关闭空白格式告警。其余文件的 `git diff --cached --check` 通过。

历史验收链接到本机 outputs 的媒体和日志，不随 Git 分发；文档中的实际结果摘要保留。负责人此次确认作为正式采用授权，不延伸为真实 Xbox／raw delta、目标硬件或商业导出认证。
