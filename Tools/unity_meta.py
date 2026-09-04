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
    ".hlsl": ("ShaderImporter", "defaultTextures: []\n  nonModifiableTextures: []\n"),
    ".cginc": ("ShaderImporter", "defaultTextures: []\n  nonModifiableTextures: []\n"),
    ".mat": ("NativeFormatImporter", "mainObjectFileID: 2100000\n"),
    ".playable": ("NativeFormatImporter", "externalObjects: {}\n  mainObjectFileID: 0\n"),
}

# بافت‌های رویه‌ای بازی: نقشه‌های نرمال و ماسک باید بدون تصحیح گاما (خطی) خوانده شوند،
# وگرنه عددِ نرمال/زبری خراب می‌شود. یونیتی تنها از روی `.meta` این را می‌فهمد، پس
# ایمپورتِ خودکار کافی نیست و همین‌جا نوشته می‌شود.
LINEAR_TEXTURE_MARKERS = ("normal", "mask", "noise", "height", "ao")
TEXTURE_BLOCK = """TextureImporter:
  externalObjects: {{}}
  serializedVersion: 128
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {srgb}
    linearTexture: {linear}
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  isReadable: 0
  streamingMipmaps: 1
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {maxsize}
  textureCompression: -1
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: {aniso}
    mipBias: 0
    wrapU: {wrap}
    wrapV: {wrap}
    wrapW: {wrap}
  nprTextureSettings: []
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
"""


def is_linear_texture(relative: str) -> bool:
    stem = Path(relative).name.lower()
    return any(marker in stem for marker in LINEAR_TEXTURE_MARKERS)


def texture_importer(relative: str) -> str:
    """متای بافت: Wrap=Repeat برای بافت‌های محیطی و Clamp/Point برای رابط کاربری و ذرات."""
    linear = is_linear_texture(relative)
    ui = "/UI/" in relative or "/Sprites/" in relative
    wrap = 0 if ui else 0            # 0 = Repeat (پیش‌فرضِ بافت محیطی)؛ رابط کاربری هم تایل نمی‌شود
    return TEXTURE_BLOCK.format(srgb=0 if linear else 1, linear=1 if linear else 0,
                                maxsize=256 if ui else 512, aniso=1 if ui else 4, wrap=wrap)


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
        suffix = Path(relative).suffix.lower()
        if suffix in (".png", ".tga", ".jpg", ".jpeg"):
            lines.append(texture_importer(relative).rstrip("\n"))
            lines += ["  userData: ", "  assetBundleName: ", "  assetBundleVariant: "]
            return "\n".join(lines) + "\n"
        importer, payload = IMPORTERS.get(suffix, ("DefaultImporter", ""))
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
    parser.add_argument("--fix-textures", action="store_true", help="متای بافت‌ها (PNG) را با تنظیمات استاندارد بازنویسی کن")
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

    if args.fix_textures:
        for texture_meta in sorted((base / args.root).rglob("*.png.meta")):
            relative = texture_meta.relative_to(base).with_suffix("").as_posix()
            expected_tail = texture_importer(relative).rstrip("\n").splitlines()
            text = texture_meta.read_text(encoding="utf-8")
            has_importer = "TextureImporter:" in text
            needs = (not has_importer) or ("wrapU: 0" not in text) or ("enableMipMap: 1" not in text)
            if needs:
                changed += 1
                guid = ""
                for line in text.splitlines():
                    if line.startswith("guid:"):
                        guid = line.split(":", 1)[1].strip()
                body = "\n".join(["fileFormatVersion: 2", f"guid: {guid}"] + expected_tail +
                                 ["  userData: ", "  assetBundleName: ", "  assetBundleVariant: "]) + "\n"
                if args.apply:
                    texture_meta.write_text(body, encoding="utf-8", newline="\n")
                    print(f"patched  {texture_meta.relative_to(base).as_posix()} (TextureImporter)")
                else:
                    print(f"stale    {relative} (TextureImporter settings expected)")

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
