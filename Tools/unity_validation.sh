#!/usr/bin/env bash
# اجرای اعتبارسنجی «Unity واقعی» بدون باز کردن گرافیکیِ اِدیتور.
#
# این اسکریپت همان چیزی است که در مرحله‌ی «Unity Runtime Validation» لازم است:
#   ۰) پیش‌بررسی ایستا (همیشه اجرا می‌شود؛ حتی بدون Unity):
#        Tools/project_lint.py            → دروازه‌های متنی/بومی‌سازی/ساختار/نسخه
#        Tools/localization_table.py      → پوشش کامل جدول بومی‌سازی
#        Tools/validate_project.py        → سلامت کلی پروژه و آماده‌سازی Android
#   ۱) ایمپورت کامل پروژه (کامپایل اسکریپت‌ها) → خطاهای CS کنسول
#   ۲) فونتِ فارسیِ TMP: BaziBaqa.EditorTools.TypographyBaker.BakeBatch (بیک + پوششِ حروف)
#   ۳) هماهنگیِ نسخه: BaziBaqa.EditorTools.VersionManager.VerifyBatch (فایل ↔ PlayerSettings)
#   ۴) اجرای تست‌های EditMode  (Assets/Tests/EditMode)
#   ۵) اجرای تست‌های PlayMode   (Assets/Tests/PlayMode → بارگذاری صحنه، Save/Load، تعامل UI)
#   ۶) اجرای BaziBaqa.EditorTools.RuntimeValidation.ValidateBatch (ممیزی + صحت نسخه + بومی‌سازی)
#   ۷) پالایش لاگ‌ها برای error / warning / Missing Reference و چاپ خلاصه
#
# usage:
#   Tools/unity_validation.sh                 # خودکار (UNITY_BIN یا Unity Hub)
#   UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.50f1/Unity.app/Contents/MacOS/Unity" Tools/unity_validation.sh
#   TOOLS=/opt/Unity/Editor/Unity Tools/unity_validation.sh skip-playmode
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOGS="$ROOT/Logs"
mkdir -p "$LOGS"

UNITY="${UNITY_BIN:-}"
if [[ -z "$UNITY" ]]; then
  for candidate in \
    /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
    "$HOME"/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity \
    "$HOME"/snap/unity-editor/current/Editor/Unity \
    /opt/Unity/Editor/Unity \
    "/Applications/Unity/Unity.app/Contents/MacOS/Unity"; do
    if [[ -x "$candidate" ]]; then UNITY="$candidate"; break; fi
  done
fi
if [[ -z "$UNITY" ]] && command -v unity-editor >/dev/null 2>&1; then UNITY="$(command -v unity-editor)"; fi
preflight() {
  local code=0
  echo "→ preflight: project_lint"
  python3 "$ROOT/Tools/project_lint.py" --quiet || { code=1; echo "  ✗ project_lint"; }
  echo "→ preflight: localization_table --check"
  python3 "$ROOT/Tools/localization_table.py" --check | tail -n 3 || true
  python3 "$ROOT/Tools/localization_table.py" --check >/dev/null 2>&1 || { code=1; echo "  ✗ جدول بومی‌سازی کامل نیست"; }
  echo "→ preflight: validate_project"
  python3 "$ROOT/Tools/validate_project.py" | tail -n 3 || true
  python3 "$ROOT/Tools/validate_project.py" >/dev/null 2>&1 || { code=1; echo "  ✗ validate_project"; }
  return $code
}

PREFLIGHT_STATUS=0
if preflight; then
  echo "✓ پیش‌بررسی ایستا پاس شد."
else
  echo "✗ پیش‌بررسی ایستا خطا دارد (متن‌ها/ساختار/نسخه را درست کنید؛ سپس Unity را اجرا کنید)." >&2
  PREFLIGHT_STATUS=1
fi

if [[ -z "$UNITY" ]]; then
  echo "✗ Unity پیدا نشد؛ مراحلِ Unity (کامپایل/تست‌ها/بیکِ فونت) اجرا نشد. با UNITY_BIN=/path/to/Unity دوباره اجرا کنید." >&2
  if [[ "$PREFLIGHT_STATUS" -ne 0 ]]; then
    exit 1        # پیش‌بررسی ایستا هم خطا داشت
  fi
  exit 127        # بررسی‌های ایستا پاس شدند، ولی Unity در این محیط نیست
fi

echo "Unity: $UNITY"
"$UNITY" -version 2>/dev/null || true

PROJECT="$ROOT"
STATUS=0
MODE="${1:-full}"

