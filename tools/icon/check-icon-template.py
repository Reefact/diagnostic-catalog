#!/usr/bin/env python3
"""Check that assets/icon-template.svg still draws the mark the catalogue icons wear.

    tools/icon/check-icon-template.py                  # every src/*/icon.png
    tools/icon/check-icon-template.py path/to/icon.png # a candidate, before committing it

Exit status: 0 = the template and the icons agree, 1 = they do not.

WHY THIS EXISTS. The template carries a claim — that its geometry was recovered from the
shipped icons and reproduces them — and a claim in a comment is exactly the kind this
repository does not leave unchecked. Two things can break it, both silently: somebody edits
the template, or somebody draws a new catalogue's icon by hand instead of exporting it.
Neither is visible to PackageIconTests, which asserts that no two catalogues ship the SAME
icon and never asks what any of them looks like (ADR-0032).

WHAT IT DOES NOT CHECK. The badge lettering. The template sets it as <text>, so it renders
with whatever font the exporter resolved, and the four shipped badges predate the template
and were set in a font nobody recorded. The plate's interior is therefore excluded from the
comparison and the letters remain a matter for review — which is the trade ADR-0032 records.

WHY PYTHON, IN A tools/ THAT IS OTHERWISE POSIX SH. ADR-0013 fixes the dialect of the shell
tooling because those scripts decide what a release publishes and must run wherever a release
runs. This one decides nothing about a release: it inflates a PNG, rasterises a path and
compares two arrays of numbers, which sh cannot do at all and which no release depends on.
The stdlib is the whole dependency — zlib, struct, xml.etree — so there is nothing to install
beyond an interpreter the runners already carry.
"""

import math
import re
import struct
import sys
import xml.etree.ElementTree as ET
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEMPLATE = ROOT / "assets" / "icon-template.svg"
SVG_NS = "{http://www.w3.org/2000/svg}"

# Measured against the icons this template was recovered from: RMS 0.068, worst pixel 0.461
# at the tip of the C's lower terminal, identically for all four. The bars sit above those
# rather than at them, because a legitimate re-export through a different rasteriser moves a
# sharp corner by a fraction of a pixel and must not be reported as drift. They are still well
# under what a real mistake costs: a systematic shift of a third of a pixel — the difference
# between the fitted geometry and the same shape rounded to whole units — already reads 0.12.
MAX_RMS = 0.15
MAX_WORST = 0.65

# Per-channel tolerance when comparing the template's gradient stops with the ink of a PNG,
# extrapolated from two rows. The stops are whole 8-bit values and the extrapolation is exact
# on a linear ramp, so this absorbs rounding and nothing else.
MAX_STOP_DRIFT = 3

SUBSAMPLES = 4          # vertical only: the scanline pass is exact horizontally
ARC_STEPS = 400         # flattening resolution, ~0.002 px of chord error at r = 96


# --- PNG ----------------------------------------------------------------------------------

def read_png(path):
    """Return (width, height, rows of RGBA tuples). No third-party decoder, no PIL."""
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path} is not a PNG")

    idat, palette, transparency = b"", None, None
    offset = 8
    while offset < len(data):
        (length,) = struct.unpack(">I", data[offset:offset + 4])
        kind = data[offset + 4:offset + 8]
        body = data[offset + 8:offset + 8 + length]
        if kind == b"IHDR":
            width, height, depth, colour, _comp, _filter, interlace = struct.unpack(">IIBBBBB", body)
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

    # Undo the per-scanline filters. Written out rather than delegated because the whole
    # point of this file is to have no image dependency.
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
                if dl <= du and dl <= dul:
                    line[x] = (line[x] + left) & 255
                elif du <= dul:
                    line[x] = (line[x] + up) & 255
                else:
                    line[x] = (line[x] + upleft) & 255
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
                index = out[at]
                alpha = transparency[index] if transparency and index < len(transparency) else 255
                row.append((palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2], alpha))
            elif colour == 4:
                row.append((out[at],) * 3 + (out[at + 1],))
            else:
                row.append((out[at],) * 3 + (255,))
        rows.append(row)
    return width, height, rows


# --- SVG ----------------------------------------------------------------------------------

TOKEN = re.compile(r"[MLAZ]|-?\d+(?:\.\d+)?")


