#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ممیز ایستای پروژه — جایِ «کامپایل و اجرای Unity» را در CI می‌گیرد.

این ابزار به‌جای اجرای یونیتی (که در محیط بدون گرافیک/بدون نصبِ Unity ممکن نیست)
بزرگ‌ترین دسته‌ی خطاهای واقعی را ایستا پیدا می‌کند:

  • ساختار واژگانی C#: توازن آکلاد/پرانتز، رشته‌های باز، کامنت‌های بسته‌نشده.
  • ارجاع‌های درون‌پروژه‌ای: `Type.Member` که عضو آن وجود ندارد (خطای CS1061/CS0117).
  • کلیدهای بومی‌سازی: هر `Loc.Get("…")` باید در جدول fa و en وجود داشته باشد.
  • کلیدهای پویا: `"resource." + value.ToString().ToLower()` باید برای همه‌ی اعضای enum ساخته شود.
  • مسیرهای `Resources.Load` باید روی دیسک وجود داشته باشند.
  • قواعد یونیتی: نام فایل == نام کلاسِ MonoBehaviour، وجود .meta، یکتا بودن GUID،
    سالم بودن ارجاع m_Script در صحنه/پریفب، پوشش asmdef برای `using` های بسته‌ها.
  • TextMeshPro: هیچ متنِ UI ای با `UnityEngine.UI.Text` ساخته نشود؛ فونت‌اسست فارسی معرفی‌شده باشد.
  • نسخه: `VersionConfig.json` باید با `ProjectSettings.asset` کاملاً هم‌خوان باشد.
  • متنِ سخت‌کدشده‌ی قابل‌مشاهده: رشته‌ی فارسی در مسیر نمایشِ UI مجاز نیست (لایه‌ی استثنا دارد).

usage:
    python3 Tools/project_lint.py            # گزارش کامل
    python3 Tools/project_lint.py --quiet    # فقط خطاها
    python3 Tools/project_lint.py --json     # خروجی ماشین‌خوان
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PERSIAN = re.compile(r"[\u0600-\u06FF\uFB50-\uFDFF\uFE70-\uFEFF]")
ASSET_DIRS = ["Assets"]

# ---------- ابزارهای خواندن C# ----------


class Masked:
    """متنِ کد با رشته‌ها/کامنت‌ها «پوشیده» شده + فهرست literalها با شماره سطر."""

    def __init__(self, code: str, literals: list, comments: list):
        self.code = code
        self.literals = literals  # list[Literal]
        self.comments = comments  # list[(line, text)]


class Literal:
    def __init__(self, line: int, text: str):
        self.line = line
        self.text = text


unterminated: list[int] = []


def mask_source(source: str) -> Masked:
    out = []
    literals = []
    comments = []
    i = 0
    line = 1
    n = len(source)
    while i < n:
        ch = source[i]
        nxt = source[i + 1] if i + 1 < n else ""
        if ch == "\n":
            out.append("\n")
            line += 1
            i += 1
            continue
        # خط کامنت
        if ch == "/" and nxt == "/":
            start = line
            while i < n and source[i] != "\n":
                i += 1
            comments.append((start, source[source.rfind("\n", 0, i) + 1 : i]))
            continue
        # کامنت بلوکی
        if ch == "/" and nxt == "*":
            start = line
            i += 2
            buf = []
            while i < n and not (source[i] == "*" and i + 1 < n and source[i + 1] == "/"):
                if source[i] == "\n":
                    line += 1
                    out.append("\n")
                i += 1
            i += 2
            comments.append((start, "".join(buf)))
            continue
        # @"verbatim" و $"interpolated" و C#11 """raw"""
        prefix = ""
        j = i
        while j > 0 and source[j - 1] in "$@":
            j -= 1
            prefix = source[j] + prefix
        verbatim = "@" in prefix
        interpolated = "$" in prefix
        if ch == '"' and source[i : i + 3] == '"""':
            end = source.find('"""', i + 3)
            if end == -1:
                end = n
            value = source[i + 3 : end]
            line += value.count("\n")
            literals.append(Literal(line, value))
            i = end + 3
            continue
        if ch == '"':
            start_line = line
            buf = []
            i += 1
            depth = 0
            while i < n:
                c = source[i]
                if verbatim:
                    if c == '"' and i + 1 < n and source[i + 1] == '"':
                        buf.append('"')
                        i += 2
                        continue
                    if c == '"':
                        if depth == 0:
                            i += 1
                            break
                        depth -= 1
                        buf.append(c)
                        i += 1
                        continue
                elif c == "\\":
                    buf.append(source[i : i + 2])
                    i += 2
                    continue
                elif c == '"':
                    i += 1
                    break
                if c == "\n":
                    line += 1
                    if not verbatim:
                        unterminated.append(start_line)
                        break  # رشته‌ی باز (خطای واقعی)
                if interpolated and c == "{":
                    depth += 1
                elif interpolated and c == "}":
                    depth += 1 if depth < 0 else max(0, depth - 1)
                buf.append(c)
                i += 1
            literals.append(Literal(start_line, "".join(buf)))
            out.append("\u0001" * 1)  # جای‌نگهدارِ literal
            continue
        if ch == "'":
            buf = []
            i += 1
            while i < n and source[i] != "'":
                if source[i] == "\\":
                    buf.append(source[i : i + 2])
                    i += 2
                    continue
                buf.append(source[i])
                i += 1
            i += 1
            # کاراکترهای تکی («۰»، '\u0600' …) متنِ رابط کاربری نیستند؛ در ممیزیِ Hardcode نادیده گرفته می‌شوند.
            out.append("\u0002")
            continue
        out.append(ch)
        i += 1
    return Masked("".join(out), literals, comments)


