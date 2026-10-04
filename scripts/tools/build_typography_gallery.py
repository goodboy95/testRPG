"""Build a local-only index of native Godot captures; no image transformation."""
from pathlib import Path

root = Path(__file__).resolve().parents[2] / "outputs" / "typography"
root.mkdir(parents=True, exist_ok=True)
(root / "review.html").write_text("""<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><title>字号对照 · town-rpg</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#171c1e;color:#f2ede3;font:15px/1.7 'Microsoft YaHei',sans-serif}
header{position:sticky;top:0;background:#171c1ef5;padding:16px 24px;border-bottom:1px solid #424a49;z-index:1}
h1{font-size:21px;margin:0}p{margin:4px 0 12px;color:#b7c0b8}label{display:inline-block;margin-right:18px}
select{background:#242b2d;color:inherit;border:1px solid #71796f;padding:6px;font:inherit}
main{padding:20px;display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}
main.single{grid-template-columns:1fr}figure{margin:0;overflow:auto}figcaption{padding-bottom:8px}img{width:100%;display:block}
main.actual img{width:auto;max-width:none}a{color:#e4ba94}small{color:#b7c0b8}
@media(max-width:800px){main{grid-template-columns:1fr}}
</style>
<header><h1>原生 Godot 字号对照</h1>
<p>同一份内容，原版与三档思源黑体。正式默认尚未变更。选单档或打开原图，按 100% 查看实际像素；并排图会随页面缩小。</p>
<label>页面 <select id="page"></select></label>
<label>分辨率 <select id="size"><option>720</option><option>900</option><option selected>1080</option></select></label>
<label>倍率 <select id="scale"><option>100</option><option>125</option><option>150</option><option>200</option><option value="menu1-subtitle2">菜单 100 / 字幕 200</option><option value="menu2-subtitle1">菜单 200 / 字幕 100</option></select></label>
<label>显示 <select id="profile"><option value="all">四档并排</option><option value="0">原版</option><option value="1">A 紧凑</option><option value="2" selected>B 均衡</option><option value="3">C 舒适</option></select></label>
<label><input id="actual" type="checkbox">原始像素</label>
</header><main id="gallery"></main>
<script>
const pages={dialogue:'长对白',choices:'对白选项',journal:'事项列表',tracking:'探索跟踪',inventory:'背包',records:'记录',mechanism:'机关',settings:'设置'};
const names=['原版对照','A 紧凑 · 正文 8 / 对白 9 / 辅助 7 / 标题 12','B 均衡 · 正文 9 / 对白 10 / 辅助 7 / 标题 13','C 舒适 · 正文 10 / 对白 11 / 辅助 8 / 标题 14'];
const page=document.getElementById('page'),size=document.getElementById('size'),scale=document.getElementById('scale'),profile=document.getElementById('profile'),actual=document.getElementById('actual'),gallery=document.getElementById('gallery');
Object.entries(pages).forEach(([v,t])=>page.add(new Option(t,v)));page.value='journal';
function show(){const separate=scale.value.startsWith('menu');if(separate)size.value='1080';size.disabled=separate;gallery.replaceChildren();gallery.className=(profile.value==='all'?'':'single ')+(actual.checked?'actual':'');
const picks=profile.value==='all'?[0,1,2,3]:[Number(profile.value)];
for(const p of picks){const url=`${size.value}-${scale.value}/${p}-${page.value}.png`;const f=document.createElement('figure');const c=document.createElement('figcaption');c.textContent=names[p]+' · ';const a=document.createElement('a');a.href=url;a.target='_blank';a.textContent='打开原图';c.append(a);const im=document.createElement('img');im.src=url;im.alt=names[p]+' · '+pages[page.value];f.append(c,im);gallery.append(f);}}
[page,size,scale,profile,actual].forEach(el=>el.addEventListener('change',show));show();
</script></html>""", encoding="utf-8")
print(root / "review.html")
