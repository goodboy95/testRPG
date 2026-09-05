"""Build the deterministic 4K commercial-asset pre-purchase review board."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


FONT_CN = Path(r"C:\Windows\Fonts\msyh.ttc")
FONT_LATIN = Path(r"C:\Windows\Fonts\segoeui.ttf")


def font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    if not path.is_file():
        raise RuntimeError(f"Required review font is missing: {path}")
    return ImageFont.truetype(str(path), size)


def fit(path: Path, size: tuple[int, int]) -> Image.Image:
    if not path.is_file():
        raise RuntimeError(f"Commercial candidate thumbnail is missing: {path}")
    return ImageOps.fit(Image.open(path).convert("RGB"), size, Image.Resampling.LANCZOS)


def gradient(size: tuple[int, int]) -> Image.Image:
    width, height = size
    image = Image.new("RGB", size)
    pixels = image.load()
    for y in range(height):
        t = y / max(1, height - 1)
        for x in range(width):
            edge = abs(x / max(1, width - 1) - 0.5) * 2
            pixels[x, y] = (
                int(8 + 5 * t + 2 * edge),
                int(16 + 9 * t),
                int(22 + 10 * t + 3 * (1 - edge)),
            )
    return image


def text(draw: ImageDraw.ImageDraw, xy: tuple[int, int], value: str, size: int,
         fill: tuple[int, int, int], latin: bool = False) -> None:
    draw.text(xy, value, font=font(FONT_LATIN if latin else FONT_CN, size), fill=fill)


def card(canvas: Image.Image, draw: ImageDraw.ImageDraw, root: Path, x: int, y: int,
         number: str, title: str, use: str, image_name: str, price: str, specs: str,
         verdict: str, verdict_color: tuple[int, int, int], note: str) -> None:
    width, height = 1800, 780
    draw.rounded_rectangle((x, y, x + width, y + height), radius=24,
                           fill=(17, 27, 34), outline=(53, 72, 78), width=2)
    draw.line((x + 760, y + 34, x + 760, y + height - 34), fill=(54, 74, 78), width=2)

    image = fit(root / "art/concepts/environment-cinematic" / image_name, (690, 610))
    image = ImageOps.autocontrast(image, cutoff=0.4)
    canvas.paste(image, (x + 35, y + 35))
    draw.rectangle((x + 35, y + 35, x + 725, y + 645), outline=(107, 103, 81), width=2)
    text(draw, (x + 38, y + 675), number, 22, (178, 155, 103), latin=True)
    text(draw, (x + 260, y + 670), use, 25, (162, 176, 177))

    text(draw, (x + 815, y + 42), title, 33, (239, 229, 207))
    draw.rounded_rectangle((x + 815, y + 112, x + 815 + 234, y + 166), radius=27,
                           fill=verdict_color)
    text(draw, (x + 853, y + 120), verdict, 23, (247, 243, 228))
    text(draw, (x + 815, y + 208), "调研价格", 20, (125, 148, 151))
    text(draw, (x + 815, y + 246), price, 29, (223, 191, 126))
    text(draw, (x + 815, y + 326), "公开规格", 20, (125, 148, 151))

    line_y = y + 366
    for line in specs.split("\n"):
        text(draw, (x + 815, line_y), line, 24, (205, 214, 208))
        line_y += 38

    draw.line((x + 815, y + 518, x + width - 45, y + 518), fill=(53, 72, 78), width=2)
    text(draw, (x + 815, y + 552), "判断", 20, (125, 148, 151))
    note_y = y + 594
    for line in note.split("\n"):
        text(draw, (x + 815, note_y), line, 23, (193, 201, 196))
        note_y += 36


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    root = Path(parser.parse_args().root).resolve()
    output = root / "art/concepts/environment-cinematic/commercial-candidate-board-v1.png"

    canvas = gradient((3840, 2160))
    draw = ImageDraw.Draw(canvas)

    text(draw, (92, 58), "商业素材候选 / 采购前筛选", 51, (238, 228, 204))
    text(draw, (94, 132), "FILMIC ENVIRONMENT · PRIVATE REVIEW · 2026—08—29", 23,
         (133, 158, 161), latin=True)
    draw.line((92, 190, 3748, 190), fill=(117, 96, 58), width=2)
    text(draw, (2920, 72), "未购买  ·  未下载模型  ·  未进入运行时", 24, (186, 163, 112))

    cards = [
        (90, 250, "01 / LIGHT", "Classical 3D Assets Pack", "壁灯候选",
         "sconce-pack-candidate.png", "CNY 235.30  /  index USD 34.99",
         "PBR · 真实尺度\ntris / 纹理分辨率未披露", "有条件", (125, 91, 36),
         "装饰性偏强；确认面数与可拆分性后再评估\n样板英雄壁灯仍优先自制"),
        (1950, 250, "02 / PROP", "Vintage Book Collections", "散书候选",
         "vintage-books-candidate.png", "CNY 60.45  /  index USD 18.99",
         "2K PBR · 低模 · GLB/FBX/OBJ/STL\ntris 未披露", "有条件", (125, 91, 36),
         "成本较低但风格略偏奇幻\n只考虑少量散件与统一调色后的使用"),
        (90, 1090, "03 / DOOR", "Entrance Door 1900s", "门体参考",
         "entrance-door-1900s-candidate.png", "CNY 53.73  /  index USD 5.59",
         "4K · 真实尺度 · 完整 UV\ntris 未披露", "不建议", (104, 55, 48),
         "含现代对讲机；核心洋房门必须自制\n仅保留为形态参考"),
        (1950, 1090, "04 / HARDWARE", "Door Handles and Locks", "五金参考",
         "door-handles-locks-candidate.png", "CNY 1008.65  /  index USD 149.99",
         "4K PBR · LOW 约 5k—80k vertices\ntris 未披露", "不建议", (104, 55, 48),
         "价格过高且网格跨度不符合区域预算\n门锁与把手继续自制"),
    ]
    for entry in cards:
        card(canvas, draw, root, *entry)

    draw.line((92, 2012, 3748, 2012), fill=(54, 74, 78), width=2)
    text(draw, (92, 2042), "FAB STANDARD LICENSE · 仅用于内部评审 · 价格/税费/tier 购买前复核", 22,
         (130, 151, 152))
    text(draw, (2950, 2042), "当前建议：首轮全部不购买", 25, (222, 187, 119))

    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, "PNG", optimize=True)
    print(f"ENVIRONMENT_COMMERCIAL_CANDIDATE_BOARD_PASS candidates={len(cards)} size=3840x2160")


if __name__ == "__main__":
    main()
