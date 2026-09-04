# مدیریت نسخه (فاز ۲٫۵ — بخش ۴)

## منبع حقیقت

همه‌ی شماره‌ها فقط در یک فایل زندگی می‌کنند: **`Assets/Resources/VersionConfig.json`**

```json
{
  "versionName": "0.2.0",      // Version Name / bundleVersion  (نمایش به کاربر)
  "versionCode": 2,            // Build Number / AndroidBundleVersionCode  (باید در هر انتشار بیشتر شود)
  "bundleId": "com.parsaapps.bazibaqa",   // Package Identifier (Android / iOS / Standalone)
  "company": "Parsa Apps",     // companyName
  "product": "سرزمین بقا",     // productName (نام بازی در لانچر/استور)
  "minSdkVersion": 26,         // Android Min SDK  (الزام گوگل‌پلی: ≥ 26 / Android 8.0)
  "targetSdkVersion": 34       // Android Target SDK  (الزام گوگل‌پلی ۲۰۲۴: ≥ 34 / Android 14)
}
```

در زمان اجرا `GameBootstrap` همین فایل را با `Resources.Load<TextAsset>("VersionConfig")` می‌خواند و
`GameVersion` نگهش می‌دارد؛ پنجره‌ی «درباره‌ی سازنده» `GameVersion.Display` (مثلاً `0.2.0 (2)`) و
`GameVersion.Summary` (بسته + SDKها) را نشان می‌دهد — پس شماره‌ای که کاربر می‌بیند همان است که بیلد می‌شود.

## VersionManager (Editor)

| منو | کار |
| --- | --- |
| `BaziBaqa > Version > Apply Version To Player Settings` | فایل را روی `PlayerSettings` می‌نشاند (Version Name، Build Number، Package ID در هر سه پلتفرم، company/product، min/target SDK). |
| `BaziBaqa > Version > Bump Patch / Minor / Major` | فقط فایل را افزایش می‌دهد (`0.2.0 → 0.2.1`)، `versionCode` را one-up می‌کند، بعد Apply + Verify می‌کند. |
| `BaziBaqa > Version > Verify Version Consistency` | بدون نوشتن، اختلاف فایل و `PlayerSettings` را گزارش می‌کند. |
| `BaziBaqa > Version > Open Version Config` | فایل را در ادیتور باز می‌کند. |

نکته‌ی سازگاری: `minSdkVersion`/`targetSdkVersion` به‌صورت عددِ API level ذخیره می‌شوند و هنگام اعمال
با `Enum.IsDefined` تبدیل می‌شوند؛ اگر Unityِ موجود آن سطح را نشناسد، به‌جای خطای کامپایل از
`AndroidApiLevelAuto` استفاده می‌شود (در Unity 2022.3 عضوهای نمادینِ API ۳۵/۳۶ وجود ندارند).

## خط فرمان / CI

```bash
Unity -batchmode -quit -projectPath . \
  -executeMethod BaziBaqa.EditorTools.VersionManager.ApplyBatch     # اعمال + بررسی، کد خروجی ۱ یعنی ناهماهنگ
Unity -batchmode -quit -projectPath . \
  -executeMethod BaziBaqa.EditorTools.VersionManager.VerifyBatch    # فقط بررسی
```

`Tools/unity_validation.sh` مرحله‌ی `version` را دارد (بین بیک فونت و تست‌ها). دروازه‌ی ایستای
`Tools/project_lint.py` هم همین قرارداد را بین `VersionConfig.json` و `ProjectSettings/ProjectSettings.asset`
می‌سنجد، پس ناهماهنگی در CI همان‌جا گرفته می‌شود — حتی وقتی Unity در محیط نیست.

## هماهنگی با بیلد Android

`Assets/Editor/AndroidBuild.cs`:

1. پیش از بیلد: `ProjectAudit.RunFullAudit` → `VersionManager.Apply()` → `VersionManager.Verify()`؛
   اگر نسخه هماهنگ نباشد بیلد **متوقف** می‌شود (خروجی با Build Number تکراری در استور رد می‌شود).
2. SDKها، IL2CPP، ARM64، ASTC و `buildAppBundle` از همان پیکربندی تنظیم می‌شوند.
3. نامِ فایل خروجی با نسخه برچسب می‌خورد: `BaziBaqa.aab` → `BaziBaqa-0.2.0.aab`
   (اگر نسخه در نام باشد، دوبار اضافه نمی‌شود).
4. امضا: اگر `BAZIBAAQA_KEYSTORE` ست شده باشد یا فایلی در `Assets/Keystore/*.keystore` باشد،
   `useCustomKeystore` فعال و رمزها از `BAZIBAAQA_KEYSTORE_PASS` / `BAZIBAAQA_KEYALIAS` /
   `BAZIBAAQA_KEYALIAS_PASS` خوانده می‌شوند؛ اگر نبود، بیلد با keystore پیش‌فرض ادامه می‌دهد
   (برای تست روی دستگاه) و هیچ‌وقت به‌خاطر نبودِ keystore نمی‌شکند.

## قراردادِ افزایش نسخه

| موقعیت | چه چیزی عوض می‌شود |
| --- | --- |
| اصلاحِ باگِ کوچک | `Bump Patch` → `0.2.1`، Build Number +۱ |
| قابلیتِ تازه (مثلاً ارتقای AAA) | `Bump Minor` → `0.3.0`، Build Number +۱ |
| شکستِ سازگاریِ ذخیره/بسته | `Bump Major` → `1.0.0`، Build Number +۱ |

- `versionCode` هیچ‌وقت کم نمی‌شود و تکراری نمی‌شود (شرطِ Google Play).
- شماره‌ی ذخیره (`SaveSystem.CurrentSaveVersion`) مستقل از نسخه‌ی بسته است و فقط با تغییرِ ساختارِ سیو بالا می‌رود؛ سندش در `Docs/Architecture.md` است.
- پس از هر Bump: `python3 Tools/project_lint.py` (بررسیِ YAML↔JSON) و در دسترس بودن Unity: `ApplyBatch`.

## وضعیتِ فعلی

| field | مقدار |
| --- | --- |
| Version Name | `0.2.0` |
| Build Number | `2` |
| Package Identifier | `com.parsaapps.bazibaqa` (Android/iOS/Standalone) |
| Company / Product | `Parsa Apps` / `سرزمین بقا` |
| minSdk / targetSdk | `26` / `34` |
| Scripting / Architectures | IL2CPP، ARM64، ASTC، Managed Stripping: High |
| Active Input Handling | `activeInputHandler: 0` — Input Manager کلاسیک (پکیژ Input System نصب نیست و کد از `Input.*` استفاده می‌کند) |
