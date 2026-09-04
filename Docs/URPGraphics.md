# خطِ رندر URP و لایه‌ی گرافیک (فاز ۳)

از این پس ارتقای بصری **کد-محور و خودکار** است، نه ۱۰ گام دستی در Editor. تنها کاری که
توسعه‌دهنده باید بکند، اجرای یک منو است؛ همه‌ی فایل‌های URP با API ساخته، اعتبارسنجی و
به سطوحِ کیفیت وصل می‌شوند. اگر آن منو اجرا نشود هم بازی اجرا می‌شود (مسیرِ پشتیبانِ
Built-in با همان شیدرهای پروژه) — هیچ نیمه‌نصبی باقی نمی‌ماند.

## ۱) معماریِ لایه‌ی گرافیک

| فایل | نقش |
|---|---|
| `Assets/Scripts/Graphics/GraphicsProfile.cs` | تنها منبعِ اعدادِ بصری (۴ سطح) + اعتبارسنجی؛ از `Resources/Graphics/GraphicsProfile.json` خوانده می‌شود |
| `Assets/Scripts/Graphics/MaterialLibrary.cs` | حل‌کننده‌ی شیدر/متریال (URP ↔ built-in ↔ Diffuse)، کشِ متریال، ثابت‌های جهانیِ باد/مه/اتمسفر |
| `Assets/Scripts/Graphics/CinematicVolumeRig.cs` | استکِ Volume زمان‌اجرا: Bloom، Tonemapping (ACES)، Color Adjustments، Vignette، DoF، Film Grain، Chromatic Aberration، Lift/Gamma/Gain + ساختار «حال‌وهوا» (صبح/ظهر/غروب/شب) |
| `Assets/Scripts/Graphics/RenderPipelineBridge.cs` | اعمالِ سطحِ کیفیت روی `QualitySettings`/دوربین؛ گزارشِ وضعیت؛ **تک‌نویسنده**‌ی تنظیماتِ بصری |
| `Assets/Scripts/Graphics/ScreenSpaceAmbientOcclusionFeature.cs` | Renderer Feature یِ SSAO (تولید → نرم‌کردن → ترکیب) با خاموش‌شدنِ خودکار اگر Depth Texture نبود |
| `Assets/Scripts/Graphics/GraphicsDirector.cs` | نصب‌کننده‌ی خودکار (`RuntimeInitializeOnLoadMethod`)؛ هیچ فایلِ Gameplay را صدا نمی‌زند |
| `Assets/Resources/Shaders/*` | `BaziBaqa-Surface` (PBR + باد + مه ارتفاعی + آسیب)، `BaziBaqa-Emissive` (چشمه‌های نور/ذرات)، `BaziBaqa-ScreenSpaceAO` |
| `Assets/Resources/Textures/Graphics/*` | بافت‌های رویه‌ایِ بی‌درز (Albedo/Normal/Mask) که `Tools/procedural_textures.py` می‌سازد |
| `Assets/Editor/RenderingPipelineSetup.cs` | ساختِ URP Asset ها + Renderer + Feature، اتصالِ هر سطحِ کیفیت، و `Validate` |

### چرا `Assets/Resources/Shaders`؟
در بیلد، `Shader.Find` فقط شیدرهایی را می‌بیند که یا در `Always Included Shaders` باشند یا
به‌صورت فایلِ به‌کاررفته در صحنه/متریال وجود داشته باشند. متریال‌های این پروژه در زمان اجرا
ساخته می‌شوند، پس شیدرها در `Resources` قرار می‌گیرند (همیشه در بیلد می‌مانند) و
`MaterialLibrary` علاوه بر `Shader.Find`، `Resources.Load<Shader>("Shaders/<file>")` را هم
امتحان می‌کند. به همین دلیل **هیچ** متریالِ `.mat` یا صحنه‌ی از پیش ساخته‌شده‌ای لازم نیست.

