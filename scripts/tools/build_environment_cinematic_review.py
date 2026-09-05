"""Build deterministic review boards and the first-milestone technical report."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


FONT = Path(r"C:\Windows\Fonts\msyh.ttc")


def font(size: int) -> ImageFont.FreeTypeFont:
    if not FONT.is_file():
        raise RuntimeError(f"Required review font is missing: {FONT}")
    return ImageFont.truetype(str(FONT), size)


def fit(path: Path, size: tuple[int, int]) -> Image.Image:
    if not path.is_file():
        raise RuntimeError(f"Review input is missing: {path}")
    return ImageOps.fit(Image.open(path).convert("RGB"), size, Image.Resampling.LANCZOS)


def comparison(output: Path, title: str, entries: list[tuple[str, Path]]) -> None:
    canvas = Image.new("RGB", (3840, 1440), (10, 16, 22))
    draw = ImageDraw.Draw(canvas)
    draw.text((58, 34), title, font=font(38), fill=(235, 226, 205))
    for index, (label, path) in enumerate(entries):
        x = 38 + index * 1272
        canvas.paste(fit(path, (1248, 1120)), (x, 104))
        draw.rectangle((x, 1234, x + 1248, 1346), fill=(20, 29, 36))
        draw.text((x + 24, 1263), label, font=font(24), fill=(226, 221, 207))
    canvas.save(output, "PNG", optimize=True)


def material_board(output: Path, root: Path, manifest: dict) -> None:
    canvas = Image.new("RGB", (3840, 2160), (10, 16, 22))
    draw = ImageDraw.Draw(canvas)
    draw.text((56, 32), "电影式环境样板：Poly Haven CC0 Albedo / Normal(GL) / ARM",
              font=font(38), fill=(235, 226, 205))
    for index, asset in enumerate(manifest["assets"]):
        row, column = divmod(index, 4)
        x = 38 + column * 950
        y = 104 + row * 1010
        draw.rounded_rectangle((x, y, x + 920, y + 970), radius=18,
                               fill=(18, 27, 34), outline=(56, 73, 80), width=2)
        draw.text((x + 20, y + 18), f"{asset['name']}  ·  {asset['id']}", font=font(22), fill=(229, 220, 198))
        for map_index, map_name in enumerate(("diff", "normal", "arm")):
            map_path = root / asset["maps"][map_name]["path"]
            px = x + 18 + map_index * 300
            canvas.paste(fit(map_path, (282, 790)), (px, y + 72))
            draw.text((px + 6, y + 878), ("ALBEDO", "NORMAL(GL)", "ARM")[map_index],
                      font=font(18), fill=(190, 201, 204))
        draw.text((x + 20, y + 925), "CC0 1.0 · 来源与哈希见 source-manifest.json",
                  font=font(17), fill=(142, 161, 166))
    canvas.save(output, "PNG", optimize=True)


def report(output: Path, classroom: dict, manor: dict, source: dict) -> None:
    c = classroom["geometry"]
    m = manor["geometry"]
    lines = [
        "# 教室与洋房电影式半写实完整运行时技术报告",
        "",
        "状态：`[已实现·当前运行时]`。正式场景使用教室 v5 与洋房内廊 v2；v4/v1 保留为历史基线。",
        "",
        "| 样板 | 三角面 | 总网格 / 最大同时可见 | 材质 | GLB | 估算纹理显存 |",
        "| --- | ---: | ---: | ---: | ---: | ---: |",
        f"| 教室 v5 全室 | {c['triangleCount']} | {c['meshObjectCount']} / {c['maximumVisibleMeshCount']} | {c['materialCount']} | {classroom['runtimeGlbBytes'] / 1048576:.1f} MiB | {classroom['textureMemoryEstimateMiB']:.1f} MiB |",
        f"| 洋房 v2 全内廊 | {m['triangleCount']} | {m['meshObjectCount']} / {m['maximumVisibleMeshCount']} | {m['materialCount']} | {manor['runtimeGlbBytes'] / 1048576:.1f} MiB | {manor['textureMemoryEstimateMiB']:.1f} MiB |",
        "",
        f"- CC0 来源：Poly Haven，{len(source['assets'])} 个材质族、{sum(len(item['maps']) for item in source['assets'])} 张 2K 贴图；许可证为 {source['license']}。",
        "- 两个 Blend 均为权威手工源，缺失外链为 0；GLB 临时复导 SHA-256 与正式运行时导出一致。",
        "- Lookdev 相机、灯光和主角比例参考均未导出进环境 GLB；主角 v2 仍为静态未批准候选。",
        "- LightmapGI 未进入当前运行时：Compatibility 可显示烘焙结果，但当前仓库尚无可复验的 RenderingDevice 烘焙链；采用纹理 AO/cavity、带阴影实时灯与每区两个 ReflectionProbe。",
        "- Godot 捕获设备为 RTX 2080 Ti，仅证明 Compatibility 资源加载与画面路径可用；GTX 1660 级 1080p/60 FPS 目标尚未实机验收。",
        "- Poly Haven 原始 CC0 文件可再分发；本轮未购买 Fab 或 BlenderKit 资产，付费候选也未进入 Blend/GLB。",
        "",
        "## 视觉结论",
        "",
        "扫描材质已经替换大面积单色和重复低频程序纹理；全室黑板框、教师桌与地图柜机关、18 组共享课桌细节、门板、护墙板、壁灯、壁炉和住宅陈设均具备可读的中尺度结构。静态 3D 人物候选仍未批准，当前游戏继续使用 2D billboard；后续英雄物件精修可独立迭代而不改变碰撞或稳定 ID。",
    ]
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    root = Path(parser.parse_args().root).resolve()
    output = root / "outputs/environment-cinematic-samples"
    output.mkdir(parents=True, exist_ok=True)
    comparison(output / "review-classroom-current-target-v5.png", "教室：历史基线 / 电影式目标 / v5 完整运行时", [
        ("历史 v4 运行时", root / "outputs/classroom-v4/godot-1080p/01-view-blackboard.png"),
        ("目标概念", root / "art/concepts/dream_classroom/classroom-cinematic-target-v5.png"),
        ("v5 Compatibility 完整运行时", root / "outputs/classroom-v5-full-runtime/01-view-blackboard.png"),
    ])
    comparison(output / "review-manor-current-target-v2.png", "洋房：历史基线 / 电影式目标 / v2 完整运行时", [
        ("历史 v1 运行时", root / "outputs/dream-manor-godot-1080p/01-foyer-front-door.png"),
        ("目标概念", root / "art/concepts/dream_manor/manor-foyer-cinematic-target-v2.png"),
        ("v2 Compatibility 完整运行时", root / "outputs/dream-manor-v2-full-runtime/01-foyer-front-door.png"),
    ])
    source = json.loads((root / "art/sources/cc0/polyhaven/environment-cinematic-v1/source-manifest.json").read_text(encoding="utf-8"))
    material_board(output / "review-cc0-material-breakdown.png", root, source)
    classroom = json.loads((root / "assets/generated/dream_escape/3d/classroom-art-pass-v5-manifest.json").read_text(encoding="utf-8"))
    manor = json.loads((root / "assets/generated/dream_escape/3d/dream-manor-hall-v2-manifest.json").read_text(encoding="utf-8"))
    report(output / "technical-report.md", classroom, manor, source)
    print("ENVIRONMENT_CINEMATIC_REVIEW_PASS boards=3 report=technical-report.md")


if __name__ == "__main__":
    main()
