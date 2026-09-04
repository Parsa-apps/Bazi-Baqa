#!/usr/bin/env python3
"""تولیدکننده‌ی بافت‌های رویه‌ای (procedural) بازی «سرزمین بقا».

چرا رویه‌ای؟
    بافت‌های آماده‌ی خارجی هم حجم APK را بالا می‌برند و هم مجوز/ارزش هنری‌شان نامعلوم است.
    این اسکریپت نقشه‌های Albedo/Normal/Mask را از نویزهای سازگارِ بی‌درز (seamless) می‌سازد
    تا Shader سطح، بدون هیچ فایل سنگین، جزئیات واقعی داشته باشد.

نکته‌ی فنی: محیط این مخزن `numpy` و `Pillow` ندارد؛ برای همین PNG مستقیم با `zlib`
    نوشته می‌شود (بدون وابستگی). همه‌ی نویزها تناوبی‌اند، پس تایل‌شدن درز ندارد.

usage:
    python3 Tools/procedural_textures.py            # فقط گزارش و اندازه‌ها
    python3 Tools/procedural_textures.py --apply    # نوشتن PNG ها در Assets/Resources/Textures/Graphics
    python3 Tools/procedural_textures.py --check    # بررسی همسانی فایل‌های موجود (برای CI)
"""
from __future__ import annotations

import argparse
import math
import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "Assets" / "Resources" / "Textures" / "Graphics"

# ---------------------------------------------------------------------------
# انکودر PNG (RGB و RGBA، ۸ بیت، بدون اینترلیس)
# ---------------------------------------------------------------------------


def _paeth(a: int, b: int, c: int) -> int:
    """پیش‌بین Paeth؛ برای نقشه‌ی نرمال/ماسکِ گرادیان‌دار فشرده‌سازی بهتری از Sub می‌دهد."""
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def write_png(path: Path, width: int, height: int, pixels: bytes, channels: int = 3) -> int:
    """نوشتن PNG با فیلتر Paeth (هر سطر با کد فیلتر ۶)."""
    if len(pixels) != width * height * channels:
        raise ValueError(f"buffer size mismatch: {len(pixels)} != {width * height * channels}")
    stride = width * channels
    raw = bytearray()
    previous = bytearray(stride)
    for y in range(height):
        row = pixels[y * stride:(y + 1) * stride]
        raw.append(6)  # filter type: Paeth
        for i, value in enumerate(row):
            left = row[i - channels] if i >= channels else 0
            up = previous[i]
            up_left = previous[i - channels] if i >= channels else 0
            raw.append((value - _paeth(left, up, up_left)) & 0xFF)
        previous = row

    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    color_type = 2 if channels == 3 else 6
    header = struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    path.write_bytes(png)
    return len(png)


# ---------------------------------------------------------------------------
# نویزهای سازگار (periodic) — پایه‌ی همه‌ی بافت‌ها
# ---------------------------------------------------------------------------

_SIZE = 256          # بسامد پایه‌ی شبکه‌ی نویز (باید توان دو باشد تا بی‌درز بماند)
_PERM = list(range(256))


def _build_permutation(seed: int) -> list[int]:
    """جایگشت قطعی با seed — مثل `_random` در WorldGenerator، تکرارپذیر است."""
    state = (seed * 2654435761) & 0xFFFFFFFF
    table = list(range(256))
    for i in range(255, 0, -1):
        state = (state * 1103515245 + 12345) & 0x7FFFFFFF
        j = state % (i + 1)
        table[i], table[j] = table[j], table[i]
    return table


def _hash2(ix: int, iy: int, perm: list[int], salt: int) -> float:
    a = perm[(ix + perm[(iy + salt) & 255]) & 255]
    return a / 255.0