### قانونِ تک‌نویسنده
`RenderSettings.fog*/ambient*/sun/skybox` فقط در `Assets/Scripts/Graphics/SkyLightingRig.cs`
نوشته می‌شوند (`Docs/Lighting.md`)؛ `WeatherSystem` از گام ۲ دیگر چیزی در `RenderSettings`
نمی‌نویسد و فقط `NotifyWeather(weather, rainRate)` را صدا می‌زند، و `GraphicsProfile`/
`MaterialLibrary` هیچ مقدارِ تازه‌ای روی این‌ها نمی‌نویسند.
دروازه‌ی `Tools/project_lint.py` (مجموعه‌ی `FOG_WRITERS`) این را در کل `Assets/Scripts` بررسی
می‌کند؛ اگر سیستمِ تازه‌ای واقعاً باید مه را عوض کند، باید به `FOG_WRITERS` اضافه شود و
دلیلش در سند نوشته شود.

## ۲) نصبِ URP Asset (یک منو)

```
Unity > BaziBaqa > Rendering > Install URP Assets
```
چه کاری انجام می‌شود:
1. برای هر سطحِ `GraphicsProfile.json` یک جفتِ فایل ساخته می‌شود:
   `Assets/Settings/URP/BaziBaqa_<tier>.asset` و `Assets/Settings/URP/BaziBaqa_Renderer_<tier>.asset`.
2. `ScreenSpaceAmbientOcclusionFeature` به آرایه‌ی `m_RendererFeatures` رندرر اضافه می‌شود
   و پارامترهایش از همان نمایه پر می‌شود.
3. `PostProcessData` از پکیج URP پیدا و متصل می‌شود (جست‌وجو با `t:PostProcessData`؛ نه مسیرِ ثابت).
4. فیلدهای سریال‌شده با نام‌هایِ جایگزین نوشته می‌شوند (مثلاً `m_SupportsCameraDepthTexture`
   یا `m_RequireDepthTexture`)؛ نامی که در نسخه‌ی شما نباشد با لاگ رد می‌شود، نه با استثنا.
5. هر سطحِ کیفیت (`پایین/متوسط/بالا`) به URP Asset متناظرش وصل می‌شود و
   `GraphicsSettings.defaultRenderPipeline` روی سطحِ برتر ست می‌شود.

```
BaziBaqa > Rendering > Validate Rendering Setup        # گزارش + دیالوگ
BaziBaqa > Rendering > Unassign Render Pipeline (Safe Revert)
```
خط فرمان:
```bash
Unity -batchmode -quit -projectPath . \
  -executeMethod BaziBaqa.EditorTools.RenderingPipelineSetup.InstallBatch
Unity -batchmode -quit -projectPath . \
  -executeMethod BaziBaqa.EditorTools.RenderingPipelineSetup.ValidateBatch
```
خروجی در `Logs/RenderingSetupReport.txt` نوشته می‌شود. **قاعده‌ی ایمنی:** اگر چیزی قابل‌تأیید
نباشد، خطِ رندر وصل نمی‌شود و پروژه در همان حالتِ قبلی (Built-in) اجرا می‌ماند.

## ۳) سطوحِ کیفیت

| سطح | کیفیتِ یونیتی | renderScale | HDR | MSAA | سایه | AO | DoF | ذرات |
|---|---|---|---|---|---|---|---|---|
| `low` | پایین | 0.72 | نه | 0 | 512 | خاموش | خاموش | 260 |
| `medium` | متوسط | 0.90 | نه | 0 | 1024 | روشن | خاموش | 600 |
| `high` | بالا | 1.00 | بله | 4× | 2048 (۲ کاسکید) | روشن | روشن | 1100 |
| `ultra` | بالا | 1.00 | بله | 4× | 4096 (۴ کاسکید) | روشن | روشن | 1500 |

اعداد در `Assets/Resources/Graphics/GraphicsProfile.json` هستند و برای تغییرشان لازم نیست کد
بازسازی شود؛ `GraphicsProfile.Validate` شدتِ AO صفر با حالتِ روشن، renderScale خارج از
۰٫۴–۱٫۵، رزولوشن سایه‌ی غیرتوان‌دو و سطحِ کیفیتیِ ناموجود را خطا می‌گیرد.
`targetFrameRate` از این فایل خوانده می‌شود ولی **نوشته نمی‌شود** — صاحبِ آن
`PerformanceManager` است (قاعده‌ی تک‌نویسنده).

