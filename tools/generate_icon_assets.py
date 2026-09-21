from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def main() -> None:
    parser = argparse.ArgumentParser(description="Build transparent QuickTrans app icon assets.")
    parser.add_argument("source", type=Path)
    parser.add_argument("output_dir", type=Path)
    args = parser.parse_args()

    image = Image.open(args.source).convert("RGB")
    rgb = np.asarray(image, dtype=np.uint8).astype(np.int16)

    # Find the supplied mark against its white canvas so excessive source-image
    # whitespace can be removed without changing the artwork itself.
    distance_from_white = 255 - rgb.min(axis=2)
    channel_spread = rgb.max(axis=2) - rgb.min(axis=2)
    visible = np.maximum(distance_from_white, channel_spread) > 14
    ys, xs = np.nonzero(visible)
    bbox = None if len(xs) == 0 else (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    if bbox is None:
        raise RuntimeError("No visible logo pixels found.")

    padding = round(max(bbox[2] - bbox[0], bbox[3] - bbox[1]) * 0.055)
    bbox = (
        max(0, bbox[0] - padding),
        max(0, bbox[1] - padding),
        min(image.width, bbox[2] + padding),
        min(image.height, bbox[3] + padding),
    )
    cropped = image.crop(bbox)
    canvas_size = 1024
    usable_size = 820
    scale = min(usable_size / cropped.width, usable_size / cropped.height)
    resized = cropped.resize(
        (round(cropped.width * scale), round(cropped.height * scale)),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (255, 255, 255, 255))
    canvas.paste(resized, ((canvas_size - resized.width) // 2, (canvas_size - resized.height) // 2))

    # Keep the requested white tile, with transparency only outside its rounded
    # corners. A restrained border keeps the white icon visible on white desktops.
    radius = round(canvas_size * 0.22)
    mask = Image.new("L", (canvas_size, canvas_size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, canvas_size - 1, canvas_size - 1), radius=radius, fill=255)
    canvas.putalpha(mask)
    ImageDraw.Draw(canvas).rounded_rectangle(
        (2, 2, canvas_size - 3, canvas_size - 3),
        radius=radius - 2,
        outline=(222, 226, 232, 255),
        width=4,
    )

    args.output_dir.mkdir(parents=True, exist_ok=True)
    canvas.save(args.output_dir / "QuickTrans.png", optimize=True)
    canvas.save(
        args.output_dir / "QuickTrans.ico",
        format="ICO",
        sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)],
    )


if __name__ == "__main__":
    main()