def value_noise(u: float, v: float, freq: int, perm: list[int], salt: int = 0, aniso: int = 1) -> float:
    """مقدار-نویز دوبعدی روی شبکه‌ی `freq × freq*aniso` که روی [0,1) بی‌درز تکرار می‌شود.

    نکات:
      * مختصات نرمال (۰ تا ) می‌گیرد و خودش بسامد را ضرب می‌کند؛ دو‌بار‌ضرب، الگو را در
        هر خانه تکرار می‌کرد و درز می‌آورد.
      * `aniso` عدد صحیح است تا بسامد راستای عمودی هم مضربی از شبکه بماند و درز ایجاد نشود
        (برای نویز جهت‌دار پوست درخت و برس‌کاری فلز).
    """
    freq = max(1, int(freq))
    freq_y = max(1, int(freq) * max(1, int(aniso)))
    u %= 1.0
    v %= 1.0
    fx, fy = u * freq, v * freq_y
    ix, iy = int(math.floor(fx)), int(math.floor(fy))
    tx, ty = fx - ix, fy - iy

    def smooth(t: float) -> float:  # کیونتیک؛ مشتق پیوسته ⇒ نرمال‌ها لبه ندارند
        return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)

    sx, sy = smooth(tx), smooth(ty)
    x0, x1 = ix % freq, (ix + 1) % freq
    y0, y1 = iy % freq_y, (iy + 1) % freq_y
    n00 = _hash2(x0, y0, perm, salt)
    n10 = _hash2(x1, y0, perm, salt)
    n01 = _hash2(x0, y1, perm, salt)
    n11 = _hash2(x1, y1, perm, salt)
    return (n00 * (1 - sx) + n10 * sx) * (1 - sy) + (n01 * (1 - sx) + n11 * sx) * sy


def fbm_noise(u: float, v: float, perm: list[int], base: int = 4, octaves: int = 5, gain: float = 0.5,
              salt: int = 0, aniso: int = 1) -> float:
    """فراکنالِ سازگار: هر اکتاو بسامد را دو برابر می‌کند پس بی‌درزی حفظ می‌شود."""
    total = 0.0
    amplitude = 1.0
    weight = 0.0
    freq = base
    for _ in range(octaves):
        total += amplitude * value_noise(u, v, freq, perm, salt, aniso)
        weight += amplitude
        amplitude *= gain
        freq *= 2
    return total / max(weight, 1e-6)


def worley(x: float, y: float, cells: int, perm: list[int], salt: int = 0, ridge: bool = False) -> float:
    """نویز سلولی (Worley) سازگار؛ برای ترک سنگ، چکه باران و لبه‌ی فرسایشی."""
    x = x % 1.0
    y = y % 1.0
    fx, fy = x * cells, y * cells
    cx, cy = int(math.floor(fx)), int(math.floor(fy))
    best = 9.0
    second = 9.0
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            gx, gy = (cx + ox) % cells, (cy + oy) % cells
            jx = _hash2(gx, gy, perm, salt)
            jy = _hash2(gx, gy, perm, salt + 91)
            # نقاط سلولی در حوزه‌ی تناوبی [0,cells) پیچیده می‌شوند تا درز نماند
            # نقطه در *همان* خانه‌ی k می‌نشیند (نه خانه‌ی k-1)؛ وگرنه جابه‌جایی نصف‌خانه
            # در مرزِ تایل جهت عوض می‌کند و درزِ واضح ایجاد می‌شود.
            px = ((cx + ox) + (jx - 0.5) * 0.9) % cells
            py = ((cy + oy) + (jy - 0.5) * 0.9) % cells
            dx = abs(fx - px)
            dy = abs(fy - py)
            dx = min(dx, cells - dx)
            dy = min(dy, cells - dy)
            d = math.hypot(dx, dy)
            if d < best:
                second = best
                best = d
            elif d < second:
                second = d
    if ridge:
        return max(0.0, min(1.0, 1.0 - (second - best) * 2.2))
    return max(0.0, min(1.0, best * 1.6))


def clamp01(v: float) -> float:
    return 0.0 if v < 0.0 else (1.0 if v > 1.0 else v)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def to_byte(v: float) -> int:
    return int(round(clamp01(v) * 255.0))