def arc_points(x1, y1, radius, large, sweep, x2, y2):
    """Endpoint-to-centre conversion, per the SVG implementation notes. Circular arcs only."""
    dx, dy = (x1 - x2) / 2.0, (y1 - y2) / 2.0
    numerator = radius * radius * radius * radius - radius * radius * (dy * dy + dx * dx)
    denominator = radius * radius * (dy * dy + dx * dx)
    scale = math.sqrt(max(numerator / denominator, 0.0))
    if large == sweep:
        scale = -scale
    cxp, cyp = scale * dy, -scale * dx
    cx, cy = cxp + (x1 + x2) / 2.0, cyp + (y1 + y2) / 2.0

    def angle(ux, uy, vx, vy):
        cosine = (ux * vx + uy * vy) / (math.hypot(ux, uy) * math.hypot(vx, vy))
        value = math.acos(max(-1.0, min(1.0, cosine)))
        return -value if ux * vy - uy * vx < 0 else value

    start = angle(1, 0, dx - cxp, dy - cyp)
    span = angle(dx - cxp, dy - cyp, -dx - cxp, -dy - cyp)
    if sweep == 0 and span > 0:
        span -= 2 * math.pi
    elif sweep == 1 and span < 0:
        span += 2 * math.pi
    return [(cx + radius * math.cos(start + span * i / ARC_STEPS),
             cy + radius * math.sin(start + span * i / ARC_STEPS))
            for i in range(1, ARC_STEPS + 1)]


def flatten(d):
    """Turn a path's M/L/A/Z into one closed polyline. The template uses no other command."""
    tokens = TOKEN.findall(d)
    points, i, current = [], 0, None
    while i < len(tokens):
        command = tokens[i]
        i += 1
        if command in ("M", "L"):
            current = (float(tokens[i]), float(tokens[i + 1]))
            i += 2
            points.append(current)
        elif command == "A":
            radius, _ry, _rotation, large, sweep, x, y = (float(tokens[i + k]) for k in range(7))
            i += 7
            points.extend(arc_points(current[0], current[1], radius, int(large), int(sweep), x, y))
            current = (x, y)
        elif command == "Z":
            pass
        else:
            raise ValueError(f"unsupported path command {command!r}")
    return points


def rounded_rect(x, y, width, height, radius):
    quarter = []
    for cx, cy, from_angle in ((x + width - radius, y + radius, -math.pi / 2),
                               (x + width - radius, y + height - radius, 0.0),
                               (x + radius, y + height - radius, math.pi / 2),
                               (x + radius, y + radius, math.pi)):
        quarter += [(cx + radius * math.cos(from_angle + math.pi / 2 * i / ARC_STEPS),
                     cy + radius * math.sin(from_angle + math.pi / 2 * i / ARC_STEPS))
                    for i in range(ARC_STEPS + 1)]
    return quarter


def read_template():
    root = ET.parse(TEMPLATE).getroot()
    viewbox = [float(v) for v in root.get("viewBox").split()]
    group = root.find(SVG_NS + "g")
    if group is None:
        raise ValueError("the template has no <g> holding the mark")

    contours = [flatten(path.get("d")) for path in group.findall(SVG_NS + "path")]
    plate = group.find(SVG_NS + "rect")
    if plate is None:
        raise ValueError("the template has no <rect> for the badge plate")
    box = tuple(float(plate.get(k)) for k in ("x", "y", "width", "height"))
    contours.append(rounded_rect(*box, float(plate.get("rx"))))

    gradient = root.iter(SVG_NS + "linearGradient").__next__()
    stops = [stop.get("stop-color").upper() for stop in gradient.findall(SVG_NS + "stop")]
    span = (float(gradient.get("y1")), float(gradient.get("y2")))
    return viewbox, contours, box, stops, span


# --- rasterise ----------------------------------------------------------------------------

