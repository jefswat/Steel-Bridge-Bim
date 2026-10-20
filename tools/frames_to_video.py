"""Turns the turntable frames written by `SteelBridgeModeler.exe ... --snapshot out.png --turntable N` into a video.

    python -I tools/frames_to_video.py <folder or prefix> out.mp4 [--fps 30]

<folder or prefix>: a folder (all *_frame0001.png style files in it, sorted) or the snapshot path / prefix
(`temp\\media\\nebraska` or `temp\\media\\nebraska.png` -> temp\\media\\nebraska_frame*.png).
MP4 needs imageio + imageio-ffmpeg (`pip install imageio imageio-ffmpeg`); without them, or for an output ending in .gif,
an animated GIF is written with Pillow (`pip install pillow`).
"""
import argparse
import glob
import os
import re
import sys


def find_frames(src):
    if os.path.isdir(src):
        files = glob.glob(os.path.join(src, "*_frame[0-9][0-9][0-9][0-9]*.png"))
    else:
        stem = os.path.splitext(src)[0] if src.lower().endswith(".png") else src
        files = glob.glob(glob.escape(stem) + "_frame[0-9][0-9][0-9][0-9]*.png")
    def key(f):
        m = re.findall(r"_frame(\d+)", os.path.basename(f))
        return int(m[-1]) if m else 0
    return sorted(files, key=key)


def write_gif(files, out, fps):
    from PIL import Image
    imgs = [Image.open(f).convert("RGB") for f in files]
    imgs[0].save(out, save_all=True, append_images=imgs[1:], duration=int(1000 / fps), loop=0, optimize=False)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source")
    ap.add_argument("output")
    ap.add_argument("--fps", type=float, default=30)
    a = ap.parse_args()
    files = find_frames(a.source)
    if not files:
        sys.exit("no frames found for " + a.source)
    print(f"{len(files)} frames, {a.fps:g} fps")
    out = a.output
    if out.lower().endswith(".gif"):
        write_gif(files, out, a.fps)
        print("wrote", out)
        return
    try:
        import imageio.v2 as imageio
        import imageio_ffmpeg  # noqa: F401
        # even dimensions for yuv420p
        with imageio.get_writer(out, fps=a.fps, codec="libx264", quality=8, macro_block_size=2) as w:
            for f in files:
                w.append_data(imageio.imread(f)[..., :3])
        print("wrote", out)
    except ImportError:
        gif = os.path.splitext(out)[0] + ".gif"
        print("imageio / imageio-ffmpeg not installed (pip install imageio imageio-ffmpeg): writing an animated GIF instead")
        write_gif(files, gif, a.fps)
        print("wrote", gif)


if __name__ == "__main__":
    main()