def height_to_normal(field: list[float], size: int, strength: float) -> bytes:
    """محاسبه‌ی نقشه‌ی نرمال از میدان ارتفاع با اختلاف مرکزی (بی‌درز، wrap)"""
    out = bytearray(size * size * 3)
    for y in range(size):
        ym = (y - 1) % size
        yp = (y + 1) % size
        for x in range(size):
            xm = (x - 1) % size
            xp = (x + 1) % size
            dx = (field[y * size + xp] - field[y * size + xm]) * strength
            dy = (field[yp * size + x] - field[ym * size + x]) * strength
            nx, ny, nz = -dx, -dy, 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz) or 1.0
            nx /= length
            ny /= length
            nz /= length
            index = (y * size + x) * 3
            out[index] = to_byte(nx * 0.5 + 0.5)
            out[index + 1] = to_byte(ny * 0.5 + 0.5)
            out[index + 2] = to_byte(nz * 0.5 + 0.5)
    return bytes(out)


# ---------------------------------------------------------------------------
# تولید هر بافت: تابع‌ها (u, v, field) را می‌گیرند و (r, g, b[, a]) در [0,1]
# ---------------------------------------------------------------------------


def _ground_masks(size: int, seed: int):
    """میدان‌های مشترکِ زمین: پوشش چمن، رطوبت، کاواک و ارتفاع."""
    perm = _build_permutation(seed)
    grass = [0.0] * (size * size)
    height = [0.0] * (size * size)
    cavity = [0.0] * (size * size)
    moist = [0.0] * (size * size)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            patch = fbm_noise(u, v, perm, base=3, octaves=3, salt=11)
            fleck = fbm_noise(u, v, perm, base=16, octaves=3, salt=23)
            grass[i] = clamp01((patch - 0.34) * 2.6 + (fleck - 0.5) * 0.28)
            h = patch * 0.6 + fleck * 0.4 - worley(u, v, 8, perm, salt=5) * 0.35
            height[i] = h
            cavity[i] = clamp01(0.5 - h * 0.9)
            moist[i] = clamp01(fbm_noise(u, v, perm, base=2, octaves=2, salt=41) * 1.2 - 0.15)
    return perm, grass, height, cavity, moist


def tex_ground_albedo(size: int) -> tuple[bytes, list[float]]:
    perm, grass, height, _cavity, _moist = _ground_masks(size, 1337)
    pixels = bytearray(size * size * 3)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            grain = fbm_noise(u, v, perm, base=64, octaves=2, salt=77)
            pebble = worley(u, v, 22, perm, salt=17)
            g = grass[i]
            # چمن: سبزِ خاکیِ سرد؛ خاک: قهوه‌ی گرم. مقادیر در فضای sRGB نوشته می‌شوند.
            shade = 0.86 + 0.3 * height[i] + 0.12 * grain + 0.07 * pebble
            r = lerp(0.396, 0.255, g) * shade
            gg = lerp(0.318, 0.408, g) * shade
            b = lerp(0.220, 0.192, g) * shade
            shadow = 1.0 - 0.16 * (1.0 - pebble) * g
            index = i * 3
            pixels[index] = to_byte(r * shadow)
            pixels[index + 1] = to_byte(gg * shadow)
            pixels[index + 2] = to_byte(b * shadow)
    return bytes(pixels), height


def tex_ground_mask(size: int) -> bytes:
    perm, grass, height, cavity, moist = _ground_masks(size, 1337)
    pixels = bytearray(size * size * 4)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            pebble = worley(u, v, 22, perm, salt=17)
            rough = clamp01(0.94 - grass[i] * 0.22 - pebble * 0.1 + cavity[i] * 0.12)
            ao = clamp01(1.0 - cavity[i] * 0.7 - (1.0 - pebble) * 0.12 + height[i] * 0.1)
            index = i * 4
            pixels[index] = to_byte(rough)
            pixels[index + 1] = to_byte(ao)
            pixels[index + 2] = to_byte(moist[i])
            pixels[index + 3] = to_byte(grass[i])
    return bytes(pixels)


