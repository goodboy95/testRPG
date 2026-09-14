# 村庄 v2 第一批外景验收记录

日期：2026-09-14。对应[制作说明](../engineering/village-v2.md)和[外景设计](../design/village-exterior.md)。仅为第一批静态外景与独立预览验证，不是第一章剧情验收。

## 资产与工作副本

- `[已实现]` 从保存的村庄 v1 派生 v2，基础布局、v1 Blend／GLB、住宅源及 24 栋建筑体量位置保留。当前 v2 为 718,571 三角面、247 网格对象／176 独立网格、41 材质、47,062,464 字节 GLB（44.88 MiB）、172 MiB 纹理估算，低于全部预算。
- `[已实现]` Blender 5.2.0 离线建模、UV、小倒角、贴图打包、GLB 导出及临时复导检查通过。实际 GLB 检查 468 个地形顶点采样、57 个树实例、202 个保留网格的顶点／索引／UV／材质数据；源中 229 个未替换对象指纹一致。
- 新木凳与同尺寸立柱共享网格，分别保留对象和摆设根；四张公告纸、三个配送包裹、手推车与待修／待领凳可独立制作后续状态。没有章末工具箱、人物、动画或任务内容混入村庄 GLB。
- 开工前工作副本基线在本机临时目录 `village-v2-start.json`，本轮增量在 `village-v2-increment.json`。审查以该基线为准，不把用户此前的未提交内容算入本轮；无文件删除，原有角色、剧情与住宅资产没有修改。

## 实际执行的检查

| 检查 | 本轮结果与日志 |
| --- | --- |
| Core／内容／表现层测试 | 164 通过，0 失败，0 跳过；原 162＋2 项工作净空／实际资产检查。`outputs/village-v2-tests.log` |
| Godot Debug 构建 | 0 警告、0 错误。`outputs/village-v2-build.log` |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。`outputs/village-v2-smoke.log` |
| 只读资产复验 | `VILLAGE_V2_CHECK_PASS readOnly=true preserved=229`。`outputs/village-v2-asset-check.log` |
| 灰模工作区走查 | 131 项通过；五条新增路线、五处净空、碰撞、镜头、住宅往返与正式状态不变。`outputs/village-v2/graybox/results.json` |
| v2 完整图形专项 | 510 项通过，25 条路线记录；原三条回环、五条新增路线、五处净空绕行、双桥双向偏移通行。`outputs/village-v2/validation/results.json`，最终日志 `outputs/village-v2-final-probe.log`，正常退出且无错误 |
| 最终画面更新 | 90 张，三档分辨率；铺地贴合、网格复用与验收机位更新后重拍，布局与碰撞未变；资源生命周期修正后再次完整走查并重拍。`outputs/village-v2/validation/capture-refresh.json` |
| 显式 v1 回归 | 423 项通过、正常退出，最终日志无错误；`outputs/village-v2-v1-final.log`，本轮复拍进入 `outputs/village-v1-regression/`，不覆盖 09-12 原验收目录 |

完整专项还覆盖独立场景仅保留自家正门交互、暂停与失焦阻断、住宅 F／A 往返、临时灯态保留、重复转场拒绝、事件订阅只执行一次、重置释放缓存、正式会话和存档不变，以及本机角色缺失时 billboard 回退。

## 画面、路线问题与修正

1. 初次灰模中推车阻挡旧站回环；移到原路线之外后完整三条村庄路线及新接近路线通过。杂货铺长凳与右棚柱同步移出不适合的位置。
2. 初次棚顶为单面几何，遮蔽效果错误；修正为有厚度的棚布／金属构件。保留固定夏日上午光照，未通过改变全村照明掩盖问题；浅色胶合板帮助识别阴影下工具轮廓。
3. 广场局部铺地按原地形三角网格裁切，避开原地面的重叠闪烁；家具脚补足坡地落差。
4. 初次侧视截图落点与新推车相交，角色被物理推出后镜头靠近大树；只调整验收站位，最终三档画面重拍。该问题不作为原始截图“通过”保留。

