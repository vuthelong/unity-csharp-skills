import argparse
import math
import random
import struct
import sys
import zlib

KINDS = ["glow", "ring", "star", "streak", "slash", "smoke", "noise", "cells", "pixel", "gradient"]


def write_png(path, width, height, rgba):
    stride = width * 4
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        raw += rgba[y * stride:(y + 1) * stride]

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def clamp01(value):
    return 0.0 if value < 0.0 else 1.0 if value > 1.0 else value


def smooth(edge0, edge1, value):
    t = clamp01((value - edge0) / (edge1 - edge0)) if edge1 != edge0 else (1.0 if value >= edge1 else 0.0)
    return t * t * (3 - 2 * t)


class TileNoise:
    def __init__(self, seed, period):
        rng = random.Random(seed)
        self.period = period
        self.grid = [rng.random() for _ in range(period * period)]

    def at(self, x, y):
        p = self.period
        x0 = math.floor(x)
        y0 = math.floor(y)
        fx = x - x0
        fy = y - y0
        sx = fx * fx * fx * (fx * (fx * 6 - 15) + 10)
        sy = fy * fy * fy * (fy * (fy * 6 - 15) + 10)
        i0 = x0 % p
        i1 = (x0 + 1) % p
        j0 = y0 % p
        j1 = (y0 + 1) % p
        g = self.grid
        a = g[j0 * p + i0]
        b = g[j0 * p + i1]
        c = g[j1 * p + i0]
        d = g[j1 * p + i1]
        return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy


class Fbm:
    def __init__(self, seed, scale, octaves, persistence):
        self.layers = []
        amplitude = 1.0
        total = 0.0
        for octave in range(octaves):
            period = scale * (2 ** octave)
            self.layers.append((TileNoise(seed * 7919 + octave, period), period, amplitude))
            total += amplitude
            amplitude *= persistence
        self.total = total

    def at(self, u, v):
        value = 0.0
        for noise, period, amplitude in self.layers:
            value += noise.at(u * period, v * period) * amplitude
        return value / self.total


class Cells:
    def __init__(self, seed, count):
        rng = random.Random(seed)
        self.count = count
        self.points = [(rng.random(), rng.random()) for _ in range(count * count)]

    def at(self, u, v):
        n = self.count
        x = u * n
        y = v * n
        cx = math.floor(x)
        cy = math.floor(y)
        f1 = 9.0
        f2 = 9.0
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                gx = cx + dx
                gy = cy + dy
                px, py = self.points[(gy % n) * n + (gx % n)]
                ddx = gx + px - x
                ddy = gy + py - y
                d = math.sqrt(ddx * ddx + ddy * ddy)
                if d < f1:
                    f2 = f1
                    f1 = d
                elif d < f2:
                    f2 = d
        return f1, f2


