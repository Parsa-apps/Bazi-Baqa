# سیستم بومی‌سازی «سرزمین بقا» (فاز ۲٫۵)

هدف: **هیچ متن قابل‌مشاهده‌ای در کد نباشد.** همه‌ی رشته‌هایی که بازیکن می‌بیند در یک جدول
کلیدمحور زندگی می‌کنند؛ افزودن زبان تازه یعنی «یک بلوک تازه در JSON»، نه ویرایش اسکریپت.

```
Assets/Resources/Localization/LocalizationTable.json   ← همه‌ی متن‌ها (fa + en)
Tools/localization_entries.json                        ← منبع ویرایشِ متن‌های تازه (fa/en)
Tools/localization_table.py                            ← سازنده/ممیزِ جدول (بدون Unity)
Assets/Scripts/Core/LocalizationManager.cs             ← مدیریت‌کننده‌ی مرکزی + نماینده‌ی Loc
Assets/Scripts/UI/LocalizedText.cs                     ← گره‌زدن یک متن صحنه‌ای به کلید
```

## ۱) قرارداد کلیدها

| پیشوند | کاربرد | مثال |
| --- | --- | --- |
| `game.` / `menu.` / `modal.` | عنوان‌ها و منوهای اصلی | `menu.new_game` |
| `hud.` | برچسب‌های نوار اطلاعات (قالب‌دار) | `hud.day_line` = «روز {0}  •  {1}» |
| `ui.<area>.` | متن‌های ساخته‌شده در کد (پنل‌ها/دکمه‌ها) | `ui.build.title` |
| `toast.` | اعلان‌های زودگذر | `toast.built` = «{0} ساخته شد.» |
| `resource.` `building.` `role.` `weather.` `technology.` `achievement.` `equipment.` `status.` | نامِ داده‌های enum‌محور | `building.watchtower` |
| `quest.<id>.` `story.<id>.` | متن مأموریت/داستان (کلید از شناسه ساخته می‌شود) | `quest.q_house.title` |
| `survivor.name.<i>` | استخر نام بازمانده‌ها | `survivor.name.3` |
| `format.` `label.` | قالب‌ها و برچسب‌های مشترک | `format.cost_join` |
| `log.` | پیام‌هایی که از GameLogger به بازیکن می‌رسد | `log.error_prefix` |
| `language.` | نام هر زبان با خودِ همان زبان | `language.fa` = «فارسی» |

قاعده‌ها:
- نام‌های کلید فقط حروف کوچک، رقم، `_` و جداکننده‌ی `.` (بدون فاصله، بدون حرف بزرگ).
- عدد هرگز در متن hard-code نمی‌شود؛ از `{0}` و `Loc.Num(...)` استفاده می‌کنیم تا
  در فارسی «۱۲» و در انگلیسی «12» نمایش داده شود.
- جداکننده‌ها و قالب‌های مشترک (مثل «  •  ») هم کلید دارند (`format.cost_join`) تا در هر زبان درست باشند.
- `Loc.Get` برای کلیدِ گمشده `[loc:<key>]` برمی‌گرداند **و** یک `Debug.LogError` می‌زند؛
  پس متن خالی هیچ‌وقت بی‌صدا نمی‌ماند.

## ۲) خواندن متن در کد

```csharp
// ثابت
Text title = CreateText(parent, Loc.Get("ui.build.title"), 20, Teal, TextAnchor.MiddleCenter);

// قالب‌دار
GameEvents.Notify(Loc.Get("toast.raid_won", loot));

// داده‌محور (نام enum‌ها و شناسه‌ها به کلید تبدیل می‌شود)
GameText.BuildingName(BuildingType.WatchTower);
GameText.ResourceAmount(ResourceType.Wood, 20);
```

کامپوننت‌های صحنه‌ای (که در Editor دستی چیده می‌شوند) `LocalizedText` را دارند:
`key` را پر کنید و `formatArgs` را در Inspector بگذارید؛ با تغییر زبان خودکار تازه می‌شود.
UI ساخته‌شده در کد هم با رویداد `LocalizationManager.LanguageChanged` بازسازی می‌شود
(`UIManager.OnLanguageChanged`).

## ۳) افزودن متن تازه (دو دقیقه)

1. در کد: `Loc.Get("ui.shop.title")` (کلید تازه با پیشوند درست).
2. در `Tools/localization_entries.json`:
   ```json
   "ui.shop.title": { "fa": "فروشگاه", "en": "Shop" }
   ```
