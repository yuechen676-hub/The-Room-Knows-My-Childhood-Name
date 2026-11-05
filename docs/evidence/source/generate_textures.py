# Procedural, tileable textures for the room shell (walls, trim, carpet, ceiling,
# corridor stone, paper). Everything is synthesised from seeded noise; no image
# is read. Output: ../Textures/<Name>_BaseColor.png and <Name>_Normal.png
# Usage: python generate_textures.py
from pathlib import Path
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "Textures"


def noise(size, beta, seed, stretch=(1.0, 1.0)):
    """Periodic 1/f^beta noise in 0..1. stretch > 1 elongates features along that axis (y, x)."""
    rng = np.random.default_rng(seed)
    fy = np.fft.fftfreq(size)[:, None] * stretch[0]
    fx = np.fft.fftfreq(size)[None, :] * stretch[1]
    radius = np.sqrt(fx * fx + fy * fy)
    radius[0, 0] = 1
    spectrum = np.fft.fft2(rng.standard_normal((size, size))) / radius ** (beta / 2)
    spectrum[0, 0] = 0
    field = np.fft.ifft2(spectrum).real
    low, high = np.percentile(field, [0.5, 99.5])
    return np.clip((field - low) / (high - low), 0, 1)


def normal_map(height, strength):
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * strength
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * strength
    n = np.dstack([-dx, dy, np.ones_like(height)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return np.uint8(np.clip(n * 127.5 + 127.5, 0, 255))


def save(name, color, height, strength):
    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.uint8(np.clip(color, 0, 255))).save(OUT / f"{name}_BaseColor.png")
    Image.fromarray(normal_map(height, strength)).save(OUT / f"{name}_Normal.png")
    print(name, "mean", color.reshape(-1, 3).mean(0).round(0), "std", round(float(color.std()), 1))


def tint(value, rgb):
    return value[..., None] * np.array(rgb, dtype=float)[None, None, :]


def plaster(size=2048):
    # Old painted plaster, no pattern: broad uneven patches, a few damp stains,
    # faint runs under them, and roller grain. One tile spans four metres.
    patches = noise(size, 3.0, 11)
    stains = np.clip((noise(size, 3.4, 12) - 0.62) / 0.38, 0, 1) ** 1.5
    runs = noise(size, 2.2, 13, stretch=(0.06, 3.0)) * np.clip((noise(size, 3.2, 14) - 0.45) * 3, 0, 1)
    grain = noise(size, 0.9, 15)
    value = 223 + (patches - 0.5) * 9 - stains * 13 - runs * 5 + (grain - 0.5) * 4
    color = tint(value / 223, (223, 221, 215))
    color[..., 0] += stains * 3      # stains yellow slightly
    color[..., 2] -= stains * 6 + (patches - 0.5) * 2
    height = noise(size, 1.5, 16) * 0.6 + grain * 0.4 - stains * 0.15
    save("Plaster_Wall", color, height, 2.2)


def ceiling(size=1024):
    patches, grain = noise(size, 3.0, 21), noise(size, 1.0, 22)
    color = tint((248 + (patches - 0.5) * 3 + (grain - 0.5) * 2) / 248, (248, 248, 247))
    save("Plaster_Ceiling", color, noise(size, 1.6, 23) * 0.5 + grain * 0.5, 1.6)


def trim(size=1024):
    # Brushed gloss paint over wood: long strokes along U, slight yellowing.
    strokes = noise(size, 1.8, 31, stretch=(4.0, 0.04))
    patches = noise(size, 3.0, 32)
    color = tint((226 + (strokes - 0.5) * 7 + (patches - 0.5) * 6) / 226, (228, 226, 220))
    color[..., 2] -= patches * 3
    save("Trim_Paint", color, strokes * 0.7 + noise(size, 1.2, 33) * 0.3, 1.4)


def carpet(size=1024):
    # Loop pile: dense fibre noise with broad wear; one tile spans two metres.
    fibre = noise(size, 0.35, 41)
    twist = noise(size, 1.3, 42)
    wear = noise(size, 3.2, 43)
    value = 122 + (fibre - 0.5) * 16 + (twist - 0.5) * 8 + (wear - 0.5) * 7
    color = tint(value / 122, (121, 122, 126))
    save("Carpet_Grey", color, fibre * 0.6 + twist * 0.4, 5.0)


def stone(size=2048):
    # Worn poured stone for the exit corridor: mottling, pitting, hairline cracks.
    mottle, cloud, grit = noise(size, 2.6, 51), noise(size, 3.4, 52), noise(size, 0.8, 53)
    pits = np.clip((noise(size, 1.2, 54) - 0.80) / 0.20, 0, 1)
    cracks = np.clip(1 - np.abs(noise(size, 2.3, 55) - 0.5) / 0.006, 0, 1) * np.clip((noise(size, 3.0, 56) - 0.5) * 4, 0, 1)
    value = 97 + (mottle - 0.5) * 22 + (cloud - 0.5) * 12 + (grit - 0.5) * 8 - pits * 18 - cracks * 26
    color = tint(value / 97, (99, 96, 92))
    height = mottle * 0.35 + grit * 0.25 - pits * 0.5 - cracks * 0.8
    save("Worn_Stone", color, height, 3.0)


def paper(size=1024):
    # Plain aged paper with fibres; the normal carries soft creases.
    fibres = noise(size, 1.0, 61, stretch=(1.0, 0.3))
    patches = noise(size, 3.0, 62)
    color = tint((238 + (fibres - 0.5) * 5 + (patches - 0.5) * 7) / 238, (240, 236, 226))
    creases = np.abs(noise(size, 2.4, 63) - 0.5) * 2
    save("Paper_Plain", color, creases * 0.8 + fibres * 0.2, 2.5)


if __name__ == "__main__":
    plaster(); ceiling(); trim(); carpet(); stone(); paper()
