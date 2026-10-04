# 完整角色来源初筛：2026-09-21

`[已实现]` 本轮检查八项来源，完成官方／商家真实展示读图和资料比较。**没有合格免费角色进入资产试验；一项付费观察，未通过入库门槛。** 下载模型 0，购买 0，实际支出 USD 0。模型阶段未完成，不能描述为新的三维候选已交付。

- [实际展示比较页](../../outputs/character-workflow/mature-character-screening/review.html)：商家在线图集、逐项评价、费用和未验证能力；图片需要联网。
- [结构化记录](../../art/blender/characters/mature-character-screening/reports/20260921-screening.json)：来源、许可、价格、观察与淘汰原因。
- [报告生成器](../../art/blender/characters/mature-character-screening/build_report.py)：仅生成本地 HTML，不下载图片或模型。重新生成：`python art/blender/characters/mature-character-screening/build_report.py`。

## 范围与结果

`[固定约束]` 本轮采用负责人批准的“完整角色 → 小幅定制 → 静态验证”方案，最多八项初筛、最多三个候选，免费优先、付费仅评估。MPFB 保留为对照，不再继续本轮改形。没有合格免费项时走资料交付分支，不以降低造型要求补足技术试验。

| 来源 | 查到的获取成本 | 读图及筛选结果 |
| --- | --- | --- |
| [Snow v4 / Blender Studio](https://studio.blender.org/characters/snow/v4/) | 免费，CC-BY 4.0 | 官方完整绑定角色，但头眼耳比例和发髻偏动画电影，未保留。 |
| [Stylized Young Man / ADRIANO AZEVEDO](https://www.blendkit.com/asset-gallery-detail/2906a39e-c7b1-4b89-9d05-9340c1c2ebac/) | Full Plan 付费订阅，因造型淘汰未核对订阅价 | 主图大眼、扁平五官偏年少二次元；模块化为作者声明，未保留。 |
| [Business Casual Asian Man](https://www.cgtrader.com/3d-models/character/man/business-casual-asian-man-arigged) | USD 48 页面标价 | 广告卡通式点眼／简化五官；店铺 Zudrit 与正文 ThreeDee 的署名关系未核实，未保留。 |
| [Kaito / Directed By Ernie](https://directedbyrenie.gumroad.com/l/mxvjxh) | USD 18 起，分档文字不一致，完整包价格未知 | 展示过暗，中性多视图不足，许可未明确；未保留。 |
| [AAA Realistic Asian Male / Amina Store](https://www.turbosquid.com/3d-models/aaa-3d-realistic-asian-male-character-human-rigged-character-3d-model-2010271) | USD 49 Standard，税费未知 | 已读前后人体、近景与线框，仅看到内衣；无法证实描述中的便服，未保留。 |
| [Frank / Gabriel Mensah](https://www.blendkit.com/asset-gallery-detail/048012b5-7fac-4cb6-b28e-3b2c4b291f2f/) | Free Plan | 帽子、胡须、Polo、短裤、壮实比例；脸部近景证据不足，未保留。 |
| [Andrew / Kyb3r Project](https://www.blendkit.com/asset-gallery-detail/390a2d40-740f-4224-95fb-2a3583391889/) | Free Plan | 写实兜帽摆姿，脸和头发受遮挡，绑定／拆分未知，未保留。 |
| [Casual male turtle-neck / elegantmesh.studios](https://3dexport.com/3d-model-casual-male-turtle-neck-cartoon-semi-realistic-532677) | Basic USD 9；实际选择 Extended 后 USD 18 | **仅付费观察，未放行**。造型较接近，但眼睑偏粗、体型成熟健壮；许可与上游归属未核清。 |

检索命中和商品推荐栏不算实质筛查；以上八项均打开了具体来源并查看展示。淘汰项未继续做无关的兼容性测试，不能把未说明的能力写成不具备。所有价格为本轮页面所见，不是长期报价。

## 唯一付费观察项

`[可调整草案]` 高领便服款保留的理由，是完整发型、脸部和现代服装已在同一人物上形成相对协调的设计。**建议先看图，不建议目前直接购买。** 上眼睑黑边、宽下颌和健壮肩胸仍偏离温和普通青年，不能保证小改就能达标。

已读商家封面、头肩两个角度、面部近景、表情线框、全身便服、人体背面与线框、Maya 控制器截图。没有本机渲染，没有完成严格侧脸和穿衣背面审查。人体背面不能替代服装背面；商家表情静帧不是动态测试结果。图集由比较页直接引用源站，未下载、未放进游戏资源、未用作图像生成输入。

**成本：** 扩展许可标价 USD 18；沿用现有 Blender/Godot，软件新增 USD 0。商家称无需插件，未声明必购附加资源，但兼容性未测。税费／支付费用未知，故已知小计 USD 18，最终结算总额未知；没有下单。

**许可：** [3DExport 许可正文](https://3dexport.com/basic-license-and-extended-license)明确扩展许可含商业游戏，基础许可正文与表格的付费成品范围却不一致，因此不按 USD 9 确认商业游戏成本。源模型不能独立分发；最终打包保护方式待验。修改权未在所读条款中得到明确确认，商品 No AI Training 范围也待澄清。正文出现 Design Connected collection，与店铺署名关系未知；这是待核实事项，不是侵权结论。**许可未完整明确，所以不列入项目可用资产。**

**扩展能力：** Blend/FBX、多种格式、PBR 和身体／面部绑定均为商家声明。不能确认头发类型、服装拆分、骨架兼容、表情质量或自由换装。页面 38,246 多边形与 Maya 截图 75,868 三角形分别记录，均非本机测量；实际材质面、纹理及 GLB 成本未知。

其他许可入口：[CC-BY 4.0](https://creativecommons.org/licenses/by/4.0/)、[BlenderKit 许可](https://www.blendkit.com/docs/licenses/)、[TurboSquid 许可](https://www.turbosquid.com/licensing)。免费计划与 Royalty Free 不代表源模型可再分发。没有为本轮采用任何资产，未创建下载包哈希或伪造源许可副本。

## 已执行验证与未执行项

`[已实现]` Core／内容测试本轮 **219 通过，0 失败**，数量没有新增；日志位于 `outputs/character-workflow/mature-character-screening/core-tests.log`。结构化数据、生成脚本语法、HTML 内部锚点和本地链接通过检查；七幅远程图像只检查引用结构，未据此宣称在线加载通过。定向 `git diff --check` 无空白错误，仅已有 CRLF 转换提示。

浏览器工具拒绝本地 `file:` 比较页，原因为其 URL 安全策略；未绕过。故比较页排版的浏览器目视检查未完成，商品源站逐图阅读与此页面排版检查分开记录。验证结果见输出目录 `validation.json`。

本轮只新增研究记录、报告生成器和文档索引，不改共享建模脚本、Godot 场景或运行时资源，因此不执行模型专项、Godot 编译和九场景冒烟，也不引用历史结果充当本轮结果。未启动游戏；正式会话／存档运行时隔离探针未执行。

制作前后校验批准 v001、v007、v014、v016 的 Blend，以及 `character_workflow.py`、`character_head_refine.py`、`CharacterBaselineReview.cs`、`project.godot`；**八个文件 SHA-256 全部一致**，逐文件结果保存在输出目录 `preservation-before.json` 与 `preservation-after.json`。这只证明所列文件未改变，不代替运行时隔离或全项目哈希比较。

| 阶段 | 本轮状态 |
| --- | --- |
| 来源筛查／真实展示读图 | 完成，八项 |
| 代理视觉判断 | 记录；唯一观察项也未批准 |
| 用户造型批准 | 待定 |
| 原包获取、保存重开、缺失贴图检查 | 未执行，无合格免费项／无已购项 |
| 固定相机材质／灰模、改色恢复 | 未执行 |
| GLB 导出重导入、Godot 三分辨率 | 未执行 |
| 绑定、表情、LOD、量产、正式接入 | 未执行，超出本轮 |

用户主动操作时间与完整机器耗时未测，标为未知。无模型操作失败日志，因为没有进入模型操作。

## 下一步

`[计划]` 负责人先审看比较页。若外观不接受，淘汰该观察项并另批检索；若接受，先解决来源／修改权／分发条件，再决定是否购买与取得文件。用户本轮授权仅评估付费资产，不自动购买，不擅自发送商家消息。

合法取得后再建立只读原包和来源哈希，执行原样保存重开、固定审图、GLB 与 Godot 验证。原样通过后仅作一次可恢复上衣改色。当前不提前扩展外部人物 manifest 或套用 MPFB 拓扑规则，避免没有实际样本的适配工程。

`[固定约束]` 正式 v6、低模 NPC、Core、剧情、存档、MPFB 批准资产和默认正常脸入口保持。动画、LOD 与批量居民须等新起点视觉确认。
