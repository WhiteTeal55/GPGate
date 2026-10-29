"""从 GnuPG 官方 logo 生成 GPGate 图标；素材与许可见 src/GPGate/Assets/README.md。"""

import io
import struct
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "src" / "GPGate" / "Assets"
SOURCE = ASSETS / "source" / "gnupg-lock.png"
SMALL_SOURCE = ASSETS / "source" / "gnupg-lock-80x100tr.png"  # 小尺寸用不带外环的挂锁
WORK = 1024
SIZES = [16, 20, 24, 28, 32, 40, 48, 64, 96, 128, 256]
BMP_MAX = 64  # Shell 小图标只认 BMP/DIB 帧，PNG 帧会被忽略后放大低清帧
SHARP_MAX = 32  # 小尺寸会被点采样缩放，收紧 alpha 才不糊
SMALL_MAX = 32  # 到这个尺寸为止改用 SMALL_SOURCE 素材


def square(image: Path, pad: float) -> Image.Image:
    """裁掉透明边，按长边留 pad 边距居中放进正方形画布，再缩放到 WORK。"""
    art = Image.open(image).convert("RGBA")
    art = art.crop(art.getchannel("A").getbbox())

    side = int(max(art.size) * (1 + pad))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.alpha_composite(art, ((side - art.width) // 2, (side - art.height) // 2))

    return canvas.resize((WORK, WORK), Image.Resampling.LANCZOS)


def recolor(image: Image.Image, color: tuple[int, int, int]) -> Image.Image:
    layer = Image.new("RGBA", image.size, color + (255,))
    layer.putalpha(image.getchannel("A"))
    return layer


def sharpen_alpha(image: Image.Image) -> Image.Image:
    """收窄 alpha 过渡带，缩小后轮廓仍清晰。"""
    alpha = image.getchannel("A").point(lambda v: max(0, min(255, round((v - 70) * 255 / 115))))
    layer = image.copy()
    layer.putalpha(alpha)
    return layer


def dib_frame(image: Image.Image) -> bytes:
    """ICO 用的 32bpp DIB 帧（含 AND mask）。"""
    width, height = image.size
    pixels = image.load()
    header = struct.pack("<IiiHHIIiiII", 40, width, height * 2, 1, 32, 0, width * height * 4, 0, 0, 0, 0)

    body = bytearray()
    for y in range(height - 1, -1, -1):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            body += bytes((b, g, r, a))

    stride = (width + 31) // 32 * 4
    mask = bytearray()
    for y in range(height - 1, -1, -1):
        row = bytearray(stride)
        for x in range(width):
            if pixels[x, y][3] == 0:
                row[x >> 3] |= 0x80 >> (x & 7)
        mask += row

    return header + bytes(body) + bytes(mask)


def encode_frame(image: Image.Image) -> bytes:
    if image.width <= BMP_MAX:
        return dib_frame(image)

    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def write_ico(path: Path, frames: dict[int, Image.Image]) -> None:
    """合并成 ICO；大尺寸在前，Shell 默认图标会取到 256px。"""
    sizes = sorted(frames, reverse=True)
    entries: list[bytes] = []
    images: list[bytes] = []
    offset = 6 + 16 * len(sizes)

    for size in sizes:
        image = encode_frame(frames[size])

        dimension = 0 if size >= 256 else size
        entries.append(struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(image), offset))
        images.append(image)
        offset += len(image)

    path.write_bytes(struct.pack("<HHH", 0, 1, len(sizes)) + b"".join(entries) + b"".join(images))


def save_set(name: str, base: Image.Image, small: Image.Image) -> None:
    frames = {}
    for size in SIZES:
        frame = (small if size <= SMALL_MAX else base).resize((size, size), Image.Resampling.LANCZOS)
        frames[size] = sharpen_alpha(frame) if size <= SHARP_MAX else frame

    write_ico(ASSETS / f"{name}.ico", frames)
    frames[256].save(ASSETS / f"{name}.png", optimize=True)


def main() -> None:
    base = square(SOURCE, 0.04)
    # 挂锁瘦长，多留边距；素材本身单色，压成品牌蓝可去掉缩放振铃
    small = recolor(square(SMALL_SOURCE, 0.10), (0, 147, 221))
    save_set("app", base, small)
    save_set("app-off", recolor(base, (255, 255, 255)), recolor(small, (255, 255, 255)))
    save_set("app-off-light", recolor(base, (0, 0, 0)), recolor(small, (0, 0, 0)))


if __name__ == "__main__":
    main()
