# Materials

رنگ‌های نمونه برای نسخه‌ی قابل‌بازی در `WorldGenerator` تولید می‌شوند. متریال‌های نهایی با Shader مناسب Android اینجا قرار می‌گیرند.

تا فاز ۳، هیچ فایل `.mat` در مخزن نیست: `MaterialLibrary` متریال‌ها را با شیدرِ
`BaziBaqa/Surface` (و `BaziBaqa/Emissive` برای ذرات/نور) در زمان اجرا می‌سازد و کش می‌کند.
این انتخاب عمدی است: فایل `.mat` به GUIDِ شیدر/پکیج ارجاع می‌دهد و بدون باز شدنِ Unity
قابل‌تولیدِ مطمئن نیست ⇒ ریسک Missing Reference. اگر متریالِ هنریِ دستی خواستید، همین‌جا
بگذاریدید و `Assets/Scripts/Graphics/MaterialLibrary.cs` را طوری گسترش دهید که اول
`Resources`/آsetِ دستی و بعد شیدرِ رویه‌ای را امتحان کند.
