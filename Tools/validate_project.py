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
    "Assets/Scripts/Audio", "Assets/Scripts/Utilities", "Assets/Scenes", "Assets/Prefabs",
    "Assets/Materials", "Assets/Textures", "Assets/Animations", "Assets/Resources/Localization",
    "Assets/Resources/Fonts", "Assets/Editor", "ProjectSettings", "Packages"
]
required_files = [
    "Assets/Scenes/Main.unity", "Assets/Scripts/Core/GameBootstrap.cs",
    "Assets/Scripts/Core/GameManager.cs", "Assets/Scripts/Core/SaveSystem.cs",
    "Assets/Scripts/Core/GameLogger.cs", "Assets/Scripts/Systems/ResourceSystem.cs",
    "Assets/Scripts/Systems/ConstructionSystem.cs", "Assets/Scripts/Systems/ProgressionSystem.cs",
    "Assets/Scripts/Systems/QuestSystem.cs", "Assets/Scripts/Systems/AchievementSystem.cs",
    "Assets/Scripts/Systems/DailyRewardSystem.cs", "Assets/Scripts/Systems/PerformanceManager.cs",
    "Assets/Scripts/Systems/EquipmentSystem.cs", "Assets/Scripts/Systems/StoryDirector.cs",
    "Assets/Scripts/Systems/RaidSystem.cs", "Assets/Scripts/World/WorldVFX.cs",
    "Assets/Scripts/World/AmbientLife.cs", "Assets/Scripts/AI/SurvivorAgent.cs",
    "Assets/Scripts/AI/EnemyAgent.cs", "Assets/Scripts/UI/UIManager.cs",
    "Assets/Scripts/UI/ButtonFx.cs", "Assets/Scripts/UI/CrownPulse.cs",
    "Assets/Scripts/Utilities/ObjectPool.cs", "Assets/Editor/AndroidBuild.cs",
    "ProjectSettings/ProjectVersion.txt", "Packages/manifest.json",
    "Assets/Resources/Localization/LocalizationTable.json",
    "Assets/Scripts/World/WorldParts.cs", "Assets/Editor/RuntimeValidation.cs",
    "Assets/Tests/EditMode/QualityGateEditModeTests.cs", "Assets/Tests/PlayMode/RuntimeValidationPlayModeTests.cs",
    "Docs/RuntimeValidation.md", "Tools/project_lint.py", "Tools/unity_validation.sh",
    "Tools/localization_allowlist.json"
]

errors = []
for directory in required_dirs:
    if not (ROOT / directory).is_dir():
        errors.append(f"missing directory: {directory}")
for filename in required_files:
    path = ROOT / filename
    if not path.is_file() or path.stat().st_size == 0:
        errors.append(f"missing file: {filename}")

for json_file in ["Packages/manifest.json", "Packages/packages-lock.json", "Assets/Resources/Localization/LocalizationTable.json"]:
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
if len(scripts) < 20:
    errors.append(f"expected layered AAA implementation, found {len(scripts)} runtime scripts")
if not editor_scripts:
    errors.append("missing Android build automation in Assets/Editor")
for path in all_scripts:
    content = path.read_text(encoding="utf-8")
    if content.count("{") != content.count("}"):
        errors.append(f"unbalanced braces: {path.relative_to(ROOT)}")
    if not re.search(r"\b(class|struct|enum)\s+\w+", content):
        errors.append(f"no type declaration: {path.relative_to(ROOT)}")

# حذف فاصله‌ی Tab و بررسی وجود حداقل یک فایل متنی فارسی در سطح پروژه.
for path in all_scripts:
    if path.suffix != ".cs":
        continue
    if "\t" in path.read_text(encoding="utf-8"):
        errors.append(f"contains tab characters: {path.relative_to(ROOT)}")
total_persian = sum(1 for path in all_scripts if path.suffix == ".cs" and re.search(r"[\u0600-\u06FF]", path.read_text(encoding="utf-8")))
if total_persian < 10:
    errors.append(f"too few Persian-annotated scripts: {total_persian}")

