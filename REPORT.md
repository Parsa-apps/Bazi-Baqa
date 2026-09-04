# گزارش نهایی بازیابی فاز ۰ و فاز ۱ — Bazi Baqa

تاریخ: ۲۰۲۶-۰۹-۰۴ | شاخه: `arena/01a06d97-bazi-baqa` | HEAD: `875caf1`

## خلاصه

هفت گام بازیابی پایداری طبق `promptBazi.txt` اجرا شد. هر گام در یک کامیت جداگانه ثبت شد و
همه‌ی کارها به‌صورت محافظت‌شده روی همان شاخه به گیت‌هاب ارسال شد. تمام تغییرات طبق قاعده‌ی
«بررسی کامل پروژه → کامیت محافظتی → تغییرات مرحله‌ای» انجام شد و هیچ بخش سالمی حذف نشد.

## زنجیره‌ی کامیت‌ها

| گام | کامیت | شرح |
|-----|-------|-----|
| فاز ۰ (محافظتی) | `983578c` | عکس‌برداری محافظتی از کل کار AAA در جریان |
| ۱ | `d1dfb8b` | تعریف ساختارهای ذخیره‌ی مفقود (`EquipmentSaveState`، `StorySaveState`، `RaidSaveState`) |
| ۲ | `7913f7f` | بهبود سیستم مهاجرت ذخیره (null guard + `Migrate`، `CurrentSaveVersion=3`) |
| ۳ | `ebd84e2` | رفع نشت حافظه‌ی `WorldVFX` (پاک‌سازی emitterها در تولّد/نابودی) |
| ۴ | `19f3794` | رفع تکرار پاداش مأموریت در `ClaimAll` (شمارنده‌ی `_issued` + `PositiveMod`) |
| ۵ | `d20412e` | جداسازی خروجی APK و AAB در بیلد اندروید |
| ۶ | `c77d758` | حرفه‌ای‌کردن `GameLogger` + `RuntimeLogger` (کرش/اجرا/ذخیره/سیستم) |
| — | `875caf1` | ادغام تاریخچه‌ی موجود شاخه با کار بازیابی (بدون از دست رفتن هیچ محتوایی) |

## جزئیات هر گام

### گام ۱ — ساختارهای ذخیره‌ی مفقود
- کلاس‌های `EquipmentSaveState`، `StorySaveState` و `RaidSaveState` در `GameData.cs` تعریف شدند.
- `GameSaveData` برابر با `saveVersion=3` و خرابه این ساختارها است.
- `CreateNew` مقدار اولیه‌ی `questIndex=3` را می‌گذارد (سازگار با ۳ مأموریت فعال جدید).

### گام ۲ — مهاجرت ذخیره
- `SaveSystem.CurrentSaveVersion=3`. متد `Migrate(GameSaveData)` نقاط خالی
  (`settings/survivors/buildings/achievements/dailyReward/equipment/story/raid`) را با
  مقادیر پیش‌فرض پر می‌کند و `questIndex` را محدود (clamp) می‌کند، تا ذخیره‌های قدیمی داده از دست ندهند.
- `StoryDirector` و `CopyTo` سیستم‌های `EquipmentSystem/RaidSystem` ضد-null و بدون اشتراک مرجع (de-alias) شدند.
- `GameManager` از `SaveSystem.CurrentSaveVersion` استفاده می‌کند.

### گام ۳ — نشت حافظه‌ی WorldVFX
- `WorldVFX` اکنون فهرست emitterها را نگه می‌دارد، متد `Clear()` دارد و آن را در
  `Initialize` و `OnDestroy` صدا می‌زند تا اعضای `ParticleSystem` روی تولید مجدد دنیا انباشته نشوند.
- `WorldGenerator.ClearGeneratedWorld` نیز `_worldVfx.Clear()` را فراخوانی می‌کند.

### گام ۴ — رفع تکرار پاداش مأموریت
- `QuestSystem` به‌جای اندیس چرخشی تکراری، از شمارنده‌ی `_issued` استفاده می‌کند؛
  `ClaimAll` مأموریت‌های تازه و متمایز صادر می‌کند و دیگر همان تعریف بارها پاداش نمی‌دهد.
