# خروجی Android

## تنظیمات پیشنهادی

- Unity: 2022.3 LTS
- Platform: Android
- Scripting Backend: IL2CPP
- API Compatibility: .NET Standard 2.1
- Minimum API: Android 8.0 / API 26
- Target API: آخرین API نصب‌شده
- Architecture: ARM64
- Texture Compression: ASTC در دستگاه‌های جدید، ETC2 برای سازگاری
- Managed Stripping: Medium

## خروجی خودکار (پیشنهادی)

اسکریپت `Assets/Editor/AndroidBuild.cs` خروجی را بدون باز کردن صفحه‌ی Build Settings می‌سازد:

- `BaziBaqa > Build Options > Prepare Android Settings` — پیکربندی IL2CPP، ARM64، minSdk 26 و شناسه‌ی بسته
- `BaziBaqa > Build > Build APK (تست)`
- `BaziBaqa > Build > Build APK (Release)`
- `BaziBaqa > Build > Build AAB (Google Play)`

خروجی‌ها در پوشه‌ی `Builds/Android` ذخیره می‌شوند. شناسه‌ی بسته‌ی پیش‌فرض `com.parsaapps.bazibaqa` است. پیش از انتشار، Keystore اختصاصی استودیو را در خارج از مخزن تنظیم کنید (در `PlayerSettings > Publishing Settings`).

## خروجی دستی

از مسیر `File > Build Settings` پلتفرم Android را انتخاب کنید. صحنه‌ی `Assets/Scenes/Main.unity` را به Scenes In Build اضافه کنید، سپس:

- برای نصب مستقیم: `Build > Build APK`
- برای انتشار مارکت: `Build App Bundle (Google Play)` و خروجی `.aab`

## کنترل کیفیت قبل از انتشار

- اجرای سرد و بازگشت از پس‌زمینه
- دکمه‌ی برگشت Android در منو و بازی
- چرخش یا نسبت‌های 16:9، 19.5:9 و تبلت
- ذخیره در حالت بدون دسترسی موقت به فضای ذخیره‌سازی
- تست 30 دقیقه‌ای با روشنایی و نرخ فریم دستگاه متوسط
- بررسی متن فارسی، بریدگی دوربین و ناحیه‌ی امن
