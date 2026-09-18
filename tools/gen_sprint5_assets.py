#!/usr/bin/env python3
"""Sprint 5 P0-A/P0-B 素材生成（纯标准库，无外部依赖）：
- assets/background/zone1..4.png  512x512 星云平铺背景（第1章冷蓝/第2章青绿/第3章紫红/第4章暗红）
- assets/background/mothership_grid.png  128x128 深色金属科技网格底纹
- assets/icons/weapon.png armor.png power.png special.png  16x16 槽位像素图标
"""
import struct, zlib, os, math, random

def write_png(path, w, h, pixels):
    """pixels: list of rows, each row list of (r,g,b,a)."""
    raw = b""
    for y in range(h):
        raw += b"\x00"  # filter: none
        for x in range(w):
            raw += bytes(pixels[y][x])
    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        c += struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        return c
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)

def make_canvas(w, h, base):
    return [[base for _ in range(w)] for _ in range(h)]

def blend(pixels, x, y, rgba, alpha=None):
    if not (0 <= x < len(pixels[0]) and 0 <= y < len(pixels)):
        return
    a = rgba[3] if alpha is None else alpha
    r, g, b = rgba[0], rgba[1], rgba[2]
    pr, pg, pb, pa = pixels[y][x]
    out_a = a + pa * (255 - a) / 255
    if out_a <= 0:
        return
    pixels[y][x] = (
        int((r * a + pr * pa * (255 - a) / 255) / out_a),
        int((g * a + pg * pa * (255 - a) / 255) / out_a),
        int((b * a + pb * pa * (255 - a) / 255) / out_a),
        int(out_a),
    )

def vline(pixels, x, y0, y1, rgba):
    for y in range(y0, y1 + 1):
        blend(pixels, x, y, rgba)

def hline(pixels, y, x0, x1, rgba):
    for x in range(x0, x1 + 1):
        blend(pixels, x, y, rgba)

def rect(pixels, x0, y0, x1, y1, rgba, fill=False):
    if fill:
        for y in range(y0, y1 + 1):
            hline(pixels, y, x0, x1, rgba)
    else:
        hline(pixels, y0, x0, x1, rgba)
        hline(pixels, y1, x0, x1, rgba)
        vline(pixels, x0, y0, y1, rgba)
        vline(pixels, x1, y0, y1, rgba)

def circle(pixels, cx, cy, radius, rgba, thickness=1):
    for y in range(cy - radius, cy + radius + 1):
        for x in range(cx - radius, cx + radius + 1):
            d = math.hypot(x - cx, y - cy)
            if radius - thickness <= d <= radius:
                blend(pixels, x, y, rgba)

# ---------------- 星云背景 ----------------
def nebula_background(seed, base_rgb, nebula_rgbs, w=512, h=512):
    rng = random.Random(seed)
    px = make_canvas(w, h, base_rgb + (255,))
    # 多层径向星云光晕
    for _ in range(26):
        cx, cy = rng.uniform(0, w), rng.uniform(0, h)
        radius = rng.uniform(w * 0.08, w * 0.30)
        col = rng.choice(nebula_rgbs)
        peak = rng.uniform(18, 55)
        for y in range(max(0, int(cy - radius)), min(h, int(cy + radius) + 1)):
            for x in range(max(0, int(cx - radius)), min(w, int(cx + radius) + 1)):
                d = math.hypot(x - cx, y - cy) / radius
                if d <= 1:
                    a = int(peak * (1 - d) ** 2)
                    if a > 0:
                        blend(px, x, y, col + (a,))
    # 星点（密度随章节）
    star_count = int(w * h * rng.uniform(0.004, 0.007))
    for _ in range(star_count):
        x, y = rng.randrange(w), rng.randrange(h)
        b = rng.randint(140, 255)
        blend(px, x, y, (b, b, b, rng.randint(120, 255)))
    # 少量亮星十字光斑
    for _ in range(14):
        x, y = rng.randrange(w), rng.randrange(h)
        bright = rng.randint(200, 255)
        c = (bright, bright, bright)
        blend(px, x, y, c + (255,))
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (-2, 0), (0, 2), (0, -2)):
            blend(px, x + dx, y + dy, c + (140,))
    return px

zones = {
    # zone: (seed, base, [nebula colors])
    1: (101, (10, 16, 34), [(70, 130, 220), (90, 160, 235), (50, 90, 180), (120, 180, 255)]),   # 冷蓝
    2: (202, (8, 26, 24), [(60, 190, 160), (90, 220, 190), (40, 150, 130), (140, 240, 210)]),    # 青绿
    3: (303, (30, 10, 34), [(170, 80, 210), (140, 60, 190), (200, 110, 230), (110, 50, 160)]),   # 紫红
    4: (404, (30, 8, 10), [(190, 50, 50), (220, 80, 60), (150, 40, 40), (230, 120, 90)]),        # 暗红
}
out = r"C:\Users\skyer\BaiduSyncdisk\工作空间\ws5\repos\deakin\sit771\7.4h\proj\Hullward\assets\background"
for zone, (seed, base, cols) in zones.items():
    write_png(os.path.join(out, f"zone{zone}.png"), 512, 512, nebula_background(seed, base, cols))
    print("zone%d.png written" % zone)