- `PositiveMod` اضافه شد تا اندیس منفی در بازه‌ی امن بماند.
- تست شبیه‌سازی چرخش مأموریت (بدون تکرار تعریف) و تست `questIndex` نهایی بهروزرسانی شدند.

### گام ۵ — بیلد APK/AAB
- `AndroidBuild.Build(..., bool BuildAppBundle)` و `ConfigureAndroidPlayer(bool)` اضافه شد.
- منوهای APK `buildAppBundle=false` و منوی AAB (گوگل‌پلی) `buildAppBundle=true` می‌گذارند؛
  خروجی و پیام‌های لاگ نیز بین APK و AAB تفکیک شدند.

### گام ۶ — حرفه‌ای‌کردن لاگر
- `RuntimeLogger` (MonoBehaviour) با `Application.logMessageReceived` همه‌ی خطاها،
  هشدارها و فراخوان‌های کرش را ثبت و در فایل `bazi_baqa_log.txt` روی حافظه‌ی دستگاه ذخیره می‌کند.
- `GameLogger` دسته‌های `Save()` و `System()` و یک overload خالی `Try(Action)` گرفت.
- `GameBootstrap` لاگر را در بدو ورود راه‌اندازی و در نابودی flush می‌کند؛ بدون اینکه کل
  «GameManager/UIManager» بین صحنه‌ها ماندگار شود (جلوگیری از مدیریّت تکراری).

### گام ۷ — آزمون نهایی
- یونیتی در این محیط نصب نیست، بنابراین آزمون واقعی `Compile/PlayMode` ممکن نبود؛
  اعتبارسنجی به‌صورت ایستا انجام شد:
  - تعادل آکلاد/پرانتز/براکت در همه‌ی ۴۵ فایل `.cs`: ۰ ناهماهنگی.
  - اجرای `Tools/validate_project.py`: خروجی ۰ با «اعتبارسنج موفق: ۴۳ اسکریپت…».
  - «بازبینی ارجاع‌ها»: `GameEvents.Notify`, `Application.logMessageReceived`,
    `EditorUserBuildSettings.buildAppBundle`, `GameSaveData.questIndex` همگی معتبرند.
  - هشدار قبلی در مورد `UIManager.ActiveQuestText()` یک هشدار نادرست بود؛ این متد
    در `UIManager` (خط ۷۵۱) تعریف و در خط ۲۵۴ استفاده شده و انواع مأموریت (`QuestRuntime`,
    `QuestStatus`, `QuestDefinition.title`) همگی هم‌راستا هستند.

## نحوه‌ی حل اختلاف شاخه
شاخه‌ی دور `arena/01a06d97-bazi-baqa` دارای تاریخچه‌ی توسعه‌ی AAA ازگلی (بدون رفعِ باگ‌ها)
بود که با کار این جلسه واگرا می‌شد. برای آنکه هیچ محتوایی از دور از دست نرود، تاریخچه‌ی شاخه با
`git merge` ادغام شد و درگیری‌ها به سمت پیاده‌سازیِ اصلاح‌شده حل شد. نتیجه یک حالت نهایی است
که هم `Docs/URPGraphics.md` و سیستم‌های نقشه/تجهیزات/داستان/حمله را نگه می‌دارد و هم همه‌ی
رفع‌های پایداری فاز ۰/۱ را دارد. هیچ فایلی از شاخه‌ی دور حذف نشد (مجموعه‌ی فایل‌های HEAD
زیرمجموعه‌ی کامل مجموعه‌ی شاخه‌ی دور است).

## محدودیت‌ها
- یونیتی نصب نیست؛ لذا کامپایل واقعی C# و تست `PlayMode`/بیلد اندروید اجرا نشد و فقط
  اعتبارسنجی ایستا امکان‌پذیر بود. بازبینی دستی در یونیتی توصیه می‌شود.
