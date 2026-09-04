#!/usr/bin/env python3
"""تولید فایل .meta برای منبع‌هایی که فایل کنار‌ی آن‌ها وجود ندارد.

یونیتی هنگام اولین باز شدن پروژه فایل .meta می‌سازد، اما نسخه‌نشدنِ آن‌ها یعنی GUID ها
در هر بیلد/سیستمی عوض می‌شوند و ارجاع‌های صحنه (m_Script) می‌شکنند. این ابزار GUID را از
خودِ مسیر فایل به‌صورت قطعی (deterministic) می‌سازد تا هم تکراری نباشد و هم پایدار بماند.

usage:
    python3 Tools/unity_meta.py [--apply] [--root Assets]
"""
from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# نگاشت پسوند فایل به ایمپورترِ صحیح یونیتی (متنِ داخل .meta باید با نوع منبع بخواند).
IMPORTERS = {
    ".cs": ("MonoImporter", "serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"),
    ".asmdef": ("AssemblyDefinitionImporter", ""),
    ".asmref": ("AssemblyDefinitionImporter", ""),
    ".json": ("TextScriptImporter", ""),
    ".txt": ("TextScriptImporter", ""),
    ".md": ("TextScriptImporter", ""),
    ".ttf": ("TrueTypeFontImporter", "serializedVersion: 4\n  fontSize: 16\n  forceTextureCase: -2\n  characterSpacing: 0\n  characterPadding: 1\n  includeFontData: 1\n  fontNames: []\n  fallbackFontReferences: []\n  customCharacters: \n  fontRenderingMode: 0\n  ascentCalculationMode: 1\n  useLegacyBoundsCalculation: 0\n  shouldRoundAdvanceValue: 1\n"),
    ".shader": ("ShaderImporter", "defaultTextures: []\n  nonModifiableTextures: []\n"),
    ".playable": ("NativeFormatImporter", "externalObjects: {}\n  mainObjectFileID: 0\n"),
}

FOLDER_META = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"


def guid_for(path: str, salt: str = "") -> str:
    digest = hashlib.sha1((salt + path).encode("utf-8")).hexdigest()
    return digest[:32]


def meta_body(existing_guids: set, relative: str, is_folder: bool) -> str:
    guid = guid_for(relative)
    counter = 0
    while guid in existing_guids:
        counter += 1
        guid = guid_for(relative, f"#{counter}")
    existing_guids.add(guid)
    lines = ["fileFormatVersion: 2", f"guid: {guid}"]
    if is_folder:
        lines.append(FOLDER_META.rstrip("\n"))
    else:
        importer, payload = IMPORTERS.get(Path(relative).suffix.lower(), ("DefaultImporter", ""))
        lines.append(f"{importer}:")
        if importer != "DefaultImporter":
            lines.append("  externalObjects: {}")
        if payload:
            lines.append(payload.rstrip("\n"))
        lines += ["  userData: ", "  assetBundleName: ", "  assetBundleVariant: "]
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="فایل‌های .meta را واقعاً بنویس")
    parser.add_argument("--root", default="Assets", help="شاخه‌ای که بررسی می‌شود")
    parser.add_argument("--fix-fonts", action="store_true", help="متای فونت‌ها را هم بازنویسی کن (includeFontData روشن)")
    args = parser.parse_args()

    base = ROOT
    existing = set()
    metas = list((base / args.root).rglob("*.meta"))
    for meta in metas:
        for line in meta.read_text(encoding="utf-8").splitlines():
            if line.startswith("guid:"):
                existing.add(line.split(":", 1)[1].strip())

    targets = []
    for path in sorted((base / args.root).rglob("*")):
        if ".git" in path.parts:
            continue
        if path.is_dir():
            # یونیتی متای پوشه را خواهرِ پوشه می‌نویسد: Assets/Folder  →  Assets/Folder.meta
            if path.with_suffix(path.suffix + ".meta").exists():
                continue
            targets.append((path, True))
        else:
            if path.suffix == ".meta":
                continue
            if path.with_suffix(path.suffix + ".meta").exists():
                continue
            targets.append((path, False))

    changed = 0
    for path, is_folder in targets:
        relative = path.relative_to(base).as_posix()
        body = meta_body(existing, relative, is_folder)
        destination = path.with_suffix(path.suffix + ".meta")
        changed += 1
        if args.apply:
            destination.write_text(body, encoding="utf-8", newline="\n")
            print(f"created  {destination.relative_to(base).as_posix()}")
        else:
            print(f"missing  {relative}")

    if args.fix_fonts:
        for font_meta in sorted((base / args.root).rglob("*.ttf.meta")):
            text = font_meta.read_text(encoding="utf-8")
            before = text
            text = text.replace("includeFontData: 0", "includeFontData: 1").replace("includeFontData: 2", "includeFontData: 1")
            if text != before:
                changed += 1
                if args.apply:
                    font_meta.write_text(text, encoding="utf-8", newline="\n")
                    print(f"patched  {font_meta.relative_to(base).as_posix()} (includeFontData: 1)")
                else:
                    print(f"stale    {font_meta.relative_to(base).as_posix()} (includeFontData should be 1)")

    if not changed:
        print("هیچ فایل .meta کم نبود.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
