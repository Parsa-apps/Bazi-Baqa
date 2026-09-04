# اعتبارسنجی زمان اجرا در Unity — Bazi Baqa

هدف این سند: هر تغییر مهم **پیش از** رفتن به مرحله‌ی بعد، در Unity واقعی سنجیده شود؛ نه فقط ایستا.
چهار لایه دارد و هر لایه ابزار خودش را دارد.

```
لایه ۱  ساختار/کامپایل    →  Tools/unity_validation.sh  +  python3 Tools/project_lint.py
لایه ۲  تست‌های واحد      →  Test Runner (EditMode + PlayMode)
لایه ۳  سناریوهای اجرا    →  Assets/Tests/PlayMode/RuntimeValidationPlayModeTests.cs
لایه ۴  ممیزی‌های Editor  →  BaziBaqa > Audit / Validation / Typography منوها
```

## ۱) اجرای خودکار در خط فرمان (توصیه‌شده برای CI)

```bash
# از ریشه‌ی پروژه
UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.50f1/Unity.app/Contents/MacOS/Unity" \
  Tools/unity_validation.sh

# لینوکس
UNITY_BIN="/opt/Unity/Editor/Unity" Tools/unity_validation.sh

# فقط EditMode (سریع‌تر)
Tools/unity_validation.sh skip-playmode
```

اسکریپت این کارها را انجام می‌دهد و در صورت بروز خطا کد خروجی غیرصفر می‌دهد:

0. پیش‌بررسی ایستا (بدون Unity): `project_lint.py` + `localization_table.py --check` + `validate_project.py`.
1. کامپایل کامل پروژه (`error CS…` در لاگ = شکست).
2. اجرای `BaziBaqa.EditorTools.TypographyBaker.BakeBatch` (بیک assetِ فونتِ TMP + پوششِ حروف).
3. اجرای `BaziBaqa.EditorTools.VersionManager.VerifyBatch` (هماهنگی VersionConfig ↔ PlayerSettings).
4. اجرای تست‌های **EditMode** و **PlayMode** با `-runTests` و تجزیه‌ی XML نتیجه.
5. اجرای `BaziBaqa.EditorTools.RuntimeValidation.ValidateBatch` (ممیزی صحنه، Save/Load، UI، تایپوگرافی، نسخه، بومی‌سازی).
6. پالایش لاگ‌ها برای `Missing Script/Reference`، APIهای منسوخ‌شده، «Unable to load font face» و اخطارهای مهم.

کد خروجی: `0` = همه‌چیز پاس، `1` = مشکل در بررسی‌ها، `127` = بررسی‌های ایستا پاس شد ولی Unity در این محیط نیست
(پس مراحلِ ۱ تا ۶ اجرا نشده‌اند).

همه‌ی لاگ‌ها در `Logs/` نوشته می‌شوند (این پوشه در `.gitignore` است).

## ۲) معادلِ ایستا (بدون نصب Unity)

```bash
python3 Tools/project_lint.py        # ممیز ایستای کامل
python3 Tools/validate_project.py    # ساختار پروژه + تنظیمات انتشار
```

`Tools/project_lint.py` دقیقاً همان دسته‌ی خطاهایی را می‌گیرد که در کنسول Unity ظاهر می‌شوند:
توازن ساختاری C#، عضوهای ناموجود روی نوع‌های پروژه (`CS1061`)، استفاده‌ی نادرست از رویدادها
(`CS0079` / `CS0070`)، `using` های بدون اسمبل‌دیفinition (`CS0234/CS0246`)، کلیدهای
بومی‌سازیِ جاافتاده، مسیرهای `Resources.Load`، فایل‌های بدون `.meta`، `GUID` تکراری،
ارجاع‌های شکسته‌ی صحنه، عدم‌هماهنگی نسخه و متنِ سخت‌کدشده‌ی قابل‌مشاهده.

## ۳) منوهای داخل Editor

| منو | کار |
|------|------|
| `BaziBaqa > Validation > Run Runtime Validation` | همه‌ی بررسی‌های زمان اجرا + نوشتن `Logs/UnityValidationReport.txt` |
| `BaziBaqa > Audit > Run Full Audit` | ممیزی ساختاری (متا، GUID، صحنه‌ها، ترتیب اجرا) |
| `BaziBaqa > Audit > Find Hardcoded Persian Text` | متن‌های قابل‌مشاهده‌ی نمانده در کد + کلیدهای جاافتاده |
| `BaziBaqa > Audit > Performance & Assets` | منبع تکراری/بلااستفاده و راهنمای Draw Call |
| `BaziBaqa > Typography > Bake Persian TMP Font Asset` | ساخت assetِ فونتِ Vazirmatn برای TMP (+ import خودکارِ Essentials) |
| `BaziBaqa > Typography > Validate Typography Setup` | پوششِ حروفِ جدول، includeFontData، درستی assetها |
| `BaziBaqa > Typography > Repair Font Import Settings` | اصلاح تنظیمات import فونت‌ها |
| `BaziBaqa > Fonts > Build Persian TMP Font Assets` | ساخت فونت‌اسست TextMeshPro فارسی |
| `BaziBaqa > Version > Apply Version To Player Settings` | اعمال نسخه از `VersionConfig.json` |
| `BaziBaqa > Build > Build APK/AAB` | بیلد با دروازه‌ی ممیزی |

