# نورپردازی سینمایی (فاز ۳ — گام ۲)

هدف: هر ساعتِ روز یک «حسِ» متفاوت داشته باشد — صبحِ مه‌آلود، ظهرِ شفاف، عصرِ طلایی، غروبِ
قرمز و شبِ آبی با ستاره و چراغ — بدون آنکه یک خط به منطقِ بازی دست بخورد.

## تک‌نویسنده‌بودن

```
GameClock (ساعتِ بازی) ─┐
                        ├──▶ SkyLightingRig ──▶ RenderSettings.ambient*/fog*/sun
WeatherSystem ─────────┘                      ├─▶ MaterialLibrary.SetHeightFog / SetAtmosphere
  (فقط NotifyWeather)                          ├─▶ شیدرِ آسمان: Hidden/BaziBaqa/Sky
                                                ├─▶ بودجه‌ی چراغ‌های شبانه
                                                └─▶ QualitySettings ← RenderPipelineBridge (گام ۱)
```

پیش از این فاز، `WeatherSystem` همزمان `RenderSettings.fogColor/fogDensity/ambientLight` را
می‌نوشت و نورِ روز را با `Sin()`ِ خودش تنظیم می‌کرد، و `WorldGenerator` هم موقع ساختِ جهان
همان مقادیر را دوباره می‌نوشت ⇒ نتیجه به ترتیبِ اجرا بستگی داشت و تغییرِ هوا «پرش» داشت.
حالا:

- `SkyLightingRig` (در `Assets/Scripts/Graphics`) **تنها** فایلِ `Assets/Scripts` است که
  `RenderSettings` را می‌نویسد. `Tools/project_lint.py` و تستِ
  `SkyLightingRig_IsTheOnlyRenderSettingsWriter` همین را نگه می‌دارند.
- `WeatherSystem` فقط وضعیتِ منطقی و ذراتِ باران را نگه می‌دارد و یک خبر می‌فرستد:
  `NotifyWeather(WeatherType, rainRate)`؛ شدتِ باران از `RainIntensity` خواندنی است.
- `WorldGenerator.ConfigureEnvironment` همچنان گره‌ی `Sun` (`WorldParts.SunLight`) را می‌سازد (چون با
  `World.Clear()` پاک می‌شود) ولی دیگر هیچ `RenderSettings` نمی‌نویسد؛ Rig همان نور را
  به `RenderSettings.sun` وصل می‌کند و اگر نوری نبود، خودش یکی می‌سازد.

## چرخه‌ی شبانه‌روزی

هشت کلید در `SkyLightingRig.Keys` (زمان نرمال‌شده‌ی `GameClock.NormalizedTime`) و
میان‌یابیِ نرم (`SmoothStep` + `blendSpeed`) ⇒ هیچ‌وقت پرشِ رنگ/مه نداریم. هر کلید این‌ها را
دارد: رنگ و شدتِ نور، ارتفاع/ازیموت خورشید، سه رنگِ نور محیطی (آسمان/افق/زمین)، رنگ و
چگالیِ مه، مقدارِ شب، ابر، و دو رنگِ آسمان (رأس و افق).

| زمان | حس |
|---|---|
| ۰٫۰۰ نیمه‌شب | آبیِ عمیق، ماه، ستاره، مه غلیظ، سایه‌ها خاموش |
| ۰٫۱۷ پیش‌سحر | سرد و مه‌آلود، نورِ کم از زیرِ افق |
| ۰٫۲۷ صبح | طلاییِ گرم، سایه‌های بلند |
| ۰٫۵۰ ظهر | خنثی و روشن، کمترین مه، خورشید بالای سر |
| ۰٫۶۸ بعدازظهر | گرم + غبارِ معلق |
| ۰٫۷۸ غروب | قرمزِ اشباع، مهِ کفِ دره، پنجره‌ها روشن می‌شوند |
| ۰٫۸۸ / ۱٫۰۰ شب | بازگشت به حالتِ اول (کلیدِ انتها = کلیدِ ابتدا؛ تستِ حلقه‌بودن دارد) |

## آسمان رویه‌ای

`Assets/Resources/Shaders/BaziBaqa-Sky.shader` با نامِ `Hidden/BaziBaqa/Sky`:
گرادیانِ رأس←افق←مه‌زمین، دیسکِ خورشید/ماه + هاله، ستاره‌های سوسوزن، دو فرودِ FBM برای ابر،
نورِ پشتِ ابر و نوارِ غبارِ افق.

- **چرا متریالِ `RenderSettings.skybox` و نه یک مشِ Dome؟** یونیتی خود هندسه‌ی آسمان را برای
  هر دو حالتِ `perspective` و `orthographic` (دوربین ایزومتریکِ بازی) می‌کِشَد و همان مسیر
  بازتاب‌های پیش‌فرض (`DefaultReflectionMode.Skybox`) را هم تغذیه می‌کند؛ یک draw call و بدون
  ریسکِ کلیپ‌شدن.
