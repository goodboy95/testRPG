"""Build or read-only validate deterministic 64px dream-v4 inventory icons."""

import argparse
import hashlib
from pathlib import Path
from tempfile import TemporaryDirectory
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets/generated/dream_escape"
BG = (10, 24, 34, 255)
AMBER = (224, 168, 76, 255)
PAPER = (211, 194, 151, 255)
METAL = (105, 127, 132, 255)
RED = (151, 53, 47, 220)


def canvas():
    image = Image.new("RGBA", (64, 64), BG)
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((2, 2, 61, 61), 8, fill=(16, 38, 48, 255), outline=(76, 104, 108, 255), width=2)
    return image, draw


def save(directory, name, painter):
    image, draw = canvas()
    painter(draw)
    image.save(directory / name, optimize=True)


def clock_hand(d):
    d.ellipse((27, 27, 37, 37), fill=AMBER)
    d.line((32, 31, 32, 12), fill=AMBER, width=4)
    d.line((32, 32, 21, 42), fill=AMBER, width=4)


def clock_key(d):
    d.ellipse((10, 21, 28, 39), outline=AMBER, width=5)
    d.line((27, 30, 51, 30), fill=AMBER, width=5)
    d.line((47, 30, 47, 39), fill=AMBER, width=4)
    d.line((53, 30, 53, 36), fill=AMBER, width=4)


def slider(d):
    d.rounded_rectangle((10, 16, 54, 48), 3, fill=(50, 69, 70, 255), outline=METAL, width=2)
    for x in range(16, 50, 8):
        d.line((x, 21, x, 43), fill=PAPER, width=2)
    d.rectangle((27, 12, 35, 52), fill=(176, 191, 181, 180), outline=AMBER, width=2)


def negative(d):
    d.rectangle((9, 14, 55, 50), fill=(30, 52, 58, 255), outline=PAPER, width=2)
    for x in range(12, 54, 7):
        d.rectangle((x, 16, x + 3, 19), fill=PAPER)
        d.rectangle((x, 45, x + 3, 48), fill=PAPER)
    for y in (25, 36):
        for x in (19, 32, 45):
            d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(137, 161, 151, 255))


def overlay(d):
    d.rounded_rectangle((12, 11, 52, 53), 3, fill=(152, 181, 178, 80), outline=(182, 218, 210, 230), width=2)
    for y in range(19, 49, 8):
        d.line((17, y, 47, y), fill=(182, 218, 210, 180), width=1)
    for x in range(22, 48, 8):
        d.line((x, 16, x, 48), fill=(182, 218, 210, 180), width=1)
    d.ellipse((28, 25, 36, 33), outline=RED, width=3)


def tape(d):
    d.arc((10, 17, 37, 45), 30, 330, fill=(81, 93, 90, 255), width=4)
    d.arc((27, 17, 54, 45), 210, 150, fill=(81, 93, 90, 255), width=4)
    d.line((13, 45, 52, 52), fill=(54, 45, 34, 255), width=3)
    d.rectangle((25, 48, 39, 55), fill=AMBER)


def reel(d):
    d.ellipse((10, 10, 54, 54), fill=METAL, outline=AMBER, width=3)
    for box in ((27, 13, 37, 27), (15, 31, 29, 41), (35, 31, 49, 41)):
        d.ellipse(box, fill=BG)
    d.ellipse((27, 27, 37, 37), fill=AMBER)


def hook(d):
    d.line((19, 52, 39, 17), fill=METAL, width=5)
    d.arc((32, 8, 54, 31), 270, 100, fill=AMBER, width=5)
    d.line((17, 50, 23, 55), fill=AMBER, width=5)


def mallet(d):
    d.rounded_rectangle((12, 13, 42, 27), 5, fill=METAL, outline=PAPER, width=2)
    d.line((35, 25, 52, 52), fill=AMBER, width=5)


def listening_cone(d):
    d.polygon(((12, 18), (42, 27), (42, 39), (12, 48)), fill=METAL, outline=PAPER)
    d.rectangle((42, 29, 54, 37), fill=AMBER)


def metronome_weight(d):
    d.line((14, 50, 45, 14), fill=PAPER, width=4)
    d.rounded_rectangle((24, 27, 40, 43), 3, fill=AMBER, outline=METAL, width=2)
    d.line((12, 52, 52, 52), fill=METAL, width=4)


def piano_roll(d):
    d.rounded_rectangle((9, 12, 55, 52), 5, fill=PAPER, outline=AMBER, width=2)
    for x, y in ((17, 20), (29, 25), (41, 18), (22, 38), (43, 40)):
        d.ellipse((x - 2, y - 2, x + 2, y + 2), fill=BG)
    d.line((31, 13, 27, 51), fill=RED, width=2)


def wax_patch(d):
    d.rounded_rectangle((13, 19, 51, 46), 4, fill=(218, 196, 124, 210), outline=PAPER, width=2)
    d.line((19, 26, 45, 39), fill=AMBER, width=3)


def room_key(d):
    d.ellipse((10, 17, 31, 38), outline=AMBER, width=5)
    d.line((29, 28, 53, 42), fill=AMBER, width=5)
    d.line((45, 37, 49, 30), fill=AMBER, width=4)


def clamp_key(d):
    d.rounded_rectangle((10, 24, 48, 40), 5, fill=METAL, outline=AMBER, width=3)
    d.rectangle((43, 18, 53, 46), fill=AMBER)


