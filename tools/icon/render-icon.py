#!/usr/bin/env python3
"""Draw a catalogue's icon.png from assets/icon-template.svg and a badge string.

    tools/icon/render-icon.py SA src/DiagnosticCatalog.StyleCop/icon.png
    tools/icon/render-icon.py --all          # redraw every catalogue from eng/catalogs.json

Exit status: 0 = written, 1 = refused.

WHY THIS EXISTS. Until it did, the eight icons were the only record of what the family mark
looks like: four PNGs with no vector source, then eight, drawn by hand from sight. The template
recovered the geometry, and this closes the loop — an icon becomes an export rather than a
reconstruction, so the next catalogue's badge is a one-line command and not a drawing session.

THE LETTERING. Set in the font the template names, `Arial, Helvetica, sans-serif`, resolved here
to Liberation Sans Bold — metrically the same face as Arial Bold, which is what a renderer on
Windows or macOS picks up from that same stack. That is the whole reason the stack is worth
keeping: the export is reproducible off the shelf, with no font committed and no licence to
carry. Point --font at another file to see a different one; the icons in the repository were
drawn with the default.

THE SIZE. Recovered from the badges rather than chosen: each is set as large as it can be
without its ink exceeding MAX_INK wide or MAX_CAP tall. That one rule reproduces every size the
hand-drawn badges used — 68 for a single letter, which is height-limited, and 48 and 39 for two
and three, which are width-limited. Counting letters does not: `MST` is wider per unit of height
than `IDE` and would have overflowed the plate.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import template as tpl                                                  # noqa: E402
from truetype import Font                                               # noqa: E402

DEFAULT_FONT = "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"
MAX_CAP = 68.0
MAX_INK = 93.0

# The badge each catalogue wears. Three letters at most, abbreviating a longer rule prefix
# (ADR-0033); the prefix itself is what the catalogue's generated source calls its rules.
BADGES = {
    "DiagnosticCatalog.AspNetCore": "ASP",
    "DiagnosticCatalog.Sonar": "S",
    "DiagnosticCatalog.NetAnalyzers": "CA",
    "DiagnosticCatalog.CodeStyle": "IDE",
    "DiagnosticCatalog.StyleCop": "SA",
    "DiagnosticCatalog.Trimming": "IL",
    "DiagnosticCatalog.Xunit": "XU",
    "DiagnosticCatalog.NUnit": "NU",
    "DiagnosticCatalog.MSTest": "MST",
    "DiagnosticCatalog.AspNetCore": "ASP",
    "DiagnosticCatalog.Syslib": "SYS",
}


def badge_contours(font, text, plate):
    """The word's outlines, scaled to fit and centred on the plate."""
    heights = [p[1] for c in font.contours("H") for p in c]
    unit_cap = max(heights) - min(heights)

    placed, pen = [], 0.0
    for character in text:
        outline = font.contours(character)
        if outline is None:
            raise ValueError(f"the font has no outline for {character!r}")
        placed.append([[(pen + x, y) for x, y in contour] for contour in outline])
        pen += font.advance(character)

    points = [p for glyph in placed for contour in glyph for p in contour]
    x0, x1 = min(p[0] for p in points), max(p[0] for p in points)
    y0, y1 = min(p[1] for p in points), max(p[1] for p in points)
    scale = min(MAX_CAP / unit_cap, MAX_INK / (x1 - x0))

    # Centre the INK rather than the advances, which is what the hand-drawn badges did: a word
    # ending in a letter with a wide right sidebearing otherwise sits visibly off-centre.
    cx, cy = plate.plate_centre
    ox = cx - (x0 + x1) / 2 * scale
    oy = cy + (y0 + y1) / 2 * scale
    return [[(ox + x * scale, oy - y * scale) for x, y in contour]
            for glyph in placed for contour in glyph]


def render(text, destination, font_path=DEFAULT_FONT):
    mark = tpl.Template()
    letters = badge_contours(Font(font_path), text, mark)

    shape = tpl.coverage(mark.contours, mark.width, mark.height)
    knockout = tpl.coverage(letters, mark.width, mark.height)

    rows = []
    for y in range(mark.height):
        red, green, blue = mark.ink(y)
        row = []
        for x in range(mark.width):
            # The letters are cut OUT of the plate rather than painted over it, so the icon is
            # one gradient and a set of holes and carries no white at all.
            alpha = shape[y][x] * (1.0 - min(knockout[y][x], 1.0))
            row.append((red, green, blue, int(round(max(0.0, min(1.0, alpha)) * 255))))
        rows.append(row)

    tpl.write_png(destination, mark.width, mark.height, rows)


def main(argv):
    if not argv or argv[0] in ("-h", "--help"):
        print(__doc__)
        return 0

    font = DEFAULT_FONT
    if "--font" in argv:
        at = argv.index("--font")
        font = argv[at + 1]
        argv = argv[:at] + argv[at + 2:]

    if argv[:1] == ["--all"]:
        for project, badge in sorted(BADGES.items()):
            destination = tpl.ROOT / "src" / project / "icon.png"
            if not destination.parent.is_dir():
                # A project named here and absent means the table has gone stale; saying so is
                # the point, since nothing else would notice.
                print(f"no such project: src/{project}", file=sys.stderr)
                return 1
            render(badge, destination, font)
            print(f"  {badge:<4} {destination.relative_to(tpl.ROOT)}")
        return 0

    if len(argv) != 2:
        print(__doc__, file=sys.stderr)
        return 1
    render(argv[0], argv[1], font)
    print(f"  {argv[0]:<4} {argv[1]}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
