"""Bake the MR solar-exhibit docent narration with edge-tts.

Same contract as generate_nebula_narration.py: the C# arrays in
MRSolarDocentScript.cs are the source of truth, so voice and script cannot
drift apart. Two differences, both deliberate:

- 12 beats, not 6.
- Output goes to Assets/MilkyWay/Audio/NarrationMR/ — NOT Resources/.
  Resources ships with every platform including the 120 MB web build, and
  these clips are headset-only; the MR solar scene will reference them
  directly so no other player ever carries them.

Usage:
    python Tools/generate_mr_solar_narration.py --force
"""

from __future__ import annotations

import argparse
import asyncio
import json
import re
from pathlib import Path

import edge_tts


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "MilkyWay" / "MRSolarDocentScript.cs"
OUTPUT = ROOT / "Assets" / "MilkyWay" / "Audio" / "NarrationMR"

LINE_COUNT = 12
PREFIX = "mr_sol_"

LANGUAGES = {
    "ko": ("NarrationLines", "", "ko-KR-SunHiNeural"),
    "en": ("NarrationLinesEn", "en", "en-US-JennyNeural"),
    "ja": ("NarrationLinesJa", "ja", "ja-JP-NanamiNeural"),
    "zh": ("NarrationLinesZh", "zh", "zh-CN-XiaoxiaoNeural"),
}


def read_csharp_array(source: str, name: str) -> list[str]:
    pattern = (
        rf"public\s+static\s+readonly\s+string\[\]\s+{re.escape(name)}"
        rf"\s*=\s*\{{(?P<body>.*?)\n\s*\}};"
    )
    match = re.search(pattern, source, re.DOTALL)
    if not match:
        raise RuntimeError(f"Could not find C# array: {name}")

    literals = re.findall(r'"((?:\\.|[^"\\])*)"', match.group("body"))
    lines = [json.loads(f'"{literal}"') for literal in literals]
    if len(lines) != LINE_COUNT:
        raise RuntimeError(f"{name}: expected {LINE_COUNT} narration lines, found {len(lines)}")
    return lines


async def synthesize(text: str, voice: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(".mp3.new")
    await edge_tts.Communicate(text, voice, rate="-4%").save(str(temporary))
    if temporary.stat().st_size < 4096:
        temporary.unlink(missing_ok=True)
        raise RuntimeError(f"Generated audio is unexpectedly small: {destination}")
    temporary.replace(destination)


async def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--force", action="store_true", help="replace existing MP3 files")
    parser.add_argument("--dry-run", action="store_true", help="show planned files only")
    args = parser.parse_args()

    source = SOURCE.read_text(encoding="utf-8")
    jobs: list[tuple[str, str, Path]] = []
    for language, (array_name, folder, voice) in LANGUAGES.items():
        lines = read_csharp_array(source, array_name)
        directory = OUTPUT / folder if folder else OUTPUT
        for index, line in enumerate(lines):
            destination = directory / f"{PREFIX}{index}.mp3"
            if args.force or not destination.exists():
                jobs.append((line, voice, destination))
            else:
                print(f"keep  [{language}] {destination.relative_to(ROOT)}")

    for text, voice, destination in jobs:
        print(f"write [{voice}] {destination.relative_to(ROOT)}")
        if not args.dry_run:
            await synthesize(text, voice, destination)

    print(f"done: {len(jobs)} narration clips")


if __name__ == "__main__":
    asyncio.run(main())
