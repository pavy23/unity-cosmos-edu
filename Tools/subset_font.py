#!/usr/bin/env python3
"""Cut the shipped CJK font down to the glyphs this exhibit actually draws.

Noto Sans KR ships ~65,000 glyphs and weighs 15.7 MB. It is a *dynamic*
UnityEngine.Font loaded from Resources, which means the whole file goes into
every player — and on WebGL that one asset was an eighth of the download.
The exhibit draws 1,900-odd non-ASCII characters.

Where the glyph list comes from: every character in the project's own C#
sources. That is not a heuristic — all four languages of every caption, label,
narration line and fact strip live in string literals in those files (Loc.T,
NarrationLines*, Facts*, ChipLines*), so scanning them is exhaustive by
construction. Comments are scanned too; they cost a few hundred Hangul
syllables and remove any need to reason about which text is displayed.

    Assets/BlackHoleEffect/Fonts~/NotoSansKR-Regular.otf   full, source of truth
    Assets/BlackHoleEffect/Resources/Fonts/…-Regular.otf   subset, shipped

The '~' suffix is Unity's own "ignore this folder" convention, so the full font
stays in the repo without being imported, referenced, or built.

Run this after adding UI or narration text in a new language or with new
characters. A glyph missing from a dynamic font renders as *nothing* — no tofu
box to notice in a screenshot — so the script verifies its own output and fails
loudly rather than shipping a font with holes in it.

    python Tools/subset_font.py

Requires fonttools (pip install fonttools).
"""

import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
FULL = REPO / "Assets/BlackHoleEffect/Fonts~/NotoSansKR-Regular.otf"
OUT = REPO / "Assets/BlackHoleEffect/Resources/Fonts/NotoSansKR-Regular.otf"

# Third-party sample code draws none of the exhibit's text.
SKIP = ("Assets/Samples", "Assets/TextMesh Pro", "Assets/MRTemplateAssets", "Assets/XRI")

# Insurance, not necessity: symbols a future caption is likely to reach for.
# Each one costs a few hundred bytes and saves a silent blank.
EXTRA = (
    "".join(chr(c) for c in range(0x20, 0x7F))
    + "°·×÷±≈≠≤≥∝√∞∅→←↑↓↔▲▼◀▶●○■□◆◇★☆…—–‐‘’“”«»「」『』、。〜％＋－±©®™µΩ"
    + "αβγδεζηθικλμνξοπρστυφχψω"
    + "ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩ"
    + "₀₁₂₃₄₅₆₇₈₉⁰¹²³⁴⁵⁶⁷⁸⁹"
)


def used_characters():
    chars = set(EXTRA)
    scanned = 0
    for path in sorted((REPO / "Assets").rglob("*.cs")):
        rel = path.relative_to(REPO).as_posix()
        if any(rel.startswith(s) for s in SKIP):
            continue
        scanned += 1
        chars |= set(path.read_text(encoding="utf-8", errors="replace"))
    # Control characters have no glyphs and confuse pyftsubset's unicode list.
    chars = {c for c in chars if ord(c) >= 0x20 and ord(c) != 0x7F}
    return chars, scanned


def codepoints(chars, limit=24):
    """Report characters as U+XXXX. The Windows console this usually runs in is
    cp949, which raises on the very glyphs we would be complaining about."""
    shown = " ".join(f"U+{ord(c):04X}" for c in sorted(chars)[:limit])
    return shown + (f" … (+{len(chars) - limit} more)" if len(chars) > limit else "")


def verify(font_path, wanted):
    from fontTools.ttLib import TTFont

    with TTFont(font_path) as font:
        covered = set()
        for table in font["cmap"].tables:
            covered |= set(table.cmap.keys())
    missing = {c for c in wanted if ord(c) not in covered}
    return missing


def main():
    if not FULL.exists():
        sys.exit(f"missing the full font: {FULL}\n"
                 "It is the source of truth for subsetting; restore it from git history.")

    wanted, scanned = used_characters()
    print(f"scanned {scanned} C# sources -> {len(wanted)} distinct characters")

    # The full font is the one that must contain everything. Anything missing
    # here is a character no font in the project can draw, which is worth
    # knowing about before it reaches a caption.
    absent = verify(FULL, wanted)
    if absent:
        print(f"note: {len(absent)} characters are absent from the full font too, "
              f"so nothing can draw them: {codepoints(absent)}")
        wanted -= absent

    OUT.parent.mkdir(parents=True, exist_ok=True)
    unicodes = ",".join(f"U+{ord(c):04X}" for c in sorted(wanted))
    subprocess.run(
        [sys.executable, "-m", "fontTools.subset", str(FULL),
         f"--unicodes={unicodes}",
         f"--output-file={OUT}",
         "--layout-features=*",     # keep kerning and CJK forms
         "--name-IDs=*",
         "--notdef-outline",
         "--drop-tables+=DSIG"],
        check=True,
    )

    missing = verify(OUT, wanted)
    if missing:
        sys.exit("subset is missing characters it was asked for: " + codepoints(missing))

    before, after = FULL.stat().st_size, OUT.stat().st_size
    print(f"{before/1048576:.2f} MB -> {after/1048576:.2f} MB "
          f"({100 * after / before:.1f}%), {len(wanted)} glyphs, all verified present")


if __name__ == "__main__":
    main()
