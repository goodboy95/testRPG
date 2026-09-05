"""Build a deterministic side-by-side PNG from the saved 2D and 3D acceptance captures."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--two-d", required=True, type=Path)
    parser.add_argument("--three-d", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    with Image.open(args.two_d) as two_d_source, Image.open(args.three_d) as three_d_source:
        two_d = two_d_source.convert("RGB")
        three_d = three_d_source.convert("RGB")
        if two_d.size != (640, 360) or three_d.size != (640, 360):
            raise ValueError(f"Expected two 640x360 captures, got {two_d.size} and {three_d.size}")

        header_height = 30
        canvas = Image.new("RGB", (1280, 360 + header_height), "#071319")
        canvas.paste(two_d, (0, header_height))
        canvas.paste(three_d, (640, header_height))
        draw = ImageDraw.Draw(canvas)
        font = ImageFont.load_default(size=16)
        draw.text((12, 7), "2D original-image baseline", fill="#efb95c", font=font)
        draw.text((652, 7), "3D southeast MVP", fill="#efb95c", font=font)
        draw.line((639, 0, 639, canvas.height), fill="#efb95c", width=2)

        args.output.parent.mkdir(parents=True, exist_ok=True)
        canvas.save(args.output, format="PNG", optimize=True)
        print(f"CLASSROOM_COMPARISON_PASS size={canvas.width}x{canvas.height}")


if __name__ == "__main__":
    main()
