# محیط زنده (فاز ۳ — گام ۳)

هدف این گام: زمین، سنگ، تنه و برگ «ماده» به نظر برسند و هوا در آن‌ها جریان داشته باشد —
بدونِ مدلِ خارجی، بدونِ Asset و بدونِ تغییرِ یک سطر از منطقِ بازی.

## چه چیزی اضافه شد

| فایل | نقش |
|---|---|
| `Assets/Scripts/Graphics/WindField.cs` | تنها نویسنده‌ی `_BaziWindState`؛ شدت/بسامد/فاز/توانِ ارتفاع با بَرگشتِ Perlin |
| `Assets/Scripts/Graphics/FoliageScatter.cs` | بوته‌های چمن: همه در **یک mesh** و **یک draw call**، با رنگِ رأس و بادِ شیدر |
| `Assets/Resources/Shaders/BaziBaqa-Sky.shader` (گام ۲) | نوارِ غبارِ افق که با `_BaziAtmosphere.w` هماهنگ است |
| `WorldGenerator.ConfigureMaterials` | زمین/تنه/برگ/سنگ از `MaterialLibrary.Surface(...)` با بافتِ Albedo+Normal+Mask |
| `WorldGenerator.CreateTerrain` | رنگِ زیست‌بوم روی رأس‌هایِ مشِ زمین (Perlin، بدون RNG) |

همه‌ی این‌ها را `GraphicsDirector` نصب می‌کند؛ هیچ سیستمِ Gameplay او را صدا نمی‌زند و حذفِ
`Assets/Scripts/Graphics` بازی را بی‌نقص برمی‌گرداند.

## باد

`WindField.Update()` یک بردار می‌نویسد، صدها transform تکان نمی‌دهد:

- `x = speed`، `y = strength`، `z = phase`، `w = heightPower` (توانِ ارتفاع — نوکِ درخت أكثر از تنه).
- هدفِ شدت از هوایِ فعلی می‌آید (خواندنِ `WeatherSystem.Current` و `RainIntensity`؛ هیچ نوشتنی در
  آن فایل نیست): صاف `0.34`، باران `0.66`، مه `0.16`، توفان `1.35+`.
- نیمه‌شب تا ۴۰٪ آرام‌تر می‌شود (`SkyLightingRig.NightAmount`) و بَرگشت‌ها از
  `Mathf.PerlinNoise` می‌آیند، پس هرگز پرشِ تصادفی نداریم.
- سطحِ کیفیت فقط بسامدِ بَرگشت را کم می‌کند (`ApplyTier`)؛ هزینه‌ی اصلی در شیدر است نه CPU.

## چمنِ پراکنده

`FoliageScatter` یک مشِ ترکیبی می‌سازد (هر بوته = دو تیغه‌ی متقاطع = ۸ رأس و ۱۲ مثلث):

- بودجه از `GraphicsProfile` می‌آید: low 220، medium 700، high 1250، ultra 1700 (سقفِ سخت ۴۰۹۶).
- بذر: `System.Random` با seedِ ثابتِ خودِ کلاس. **هیچ‌وقت `UnityEngine.Random` مصرف نمی‌شود**؛
  همان منبعی که `WorldGenerator` برای چیدمانِ منابع/دشمن‌ها استفاده می‌کند و یک تماسِ اضافه،
  جای همه‌چیز را عوض می‌کرد (تستِ EditMode این را قفل می‌کند).
- محلِ کاشت: Raycast عمودی فقط رویِ مشِ `WorldParts.Ground`؛ ارتفاع بین `0.35` تا `2.4` متر
  (پس کنارِ آب و قله‌هایِ سنگی بوته ندارد) و `8.5` متر اطرافِ مرکزِ اردوگاه تمیز می‌ماند.
- رندرر: `shadowCastingMode = Off`، `receiveShadows = false`، `lightProbeUsage = Off`،
  `UploadMeshData(true)` (بافرِ CPU آزاد می‌شود) و گره زیرِ `TerrainRoot` تا با `World.Clear()`
  خودش پاک شود — بدونِ نشتیِ Object.
- رنگِ رأس‌ها: ته بوته تیره، سر بوته روشن + تنوعِ تصادفیِ ~۴۲٪ ⇒ همان `AO`ِ ارزانِ بازی.

## رنگِ زیست‌بومِ زمین

در حلقه‌ی ساختِ زمین، به‌جز ارتفاع، یک `Color` هم نوشته می‌شود (`mesh.colors`):
مستقل از `edge` (فاصله تا مرگِ جزیره) و Perlinِ بافت‌دار، بینِ «گل‌ولایِ ساحلی» و «چمنِ مرتعی»
با لکه‌هایِ خشک. شیدرِ `BaziBaqa/Surface` با کلیدِ `_BAZI_VERTEX_COLOR_ON` آن را
`albedo *= saturate(vertexColor * 1.4)` اعمال می‌کند؛ `MaterialLibrary.Surface` این کلید را
برای Ground/Rock/Bark/Foliage روشن می‌کند و برای سبک‌هایِ دیگر خاموش است.
برای مش‌هایی که رنگِ رأس ندارند، سفیدِ پیش‌فرض یعنی بی‌اثر ⇒ برای هر مشِ دیگری امن است.

## تنظیماتِ تازه‌ی نمایه (نسخه‌ی ۳)

| سطح | foliageCount | windScale | lampBudget | proceduralSky |
|---|---:|---:|---:|---|
| low | 220 | 0.85 | 1 | ✅ |
| medium | 700 | 1.00 | 3 | ✅ |
| high | 1250 | 1.06 | 4 | ✅ |
| ultra | 1700 | 1.12 | 5 | ✅ |

`Tools/project_lint.py` وجودِ این فیلدها را در فایل json برای هر چهار سطح و
`lampBudget ≤ maxAdditionalLights` را بررسی می‌کند.

## هزینه و بازگشت

- +1 draw call برای کل چمن (در low روی صحنه‌ی نمونه)، و ~۹۶۰۰ رأس در ultra — بدونِ Update در CPU.
- باد و مه ارتفاعی داخلِ همان passِ نوریِ موجود محاسبه می‌شوند؛ هیچ passِ تازه‌ای اضافه نشد.
- برگ‌ها حالا `alpha clip` دارند ⇒ رویِ دستگاه‌هایِ خیلی ضعیف می‌توان `_BAZI_ALPHA_CLIP_ON` را
  از متریالِ Foliage حذف کرد (یک سطر در `MaterialLibrary.Surface`).

## عیب‌یابی

| نشانه | علت | راه |
|---|---|---|
| درخت‌ها تکان نمی‌خورند | متریالِ آن جسم `_BAZI_WIND_ON` ندارد | از `MaterialLibrary.Surface(style, …, wind: true)` بسازید؛ `_BaziWindState` را در Frame Debugger ببینید |
| چمن وسط اردوگاه روییده | `campClearance` کوچک است | در `FoliageScatter` افزایش دهید (نه در `WorldGenerator`) |
| چمن روی آب | ارتفاعِ مجاز زیاد است | `minGroundHeight` را بالا ببرید |
| بوته‌ها بعد از بازسازیِ جهان غیب شدند | `TerrainRoot` عوض شده | `FoliageScatter.Refresh()` صدا زده می‌شود؛ اگر صحنه‌ی دستی است، `GraphicsDirector.Refresh()` را بزنید |
| زمین یکدستِ سبز است | `mesh.colors` نوشته نشده یا `Reimport` لازم دارد | در PlayMode لاگِ `GraphicsDirector` را ببینید؛ `foliage tufts=…` باید عدد بدهد |
