#!/usr/bin/env python3
"""Build and validate the deterministic 12 x 9 metre classroom v4 plan boards."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
from tempfile import TemporaryDirectory

from PIL import Image, ImageDraw, ImageFont


PLAN_RELATIVE = Path("art/concepts/dream_classroom/classroom-v4-floor-plan.png")
COMPARISON_RELATIVE = Path("art/concepts/dream_classroom/classroom-v4-concept-comparison.png")
REFERENCE_RELATIVE = Path("art/concepts/dream_classroom/classroom-art-pass-v2-concept.png")

INK = "#e8e1cc"
MUTED = "#94aaa7"
AMBER = "#d6a85d"
TEAL = "#315c5d"
WOOD = "#71523f"
PAPER = "#cbc2a6"
BACKGROUND = "#111d25"


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    names = ["msyhbd.ttc" if bold else "msyh.ttc", "simhei.ttf", "arial.ttf"]
    for name in names:
        candidate = Path("C:/Windows/Fonts") / name
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def draw_plan(path: Path) -> None:
    canvas = Image.new("RGB", (2400, 1600), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    title = font(54, True)
    heading = font(32, True)
    label = font(27)
    small = font(22)
    draw.text((90, 60), "第一室 v4 · 12×9 米确定性俯视布局", font=title, fill=INK)
    draw.text((92, 128), "净高 4m · 18 组单人课桌椅 · 两条纵向主通道 · 完整后排回访通道", font=label, fill=MUTED)

    room = (210, 225, 2190, 1395)
    draw.rounded_rectangle(room, radius=8, fill="#1a2c35", outline=INK, width=8)
    x0, y0, x1, y1 = room
    sx = (x1 - x0) / 12.0
    sz = (y1 - y0) / 9.0

    def point(x: float, z: float) -> tuple[float, float]:
        return x0 + (x + 6.0) * sx, y0 + (z + 4.5) * sz

    def rect(cx: float, cz: float, width: float, depth: float, fill: str, outline: str = INK, stroke: int = 3):
        px, py = point(cx, cz)
        box = (px - width * sx / 2, py - depth * sz / 2, px + width * sx / 2, py + depth * sz / 2)
        draw.rounded_rectangle(box, radius=5, fill=fill, outline=outline, width=stroke)
        return box

    # North teaching zone.
    zone_y = point(0, -2.8)[1]
    draw.rectangle((x0 + 8, y0 + 8, x1 - 8, zone_y), fill="#183238")
    board = rect(-1.1, -4.18, 5.6, 0.22, "#173431", AMBER, 4)
    draw.text((board[0] + 18, board[1] + 10), "5.6m 黑板", font=small, fill=INK)
    rect(0.55, -3.25, 2.05, 1.05, WOOD)
    draw.text(point(-0.35, -3.32), "教师桌", font=small, fill=INK, anchor="mm")
    rect(-5.05, -3.78, 1.2, 0.72, "#38565a")
    draw.text(point(-5.05, -3.78), "地图柜", font=small, fill=INK, anchor="mm")
    rect(3.55, -4.17, 0.55, 0.20, PAPER)
    rect(4.75, -4.17, 0.75, 0.20, PAPER)
    draw.text(point(3.55, -3.82), "7:00 时钟", font=small, fill=AMBER, anchor="mm")
    draw.text(point(4.75, -3.82), "值日表", font=small, fill=AMBER, anchor="mm")
    draw.text(point(3.65, -3.25), "时针拾取", font=small, fill=AMBER, anchor="mm")

    # Three west window bays and cache.
    for z in (-2.0, -0.15, 1.7):
        px, py = point(-5.95, z)
        draw.line((px, py - 0.70 * sz, px, py + 0.70 * sz), fill="#7bb0ad", width=12)
    draw.text(point(-5.48, -0.15), "三组可操作窗扇\n上—下—中", font=small, fill="#9fd1cb", anchor="lm")
    draw.text(point(-5.25, -0.90), "窗台暗格", font=small, fill=AMBER, anchor="lm")

    # Desk layout, exactly six columns by three rows.
    columns = (-4.25, -2.55, -0.85, 0.85, 2.55, 4.25)
    rows = (-0.40, 1.45, 3.05)
    index = 1
    for z in rows:
        for x in columns:
            desk = rect(x, z, 0.82, 1.18, WOOD, "#b28d62", 3)
            draw.rectangle((desk[0] + 8, desk[1] + 8, desk[2] - 8, desk[1] + 18), fill="#253c43")
            draw.text(point(x, z), f"{index:02d}", font=small, fill=INK, anchor="mm")
            index += 1

    # Main aisles and rear cross aisle.
    for x in (-1.70, 1.70):
        px, _ = point(x, 0)
        draw.line((px, point(x, -2.35)[1], px, point(x, 3.80)[1]), fill=AMBER, width=4)
        draw.text(point(x, 2.25), "主通道", font=small, fill=AMBER, anchor="mm")
    rear_y = point(0, 4.02)[1]
    draw.line((point(-5.25, 4.02)[0], rear_y, point(5.25, 4.02)[0], rear_y), fill=AMBER, width=4)
    draw.text(point(0, 4.05), "约 1.35m 后排回访通道", font=small, fill=AMBER, anchor="mm")

    # East door and transom cache.
    door_x, door_y = point(5.98, 3.35)
    draw.line((door_x, door_y - 0.60 * sz, door_x, door_y + 0.60 * sz), fill="#d7ae70", width=14)
    draw.arc((door_x - 1.2 * sx, door_y - 1.2 * sz, door_x, door_y + 1.2 * sz), 270, 360, fill=AMBER, width=4)
    draw.text(point(5.20, 3.30), "洋房门", font=small, fill=INK, anchor="rm")
    draw.text(point(5.10, 2.58), "气窗盒", font=small, fill=AMBER, anchor="rm")
    draw.ellipse((point(5.10, 2.85)[0] - 10, point(5.10, 2.85)[1] - 10,
                  point(5.10, 2.85)[0] + 10, point(5.10, 2.85)[1] + 10), fill="#73c0a9")
    draw.text(point(4.95, 2.85), "门内安全点", font=small, fill="#73c0a9", anchor="rm")

    # Dimensions and legend.
    draw.line((x0, y1 + 55, x1, y1 + 55), fill=INK, width=3)
    draw.line((x0, y1 + 38, x0, y1 + 72), fill=INK, width=3)
    draw.line((x1, y1 + 38, x1, y1 + 72), fill=INK, width=3)
    draw.text(((x0 + x1) / 2, y1 + 78), "12.0 m", font=heading, fill=INK, anchor="ma")
    draw.line((x1 + 55, y0, x1 + 55, y1), fill=INK, width=3)
    draw.line((x1 + 38, y0, x1 + 72, y0), fill=INK, width=3)
    draw.line((x1 + 38, y1, x1 + 72, y1), fill=INK, width=3)
    draw.text((x1 + 88, (y0 + y1) / 2), "9.0 m", font=heading, fill=INK, anchor="mm")
    draw.text((90, 1510), "权威：固定尺寸与门位；概念图仅负责材质、灯光和陈设语言。", font=small, fill=MUTED)

    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path, optimize=True)


def draw_comparison(plan_path: Path, reference_path: Path, output_path: Path) -> None:
    board = Image.new("RGB", (3840, 2160), "#0b141b")
    draw = ImageDraw.Draw(board)
    draw.text((120, 78), "第一室 v4 · 扩建布局与既有美术方向对照", font=font(62, True), fill=INK)
    draw.text((122, 158), "左：确定性空间权威　右：v2 写实概念的色调、材质与陈设参考", font=font(30), fill=MUTED)
    plan = Image.open(plan_path).convert("RGB")
    reference = Image.open(reference_path).convert("RGB")
    plan.thumbnail((1760, 1760), Image.Resampling.LANCZOS)
    reference.thumbnail((1760, 1760), Image.Resampling.LANCZOS)
    board.paste(plan, (80, 290 + (1760 - plan.height) // 2))
    board.paste(reference, (2000, 290 + (1760 - reference.height) // 2))
    draw.rounded_rectangle((78, 288, 1842, 2052), radius=8, outline=AMBER, width=5)
    draw.rounded_rectangle((1998, 288, 3762, 2052), radius=8, outline=TEAL, width=5)
    draw.text((960, 2090), "12×9m / 18 组课桌 / 完整天花板", font=font(28, True), fill=AMBER, anchor="mm")
    draw.text((2880, 2090), "旧校舍写实材质 / 深蓝青绿暗部 / 克制琥珀光", font=font(28, True), fill="#8ebbb5", anchor="mm")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    board.save(output_path, optimize=True)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(root: Path, plan: Path, comparison: Path) -> None:
    draw_plan(plan)
    draw_comparison(plan, root / REFERENCE_RELATIVE, comparison)


def check(root: Path) -> None:
    plan = root / PLAN_RELATIVE
    comparison = root / COMPARISON_RELATIVE
    if not plan.exists() or not comparison.exists():
        raise SystemExit("CLASSROOM_V4_PLAN_CHECK_FAIL missing output")
    with TemporaryDirectory(prefix="classroom-v4-plan-") as temporary:
        temp = Path(temporary)
        temp_plan = temp / plan.name
        temp_comparison = temp / comparison.name
        build(root, temp_plan, temp_comparison)
        if sha256(plan) != sha256(temp_plan) or sha256(comparison) != sha256(temp_comparison):
            raise SystemExit("CLASSROOM_V4_PLAN_CHECK_FAIL nondeterministic output")
    print(f"CLASSROOM_V4_PLAN_CHECK_PASS plan={sha256(plan)} comparison={sha256(comparison)}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("build", "check"))
    parser.add_argument("--root", default=".")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    if args.mode == "build":
        build(root, root / PLAN_RELATIVE, root / COMPARISON_RELATIVE)
        print(f"CLASSROOM_V4_PLAN_BUILD_PASS plan={sha256(root / PLAN_RELATIVE)}")
    else:
        check(root)


if __name__ == "__main__":
    main()
