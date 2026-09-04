using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BaziBaqa
{
    /// <summary>
    /// اینتروی سینماییِ استودیو (گام ۸ از خواسته‌ها، هم‌زمان با رابطِ AAA): انرژی از حاشیه‌ها
    /// به سمتِ لوگو جمع می‌شود، پرتوهایِ نور می‌چرخند، خط‌هایِ نئونیِ افقی صفحه را اسکن
    /// می‌کنند، یک فلشِ نورِ سفید و یک موجِ ضربه‌ی طلایی لوگو را «رونمایی» می‌کنند.
    ///
    /// مرزِ مالکیت: `SplashEffects` ذرات/برق را دارد، `GlowPulse` و `CrownPulse` هاله و تاج
    /// را؛ این مؤلفه هیچ‌کدام از آن‌ها را نمی‌نویسد و فقط فرزندانِ خودش را می‌سازد ⇒ اگر
    /// حذفش کنی، اسپلش دقیقاً مثلِ قبل کار می‌کند.
    /// </summary>
    public sealed class IntroFx : MonoBehaviour
    {
        private const float TimelineSeconds = 2.6f;
        private const float ConvergeStart = 0.1f;
        private const float FlashTime = 1.18f;
        private const float RingTime = 1.3f;

        private readonly List<RectTransform> _rays = new List<RectTransform>();
        private readonly List<Image> _raysImages = new List<Image>();
        private readonly List<RectTransform> _scanLines = new List<RectTransform>();
        private readonly List<Image> _scanImages = new List<Image>();
        private readonly List<RectTransform> _sparks = new List<RectTransform>();
        private readonly List<Vector2> _sparksStart = new List<Vector2>();
        private readonly List<Image> _sparkImages = new List<Image>();

        private RectTransform _root;
        private Image _flashImage;
        private RectTransform _ring;
        private Image _ringImage;
        private RectTransform _bloom;
        private Image _bloomImage;
        private float _time;
        private bool _finished;

        public static string LastReport = string.Empty;
        public bool IsFinished { get { return _finished; } }
        public float Time01 { get { return Mathf.Clamp01(_time / TimelineSeconds); } }
        public string Report()
        {
            return "intro t=" + _time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + " rays=" + _rays.Count + " scans=" + _scanLines.Count
                + " sparks=" + _sparks.Count + (_finished ? " done" : " live");
        }

        private void Awake()
        {
            _root = transform as RectTransform;
            if (_root == null) return;
            BuildBloom();
            BuildRays(7);
            BuildScanLines(3);
            BuildSparks(26);
            BuildFlash();
            BuildRing();
        }

        private void Update()
        {
            if (_root == null || _finished) return;
            _time += Time.unscaledDeltaTime;
            float t = _time;

            // ۱) پرتوها: چرخشِ آرام + درخششِ سینوسی (هر پرتو با اختلافِ فاز)
            for (int i = 0; i < _rays.Count; i++)
            {
                RectTransform ray = _rays[i];
                if (ray == null) continue;
                float phase = i * (360f / Mathf.Max(1, _rays.Count));
                ray.localRotation = Quaternion.Euler(0f, 0f, phase + t * 26f);
                float glow = Mathf.Clamp01(0.25f + 0.75f * Mathf.Sin(t * 3.4f + i * 1.1f));
                Color color = _raysImages[i].color;
                color.a = 0.06f + 0.3f * glow * Mathf.Clamp01(1.6f - t * 0.42f);
                _raysImages[i].color = color;
            }

            // ۲) خط‌هایِ نئونِ اسکن: از پایین به بالا، یکی عقب‌تر از آن یکی
            for (int i = 0; i < _scanLines.Count; i++)
            {
                RectTransform line = _scanLines[i];
                if (line == null) continue;
                float speed = 0.62f + i * 0.17f;
                float travel = Mathf.Repeat((t - i * 0.4f) * speed, 1.35f);
                Vector2 size = _root.rect.size;
                line.anchoredPosition = new Vector2(0f, Mathf.Lerp(-size.y * 0.5f, size.y * 0.62f, travel));
                Color color = _scanImages[i].color;
                color.a = Mathf.Clamp01(0.55f - Mathf.Abs(travel - 0.5f) * 0.9f) * Mathf.Clamp01(1.5f - t * 0.4f);
                _scanImages[i].color = color;
            }

            // ۳) جرقه‌ها: از حاشیه به سمتِ لوگو جمع می‌شوند («لوگو از انرژی بیرون می‌آید»)
            float converge = Mathf.InverseLerp(ConvergeStart, FlashTime, t);
            for (int i = 0; i < _sparks.Count; i++)
            {
                RectTransform spark = _sparks[i];
                if (spark == null) continue;
                Vector2 from = _sparksStart[i];
                Vector2 to = new Vector2(Mathf.Sin(i * 2.4f) * 26f, Mathf.Cos(i * 1.7f) * 18f);
                float k = Mathf.Clamp01(converge + (i % 5) * 0.03f);
                k = k * k * (3f - 2f * k);
                spark.anchoredPosition = Vector2.Lerp(from, to, k);
                float size = Mathf.Lerp(1f, 0.25f, k);
                spark.localScale = Vector3.one * size;
                Color color = _sparkImages[i].color;
                color.a = Mathf.Clamp01(0.9f - k * 0.35f) * Mathf.Clamp01(1.4f - converge * 1.15f);
                _sparkImages[i].color = color;
            }

            // ۴) شکوفاییِ پشتِ لوگو: یک‌نفس نفس می‌کشد و در لحظه‌ی رونمایی اوج می‌گیرد
            if (_bloom != null && _bloomImage != null)
            {
                float beat = 1f + Mathf.Sin(t * 2.6f) * 0.06f;
                float reveal = 1f + Mathf.Max(0f, 1f - Mathf.Abs(t - FlashTime) * 1.7f) * 0.55f;
                _bloom.localScale = Vector3.one * beat * reveal;
                Color color = _bloomImage.color;
                color.a = Mathf.Clamp01(0.22f + 0.34f * Mathf.Clamp01(t * 1.1f) * reveal);
                _bloomImage.color = color;
            }

            // ۵) فلشِ نورِ سفید: سه‌فریمه بالا می‌آید و سریع می‌میرد
            if (_flashImage != null)
            {
                float spike = Mathf.Max(0f, 1f - Mathf.Abs(t - FlashTime) * 5.5f);
                Color color = _flashImage.color;
                color.a = spike * spike * 0.85f;
                _flashImage.color = color;
            }

            // ۶) حلقه‌ی رونمایی: از لوگو به بیرون باز می‌شود و محو می‌شود
            if (_ring != null && _ringImage != null)
            {
                float open = Mathf.Clamp01((t - RingTime) / 0.6f);
                _ring.localScale = Vector3.one * Mathf.Lerp(0.15f, 2.35f, open * (2f - open));
                Color color = _ringImage.color;
                color.a = open > 0.001f && open < 1f ? Mathf.Lerp(0.9f, 0f, open) : 0f;
                _ringImage.color = color;
            }

            if (t >= TimelineSeconds)
            {
                _finished = true;
                LastReport = Report();
                // بعد از پایان، دیگر هیچ هزینه‌ای برای صحنه ندارد (اسپلش با همزمانیِ خودش می‌رود)
                SetVisible(false);
            }
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < _raysImages.Count; i++) if (_raysImages[i] != null) _raysImages[i].enabled = visible;
            for (int i = 0; i < _scanImages.Count; i++) if (_scanImages[i] != null) _scanImages[i].enabled = visible;
            for (int i = 0; i < _sparkImages.Count; i++) if (_sparkImages[i] != null) _sparkImages[i].enabled = visible;
            if (_flashImage != null) _flashImage.enabled = visible;
            if (_ringImage != null) _ringImage.enabled = visible;
            if (_bloomImage != null) _bloomImage.enabled = visible;
        }

        private Image CreateLayer(string name, Vector2 sizeDelta, Color color)
        {
            GameObject layer = new GameObject(name, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(transform, false);
            RectTransform rect = layer.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sizeDelta;
            Image image = layer.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;      // اینتروی لوگو هیچ‌وقت کلیک را نمی‌دزدد
            return image;
        }

        private void BuildBloom()
        {
            Image bloom = CreateLayer("IntroBloom", new Vector2(560f, 320f), new Color(0.22f, 0.86f, 0.8f, 0.2f));
            _bloom = bloom.rectTransform;
            _bloomImage = bloom;
            _bloom.SetAsFirstSibling();
            Material glass = MaterialLibrary.Glass();
            if (glass != null) bloom.material = glass;
        }

        private void BuildRays(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Image ray = CreateLayer("IntroRay", new Vector2(760f, 12f), new Color(0.5f, 0.95f, 1f, 0.12f));
                ray.rectTransform.sizeDelta = new Vector2(620f + (i % 3) * 90f, 7f + (i % 2) * 5f);
                ray.rectTransform.pivot = new Vector2(0f, 0.5f);
                ray.rectTransform.anchoredPosition = Vector2.zero;
                _rays.Add(ray.rectTransform);
                _raysImages.Add(ray);
            }
        }

        private void BuildScanLines(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Image line = CreateLayer("IntroScanLine", new Vector2(860f, 2f), new Color(0.42f, 0.92f, 1f, 0.3f));
                line.rectTransform.sizeDelta = new Vector2(900f - i * 120f, i == 1 ? 3f : 1.6f);
                _scanLines.Add(line.rectTransform);
                _scanImages.Add(line);
            }
        }

        private void BuildSparks(int count)
        {
            Vector2 extent = _root != null ? new Vector2(Mathf.Max(420f, _root.rect.width * 0.5f), Mathf.Max(300f, _root.rect.height * 0.5f)) : new Vector2(480f, 300f);
            for (int i = 0; i < count; i++)
            {
                Image spark = CreateLayer("IntroSpark", Vector2.one * (3f + (i % 4)), new Color(0.62f, 0.96f, 1f, 0.8f));
                // چیدمانِ قطعی (بی‌شانس): زاویه از شماره‌ی جرقه می‌آید تا در هر اجرایِ تست یکی باشد
                float angle = i * 2.399963f;
                float radius = 0.62f + (i % 5) * 0.09f;
                Vector2 start = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1f;
                start.x *= extent.x * radius;
                start.y *= extent.y * radius;
                spark.rectTransform.anchoredPosition = start;
                _sparks.Add(spark.rectTransform);
                _sparksStart.Add(start);
                _sparkImages.Add(spark);
            }
        }

        private void BuildFlash()
        {
            Image flash = CreateLayer("IntroFlash", new Vector2(900f, 500f), new Color(0.9f, 0.98f, 1f, 0f));
            flash.rectTransform.anchorMin = Vector2.zero;
            flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.offsetMin = Vector2.zero;
            flash.rectTransform.offsetMax = Vector2.zero;
            _flashImage = flash;
            flash.transform.SetAsLastSibling();
        }

        private void BuildRing()
        {
            Image ring = CreateLayer("IntroRevealRing", new Vector2(240f, 240f), new Color(1f, 0.8f, 0.32f, 0f));
            ring.rectTransform.sizeDelta = new Vector2(300f, 168f);
            Outline outline = ring.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.86f, 0.45f, 0.9f);
            outline.effectDistance = new Vector2(0f, 0f);
            // لبه‌ی روشن با شیدرِ شیشه (اگر بود) وگرنه با Outlineِ همان Image
            Material glass = MaterialLibrary.Glass();
            if (glass != null)
            {
                ring.material = glass;
                outline.enabled = false;
            }
            _ring = ring.rectTransform;
            _ringImage = ring;
        }

        private void OnDisable()
        {
            if (_finished) return;
            _finished = true;
            LastReport = Report();
        }
    }
}
