"""Compose deterministic review boards for the environment detail samples."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


FONT = Path(r"C:\Windows\Fonts\msyh.ttc")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    return parser.parse_args()


def font(size: int) -> ImageFont.FreeTypeFont:
    if not FONT.is_file():
        raise RuntimeError(f"Required review font is missing: {FONT}")
    return ImageFont.truetype(str(FONT), size)


def comparison_board(output: Path, title: str, entries: list[tuple[str, Path]]) -> None:
    canvas = Image.new("RGB", (3840, 1440), (13, 18, 23))
    draw = ImageDraw.Draw(canvas)
    draw.text((64, 34), title, font=font(34), fill=(232, 224, 202))
    label_font = font(22)
    for index, (label, path) in enumerate(entries):
        if not path.is_file():
            raise RuntimeError(f"Review input is missing: {path}")
        x = 40 + index * 1270
        image = ImageOps.fit(Image.open(path).convert("RGB"), (1240, 1140), Image.Resampling.LANCZOS)
        canvas.paste(image, (x, 108))
        draw.rectangle((x, 1248, x + 1240, 1326), fill=(22, 30, 37))
        draw.text((x + 24, 1268), label, font=label_font, fill=(225, 221, 207))
    canvas.save(output, "PNG", optimize=True)


def material_board(output: Path, texture_dir: Path) -> None:
    families = (("灰泥", "plaster"), ("深色橡木", "dark-oak"), ("旧水磨石", "terrazzo"),
                ("氧化黄铜", "aged-brass"), ("织物", "textile"))
    canvas = Image.new("RGB", (3840, 2160), (13, 18, 23))
    draw = ImageDraw.Draw(canvas)
    draw.text((58, 34), "洋房 v2 样板材质拆解：Albedo / Normal / ORM",
              font=font(34), fill=(232, 224, 202))
    label_font = font(22)
    for column, (label, key) in enumerate(families):
        x = 42 + column * 758
        draw.text((x, 95), label, font=label_font, fill=(224, 218, 199))
        for row, suffix in enumerate(("albedo", "normal", "orm")):
            path = texture_dir / f"{key}-{suffix}.png"
            if not path.is_file():
                raise RuntimeError(f"Material review input is missing: {path}")
            y = 138 + row * 640
            image = ImageOps.fit(Image.open(path).convert("RGB"), (730, 570), Image.Resampling.LANCZOS)
            canvas.paste(image, (x, y))
            draw.text((x + 14, y + 580), suffix.upper(), font=label_font, fill=(188, 196, 198))
    canvas.save(output, "PNG", optimize=True)


def main() -> None:
    root = Path(parse_args().root).resolve()
    output = root / "outputs/environment-detail-samples"
    texture = root / "work/environment-detail-samples/textures-v2"
    output.mkdir(parents=True, exist_ok=True)
    comparison_board(output / "05-classroom-concept-v3-v3-1-board.png",
                     "教室：概念图 / v3 基线 / v3.1 结构细节样板", [
                         ("概念图目标", root / "art/concepts/dream_classroom/classroom-art-pass-v2-concept.png"),
                         ("现有 v3 Lookdev", root / "outputs/classroom-3d-art-pass-v3/lookdev-southeast.png"),
                         ("v3.1 黑板墙样板", output / "01-classroom-blackboard-wall.png"),
                     ])
    comparison_board(output / "06-manor-concept-v1-v2-board.png",
                     "洋房：概念图 / v1 实机 / v2 材质与结构样板", [
                         ("概念图目标", root / "art/concepts/dream_manor/manor-interior-v2.png"),
                         ("现有 v1 Godot 实机", root / "outputs/dream-manor-godot-1080p/02-hall-looking-north.png"),
                         ("v2 第一室门材质样板", output / "03-manor-room1-foyer.png"),
                     ])
    material_board(output / "07-manor-material-breakdown.png", texture)
    print("ENVIRONMENT_DETAIL_REVIEW_PASS boards=3")


if __name__ == "__main__":
    main()