def music_tape(d):
    tape(d)
    d.polygon(((27, 15), (27, 29), (39, 25), (39, 11)), fill=PAPER)


def index_strip(d):
    d.rounded_rectangle((9, 17, 55, 47), 3, fill=PAPER, outline=AMBER, width=2)
    for x, label_y in ((16, 24), (28, 34), (40, 22), (50, 37)):
        d.rectangle((x - 3, label_y - 3, x + 3, label_y + 3), fill=METAL)
    d.line((16, 24, 28, 34, 40, 22, 50, 37), fill=RED, width=2)


def fuse_tube(d):
    d.rounded_rectangle((12, 25, 52, 39), 6, fill=(172, 161, 126, 255), outline=PAPER, width=2)
    d.rectangle((9, 23, 17, 41), fill=METAL)
    d.rectangle((47, 23, 55, 41), fill=METAL)


def copper_wire(d):
    d.arc((11, 13, 53, 55), 10, 335, fill=(190, 99, 50, 255), width=4)
    d.arc((18, 20, 46, 48), 10, 335, fill=(226, 137, 70, 255), width=3)


def repaired_fuse(d):
    fuse_tube(d)
    d.line((17, 32, 28, 24, 37, 40, 47, 32), fill=(226, 137, 70, 255), width=3)


def splicing_tape(d):
    d.ellipse((12, 12, 52, 52), fill=AMBER, outline=PAPER, width=2)
    d.ellipse((24, 24, 40, 40), fill=BG)
    d.rectangle((37, 35, 56, 43), fill=PAPER)


def inspection_mirror(d):
    d.rounded_rectangle((12, 9, 42, 36), 5, fill=(96, 139, 151, 255), outline=PAPER, width=2)
    d.line((38, 34, 53, 53), fill=AMBER, width=5)


def counterweight(d):
    d.polygon(((18, 19), (46, 19), (52, 52), (12, 52)), fill=METAL, outline=AMBER)
    d.ellipse((27, 10, 37, 22), outline=PAPER, width=3)


def projector_lens(d):
    d.ellipse((9, 9, 55, 55), fill=METAL, outline=AMBER, width=3)
    d.ellipse((16, 16, 48, 48), fill=(45, 90, 103, 255), outline=PAPER, width=2)
    d.ellipse((23, 20, 33, 30), fill=(132, 188, 191, 255))


def master_reel(d):
    reel(d)
    d.rectangle((26, 28, 38, 36), fill=BG)


def restored_tape(d):
    reel(d)
    d.arc((13, 13, 51, 51), 5, 350, fill=(53, 43, 33, 255), width=4)


def archive_tape(d):
    tape(d)
    d.rectangle((27, 15, 39, 27), fill=PAPER)
    d.line((30, 18, 36, 24), fill=METAL, width=2)


def room_four_key(d):
    room_key(d)
    d.rectangle((14, 23, 27, 32), fill=METAL)


PAINTERS = {
        "item-clock-hand.png": clock_hand,
        "item-clock-key.png": clock_key,
        "item-roster-slider.png": slider,
        "item-class-photo-negative.png": negative,
        "item-attendance-overlay.png": overlay,
        "item-tape-fragment-classroom.png": tape,
        "item-calibration-reel.png": reel,
        "item-stage-hook.png": hook,
        "item-rubber-mallet.png": mallet,
        "item-listening-cone.png": listening_cone,
        "item-metronome-weight.png": metronome_weight,
        "item-torn-piano-roll.png": piano_roll,
        "item-wax-patch.png": wax_patch,
        "item-room-three-key.png": room_key,
        "item-bench-clamp-key.png": clamp_key,
        "item-tape-fragment-music.png": music_tape,
        "item-archive-index-strip.png": index_strip,
        "item-fuse-tube.png": fuse_tube,
        "item-copper-wire.png": copper_wire,
        "item-repaired-fuse.png": repaired_fuse,
        "item-splicing-tape.png": splicing_tape,
        "item-room-four-key.png": room_four_key,
        "item-tape-fragment-archive.png": archive_tape,
        "item-inspection-mirror.png": inspection_mirror,
        "item-curtain-counterweight.png": counterweight,
        "item-projector-lens.png": projector_lens,
        "item-master-reel-core.png": master_reel,
        "item-restored-master-tape.png": restored_tape,
}


def build(directory: Path) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    for filename, painter in PAINTERS.items():
        save(directory, filename, painter)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check() -> None:
    missing = [name for name in PAINTERS if not (OUT / name).exists()]
    if missing:
        raise SystemExit(f"DREAM_V2_ICON_CHECK_FAIL missing={','.join(missing)}")
    with TemporaryDirectory(prefix="dream-v2-icons-") as temporary:
        candidate = Path(temporary)
        build(candidate)
        changed = [name for name in PAINTERS if digest(candidate / name) != digest(OUT / name)]
        if changed:
            raise SystemExit(f"DREAM_V2_ICON_CHECK_FAIL nondeterministic={','.join(changed)}")
    print(f"DREAM_V2_ICON_CHECK_PASS files={len(PAINTERS)} size=64x64 rgba=true readOnly=true")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", nargs="?", choices=("build", "check"), default="build")
    arguments = parser.parse_args()
    if arguments.mode == "build":
        build(OUT)
        print(f"DREAM_V2_ICON_BUILD_PASS files={len(PAINTERS)}")
    else:
        check()
