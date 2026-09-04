# چک‌لیست QA

> این چک‌لیست در Unity و روی دستگاه از طریق `Window > General > Test Runner` اجرا می‌شود.
> سطح منطقی بازی با تست‌های EditMode در `Assets/Tests/EditMode` و سطح ساختار/انتشار با
> `python3 Tools/validate_project.py` به‌صورت خودکار بررسی می‌شود.
> ممیزی ایستای کامل (ارجاع‌ها، asmdef، بومی‌سازی، TMP، نسخه): `python3 Tools/project_lint.py`.
> اجرای واقعی در Unity (کامپایل + EditMode + PlayMode + ممیزی Editor): `Tools/unity_validation.sh`
> یا منوی `BaziBaqa > Validation > Run Runtime Validation` — راهنما: `Docs/RuntimeValidation.md`.

## زیرساخت فاز ۲٫۵

- [ ] `Tools/unity_validation.sh` بدون `error CS` و بدون تست شکست‌خورده پایان می‌یابد.
- [ ] `python3 Tools/project_lint.py` صفر خطا می‌دهد (بومی‌سازی، TMP، نسخه، متاها).
- [ ] کنسول Unity پس از باز کردن پروژه، بدون Error و بدون «Missing Script» است.
- [ ] `BaziBaqa > Validation > Run Runtime Validation` نتیجه PASS می‌دهد (`Logs/UnityValidationReport.txt`).
- [ ] `VersionConfig.json` و `ProjectSettings.asset` هم‌خوان‌اند (`BaziBaqa > Version`).

## دودویی

- [ ] Splash بدون خطا نمایش داده می‌شود.
- [ ] آغاز بازی یک جهان تازه می‌سازد.
- [ ] ادامه‌ی بازی فایل اصلی و سپس backup را امتحان می‌کند.
- [ ] خروج از برنامه ذخیره‌ی نهایی می‌سازد.
- [ ] اگر همه‌ی بازماندگان بمیرند، صفحه‌ی پایان می‌آید.
- [ ] زنده ماندن تا روز هفتم صفحه‌ی پیروزی می‌آورد.

## بومی‌سازی (فاز ۲٫۵ — بخش ۲)

- [ ] `python3 Tools/localization_table.py --check` بدون خطا باشد (پوشش کامل fa/en).
- [ ] `python3 Tools/project_lint.py` هیچ «متن قابل‌مشاهده‌ی فارسیِ سخت‌کدشده» گزارش نکند.
- [ ] در بازی، هیچ برچسبی به شکل `[loc:…]` یا `some.key` نمایش داده نشود.
- [ ] اعداد فارسی باشند: روز/ساعت/منابع/سطح‌ها (۱۲ نه 12) — و در انگلیسی برعکسش.
- [ ] نیم‌فاصله و اعراب درست نمایش داده شوند: «می‌شود»، «هزینه‌ی ارتقا»، «ساخت‌وساز».
- [ ] مأموریت‌ها و تصمیم‌های داستانی، متن کامل داشته باشند (عنوان + توضیح + پیامد).
- [ ] نام بازمانده‌های شروع و نیروهای تازه از استخرِ نام جدول بیاید (بدون نامِ تکراری در گروه).
- [ ] ردیف هر بازمانده «نقش + کارِ فعلی» را نشان دهد (مثلاً «نگهبان / گشت‌زنی اطراف اردوگاه»).
- [ ] از تنظیمات، تغییر زبان به English همه‌ی متن‌ها را عوض کند و UI دوباره ساخته شود؛ بعد به فارسی برگردانید.
- [ ] انتخاب زبان پس از بستن و باز کردن بازی حفظ شود (`PlayerPrefs: bazi_baqa_language`).
- [ ] پنجره‌های بازنشده (ساخت/فناوری/مأموریت/تجهیزات/یورش/نقشه/پاداش روز) با «بازگشت اندروید» بسته شوند.
- [ ] گزارش `Logs/LocalizationAuditReport.txt` (منوی `BaziBaqa > Audit > Find Hardcoded Persian Text`) صفر مورد قابل‌مشاهده بدهد.

## تایپوگرافی و فونت فارسی (فاز ۲٫۵ — بخش ۳)

