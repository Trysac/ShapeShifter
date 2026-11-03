"""A tiny RGBA image with a PNG writer (standard library only)."""

import struct
import zlib


def hex_color(text, alpha=255):
    text = text.lstrip("#")
    return (int(text[0:2], 16), int(text[2:4], 16), int(text[4:6], 16), alpha)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4))


class Image:
    def __init__(self, width, height, fill=(0, 0, 0, 0)):
        self.width = width
        self.height = height
        self.pixels = [list(fill) for _ in range(width * height)]

    def get(self, x, y):
        if 0 <= x < self.width and 0 <= y < self.height:
            return tuple(self.pixels[y * self.width + x])
        return (0, 0, 0, 0)

    def set(self, x, y, color):
        """Sets a pixel (y grows downwards). Colours with alpha < 255 blend over what is there."""
        if not (0 <= x < self.width and 0 <= y < self.height) or color is None:
            return
        if color[3] >= 255:
            self.pixels[y * self.width + x] = list(color)
            return
        if color[3] <= 0:
            return
        below = self.get(x, y)
        a = color[3] / 255
        out_a = a + below[3] / 255 * (1 - a)
        if out_a <= 0:
            return
        rgb = [int(round((color[i] * a + below[i] * below[3] / 255 * (1 - a)) / out_a)) for i in range(3)]
        self.pixels[y * self.width + x] = rgb + [int(round(out_a * 255))]

    def rect(self, x0, y0, w, h, color):
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                self.set(x, y, color)

    def stamp(self, x0, y0, rows, palette, flip=False):
        """Draws an ASCII template: each character is a palette key, '.' is transparent."""
        width = max(len(r) for r in rows)
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in ". ":
                    continue
                px = x0 + (width - 1 - x if flip else x)
                self.set(px, y0 + y, palette[ch])

    def paste(self, other, x0, y0):
        for y in range(other.height):
            for x in range(other.width):
                c = other.get(x, y)
                if c[3]:
                    self.set(x0 + x, y0 + y, c)

    def scaled(self, factor):
        out = Image(self.width * factor, self.height * factor)
        for y in range(out.height):
            for x in range(out.width):
                out.pixels[y * out.width + x] = list(self.get(x // factor, y // factor))
        return out

    def save(self, path):
        raw = bytearray()
        for y in range(self.height):
            raw.append(0)
            for x in range(self.width):
                raw.extend(bytes(self.pixels[y * self.width + x]))

        def chunk(kind, data):
            body = kind + data
            return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

        header = struct.pack(">IIBBBBB", self.width, self.height, 8, 6, 0, 0, 0)
        with open(path, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\n")
            f.write(chunk(b"IHDR", header))
            f.write(chunk(b"IDAT", zlib.compress(bytes(raw), 9)))
            f.write(chunk(b"IEND", b""))