def slice_balanced(text: str, open_index: int) -> tuple[int, int]:
    """از جای «{» برگرداندن (start, end) بدنه‌ی متوازن."""
    depth = 0
    i = open_index
    while i < len(text):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return open_index + 1, i
        i += 1
    return open_index + 1, len(text)


TYPE_DECL = re.compile(r"\b(class|struct|enum|interface)\s+([A-Za-z_]\w*)\s*(:[^\n{;{]+)?")
MEMBER_SIG = re.compile(
    r"""^[^\n{}()]*?\b([A-Za-z_]\w*)\s*(?=(?:\s*(?:\{|;|=>|=|\())|(?:<[^>]*>)?\s*\()""",
    re.MULTILINE,
)
ACCESSIBILITY = re.compile(r"\b(public|internal|private|protected|static|virtual|override|sealed|readonly|const|new|partial|extern|unsafe|async|event|abstract)\b")


def collect_types(text: str) -> dict[str, dict]:
    """نقشه‌ی نامِ نوع → {'members': set, 'kind': str, 'base': str, 'file': str}"""
    types: dict[str, dict] = {}

    def visit(fragment: str):
        for match in TYPE_DECL.finditer(fragment):
            kind, name = match.group(1), match.group(2)
            brace = fragment.find("{", match.end())
            if brace == -1:
                continue
            start, end = slice_balanced(fragment, brace)
            body = fragment[start:end]
            members: set[str] = set()
            if kind == "enum":
                members = {m.strip().split("=")[0].strip() for m in body.split(",") if re.fullmatch(r"[A-Za-z_]\w*", m.strip())}
            else:
                for line in body.splitlines():
                    stripped = line.strip()
                    if not stripped or stripped.startswith(("//", "[", "#")):
                        continue
                    for paren in re.finditer(r"\b([A-Za-z_]\w*)\s*(?:<[^<>]*>)?\s*\(", stripped):
                        members.add(paren.group(1))
                    if re.match(r"^[A-Za-z_][\w<>,\[\]\.\? ]*\s+[A-Za-z_]\w*\s*(?:\{.*)?$", stripped) and "{" in stripped:
                        prop = re.match(r"^[A-Za-z_][\w<>,\[\]\.\? ]*\s+([A-Za-z_]\w*)\s*\{", stripped)
                        if prop:
                            members.add(prop.group(1))
                    assign = re.match(r"^[A-Za-z_][\w<>,\[\]\.\? ]+\s+([A-Za-z_]\w*)\s*[=;]", stripped)
                    if assign:
                        members.add(assign.group(1))
                    for field in re.finditer(r"\b(?:const|readonly|static)\s+[A-Za-z_][\w<>,\[\]\.]*\s+([A-Za-z_]\w*)", stripped):
                        members.add(field.group(1))
                    for evt in re.finditer(r"\bevent\s+[A-Za-z_][\w<>,\[\]\.]*\s+([A-Za-z_]\w*)", stripped):
                        members.add(evt.group(1))
                    for nested in TYPE_DECL.finditer(stripped):
                        members.add(nested.group(2))
                        types.setdefault(nested.group(2), {"members": set(), "kind": nested.group(1), "file": "", "base": "", "events": set(), "methods": set()})
            events = {m.group(1) for m in re.finditer(r"\bevent\s+[A-Za-z_][\w<>,\[\]\.]*\s+([A-Za-z_]\w*)", body)}
            methods = set()
            for line in body.splitlines():
                stripped = line.strip()
                call = re.match(r"^(?:public|internal|private|protected|static|virtual|override|sealed|extern|async|unsafe|partial|[A-Za-z_][\w<>,\[\]\.\?\s])*?\b([A-Za-z_]\w*)\s*(?:<[^<>]*>)?\s*\([^;]*\)\s*(?:\{|;|$)", stripped)
                if call and not stripped.startswith(("if", "for", "while", "switch", "catch", "lock", "using", "return", "new")):
                    methods.add(call.group(1))

            entry = types.setdefault(name, {"members": set(), "kind": kind, "file": "", "base": "", "events": set(), "methods": set()})
            entry.setdefault("events", set()).update(events)
            entry.setdefault("methods", set()).update(methods)
            entry["members"] |= members
            entry["kind"] = kind
            if match.group(3):
                base = match.group(3).strip().lstrip(":").split(",")[0].strip()
                entry["base"] = base
            # بدنه‌ی داخلش کلاسِ تودرتو دارد: یک بار دیگر همان بدنه را ببین.
            if kind != "enum" and TYPE_DECL.search(body):
                visit(body)

    visit(text)
    # اعضایِ ارث‌بری‌شده‌ی درون‌پروژه‌ای را هم اضافه کن.
    for _ in range(3):
        for info in types.values():
            base = info.get("base", "")
            if base in types:
                info["members"] |= types[base]["members"]
                info.setdefault("events", set()).update(types[base].get("events", set()))
                info.setdefault("methods", set()).update(types[base].get("methods", set()))
    return types


