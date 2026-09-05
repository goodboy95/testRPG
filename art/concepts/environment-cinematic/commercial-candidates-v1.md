# 电影式环境商业素材候选 v1

状态：`[计划·采购前评审，未采用]`
调研日期：2026-08-29
范围：只评估 Fab 与 BlenderKit/Blendkit 的公开候选；未购买、未下载模型原包、未进入 Blend/GLB，也未写入 adopted-asset provenance。

> 价格会因地区、税费、促销和 Fab 许可 tier 变化。下列“当前结构化价格”与搜索索引价只记录调研时可见结果，任何购买都必须由负责人重新打开 listing 核对最终价格、许可 tier、文件清单和技术规格。

## Fab 候选

| 候选 | 预期用途 | 当前结构化价格 | 同期搜索索引价 | 公开技术规格 | 许可 | 结论 |
| --- | --- | ---: | ---: | --- | --- | --- |
| Classical 3D Assets Pack | 洋房壁灯、台灯或吊灯造型参考 | CNY 235.30 | USD 34.99 | PBR、真实尺度；listing 未披露三角面与纹理分辨率 | Fab Standard License | **有条件**：造型偏华丽，只在确认面数、贴图和可拆分性后考虑；样板壁灯仍优先自制 |
| Vintage Book Collections – 18 Books | 教室/洋房少量散书 | CNY 60.45 | USD 18.99 | 2K PBR、低模；FBX/GLB/OBJ/STL；listing 未披露三角面 | Fab Standard License | **有条件**：单价较低，但风格略偏奇幻，只有通过色调统一和节点预算检查才考虑 |
| Entrance Door 1900s – Animated | 洋房门板与五金造型参考 | CNY 53.73 | USD 5.59 | 4K、真实尺度、完整 UV；listing 未披露三角面 | Fab Standard License | **不建议**：含现代对讲机，且大门是项目核心英雄物件，必须自制 |
| Door Handles and Locks | 门锁与把手形态参考 | CNY 1008.65 | USD 149.99 | 4K PBR；LOW 网格约 5,096–80,475 vertices；listing 未披露三角面 | Fab Standard License | **不建议**：价格和网格跨度都不符合当前预算，项目五金应自制或使用更小型候选 |

### 来源记录

1. **Classical 3D Assets Pack**
   - Listing：<https://www.fab.com/listings/418f775d-99ca-401f-9263-b4996aaddae7>
   - 公开画廊预览：<https://media.fab.com/image_previews/gallery_images/272ff9af-c425-499f-b9dc-daa55ffe9b30/7e7f3f58-f760-4d54-a865-5ca77e1d6f4d.jpg>
   - 本地评审缩略图：`sconce-pack-candidate.png`
2. **Vintage Book Collections – 18 Books**
   - Listing：<https://www.fab.com/listings/73ca0109-a02e-4842-b974-db06ede1b5a0>
   - 公开画廊直链：调研会话未可靠留存；不得根据缓存文件反向猜测 URL。
   - 本地评审缩略图：`vintage-books-candidate.png`
3. **Entrance Door 1900s – Animated**
   - Listing：<https://www.fab.com/listings/42e6c7cc-72ba-48e9-a578-df053d14934b>
   - 公开画廊直链：调研会话未可靠留存；不得根据缓存文件反向猜测 URL。
   - 本地评审缩略图：`entrance-door-1900s-candidate.png`
4. **Door Handles and Locks**
   - Listing：<https://www.fab.com/listings/6886aa4e-2e59-4de2-8a90-8c08bb54b869>
   - 公开画廊直链：调研会话未可靠留存；不得根据缓存文件反向猜测 URL。
   - 本地评审缩略图：`door-handles-locks-candidate.png`

缩略图仅用于私有项目内部采购评审，版权归各 listing 卖家；它们不是已采用的游戏资产，不得单独再分发或进入运行时包。

## 许可判断

Fab Standard License 的公开摘要允许将资产用于商业项目、修改并以不可独立提取的方式纳入项目；禁止把资产本身作为独立素材转售或再分发。Personal 与 Professional tier 的权利范围相同，但具体 listing 的可选 tier 和最终条款仍须在购买前复核。权威条款：<https://www.fab.com/eula>。

商业素材原始下载包不得进入 Git；若未来获批，只保存经过项目化处理且符合许可再分发边界的派生资源，并在 `assets/generated/provenance.json` 记录资产 ID、购买主体、tier、许可证快照、原包哈希、修改历史和运行时派生物。

## BlenderKit / Blendkit

官方价格页：<https://blendkit.com/plans/pricing>

- Free：USD 0/月。
- Full：USD 9.90/月。
- Business：USD 29/月/席位。
- 官方说明资产可用于商业项目，但每项仍需检查其 Royalty Free 或 CC0 标记。
- 本轮未找到同时公开价格、三角面和纹理规格且明显优于自制方案的单项候选，因此**不订阅、不采用**。

## 当前采购结论

- 首轮建议全部不购买。
- 如果全场景扩展阶段确实缺少非核心散件，优先重新评估书本包；壁灯包仅作为第二顺位。
- 洋房大门、门锁、护墙板、壁炉、教室课桌、地图柜和谜题机关继续自制，确保年代、比例、交互状态与稳定节点完全受控。
