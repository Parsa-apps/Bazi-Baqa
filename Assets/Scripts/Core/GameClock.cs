using System;
using UnityEngine;

namespace BaziBaqa
{
    public sealed class GameClock
    {
        public int Day { get; private set; } = 1;
        public float NormalizedTime { get; private set; } = 0.28f;
        public float DayLengthSeconds { get; set; } = 150f;
        public bool IsNight { get { return NormalizedTime < 0.22f || NormalizedTime > 0.78f; } }
        public float Hour { get { return NormalizedTime * 24f; } }

        public event Action<int> DayChanged;
        public event Action<bool> NightChanged;

        private bool _lastNight;

        public void Initialize(int day, float time)
        {
            Day = Mathf.Max(1, day);
            NormalizedTime = Mathf.Repeat(time, 1f);
            _lastNight = IsNight;
        }

        public void Advance(float realDelta)
        {
            if (DayLengthSeconds <= 0f) return;
            bool wasNight = IsNight;
            NormalizedTime += Mathf.Max(0f, realDelta) / DayLengthSeconds;
            if (NormalizedTime >= 1f)
            {
                NormalizedTime -= 1f;
                Day++;
                DayChanged?.Invoke(Day);
            }

            bool night = IsNight;
            if (night != wasNight)
            {
                _lastNight = night;
                NightChanged?.Invoke(night);
            }
        }

        public string GetClockText()
        {
            int hour = Mathf.FloorToInt(Hour);
            int minute = Mathf.FloorToInt((Hour - hour) * 60f);
            // ساعت با ارقامِ زبانِ فعال نمایش داده می‌شود (فارسی: ۱۴:۳۰ / انگلیسی: 14:30).
            string clock = hour.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":"
                + minute.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            return LocalizationManager.Number(clock);
        }

        public static string ToPersianDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            const string latin = "0123456789";
            const string persian = "۰۱۲۳۴۵۶۷۸۹";
            char[] chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                int index = latin.IndexOf(chars[i]);
                if (index >= 0) chars[i] = persian[index];
            }
            return new string(chars);
        }
    }
}
