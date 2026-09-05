#!/usr/bin/env python3
"""Build deterministic, ignored review boards for protagonist art pass v2.

The script only composes already adopted references and review captures. It
does not alter the authoritative Blend, tracked GLB, or any runtime scene.
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "outputs/protagonist-art-pass-v2"
BACKGROUND = (20, 24, 29, 255)
PANEL = (31, 37, 44, 255)
TEXT = (235, 232, 222, 255)
MUTED = (164, 174, 180, 255)
ACCENT = (213, 146, 75, 255)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=ROOT)
    return parser.parse_args()


def load_font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = (
        Path("C:/Windows/Fonts/seguisb.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"),
        Path("C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"),
    )
    for path in candidates:
        if path.exists():
            return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


def open_rgba(path: Path) -> Image.Image:
    if not path.is_file():
        raise FileNotFoundError(path)
    with Image.open(path) as image:
        return image.convert("RGBA")


def paste_contained(canvas: Image.Image, image: Image.Image, box: tuple[int, int, int, int]) -> None:
    left, top, right, bottom = box
    fitted = ImageOps.contain(
        image,
        (right - left, bottom - top),
        method=Image.Resampling.LANCZOS,
    )
    x = left + (right - left - fitted.width) // 2
    y = top + (bottom - top - fitted.height) // 2
    canvas.alpha_composite(fitted, (x, y))


def panel(
    canvas: Image.Image,
    draw: ImageDraw.ImageDraw,
    box: tuple[int, int, int, int],
    title: str,
    subtitle: str,
    image: Image.Image,
) -> None:
    left, top, right, bottom = box
    draw.rounded_rectangle(box, radius=18, fill=PANEL, outline=(60, 70, 78, 255), width=2)
    draw.text((left + 24, top + 18), title, font=load_font(25, bold=True), fill=TEXT)
    draw.text((left + 24, top + 54), subtitle, font=load_font(17), fill=MUTED)
    paste_contained(canvas, image, (left + 20, top + 92, right - 20, bottom - 20))


def build_silhouette(root: Path) -> Path:
    concept = open_rgba(root / "art/concepts/characters/protagonist-art-direction-v1.png")
    # The adopted identity sheet places the large full-body anchor on its left.
    concept_anchor = concept.crop((0, 0, round(concept.width * 0.48), concept.height))
    v1 = open_rgba(root / "outputs/protagonist-static-v1/02-protagonist-natural-stance-1080p.png")
    v2 = open_rgba(root / "outputs/protagonist-art-pass-v2/09-stance-front-1080p.png")

    canvas = Image.new("RGBA", (2400, 1200), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.text((60, 32), "PROTAGONIST V2 / SILHOUETTE COMPARISON", font=load_font(38, bold=True), fill=TEXT)
    draw.text(
        (60, 82),
        "Identity anchor -> v1 export baseline -> v2 static visual candidate (not runtime)",
        font=load_font(21),
        fill=MUTED,
    )
    widths = (60, 840, 1620, 2340)
    panel(canvas, draw, (widths[0], 140, widths[1] - 20, 1140), "ADOPTED IDENTITY", "2D art-direction anchor", concept_anchor)
    panel(canvas, draw, (widths[1], 140, widths[2] - 20, 1140), "V1 TECHNICAL SAMPLE", "Scale/export baseline only", v1)
    panel(canvas, draw, (widths[2], 140, widths[3], 1140), "V2 STATIC CANDIDATE", "1.7017 m / 34,972 tris / pending review", v2)
    draw.rectangle((60, 1164, 2340, 1168), fill=ACCENT)

    output = root / "outputs/protagonist-art-pass-v2/protagonist-v2-silhouette-comparison.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output, format="PNG", optimize=True)
    return output


def build_review_board(root: Path) -> Path:
    entries = (
        ("FACE / HAIR REFERENCE", "Adopted multi-view detail board", "art/concepts/characters/protagonist-face-hair-reference-v2.png"),
        ("FACE 3/4", "Current v2 studio render", "outputs/protagonist-art-pass-v2/05-face-three-quarter-1080p.png"),
        ("COLLAR / POCKET", "Current v2 garment close-up", "outputs/protagonist-art-pass-v2/15-collar-pocket-detail-1080p.png"),
        ("A-POSE FRONT", "Authoritative static mesh", "outputs/protagonist-art-pass-v2/01-a-pose-front-1080p.png"),
        ("RELAXED REVIEW", "Review-only 11-degree arm lowering", "outputs/protagonist-art-pass-v2/09-stance-front-1080p.png"),
        ("CLASSROOM CAPTURE", "Review GLB; runtime remains 2D billboard", "outputs/protagonist-art-pass-v2/godot/02-front_left-1080p.png"),
    )
    canvas = Image.new("RGBA", (2400, 1440), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.text((60, 28), "PROTAGONIST V2 / STATIC REVIEW BOARD", font=load_font(38, bold=True), fill=TEXT)
    draw.text(
        (60, 78),
        "Technical validation passed; visual identity, hair and garment finish still require owner approval.",
        font=load_font(21),
        fill=MUTED,
    )
    column_width = 740
    row_height = 610
    for index, (title, subtitle, relative) in enumerate(entries):
        column = index % 3
        row = index // 3
        left = 60 + column * 780
        top = 130 + row * 640
        panel(
            canvas,
            draw,
            (left, top, left + column_width, top + row_height),
            title,
            subtitle,
            open_rgba(root / relative),
        )
    draw.rectangle((60, 1394, 2340, 1398), fill=ACCENT)
    output = root / "outputs/protagonist-art-pass-v2/protagonist-v2-static-review-board.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output, format="PNG", optimize=True)
    return output


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    for output in (build_silhouette(root), build_review_board(root)):
        print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
