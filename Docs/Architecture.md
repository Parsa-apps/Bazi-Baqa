# معماری فنی بازی «بازی بقا»

## چشم‌انداز

این مخزن یک پروژه‌ی Unity برای Android است. بازی یک راهبرد بقا با دوربین ایزومتریک سبک است؛ بازیکن یک گروه شش‌نفره را در جزیره‌ای ناشناخته هدایت می‌کند. هدف نمونه‌ی قابل‌بازی، زنده ماندن تا روز هفتم، ساختن پناهگاه و باز کردن فناوری «همکاری» است.

## لایه‌ها

```text
GameBootstrap
└── GameManager (چرخه‌ی بازی و رویدادها)
    ├── GameClock          زمان، روز و شب
    ├── ResourceSystem     انبار اتمیک و هزینه‌ها
    ├── WorldGenerator     زمین، منابع طبیعی و بازیابی جهان
    ├── ConstructionSystem ساخت، ارتقا و جانمایی لمسی
    ├── WeatherSystem      باران، مه و نور
    ├── EnemyDirector      حمله‌های شبانه (موج‌های هماهنگ)
    ├── ProgressionSystem  تجربه و «مرحله‌ی گروه»
    ├── QuestSystem        مأموریت‌ها و مراحل داستانی
    ├── AchievementSystem  دستاوردها و پاداش‌ها
    ├── DailyRewardSystem  پاداش روزانه و ردیف روزها
    ├── EquipmentSystem    تجهیزات و ارتقای ابزار/سلاح/زره
    ├── StoryDirector      تصمیم‌های داستانی در روزهای کلیدی
    ├── RaidSystem         حمله‌ی روزانه و غنیمت
    ├── PerformanceManager تنظیم خودکار کیفیت بر اساس نرخ فریم
    └── GraphicsDirector  (لایه‌ی بصری؛ خودش را نصب می‌کند، GameManager آن را صدا نمی‌زند)
        ├── CinematicVolumeRig          استک Volume: Bloom/Tonemap/Color/Vignette/DoF/Grain
        ├── SkyLightingRig              چرخه‌ی شب/روز، آسمان رویه‌ای، مه، بودجه‌ی چراغ‌ها
        ├── WindField                   تک‌نویسنده‌ی _BaziWindState (بادِ بَرگشت‌دار، هوامحور)
        ├── FoliageScatter              بوته‌هایِ رویه‌ای در یک mesh (بذرِ جدا از RNGِ بازی)
        └── VfxDirector                 آب/آتش/دود/جرقه/موجِ ضربه با ObjectPool (Shuriken، بدونِ Instantiate)
        ├── RenderPipelineBridge        اعمالِ سطحِ کیفیت روی موتور (تک‌نویسنده)
        ├── MaterialLibrary             حلِ شیدر URP/built-in + کشِ متریال + ثابت‌های جهانی
        └── ScreenSpaceAmbientOcclusionFeature  (داخل Renderer Data یِ URP)
    ├── GameLogger         خطایابی و پایداری
    ├── SaveSystem         ذخیره‌ی اتمیک و نسخه‌ی پشتیبان
    └── UIManager          رابط فارسی و راست‌چین + نقشه‌ی جزیره

SurvivorAgent ── SurvivorBrain ── ResourceNode / BuildingController
SurvivorAgent ── Fleeing (واکنش به خطر شبانه) ── EnemyAgent
EnemyAgent      ── Retire/Retreat (تشخیص خطر) ── WeatherSpeed (واکنش محیط)
TrainingSystem ── Workshop ── GameManager.RecruitSurvivor
ProgressionSystem ── GameEvents / SurvivingActions (جمع‌آوری، ساخت، ارتقا، تربیت، فناوری، دفاع)
QuestSystem    ── Construction / Resources / Survivors / Clock
AchievementSystem ── Build / Defeat / Day
AudioManager   ── UIManager / GameManager / Clock.NightChanged (موسیقی خطر)
AmbientLife    ── WorldGenerator (تکان درختان و پرندگان)
WorldVFX       ── WorldGenerator (آتش، دود، جرقّه‌ی اردوگاه)
EquipmentSystem ── SurvivorAgent / RaidSystem (جمع‌آوری، حمله، کاهش آسیب)
StoryDirector  ── GameManager.Clock (تصمیم‌های روزانه)
RaidSystem     ── Guards / Resources / Equipment (یورش روزانه)
UIManager      ── MiniMap / Equipment / Raid / Story panels
AndroidBuild (Assets/Editor) ── BuildPipeline ── APK / AAB
```

## جریان شروع

