"""Render the dated screening notes as a local HTML document; no asset download."""
from html import escape
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
DATA = HERE / 'reports/20260921-screening.json'
OUT = ROOT / 'outputs/character-workflow/mature-character-screening/review.html'


def render():
    report = json.loads(DATA.read_text(encoding='utf-8'))
    items = report['candidates']
    assert len(items) <= 8 and len({x['id'] for x in items}) == len(items)
    watch = [x for x in items if x['decision'] == 'paid_watchlist_not_cleared']
    assert len(watch) == 1 and not report['visualApproval']
    item = watch[0]
    e = escape

    def figure(filename, caption):
        url = e(item['imagePrefix'] + filename)
        return f'<figure><a href="{url}" target="_blank" rel="noopener"><img src="{url}" alt="{e(caption)}" loading="lazy"></a><figcaption>{e(caption)} · <a href="{url}" target="_blank" rel="noopener">原图 ↗</a></figcaption></figure>'

    gallery = ''.join(figure(x['file'], x['caption']) for x in item['reviewedImages'] if x['file'] != '5489412.webp')
    rows = ''.join(f'<tr><th scope="row"><a href="{e(x["url"])}" target="_blank" rel="noopener">{e(x["name"])} ↗</a><small>{e(x["author"])}</small></th><td>{e(x["price"])}</td><td>{e(x["reason"])}</td><td>{"仅观察／未放行" if x in watch else "淘汰"}</td></tr>' for x in items)
    detail = ''.join(f'<details><summary>{e(x["name"])}</summary><p><b>商家声明：</b>{e(x["sellerClaims"])}</p><p><b>实际读图：</b>{e(x["observed"])}</p><p><b>许可：</b>{e(x["license"])} {"<a href=" + chr(34) + e(x["licenseUrl"]) + chr(34) + ">许可原文 ↗</a>" if "licenseUrl" in x else ""}</p><p><b>本机状态：</b>{e(x["localTest"])}</p></details>' for x in items)
    page = '''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>完整角色来源初筛 · town-rpg</title><style>
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#f4f1e9;color:#232c2b;font:16px/1.8 "Microsoft YaHei",sans-serif}main{max-width:1250px;margin:auto;padding:32px 5vw 90px}a{color:#275e57;text-underline-offset:5px}a:hover{color:#a14e2d}a:focus-visible,summary:focus-visible{outline:3px solid #b95f38;outline-offset:4px}header{display:flex;gap:24px;justify-content:space-between;border-bottom:1px solid #9da9a1;padding-bottom:16px;font-size:12px;letter-spacing:2px}nav{display:flex;gap:20px;letter-spacing:0}h1,h2,h3{font-family:"Noto Serif SC","SimSun",serif;font-weight:500;line-height:1.25}h1{font-size:clamp(38px,5.5vw,68px);margin:35px 0 25px;max-width:900px}h2{font-size:32px;margin:0 0 22px}h3{font-size:24px}p{max-width:850px}.lead{font-size:19px;max-width:820px}.kicker{font:12px/1.5 monospace;letter-spacing:2px;color:#587068}.counts{display:flex;gap:55px;margin:32px 0 40px;border-block:1px solid #c4c9c0;padding:16px 0}.counts b{font:36px Georgia,serif;margin-right:8px}.counts span{font-size:13px}section{padding-top:46px;margin-top:34px;border-top:1px solid #9da9a1}.hero{display:grid;grid-template-columns:1.1fr 1fr;gap:40px;align-items:start}.hero figure{margin:0}.status{color:#8e4729;font-weight:bold;font-size:13px}.name{font-size:13px;color:#63726b}.note{border-left:3px solid #a95b36;padding-left:18px}.gallery{display:grid;grid-template-columns:1fr 1fr;gap:32px 24px}.gallery figure{margin:0}img{display:block;width:100%;height:auto;background:#e8e6df}.gallery img{aspect-ratio:1;object-fit:contain}figcaption{font-size:12px;line-height:1.6;margin-top:10px;color:#53625c}.wide{overflow-x:auto}table{width:100%;border-collapse:collapse;min-width:650px;font-size:14px}th,td{text-align:left;vertical-align:top;border-bottom:1px solid #c8cdc3;padding:16px 12px}th:first-child,td:first-child{padding-left:0}th{font-weight:500}thead{font-size:12px;color:#63726b}small{display:block;font-size:11px;font-weight:normal;margin-top:8px;color:#63726b}td:last-child{white-space:nowrap}.cost{min-width:0;max-width:750px}.cost td:last-child{white-space:normal}.pair{display:grid;grid-template-columns:1fr 1fr;gap:36px}details{border-bottom:1px solid #c8cdc3;padding:15px 0}summary{cursor:pointer;font-size:15px}details p{font-size:14px}footer{margin-top:65px;font-size:12px;color:#63726b;border-top:1px solid #9da9a1;padding-top:20px}.stage{font-size:14px}li{margin:8px 0}@media(max-width:760px){main{padding:20px 22px 55px}header{flex-direction:column;gap:8px}.hero,.pair,.gallery{grid-template-columns:1fr}.counts{gap:20px;flex-wrap:wrap}.hero{gap:20px}.counts b{font-size:28px}h2{font-size:27px}.gallery img{max-height:650px}section{padding-top:30px;margin-top:30px}}@media print{body{background:white}details{display:block}nav{display:none}.gallery{grid-template-columns:1fr 1fr}section{break-inside:avoid}}
</style></head><body><main><header><span>TOWN RPG / CHARACTER STUDY 01</span><nav><a href="#watch">观察项</a><a href="#gallery">实际展示</a><a href="#screen">八项比较</a></nav></header>
<p class="kicker">SOURCE SCREENING · 2026.09.21</p><h1>换一个完整的起点。<br>先看角色本身。</h1><p class="lead">本轮没有找到可直接进入静态试验的合格免费角色。保留一项付费观察，供判断风格；它尚未通过来源、许可和视觉门槛。</p><div class="counts"><span><b>08</b>项来源初筛</span><span><b>01</b>项付费观察</span><span><b>00</b>项模型取得 / 本机试验</span><span><b>$0</b>实际支出</span></div>
<p class="stage">筛选与读图已完成 → <b>当前：负责人审看</b> → 许可与来源核清 → 合法取得 → Blender / Godot 静态试验。后四项尚未完成。</p>
<section id="watch"><p class="kicker">01 / PAID WATCHLIST</p><div class="hero"><div>__HERO__</div><div><p class="status">仅观察 · 未购买 · 未本机验证 · 不可入库</p><h2>高领便服男性</h2><p class="name">Casual male turtle-neck cartoon semi Realistic<br>商品店铺：elegantmesh.studios / 3DExport</p><p>头发、五官和便服已经组成完整造型，较接近“动画化设计＋立体人体”。这是保留它供你看图的理由。</p><p class="note"><b>我的判断：</b>上眼睑黑边粗重；下颌、肩胸较强壮，年龄感偏成熟。它还不等于我们想要的温和自然青年，也无法承诺简单改色就能达到目标。</p><p>商品正文出现另一来源名称 Design Connected collection，归属关系待核实。未确认修改权与全部交付条件，暂不建议购买。</p><p><a href="__SOURCE__" target="_blank" rel="noopener">打开商品与完整图集 ↗</a>　<a href="__LICENSE__" target="_blank" rel="noopener">许可原文 ↗</a></p></div></div></section>
<section id="gallery"><p class="kicker">02 / SELLER'S MODEL EVIDENCE</p><h2>看过的模型展示</h2><p>以下全部由商品图集在线引用，属于商家展示，不是本机 Blender 或 Godot 渲染。光照和姿态不统一，不构成同条件技术比较。点击可查看原图；图片需要联网，失效时请打开商品页。图片未下载入项目，也未用作生成模型输入。</p><div class="gallery">__GALLERY__</div><p class="note">仍缺少已审读的中性严格侧面和穿衣背面。身体背面不能代替服装背面；表情与控制器静帧不能证明动画质量。</p></section>
<section><p class="kicker">03 / ACQUISITION &amp; CAPABILITIES</p><h2>价格明确到哪一步</h2><table class="cost"><tbody><tr><th>基础许可</th><td>USD 9；许可正文与表格对付费游戏的表述不一致，不按此价格确认商业交付。</td></tr><tr><th>扩展许可</th><td><b>USD 18</b>；实际切换选项后所见标价。许可正文明确包含商业游戏。</td></tr><tr><th>软件 / 附加资源</th><td>沿用 Blender 与 Godot，软件新增费用 $0；商品称无需插件，未声明必购附加资源。兼容性未验。</td></tr><tr><th>完整结算金额</th><td><b>未知</b>；已知小计 $18，另加可能的税费、支付费用。未结算、未购买。</td></tr></tbody></table><div class="pair"><div><h3>卖方所称能力</h3><p>Blend / FBX 等源格式、PBR、身体与面部绑定。服装与身体可否独立编辑、共享骨架能否换装，都没有本机确认。</p><p>商品标示 38,246 面；Maya 展示截图为 75,868 三角形。实际导出面数、材质面与贴图成本待测。</p></div><div><h3>入库前必须解决</h3><ul><li>修改授权、来源署名关系及 No AI Training 条款范围。</li><li>实际文件的纹理、发型结构和可编辑部件。</li><li>商业游戏打包条件；不得随项目分发独立模型。</li><li>你是否接受当前眼部设计与成熟体型。</li></ul></div></div></section>
<section id="screen"><p class="kicker">04 / EIGHT-SOURCE REVIEW</p><h2>其余来源为何没有留下</h2><p>按本轮最多八项停止初筛。免费优先，但没有以“能下载”替代造型要求；也没有为了凑三个候选保留明显不合适的角色。</p><div class="wide"><table><thead><tr><th>来源 / 作者</th><th>获取价格</th><th>判断与缺口</th><th>结果</th></tr></thead><tbody>__ROWS__</tbody></table></div><h3>逐项证据与许可记录</h3>__DETAIL__</section>
<section><p class="kicker">05 / DELIVERY STATUS</p><h2>完成筛选，尚未开始资产试验</h2><div class="pair"><div><h3>已经做过</h3><p>八项官方/商店页面与实际展示读图；许可与报价查核；独立研究记录与此比较页。Core / 内容测试 219 项通过。</p><p>MPFB 批准版和 v007 / v014 / v016 保留。正式 v6、NPC 功能试点、审图脚本和默认入口未改。</p></div><div><h3>尚未做过</h3><p>下载模型、保存重开、固定视角灰模、改色恢复、GLB 导出、Godot 三分辨率审图、动画、性能与运行时存档隔离测试。本轮没有合格免费资产，因此未进入这些环节。</p></div></div><p class="note"><b>建议：</b>先看这一个观察项的脸和体型。如果你不喜欢，就淘汰它，另批寻找；如果接受外观，再核清来源与修改许可，决定是否购买。本轮不自动采购或定制。</p></section>
<footer>资料核查日期：2026-09-21 · 商家报价可能变化 · 代理视觉判断与用户批准分别记录<br><a href="../../../doc/production/character-mature-source-screening.md">完整交接记录</a> · <a href="../../../art/blender/characters/mature-character-screening/reports/20260921-screening.json">结构化来源记录</a><br>本页为本地静态文档，无服务、端口、外部脚本或在线生成调用。远程图像版权归相应权利人。</footer></main></body></html>'''
    replacements = {'__HERO__': figure('5489412.webp', '商家头肩近景 · 已读'), '__SOURCE__': e(item['url']), '__LICENSE__': e(item['licenseUrl']), '__GALLERY__': gallery, '__ROWS__': rows, '__DETAIL__': detail}
    for key, value in replacements.items():
        page = page.replace(key, value)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(page, encoding='utf-8')
    print(OUT)


if __name__ == '__main__':
    render()