- [ ] `python3 Tools/project_lint.py` هیچ خطایی ندهد (پوششِ حروف، بک‌اندِ TMP، فایل فونت‌ها).
- [ ] در Unity: `BaziBaqa > Typography > Bake Persian TMP Font Asset` موفق باشد و `Assets/Resources/Fonts/Vazirmatn SDF.asset` ساخته شود.
- [ ] `BaziBaqa > Typography > Validate Typography Setup` صفر مشکل بدهد (کنسول: «تایپوگرافی فارسی کامل است»).
- [ ] در Inspector هر برچسبِ UI یک `UIText` دارد (نه `Text` خام) و `Font Asset` آن Vazirmatn SDF است.
- [ ] حروف متصل درست‌اند: «می‌شود» نه «م ی ‌ ش و د»؛ «هزینه‌ی ارتقا» با نیم‌فاصله‌ی سالم.
- [ ] هیچ □/توفو در هیچ متنی دیده نشود؛ اگر دیده شد کاراکتر به `PersianGlyphs.txt` اضافه و دوباره Bake شود.
- [ ] برچسب‌های بزرگ (عنوان، تاجِ اسپلش) زوم/بزرگ‌نمایی شوند و لبه‌ی حروف نسوخته باشد.
- [ ] عددِ HUD با زبان عوض شود: فارسی ۱۲ / انگلیسی 12 — و `٪` درست رندر شود.
- [ ] حالتِ بازگشت: `PlayerPrefs.SetInt("bazi_baqa_text_backend", 1)` ⇒ رابط با Text قدیمی و همان Vazirmatn کامل رندر شود؛ بعد مقدار را صفر کنید.
- [ ] تست‌های `TypographyTests` و `RuntimeValidationPlayModeTests` سبز باشند (کنسول بدون Warning).
- [ ] متن‌های چیده‌شده در Editor با `LocalizedText` پس از تغییر زبان خودکار تازه شوند.

## نسخه و انتشار (فاز ۲٫۵ — بخش ۴)

- [ ] `python3 Tools/project_lint.py` بدون خطا باشد (دروازه‌ی هماهنگی نسخه، از جمله VersionConfig ↔ ProjectSettings).
- [ ] `Assets/Resources/VersionConfig.json` = `0.2.0` و Build Number `2`؛ `ProjectSettings.asset` همان مقادیر را دارد.
- [ ] در Unity: `BaziBaqa > Version > Verify Version Consistency` پیام «منسجم» بدهد.
- [ ] پس از `Bump Patch/Minor/Major` دوباره Verify بگیرید و `versionName` در Inspectorِ Player هم عوض شده باشد.
- [ ] پنجره‌ی «درباره‌ی سازنده» در بازی، `نسخه 0.2.0 (2)` و شناسه‌ی بسته را نشان دهد.
- [ ] `Prepare Android Settings`: IL2CPP + ARM64 + ASTC + minSdk 26 + targetSdk 34 و `activeInputHandler: 0`.
- [ ] `Build AAB (Google Play)` فایل `Builds/Android/BaziBaqa-0.2.0.aab` بسازد (نام با نسخه).
- [ ] اگر keystore گذاشته نشده بیلدِ تستی موفق باشد؛ برای انتشار `BAZIBAAQA_KEYSTORE*` ست شود.
- [ ] `SaveSystem.CurrentSaveVersion` (۳) دست نخورده باشد — فقط با تغییرِ ساختارِ سیو بالا می‌رود.


## سیستم‌ها

- [ ] جمع‌آوری منابع مقدار انبار را افزایش می‌دهد.
- [ ] خرید ناموفق منبع کم نمی‌کند و پیام فارسی نشان می‌دهد.
- [ ] ساخت‌وساز روی زمین و ارتقا هزینه‌ی درست دارد.
- [ ] گرسنگی و تشنگی سلامت را کاهش می‌دهد.
- [ ] مزرعه غذا، نیروگاه انرژی و برج دیده‌بانی دفاع تولید می‌کنند.
- [ ] باران، مه، تغییر نور و حمله‌ی شبانه دیده می‌شوند.
- [ ] مأموریت‌ها با پیشرفت، «آماده» می‌شوند و با «گرفتن پاداش»، منبع و تجربه می‌دهند.
- [ ] دستاوردها (سازنده، مدافع، بازمانده) با انجام شرایط باز می‌شوند.
- [ ] پاداش روزانه هر روز یک‌بار و با ردیف روزهای پیاپی بیشتر می‌شود.
- [ ] مرحله‌ی گروه بالا می‌رود و روحیه/طلا پاداش می‌دهد.
- [ ] در شب، موسیقی به حالت خطر تغییر می‌کند و دشمنان گروهی می‌آیند.
- [ ] پرندگان پرواز و درختان به‌آرامی تکان می‌خورند؛ بدون افت محسوس نرخ فریم.

