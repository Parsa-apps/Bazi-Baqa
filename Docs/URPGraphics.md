# پاس گرافیکی AAA — راهنمای اعمال در Unity Editor

این سند، گام‌های دقیق ارتقای گرافیک به سطح AAA را شرح می‌دهد. بخش‌هایی که در Editor انجام می‌شوند
(URP، Post-Processing، بافت‌ها و مدل‌های نهایی) چون به Unity Editor و بسته‌های زیر نیاز دارند،
باید توسط توسعه‌دهنده در محیط Unity اعمال شود. کدها و افکت‌های رویه‌ای که بدون URP کار می‌کنند
(`WorldVFX`, `AmbientLife`, `WeatherSystem`, `SplashEffects`) همین حالا فعال‌اند.

> **وضعیت**: این مخزن Unity را در خود ندارد؛ پس از باز کردن در Unity 2022.3 LTS، گام‌ها را
> به همین ترتیب اجرا کنید. هیچ بخش سالمی حذف نمی‌شود؛ فقط بسته‌ها افزوده و مواد ذخیره‌شده پایدارند.

## مرحله ۱ — افزودن URP

1. `Window > Package Manager`.
2. `+` → `Add package by name`.
3. `com.unity.render-pipelines.universal` → نسخه‌ی `12.1.x` (با Unity 2022.3 سازگار) → `Add`.
4. پس از بارگذاری، Unity می‌پرسد که Render Pipeline را تغییر دهید؛ **فعلاً نه** (تا بعداً تنظیم کنیم).

## مرحله ۲ — ساخت Pipeline Asset

1. `Assets` راست‌کلیک → `Create > Rendering > Universal Render Pipeline > Pipeline Asset`.
   دو فایل ساخته می‌شود: `Asset` و `Renderer Data`.
2. نام بگذارید: `URP_Quality` و `URP_Quality_Renderer`.

## مرحله ۳ — اتصال Pipeline به پروژه

1. `Edit > Project Settings > Graphics`.
2. در `Scriptable Render Pipeline Settings` فایل `URP_Quality` را بکشید.
3. `Edit > Project Settings > Quality`.
4. برای هر سطح کیفیت، `Render Pipeline Asset` را روی `URP_Quality` قرار دهید (به‌ویژه «بالا»).

## مرحله ۴ — فعال‌سازی Post-Processing (Bloom / AO / Vignette)

1. `Windows > Package Manager` → `com.unity.postprocessing` → `Add`.
2. روی دوربین اصلی (`CameraController` یا همان `دوربین بازی`) یک `Post-process Layer` و یک
   `Volume` در صحنه بگذارید.
3. در `Volume Profile` این موارد را روشن و تنظیم کنید:
   - **Bloom** – `Intensity: 0.6`, `Threshold: 0.9`, `Soft Knee: 0.7` (برای نورپردازی اردوگاه).
   - **Ambient Occlusion** – `Intensity: 1.0`, `Radius: 0.4` (سایه‌ی تماس در ساختمان‌ها).
   - **Vignette** – `Intensity: 0.35` (حس عمق و تمرکز).
   - **Motion Blur** – مقدار خیلی کم (`0.15`) برای حس روانی.
4. در `Post-process Layer` گزینه‌ی `Volume Blending` را روی `Linear` و `Antialiasing` را روی
   `Subpixel Morphological` قرار دهید.

## مرحله ۵ — نورپردازی حرفه‌ای

- در `Light` اصلی: `Source` را `Color Temperature` بگذارید و در طول روز رنگ گرم و هنگام شب سرد کنید.
- `Intensity` را با چرخه‌ی روز هماهنگ کنید (`WeatherSystem.UpdateDayLight` همین کار را می‌کند).
- برای سایه‌های بهتر از `Bloom` و AO استفاده شود؛ `Shadow Distance` ~ 55 و `Shadow Resolution` بالا در کیفیت «بالا».

## مرحله ۶ — متریال و بافت با کیفیت

- برای جایگزینی، در `Assets/Materials` مواد استاندارد با `Lit (URP)` بسازید.
- در `Assets/Textures` بافت‌های PBR (`Albedo`, `Normal`, `Metallic`) در اندازه‌ی 1024/2048 قرار دهید.
- `Texture Compression` روی `ASTC` بگذارید (در Project Settings > Player > Android).

## مرحله ۷ — آب، آتش و دود واقعی

- `WorldVFX.cs` همین حالا آتش، دود و جرقّه‌ی اردوگاه را ایجاد می‌کند (رویه‌ای، بدون Asset).
- برای نسخه‌ی نهایی، در `Assets/Prefabs` یک `VFXGraph` یا `ParticleSystem` با مواد URP بسازید و
  در `WorldGenerator` جایگزین `WorldVFX` کنید.
- آب را با `Water` (URP) یا `Shader Graph` شفاف جایگزین پلین فعلی کنید.

## مرحله ۸ — محیط زنده

- درختان و پرنده‌ها با `AmbientLife.cs` فعال‌اند. در نسخه‌ی نهایی، با `Pivot` و
  `Animator` راست‌کلیک، انیمیشن باد را روی مدل‌های درخت (که در `Assets/Animations` می‌گذارید) اعمال کنید.

## مرحله ۹ — بهینه‌سازی

- در `Quality Settings`، MSAA را برای دستگاه‌های متوسط خاموش نگه دارید (پروژه همین حالا خاموش است).
- `PerformanceManager.cs` کیفیت را بر اساس FPS خودکار کم/زیاد می‌کند؛ بعد از URP هم کار می‌کند.
- Draw Call را با `Static Batching` روی مدل‌های استاتیک کاهش دهید؛ `ObjectPool` برای اجرام مکرر آماده است.

## مرحله ۱۰ — بازبینی

- همه‌ی صحنه‌ها را باز و `Play` کنید؛ هیچ خطای Missing Reference نباید باشد.
- روی دستگاه متوسط، FPS را در `Profiler` چک کنید (هدف: بالای ۳۰).
- پس از رضایت، از منوی `BaziBaqa > Build` خروجی APK/AAB بگیرید.
