# انیمیشنِ حرفه‌ای (لایه‌ی رویه‌ای)

این لایه به شخصیت‌ها و ساختمان‌ها «زندگی» می‌دهد: گام، دویدن، کارکردن، ضربه‌خوردن،
افتادنِ نرم، رشدِ ساختمان هنگام ساخت، پالسِ ارتقا و دوده/آسیبِ دیدنی. هیچ فایلِ
Gameplay ای برای این کار تغییر نکرده و نباید تغییر کند.

## چرا «روی‌نویسی» نه «AnimationClip»؟

1. پروژه باید بدونِ هیچ Asset خارجی باز شود؛ clip/Animator Controller یعنی فونت‌ها و
   مدل‌هایِ آماده که در این فاز در دسترس نیستند.
2. منطقِ بازی (جهت‌گیری، افتادن، مقیاسِ ارتقا) از قبل روی `transform` می‌نویسد. اگر
   انیماتور هم همان‌جا بنویسد، هر فریم یک نفر برنده می‌شود و نتیجه لرزش است.

بنابراین قراردادِ لایه‌ی انیمیشن این است: **هر گرهِ مالکیتِ خودش را دارد.**

| گره | مالک | نوشته‌هایِ لایه‌ی انیمیشن |
|---|---|---|
| ریشهٔ بازمانده/دشمن | `SurvivorAgent` / `EnemyAgent` (موقعیت، چرخش، `Euler(90)` هنگام مرگ) | هیچ |
| `Torso` (فرزندِ ریشه) | موقعیتِ محلی با `SurvivorAgent.AnimateBody` (گامِ ساده) | `localScale` (له‌شدن/بازشدن) + `localRotation` (خمِ دویدن، تکانهٔ ضربه) |
| `Backpack` | لایهٔ انیمیشن | تکانهٔ کوچک با گام |
| `Limb_*` (چهار اندامِ تازه) | لایهٔ انیمیشن | چرخشِ دست/پا، شل‌شدن هنگام افتادن |
| ریشهٔ ساختمان | `WorldGenerator` + `BuildingController.UpdateVisuals` (مقیاسِ ارتقا) | هیچ |
| فرزندانِ ساختمان (`Body`, `Roof`, …) | لایهٔ انیمیشن | `localScale`/`localPosition` برای رشد، لرزش، فروپاشی |

اندام‌ها از همان مشِ `Torso` (کپسول) ساخته می‌شوند و **Collider ندارند**؛ برای همین
`WorldSelectionSystem` و ریکست‌هایِ لمسی دقیقاً مثلِ قبل کار می‌کنند.

## مؤلفه‌ها

### `ActorMotion`
- سرعت را از اختلافِ موقعیتِ ریشه در هر فریم می‌گیرد (`dt` نرم‌شده)؛ هیچ API حرکتی روی
  Agentها وجود ندارد و ما هم نمی‌سازیم.
- `SurvivorAgent.State` تعیین‌کنندهٔ «کارکردن» است: `Gathering/Building/Returning` دست‌ها
  را بالا می‌آورد، `Fleeing` حالتِ دویدن، `Injured` خمیدگی.
- افتِ `Health` (هر منبعی: دشمن، گرسنگی، …) یک `_flinch` می‌سازد که با `MoveTowards`
  خاموش می‌شود ⇒ انیمیشنِ ضربه از خودِ رویدادها می‌آید، نه از رویدادِ جداگانه.
- `!IsAlive` ⇒ شل‌شدنِ اندام‌ها و له‌شدنِ بدن؛ خوابیدنِ ریشه کارِ `SurvivorAgent.Fall()` است.
- `Report()` (ASCII) برای تست/لاگ: `speed=.. phase=.. attack=.. flinch=.. [down]`.

### `BuildingMotion`
- `EaseOutBack` روی مقیاسِ فرزندان ⇒ ساختمان از زمین «رشد» می‌کند.
- تغییرِ `BuildingController.Level` ⇒ پالسِ ارتقا و بازخوانیِ `CacheParts()` (چون
  `UpdateVisuals` مقیاسِ ریشه را عوض می‌کند، پایهٔ ما همیشه تازه می‌ماند).
- افتِ `Health` ⇒ لرزشِ کوتاه (Perlin، بی‌شانسِ سراسری) + نوشتنِ `1 − health/max` روی
  `_BaziDamage` با **MaterialPropertyBlock**؛ چون متریال‌ها در `MaterialLibrary` کش
  سراسری‌اند، نوشتنِ مستقیم روی خودِ متریال همهٔ ساختمان‌ها را سیاه می‌کرد.
- `IsOperational == false` ⇒ فروپاشیِ آرام (فشار به پایین + تیره‌تر).
- در `OnDisable` همهٔ property blockها پاک می‌شوند تا استخر/بازاستفاده سالم بماند.

### `MotionDirector`
- تنها نقطهٔ اتصال: `GraphicsDirector` آن را نصب می‌کند (`Motion = GetOrAdd<MotionDirector>()`)؛
  هیچ فایلِ Gameplay ای نامِ `ActorMotion`/`BuildingMotion`/`MotionDirector` را نمی‌داند
  (تستِ `MotionLayer_IsInstalledOnlyByGraphicsDirector`).
- فهرست‌ها را با اسکنِ `World.ActorRoot` / `World.BuildingRoot` می‌سازد، فقط وقتی
  `GetInstanceID()`ِ ریشه عوض شده باشد؛ `GetComponent` قبل از `AddComponent` ⇒ idempotent.
- بودجه: حداکثر ۴۸ شخصیت و ۹۶ ساختمان؛ در سطحِ پایین (`lodBias >= 1.3`) اجزایِ دورتر از
  ۳۴ متر یک‌درمیان/سه‌درمیان به‌روز می‌شوند (`stride=2..3`) تا CPUِ موبایل آزاد بماند.
- «ساختِ تازه» از «بارگذاریِ ذخیره» با نسلِ ریشه جدا می‌شود: اگر فرزندهایِ جدید بعد از
  بازسازیِ جهان ظاهر شوند، انیمیشنِ رشد پخش می‌شود؛ در شروعِ بازی/لود، نه.

## پشتیبانیِ شیدریِ آسیب
`MaterialLibrary.Surface` برای `Panel` و `Metal` کلیدواژهٔ `_BAZI_DAMAGE_ON` را روشن و
`DamageMask` را وصل می‌کند؛ `BaziBaqa-Surface.shader` مقدارِ `_BaziDamage` را (که از
property block می‌آید) در albedo/roughness/normal فرو می‌کند. مقدارِ صفر هیچ هزینه‌ای جز
یک نمونه‌برداریِ بافتِ ۱۶پیکسلی دارد.

## چطور بررسی کنیم؟
```
python3 Tools/project_lint.py
python3 Tools/unity_meta.py --apply
# در Unity: Test Runner → EditMode → AnimationLayerEditModeTests (۷ تست)
#          Test Runner → PlayMode  → GraphicsPlayModeTests (۱۷ تست)
```
اگر بازیکنی بی‌اندام دیده شد، اول `Report()` مدیر را ببینید: `motion actors=0/48` یعنی
جهان هنوز ساخته نشده (طبیعی در منو)؛ `stride=3` یعنی سطحِ کیفیتِ پایین، انیمیشنِ دورها
تنک است و با بالا بردنِ کیفیت برطرف می‌شود.
