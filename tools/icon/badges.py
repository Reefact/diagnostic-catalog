"""Which badge each catalogue's icon wears.

The roster, in one place, because two things need it and they need to agree: render-icon.py
draws from it, and check-icon-template.py fails a catalogue that is missing from it. Kept out of
either script so that neither owns it — a table one tool defines and another reaches into is a
table that acquires a second copy the first time the reach is inconvenient.

Three letters at most, abbreviating the rule prefix when the prefix is longer (ADR-0033). The
prefix itself is not written down here: it is what the catalogue's own generated source calls
its rules, and a second copy of it would be one more thing to keep true.

A catalogue absent from this table is a check failure, not a default. That is the point of the
table: DiagnosticCatalog.AspNetCore arrived while the renderer was being merged, its icon was
drawn by hand, its mark happened to be right, and nothing said so.
"""

import ast
from pathlib import Path

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
    "DiagnosticCatalog.Syslib": "SYS",
    "DiagnosticCatalog.Roslyn": "RS",
    "DiagnosticCatalog.PublicApi": "API",
    "DiagnosticCatalog.BannedApi": "BAN",
}


def _declared_twice():
    """Rows this file declares more than once, read off its own source.

    A dict literal keeps the last value for a repeated key and reports nothing, so two rows for
    one catalogue with two different badges is a silent choice between them. It is not
    hypothetical: the row for DiagnosticCatalog.AspNetCore was added a second time while
    DiagnosticCatalog.Syslib was being added, and both values happened to agree.
    """
    for node in ast.walk(ast.parse(Path(__file__).read_text(encoding="utf-8"))):
        if isinstance(node, ast.Dict) and any(
                isinstance(k, ast.Constant) and k.value in BADGES for k in node.keys):
            keys = [k.value for k in node.keys if isinstance(k, ast.Constant)]
            return sorted({key for key in keys if keys.count(key) > 1})
    return []


def roster(root):
    """What the table and the tree disagree about, as a list of sentences. Empty means they agree.

    Both directions, because each catches a different mistake: a project with an icon and no row
    is a catalogue somebody drew by hand, and a row with no project is a table left behind by a
    rename.
    """
    with_icon = {path.parent.name for path in root.glob("src/*/icon.png")}
    complaints = []

    for project in _declared_twice():
        complaints.append(
            f"tools/icon/badges.py declares src/{project} twice; a dict literal keeps the last "
            "row and says nothing about the other")

    for project in sorted(with_icon - set(BADGES)):
        complaints.append(
            f"src/{project} ships an icon that tools/icon/render-icon.py cannot draw, because "
            "no badge is declared for it in tools/icon/badges.py. Add the row and run "
            "render-icon.py --all")

    for project in sorted(set(BADGES) - with_icon):
        if not (root / "src" / project).is_dir():
            complaints.append(
                f"tools/icon/badges.py declares a badge for src/{project}, which does not exist")
        else:
            complaints.append(
                f"src/{project} has a badge declared but no icon.png; run render-icon.py --all")

    return complaints