# ---------------- 母舰网格底纹 ----------------
grid = make_canvas(128, 128, (22, 18, 31, 255))
line_col = (58, 52, 82, 255)
grid_col = (90, 82, 120, 200)
for gx in range(0, 129, 32):
    vline(grid, gx, 0, 127, line_col)
for gy in range(0, 129, 32):
    hline(grid, gy, 0, 127, line_col)
# 交角装饰点
for gx in range(0, 129, 32):
    for gy in range(0, 129, 32):
        blend(grid, gx, gy, grid_col)
        blend(grid, gx + 1, gy, grid_col)
        blend(grid, gx, gy + 1, grid_col)
# 随机微尘
rng = random.Random(7)
for _ in range(180):
    blend(grid, rng.randrange(128), rng.randrange(128), (70, 66, 100, rng.randint(60, 160)))
write_png(os.path.join(out, "mothership_grid.png"), 128, 128, grid)
print("mothership_grid.png written")

# ---------------- 槽位图标 16x16 ----------------
def icon16(name, draw):
    px = make_canvas(16, 16, (0, 0, 0, 0))
    draw(px)
    write_png(os.path.join(icon_dir, name), 16, 16, px)
    print(name, "written")

icon_dir = r"C:\Users\skyer\BaiduSyncdisk\工作空间\ws5\repos\deakin\sit771\7.4h\proj\Hullward\assets\icons"

def weapon(px):
    # 炮管朝右：粗管 + 白高光 + 炮口
    rect(px, 2, 7, 13, 10, (90, 150, 235, 255), fill=True)   # 管身蓝
    rect(px, 2, 8, 13, 9, (150, 200, 255, 255), fill=True)   # 高光
    rect(px, 5, 6, 7, 11, (60, 100, 200, 255), fill=True)    # 炮尾基座
    rect(px, 13, 7, 14, 10, (255, 255, 255, 255), fill=True) # 炮口白
    rect(px, 2, 5, 4, 12, (50, 80, 170, 255), fill=True)     # 基座加深

def armor(px):
    # 盾形白边：顶部弧 + 下部尖
    rect(px, 6, 3, 9, 4, (240, 240, 250, 255), fill=True)     # 盾顶
    vline(px, 5, 3, 8, (240, 240, 250, 255))
    vline(px, 10, 3, 8, (240, 240, 250, 255))
    vline(px, 6, 3, 12, (240, 240, 250, 255))
    vline(px, 9, 3, 12, (240, 240, 250, 255))
    blend(px, 7, 13, (240, 240, 250, 255))
    blend(px, 8, 13, (240, 240, 250, 255))
    blend(px, 7, 12, (240, 240, 250, 255))
    blend(px, 8, 12, (240, 240, 250, 255))
    # 内部填充
    for y in range(5, 12):
        hline(px, y, 6, 9, (60, 90, 190, 255))
    blend(px, 7, 12, (60, 90, 190, 255))
    blend(px, 8, 12, (60, 90, 190, 255))
    # 内部十字纹
    vline(px, 7, 5, 11, (140, 170, 240, 255))
    hline(px, 8, 6, 9, (140, 170, 240, 255))

def power(px):
    # 闪电黄
    pts = [(8, 2), (4, 9), (7, 9), (6, 14), (12, 6), (8, 6), (11, 2)]
    rect(px, 8, 2, 10, 3, (255, 220, 90, 255), fill=True)
    rect(px, 4, 8, 7, 10, (255, 220, 90, 255), fill=True)
    rect(px, 6, 11, 7, 14, (255, 220, 90, 255), fill=True)
    rect(px, 11, 5, 12, 7, (255, 220, 90, 255), fill=True)
    rect(px, 8, 5, 10, 7, (255, 230, 130, 255), fill=True)
    blend(px, 5, 9, (255, 230, 130, 255))

def special(px):
    # 雷达：圆环 + 扫描线（青绿）
    circle(px, 8, 8, 6, (80, 230, 210, 255), thickness=1)
    circle(px, 8, 8, 3, (80, 230, 210, 255), thickness=1)
    hline(px, 8, 2, 13, (120, 255, 235, 255))
    blend(px, 8, 2, (120, 255, 235, 255))
    blend(px, 8, 13, (120, 255, 235, 255))
    blend(px, 8, 8, (160, 255, 245, 255))

icon16("weapon.png", weapon)
icon16("armor.png", armor)
icon16("power.png", power)
icon16("special.png", special)
print("ALL ASSETS DONE")