def tex_ground_normal(size: int) -> bytes:
    _pixels, height = tex_ground_albedo(size)
    return height_to_normal(height, size, 2.6)


def _rock_field(size: int, seed: int):
    perm = _build_permutation(seed)
    height = [0.0] * (size * size)
    crack = [0.0] * (size * size)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            blocky = fbm_noise(u, v, perm, base=5, octaves=4, salt=3)
            c = worley(u, v, 9, perm, salt=29, ridge=True)
            crack[i] = clamp01(c * 1.35 - 0.28)
            facets = abs(blocky - 0.5) * 1.7
            height[i] = clamp01(facets * 0.7 + fbm_noise(u, v, perm, base=42, octaves=3, salt=13) * 0.32 - crack[i] * 0.4)
    return perm, height, crack


def tex_rock_albedo(size: int) -> tuple[bytes, list[float]]:
    perm, height, crack = _rock_field(size, 4242)
    pixels = bytearray(size * size * 3)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            tone = 0.56 + height[i] * 0.3 - crack[i] * 0.24
            warm = fbm_noise(u, v, perm, base=6, octaves=3, salt=51)
            index = i * 3
            pixels[index] = to_byte(tone * (1.0 + warm * 0.06))
            pixels[index + 1] = to_byte(tone * 0.99)
            pixels[index + 2] = to_byte(tone * (0.97 - warm * 0.05))
    return bytes(pixels), height


def tex_rock_normal(size: int) -> bytes:
    _pixels, height = tex_rock_albedo(size)
    return height_to_normal(height, size, 3.4)


def tex_rock_mask(size: int) -> bytes:
    perm, height, crack = _rock_field(size, 4242)
    pixels = bytearray(size * size * 4)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            rough = clamp01(0.72 + crack[i] * 0.22 - height[i] * 0.14)
            ao = clamp01(0.55 + height[i] * 0.5 - crack[i] * 0.35)
            edge = clamp01(height[i] * 1.4 - 0.35)
            index = i * 4
            pixels[index] = to_byte(rough)
            pixels[index + 1] = to_byte(ao)
            pixels[index + 2] = to_byte(clamp01(0.2 + crack[i] * 0.65))
            pixels[index + 3] = to_byte(edge)
    return bytes(pixels)


def tex_bark_albedo(size: int) -> tuple[bytes, list[float]]:
    """بافت پوست درخت: نویز جهت‌دار (ستون‌های عمودی) + شیار."""
    perm = _build_permutation(9091)
    height = [0.0] * (size * size)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            # بسامد موثرِ راستای v برابر base*2^(octaves-1)*aniso است و نباید از نصفِ
            # اندازه‌ی بافت بیشتر شود، وگرنه نمونه‌برداری زیر‌نمونه شده و در مرزِ تایل خط می‌افتد.
            groove = fbm_noise(u, v, perm, base=3, octaves=3, salt=61, aniso=4)
            fine = fbm_noise(u, v, perm, base=8, octaves=2, salt=71, aniso=2)
            height[i] = clamp01(groove * 0.78 + fine * 0.22)
    pixels = bytearray(size * size * 3)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            h = height[i]
            shade = 0.30 + h * 0.34
            index = i * 3
            pixels[index] = to_byte(shade * 1.12)
            pixels[index + 1] = to_byte(shade * 0.72)
            pixels[index + 2] = to_byte(shade * 0.42)
    return bytes(pixels), height


def tex_bark_normal(size: int) -> bytes:
    _pixels, height = tex_bark_albedo(size)
    return height_to_normal(height, size, 4.2)