# ---------- بررسی‌ها ----------

ERRORS: list[str] = []
WARNINGS: list[str] = []
INFO: list[str] = []


def err(message: str):
    ERRORS.append(message)


def warn(message: str):
    WARNINGS.append(message)


def info(message: str):
    INFO.append(message)


COMMENT_LINE = re.compile(r"//[^\n]*")
COMMENT_BLOCK = re.compile(r"/\*.*?\*/", re.DOTALL)


def raw_code(path: Path) -> str:
    """کدِ خام با حذف کامنت‌ها (رشته‌ها سالم می‌مانند؛ برای بررسی کلیدها و مسیرها)."""
    text = path.read_text(encoding="utf-8")
    text = COMMENT_BLOCK.sub(" ", text)
    text = COMMENT_LINE.sub(" ", text)
    return text


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def iter_source(subdirs=("Assets",)) -> list[Path]:
    files: list[Path] = []
    for sub in subdirs:
        files.extend((ROOT / sub).rglob("*.cs"))
    return sorted(p for p in files if (ROOT / p).exists() or p.exists())


def load_allowlist() -> dict:
    path = ROOT / "Tools" / "localization_allowlist.json"
    if not path.exists():
        return {"internal_contexts": [], "files": {}}
    return json.loads(path.read_text(encoding="utf-8"))


