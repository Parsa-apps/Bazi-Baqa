# سیستم تایپوگرافی فارسی (فاز ۲٫۵ — بخش ۳)

هدف: متنِ بازی با فونتِ **Vazirmatn** و روی **TextMeshPro** رندر شود؛ خوانا در موبایل، راست‌به‌چپ،
با ارقام فارسی و بدون «توفو» (□). کلِ مسیر در یک لایه بسته شده تا کدِ بازی هرگز مستقیم `Text`
یا `TextMeshProUGUI` نسازد.

## معماری

| فایل | نقش |
| --- | --- |
| `Assets/Scripts/UI/UIText.cs` | تنها راه ساخت/به‌روزرسانی متن. بک‌اند را انتخاب و همه‌ی تنظیمات را روی آن می‌نویسد. |
| `Assets/Scripts/Core/GameTextBackend.cs` | تصمیمِ «TMP آماده است یا نه»، نگاشتِ چینش‌ها، سیاستِ جهت، `RebuildAll()`. |
| `Assets/Scripts/Utilities/GameFont.cs` | بارگذاری assetِ فونتِ TMP (بیک‌شده یا داینامیک)، فونتِ TTF برای مسیرِ قدیمی، خواندن مجموعه‌حروف. |
| `Assets/Scripts/UI/LocalizedText.cs` | کامپوننتِ «متنِ چیده‌شده در Editor» که به کلیدِ بومی‌سازی وصل است. |
| `Assets/Scripts/Utilities/PersianText.cs` | شکل‌دهیِ دستی برای مسیرِ قدیمی و برچسب‌های سه‌بعدی (`TextMesh`). |
| `Assets/Editor/TypographyBaker.cs` | منوهای بیک/اعتبارسنج/اصلاح import. |
| `Assets/Resources/Fonts/PersianGlyphs.txt` | مجموعه‌حروف: یک منبع برای بیکر و برای دروازه‌ی پوششِ حروف. |

### چرا لایه‌بسته (و نه «فقط TMP»)؟

`TextMeshProUGUI` بدون import شدن «TMP Essential Resources» هیچ متنی رندر نمی‌کند و کنسول پر از
خطا می‌شود. پس `UIText` اول `GameTextBackend.TmpReady` را می‌پرسد:

1. `TMP_Settings.instance != null` (Essentials import شده) **و** یک assetِ فونتِ قابل استفاده هست
   ⇒ `TextMeshProUGUI` با assetِ `Fonts/Vazirmatn SDF`؛
2. در غیر این صورت ⇒ `UnityEngine.UI.Text` با `Resources/Fonts/Vazirmatn.ttf` (همان فونت، رندرِ قدیمی).

نتیجه: در هر کلونی — با TMP بیک‌شده یا بدون آن — رابط کامل و فارسی است و کنسول خطا نمی‌دهد.
برای عیب‌یابی می‌توان مسیرِ قدیمی را تحمیل کرد:

```
PlayerPrefs.SetInt("bazi_baqa_text_backend", 1);   // ۱ = فقط Text قدیمی، ۰ = خودکار
```

## TMP و فارسی

* **شکل‌دهی حروف و RTL**: خودِ TMP انجام می‌دهد (shaping + الگوریتم دوطرفه). به همین دلیل
  `UIText` در مسیرِ TMP هرگز `PersianText.Process` را صدا نمی‌زند؛ این تابع فقط برای
  `Text`/`TextMesh` است (اگر روی TMP اعمال شود حروف دوبار وارونه و خراب می‌شوند).
* **ارقام**: `GameTextBackend.LocalizeDigits` در زبانِ فارسی ارقام لاتین را به `۰-۹` تبدیل می‌کند
  و در انگلیسی دست نمی‌زند — یعنی با تغییر زبان، هم متن و هم عدد عوض می‌شود.
* **کرنینگ**: assetِ فونت از جدولِ OpenType/GPOS خودِ Vazirmatn استفاده می‌کند؛ فاصله‌ی حروف
  متصل‌شونده (مثل «ـی» پایان کلمه) به Atlas اضافه می‌شود و نیاز به تنظیم دستی ندارد.
* **ضخیم**: اگر `Vazirmatn-Bold SDF.asset` بیک شده باشد، `FontStyles.Bold` همان asset را
  استفاده می‌کند؛ اگر نباشد TMP همان asset را با شبیه‌سازیِ ضخامت به کار می‌گیرد.

## یک‌بار راه‌اندازی (در Editor)

1. `BaziBaqa > Typography > Bake Persian TMP Font Asset`
   — اگر Essentials نباشد، خودش import می‌کند؛ سپس `Assets/Resources/Fonts/Vazirmatn SDF.asset`
   (و نسخه‌ی ضخیم) را می‌سازد و در `TMP Settings` به‌عنوان فونتِ پیش‌فرض ثبت می‌کند.
