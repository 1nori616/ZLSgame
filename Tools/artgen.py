r"""
占位美术生成器（ZLSgame · 猩猩丛林）
用法：
    python artgen.py --out "D:\UnityProjects\UnityGame\Assets\Art\Sprites"

约定：角色 64x64、道具 32x32、地砖 64x64、UI 按需；透明背景；PPU 100。
生成的是"占位图"——结构对、命名对、尺寸对，美术在此基础上改。
"""
import argparse, math, os, random
from PIL import Image, ImageDraw

S = 4  # 超采样倍数，降采样后边缘更干净


def canvas(w, h, ss=S):
    img = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img), w * ss, h * ss


def save(img, w, h, path):
    img = img.resize((w, h), Image.NEAREST)
    img.save(path)


def save_fit(img, w, h, path):
    save(fit_canvas(img, w, h), w, h, path)


def fit_canvas(img, w, h):
    """裁掉透明边，再按高度撑满画布（宽度保持比例居中）。
    这样角色正好"顶天立地"，配合中心枢轴和居中碰撞体，翻转/站立都不会悬空或下陷。"""
    bbox = img.split()[3].getbbox()
    if bbox is None:
        return img

    cropped = img.crop(bbox)
    cw, ch = cropped.size
    scale = (h * S) / ch
    new_w = max(1, int(round(cw * scale)))
    resized = cropped.resize((new_w, h * S), Image.NEAREST)

    out = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    out.paste(resized, ((w * S - new_w) // 2, 0), resized)
    return out


# ---------------- 调色板 ----------------
PAL = {
    "fur":      (74, 52, 40, 255),
    "fur_dark": (48, 33, 26, 255),
    "skin":     (214, 168, 130, 255),
    "eye":      (245, 240, 230, 255),
    "pupil":    (20, 16, 14, 255),
    "banana":   (247, 206, 70, 255),
    "banana_d": (214, 160, 40, 255),
    "poop":     (110, 74, 44, 255),
    "poop_d":   (78, 50, 30, 255),
    "leaf":     (86, 150, 76, 255),
    "leaf_d":   (54, 104, 52, 255),
    "stone":    (104, 106, 98, 255),
    "stone_d":  (74, 78, 72, 255),
}


def gorilla(draw, w, h, t, pose="idle"):
    """简单猩猩剪影：躯干椭圆 + 头 + 双臂 + 双腿。t=0..1 动画相位。"""
    cx, cy = w // 2, h // 2
    bob = int(math.sin(t * 2 * math.pi) * 1.2 * S)
    lean = 0

    # 腿
    legw = 5 * S
    swing = int(math.sin(t * 2 * math.pi) * 6 * S) if pose == "run" else 0
    draw.rounded_rectangle([cx - 7 * S, cy + 6 * S + bob, cx - 2 * S, h - 2 * S],
                           radius=2 * S, fill=PAL["fur_dark"])
    draw.rounded_rectangle([cx + 2 * S, cy + 6 * S + bob - swing, cx + 7 * S, h - 2 * S],
                           radius=2 * S, fill=PAL["fur_dark"])

    # 躯干
    draw.ellipse([cx - 11 * S, cy - 6 * S + bob, cx + 11 * S, cy + 12 * S + bob], fill=PAL["fur"])
    # 胸口浅色
    draw.ellipse([cx - 6 * S, cy - 1 * S + bob, cx + 6 * S, cy + 10 * S + bob], fill=PAL["skin"])

    # 手臂
    if pose == "jump":
        draw.rounded_rectangle([cx - 15 * S, cy - 10 * S + bob, cx - 10 * S, cy + 4 * S],
                               radius=2 * S, fill=PAL["fur"])
        draw.rounded_rectangle([cx + 10 * S, cy - 10 * S + bob, cx + 15 * S, cy + 4 * S],
                               radius=2 * S, fill=PAL["fur"])
    elif pose == "fall":
        draw.rounded_rectangle([cx - 17 * S, cy - 2 * S + bob, cx - 10 * S, cy + 2 * S],
                               radius=2 * S, fill=PAL["fur"])
        draw.rounded_rectangle([cx + 10 * S, cy - 2 * S + bob, cx + 17 * S, cy + 2 * S],
                               radius=2 * S, fill=PAL["fur"])
    else:
        draw.rounded_rectangle([cx - 14 * S, cy - 4 * S + bob, cx - 9 * S, cy + 8 * S],
                               radius=2 * S, fill=PAL["fur"])
        draw.rounded_rectangle([cx + 9 * S, cy - 4 * S + bob, cx + 14 * S, cy + 8 * S],
                               radius=2 * S, fill=PAL["fur"])

    # 头
    head_r = 9 * S
    draw.ellipse([cx - head_r, cy - 20 * S + bob, cx + head_r, cy - 2 * S + bob], fill=PAL["fur"])
    # 脸
    draw.ellipse([cx - 6 * S, cy - 16 * S + bob, cx + 6 * S, cy - 4 * S + bob], fill=PAL["skin"])
    # 眼睛
    for dx in (-3 * S, 3 * S):
        draw.ellipse([cx + dx - 2 * S, cy - 13 * S + bob, cx + dx + 2 * S, cy - 9 * S + bob], fill=PAL["eye"])
        draw.ellipse([cx + dx - 1 * S, cy - 12 * S + bob, cx + dx + 1 * S, cy - 10 * S + bob], fill=PAL["pupil"])
    # 鼻孔
    draw.ellipse([cx - 2 * S, cy - 7 * S + bob, cx - 1 * S, cy - 6 * S + bob], fill=PAL["pupil"])
    draw.ellipse([cx + 1 * S, cy - 7 * S + bob, cx + 2 * S, cy - 6 * S + bob], fill=PAL["pupil"])


def enemy(draw, w, h):
    cx, cy = w // 2, h // 2
    draw.ellipse([cx - 13 * S, cy - 8 * S, cx + 13 * S, cy + 12 * S], fill=PAL["leaf_d"])
    draw.ellipse([cx - 11 * S, cy - 10 * S, cx + 11 * S, cy + 8 * S], fill=PAL["leaf"])
    for dx in (-5 * S, 5 * S):
        draw.ellipse([cx + dx - 3 * S, cy - 4 * S, cx + dx + 1 * S, cy], fill=PAL["eye"])
        draw.ellipse([cx + dx - 2 * S, cy - 3 * S, cx + dx, cy - 1 * S], fill=PAL["pupil"])
    # 小尖刺
    for i in (-8, 0, 8):
        draw.polygon([(cx + i * S - 2 * S, cy - 9 * S), (cx + i * S + 2 * S, cy - 9 * S),
                      (cx + i * S, cy - 14 * S)], fill=PAL["leaf_d"])


def banana(draw, w, h):
    pts = []
    for i in range(24):
        a = math.radians(200 + i * 5)
        pts.append((w // 2 + int(math.cos(a) * 10 * S), h // 2 + int(math.sin(a) * 9 * S) + 4 * S))
    for i in range(24):
        a = math.radians(320 - i * 5)
        pts.append((w // 2 + int(math.cos(a) * 8 * S), h // 2 + int(math.sin(a) * 7 * S) + 4 * S))
    draw.polygon(pts, fill=PAL["banana"])
    draw.line([(w // 2 - 10 * S, h // 2 + 4 * S), (w // 2 + 10 * S, h // 2 + 4 * S)],
              fill=PAL["banana_d"], width=2 * S)


def poop(draw, w, h):
    cx, cy = w // 2, h // 2
    draw.ellipse([cx - 12 * S, cy + 2 * S, cx + 12 * S, cy + 13 * S], fill=PAL["poop"])
    draw.ellipse([cx - 9 * S, cy - 4 * S, cx + 9 * S, cy + 8 * S], fill=PAL["poop"])
    draw.ellipse([cx - 6 * S, cy - 10 * S, cx + 6 * S, cy + 2 * S], fill=PAL["poop"])
    draw.arc([cx - 6 * S, cy - 10 * S, cx + 6 * S, cy + 2 * S], 200, 340, fill=PAL["poop_d"], width=2 * S)


def tile(draw, w, h, seed=1):
    rnd = random.Random(seed)
    draw.rectangle([0, 0, w, h], fill=PAL["stone"])
    draw.rectangle([0, 0, w, 3 * S], fill=PAL["stone_d"])
    for _ in range(40):
        x = rnd.randrange(0, w - 2 * S)
        y = rnd.randrange(3 * S, h - 2 * S)
        c = PAL["stone_d"] if rnd.random() < 0.6 else PAL["stone"]
        draw.rectangle([x, y, x + 2 * S, y + 2 * S], fill=c)


def ui_bar(draw, w, h, fill_ratio=1.0, frame=True):
    if frame:
        draw.rectangle([0, 0, w, h], outline=(240, 236, 220, 255), width=2 * S)
        draw.rectangle([2 * S, 2 * S, w - 2 * S, h - 2 * S], fill=(40, 34, 30, 200))
    else:
        draw.rectangle([0, 0, w, h], fill=(214, 72, 60, 255))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    out = args.out
    os.makedirs(out, exist_ok=True)

    # 主角 idle 4 帧
    for i in range(4):
        img, d, w, h = canvas(64, 64)
        gorilla(d, w, h, i / 4, "idle")
        save_fit(img, 64, 64, os.path.join(out, f"Player_Idle_{i:02d}.png"))

    # 主角 run 6 帧
    for i in range(6):
        img, d, w, h = canvas(64, 64)
        gorilla(d, w, h, i / 6, "run")
        save_fit(img, 64, 64, os.path.join(out, f"Player_Run_{i:02d}.png"))

    # 跳跃 / 下落
    for name, pose in (("Jump", "jump"), ("Fall", "fall")):
        img, d, w, h = canvas(64, 64)
        gorilla(d, w, h, 0, pose)
        save_fit(img, 64, 64, os.path.join(out, f"Player_{name}_00.png"))

    # 敌人
    img, d, w, h = canvas(64, 64)
    enemy(d, w, h)
    save_fit(img, 64, 64, os.path.join(out, "Enemy_Walk_00.png"))

    # 道具
    img, d, w, h = canvas(32, 32)
    banana(d, w, h)
    save(img, 32, 32, os.path.join(out, "Pickup_Banana_00.png"))

    img, d, w, h = canvas(32, 32)
    poop(d, w, h)
    save(img, 32, 32, os.path.join(out, "Pickup_Poop_00.png"))

    img, d, w, h = canvas(32, 32)
    banana(d, w, h)
    d.ellipse([6 * S, 6 * S, 26 * S, 26 * S], outline=(255, 255, 255, 180), width=2 * S)
    save(img, 32, 32, os.path.join(out, "Pickup_SpecialBanana_00.png"))

    # 地砖
    for i in range(2):
        img, d, w, h = canvas(64, 64)
        tile(d, w, h, seed=i + 1)
        save(img, 64, 64, os.path.join(out, f"Tile_Ground_{i:02d}.png"))

    # UI
    img, d, w, h = canvas(64, 10)
    ui_bar(d, w, h, frame=True)
    save(img, 64, 10, os.path.join(out, "UI_HealthBar_Frame.png"))

    img, d, w, h = canvas(64, 10)
    ui_bar(d, w, h, frame=False)
    save(img, 64, 10, os.path.join(out, "UI_HealthBar_Fill.png"))

    print(f"generated -> {out}")
    for f in sorted(os.listdir(out)):
        print(" ", f)


if __name__ == "__main__":
    main()