- [ ] تجهیزات (ابزار/سلاح/زره) با ارتقا اثر ملموس بر جمع‌آوری، حمله و کاهش آسیب دارند.
- [ ] تصمیم‌های داستانی در روزهای ۲/۴/۶ ظاهر می‌شوند و پیامد واقعی دارند.
- [ ] یورش روزانه یک‌بار در روز، با هزینه و غنیمت صحیح؛ در شب و کمبود نگهبان قابل انجام نیست.
- [ ] نقشه‌ی جزیره ساختمان‌ها، منابع، بازمانده‌ها و دشمنان را نشان می‌دهد.
- [ ] پس از اعمال URP طبق `Docs/URPGraphics.md` صحنه بدون Missing Reference باز می‌شود.
## Android

- [ ] کنترل لمس، pinch zoom و دکمه‌ی برگشت تست شده است.
- [ ] متن‌ها در اندازه‌های مختلف خوانا هستند.
- [ ] پروژه در IL2CPP و ARM64 بدون خطای کامپایل ساخته می‌شود.

| بررسی | روش | انتظار |
|---|---|---|
| ساختارِ لایه‌ی گرافیک | `python3 Tools/project_lint.py` | `0 خطا، 0 هشدار`؛ شاملِ بررسیِ شیدر، CBUFFER، بافت‌ها و تک‌نویسنده‌ی مه |
| میزِ شیدرها | `BaziBaqa > Rendering > Validate Rendering Setup` | بدونِ «خطای کامپایل»؛ هر سه شیدر از `Resources` بارگذاری شوند |
| نصبِ URP | `BaziBaqa > Rendering > Install URP Assets` | چهار فایل در `Assets/Settings/URP` + اتصالِ هر سطحِ کیفیت؛ گزارش در `Logs/RenderingSetupReport.txt` |
| بافت‌های بی‌درز | `python3 Tools/procedural_textures.py --check` | ۱۵ فایلِ موجود و حجمِ معقول (< ۲ مگابایت منبع) |
| تست‌های ایستایِ گرافیک | Test Runner → EditMode → `RenderingStackEditModeTests` | ۱۵ تست سبز |
| تست‌های زنده‌ی گرافیک | Test Runner → PlayMode → `GraphicsPlayModeTests` | ۹ تست سبز؛ با URP نصب‌شده `volume=active` در لاگ |
| تک‌نویسنده‌ی نور/مه | `python3 Tools/project_lint.py` و تستِ `SkyLightingRig_IsTheOnlyRenderSettingsWriter` | `RenderSettings` فقط در `SkyLightingRig.cs` نوشته می‌شود |
| چرخه‌ی شب و روز | Play → `GraphicsPlayModeTests.SkyRig_NoonAndMidnight_FeelDifferent` | ظهر و نیمه‌شب در نور/محیطی/مه تفاوتِ واقعی دارند |
| بودجه‌ی چراغ‌ها | `SkyRig_LampBudgetIsRespectedAtNight` | `LampsActive ≤ lampBudget` در هر سطح |
| محیط زنده (باد/چمن/زیست‌بوم) | Test Runner → EditMode → `EnvironmentVisualsEditModeTests` | ۶ تست سبز (تک‌نویسنده‌ی باد، بذرِ جدا، یک draw call) |
| بی‌خطر‌بودنِ پراکندگی روی Gameplay | PlayMode → `FoliageScatter_BuildsOneMeshWithoutTouchingGameplayObjects` | `childCount` منابع/ساختمان‌ها عوض نمی‌شود |
| پاسخِ باد به هوا | PlayMode → `WindField_WritesWindGlobalAndAnswersToWeather` | طوفان، شدتِ `_BaziWindState` را بالا می‌برد |
| رگرسیونِ Gameplay | `Tools/unity_validation.sh` + تست‌های موجود | هیچ تستِ منطق/ذخیره/رابطی نباید قرمز شود |

یادآوری: در محفظه‌ی توسعه‌ی این مخزن، Unity نصب نیست؛ لذا «بیلد» به‌صورت ایستا + ابزارهای
ارائه‌شده راستی‌آزمایی شده و اجرای واقعیِ Test Runner/Shader Compiler بر عهده‌ی ماشین توسعه‌دهنده است.