- جهتِ دید در شیدر از `unity_MatrixInvP`/`unity_MatrixInvV` ساخته می‌شود (نه از UV یا normal)،
  پس با هر aspect و هر projection درست می‌ماند.
- `ZWrite Off` + `ZTest Always` + `Fog { Mode Off }`: آسمان depth نمی‌نویسد تا مه ارتفاعی و
  SSAOِ گام یک خراب نشوند، و مه‌ی صحنه هم روی آسمان نمی‌نشیند.
- **مسیرِ پشتیبان:** اگر `MaterialLibrary.Sky()` شیدر را پیدا نکند، Rig دوربین را روی
  `SolidColor` با رنگِ هم‌خانواده‌ی افق می‌گذارد ⇒ هیچ‌وقت آسمانِ سیاه یا ارغوانی نداریم.
  به همین دلیل `MaterialLibrary.Sky()` بر خلاف `Surface()` زنجیره‌ی جایگزین ندارد.

## هوا

`NotifyWeather` فقط «سواری» می‌دهد (ابر، خیسی، مه، کم‌شدنِ نور) و Rig آن را در چند ثانیه به
مقدارِ هدف می‌کِشَد؛ یعنی باران که شروع می‌شود، آسمان تدریجی ابری و سطح‌ها براق می‌شوند
(`_BaziAtmosphere.z = wetness` در شیدرِ Surface و `SetHeightFog` برای مه ارتفاعی).

## بودجه‌ی چراغ‌ها (شب)

هر ساختمان/آتش نورِ نقطه‌ایِ خودش را دارد؛ Rig در هر `0.6s` نزدیک‌ترین `lampBudget` نور را
روشن می‌کند، بقیه خاموش، و همه با `LightShadows.None` (سایه‌ی چراغ روی اندروید میان‌رده گران
است). فهرستِ چراغ با `root.GetInstanceID()` تشخیص داده می‌شود که جهان بازسازی شده و maximum
۳۲ نور در آرایه‌ی ثابت نگه داشته می‌شود ⇒ بدون allocation.

`GraphicsProfile` (نسخه‌ی ۲) برای هر سطح:

| سطح | lampBudget | maxAdditionalLights | ambientScale | shadowStrength | سقفِ مه ارتفاعی |
|---|---:|---:|---:|---:|---:|
| low | 1 | 1 | 0.92 | 0.62 | 16 |
| medium | 3 | 3 | 1.00 | 0.80 | 24 |
| high | 4 | 4 | 1.06 | 0.88 | 30 |
| ultra | 5 | 6 | 1.12 | 0.95 | 36 |

`lampBudget` هیچ‌وقت از `maxAdditionalLights` بیشتر نمی‌شود (clamp در `Normalize()` + دروازه‌ی
lint روی فایل json)؛ نورِ اضافه در URP بیش از این تعداد، فقط هزینه دارد نه اثر.

## تست‌ها

- EditMode (`RenderingStackEditModeTests`): تک‌نویسنده‌بودن، قراردادِ شیدرِ آسمان،
  حلقه‌بودن و کنتراستِ کلیدهای شب/روز، فیلدهایِ نسخه‌ی ۲ نمایه، واگذاریِ `WeatherSystem`.
- PlayMode (`GraphicsPlayModeTests`): تفاوتِ واقعیِ ظهر/نیمه‌شب (نور، محیطی، مه، ambient
  mode)، پاسخِ هوا (کم‌شدنِ نور + غلیظ‌شدنِ مه)، رعايتِ بودجه‌ی چراغ‌ها.

## عیب‌یابی

| نشانه | علت | راه |
|---|---|---|
| آسمانِ تک‌رنگِ خاکستری | شیدرِ آسمان import نشده یا `Hidden/BaziBaqa/Sky` پیدا نشده | `Reimport` روی `Assets/Resources/Shaders`؛ در گزارشِ `GraphicsDirector` باید `sky=procedural` باشد |
| صحنه در شب خیلی تاریک | `ambientScale` سطحِ کیفیت یا هوای طوفانی | `GraphicsProfile.json` → `ambientScale`؛ گزارشِ Rig (`night=…`) را ببینید |
| چراغ‌های ساختمان خاموش | هنوز شب نشده (`night > 0.22`) یا بودجه‌ی سطحِ low | `LampsActive` در گزارش؛ ساعت را با `GameClock.Initialize(day, 0.02f)` جلو ببرید |
| سایه‌ی سیاهِ تخت در شب | انتظارِ خودِ طراحی است (نورِ ماه بدونِ سایه) | اگر خواستید، `ApplyShadowSettings` را تغییر دهید — ولی هزینه‌ی آن روی موبایل زیاد است |
| مه روی آسمان نشسته | شیدرِ آسمان `Fog { Mode Off }` ندارد | فایل را با نسخه‌ی مخزن جایگزین کنید |
