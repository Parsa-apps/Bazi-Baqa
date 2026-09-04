#!/usr/bin/env python3
"""سازِ جدول بومی‌سازی «سرزمین بقا» (بی‌نیاز از یونیتی).

اصول: «کد کلید را می‌گوید، جدول متن را». همه‌ی متن‌های قابل‌مشاهده در
Assets/Resources/Localization/LocalizationTable.json زندگی می‌کنند و این ابزار همان جدول را
تولید، ادغام و ممیزی می‌کند:

  1) کلیدهایی که کد استفاده می‌کند را بیرون می‌کشد:
       • Loc.Get / Loc.Has / LocalizationManager.Get (کلیدِ صریح)
       • الگوهای ترکیبی مثل "resource." + type.ToString().ToLowerInvariant() (کلیدِ enum‌محور)
       • خانواده‌های تولیدشده: survivor.name.<i>، language.<code>، quest.<id>.<part>، story.<id>.<part>
  2) با فایل ورودیِ دستی (Tools/localization_entries.json: کلید → {fa, en}) ادغام می‌کند،
  3) جدول JSON را مرتب و پایدار می‌نویسد (کلیدهای تازه به همه‌ی زبان‌ها می‌رسند)،
  4) و گزارش می‌دهد: کلیدِ بی‌متن، متنِ بی‌استفاده، زبانِ ناتمام، و پوششِ enum‌ها.

اجرا:
    python3 Tools/localization_table.py            # ادغام + بازنویسی جدول
    python3 Tools/localization_table.py --check     # فقط بررسی (CI); در صورت مشکل خروجی ۱
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))

import project_lint as lint  # noqa: E402  (منبع یکتای تحلیل کد: Tools/project_lint.py)
TABLE_PATH = ROOT / "Assets/Resources/Localization/LocalizationTable.json"
ENTRIES_PATH = ROOT / "Tools/localization_entries.json"
SCRIPTS_DIR = ROOT / "Assets/Scripts"
DEFAULT_LANGUAGE = "fa"
FALLBACK_LANGUAGE = "en"

# "prefix." + … .ToString().ToLowerInvariant() [+ ".suffix"]
DYNAMIC_KEY_RE = re.compile(r'"([a-z0-9_.]+\.)"\s*\+[^;]*?\.ToString\(\)\.ToLowerInvariant\(\)\s*(?:\+\s*"\.?([a-z_]+)")?')

ENUM_RE = re.compile(r"public\s+enum\s+(\w+)[^{]*\{([^}]*)\}", re.S)


def parse_enums(text: str) -> dict[str, list[str]]:
    """enum‌های کد (برای الزامِ پوششِ کاملِ کلیدها در جدول)."""
    enums: dict[str, list[str]] = {}
    for name, body in ENUM_RE.findall(text):
        members = [part.split("=")[0].strip() for part in body.split(",")]
        enums[name] = [member for member in members if member and not member.startswith("//")]
    return enums


# هر enum که نامش در جدول است: پیشوندِ کلید + پسوندهای فرعی
ENUM_PREFIXES: dict[str, tuple[str, list[str]]] = {
    "ResourceType": ("resource.", []),
    "BuildingType": ("building.", []),
    "SurvivorRole": ("role.", []),
    "WeatherType": ("weather.", []),
    "TechnologyType": ("technology.", [".unlocked"]),
    "AchievementId": ("achievement.", []),
    "EquipmentType": ("equipment.", []),
    "SurvivorState": ("status.", []),
}


def raw_code(path: Path) -> str:
    """کدِ فایل، بدون خط‌های توضیحی (تا کلیدهای داخل کامنت شمرده نشوند)."""
    kept = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip().startswith("//"):
            continue
        kept.append(line)
    return "\n".join(kept)


def source_files() -> list[Path]:
    return sorted(file for file in SCRIPTS_DIR.rglob("*.cs") if "/Tests/" not in str(file.as_posix()))


def collect_used_keys() -> tuple[set[str], dict[str, list[str]]]:
    """همه‌ی کلیدهایی که کد (یا داده‌ی کد) به آن‌ها تکیه می‌کند."""
    used: set[str] = set()
    files = [(file, raw_code(file)) for file in source_files()]

    # ۱) enum‌ها را یک‌بار جمع کن (کلاس‌ها ممکن است در فایل دیگری باشند)
    enums: dict[str, list[str]] = {}
    for _file, text in files:
        enums.update(parse_enums(text))

    # ۲) کلیدهای صریح + کلیدهای ترکیبیِ enum‌محور
    dynamic: list[tuple[str, str]] = []
    for _file, text in files:
        used |= lint.localization_keys(text)
        dynamic.extend(DYNAMIC_KEY_RE.findall(text))
    for prefix, suffix in dynamic:
        for enum_name, (enum_prefix, _extra) in ENUM_PREFIXES.items():
            if prefix != enum_prefix:
                continue
            for member in enums.get(enum_name, []):
                used.add(prefix + member.lower() + (suffix or ""))

    # پوششِ کامل enum‌ها: هر عضو باید متن خودش را داشته باشد (نام + پسوندها)
    for enum_name, (prefix, suffixes) in ENUM_PREFIXES.items():
        for member in enums.get(enum_name, []):
            used.add(prefix + member.lower())
            for suffix in suffixes:
                used.add(prefix + member.lower() + suffix)

    # خانواده‌های کلیدِ داده‌محور
    game_data = (SCRIPTS_DIR / "Data/GameData.cs").read_text(encoding="utf-8")
    pool = re.search(r"public const int SurvivorNameCount = (\d+);", game_data)
    for index in range(int(pool.group(1)) if pool else 0):
        used.add(f"survivor.name.{index}")
    for code in (DEFAULT_LANGUAGE, FALLBACK_LANGUAGE):
        used.add(f"language.{code}")

    systems = SCRIPTS_DIR / "Systems"
    for file in sorted(systems.glob("*.cs")) if systems.exists() else []:
        text = file.read_text(encoding="utf-8")
        for quest_id in re.findall(r'new QuestDefinition\("([^"]+)"', text):
            used.add(f"quest.{quest_id}.title")
            used.add(f"quest.{quest_id}.description")
        for event_id in re.findall(r'new StoryEvent\(\s*\d+\s*,\s*"([^"]+)"', text):
            used.add(f"story.{event_id}.title")
            used.add(f"story.{event_id}.body")
        for event_id, choice_id in re.findall(r'new StoryChoice\("([^"]+)",\s*"([^"]+)"', text):
            used.add(f"story.{event_id}.{choice_id}.title")
            used.add(f"story.{event_id}.{choice_id}.hint")

    return {key for key in used if key and not key.endswith(".")}, enums


def read_table() -> tuple[list[dict], dict[str, dict[str, str]]]:
    """ساختار فعلی جدول: (ترتیبِ زبان‌ها، {زبان: {کلید: متن}})."""
    if not TABLE_PATH.exists():
        return ([{"language": DEFAULT_LANGUAGE, "direction": "rtl"},
                 {"language": FALLBACK_LANGUAGE, "direction": "ltr"}],
                {DEFAULT_LANGUAGE: {}, FALLBACK_LANGUAGE: {}})
    data = json.loads(TABLE_PATH.read_text(encoding="utf-8"))
    order: list[dict] = []
    tables: dict[str, dict[str, str]] = {}
    for language in data.get("languages", []):
        code = language.get("language", "")
        if not code:
            continue
        tables[code] = {entry["key"]: entry.get("value", "") for entry in language.get("entries", [])}
        order.append({"language": code, "direction": language.get("direction", "ltr")})
    if DEFAULT_LANGUAGE not in tables:
        tables[DEFAULT_LANGUAGE] = {}
        order.insert(0, {"language": DEFAULT_LANGUAGE, "direction": "rtl"})
    if FALLBACK_LANGUAGE not in tables:
        tables[FALLBACK_LANGUAGE] = {}
        order.append({"language": FALLBACK_LANGUAGE, "direction": "ltr"})
    return order, tables


def write_table(order: list[dict], tables: dict[str, dict[str, str]]) -> None:
    payload = {
        "defaultLanguage": DEFAULT_LANGUAGE,
        "_comment": (
            "جدول متن‌های بازی «سرزمین بقا». هر کلید یک متن قابل‌مشاهده است؛ برای افزودن زبان تازه"
            " یک بلوک زبان با همین کلیدها بیفزایید. فایل با Tools/localization_table.py تولید/ممیزی"
            " می‌شود (منبع ویرایش: Tools/localization_entries.json)؛ ترتیب کلیدها پایدار است."
        ),
        "languages": [
            {
                "language": info["language"],
                "direction": info["direction"],
                "entries": [{"key": key, "value": tables[info["language"]][key]}
                            for key in sorted(tables[info["language"]])],
            }
            for info in order
        ],
    }
    TABLE_PATH.parent.mkdir(parents=True, exist_ok=True)
    TABLE_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def main() -> int:
    parser = argparse.ArgumentParser(description="Localization table builder/auditor (no Unity required).")
    parser.add_argument("--check", action="store_true", help="فقط بررسی کن؛ فایل را بازنویسی نکن.")
    parser.add_argument("--prune", action="store_true", help="کلیدهای بی‌استفاده از جدول حذف شوند.")
    args = parser.parse_args()

    order, tables = read_table()
    used, enums = collect_used_keys()
    problems: list[str] = []
    added = 0

    if ENTRIES_PATH.exists():
        entries = json.loads(ENTRIES_PATH.read_text(encoding="utf-8"))
        for key, value in entries.items():
            fa = (value or {}).get("fa")
            en = (value or {}).get("en")
            if not fa or not en:
                problems.append(f"ورودی «{key}» در Tools/localization_entries.json یک‌زبانه است.")
                continue
            if key in used or key in tables[DEFAULT_LANGUAGE]:
                added += 1
            else:
                problems.append(f"ورودی «{key}» هیچ‌جا در کد استفاده نمی‌شود (یتیم است).")
            tables[DEFAULT_LANGUAGE][key] = fa
            tables[FALLBACK_LANGUAGE][key] = en
    else:
        problems.append("Tools/localization_entries.json نیست؛ متن‌های تازه از کجا بیایند؟")

    missing = sorted(key for key in used if key not in tables[DEFAULT_LANGUAGE])
    for key in missing:
        problems.append(f"کلید «{key}» در کد استفاده شده ولی متنی برایش نوشته نشده است.")

    # متن‌ها نباید «دو بار اسکیپ» شده باشند (بک‌اسلش+n به‌جای خط‌شکن واقعی) — رایج‌ترین خرابیِ ویرایش دستی JSON
    for code, entries_map in tables.items():
        for key, value in entries_map.items():
            for token in (chr(92) + "n", chr(92) + "t", chr(92) + chr(34)):
                if token in value:
                    problems.append(f"متن «{key}» در زبان {code} کاراکتر فرارِ دوباره‌اسکیپ‌شده دارد ({token}).")

    unused = sorted(key for key in tables[DEFAULT_LANGUAGE] if key not in used)
    if args.prune and unused:
        for key in unused:
            for code in tables:
                tables[code].pop(key, None)
        print(f"{len(unused)} کلید بی‌استفاده از جدول حذف شد: {', '.join(unused)}")
    else:
        for key in unused:
            problems.append(f"کلید «{key}» در جدول است ولی کد آن را نمی‌خواند.")

    # هیچ زبانی نباید کلیدی را کم داشته باشد
    for info in order:
        code = info["language"]
        for key in tables[DEFAULT_LANGUAGE]:
            if key not in tables[code]:
                tables[code][key] = tables[DEFAULT_LANGUAGE][key]
                problems.append(f"زبان «{code}» کلید «{key}» را نداشت؛ متن پیش‌فرض جای آن گذاشته شد.")

    if not args.check:
        write_table(order, tables)

    print(f"کلیدهای موردنیاز کد: {len(used)} | کلیدهای جدول: {len(tables[DEFAULT_LANGUAGE])} | "
          f"متن‌های تازه از فایل ورودی: {added} | زبان‌ها: {', '.join(info['language'] for info in order)}")
    if problems:
        for problem in problems:
            print("  E " + problem)
        print(f"{len(problems)} مشکل یافت شد.")
        return 1
    print("جدول بومی‌سازی سالم است: هر کلیدِ موردنیاز کد، متنِ کامل دارد.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