def tex_panel_albedo(size: int) -> tuple[bytes, list[float]]:
    """دیواره‌ی ساختمان: خط‌های پنل، پرچ و سایه‌ی اتصال."""
    perm = _build_permutation(5150)
    height = [0.0] * (size * size)
    soot = [0.0] * (size * size)
    cells = 4
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            gx, gy = u * cells, v * cells
            fx = abs((gx % 1.0) - 0.5) * 2.0
            fy = abs((gy % 1.0) - 0.5) * 2.0
            seam = 1.0 - clamp01(min(fx, fy) * 7.0 - 4.6)
            brush = fbm_noise(u, v, perm, base=12, octaves=2, salt=31, aniso=6)
            height[i] = clamp01(0.55 + brush * 0.2 - seam * 0.55)
            soot[i] = fbm_noise(u, v, perm, base=3, octaves=3, salt=19)
    pixels = bytearray(size * size * 3)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            tone = 0.46 + height[i] * 0.26 + (1.0 - soot[i]) * 0.05
            index = i * 3
            pixels[index] = to_byte(tone * 0.92)
            pixels[index + 1] = to_byte(tone * 0.97)
            pixels[index + 2] = to_byte(tone * 1.04)
    return bytes(pixels), height


def tex_panel_normal(size: int) -> bytes:
    _pixels, height = tex_panel_albedo(size)
    return height_to_normal(height, size, 3.0)


def tex_panel_mask(size: int) -> bytes:
    perm = _build_permutation(5150)
    pixels = bytearray(size * size * 4)
    cells = 4
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            gx, gy = u * cells, v * cells
            fx = abs((gx % 1.0) - 0.5) * 2.0
            fy = abs((gy % 1.0) - 0.5) * 2.0
            seam = 1.0 - clamp01(min(fx, fy) * 7.0 - 4.6)
            rough = clamp01(0.44 + seam * 0.4 + fbm_noise(u, v, perm, base=32, octaves=2, salt=37) * 0.12)
            ao = clamp01(0.82 - seam * 0.5)
            edge = clamp01(seam * 0.85 + fbm_noise(u, v, perm, base=9, octaves=3, salt=43) * 0.35)
            index = i * 4
            pixels[index] = to_byte(rough)
            pixels[index + 1] = to_byte(ao)
            pixels[index + 2] = to_byte(0.25)
            pixels[index + 3] = to_byte(edge)
    return bytes(pixels)


def tex_damage(size: int) -> bytes:
    """ماسک آسیب ساختمان: ترک (R)، دوده/سوختگی (G)، لبه‌ی کنده‌شدگی (B)، آلفای روکش (A)."""
    perm = _build_permutation(8081)
    pixels = bytearray(size * size * 4)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            crack = clamp01(worley(u, v, 7, perm, salt=53, ridge=True) * 1.5 - 0.42)
            soot = clamp01(fbm_noise(u, v, perm, base=4, octaves=4, salt=67) * 1.3 - 0.35)
            chip = clamp01(worley(u, v, 13, perm, salt=79) * 1.1 - 0.55)
            alpha = clamp01(crack * 0.9 + chip * 0.7)
            index = i * 4
            pixels[index] = to_byte(crack)
            pixels[index + 1] = to_byte(soot)
            pixels[index + 2] = to_byte(chip)
            pixels[index + 3] = to_byte(alpha)
    return bytes(pixels)


def tex_noise_rgba(size: int) -> bytes:
    """چهار باند مستقل نویز برای شکستن تکرارِ بافت‌ها و پارامترهای تصادفیِ پایدار."""
    perm = _build_permutation(2468)
    pixels = bytearray(size * size * 4)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            index = i * 4
            pixels[index] = to_byte(fbm_noise(u, v, perm, base=2, octaves=3, salt=101))
            pixels[index + 1] = to_byte(fbm_noise(u, v, perm, base=6, octaves=4, salt=202))
            pixels[index + 2] = to_byte(fbm_noise(u, v, perm, base=18, octaves=3, salt=303))
            pixels[index + 3] = to_byte(value_noise(u, v, 64, perm, 404))
    return bytes(pixels)


