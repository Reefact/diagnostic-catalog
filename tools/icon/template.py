"""Reading assets/icon-template.svg, and turning it into pixels.

Shared by check-icon-template.py and render-icon.py so that the mark they compare and the mark
they draw come from one reading of one file. Two readings would drift, and the failure would be
a check that passes over icons it no longer describes.

No third-party dependency, by the same reasoning the scripts beside it carry: this inflates a PNG
and fills a path, which the POSIX sh the rest of tools/ is written in cannot do, and it decides
nothing about a release, which is what ADR-0013 constrains.
"""

import math
import re
import struct
import xml.etree.ElementTree as ET
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SVG = ROOT / "assets" / "icon-template.svg"
NS = "{http://www.w3.org/2000/svg}"

ARC_STEPS = 400     # ~0.002 px of chord error at the C's radius
SUBSAMPLES = 4      # vertical only: the scanline pass is exact horizontally


# --- PNG ------------------------------------------------------------------------------------

def read_png(path):
    """(width, height, rows of RGBA). Written out because there is no image dependency here."""
    data = Path(path).read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path} is not a PNG")

    idat, palette, transparency = b"", None, None
    offset = 8
    while offset < len(data):
        (length,) = struct.unpack(">I", data[offset:offset + 4])
        kind = data[offset + 4:offset + 8]
        body = data[offset + 8:offset + 8 + length]
        if kind == b"IHDR":
            width, height, depth, colour, _c, _f, interlace = struct.unpack(">IIBBBBB", body)
        elif kind == b"IDAT":
            idat += body
        elif kind == b"PLTE":
            palette = body
        elif kind == b"tRNS":
            transparency = body
        offset += 12 + length

    if depth != 8 or interlace:
        raise ValueError(f"{path}: only 8-bit non-interlaced PNGs are read here")

    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[colour]
    stride = width * channels
    raw = zlib.decompress(idat)

    out = bytearray(height * stride)
    previous = bytearray(stride)
    pos = 0
    for y in range(height):
        method = raw[pos]
        pos += 1
        line = bytearray(raw[pos:pos + stride])
        pos += stride
        if method == 1:
            for x in range(channels, stride):
                line[x] = (line[x] + line[x - channels]) & 255
        elif method == 2:
            for x in range(stride):
                line[x] = (line[x] + previous[x]) & 255
        elif method == 3:
            for x in range(stride):
                left = line[x - channels] if x >= channels else 0
                line[x] = (line[x] + ((left + previous[x]) >> 1)) & 255
        elif method == 4:
            for x in range(stride):
                left = line[x - channels] if x >= channels else 0
                up = previous[x]
                upleft = previous[x - channels] if x >= channels else 0
                estimate = left + up - upleft
                dl, du, dul = abs(estimate - left), abs(estimate - up), abs(estimate - upleft)
                line[x] = (line[x] + (left if dl <= du and dl <= dul else
                                      up if du <= dul else upleft)) & 255
        elif method != 0:
            raise ValueError(f"{path}: unknown filter {method} on row {y}")
        out[y * stride:(y + 1) * stride] = line
        previous = line

    rows = []
    for y in range(height):
        row = []
        for x in range(width):
            at = y * stride + x * channels
            if colour == 6:
                row.append(tuple(out[at:at + 4]))
            elif colour == 2:
                row.append((out[at], out[at + 1], out[at + 2], 255))
            elif colour == 3:
                i = out[at]
                a = transparency[i] if transparency and i < len(transparency) else 255
                row.append((palette[i * 3], palette[i * 3 + 1], palette[i * 3 + 2], a))
            elif colour == 4:
                row.append((out[at],) * 3 + (out[at + 1],))
            else:
                row.append((out[at],) * 3 + (255,))
        rows.append(row)
    return width, height, rows


def write_png(path, width, height, rows):
    """8-bit RGBA, one IDAT, filter 0 on every scanline. Deterministic for a given input."""
    raw = bytearray()
    for row in rows:
        raw.append(0)
        for pixel in row:
            raw += bytes(pixel)

    def chunk(tag, body):
        return (struct.pack(">I", len(body)) + tag + body
                + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF))

    Path(path).write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + chunk(b"IEND", b""))


# --- the template ---------------------------------------------------------------------------

def _arc(x1, y1, radius, large, sweep, x2, y2):
    """Endpoint-to-centre conversion, per the SVG implementation notes. Circular arcs only."""
    dx, dy = (x1 - x2) / 2.0, (y1 - y2) / 2.0
    span = radius * radius * (dx * dx + dy * dy)
    scale = math.sqrt(max((radius ** 4 - span) / span, 0.0))
    if large == sweep:
        scale = -scale
    cxp, cyp = scale * dy, -scale * dx
    cx, cy = cxp + (x1 + x2) / 2.0, cyp + (y1 + y2) / 2.0

    def angle(ux, uy, vx, vy):
        cosine = (ux * vx + uy * vy) / (math.hypot(ux, uy) * math.hypot(vx, vy))
        value = math.acos(max(-1.0, min(1.0, cosine)))
        return -value if ux * vy - uy * vx < 0 else value

    start = angle(1, 0, dx - cxp, dy - cyp)
    sweep_angle = angle(dx - cxp, dy - cyp, -dx - cxp, -dy - cyp)
    if sweep == 0 and sweep_angle > 0:
        sweep_angle -= 2 * math.pi
    elif sweep == 1 and sweep_angle < 0:
        sweep_angle += 2 * math.pi
    return [(cx + radius * math.cos(start + sweep_angle * i / ARC_STEPS),
             cy + radius * math.sin(start + sweep_angle * i / ARC_STEPS))
            for i in range(1, ARC_STEPS + 1)]