def build_sampler(args):
    kind = args.kind
    fbm = Fbm(args.seed, args.scale, args.octaves, args.persistence) if kind in ("smoke", "noise", "pixel") else None
    cells = Cells(args.seed, args.scale) if kind == "cells" else None
    arc0 = math.radians(args.arc_start)
    arc_span = math.radians(args.arc)

    def glow(d, falloff, core):
        body = clamp01(1.0 - d) ** falloff
        hot = core * math.exp(-(d / 0.08) ** 2) if core > 0 else 0.0
        return clamp01(body + hot)

    def sample(x, y, u, v, t, animated):
        d = math.sqrt(x * x + y * y)
        if kind == "glow":
            if animated:
                scale = 0.35 + 0.65 * t
                return glow(d / scale, args.falloff, args.core) * (1.0 - t) ** 1.5
            return glow(d, args.falloff, args.core)
        if kind == "ring":
            radius = args.radius * (0.15 + 0.85 * t) if animated else args.radius
            width = args.width * (1.0 + t) if animated else args.width
            band = math.exp(-((d - radius) / width) ** 2)
            inner = smooth(0.0, radius, d) if args.inner_fill <= 0 else max(smooth(0.0, radius, d), args.inner_fill * clamp01(1.0 - d / radius))
            fade = (1.0 - t) ** 1.2 if animated else 1.0
            return clamp01(band * inner * fade * smooth(1.0, 0.92, d))
        if kind == "star":
            rays = 0.0
            for i in range(args.spikes):
                theta = math.pi * i / args.spikes
                along = abs(x * math.cos(theta) + y * math.sin(theta))
                across = abs(-x * math.sin(theta) + y * math.cos(theta))
                ray = math.exp(-(across / (args.width * (1.0 - clamp01(along)) + 1e-4)) ** 2) * clamp01(1.0 - along) ** args.falloff
                rays = max(rays, ray)
            return clamp01(max(rays, glow(d / 0.35, 2.0, args.core)))
        if kind == "streak":
            return clamp01(math.exp(-(y / args.width) ** 2) * clamp01(1.0 - abs(x)) ** args.falloff)
        if kind == "slash":
            angle = (math.atan2(y, x) - arc0) % (2 * math.pi)
            if angle > arc_span:
                return 0.0
            p = angle / arc_span
            taper = math.sin(math.pi * p) ** 0.7
            width = args.width * taper + 1e-4
            band = math.exp(-((d - args.radius) / width) ** 2)
            head = 0.35 + 0.65 * p
            return clamp01(band * head * smooth(1.0, 0.92, d))
        if kind == "smoke":
            shift = 0.35 * t if animated else 0.0
            n = fbm.at((u + shift) % 1.0, v)
            reach = 0.45 + 0.5 * t if animated else 0.95
            mask = smooth(reach, reach * 0.25, d) ** (args.falloff * 0.5)
            threshold = args.threshold + (0.45 * t if animated else 0.0)
            value = mask * clamp01((n - 0.5) * args.contrast * 1.5 + 0.75)
            fade = 1.0 - 0.7 * t * t if animated else 1.0
            return clamp01((value - threshold) / max(1e-4, 1.0 - threshold)) * fade
        if kind == "noise" or kind == "pixel":
            n = fbm.at(u, (v + t) % 1.0 if animated else v)
            return clamp01((n - 0.5) * args.contrast + 0.5)
        if kind == "cells":
            f1, f2 = cells.at(u, v)
            value = clamp01((f2 - f1) * 3.0) if args.edges else clamp01(1.0 - f1 * 1.4)
            return clamp01((value - 0.5) * args.contrast + 0.5)
        if kind == "gradient":
            return clamp01(u if args.dir == "h" else 1.0 - v) ** args.falloff
        raise ValueError(kind)

    return sample