def tex_leaf_cluster(size: int) -> tuple[bytes, list[float]]:
    """برگ‌های رویه‌ای برای شاخ‌وبرگ (کارتِ الفای‌دار)؛ بی‌درز نیست و لازم هم نیست."""
    perm = _build_permutation(3579)
    pixels = bytearray(size * size * 4)
    height = [0.0] * (size * size)
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            i = y * size + x
            leaf = clamp01(1.0 - worley(u, v, 6, perm, salt=11) * 2.4)
            vein = abs(fbm_noise(u, v, perm, base=24, octaves=2, salt=22) - 0.5) * 1.4
            tone = 0.3 + leaf * 0.42 + vein * 0.1
            height[i] = leaf
            index = i * 4
            pixels[index] = to_byte(tone * 0.62)
            pixels[index + 1] = to_byte(tone)
            pixels[index + 2] = to_byte(tone * 0.44)
            pixels[index + 3] = to_byte(leaf)
    return bytes(pixels), height


def tex_leaf_normal(size: int) -> bytes:
    _pixels, height = tex_leaf_cluster(size)
    return height_to_normal(height, size, 2.2)


# ---------------------------------------------------------------------------
# جدول خروجی
# ---------------------------------------------------------------------------

# نام فایل → (تولیدکننده، اندازه، srgb)
JOBS: list[tuple[str, object, int, bool]] = [
    ("GroundAlbedo", tex_ground_albedo, 256, True),
    ("GroundNormal", tex_ground_normal, 256, False),
    ("GroundMask", tex_ground_mask, 256, False),
    ("RockAlbedo", tex_rock_albedo, 256, True),
    ("RockNormal", tex_rock_normal, 256, False),
    ("RockMask", tex_rock_mask, 256, False),
    ("BarkAlbedo", tex_bark_albedo, 128, True),
    ("BarkNormal", tex_bark_normal, 128, False),
    ("PanelAlbedo", tex_panel_albedo, 256, True),
    ("PanelNormal", tex_panel_normal, 256, False),
    ("PanelMask", tex_panel_mask, 256, False),
    ("DamageMask", tex_damage, 256, False),
    ("DetailNoise", tex_noise_rgba, 128, False),
    ("LeafCluster", tex_leaf_cluster, 128, True),
    ("LeafNormal", tex_leaf_normal, 128, False),
]


def generate(name: str, producer, size: int, srgb: bool) -> tuple[bytes, int]:
    result = producer(size)
    if isinstance(result, tuple):
        pixels, _height = result
    else:
        pixels = result
    channels = 4 if name in {"GroundMask", "RockMask", "PanelMask", "DamageMask", "DetailNoise", "LeafCluster"} else 3
    path = OUT_DIR / f"{name}.png"
    out = write_png(path, size, size, pixels, channels)
    return pixels, out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="فایل‌ها را بنویس")
    parser.add_argument("--check", action="store_true", help="فقط وجود و اندازه‌ها را گزارش کن")
    parser.add_argument("--only", default="", help="فقط یک بافت با این نام (برای آزمایش)")
    args = parser.parse_args()

    if args.check:
        missing = [name for name, _p, _s, _g in JOBS if not (OUT_DIR / f"{name}.png").is_file()]
        total = sum((OUT_DIR / f"{name}.png").stat().st_size for name, _p, _s, _g in JOBS
                    if (OUT_DIR / f"{name}.png").is_file())
        print(f"بافت‌ها: {len(JOBS) - len(missing)}/{len(JOBS)} موجود | حجم {total / 1024.0:.0f} کیلوبایت")
        if missing:
            print("نبودها: " + ", ".join(missing))
            return 1
        return 0

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    total = 0
    for name, producer, size, srgb in JOBS:
        if args.only and args.only != name:
            continue
        if not args.apply:
            print(f"dry-run: {name} ({size}x{size}, srgb={srgb})")
            continue
        _pixels, written = generate(name, producer, size, srgb)
        total += written
        print(f"wrote Assets/Resources/Textures/Graphics/{name}.png  ({size}px, {written / 1024.0:.0f} KB)")
    if args.apply:
        print(f"مجموع حجم: {total / 1024.0:.0f} کیلوبایت در {OUT_DIR.relative_to(ROOT)}")
        # متاها باید توسط unity_meta.py ساخته شوند تا GUID ثابت بماند
        print("بعد از تولید: python3 Tools/unity_meta.py --apply   (متای بافت‌ها)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
