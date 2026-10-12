# 속박 아이콘을 게임의 속박 아이콘 형식(32x32, 회색 둥근 타일 + 붉은 3단 음영 + 검은 외곽선)으로 만든다.
# 사용: python binding_icon_32.py <게임 속박 아이콘 폴더> <원본 128px 아이콘> <출력 png> [미리보기 png]
# 게임 아이콘 폴더에는 Binding_Icon_Hunted.png, Binding_Icon_HalfStamina.png 가 있어야 한다 (타일 배경을 가져옴)
import sys, os
from PIL import Image

icon_dir, src_path, out_path = sys.argv[1], sys.argv[2], sys.argv[3]
preview = sys.argv[4] if len(sys.argv) > 4 else None

TILE = {(82, 82, 82), (107, 107, 107), (58, 58, 58), (65, 64, 64), (68, 66, 66)}
DARK, MID, LIGHT, INK = (108, 22, 22), (193, 48, 48), (240, 97, 97), (0, 0, 0)

# 1) 타일 배경: 두 아이콘에서 그림이 없는(타일 색인) 칸을 모아 합친다
a = Image.open(os.path.join(icon_dir, "Binding_Icon_Hunted.png")).convert("RGBA")
b = Image.open(os.path.join(icon_dir, "Binding_Icon_HalfStamina.png")).convert("RGBA")
tile = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
for y in range(32):
    for x in range(32):
        pa, pb = a.getpixel((x, y)), b.getpixel((x, y))
        if pa[3] and pa[:3] in TILE: tile.putpixel((x, y), pa)
        elif pb[3] and pb[:3] in TILE: tile.putpixel((x, y), pb)
        elif pa[3] or pb[3]:
            # 둘 다 그림에 가려진 칸: 안쪽 타일 디더 (65/68 체크무늬)
            tile.putpixel((x, y), (65, 64, 64, 255) if (x + y) % 2 else (68, 66, 66, 255))

# 2) 그림: 원본에서 붉은 인물(달리는 실험체)만 골라 32칸에 맞게 줄인다
src = Image.open(src_path).convert("RGBA")
W, H = src.size
def is_fig(c):
    r, g, bb, al = c
    return al > 0 and r > 70 and r > g * 1.8 and r > bb * 1.6
pts = [(x, y) for y in range(H) for x in range(W) if is_fig(src.getpixel((x, y)))]
x0 = min(p[0] for p in pts); x1 = max(p[0] for p in pts); y0 = min(p[1] for p in pts); y1 = max(p[1] for p in pts)
size = 29  # 그림이 차지할 칸 (타일 테두리를 살짝 넘어가는 게임 아이콘과 비슷하게)
scale = max(x1 - x0 + 1, y1 - y0 + 1) / size
gw, gh = int((x1 - x0 + 1) / scale + 0.5), int((y1 - y0 + 1) / scale + 0.5)
ox, oy = (32 - gw) // 2, (32 - gh) // 2
fig = {}
for gy in range(gh):
    for gx in range(gw):
        sx0, sy0 = x0 + gx * scale, y0 + gy * scale
        lum, n, tot = 0.0, 0, 0
        for yy in range(int(sy0), int(sy0 + scale)):
            for xx in range(int(sx0), int(sx0 + scale)):
                if xx > x1 or yy > y1: continue
                tot += 1
                c = src.getpixel((xx, yy))
                if is_fig(c): n += 1; lum += c[0]
        if tot and n / tot >= 0.22:
            fig[(ox + gx, oy + gy)] = lum / n
# 떨어진 파편(이웃이 1칸 이하)은 지운다
for _ in range(2):
    for q in [q for q in fig if sum((q[0] + dx, q[1] + dy) in fig for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy) <= 1]:
        del fig[q]
# 가는 팔다리를 1칸 굵게 (오른쪽·아래로 번지게)
for (x, y), v in list(fig.items()):
    for q in ((x + 1, y), (x, y + 1)):
        if q not in fig and 1 <= q[0] < 31 and 1 <= q[1] < 31: fig[q] = v * 0.85
# 3단 음영: 원본 밝기 순위로 나눈다 (어두운 30% / 가운데 / 밝은 15%)
vals = sorted(fig.values())
lo, hi = vals[int(len(vals) * 0.22)], vals[int(len(vals) * 0.7)]
icon = tile.copy()
for (x, y) in fig:
    # 1px 검은 외곽선 (그림 바깥 4방향)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)):
        q = (x + dx, y + dy)
        if q not in fig and 0 <= q[0] < 32 and 0 <= q[1] < 32: icon.putpixel(q, INK + (255,))
for (x, y), v in fig.items():
    col = DARK if v <= lo else LIGHT if v >= hi else MID
    # 위·왼쪽이 그림 가장자리면 밝게 (게임 아이콘처럼 왼쪽 위에서 빛)
    if col == MID and ((x, y - 1) not in fig or (x - 1, y) not in fig): col = LIGHT
    icon.putpixel((x, y), col + (255,))
icon.save(out_path)

if preview:
    names = ["Hunted", "HalfStamina", "RoachMode", "Survival"]
    S = 256
    pv = Image.new("RGBA", (S * (len(names) + 1), S), (30, 30, 34, 255))
    for i, n in enumerate(names):
        pv.alpha_composite(Image.open(os.path.join(icon_dir, "Binding_Icon_%s.png" % n)).convert("RGBA").resize((S, S), 0), (i * S, 0))
    pv.alpha_composite(icon.resize((S, S), 0), (len(names) * S, 0))
    pv.save(preview)
print("ok", len(fig), "px")
