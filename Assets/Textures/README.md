# Textures

جایگزین تکسچرهای فشرده‌ی ASTC/ETC2 محیط، UI و آیکون‌ها.

بافت‌هایِ بازی در `Assets/Resources/Textures/Graphics` نگه داشته می‌شوند تا در زمان اجرا
قابل‌بارگذاری باشند (تولید: `python3 Tools/procedural_textures.py --apply`). این پوشه برای
بافت‌های هنریِ نهایی (تصویرِ مدل‌ها، آیکون‌های دست‌ساز) آماده می‌ماند؛ در صورت افزودن فایل،
`Tools/unity_meta.py --apply --fix-textures` تنظیماتِ ایمپورتِ یکسان را نوشته می‌کند.
