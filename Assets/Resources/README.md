# Resources

منابعی که باید بدون Addressables در شروع بازی در دسترس باشند؛ فونت فارسی نمونه اینجاست.

```
Assets/Resources/
├── Fonts/          فونت فارسی و asset های TMP
├── Localization/   جدول بومی‌سازی
├── Graphics/       GraphicsProfile.json  (تنها جای اعدادِ بصری)
├── Shaders/        شیدرهای پروژه: BaziBaqa-Surface / -Emissive / -ScreenSpaceAO + include ها
└── Textures/       Graphics/*.png  (بافت‌های رویه‌ای Albedo/Normal/Mask)
```
دلیلِ حضورِ `Shaders` و `Textures` در Resources: متریال‌ها در زمان اجرا ساخته می‌شوند و
یونیتی فقط شیدر/بافتِ ارجاع‌داده‌شده را در بیلد نگه می‌دارد؛ Resources ساده‌ترین راهِ مطمئن است
(به‌جای وابستگی به Always Included Shaders یا Addressables که در این فاز لازم نیستند).
