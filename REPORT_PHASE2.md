# گزارش نهایی فاز ۲ — پایه‌ی کیفیت حرفه‌ای (Bazi Baqa)

تاریخ: ۲۰۲۶-۰۹-۰۴ | شاخه: `arena/01a06d97-bazi-baqa` | HEAD: `5fbec23`

## خلاصه

پروژه پس از رفع مشکلات بحرانی فاز ۰/۱، برای پایه‌ی کیفیت حرفه‌ای آماده شد. هر بخش در یک کامیت
جدا ثبت شد و طبق قاعده پیش از هر تغییر، یک کامیت محافظتی (`2566388`) گرفته شد. اجرای کامل در
Unity (ویندوزِ Unity) در این محیط ممکن نبود، بنابراین همه‌ی بررسی‌ها به‌صورت ایستا انجام شد و
ابزارهای Editor برای اجرای خودکار داخل Unity تحویل داده شدند.

## زنجیره‌ی کامیت‌ها

| بخش | کامیت | شرح |
|------|-------|-----|
| پایه‌ی محافظتی | `2566388` | عکس‌برداری محافظتی پیش از تغییرات |
| ۱ | `da140a6` | Professional Unity Audit |
| ۲ | `6173cf5` | Added PlayMode Testing |
| ۳ | `537618e` | Improved Localization System |
| ۴ | `1a11beb` | Upgraded Persian Font Support |
| ۵ | `8a38fb3` | Improved Version Management |
| ۶ | `5fbec23` | Initial Performance Optimization |

---

## بخش ۱ — ممیزی حرفه‌ای Unity (`da140a6`)

ابزار `Assets/Editor/ProjectAudit.cs` اضافه شد (منو: `BaziBaqa/Audit/Run Full Audit`) که
پیش از هر بیلد یک اسکن ایستا و بدون مزاحمت انجام می‌دهد:
- وجود صحنه‌های فعال بیلد روی دیسک.
- نبودِ مرجع اسکریپتِ شکسته (broken script / missing m_Script) در صحنه‌ها و پریفب‌ها (با بررسی GUID).
- نبودِ GUID تکراری در فایل‌های `.meta`.
- نبودِ فایلِ بدون `.meta` در شاخه‌ی `Assets`.
- ترتیب اجرای صریح کلاس‌های حیاتی.

به همه‌ی سامانه‌های حیاتی (`GameBootstrap`, `GameManager`, `UIManager`, `RuntimeLogger`,
`PerformanceManager`, `AudioManager`, `WorldGenerator`) `[DefaultExecutionOrder]` داده شد تا
ترتیب اجرای `Awake` پیش‌بینی‌پذیر شود. `AndroidBuild` پیش از ساخت، ممیزی را اجرا و در صورت
مشکل سخت، بیلد را متوقف می‌کند (دروازه‌ی کیفیت).

## بخش ۲ — سیستم تست PlayMode (`6173cf5`)

سامانه‌ی تست حرفه‌ای با اسمبل‌بندی جدا `BaziBaqa.PlayModeTests` اضافه شد و تست‌های `PlayMode`
(اجرای واقعی بازی از مسیر `GameBootstrap`) برای این سناریوها نوشته شد:
- شروع بازی (فاز، بازمانده‌ها، منابع اولیه، جهان).
- جمع‌آوری منبع و ردِ هزینه‌ی بیشتر از موجودی.
- ساخت ساختمان (انتخاب/لغو محل ساخت، اردوگاه پیش‌فرض).
- ذخیره روی دیسک و بررسی وجود فایل ذخیره.
- بارگذاری و بازگردانی وضعیت ذخیره‌شده.
- سلامت Save پس از چرخه‌ی ذخیره/بارگذاری (نسخه‌ی ثابت + مجموعه‌های مهاجرت‌شده).
- سامانه‌ی مأموریت (سه مأموریتِ متمایز، بدون تکرارِ چرخش).
- سامانه‌ی یورش (بازه‌ی شانس و اجرای بدون خطا).

هر تست پس از پایان، سامانه (مدیر، دوربین، EventSystem) را کاملاً پاک‌سازی می‌کند.
(اجرای این تست‌ها نیازمند Unity است؛ کد آن‌ها به‌صورت ایستا اعتبارسنجی شده.)