最终图形采集含每档 30 张：原村庄／住宅 24 处，加办事桌、公告牌、杂货铺侧面／交货台、修理铺侧面／工作台六处。已复核三档重点地点、正常玩家高度、侧面与棚下画面，确认主要工作位置可观察；棚梁在部分仰侧方向仍会进入镜头上缘，属于实体构造，未承诺所有角度无遮挡。

- [三处带尺寸平面](../../outputs/village-v2/village-v2-first-batch-plan.png)
- [杂货铺前后对照](../../outputs/village-v2/compare-05-shop.jpg)
- [广场前后对照](../../outputs/village-v2/compare-06-plaza-tree.jpg)
- [修理铺前后对照](../../outputs/village-v2/compare-09-workshop.jpg)
- [720p 图集](../../outputs/village-v2/contact-1280x720-1.jpg)、[900p 图集](../../outputs/village-v2/contact-1600x900-1.jpg)、[1080p 图集](../../outputs/village-v2/contact-1920x1080-1.jpg)
- [广场近景](../../outputs/village-v2/validation/1920x1080/25-desk.png)、[工具台近景](../../outputs/village-v2/validation/1920x1080/30-workbench-close.png)

## 性能与日志边界

本机 NVIDIA GeForce RTX 2080 Ti，Compatibility／OpenGL 3.3。每档记录 360 个漫游渲染帧和 180 个环绕帧，结果包含帧率节拍／VSync 等待，不能当作纯 GPU 时间或目标硬件认证。

最终同机数据如下（每格为漫游平均／P95、环绕平均／P95，毫秒）：

| 分辨率 | v1 本轮复跑 | v2 最终复跑 |
| --- | --- | --- |
| 1280×720 | 16.698／16.747；16.692／16.748 | 16.698／16.753；16.694／16.750 |
| 1600×900 | 16.697／16.762；16.694／16.750 | 16.698／16.750；16.693／16.768 |
| 1920×1080 | 16.698／16.755；16.694／16.759 | 16.698／16.758；16.693／16.797 |

本轮最终 v1 加载约 1.17 秒、v2 约 1.26 秒。数据见各目录 `frame-times.json` 和 `results.json`；非受控冷启动，不根据单次加载大小宣称版本性能优劣。v1 与住宅原始资产也分别通过 `build_village_v1.py check` 和 `build_home_v1.py check`，日志保存在 `outputs/village-v2-v1-asset-check.log` 与 `outputs/village-v2-home-asset-check.log`。


首次 v2 图形运行出现 WASAPI 设备不可用提示并回退 Dummy 音频；本轮不涉及声音，后续复拍显式指定 Dummy。首次 v1 回归虽完成 423 项检查，但角色 GLB 加载曾输出 `Handle is not initialized` 的 Godot .NET GCHandle 异常，原始日志保留在 `outputs/village-v2-v1-regression.log`，第二次复跑在退出阶段还出现资源泄漏与终结器错误，日志另存 `outputs/village-v2-v1-regression-clean.log`，文件名不代表其通过。随后修正入口射线查询释放、预览期间角色 PackedScene 强引用以及验收退出前的节点释放／托管终结器等待。最终独立 v1 回归 `outputs/village-v2-v1-final.log` 423 项通过、退出码 0、无上述错误；最终 v2 也完成 510 项检查、主动 GC 后住宅往返与退出清理，退出码 0、无错误。失败日志保留，不把先前的 PASS 行当作成功退出。

文档检查：492 个本地链接有效；AGENTS.md 一次点击到 v2 制作说明，全部 45 份项目文档最多两次点击可达。文档差异检查通过，已有换行格式提示不作为失败。

## 未执行与后续

`[计划]` 负责人视觉确认、真实 Xbox／物理鼠标 raw delta、目标硬件冷启动和性能、完整人工游玩及游戏导出仍待安排。没有新增剧情、任务、NPC、森林或战斗验收；没有提交、推送或导出游戏。

`[固定约束]` 本机角色及含角色截图只限本机非商业，禁止提交、推送、分发和导出。无角色的环境源与 GLB不改变该角色限制。
