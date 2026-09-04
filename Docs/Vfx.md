# آب، آتش و انفجار (فاز ۳ — گام ۴)

ذره‌ها همه **Shuriken** هستند (نه VFX Graph): روی Android/GLES3 نیازِ Compute ندارد، در بیلد
استریپ نمی‌شود و متریال‌هایش شیدرهای خودِ پروژه‌اند.

## شیدرها

| فایل | نام | نقش |
|---|---|---|
| `BaziBaqa-Water.shader` | `BaziBaqa/Water` | موجِ دو-سینوسی با شیبِ تحلیلی، fresnel، بازتابِ آسمان، کفِ قله‌ها، ریزموجِ باران، مه ارتفاعی + `MixFog` |
| `BaziBaqa-Fire.shader` | `Hidden/BaziBaqa/Fire` | سه مد با یک شیدر: ۰ شعله، ۱ دود، ۲ جرقه؛ دو لایه‌ی FBMِ جابه‌جا + پیچشِ uv + بادِ جهانی |

- آب زیرِ Built-in **نمی‌شکند**: `Fallback "BaziBaqa/Surface"` و در `MaterialLibrary.Water`
  هم اگر `IsUniversal` نبود، متریالِ `Surface(Metal…)` برگردانده می‌شود (موج را از دست
  می‌دهیم، رنگِ ارغوانی نه).
- شیدرِ آب `GetWorldSpaceViewDir` را صدا نمی‌زند؛ امضایِ این helper بینِ نسخه‌هایِ URP عوض
  می‌شود، پس جهتِ دید با `_WorldSpaceCameraPos` دستی حساب می‌شود (تستِ EditMode این را قفل کرده).
- شیدرِ ذرات عمداً `CGPROGRAM + UnityCG.cginc` است و `CBUFFER` ندارد: یک متریالِ یکتا برای
  همه‌ی ذرات، پس SRP Batcher موضوعیت ندارد و کامپایل در هر دو خطِ رندر تضمین می‌شود.
- `_BaziAtmosphere.z` (خیسی) و `_BaziAtmosphere.x` (شب) را همان `SkyLightingRig` می‌نویسد؛
  باران ⇒ ریزموجِ بیشتر و کفِ بیشتر، شب ⇒ نورِ کم‌تر و بازتابِ کم‌تر.

## `VfxDirector`

`Assets/Scripts/Graphics/VfxDirector.cs` — API عمومی برای انفجار/برخورد/پاشش:

```csharp
VfxDirector.Instance.PlayExplosion(position, radius);   // شعله + دود + جرقه + موجِ ضربه
VfxDirector.Instance.PlayImpact(position, tint);        // جرقه و حلقه‌ی کوچک
VfxDirector.Instance.PlaySplash(position);               // پاششِ آب
VfxDirector.Instance.Refresh();                          // بعد از بازسازیِ جهان
```

- **استخر:** `ObjectPool<Burst>` با `PoolSize = 6` گروه؛ هر گروه چهار `ParticleSystem` دارد که
  در `Awake` ساخته می‌شوند. داخلِ `Play*` و `Update` هیچ `new GameObject`/`AddComponent`/
  `Instantiate` مجاز نیست (تستِ EditMode بدنه‌ی متدها را می‌خواند) ⇒ صفر GC در نبرد.
- **سقفِ هم‌زمان:** `BudgetForCurrentTier()` از `GraphicsProfile.particleBudget` می‌آید؛ اگر
  استخر پر باشد، افکتِ جدید **رد** می‌شود (صف نمی‌شود) ⇒ افتِ فریم زنجیره‌ای نمی‌سازیم.
- **انتشارِ یک‌باره:** `emission.rateOverTime = 0` و `system.Emit(count)`؛ برای انفجارِ کوتاه‌عمر
  درست‌تر از نرخِ پیوسته است و تعدادِ ذره‌ها دقیقاً کنترل‌شده می‌ماند.
- **دورِ دوربین:** `TryFarEnough` (۶۸ متر) از ریشه‌ی `EffectRoot` بیرون باشد پخش نمی‌شود.
- **آتشِ جهان:** هر ~۰٫۵ ثانیه، فرزندانی که نامشان `WorldParts.Fire/Smoke/Spark` است در
  `EffectRoot` پیدا و با `MaterialLibrary.Fire(...)` متریال‌دهی می‌شوند؛ هیچ داده‌ی بازی
  دست‌نخورده و هیچ کامپوننتی به آن‌ها اضافه نمی‌شود.
- جهتِ معماری: لایه‌ی بصری Gameplay را صدا می‌زند، نه برعکس — هیچ فایلِ بازی `VfxDirector`
  را نمی‌شناسد، پس حذفِ `Assets/Scripts/Graphics` بازی را کامل بازمی‌گرداند.

## آبِ صحنه

`WorldGenerator.ConfigureMaterials` آب را از `MaterialLibrary.Water(deep, shallow, sky, wave, smoothness)`
می‌گیرد؛ `wave = (speed, amp, steepness, foam)`. برای تنظیمِ حسِ دریاچه، همین چهار عدد کافی است:

| پارامتر | پیش‌فرض | اثر |
|---|---:|---|
| `speed` | 0.55 | سرعتِ گذرِ موج |
| `amp` | 0.09 | ارتفاعِ موج (متر) |
| `steepness` | 1.60 | تیزیِ قله‌ها و شیبِ نرمال |
| `foam` | 0.35 | شدتِ کف روی قله‌ها |

## هزینه

- آب: یک Passِ شفاف با چهار نمونه‌ی بافتِ ساده؛ هیچ RT/Probe/Blur ندارد.
- هر انفجار: ≤ ۹۶ ذره در هر امیتر، بدونِ سایه، `Billboard`/`Stretch`؛ بودجه‌ی `particleBudget`
  هرج‌ومرج را سقف می‌کند.
- متریال‌ها کش‌شده‌اند (تستِ PlayMode ادعا می‌کند `CachedMaterialCount` هنگام پخشِ افکت رشد نمی‌کند).

## عیب‌یابی

| نشانه | علت | راه |
|---|---|---|
| آبِ یکدستِ آبیِ بی‌حرکت | URP نصب نیست (مسیرِ پشتیبان) | `BaziBaqa > Rendering > Install URP Assets` |
| ذره‌ها ارغوانی‌اند | `Hidden/BaziBaqa/Fire` import نشده | `Reimport` روی `Assets/Resources/Shaders` |
| انفجار پخش نمی‌شود | استخر پر است یا فاصله بیش از ۶۸ متر | `Report()` → `active=x/y budget=z` |
| آتشِ اردوگاه بی‌دود | گره‌ها زیرِ `EffectRoot` نیستند | `VfxDirector.Refresh()` و نامِ گره (`WorldParts.Smoke`) را ببینید |