## بخش ۳ — ارتقای سیستم بومی‌سازی (`537618e`)

`LocalizationManager` واقعی + رابطِ `Loc` ساخته شد:
- جدول JSON با ساختار JsonUtility (`defaultLanguage` + زبان‌ها + direction + entries).
- `SetLanguage` / `ApplySavedLanguage` با ذخیره در `PlayerPrefs`.
- `Get(key)` و `Get(key, args)` با رفتارسازی لایه‌ای: زبان فعلی ← زبان پیش‌فرض ← پیش‌فرض داخلی ← خودِ کلید.
- پیش‌فرض‌های داخلیِ فارسی تا بدون بارگذاری جدول هم همه‌ی سامانه‌ها کار کنند.

`GameText` (نام منبع/ساختمان/نقش/آب‌وهوا) و متن‌های هویتی اسپلش (عنوان، تگ‌لاین، استودیو،
مدیر، بارگذاری، فوتر، درباره) از `Loc` خوانده می‌شوند. جدول به
`Assets/Resources/Localization/LocalizationTable.json` منتقل و در شروع بازی بارگذاری می‌شود.
ابزار `LocalizationAudit` (منو: `BaziBaqa/Audit/Find Hardcoded Persian Text`) متن‌های فارسیِ
سخت‌کدشده‌ی باقی‌مانده را پیدا و گزارش می‌کند. تست EditMode برای مدیر/رابط اضافه شد.

## بخش ۴ — ارتقای فونت فارسی (`1a11beb`)

فونت **Vazirmatn** (مجوز SIL OFL) به‌همراه Bold اضافه شد و به‌صورت برنامه‌ای بررسی شد که
Vazirmatn شامل «Presentation Forms» نیازمندیِ خطِ شکل‌دهی موجود و نیز GSUB/GPOS و اعداد فارسی
است، بنابراین جابه‌جایی امن است. `GameFont` با زنجیره‌ی fallback (Vazirmatn ← DejaVuSans ← Arial)
ساخته و `UIManager` و `WorldGenerator` به آن وصل شدند. `PersianText` اعداد لاتین را به فارسی
(۰-۹) تبدیل می‌کند و چینش RTL و شکل‌دهی را حفظ می‌کند. تست EditMode برای تبدیل ارقام اضافه شد.

## بخش ۵ — سیستم مدیریت نسخه (`8a38fb3`)

`VersionConfig.json` (منبع واحد حقیقت) + `GameVersion` (زمان اجرا) + `VersionManager` (Editor)
اضافه شد. `VersionManager` (منو: `BaziBaqa/Version`) این مقادیر را روی `PlayerSettings` اعمال
می‌کند: `bundleVersion`، `Android bundleVersionCode`، `applicationIdentifier` (Android/Standalone/iOS)،
نام شرکت/محصول و min/target SDK؛ و برای افزایش Major/Minor/Patch نسخه را بالا می‌برد و کد نسخه‌ی
اندروید را افزایش می‌دهد. `ProjectSettings` از قبل با پیکربندی هماهنگ است
(`0.1.0`، کد `1`، `com.parsaapps.bazibaqa`). تست EditMode برای تجزیه‌ی نسخه اضافه شد.

## بخش ۶ — بهینه‌سازی اولیه (`5fbec23`)

- `ObjectPool<T>` که بلااستفاده بود، به یک استخر حرفه‌ای (با `Clear(onDispose)`) ارتقا یافت و
  برای **نشانگرِ ساخت** در `ConstructionSystem` فعال شد: به‌جای ساخت/نابودیِ استوانه و Material
  در هر انتخابِ سازه، نشانگر با `Get/Release` (و `SetActive`) و Materialِ کش‌شده بازیابی می‌شود.
  `OnDestroy` استخر و Material را آزاد می‌کند.
- `PerformanceManager` به `Application.lowMemory` گوش می‌دهد تا هنگام فشار حافظه، کیفیت را کم و
  `Resources.UnloadUnusedAssets()` را صدا بزند (و در نابودی unsubscribe کند).