_TOKEN = re.compile(r"[MLAZ]|-?\d+(?:\.\d+)?")


def flatten(d):
    """One closed polyline from a path's M/L/A/Z. The template uses no other command."""
    tokens = _TOKEN.findall(d)
    points, i, current = [], 0, None
    while i < len(tokens):
        command = tokens[i]
        i += 1
        if command in ("M", "L"):
            current = (float(tokens[i]), float(tokens[i + 1]))
            i += 2
            points.append(current)
        elif command == "A":
            radius, _ry, _rot, large, sweep, x, y = (float(tokens[i + k]) for k in range(7))
            i += 7
            points.extend(_arc(current[0], current[1], radius, int(large), int(sweep), x, y))
            current = (x, y)
        elif command != "Z":
            raise ValueError(f"unsupported path command {command!r}")
    return points


def rounded_rect(x, y, width, height, radius):
    points = []
    for cx, cy, from_angle in ((x + width - radius, y + radius, -math.pi / 2),
                               (x + width - radius, y + height - radius, 0.0),
                               (x + radius, y + height - radius, math.pi / 2),
                               (x + radius, y + radius, math.pi)):
        points += [(cx + radius * math.cos(from_angle + math.pi / 2 * i / ARC_STEPS),
                    cy + radius * math.sin(from_angle + math.pi / 2 * i / ARC_STEPS))
                   for i in range(ARC_STEPS + 1)]
    return points


class Template:
    """The mark, the badge plate and the gradient, as the SVG declares them."""

    def __init__(self, path=SVG):
        root = ET.parse(path).getroot()
        viewbox = [float(v) for v in root.get("viewBox").split()]
        self.width, self.height = int(viewbox[2]), int(viewbox[3])

        group = root.find(NS + "g")
        if group is None:
            raise ValueError(f"{path} has no <g> holding the mark")
        self.contours = [flatten(p.get("d")) for p in group.findall(NS + "path")]

        plate = group.find(NS + "rect")
        if plate is None:
            raise ValueError(f"{path} has no <rect> for the badge plate")
        self.plate = tuple(float(plate.get(k)) for k in ("x", "y", "width", "height"))
        self.plate_radius = float(plate.get("rx"))
        self.contours.append(rounded_rect(*self.plate, self.plate_radius))

        gradient = next(root.iter(NS + "linearGradient"))
        self.stops = [s.get("stop-color").upper() for s in gradient.findall(NS + "stop")]
        self.gradient_span = (float(gradient.get("y1")), float(gradient.get("y2")))

    @property
    def plate_centre(self):
        return self.plate[0] + self.plate[2] / 2, self.plate[1] + self.plate[3] / 2

    def ink(self, y):
        """The gradient's colour on a given row, which every shape shares."""
        y0, y1 = self.gradient_span
        t = ((y + 0.5) - y0) / (y1 - y0)
        first = tuple(int(self.stops[0][i:i + 2], 16) for i in (1, 3, 5))
        last = tuple(int(self.stops[-1][i:i + 2], 16) for i in (1, 3, 5))
        return tuple(int(round(first[i] + (last[i] - first[i]) * t)) for i in range(3))


def coverage(contours, width, height):
    """Scanline fill: exact horizontally, SUBSAMPLES rows per pixel vertically.

    Each contour is filled even-odd on its own and the results are unioned, so overlapping
    shapes add up to ink rather than cancel — a mark whose C moved over a bracket must not
    silently punch a hole through it.
    """
    edges = []
    for contour in contours:
        segments = []
        for i, (x1, y1) in enumerate(contour):
            x2, y2 = contour[(i + 1) % len(contour)]
            if y1 != y2:
                segments.append((min(y1, y2), max(y1, y2), x1, y1, x2, y2))
        edges.append(segments)

    grid = [[0.0] * width for _ in range(height)]
    for row in range(height * SUBSAMPLES):
        y = (row + 0.5) / SUBSAMPLES
        spans = []
        for segments in edges:
            crossings = sorted(x1 + (x2 - x1) * (y - y1) / (y2 - y1)
                               for lo, hi, x1, y1, x2, y2 in segments if lo <= y < hi)
            spans += [(crossings[i], crossings[i + 1]) for i in range(0, len(crossings) - 1, 2)]
        if not spans:
            continue
        spans.sort()
        merged = [list(spans[0])]
        for start, end in spans[1:]:
            if start <= merged[-1][1]:
                merged[-1][1] = max(merged[-1][1], end)
            else:
                merged.append([start, end])
        target = grid[row // SUBSAMPLES]
        for start, end in merged:
            start, end = max(start, 0.0), min(end, float(width))
            if end <= start:
                continue
            for x in range(int(start), min(int(end), width - 1) + 1):
                target[x] += (min(end, x + 1.0) - max(start, float(x))) / SUBSAMPLES
    return grid
