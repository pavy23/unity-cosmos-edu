"""Regenerate the four-language nebula-tour narration with edge-tts.

The C# NarrationLines arrays remain the source of truth, so the spoken audio
and on-screen subtitles cannot silently drift apart.

Usage:
    python Tools/generate_nebula_narration.py --force
"""

from __future__ import annotations

import argparse
import asyncio
import json
import re
from pathlib import Path

import edge_tts


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets" / "MilkyWay" / "NebulaTour.cs"
OUTPUT = ROOT / "Assets" / "MilkyWay" / "Resources" / "Narration"

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
    if len(lines) != 6:
        raise RuntimeError(f"{name}: expected 6 narration lines, found {len(lines)}")
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
            destination = directory / f"neb_life_{index}.mp3"
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
