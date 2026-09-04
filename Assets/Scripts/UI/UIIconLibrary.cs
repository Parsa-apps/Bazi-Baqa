using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// آیکن‌هایِ رویه‌ای (گام ۶): هر آیکن با توابعِ فاصله‌یِ امضاشده روی یک Texture2Dِ ۶۴×۶۴
    /// کشیده می‌شود ⇒ بدونِ PNG، بدونِ Atlas، بدونِ وابستگیِ هنری؛ و کش‌شده تا هر پنجره
    /// که باز می‌شود بافتِ تازه نسازد.
    ///
    /// آیکن‌ها `raycastTarget = false` هستند: انتخابِ لمسیِ پنل‌ها و دکمه‌ها دست‌نخورده می‌ماند.
    /// </summary>
    public static class UIIconLibrary
    {
        public const int IconSize = 64;

        private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<ResourceType, Color> _tints = new Dictionary<ResourceType, Color>
        {
            { ResourceType.Wood, new Color(0.72f, 0.48f, 0.26f) },
            { ResourceType.Stone, new Color(0.66f, 0.7f, 0.76f) },
            { ResourceType.Food, new Color(0.55f, 0.8f, 0.36f) },
            { ResourceType.Gold, new Color(1f, 0.78f, 0.28f) },
            { ResourceType.Energy, new Color(0.42f, 0.86f, 1f) },
            { ResourceType.Water, new Color(0.36f, 0.66f, 1f) }
        };

        public static int CachedCount { get { return _sprites.Count; } }

        public static string Report()
        {
            return "icons=" + _sprites.Count + "/" + (System.Enum.GetValues(typeof(ResourceType)).Length + 8);
        }

        public static void ClearCache()
        {
            foreach (Sprite sprite in _sprites.Values)
            {
                if (sprite != null)
                {
                    if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
                    UnityEngine.Object.Destroy(sprite);
                }
            }
            _sprites.Clear();
        }

        public static Color TintFor(ResourceType type)
        {
            Color color;
            return _tints.TryGetValue(type, out color) ? color : new Color(0.8f, 0.86f, 0.9f);
        }

        public static Sprite Get(ResourceType type)
        {
            return Get(Key(type));
        }

        /// <summary>کلیدهایِ آزاد: wood/stone/food/gold/energy/water/population/tech/clock/day/night/rain/storm/health.</summary>
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;
            Sprite baked = Bake(key);
            _sprites[key] = baked;
            return baked;
        }

        public static string Key(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return "wood";
                case ResourceType.Stone: return "stone";
                case ResourceType.Food: return "food";
                case ResourceType.Gold: return "gold";
                case ResourceType.Energy: return "energy";
                case ResourceType.Water: return "water";
                default: return "tech";
            }
        }

        /// <summary>یک نشانِ گردِ کوچک کنارِ برچسب می‌سازد (بدونِ LayoutGroup؛ با لنگرِ دستی).</summary>
        public static UnityEngine.UI.Image AttachBadge(Transform parent, ResourceType type, float size, float rightInset)
        {
            if (parent == null) return null;
            GameObject badgeObject = new GameObject("IconBadge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            RectTransform rect = badgeObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            // راست‌چین: در RTL آیکن سمتِ راستِ متن می‌نشیند
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-rightInset, 0f);
            UnityEngine.UI.Image image = badgeObject.GetComponent<UnityEngine.UI.Image>();
            image.sprite = Get(type);
            image.color = TintFor(type);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static Sprite Bake(string key)
        {
            int size = IconSize;
            Color32[] pixels = new Color32[size * size];
            Color baseColor = KeyToColor(key);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // مختصاتِ مراکز‌محور با y رو‌به‌بالا، واحد [-1,1]
                    Vector2 p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    float shape = ShapeDistance(key, p);
                    float aa = 0.045f;
                    float cover = 1f - SmoothStep(-aa, aa, shape);

                    // نشانِ پس‌زمینه: مربعِ گرد با گرادیانِ عمودیِ ملایم
                    float badge = 1f - SmoothStep(-0.02f, 0.06f, RoundBox(p, new Vector2(0.94f, 0.94f), 0.34f));
                    float gradient = 1f - (p.y * 0.5f + 0.5f) * 0.45f;
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(RoundBox(p, new Vector2(0.94f, 0.94f), 0.34f) + 0.05f) / 0.06f);

                    Color32 pixel = new Color32();
                    Color back = baseColor * (0.14f + 0.16f * gradient);
                    Color ink = Color.Lerp(baseColor, Color.white, 0.55f) * 1.35f;
                    Color mixed = Color.Lerp(back, ink, Mathf.Clamp01(cover));
                    mixed = Color.Lerp(mixed, ink * 1.15f, rim * 0.5f);
                    float alpha = Mathf.Clamp01(badge * 0.8f + cover * 0.95f + rim * 0.35f);
                    if (alpha <= 0.002f)
                    {
                        pixels[y * size + x] = pixel;
                        continue;
                    }
                    pixel.r = (byte)Mathf.Clamp(Mathf.RoundToInt(mixed.r * 255f), 0, 255);
                    pixel.g = (byte)Mathf.Clamp(Mathf.RoundToInt(mixed.g * 255f), 0, 255);
                    pixel.b = (byte)Mathf.Clamp(Mathf.RoundToInt(mixed.b * 255f), 0, 255);
                    pixel.a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                    pixels[y * size + x] = pixel;
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BaziIcon_" + key,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0,
                SpriteMeshType.FullRect);
        }

        private static Color KeyToColor(string key)
        {
            foreach (KeyValuePair<ResourceType, Color> pair in _tints)
            {
                if (Key(pair.Key) == key) return pair.Value;
            }
            switch (key)
            {
                case "population": return new Color(0.6f, 0.86f, 0.95f);
                case "tech": return new Color(0.55f, 0.7f, 1f);
                case "clock": return new Color(0.85f, 0.9f, 0.95f);
                case "day": return new Color(1f, 0.82f, 0.4f);
                case "night": return new Color(0.5f, 0.6f, 1f);
                case "rain":
                case "storm": return new Color(0.45f, 0.72f, 1f);
                case "health": return new Color(1f, 0.45f, 0.5f);
                default: return new Color(0.8f, 0.86f, 0.9f);
            }
        }

        /// <summary>
        /// هر آیکن فقط از اجتماعِ جعبه‌ی گرد، بیضی و کپسول ساخته می‌شود؛ سه عملِ boolِ
        /// min/max که رفتارشان قابل‌ پیش‌بینی است (نه چندضلعیِ دستی، نه بافتِ مرجع).
        /// </summary>
        private static float ShapeDistance(string key, Vector2 p)
        {
            switch (key)
            {
                case "wood":
                {
                    // دو تنهٔ چوبِ خوابیده و یک تختهٔ کج
                    float d = RoundBox(Rotate(p, 0.02f) - new Vector2(0f, 0.34f), new Vector2(0.62f, 0.15f), 0.1f);
                    d = Min(d, RoundBox(Rotate(p, -0.02f) - new Vector2(-0.06f, -0.02f), new Vector2(0.5f, 0.15f), 0.1f));
                    d = Min(d, RoundBox(Rotate(p, -0.42f) - new Vector2(0.24f, -0.4f), new Vector2(0.34f, 0.13f), 0.09f));
                    return d;
                }
                case "stone":
                {
                    float d = RoundBox(Rotate(p, 0.5f) - new Vector2(-0.02f, -0.08f), new Vector2(0.5f, 0.36f), 0.2f);
                    d = Min(d, RoundBox(Rotate(p, -0.4f) - new Vector2(0.32f, 0.34f), new Vector2(0.2f, 0.17f), 0.12f));
                    return d;
                }
                case "food":
                {
                    float body = Min(Ellipse(p - new Vector2(-0.17f, -0.08f), new Vector2(0.31f, 0.36f)),
                        Ellipse(p - new Vector2(0.17f, -0.08f), new Vector2(0.31f, 0.36f)));
                    float leaf = RoundBox(Rotate(p, -0.5f) - new Vector2(0.18f, 0.42f), new Vector2(0.2f, 0.07f), 0.06f);
                    float stem = Capsule(p, new Vector2(0f, 0.14f), new Vector2(0.03f, 0.4f), 0.045f);
                    return Min(Min(body, leaf), stem);
                }
                case "gold":
                {
                    // سکه: حلقه + شیارِ مورب
                    float ring = Max(Ellipse(p, new Vector2(0.54f, 0.54f)), -Ellipse(p, new Vector2(0.36f, 0.36f)));
                    float slot = Capsule(p, new Vector2(-0.13f, -0.14f), new Vector2(0.13f, 0.16f), 0.05f);
                    return Min(ring, slot);
                }
                case "energy":
                {
                    // رعد: سه پاره‌خطِ قطور
                    float d = Capsule(p, new Vector2(0.14f, 0.6f), new Vector2(-0.3f, 0.02f), 0.075f);
                    d = Min(d, Capsule(p, new Vector2(-0.3f, 0.02f), new Vector2(0.08f, 0.02f), 0.075f));
                    d = Min(d, Capsule(p, new Vector2(0.08f, 0.02f), new Vector2(-0.16f, -0.6f), 0.075f));
                    return d;
                }
                case "water":
                {
                    // قطره: بیضیِ پایین + مخروطِ باریکِ بالایی (نوک)
                    float body = Ellipse(p - new Vector2(0f, -0.18f), new Vector2(0.4f, 0.42f));
                    float width = Mathf.Max(0f, Mathf.Abs(p.x)) - Mathf.Max(0f, 0.42f - (p.y + 0.1f) * 0.55f);
                    float cone = Mathf.Max(width, Mathf.Max(0f, p.y - 0.62f), Mathf.Max(-0.2f, -p.y) - 0.2f);
                    return Min(body, cone);
                }
                case "population":
                {
                    float head = Ellipse(p - new Vector2(0f, 0.3f), new Vector2(0.19f, 0.19f));
                    float body = RoundBox(p - new Vector2(0f, -0.28f), new Vector2(0.4f, 0.3f), 0.24f);
                    return Min(head, body);
                }
                case "tech":
                {
                    float ring = Max(Ellipse(p, new Vector2(0.5f, 0.5f)), -Ellipse(p, new Vector2(0.32f, 0.32f)));
                    float core = Ellipse(p, new Vector2(0.13f, 0.13f));
                    float armA = Capsule(p, new Vector2(0f, 0.42f), new Vector2(0f, 0.66f), 0.05f);
                    float armB = Capsule(p, new Vector2(0.36f, -0.22f), new Vector2(0.57f, -0.34f), 0.05f);
                    float armC = Capsule(p, new Vector2(-0.36f, -0.22f), new Vector2(-0.57f, -0.34f), 0.05f);
                    return Min(Min(Min(ring, core), armA), Min(armB, armC));
                }
                case "clock":
                {
                    float ring = Max(Ellipse(p, new Vector2(0.54f, 0.54f)), -Ellipse(p, new Vector2(0.4f, 0.4f)));
                    float hour = Capsule(p, new Vector2(0f, 0f), new Vector2(0.22f, 0.16f), 0.055f);
                    float minute = Capsule(p, new Vector2(0f, 0f), new Vector2(0f, 0.34f), 0.055f);
                    return Min(Min(ring, hour), minute);
                }
                case "day":
                {
                    float disc = Ellipse(p, new Vector2(0.26f, 0.26f));
                    float rays = float.MaxValue;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 0.7853982f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        rays = Min(rays, Capsule(p, dir * 0.36f, dir * 0.6f, 0.05f));
                    }
                    return Min(disc, rays);
                }
                case "night":
                {
                    // هلال: تفاضلِ دو دایره
                    return Max(Ellipse(p - new Vector2(0.04f, 0f), new Vector2(0.5f, 0.5f)),
                        -Ellipse(p - new Vector2(0.32f, 0.14f), new Vector2(0.44f, 0.44f)));
                }
                case "rain":
                case "storm":
                {
                    float cloud = Min(Ellipse(p - new Vector2(-0.16f, 0.24f), new Vector2(0.24f, 0.2f)),
                        RoundBox(p - new Vector2(0.04f, 0.14f), new Vector2(0.42f, 0.16f), 0.16f));
                    cloud = Min(cloud, Ellipse(p - new Vector2(0.24f, 0.2f), new Vector2(0.2f, 0.18f)));
                    float drops = Capsule(p, new Vector2(-0.22f, -0.14f), new Vector2(-0.3f, -0.5f), 0.045f);
                    drops = Min(drops, Capsule(p, new Vector2(0.04f, -0.14f), new Vector2(-0.04f, -0.5f), 0.045f));
                    drops = Min(drops, Capsule(p, new Vector2(0.3f, -0.14f), new Vector2(0.22f, -0.5f), 0.045f));
                    if (key == "storm")
                    {
                        drops = Min(drops, Capsule(p, new Vector2(0.02f, -0.2f), new Vector2(-0.1f, -0.6f), 0.07f));
                    }
                    return Min(cloud, drops);
                }
                case "health":
                {
                    // قلبِ ساده: دو بیضی + سه ضلعِ قطور
                    float lobes = Min(Ellipse(p - new Vector2(-0.2f, 0.18f), new Vector2(0.28f, 0.28f)),
                        Ellipse(p - new Vector2(0.2f, 0.18f), new Vector2(0.28f, 0.28f)));
                    float sides = Capsule(p, new Vector2(-0.44f, 0.16f), new Vector2(0f, -0.56f), 0.02f);
                    sides = Min(sides, Capsule(p, new Vector2(0.44f, 0.16f), new Vector2(0f, -0.56f), 0.02f));
                    sides = Min(sides, Capsule(p, new Vector2(-0.42f, 0.02f), new Vector2(0.42f, 0.02f), 0.02f));
                    return Min(lobes, sides);
                }
                default:
                    return Ellipse(p, new Vector2(0.36f, 0.36f));
            }
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Min(float a, float b) { return a < b ? a : b; }
        private static float Max(float a, float b) { return a > b ? a : b; }

        private static Vector2 Rotate(Vector2 p, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
        }

        /// <summary>جعبهٔ گرد (SDFِ استاندارد): صفر روی مرز، منفی داخل.</summary>
        private static float RoundBox(Vector2 p, Vector2 half, float radius)
        {
            float r = Mathf.Clamp(radius, 0.0001f, 0.99f * Mathf.Min(half.x, half.y));
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(r, r);
            // نسخهٔ استاندارد SDF: فقط مؤلفه‌های مثبت فاصلهٔ بیرونی را می‌سازند، وگرنه
            // داخلِ جعبه هم «خارج» خوانده می‌شود و شکلِ آیکن ناپدید می‌شود.
            Vector2 clamped = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            float outside = clamped.magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
            return outside + inside - r;
        }

        private static float Ellipse(Vector2 p, Vector2 radius)
        {
            float r = Mathf.Max(0.0001f, radius.x);
            float s = Mathf.Max(0.0001f, radius.y);
            return (new Vector2(p.x / r, p.y / s)).magnitude - 1f;
        }

        private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float denom = Vector2.Dot(ba, ba);
            float h = denom > 0.0001f ? Mathf.Clamp01(Vector2.Dot(pa, ba) / denom) : 0f;
            return (pa - ba * h).magnitude - thickness;
        }
    }
}