`RuntimeValidation` چه چیزی را بررسی می‌کند:

- **کامپایل**: `EditorUtility.scriptCompilationFailed`.
- **بارگذاری صحنه**: باز کردن `Assets/Scenes/Main.unity`، شمردن گره‌ها و پیدا کردن `Missing Script`
  (از طریق `SerializedObject.m_Script`).
- **ذخیره/بارگذاری**: نوشتن فایل، خراب‌کردن عمدی، بازیابی از پشتیبان، بررسی مهاجرت نسخه.
- **تعامل UI**: ساخت بازی، یافتن `Canvas` و `GraphicRaycaster`، شمارش دکمه‌های قابل‌کلیک،
  باز/بسته‌کردن پنل‌ها با `HandleBack`، نبودِ «کلیدِ خام» در متن‌ها و نبودِ برچسب بدون فونت.
- **پایش کنسول**: هر `Error/Exception` که در طول اجرا ثبت شود، باعث شکست می‌شود.

## ۴) Test Runner (دستی)

`Window > General > Test Runner` → **Run All** روی هر دو تب EditMode و PlayMode.

تست‌های کلیدی:

| تست | چه چیزی را می‌سنجد |
|------|--------------------|
| `SceneLoading_MainSceneBuildsBootstrapWithoutMissingReferences` | صحنه بار می‌شود، بوت‌استرپ دارد، کامپوننت شکسته ندارد |
| `UserInterface_BuildPanelAndBackButton_StayResponsive` | پنل‌ها باز/بسته می‌شوند؛ دکمه‌ی بازگشت اندروید کار می‌کند |
| `UserInterface_EveryButton_IsClickableThroughRaycaster` | هر دکمه `targetGraphic` و `raycastTarget` سالم دارد |
| `UserInterface_PersianTextIsRenderedWithGameFont` | هیچ برچسبی بدون فونت نیست و کلید خام نمایش داده نمی‌شود |
| `UserInterface_RtlPersianLayout_UsesRightAlignment` | چینش راست‌به‌چپ روی برچسب‌ها اعمال شده است |
| `SaveLoad_ThroughUserInterface_RestoresProgress` | ذخیره از UI و بازگردانی مقدارها |
| `Notification_ReachesHudLabel` | اعلانِ سیستم به برچسب HUD می‌رسد |
| `QualityGateEditModeTests` | سلامت SaveSystem، مهاجرت نسخه، هماهنگی `VersionConfig` با PlayerSettings |

## وضعیت فعلی (پایان فاز ۲٫۵)

- Unity در محیط توسعه‌ی متنیِ این فاز در دسترس نبود؛ بنابراین **اجرای واقعی** با ابزارهای بالا
  تحویل داده شد و همه‌ی بررسی‌های ممکن به‌صورت ایستا انجام گرفت.
- خطاهای واقعی که با همین ممیزی ایستا پیدا و رفع شد:
  1. `GameEvents.StateChanged` متد بود و در `UIManager` با `+=` عضو می‌شد → **CS0079** (کامپایل نشوه).
  2. `Assets/Editor/AndroidBuild.cs` از `EditorUserBuildSettings.androidBuildSystem` و
     `androidBuildType` استفاده می‌کرد که در Unity 2022.3 **حذف** شده‌اند → CS0117؛
     و `androidETC2Fallback` که **منسوخ** است → Warning.
  3. `BaziBaqa.Runtime.asmdef` هیچ `references` نداشت در حالی که کد از `UnityEngine.UI`
     (و بعداً `TMPro`) استفاده می‌کند → CS0234/CS0246.
  4. `LocalizationManager.cs` و `RuntimeLogger.cs` فایل `.meta` نداشتند؛ پوشه‌های
     `Assets/Editor` و `Assets/Tests/PlayMode` هم متای پوشه نداشتند → GUID ناپایدار/اخطار ممیزی.
  5. `SurvivorAgent` با `transform.Find("بدن")` به نامِ فارسیِ گره وابسته بود (با هر تغییر متن،
     رفرنس می‌شکند) → نام‌های درختی به کلیدهای ثابتِ انگلیسی منتقل شد.
  6. جدول بومی‌سازی و پیش‌فرض‌های کد با هم ناهم‌خوان بودند (`game.tagline`) و متن‌ها
     در دو جا نگهداری می‌شدند.
- برای اطمینان کامل، یک‌بار در Unity واقعی اجرا کنید:

```bash
Tools/unity_validation.sh
```

اگر `Logs/*.log` خطای جدیدی نشان داد، خروجی را برای تیم بفرستید؛ ابزارها طوری نوشته شده‌اند
که مشکل را با نام فایل و سطر گزارش کنند.