3. `python3 Tools/localization_table.py` → جدول بازنویسی و مرتب می‌شود.
4. `python3 Tools/project_lint.py` → اگر کلیدی جا بیفتد یا تعداد `{0}` با آرگومان‌ها نخواند، خطا می‌گیرید.

## ۴) افزودن زبان تازه

1. در `Assets/Resources/Localization/LocalizationTable.json` یک بلوک بسازید:
   ```json
   { "language": "ar", "direction": "rtl", "entries": [] }
   ```
   (یا موقتاً `Tools/localization_entries.json` را به `{ "fa": …, "en": …, "ar": … }` گسترش دهید
   و در `localization_table.py` همان فهرست زبان‌ها را اضافه کنید.)
2. جهت متن از همان بلوک خوانده می‌شود: `LocalizationManager.IsRtl` / `.Direction`.
3. برای چپ‌به‌راست، `PersianText` شکل‌دهی و ارقام فارسی را **تحمیل نمی‌کند**
   (همه‌چیز از `LocalizationManager.IsRtl` پیروی می‌کند).

## ۵) دروازه‌های خودکار

| ابزار | چه چیزی را نگه می‌دارد |
| --- | --- |
| `python3 Tools/localization_table.py --check` | هر کلیدِ موردنیازِ کد متن کامل دارد؛ هیچ کلید یتیمی نیست؛ همه‌ی زبان‌ها هم‌تعدادند |
| `python3 Tools/project_lint.py` | هیچ رشته‌ی فارسی قابل‌مشاهده در `Assets/Scripts` نیست؛ کلیدهای خام در جدول وجود دارند؛ آرگومان‌ها با `{n}`ها می‌خوانند؛ نام فایل با کلاس MonoBehaviour یکی است؛ `.meta`ها سالم‌اند |
| `BaziBaqa > Audit > Find Hardcoded Persian Text` (Editor) | همان ممیزی از درون Unity؛ گزارش در `Logs/LocalizationAuditReport.txt` |
| `Assets/Tests/EditMode/LocalizationTests.cs` | ساختار جدول، جهت، رویداد تغییر زبان، پوشش enum‌ها، مأموریت/داستان/نام‌ها، رفتار `LocalizedText` |
| `Assets/Tests/PlayMode/RuntimeValidationPlayModeTests.cs` | تغییر زبان در زمان اجرا، بازسازی UI، نبودِ کلید خام/رقم فارسی در انگلیسی |

استثنای ثبت‌شده: `Assets/Scripts/Core/GameClock.cs` (جدول تبدیل ارقام) و
`Assets/Scripts/Utilities/PersianText.cs` (مکانیزم شکل‌دهی حروف) — این‌ها «متن» نیستند،
ماشین‌افزارِ متن‌اند و در `Tools/localization_allowlist.json` مستند شده‌اند.

## ۶) نتیجه‌ی فاز ۲٫۵

- ۴۲۱ رشته‌ی قابل‌مشاهده از ۳۲ اسکریپت از کد بیرون رفت (شامل ۱۴۶ رشته فقط در `UIManager`).
- ۲۶۴ کلید × ۲ زبان = ۵۲۸ متن در جدول؛ یکتا، مرتب، ممیزی‌شده.
- ۲۳۴ خوانش متن در کد از طریق `Loc` انجام می‌شود؛ باقی متن‌ها از `GameText`/`LocalizedText`.
- متن‌های داخلی (نام گره‌های هیرارشی، برچسب لاگ‌های توسعه‌دهنده، دلیلِ تحلیلی منابع) به ASCII
  رفتند تا با متن بازی اشتباه گرفته نشوند و `transform.Find`ها شکننده نمانند.


## پیوند با سیستم تایپوگرافی

جدولِ بومی‌سازی فقط «چه متنی» را تعیین می‌کند؛ «چگونه رندر شدنش» در `Docs/Typography.md` است:
همه‌ی برچسب‌ها از `UIText` ساخته می‌شوند و اگر assetِ فونتِ TMP بیک شده باشد با `TextMeshProUGUI`
و در غیر این صورت با `Text` قدیمی + همان فونتِ Vazirmatn رندر می‌شوند. کاراکترهایِ تازه‌ی
غیرلاتینِ جدول باید در `Assets/Resources/Fonts/PersianGlyphs.txt` هم باشند؛ دروازه‌ی
`Tools/project_lint.py` این را کنترل می‌کند.
