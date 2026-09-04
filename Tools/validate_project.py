#!/usr/bin/env python3
"""اعتبارسنج سبک پروژه بدون نیاز به نصب Unity در CI."""
from pathlib import Path
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
required_dirs = [
    "Assets/Scripts/Core", "Assets/Scripts/Data", "Assets/Scripts/Systems",
    "Assets/Scripts/World", "Assets/Scripts/AI", "Assets/Scripts/UI",
    "Assets/Scripts/Audio", "Assets/Scenes", "Assets/Prefabs",
    "Assets/Materials", "Assets/Textures", "Assets/Animations", "Assets/Localization",
    "Assets/Resources/Fonts", "Assets/Editor", "ProjectSettings", "Packages"
]
required_files = [
    "Assets/Scenes/Main.unity", "Assets/Scripts/Core/GameBootstrap.cs",
    "Assets/Scripts/Core/GameManager.cs", "Assets/Scripts/Core/SaveSystem.cs",
    "Assets/Scripts/Systems/ResourceSystem.cs", "Assets/Scripts/Systems/ConstructionSystem.cs",
    "Assets/Scripts/Systems/ProgressionSystem.cs", "Assets/Scripts/AI/SurvivorAgent.cs",
    "Assets/Scripts/AI/EnemyAgent.cs", "Assets/Scripts/UI/UIManager.cs",
    "Assets/Editor/AndroidBuild.cs", "ProjectSettings/ProjectVersion.txt",
    "Packages/manifest.json", "Assets/Localization/LocalizationTable.json"
]

errors = []
for directory in required_dirs:
    if not (ROOT / directory).is_dir():
        errors.append(f"missing directory: {directory}")
for filename in required_files:
    path = ROOT / filename
    if not path.is_file() or path.stat().st_size == 0:
        errors.append(f"missing file: {filename}")

for json_file in ["Packages/manifest.json", "Packages/packages-lock.json", "Assets/Localization/LocalizationTable.json"]:
    try:
        json.loads((ROOT / json_file).read_text(encoding="utf-8"))
    except Exception as exc:
        errors.append(f"invalid JSON {json_file}: {exc}")

scene = (ROOT / "Assets/Scenes/Main.unity").read_text(encoding="utf-8")
if "d9ab3f210c5d45c38fa0e9a5bc2c8a10" not in scene:
    errors.append("Main.unity does not reference GameBootstrap")
if "Assets/Scenes/Main.unity" not in (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text(encoding="utf-8"):
    errors.append("Main scene is not in build settings")

scripts = list((ROOT / "Assets/Scripts").rglob("*.cs"))
editor_scripts = list((ROOT / "Assets/Editor").rglob("*.cs"))
all_scripts = scripts + editor_scripts
if len(scripts) < 15:
    errors.append(f"expected layered C# implementation, found {len(scripts)} scripts")
if not editor_scripts:
    errors.append("missing Android build automation in Assets/Editor")
for path in all_scripts:
    content = path.read_text(encoding="utf-8")
    if content.count("{") != content.count("}"):
        errors.append(f"unbalanced braces: {path.relative_to(ROOT)}")
    if not re.search(r"\b(class|struct|enum)\s+\w+", content):
        errors.append(f"no type declaration: {path.relative_to(ROOT)}")

# بررسی تنظیمات انتشار Android (IL2CPP / ARM64 / minSdk / شناسه‌ی بسته)
settings = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8")
def yaml_has(section, key, value):
    pattern = re.compile(rf"^\s*{re.escape(key)}:\s*{re.escape(value)}\s*$", re.MULTILINE)
    match = pattern.search(section)
    return match is not None

if "com.parsaapps.bazibaqa" not in settings:
    errors.append("project package identifier is not com.parsaapps.bazibaqa")
if not yaml_has(settings, "AndroidMinSdkVersion", "26"):
    errors.append("Android minSdk is not API 26")
if not yaml_has(settings, "AndroidTargetArchitectures", "2"):
    errors.append("Android target architecture is not ARM64 (2)")
if not re.search(r"scriptingBackend:\s*\n\s*Standalone: 1\n\s*Android: 1", settings):
    errors.append("Android scripting backend is not IL2CPP")

if errors:
    print("اعتبارسنج ناموفق:")
    print("\n".join(f"- {error}" for error in errors))
    sys.exit(1)
print(f"اعتبارسنج موفق: {len(all_scripts)} اسکریپت، صحنه‌ی اصلی، سیستم‌ها و تنظیمات انتشار Android آماده هستند.")