# تنظیمات انتشار Android (IL2CPP / ARM64 / minSdk / شناسه‌ی بسته)
settings = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8")


def yaml_has(section, key, value):
    pattern = re.compile(rf"^\s*{re.escape(key)}:\s*{re.escape(value)}\s*$", re.MULTILINE)
    return pattern.search(section) is not None


if "com.parsaapps.bazibaqa" not in settings:
    errors.append("project package identifier is not com.parsaapps.bazibaqa")
if not yaml_has(settings, "AndroidMinSdkVersion", "26"):
    errors.append("Android minSdk is not API 26")
if not yaml_has(settings, "AndroidTargetArchitectures", "2"):
    errors.append("Android target architecture is not ARM64 (2)")
if not re.search(r"scriptingBackend:\s*\n\s*Standalone: 1\n\s*Android: 1", settings):
    errors.append("Android scripting backend is not IL2CPP")

# فایل‌های Assets باید .meta داشته باشند تا GUID ها پایدار بمانند (بدون آن، ارجاع صحنه شکننده می‌شود).
for path in (ROOT / "Assets").rglob("*"):
    if ".git" in path.parts or path.suffix == ".meta":
        continue
    if not path.with_suffix(path.suffix + ".meta").exists():
        errors.append(f"missing .meta for {path.relative_to(ROOT).as_posix()}")

# فونت‌های فارسی باید داده‌ی فونت را در بیلد بگنجانند (Dynamic) تا TextMeshPro و Text کار کنند.
for font_meta in (ROOT / "Assets").rglob("*.ttf.meta"):
    text = font_meta.read_text(encoding="utf-8")
    if "includeFontData: 1" not in text:
        errors.append(f"{font_meta.relative_to(ROOT).as_posix()}: includeFontData باید ۱ (Dynamic) باشد")

# اسمبل‌دیفinition ها باید بسته‌هایی که کد استفاده می‌کند را صریحاً referenced کنند.
runtime_asmdef = json.loads((ROOT / "Assets/Scripts/BaziBaqa.Runtime.asmdef").read_text(encoding="utf-8"))
runtime_references = set(runtime_asmdef.get("references", []))
runtime_code = "\n".join(path.read_text(encoding="utf-8") for path in (ROOT / "Assets/Scripts").rglob("*.cs"))
if "using TMPro;" in runtime_code and "Unity.TextMeshPro" not in runtime_references:
    errors.append("BaziBaqa.Runtime.asmdef باید Unity.TextMeshPro را reference کند (استفاده از TMPro)")
if ("UnityEngine.UI" in runtime_code or "UnityEngine.EventSystems" in runtime_code) and "UnityEngine.UI" not in runtime_references:
    errors.append("BaziBaqa.Runtime.asmdef باید UnityEngine.UI را reference کند (استفاده از UGUI)")

# منبع واحد حقیقت نسخه باید با ProjectSettings هم‌خوان باشد (VersionManager.Apply اجرا شده باشد).
version_config = json.loads((ROOT / "Assets/Resources/VersionConfig.json").read_text(encoding="utf-8"))
for key, expected in (
    ("bundleVersion", str(version_config["versionName"])),
    ("AndroidBundleVersionCode", str(version_config["versionCode"])),
    ("AndroidMinSdkVersion", str(version_config["minSdkVersion"])),
    ("AndroidTargetSdkVersion", str(version_config["targetSdkVersion"])),
):
    if not yaml_has(settings, key, expected):
        errors.append(f"ProjectSettings.{key} با VersionConfig.json هماهنگ نیست (انتظار {expected}) — VersionManager.Apply() را اجرا کنید")

if errors:
    print("اعتبارسنج ناموفق:")
    print("\n".join(f"- {error}" for error in errors))
    sys.exit(1)
print(f"اعتبارسنج موفق: {len(all_scripts)} اسکریپت، صحنه‌ی اصلی، سیستم‌های AAA و تنظیمات انتشار Android آماده هستند.")