run_step() {
  local name="$1"; shift
  local log="$LOGS/${name}.log"
  echo "→ $name"
  "$@" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$log" >/dev/null 2>&1
  local code=$?
  if grep -qE "error CS[0-9]+" "$log"; then
    echo "  ✗ خطای کامپایل در $log:"
    grep -E "error CS[0-9]+" "$log" | sort -u | head -25
    STATUS=1
  fi
  if [[ $code -ne 0 ]]; then
    echo "  ✗ خروج $name با کد $code پایان یافت (آخرین سطرها):"
    tail -n 12 "$log" | sed 's/^/     /'
    STATUS=1
  else
    echo "  ✓ $name"
  fi
}

# ۱) ایمپورت و کامپایل کامل اسکریپت‌ها
run_step compile "$UNITY"

# ۲) فونتِ فارسیِ TextMeshPro: بیک asset و اعتبارسنجیِ پوششِ حروف
run_step typography "$UNITY" -executeMethod BaziBaqa.EditorTools.TypographyBaker.BakeBatch

# ۳) هماهنگیِ نسخه (VersionConfig ↔ PlayerSettings)
run_step version "$UNITY" -executeMethod BaziBaqa.EditorTools.VersionManager.VerifyBatch

# ۴) تست‌های EditMode و PlayMode
run_tests() {
  local platform="$1"
  local log="$LOGS/tests-${platform}.log"
  local results="$LOGS/tests-${platform}.xml"
  echo "→ tests ($platform)"
  "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -logFile "$log" \
    -runTests -testPlatform "$platform" -testResults "$results" -testCategory "" >/dev/null 2>&1
  local code=$?
  if [[ -f "$results" ]]; then
    python3 - "$results" "$platform" <<'PY'
import sys, xml.etree.ElementTree as ET
path, platform = sys.argv[1], sys.argv[2]
try:
    root = ET.parse(path).getroot()
except Exception as exc:
    print(f"  ! تجزیه گزارش ممکن نشد: {exc}")
    sys.exit(0)
total = int(root.get("total") or 0)
passed = int(root.get("passed") or 0)
failed = int(root.get("failed") or 0)
skipped = int(root.get("skipped") or 0)
duration = root.get("duration") or "?"
print(f"  {platform}: {passed}/{total} پاس، {failed} شکست، {skipped} ردشده، زمان {duration}")
for case in root.iter("test-case"):
    if case.get("result") != "Passed":
        print("    ✗", case.get("fullname"))
        failure = case.find("failure/message")
        if failure is not None and failure.text:
            print("      " + failure.text.strip().splitlines()[0][:200])
sys.exit(1 if failed else 0)
PY
    if [[ $? -ne 0 ]]; then STATUS=1; fi
  fi
  if [[ $code -ne 0 && ! -f "$results" ]]; then
    echo "  ✗ اجرای تست $platform ناموفق بود؛ لاگ: $log"
    grep -E "Exception|error" "$log" | sort -u | head -15 | sed 's/^/     /'
    STATUS=1
  fi
}

run_tests editmode
if [[ "$MODE" != "skip-playmode" ]]; then run_tests playmode; fi

# ۵) ممیزی‌های داخل Editor (صحنه، Save/Load، UI، بومی‌سازی، نسخه)
echo "→ editor validation"
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -logFile "$LOGS/validation.log" \
  -executeMethod BaziBaqa.EditorTools.RuntimeValidation.ValidateBatch >/dev/null 2>&1
if [[ $? -ne 0 ]]; then
  echo "  ✗ RuntimeValidation مشکل گزارش کرد:"
  grep -E "\[BaziBaqa" "$LOGS/validation.log" | sed 's/^/     /' | head -30
  STATUS=1
else
  echo "  ✓ RuntimeValidation پاس شد"
fi

# ۶) هشدارهای مهمِ کنسول در همه‌ی لاگ‌ها
echo "→ console triage"
grep -h -E "Missing (Script|Reference|Component)|The referenced script|cannot be loaded|Unassigned reference|Default Execution Order|is deprecated|Unable to load font face|missing characters|TMP Settings" "$LOGS"/*.log 2>/dev/null | sort -u | head -25 | sed 's/^/  W  /'
if grep -qhE "Missing (Script|Reference|Component)|The referenced script" "$LOGS"/*.log 2>/dev/null; then STATUS=1; fi
# حروفِ نیفتاده در TextMeshPro یعنی مجموعه‌حروف کامل نیست
if grep -qhE "Unable to load font face|Unresolved/missing glyph" "$LOGS"/*.log 2>/dev/null; then
  echo "  ✗ فونتِ TMP مشکل دارد؛ BaziBaqa > Typography > Bake و Repair Font Import Settings را اجرا کنید."
  STATUS=1
fi

echo
if [[ $STATUS -eq 0 ]]; then
  echo "✓ اعتبارسنجی Unity کامل شد؛ لاگ‌ها در $LOGS"
else
  echo "✗ اعتبارسنجی Unity مشکل دارد؛ لاگ‌ها در $LOGS"
fi
exit $STATUS