## ۴) تنظیماتِ ایمپورتِ بافت‌ها

`Tools/unity_meta.py --apply --fix-textures` متاهای PNG را این‌طور می‌نویسد:
`enableMipMap: 1`، `filterMode: 2` (Trilinear)، `aniso: 4`، `wrap*: 0` (Repeat)،
`maxTextureSize: 512` و برای نقشه‌های `*Normal*`/`*Mask*`/`*Noise*` → `sRGBTexture: 0`.
`MaterialLibrary` هم در زمان اجرا `wrapMode/filterMode/anisoLevel` را دوباره ست می‌کند تا
اگر فایل جابه‌جا شد، تصویر خراب نشود. دروازه‌ی ایستا این موارد را روی فایل‌ها چک می‌کند.

## ۵) بازتولیدِ بافت‌ها

```bash
python3 Tools/procedural_textures.py --apply     # ساخت PNG ها
python3 Tools/unity_meta.py --apply              # GUID و تنظیماتِ ایمپورت
python3 Tools/procedural_textures.py --check     # فقط بررسیِ وجود/حجم
```
تولیدکننده نویزِ مقدارِ سازگار (periodic value-noise) و Worleyِ بی‌درز دارد و PNG را با
فیلتر Paeth و `zlib` می‌نویسد (بدون نیاز به Pillow/numpy). معیارِ بی‌درزی: اختلافِ ستونِ
اول و آخر باید هم‌اندازه‌ی اختلافِ دو ستونِ مجاور باشد؛ این عدد در توسعه اندازه‌گیری شد و
الگوریتم‌ها بر همین اساس اصلاح شدند (نویزِ جهت‌دارِ پوست درخت و مرکزِ سلول‌های Worley).

## ۶) عیب‌یابی

| نشانه | علت | راه‌حل |
|---|---|---|
| صحنه ارغوانی | شیدر به‌درستی ایمپورت نشده یا `Shader.Find` مستقیم در کد | `BaziBaqa/Rendering/Validate Rendering Setup`؛ از `MaterialLibrary` استفاده کنید (دروازه این را چک می‌کند) |
| سایه روی زمین نیست | `ShadowCaster` در شیدر نیست یا `shadowDistance` خیلی کم | `Validate` presence را چک می‌کند؛ `m_ShadowDistance` در نمایه |
| AO سیاهی‌های پلّه‌پلّه دارد | `resolutionScale` کم یا بدون blur | `options.blur`/`resolutionScale` در Feature؛ سطحِ `high` مقدار ۱ می‌گذارد |
| UI روی موبایل کدر | `renderScale < 1` کل صفحه (از جمله Canvas) را کوچک می‌کند | Canvas در `Screen Space Overlay` است و تحت‌التأثیر نیست؛ اگر شد، `QualitySettings` را بالا ببرید |
| پس‌پرداز کار نمی‌کند | `QualitySettings.renderPipeline` برای سطحِ فعلی خالی است | منوی Install را اجرا کنید؛ `Validate` هر سطح را گزارش می‌دهد |
| «Post Process Data not set» | نسخه‌ی URP با انتظاراتِ ابزار فرق دارد | از `Create > Rendering > URP Asset with Universal Renderer` استفاده کنید؛ ابزار همان فایل‌ها را به‌روز می‌کند |

## ۷) آنچه فاز ۳ در گام‌های بعد اضافه می‌کند
نورِ سینمایی (`SkyLightingRig` با حال‌وهوای صبح/ظهر/غروب/شب، آسمانِ رویه‌ای، چراغِ ساختمان‌ها)،
محیطِ زنده (بادِ GPU روی همه‌ی سطوح زنده، مه ارتفاعی، گردوغبار، خیسی، گیاهانِ Instanced)،
آب و آتش و انفجار (Shuriken + متریالِ خودِ پروژه)، انیمیشنِ رویه‌ایِ شخصیت/ساختمان،
UI شیشه‌ایِ موبایل‌پسند + اینتروِ «Parsa Apps»، و مدیرِ کیفیتِ بصری (`QualityDirector`).