- ابزار `PerformanceAudit` (منو: `BaziBaqa/Audit/Performance & Assets`) به‌صورت ایستا منبع
  تکراری (هش محتوا)، منبعِ به‌ظاهر بلااستفاده و راهنمای کاهش Draw Call/حافظه را گزارش می‌کند.

---

## چه تست‌هایی انجام شد

در غیاب Unity، اعتبارسنجی فقط ایستا بود:
- تعادل آکلاد/پرانتز/براکت در همه‌ی ۵۴ فایل `.cs`: **۰ ناهماهنگی**.
- اجرای `Tools/validate_project.py`: خروجی ۰ با «اعتبارسنج موفق: ۵۰ اسکریپت…».
- اعتبارسنجی JSON برای جدول بومی‌سازی، پیکربندی نسخه و اسمبل‌بندی؛ و بررسی ارجاع‌های متقابل
  (مثل `ObjectPool<GameObject>`، `Application.lowMemory`، `GameFont.Persian`، `Loc.Get`).
- بررسی برنامه‌ایِ پوشش گلیف‌های Vazirmatn (شامل Presentation Forms و اعداد فارسی).
- اسکریپت‌های Editor (ممیزی، نسخه، کارایی، بومی‌سازی) با ساختار منسجم ساخته و به Unity تحویل شدند.

### نکته‌ی مهم درباره‌ی محدودیت
- کامپایل واقعی C#، اجرای PlayMode، باز کردن Unity برای بررسی Console Error/مشکل Missing
  Reference/پریفب خراب، و اندازه‌گیری واقعی Draw Call/حافظه **امکان‌پذیر نبود**. این موارد با
  ابزارهای تحویل‌شده (ProjectAudit / PerformanceAudit / LocalizationAudit / تست‌های PlayMode)
  در داخل Unity انجام‌پذیرند.

---

## وضعیت آمادگی برای مرحله‌ی AAA Graphics

پایه برای گرافیک AAA آماده است ولی خودِ گرافیک هنوز اعمال نشده:
- ساختار حرفه‌ای زیر `Assets/`، ترتیب اجرای صریح، ممیزی پیش از بیلد، ابزارهای Editor، تست‌ها و
  سیستم بومی‌سازی برقرار است.
- فونت Vazirmatn به‌عنوان پایه‌ی متن فارسی اضافه شد؛ «پاس Bloom/AO در Editor با URP تکمیل می‌شود»
  (طبق یادداشت WorldVFX). `com.unity.textmeshpro` در manifest است.
- برای AAA Graphics، کارهای بعدی در Unity: تبدیل متن‌ها به TextMeshPro با فونت‌اسست Vazirmatn
  (بهتر از `Text` قدیمی)، استفاده از URP/HDRI، نورپردازی و فضایی ورودی.

## مشکلات باقی‌مانده / پیشنهاد بعد

1. **اجرای ابزارها در Unity**: `ProjectAudit`، `PerformanceAudit`، `LocalizationAudit` و تست‌های
   `PlayMode` باید در Unity اجرا شوند؛ خطاهای احتمالی کنسول که آن‌ها پیدا می‌کنند باید برطرف شود.
2. **بومی‌سازی کامل**: ۴۱۹ رشته‌ی فارسی سخت‌کدشده در ۳۰ فایل باقی مانده (بیشتر در `UIManager`).
   با `LocalizationAudit` مرحله‌به‌مرحله به جدول منتقل شود.
3. **هاد کدکش‌کردن نسخه**: `VersionManager.Bump` و `Apply` باید یک‌بار در Unity اجرا شوند تا
   مقادیر `ProjectSettings` قطعاً با `VersionConfig.json` هماهنگ شوند (اسکریپت به‌صورت ایستا صحیح است).
4. **فونت TMP**: برای بالاترین خوانایی و شکل‌دهی، فونت‌اسست TextMeshPro از Vazirmatn در Editor
   ساخته شود (در حالت فعلی از `Text` قدیمی + `PersianText` استفاده می‌شود).
5. **اجرای فاز بعد (AAA Graphics)** با URP و نورپردازی، مستلزم باز کردن Unity و انجام ویرایش‌های
   گرافیکیِ غیرقابل‌انجام به‌صورت متن است.
