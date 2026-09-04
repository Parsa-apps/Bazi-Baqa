using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// فونت‌های متن فارسی. لی‌اوتِ فارسی با Vazirmatn (پشتیبانی کامل RTL، شکل‌دهی حروف و
    /// اعداد فارسی) انجام می‌شود. اگر فونت در دسترس نبود، به ترتیب به DejaVuSans و Arial برمی‌گردد
    /// تا هیچ متنی بدون فونت نماند.
    /// </summary>
    public static class GameFont
    {
        private static Font _persian;

        public static Font Persian
        {
            get
            {
                if (_persian == null)
                {
                    _persian = Resources.Load<Font>("Fonts/Vazirmatn");
                    if (_persian == null) _persian = Resources.Load<Font>("Fonts/DejaVuSans");
                    if (_persian == null) _persian = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _persian;
            }
        }
    }
}