def coverage(contours, width, height):
    """Scanline fill: exact horizontally, SUBSAMPLES rows per pixel vertically.

    Each contour is filled even-odd on its own and the results are unioned, which is what
    makes overlapping shapes add up to ink rather than cancel — the C sits between the
    brackets today, but a template that moved it must not silently punch a hole instead.
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


# --- comparison ---------------------------------------------------------------------------

def compare_shape(grid, rows, width, height, lettering):
    """RMS and worst per-pixel disagreement, over the pixels either side calls an edge."""
    left, top, right, bottom = lettering
    total, counted, worst, where = 0.0, 0, 0.0, None
    for y in range(height):
        for x in range(width):
            if left <= x <= right and top <= y <= bottom:
                continue
            actual = rows[y][x][3] / 255.0
            drawn = grid[y][x]
            if actual == drawn and actual in (0.0, 1.0):
                continue
            error = abs(drawn - actual)
            total += error * error
            counted += 1
            if error > worst:
                worst, where = error, (x, y)
    if counted == 0:
        raise ValueError("no edge pixel was compared, which means nothing was checked")
    return math.sqrt(total / counted), worst, where, counted


def compare_gradient(rows, span, stops, box):
    """Extrapolate the PNG's ink back to the template's two stops.

    Sampled from a column well left of the badge, so a catalogue whose lettering reaches
    unusually far cannot land on the probe. The ramp is linear in sRGB — measured, not
    assumed — so two rows determine it.

    Returns None when the probe misses the ink entirely, which means the shape is not where
    the template puts it — already reported by the comparison above, and not worth a crash.
    """
    x = int(box[0] // 8)
    y1, y2 = 100, 430
    first, second = rows[y1][x], rows[y2][x]
    if first[3] != 255 or second[3] != 255:
        return None
    ends = []
    for at in span:
        channels = tuple(round(first[c] + (first[c] - second[c]) * (y1 - at) / (y2 - y1))
                         for c in range(3))
        ends.append("#%02X%02X%02X" % channels)
    drift = max(abs(int(found[i:i + 2], 16) - int(declared[i:i + 2], 16))
                for found, declared in zip(ends, stops) for i in (1, 3, 5))
    return ends, drift


# --- entry point --------------------------------------------------------------------------

def main(argv):
    if any(a in ("-h", "--help") for a in argv):
        print(__doc__)
        return 0

    icons = [Path(a) for a in argv] or sorted(ROOT.glob("src/*/icon.png"))
    if not icons:
        # A discovery that finds nothing must not report success: that is the same failure
        # tools/tests/run.sh refuses, and here it would mean the template is unchecked.
        print("no icon found under src/*/icon.png — nothing was checked", file=sys.stderr)
        return 1

    viewbox, contours, box, stops, span = read_template()
    width, height = int(viewbox[2]), int(viewbox[3])
    grid = coverage(contours, width, height)

    # The plate's interior, where the knocked-out letters live and the template cannot follow.
    # Inset by 12% of the plate: the widest ink measured on the four badges spans x 379..472
    # and y 340..407, so this clears every one of them by at least 7px while leaving the
    # plate's edges — and the outer part of its corners — inside the comparison.
    inset = box[2] * 0.12
    lettering = (box[0] + inset, box[1] + inset, box[0] + box[2] - inset, box[1] + box[3] - inset)

    print(f"template  {TEMPLATE.relative_to(ROOT)}  {stops[0]} -> {stops[-1]}")
    failures = 0
    for icon in icons:
        # A candidate export sits wherever whoever drew it saved it, which is routinely
        # outside the repository — so shorten the path when it is inside and leave it alone
        # when it is not, rather than assuming.
        try:
            name = icon.resolve().relative_to(ROOT)
        except ValueError:
            name = icon
        try:
            png_width, png_height, rows = read_png(icon)
        except (OSError, ValueError, KeyError) as unreadable:
            # A file this cannot read is a failure, never a skip. Skipping is how a check
            # reports success over the thing it did not look at.
            print(f"  FAIL {name}: {unreadable}")
            failures += 1
            continue

        if (png_width, png_height) != (width, height):
            print(f"  FAIL {name}: {png_width}x{png_height}, expected {width}x{height}")
            failures += 1
            continue

        rms, worst, where, counted = compare_shape(grid, rows, width, height, lettering)
        measured = compare_gradient(rows, span, stops, box)
        bad = rms > MAX_RMS or worst > MAX_WORST or measured is None or measured[1] > MAX_STOP_DRIFT
        failures += bad
        print(f"  {'FAIL' if bad else 'ok  '} {name}")
        print(f"         shape    rms {rms:.3f} (max {MAX_RMS})   "
              f"worst {worst:.3f} at {where} (max {MAX_WORST})   {counted} edge px")
        if measured is None:
            print("         gradient not measurable — the ink is not where the template puts it")
        else:
            ends, drift = measured
            print(f"         gradient {ends[0]} -> {ends[-1]}   "
                  f"drift {drift} (max {MAX_STOP_DRIFT})")

    if failures:
        print(f"\n{failures} icon(s) disagree with the template.\n"
              "Either the icon was not exported from assets/icon-template.svg, or the template\n"
              "was changed and no longer draws the mark the published packages wear. Redraw the\n"
              "icon from the template, or — if the mark itself is meant to move — say so, and\n"
              "move every icon with it.", file=sys.stderr)
        return 1

    print(f"\n{len(icons)} icon(s) match the template")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