def render(args):
    if args.kind == "pixel":
        if args.pixelate <= 1:
            args.pixelate = max(2, args.size // 32)
        if args.levels <= 0:
            args.levels = 4
    size = args.size
    frames = max(1, args.frames)
    cols = args.cols if args.cols > 0 else (int(math.ceil(math.sqrt(frames))) if frames > 1 else 1)
    rows = int(math.ceil(frames / cols))
    width = size * cols
    height = size * rows
    sample = build_sampler(args)
    alpha = [0.0] * (width * height)
    block = max(1, args.pixelate)
    for frame in range(frames):
        t = frame / (frames - 1) if frames > 1 else 0.0
        ox = (frame % cols) * size
        oy = (frame // cols) * size
        cache = {}
        for py in range(size):
            sy = (py // block) * block + block * 0.5 if block > 1 else py + 0.5
            for px in range(size):
                sx = (px // block) * block + block * 0.5 if block > 1 else px + 0.5
                key = (sx, sy)
                value = cache.get(key) if block > 1 else None
                if value is None:
                    u = sx / size
                    v = sy / size
                    x = u * 2.0 - 1.0
                    y = 1.0 - v * 2.0
                    value = sample(x, y, u, v, t, frames > 1)
                    if args.gamma != 1.0:
                        value = value ** args.gamma
                    if args.invert:
                        value = 1.0 - value
                    if args.levels > 1:
                        value = round(value * (args.levels - 1)) / (args.levels - 1)
                    if block > 1:
                        cache[key] = value
                alpha[(oy + py) * width + ox + px] = value
    return width, height, cols, rows, alpha


def to_rgba(alpha, mode):
    out = bytearray(len(alpha) * 4)
    for i, a in enumerate(alpha):
        b = int(round(clamp01(a) * 255))
        j = i * 4
        if mode == "white":
            out[j:j + 4] = bytes((255, 255, 255, b))
        elif mode == "mask":
            out[j:j + 4] = bytes((b, b, b, b))
        else:
            out[j:j + 4] = bytes((b, b, b, 255))
    return out


def demo(path, seed):
    cell = 128
    kinds = KINDS + ["ring-sheet", "smoke-sheet"]
    cols = 4
    rows = int(math.ceil(len(kinds) / cols))
    width = cell * cols
    height = cell * rows
    out = bytearray(width * height * 4)
    for index, name in enumerate(kinds):
        kind = name.split("-")[0]
        argv = [kind, "x.png", "--size", str(cell // 4 if name.endswith("sheet") else cell), "--seed", str(seed)]
        if name.endswith("sheet"):
            argv += ["--frames", "16"]
        args = parse(argv)
        w, h, _, _, alpha = render(args)
        ox = (index % cols) * cell
        oy = (index // cols) * cell
        for y in range(cell):
            for x in range(cell):
                a = alpha[min(h - 1, y * h // cell) * w + min(w - 1, x * w // cell)]
                bg = 24 if ((x // 16) + (y // 16)) % 2 == 0 else 36
                c = int(bg + (255 - bg) * a)
                j = ((oy + y) * width + ox + x) * 4
                out[j:j + 4] = bytes((c, c, c, 255))
    write_png(path, width, height, out)
    return "demo " + path + " " + str(cols) + "x" + str(rows) + ": " + ", ".join(kinds)


def parse(argv):
    p = argparse.ArgumentParser(prog="texgen.py", description="Procedural particle textures (RGBA PNG, stdlib only).")
    p.add_argument("kind", choices=KINDS + ["demo"])
    p.add_argument("out")
    p.add_argument("--size", type=int, default=256, help="texture size, or cell size for --frames")
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--frames", type=int, default=1, help="flipbook frames over t 0..1 (glow, ring, smoke, noise animate)")
    p.add_argument("--cols", type=int, default=0, help="flipbook columns (default square-ish)")
    p.add_argument("--falloff", type=float, default=2.0, help="edge falloff exponent")
    p.add_argument("--core", type=float, default=0.0, help="hot core added to glow/star, 0..1")
    p.add_argument("--radius", type=float, default=0.7, help="ring/slash radius, 0..1 of half size")
    p.add_argument("--width", type=float, default=0.08, help="ring/slash band, star ray or streak thickness")
    p.add_argument("--inner-fill", type=float, default=0.0, help="ring: faint fill inside the band, 0..1")
    p.add_argument("--spikes", type=int, default=2, help="star: number of lines through the center")
    p.add_argument("--arc", type=float, default=160.0, help="slash: arc length in degrees")
    p.add_argument("--arc-start", type=float, default=-40.0, help="slash: start angle, 0 = right, counter-clockwise")
    p.add_argument("--scale", type=int, default=4, help="noise/smoke base period, cells per side")
    p.add_argument("--octaves", type=int, default=4)
    p.add_argument("--persistence", type=float, default=0.5)
    p.add_argument("--contrast", type=float, default=1.6, help="noise/cells/pixel contrast around 0.5")
    p.add_argument("--threshold", type=float, default=0.15, help="smoke: cut below this density")
    p.add_argument("--edges", action="store_true", help="cells: cell borders (F2-F1) instead of blobs")
    p.add_argument("--dir", choices=["h", "v"], default="h", help="gradient direction")
    p.add_argument("--pixelate", type=int, default=1, help="block size in pixels (pixel kind default size/32)")
    p.add_argument("--levels", type=int, default=0, help="posterize to N levels (pixel kind default 4)")
    p.add_argument("--gamma", type=float, default=1.0)
    p.add_argument("--invert", action="store_true")
    p.add_argument("--rgb", choices=["white", "mask", "opaque"], default="white",
                   help="white: RGB white + alpha shape (add and alpha blend); mask: RGB = A (black background); opaque: grey RGB, A = 1")
    return p.parse_args(argv)


def main(argv):
    args = parse(argv)
    if args.kind == "demo":
        print(demo(args.out, args.seed))
        return 0
    if args.size < 8 or args.size > 2048:
        print("ERROR size must be 8..2048")
        return 1
    width, height, cols, rows, alpha = render(args)
    write_png(args.out, width, height, to_rgba(alpha, args.rgb))
    peak = max(alpha)
    border = max(max(alpha[0:width]), max(alpha[-width:]), max(alpha[y * width] for y in range(height)), max(alpha[y * width + width - 1] for y in range(height)))
    tiles = " tiles " + str(cols) + "x" + str(rows) if args.frames > 1 else ""
    tiled = args.kind in ("noise", "cells", "pixel")
    note = "" if tiled or args.kind == "gradient" or border < 0.02 else " WARN border alpha " + format(border, ".2f") + ": use Clamp wrap or shrink the shape"
    print("OK " + args.out + " " + str(width) + "x" + str(height) + tiles + " peak " + format(peak, ".2f") + (" tileable" if tiled else "") + note)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