2. `BaziBaqa > Typography > Validate Typography Setup` — پوششِ حروف و تنظیمات import را می‌سنجد.
3. `BaziBaqa > Typography > Repair Font Import Settings` — اگر `includeFontData` خاموش شده باشد
   (علتِ رایجِ خطای `Unable to load font face for [x]`) درستش می‌کند.

همین سه مرحله در `Assets/Editor/RuntimeValidation.cs` هم خودکار اجرا می‌شود (`CheckTypography`)،
پس در خط فرمان هم پوشش داده شده‌اند.

### تنظیماتِ انتخاب‌شده در بیکر

| تنظیم | مقدار | دلیل |
| --- | --- | --- |
| Sampling point size | ۹۰ | لبه‌ی حروف در رزولوشن موبایل/تبلت نرم و تار نمی‌شود |
| Atlas padding | ۹ | جا برای خط‌کشی/سایه/بورد بدون بریده‌شدن حروف |
| Atlas | ۲۰۴۸×۲۰۴۸ | کل مجموعه‌حروفِ فارسی + لاتین در یک atlas؛ بدون resize در بازی |
| Render mode | SDFAA | یکنواخت برای اندازه‌های کوچکِ HUD |
| Population | Dynamic | کاراکترِ تازه (آیکن/متنِ جدید در آینده) بی‌آنکه بیک دوباره لازم شود اضافه می‌شود |

## دروازه‌های خودکار

| دروازه | چه می‌گیرد |
| --- | --- |
| `Tools/project_lint.py` → `check_textmeshpro` | هیچ فایل UI جز لایه‌ی بک‌اند `Text`/`Font` خام نسازد؛ `UIText` واقعاً TMP را به کار برد؛ فایلِ فونت و مجموعه‌حروف موجود باشند؛ **هر کاراکترِ غیرلاتینِ جدول در مجموعه‌حروف باشد**. |
| `Tools/project_lint.py` → `check_hardcoded_text` | متنِ قابل‌مشاهده در کد نماند (مجموعه‌حروف در فایلِ txt است تا این قانون لازم نشده باشد). |
| `Assets/Tests/EditMode/TypographyTests.cs` | ۱۱ تست: import، پوششِ حروف، dedup، قراردادِ مسیرها، انتخابِ بک‌اند، ارقام، چینشِ راست‌به‌چپ، بازخوانیِ کلید با تغییر زبان. |
| `Assets/Tests/PlayMode/…#Typography_EveryLabelIsDrivenByTheTextBackend` | در زمان اجرا هر برچسب دقیقاً یک بک‌اند دارد و همان assetِ Vazirmatn روی آن نشسته است. |
| `BaziBaqa > Validation > Run Runtime Validation` | بیک + اعتبارسنجی + ممیزی کنسول، در batchmode هم قابل اجرا. |

اگر کاراکتری در متنِ تازه استفاده شود و در `PersianGlyphs.txt` نباشد، `Tools/project_lint.py`
خطا می‌دهد و کدِ آن را با `U+XXXX` نشان می‌دهد؛ کافی است کاراکتر به فایل اضافه و دوباره Bake شود.

## افزودن فونت دیگر

1. TTF را در `Assets/Resources/Fonts/` بگذارید و `python3 Tools/unity_meta.py --apply --fix-fonts` را اجرا کنید.
2. در `GameFont` نامِ resource را به‌روز کنید (یا یک assetِ دوم با همان الگو بیک کنید).
3. `BaziBaqa > Typography > Bake Persian TMP Font Asset` و بعد `Validate Typography Setup`.
4. در بازی چیزی عوض نمی‌شود: همه‌ی برچسب‌ها از `GameFont` می‌خوانند.

## عیب‌یابی

| نشانه | علت و راه‌حل |
| --- | --- |
| رابط بی‌متن/سفید | `TMP Settings` import نشده ⇒ منوی Bake را اجرا کنید (خودش Essentials را import می‌کند). |
| `Unable to load font face for [x]` | `includeFontData` خاموش ⇒ `Repair Font Import Settings` (مقدار باید `1` باشد، نه `2`). |
| □/توفو وسط کلمات | کاراکتر در مجموعه‌حروف نیست ⇒ به `PersianGlyphs.txt` بیفزایید و Bake کنید. |
| حروف جدا جدا یا وارونه | کسی `PersianText.Process` را روی متنِ TMP اعمال کرده؛ متن باید فقط از `UIText` عبور کند (دروازه این را می‌گیرد). |
| اعداد لاتین در متن فارسی | `Loc.Num`/`LocalizeDigits` دور زده شده؛ عدد باید از جدول یا `Loc.Num` بیاید. |
