#!/usr/bin/env python3
"""Check that assets/icon-template.svg still draws the mark the catalogue icons wear.

    tools/icon/check-icon-template.py                  # every src/*/icon.png
    tools/icon/check-icon-template.py path/to/icon.png # a candidate, before committing it
    tools/icon/check-icon-template.py --root DIR       # the same, against another tree

Exit status: 0 = the template and the icons agree, 1 = they do not.

WHY THIS EXISTS. The template carries a claim — that its geometry was recovered from the
shipped icons and reproduces them — and a claim in a comment is exactly the kind this
repository does not leave unchecked. Two things can break it, both silently: somebody edits
the template, or somebody draws a new catalogue's icon by hand instead of exporting it.
Neither is visible to PackageIconTests, which asserts that no two catalogues ship the SAME
icon and never asks what any of them looks like (ADR-0032).

WHAT IT DOES NOT CHECK. The badge lettering. The plate's interior is excluded from the
comparison, so what the letters say stays a matter for review — the trade ADR-0032 records.
What draws them is no longer a mystery, though: render-icon.py sets them, from the font the
template names, so a badge that differs is one somebody drew by hand.

WHY PYTHON, IN A tools/ THAT IS OTHERWISE POSIX SH. ADR-0013 fixes the dialect of the shell
tooling because those scripts decide what a release publishes and must run wherever a release
runs. This one decides nothing about a release: it inflates a PNG, rasterises a path and
compares two arrays of numbers, which sh cannot do at all and which no release depends on.
The stdlib is the whole dependency — zlib, struct, xml.etree — so there is nothing to install
beyond an interpreter the runners already carry.
"""

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import badges                                                           # noqa: E402
import template as tpl                                                  # noqa: E402

ROOT = tpl.ROOT

# The icons in the repository are drawn by render-icon.py from this same template, so they now
# report RMS 0.001. The bars stay where they were set against the hand-drawn ones — 0.068 with a
# worst pixel of 0.461 — rather than being tightened onto that, because an icon exported from an
# SVG editor instead of the renderer is legitimate and lands between the two. What they still
# catch is the case worth catching: a mark drawn by hand, which reads 0.9 and above.
MAX_RMS = 0.15
MAX_WORST = 0.65

# Per-channel tolerance when comparing the template's gradient stops with the ink of a PNG,
# extrapolated from two rows. The stops are whole 8-bit values and the extrapolation is exact
# on a linear ramp, so this absorbs rounding and nothing else.
MAX_STOP_DRIFT = 3

SUBSAMPLES = 4          # vertical only: the scanline pass is exact horizontally
ARC_STEPS = 400         # flattening resolution, ~0.002 px of chord error at r = 96


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

    root = ROOT
    if "--root" in argv:
        at = argv.index("--root")
        root = Path(argv[at + 1]).resolve()
        argv = argv[:at] + argv[at + 2:]

    # Scanning is what the roster check applies to: given explicit paths, the caller is asking
    # about a candidate that has no project yet, and answering with a complaint about the table
    # would be answering a question nobody asked.
    scanning = not argv
    template_path = root / "assets" / "icon-template.svg"

    icons = [Path(a) for a in argv] or sorted(root.glob("src/*/icon.png"))
    if not icons:
        # A discovery that finds nothing must not report success: that is the same failure
        # tools/tests/run.sh refuses, and here it would mean the template is unchecked.
        print("no icon found under src/*/icon.png — nothing was checked", file=sys.stderr)
        return 1

    mark = tpl.Template(template_path)
    width, height = mark.width, mark.height
    box, stops, span = mark.plate, mark.stops, mark.gradient_span
    grid = tpl.coverage(mark.contours, width, height)

    # The plate's interior, where the knocked-out letters live and the template cannot follow.
    # Inset by 12% of the plate: the widest ink measured on the four badges spans x 379..472
    # and y 340..407, so this clears every one of them by at least 7px while leaving the
    # plate's edges — and the outer part of its corners — inside the comparison.
    inset = box[2] * 0.12
    lettering = (box[0] + inset, box[1] + inset, box[0] + box[2] - inset, box[1] + box[3] - inset)

    print(f"template  {template_path.relative_to(root)}  {stops[0]} -> {stops[-1]}")
    failures = 0

    # Every icon in the tree is declared, and everything declared is in the tree. Counted apart
    # from the comparison below because it is a different complaint: an undeclared catalogue is
    # not an icon that disagrees with the template, it is an icon nothing draws — and the
    # comparison passes it happily, as it did for the one that arrived hand-drawn.
    undeclared = badges.roster(root) if scanning else []
    for complaint in undeclared:
        print(f"  FAIL {complaint}")

    for icon in icons:
        # A candidate export sits wherever whoever drew it saved it, which is routinely
        # outside the repository — so shorten the path when it is inside and leave it alone
        # when it is not, rather than assuming.
        try:
            name = icon.resolve().relative_to(root)
        except ValueError:
            name = icon
        try:
            png_width, png_height, rows = tpl.read_png(icon)
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
    if undeclared:
        print(f"\n{len(undeclared)} catalogue(s) and the badge table disagree about what exists.\n"
              "tools/icon/badges.py is the roster render-icon.py draws from, so a catalogue "
              "missing\nfrom it is one nothing can redraw.", file=sys.stderr)
    if failures or undeclared:
        return 1

    print(f"\n{len(icons)} icon(s) match the template"
          + (", and the badge table names every one of them" if scanning else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
