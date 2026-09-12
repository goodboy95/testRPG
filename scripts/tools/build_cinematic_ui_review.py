"""Build an offline review of original Godot screenshots; never alters image pixels."""
from pathlib import Path
import json
import struct

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/ui-cinematic"
STATES = {
    "01-exploration": "探索",
    "02-interaction-prompt": "靠近线索",
    "03-inventory-empty": "空背包",
    "04-inventory": "背包",
    "05-inventory-last-row": "背包末行焦点",
    "06-records-empty": "空记录",
    "07-records-long": "长记录",
    "08-dialogue-bright": "窗前字幕",
    "09-dialogue-tools": "对话工具",
    "10-dialogue-history": "对白历史",
    "11-dialogue-two-lines": "两行字幕",
    "12-dialogue-choice": "对白选择",
    "13-dialogue-no-background": "无背景字幕",
    "14-dialogue-high-contrast": "高对比字幕",
    "15-dialogue-music": "音乐教室字幕",
    "16-settings-preview": "设置即时预览",
    "17-dialogue-low-light": "低光压力场景",
}


def png_size(path):
    with path.open("rb") as stream:
        header = stream.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Not a PNG: {path}")
    return struct.unpack(">II", header[16:24])


def main():
    configs = [f"{height}-{scale}" for height in (720, 900, 1080) for scale in (100, 125, 150, 200)]
    configs += ["1080-menu100-sub200", "1080-menu200-sub100"]
    audit = []
    for config in configs:
        height = int(config.split("-")[0])
        for state in STATES:
            path = OUT / "matrix" / config / f"{state}.png"
            dimensions = png_size(path)
            if dimensions != (height * 16 // 9, height):
                raise ValueError(f"Wrong resolution: {path}: {dimensions}")
            audit.append({"file": path.relative_to(OUT).as_posix(), "size": dimensions})
        layout = json.loads((OUT / "matrix" / config / "layout.json").read_text(encoding="utf-8"))
        if {entry["name"] for entry in layout} != set(STATES):
            raise ValueError(f"Missing layout states: {config}")
    for version in ("before", "after"):
        for state in ("dialogue", "exploration", "inventory", "records"):
            path = OUT / version / "1080-100" / f"{state}.png"
            if png_size(path) != (1920, 1080):
                raise ValueError(f"Wrong comparison resolution: {path}")
    manifest = {"matrixImages": len(audit), "comparisonImages": 8, "configurations": configs, "images": audit}
    (OUT / "audit.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    html = TEMPLATE.replace("__CONFIGS__", json.dumps(configs)).replace("__STATES__", json.dumps(STATES, ensure_ascii=False))
    (OUT / "review.html").write_text(html, encoding="utf-8")
    print(f"UI_REVIEW_PASS matrix={len(audit)} comparison=8 configurations={len(configs)}")


TEMPLATE = r'''<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>雾山旧轨 · UI 样板</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#101514;color:#f1f0ec;font:15px/1.6 "Microsoft YaHei",sans-serif}
header,main,footer{max-width:1600px;margin:auto;padding:24px 32px}h1{font-size:26px;font-weight:500;margin:0 0 8px}
p{color:#b7beba;margin:4px 0 16px}nav,.controls{display:flex;align-items:center;gap:12px;flex-wrap:wrap;margin:18px 0}
button,select{font:inherit;color:inherit;background:#20282b;border:1px solid #515b57;border-radius:3px;padding:8px 14px;cursor:pointer}
button[aria-pressed=true]{border-bottom:3px solid #c99770}button:focus-visible,select:focus-visible,a:focus-visible{outline:2px solid #f1f0ec;outline-offset:3px}
label{display:flex;align-items:center;gap:9px}input[type=range]{width:190px;accent-color:#c99770}
a{color:#d8b593}.view{overflow:auto;background:#060a08}.compare{position:relative;width:100%;aspect-ratio:16/9}
.compare img{display:block;width:100%;height:100%}.compare .old{position:absolute;inset:0;clip-path:inset(0 50% 0 0)}
.split{position:absolute;left:50%;top:0;bottom:0;border-left:2px solid #fff;pointer-events:none}
.tag{position:absolute;top:14px;padding:4px 12px;background:#101514df;font-size:13px}.left{left:14px}.right{right:14px}
.raw .compare{width:1920px}.matrix-image{display:block;width:100%;height:auto}.raw .matrix-image{width:auto;max-width:none}
section[hidden]{display:none}footer{color:#8e9993;font-size:13px}main{padding-top:0}.status{font-size:13px;color:#d8b593}
</style>
<header><h1>雾山旧轨 · 电影感 UI 样板</h1><p>下方居中的字幕，更轻的探索提示，更紧凑的背包与记录。第一阶段实机图，等待视觉确认。</p>
<nav aria-label="查看方式"><button id="compareTab" aria-pressed="true">四类前后对照</button><button id="matrixTab" aria-pressed="false">字号与状态</button></nav></header>
<main><section id="comparison"><div class="controls">
<label>页面<select id="page"><option value="dialogue">对白</option><option value="exploration">探索</option><option value="inventory">背包</option><option value="records">记录</option></select></label>
<button id="oldOnly">只看调整前</button><button id="newOnly">只看现在</button>
<label>对照分界<input id="divider" type="range" min="0" max="100" value="50"></label>
</div><p>相同的 1080p、100% 字号测试状态。拖动分界线对照，或单独看现在的界面。</p>
<div class="view" id="compareView"><div class="compare"><img id="newImage" alt="当前界面"><img class="old" id="oldImage" alt="调整前的界面"><div class="split" id="split"></div><span class="tag left">调整前</span><span class="tag right">现在</span></div></div></section>
<section id="matrix" hidden><div class="controls"><label>分辨率 / 字号<select id="config"></select></label><label>状态<select id="state"></select></label></div>
<p id="sampleNote"></p><div class="view" id="matrixView"><img class="matrix-image" id="matrixImage" alt="当前实机界面"></div></section>
<div class="controls"><label><input id="natural" type="checkbox">原始像素查看（超出窗口时滚动）</label><a id="original" target="_blank" href="#">打开当前原图</a></div>
<p class="status">默认：1080p 菜单正文 24px · 字幕 27px。页面自适应缩小时文字也会变小，可切换原始像素查看。</p>
</main><footer>238 张矩阵图 / 14 种字号与分辨率组合。低光页为临时压力场景；其余世界图沿用正式场景。设置页外观待下一阶段统一。含本机角色的图片仅限本机非商业评审，不可分发或导出。</footer>
<script>
const configs=__CONFIGS__,states=__STATES__,byId=id=>document.getElementById(id);
const page=byId('page'),divider=byId('divider'),config=byId('config'),state=byId('state');let mode='comparison';
for(const value of configs){const option=new Option(value.includes('menu')?value.replace('1080-','1080p · ').replace('menu','菜单 ').replace('-sub','% · 字幕 ')+'%':value.replace('-','p · 菜单/字幕 ')+'%',value);config.add(option)}config.value='1080-100';
for(const [key,label] of Object.entries(states))state.add(new Option(label,key));state.value='08-dialogue-bright';
function redraw(){byId('newImage').src='after/1080-100/'+page.value+'.png';byId('oldImage').src='before/1080-100/'+page.value+'.png';byId('oldImage').style.clipPath='inset(0 '+(100-divider.value)+'% 0 0)';byId('split').style.left=divider.value+'%';byId('matrixImage').src='matrix/'+config.value+'/'+state.value+'.png';byId('original').href=mode==='comparison'?byId('newImage').src:byId('matrixImage').src;byId('sampleNote').textContent=state.value==='17-dialogue-low-light'?'临时降低场景照明，用于检视暗处字幕；未修改场景资产。':state.value==='16-settings-preview'?'菜单与字幕独立调节，预览使用正式字幕组件；设置页整体外观留待下一阶段。':'当前游戏组件的实机状态。背包与记录为便于评审而填充的测试进度。'}
function show(next){mode=next;byId('comparison').hidden=next!=='comparison';byId('matrix').hidden=next!=='matrix';byId('compareTab').setAttribute('aria-pressed',String(next==='comparison'));byId('matrixTab').setAttribute('aria-pressed',String(next==='matrix'));redraw()}
byId('compareTab').onclick=()=>show('comparison');byId('matrixTab').onclick=()=>show('matrix');byId('oldOnly').onclick=()=>{divider.value=100;redraw()};byId('newOnly').onclick=()=>{divider.value=0;redraw()};
page.onchange=config.onchange=state.onchange=divider.oninput=redraw;byId('natural').onchange=e=>{byId('compareView').classList.toggle('raw',e.target.checked);byId('matrixView').classList.toggle('raw',e.target.checked)};redraw();
</script></html>'''

if __name__ == "__main__":
    main()