1. `Main.unity` فقط یک `GameBootstrap` دارد؛ این کار صحنه را سبک و قابل نگهداری نگه می‌دارد.
2. Bootstrap تنظیمات Android، نرخ فریم و اجزای سرویس را می‌سازد.
3. Splash با هویت **Parsa Apps** نمایش داده می‌شود و سپس منوی فارسی باز می‌شود.
4. شروع بازی یک seed پایدار می‌سازد؛ ادامه‌ی بازی فایل اصلی یا نسخه‌ی پشتیبان را می‌خواند.
5. `WorldGenerator` زمین و منابع را با Primitiveهای کم‌هزینه ایجاد می‌کند؛ این پروتوتایپ برای جایگزینی مدل‌های هنری آماده است.

## قراردادهای مهم

- همه‌ی تغییرات انبار فقط از `ResourceSystem.TrySpend` و `ResourceSystem.Add` انجام می‌شوند.
- هر عامل ابتدا نیازهای بقا را بررسی می‌کند، سپس کار گروهی خود را انتخاب می‌کند.
- تمام متن قابل مشاهده‌ی بازی از `PersianText` عبور می‌کند و راست‌چین می‌شود.
- ذخیره ابتدا در فایل موقت نوشته و سپس با نسخه‌ی پشتیبان جابه‌جا می‌شود تا قطع برق فایل اصلی را خراب نکند.
- سیستم‌ها به جای `FindObjectOfType`، از مرجع `GameManager.Instance` استفاده می‌کنند.
- ساخت‌وساز از طریق `ConstructionSystem` انجام می‌شود؛ UI فقط فرمان انتخاب ساختمان را صادر می‌کند.
- پیشرفت گروه از `ProgressionSystem` انجام می‌شود؛ منطق ریاضی آن در `ProgressionMath` مستقل و قابل تست است و
  فقط در بازیکردن، سطح بالا می‌رود و پاداش می‌دهد.
- مأموریت‌ها از `QuestSystem`، دستاوردها از `AchievementSystem` و پاداش روزانه از `DailyRewardSystem` بررسی می‌شوند.
  همه در فایل ذخیره (نسخه‌ی ۲) ثبت می‌شوند و هنگام بارگذاری با مقادیر پیش‌فرض ایمن هستند.
- حیات محیط توسط `AmbientLife` (تکان درختان و پرندگان) بدون Asset خارجی تأمین می‌شود.
- لایه‌ی بصری از Gameplay جداست: `GraphicsDirector` با `RuntimeInitializeOnLoadMethod` نصب می‌شود،
  هیچ فایلِ Gameplay او را صدا نمی‌زند و حذفِ `Assets/Scripts/Graphics` بازی را بی‌نقص برمی‌گرداند
  (تستِ `GraphicsDirector_InstallsWithoutTouchingGameplayFiles` همین را نگه می‌دارد).
- `WeatherSystem` دیگر `RenderSettings` نمی‌نویسد؛ فقط `NotifyWeather` را صدا می‌زند و
  `SkyLightingRig` تنها نویسنده‌ی نور/مه/آسمان است (`Docs/Lighting.md`).
- همه‌ی متریال‌ها از `MaterialLibrary` می‌آیند؛ `Shader.Find` مستقیم در کدِ بازی ممنوع است
  (دروازه‌ی `Tools/project_lint.py` و تستِ EditMode آن را بررسی می‌کنند).
- خروجی اندروید از اسکریپت `Assets/Editor/AndroidBuild` (منوی `BaziBaqa > Build`) ساخته می‌شود و پس از پیکربندی
  خودکار IL2CPP/ARM64، APK یا AAB تولید می‌کند. `PerformanceManager` کیفیت را بر اساس نرخ فریم دستگاه تنظیم می‌کند.

## مسیرهای توسعه‌ی بعدی

- جایگزینی Primitiveها با مدل‌های کم‌حجم و LOD در `Prefabs/`.
- انتقال UI به TextMeshPro با فونت فارسی دارای مجوز و atlas پویا.
- افزودن Addressables برای بسته‌های محتوایی و تنظیمات سرور.
- افزودن تست‌های PlayMode برای سناریوهای نجات، حمله و بازیابی ذخیره.

## بودجه‌ی هدف Android

| شاخص | هدف |
|---|---:|
| نرخ فریم دستگاه متوسط | 60، حداقل پایدار 30 |
| تعداد فعال اولیه | 6 بازمانده، حداکثر 24 دشمن |
| اندازه‌ی صحنه‌ی نمونه | 64 × 44 متر |
| دفعات ذخیره | پایان روز، هر 30 ثانیه، خروج |
| کیفیت پیش‌فرض | متوسط: renderScale 0.90، سایه 1024، AO روشن، MSAA خاموش |
| بافت‌های گرافیکی | ۱۵ نقشه‌ی ۱۲–۲۵۶ پیکسلیِ رویه‌ای (mipmap + Repeat + ASTC در بیلد) |
| شیدرها | ۳ شیدرِ پروژه در `Resources/Shaders` (URP + built-in)، SRP Batcher سازگار |
| Draw Call | متریال‌ها کش سراسری دارند؛ بازتولیدِ جهان متریالِ تازه نمی‌سازد (تست PlayMode) |