def check_lexical(paths: list[Path]):
    for path in paths:
        source = path.read_text(encoding="utf-8")
        masked = mask_source(source)
        code = masked.code
        for opener, closer, label in (("{", "}", "brace"), ("(", ")", "paren"), ("[", "]", "bracket")):
            if code.count(opener) != code.count(closer):
                err(f"{rel(path)}: توازن {label} درست نیست ({code.count(opener)} باز در برابر {code.count(closer)} بسته)")
        for lineno in sorted(set(unterminated)):
            err(f"{rel(path)}:{lineno}: رشته‌ی متنی بسته نشده است (خطای کامپایل CS1010)")
        for lineno, line in enumerate(source.splitlines(), start=1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            if "locallint-disable" in line:
                continue
            if "\t" in line:
                err(f"{rel(path)}:{lineno}: کاراکتر تب (پروژه با فاصله کار می‌کند)")


def check_symbols(paths: list[Path]) -> dict[str, dict]:
    types: dict[str, dict] = {}
    for path in paths:
        masked = mask_source(path.read_text(encoding="utf-8"))
        found = collect_types(masked.code)
        for name, item in found.items():
            item["file"] = rel(path)
            types.setdefault(name, item)
    return types


PROJECT_IGNORED_TARGETS = set(
    """System UnityEngine Debug Application Mathf Resources PlayerPrefs Object GameObject Component
MonoBehaviour ScriptableObject Transform RectTransform Vector2 Vector3 Color Quaternion Math Random Time
Input Screen Shader Material MaterialPropertyBlock PrimitiveType Mesh Collider BoxMesh GameObjectType List
Dictionary HashSet IEnumerable IEnumerator String Convert Math Enumerable JsonUtility Physics Physics2D
ScreenSpace Canvas CanvasScaler Image Button Text Toggle Slider ScrollRect Outline ColorBlock AssetDatabase
PlayerSettings EditorUtility EditorApplication EditorSceneManager GUILayout GUI Event Texture Texture2D
RenderTexture Graphics GraphicsSettings QualitySettings Network UnityWebRequest EditorStyles Selection
PrefabUtility AssetImporter TrueTypeFontImporter BuildPipeline EditorUserBuildSettings SceneView Handles
SerializedObject SerializedProperty TMP_Text TMP_FontAsset TextMeshProUGUI TextMeshPro""".split()
)


ENGINE_MEMBERS = set("""
gameObject transform name hideFlags tag CompareTag GetComponent GetComponents GetComponentInChildren
GetComponentsInChildren GetComponentInParent GetOrAddComponent TryGetComponent enabled activeSelf
activeInHierarchy SetActive StartCoroutine StopCoroutine StopAllCoroutines Instantiate Destroy
DestroyImmediate FindObjectOfType equals ToString GetHashCode GetType length Count Add Remove Clear
Contains IndexOf InsertAt RemoveAt Sort Reverse ToArray CopyTo ForEach FirstOrDefault Where Select
keys values key value item capacity trim replace split
""".split())

UNITY_BASES = {"MonoBehaviour", "ScriptableObject", "Component", "Behaviour", "GameObject", "Object", "Transform", "Graphic"}


def inherits_unity_type(info: dict, types: dict[str, dict]) -> bool:
    base = info.get("base", "")
    seen = 0
    while base and seen < 4:
        if base in UNITY_BASES:
            return True
        parent = types.get(base)
        if parent is None:
            return base in UNITY_BASES or base in ("MonoBehaviour",)
        base = parent.get("base", "")
        seen += 1
    return False


def check_member_references(paths: list[Path], types: dict[str, dict]):
    static_types = {name for name, item in types.items() if name not in PROJECT_IGNORED_TARGETS}
    for path in paths:
        code = mask_source(path.read_text(encoding="utf-8")).code
        for match in re.finditer(r"\b([A-Z][A-Za-z0-9_]*)\.([A-Za-z_]\w*)", code):
            owner, member = match.group(1), match.group(2)
            if owner not in static_types or owner not in types:
                continue
            known = types[owner]["members"]
            if not known:
                continue
            if member in known or member in ENGINE_MEMBERS or member in ("Instance", "GetType"):
                continue
            if types[owner]["kind"] == "enum" and member.isupper():
                continue
            # کلاس‌هایی که از نوع‌های یونیتی ارث می‌برند عضوهای زمان اجرا/افزونه‌ها را دارند؛
            # فقط وقتی قطعی قضاوت می‌کنیم که نوعِ پایه، یونیتی نباشد.
            if inherits_unity_type(types[owner], types):
                continue
            err(f"{rel(path)}: «{owner}.{member}» یافت نشد (عضوی با این نام در {owner} اعلام نشده) — خطای کامپایل یونیتی")


def check_event_usage(paths: list[Path], types: dict[str, dict]):
    """یونیتی/سی‌شارپ: رویداد را بیرون از کلاسِ declaring نمی‌توان صدا زد و متد را += نکرد (CS0079/CS0070)."""
    for path in paths:
        code = raw_code(path)
        for match in re.finditer(r"\b([A-Z][A-Za-z0-9_]*)\.([A-Za-z_]\w*)\s*(\+=|-=)", code):
            owner, member = match.group(1), match.group(2)
            info = types.get(owner)
            if not info or owner in PROJECT_IGNORED_TARGETS:
                continue
            events = info.get("events", set())
            if member in events:
                continue
            if member in info["members"]:
                err(f"{rel(path)}: «{owner}.{member}» رویداد نیست ولی با {match.group(3)} استفاده شده (خطای CS0079 در Unity)")
        for match in re.finditer(r"\b([A-Z][A-Za-z0-9_]*)\.([A-Za-z_]\w*)\(\)", code):
            owner, member = match.group(1), match.group(2)
            info = types.get(owner)
            if not info or owner in PROJECT_IGNORED_TARGETS:
                continue
            if member in info.get("events", set()) and member not in info.get("methods", set()):
                err(f"{rel(path)}: «{owner}.{member}» یک event است و از بیرون قابل فراخوانی نیست (خطای CS0070 در Unity)")


def check_mono_filename(paths: list[Path]):
    for path in paths:
        source = path.read_text(encoding="utf-8")
        for match in re.finditer(r"class\s+([A-Za-z_]\w*)[^\n]*?:\s*(MonoBehaviour|ScriptableObject)\b", source):
            if match.group(1) != path.stem:
                err(f"{rel(path)}: کلاس MonoBehaviour «{match.group(1)}» باید در «{match.group(1)}.cs» باشد (قاعده یونیتی)")


ENUM_DECL = re.compile(r"enum\s+([A-Za-z_]\w*)\s*\{([^}]*)\}")


def parse_enums(paths: list[Path]) -> dict[str, list[str]]:
    enums: dict[str, list[str]] = {}
    for path in paths:
        code = mask_source(path.read_text(encoding="utf-8")).code
        for match in ENUM_DECL.finditer(code):
            members = [m.strip().split("=")[0].strip() for m in match.group(2).split(",")]
            enums[match.group(1)] = [m for m in members if re.fullmatch(r"[A-Za-z_]\w*", m or "")]
    return enums


def check_localization(paths: list[Path], tables: dict) -> int:
    """کلیدهای Loc.Get باید در جدول باشند؛ برگرداندن تعداد کلیدهای استفاده‌شده."""
    if not tables:
        return 0
    used: set[str] = set()
    pattern = re.compile(r"(?:Loc|LocalizationManager)\.Get\(\s*\"([^\"]*)\"\s*(?=[,)])")
    for path in paths:
        code = raw_code(path)
        for match in pattern.finditer(code):
            used.add(match.group(1))
        for match in re.finditer(r"(?:Loc|LocalizationManager)\.Get\(\s*\"([^\"]*)\"\s*\+", code):
            prefix = match.group(1)
            for language, entries in tables.items():
                if prefix and not any(key.startswith(prefix) for key in entries):
                    err(f"{rel(path)}: هیچ کلیدی با پیشوند «{prefix}» در زبان {language} نیست")
    fa = tables.get("fa", {})
    for key in sorted(used):
        if key not in fa:
            err(f"کلید بومی‌سازی «{key}» در جدول fa وجود ندارد (UI آن را به‌صورت کلید خام نشان می‌دهد)")
    return len(used)


def check_dynamic_keys(paths: list[Path], tables: dict, enums: dict[str, list[str]]):
    """متدهایی مثل GameText.ResourceName که کلید را از نام enum می‌سازند."""
    if not tables:
        return
    fa = tables.get("fa", {})
    for path in paths:
        code = raw_code(path)
        for match in re.finditer(
            r"public static string ([A-Za-z_]\w*)\(([A-Za-z_]\w*)\s+\w+\)\s*\{[^}]*?Get\(\s*\"([^\"]*)\"\s*\+\s*\w+\.ToString\(\)\.ToLower\w*\(\)",
            code,
            re.DOTALL,
        ):
            method, enum_name, prefix = match.group(1), match.group(2), match.group(3)
            members = enums.get(enum_name)
            if not members:
                warn(f"{rel(path)}: {method} برای enum ناشناخته «{enum_name}» بررسی نشد")
                continue
            for member in members:
                key = prefix + member.lower()
                if key not in fa:
                    err(f"{rel(path)}: {method} کلید «{key}» را می‌خواهد که در جدول نیست")


def check_resources_paths(paths: list[Path]):
    pattern = re.compile(r"Resources\.Load(?:<[^>]*>)?\(\s*\"([^\"]+)\"")
    for path in paths:
        code = raw_code(path)
        for match in pattern.finditer(code):
            asset = match.group(1)
            candidates = list((ROOT / "Assets" / "Resources").glob(f"{asset}.*"))
            if not candidates:
                err(f"{rel(path)}: Resources.Load(\"{asset}\") — فایلی در Assets/Resources/{asset}.* وجود ندارد")


def check_unity_asset_hygiene():
    known_guids: dict[str, str] = {}
    for base in ASSET_DIRS:
        root = ROOT / base
        if not root.exists():
            continue
        for path in sorted(root.rglob("*")):
            if path.is_dir():
                if not path.with_suffix(path.suffix + ".meta").exists():
                    err(f"{rel(path)}: پوشه بدون .meta (یونیتی GUID تازه می‌سازد و ارجاع‌ها نا پایدار می‌شوند)")
                continue
            if path.suffix == ".meta":
                text = path.read_text(encoding="utf-8", errors="ignore")
                guid = ""
                for line in text.splitlines():
                    if line.startswith("guid:"):
                        guid = line.split(":", 1)[1].strip()
                if not guid:
                    err(f"{rel(path)}: فایل .meta بدون guid")
                    continue
                if guid in known_guids:
                    err(f"GUID تکراری {guid} بین {known_guids[guid]} و {rel(path)} — ارجاع‌ها اشتباه حل می‌شوند")
                known_guids[guid] = rel(path)
                asset = path.with_suffix("")
                if not asset.exists():
                    err(f"{rel(path)}: متای یتیم (منبع {rel(asset)} وجود ندارد)")
                continue
            if not path.with_suffix(path.suffix + ".meta").exists():
                err(f"{rel(path)}: فایل بدون .meta — یونیتی هنگام باز شدن GUID تازه می‌سازد")

    # ارجاع اسکریپت در صحنه‌ها/پریفب‌ها
    for folder in ("Assets/Scenes", "Assets/Prefabs"):
        root = ROOT / folder
        if not root.exists():
            continue
        for scene in sorted(root.rglob("*.unity")) + sorted(root.rglob("*.prefab")):
            text = scene.read_text(encoding="utf-8", errors="ignore")
            for guid in re.findall(r"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32})", text):
                if guid not in known_guids:
                    err(f"{rel(scene)}: m_Script با guid {guid} به اسکریپت موجود اشاره نمی‌کند (Missing Script)")
    return known_guids


def check_asmdef_coverage(paths: list[Path]):
    definitions: dict[Path, dict] = {}
    for asmdef in sorted((ROOT / "Assets").rglob("*.asmdef")):
        try:
            definitions[asmdef] = json.loads(asmdef.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            err(f"{rel(asmdef)}: JSON نامعتبر ({exc})")
    namespace_to_assembly = {
        "TMPro": "Unity.TextMeshPro",
        "UnityEngine.UI": "UnityEngine.UI",
        "UnityEngine.EventSystems": "UnityEngine.UI",
        "UnityEngine.TextCore.LowLevel": "Unity.TextMeshPro",
        "UnityEditor.TestTools.TestRunner.Api": "UnityEditor.TestRunner",
        "UnityEngine.TestTools": "UnityEngine.TestRunner",
        "NUnit.Framework": "nunit.framework",
    }

    def owning_asmdef(path: Path) -> tuple[Path | None, dict | None]:
        best: Path | None = None
        for candidate in definitions:
            if candidate.parent in path.parents or candidate.parent == path.parent:
                if best is None or len(str(candidate)) > len(str(best)):
                    best = candidate
        if best is None:
            return None, None
        return best, definitions[best]

    for path in paths:
        code = mask_source(path.read_text(encoding="utf-8")).code
        used = set(re.findall(r"^\s*using\s+([A-Za-z_][\w\.]*)\s*;", code, re.MULTILINE))
        assembly, definition = owning_asmdef(path)
        if definition is None:
            continue  # Assembly-CSharp / Assembly-CSharp-Editor: همه‌ی اسمبل‌های autoReferenced در دسترس‌اند
        references = set(definition.get("references", []))
        for namespace, required in namespace_to_assembly.items():
            if not any(u == namespace or u.startswith(namespace + ".") for u in used):
                continue
            if required in ("nunit.framework",):
                continue
            if required not in references:
                err(
                    f"{rel(assembly)}: فایل {rel(path)} از «{namespace}» استفاده می‌کند اما «{required}» "
                    "در references اسمبل‌دیفinition نیست (خطای CS0234/CS0246 در Unity)"
                )


def load_localization_tables() -> dict[str, dict[str, str]]:
    path = ROOT / "Assets/Resources/Localization/LocalizationTable.json"
    if not path.exists():
        err("جدول بومی‌سازی پیدا نشد: Assets/Resources/Localization/LocalizationTable.json")
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        err(f"جدول بومی‌سازی JSON نامعتبر است: {exc}")
        return {}
    tables: dict[str, dict[str, str]] = {}
    for language in data.get("languages", []):
        code = language.get("language")
        entries = {}
        for entry in language.get("entries", []):
            key = entry.get("key", "")
            if not key:
                err("جدول بومی‌سازی: ورودی بدون key")
                continue
            if key in entries:
                err(f"جدول بومی‌سازی({code}): کلید تکراری «{key}»")
            entries[key] = entry.get("value", "")
        tables[code] = entries
    default = data.get("defaultLanguage")
    if default not in tables:
        err(f"جدول بومی‌سازی: زبان پیش‌فرض «{default}» در فهرست زبان‌ها نیست")
    if len(tables) > 1:
        base = set(tables.get(default, {}))
        for code, entries in tables.items():
            if code == default:
                continue
            missing = base - set(entries)
            extra = set(entries) - base
            if missing:
                err(f"جدول بومی‌سازی({code}): {len(missing)} کلید جاافتاده دارد: {', '.join(sorted(missing)[:6])}")
            if extra:
                warn(f"جدول بومی‌سازی({code}): {len(extra)} کلید اضافه دارد: {', '.join(sorted(extra)[:6])}")
    return tables


def check_textmeshpro(paths: list[Path]):
    legacy = re.compile(r"\b(?:AddComponent<\s*Text\s*>|Resources\.GetBuiltinResource<\s*Font\s*>|new Text\b|<Text>\s*\w+\s*=\s*new Dictionary)")
    tmp_files = [p for p in paths if "/UI/" in rel(p) or "UIManager" in p.name]
    for path in tmp_files:
        source = path.read_text(encoding="utf-8")
        if legacy.search(source):
            err(f"{rel(path)}: هنوز از Text/Font قدیمیِ Unity UI استفاده می‌کند؛ باید به TextMeshProUGUI منتقل شود")
    uses_tmp = [p for p in paths if "TMPro" in p.read_text(encoding="utf-8")]
    if not uses_tmp:
        err("هیچ اسکریپت UI از TMPro استفاده نمی‌کند؛ فونت‌اسست فارسیِ TextMeshPro وصل نشده است")
    else:
        info(f"متن‌های TMP: {len(uses_tmp)} فایل از TMPro استفاده می‌کنند")


def check_versions(tables: dict):
    config_path = ROOT / "Assets/Resources/VersionConfig.json"
    if not config_path.exists():
        err("Assets/Resources/VersionConfig.json وجود ندارد")
        return
    config = json.loads(config_path.read_text(encoding="utf-8"))
    settings = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8")

    def yaml_value(key: str) -> str | None:
        match = re.search(rf"^\s*{re.escape(key)}:\s*(.*)$", settings, re.MULTILINE)
        return match.group(1).strip() if match else None

    pairs = [
        ("bundleVersion", config.get("versionName")),
        ("AndroidBundleVersionCode", str(config.get("versionCode"))),
        ("AndroidMinSdkVersion", str(config.get("minSdkVersion"))),
        ("AndroidTargetSdkVersion", str(config.get("targetSdkVersion"))),
        ("companyName", config.get("company")),
        ("productName", config.get("product")),
    ]
    for key, expected in pairs:
        actual = yaml_value(key)
        if actual is None:
            err(f"ProjectSettings.asset: کلید «{key}» پیدا نشد")
            continue
        if actual != str(expected):
            err(f"نسخه ناهم‌خوان: ProjectSettings.{key} = «{actual}» ولی VersionConfig.json = «{expected}» (VersionManager.Apply اجرا نشده)")
    identifiers = re.search(r"applicationIdentifier:\n((?:\s+\w+:.*\n)+)", settings)
    block = identifiers.group(1) if identifiers else ""
    for platform in ("Android", "iPhone", "Standalone"):
        if re.search(rf"{platform}:\s*{re.escape(str(config.get('bundleId')))}\s*$", block, re.MULTILINE) is None:
            err(f"شناسه‌ی بسته‌ی {platform} با VersionConfig.json هماهنگ نیست")
    build = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text(encoding="utf-8")
    if "Assets/Scenes/Main.unity" not in build:
        err("صحنه‌ی Main در Build Settings ثبت نشده")
    if "guid:" not in config_path.read_text(encoding="utf-8"):
        info("VersionConfig فقط مقادیر نسخه را نگه می‌دارد")
    if not config_path.with_suffix(".json.meta").exists():
        err("VersionConfig.json فایل .meta ندارد")
    if "minSdkVersion" in config and int(config["minSdkVersion"]) < 26:
        err("minSdk برای الزامات Google Play (API 26) پایین است")
    if int(config.get("targetSdkVersion", 0)) < 34:
        err("targetSdkVersion باید 34 یا بالاتر باشد (الزام گوگل‌پلی ۲۰۲۴)")


def check_hardcoded_text(paths: list[Path], tables: dict):
    allow = load_allowlist()
    internal_patterns = [re.compile(p) for p in allow.get("internal_contexts", [])]
    file_rules = allow.get("files", {})
    visible_total = 0
    internal_total = 0
    for path in paths:
        source = path.read_text(encoding="utf-8")
        lines = source.splitlines()
        masked = mask_source(source)
        rules = file_rules.get(rel(path), [])
        if any(rule == "*" for rule in rules):
            continue
        literal_by_line: dict[int, list[str]] = {}
        for literal in masked.literals:
            if PERSIAN.search(literal.text):
                literal_by_line.setdefault(literal.line, []).append(literal.text)
        if not literal_by_line:
            continue
        for lineno, values in literal_by_line.items():
            context = lines[lineno - 1] if lineno - 1 < len(lines) else ""
            if "locallint-ignore" in context:
                continue
            if any(rule in context for rule in rules):
                internal_total += len(values)
                continue
            if any(pattern.search(context) for pattern in internal_patterns):
                internal_total += len(values)
                continue
            visible_total += len(values)
            err(
                f"{rel(path)}:{lineno}: متن قابل‌مشاهده‌ی فارسی در کد سخت‌کد شده → «{values[0][:38]}» "
                "(به جدول بومی‌سازی منتقلش کنید)"
            )
    if visible_total == 0:
        info("هیچ متن قابل‌مشاهده‌ی فارسیِ سخت‌کدشده‌ای در Assets/Scripts نمانده است")
    info(f"رشته‌های داخلیِ مجاز (نام گره/لاگ/توضیح تحلیلی): {internal_total}")
    if tables:
        used = set()
        for path in paths:
            used |= set(re.findall(r"(?:Loc|LocalizationManager)\.Get\(\s*\"([^\"]*)\"\s*(?=[,)])", raw_code(path)))
            used |= {key for key in re.findall(r"(?:Loc|LocalizationManager)\.Get\(\s*\"([^\"]*)\"\s*\+", raw_code(path)) if key and not key.endswith(".")}
        prefix_groups: dict[str, int] = {}
        for key in sorted(set(tables.get("fa", {})) - used):
            prefix_groups[key.split(".")[0]] = prefix_groups.get(key.split(".")[0], 0) + 1
        if prefix_groups:
            info("کلیدهای بلااستفاده (شاید از کدِ دیگری/از قبل حذف‌شده خوانده شوند): " + ", ".join(f"{k}:{v}" for k, v in sorted(prefix_groups.items())))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--quiet", action="store_true")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--include-tests", action="store_true", default=True)
    args = parser.parse_args()

    paths = iter_source()
    runtime_paths = [p for p in paths if "/Tests/" not in rel(p) and "/Editor/" not in rel(p)]

    check_lexical(paths)
    types = check_symbols(paths)
    check_member_references(paths, types)
    check_event_usage(paths, types)
    check_mono_filename(paths)
    tables = load_localization_tables()
    enums = parse_enums(runtime_paths)
    used_keys = check_localization(runtime_paths, tables)
    check_dynamic_keys(runtime_paths, tables, enums)
    check_resources_paths(paths)
    check_unity_asset_hygiene()
    check_asmdef_coverage(paths)
    check_textmeshpro(runtime_paths)
    check_versions(tables)
    check_hardcoded_text(runtime_paths, tables)

    if args.json:
        print(json.dumps({"errors": ERRORS, "warnings": WARNINGS, "info": INFO}, ensure_ascii=False, indent=2))
    else:
        if not args.quiet:
            for line in INFO:
                print(f"  i  {line}")
        for line in WARNINGS:
            print(f"  W  {line}")
        for line in ERRORS:
            print(f"  E  {line}")
        print(f"\n{len(ERRORS)} خطا، {len(WARNINGS)} هشدار | {len(paths)} فایل C# | {used_keys} کلید بومی‌سازی در استفاده.")
    if ERRORS:
        return 1
    print("✓ ممیزی ایستا پاس شد (ساختار، ارجاع‌ها، بومی‌سازی، TMP، نسخه).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
