"""Encode local-only village motion captures; never edits source models or game assets.

Usage: python scripts/tools/build_village_travel_review.py outputs/village-travel/<run>-video
Requires the existing local OpenCV and Pillow installations.
"""
import json
import sys
from pathlib import Path

import cv2
from PIL import Image, ImageDraw


def main():
    capture = Path(sys.argv[1]).resolve()
    allowed = (Path(__file__).resolve().parents[2] / "outputs" / "village-travel").resolve()
    if not capture.is_relative_to(allowed):
        raise ValueError("Use a capture directory within outputs/village-travel")
    results = []
    for width in (1280, 1600, 1920):
        folder = capture / str(width)
        info = json.loads((folder / "motion.json").read_text(encoding="utf-8"))
        height = width * 9 // 16
        assert (info["width"], info["height"], info["frames"], info["fps"]) == (width, height, 240, 30)
        frames = sorted(folder.glob("*.jpg"))
        assert len(frames) == 240
        output = capture / f"motion-{width}x{height}.mp4"
        writer = cv2.VideoWriter(str(output), cv2.VideoWriter_fourcc(*"mp4v"), 30, (width, height))
        if not writer.isOpened():
            raise RuntimeError("Video encoder unavailable")
        try:
            for frame in frames:
                pixels = cv2.imread(str(frame))
                assert pixels is not None and pixels.shape[:2] == (height, width)
                writer.write(pixels)
        finally:
            writer.release()
        reader = cv2.VideoCapture(str(output))
        decoded = 0
        try:
            while True:
                ok, pixels = reader.read()
                if not ok:
                    break
                assert pixels.shape[:2] == (height, width)
                decoded += 1
        finally:
            reader.release()
        assert decoded == 240, decoded
        sheet = Image.new("RGB", (1280, 780), "#ecebe5")
        draw = ImageDraw.Draw(sheet)
        for i, (frame, label) in enumerate(((60, "RUN"), (108, "TURN"), (130, "STOP"), (158, "SLOW"))):
            x, y = i % 2 * 640, i // 2 * 390
            with Image.open(frames[frame]) as image:
                sheet.paste(image.resize((640, 360)), (x, y + 30))
            draw.text((x + 12, y + 8), f"{width}x{height}  {label}  frame {frame}", fill="#26382c")
        sheet.save(capture / f"motion-{width}-contact.jpg", quality=93)
        results.append({"file": str(output), "width": width, "height": height,
                        "decodedFrames": decoded, "fps": 30, "seconds": 8,
                        "bytes": output.stat().st_size})
    (capture / "encoded-videos.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps(results, indent=2))


if __name__ == "__main__":
    main()
