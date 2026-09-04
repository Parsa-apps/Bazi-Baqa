using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// سیستم صوتی بازی. صداهای کوتاه به‌صورت رویه‌ای ساخته می‌شوند تا در اولین باز شدن پروژه
    /// با Asset خارجی مشکل Missing Reference پیش نیاید. هنگام شب و خطر، محیط صوتی دنج به نسخه‌ی
    /// هشداردهنده (خطر) تغییر می‌کند و صداهای طبیعت/پرنده در پس‌زمینه پخش می‌شود.
    /// </summary>
    /// <summary>صدا؛ بعد از راه‌اندازی بقیه‌ی سامانه‌ها مقداردهی می‌شود.</summary>
    [DefaultExecutionOrder(90)]
    public sealed class AudioManager : MonoBehaviour
    {
        public bool SoundEnabled { get; private set; } = true;
        public bool VibrationEnabled { get; private set; } = true;
        public bool DangerMode { get; private set; }

        private AudioSource _effects;
        private AudioSource _ambient;
        private AudioSource _atmosphere;
        private AudioClip _click;
        private AudioClip _build;
        private AudioClip _alert;
        private AudioClip _quest;
        private AudioClip _ambientClip;
        private AudioClip _dangerClip;
        private AudioClip _birdsClip;

        public void Initialize(bool enabled, bool vibrationEnabled = true)
        {
            SoundEnabled = enabled;
            VibrationEnabled = vibrationEnabled;
            if (_effects == null) _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _effects.volume = 0.55f;
            if (_ambient == null) _ambient = gameObject.AddComponent<AudioSource>();
            _ambient.playOnAwake = false;
            _ambient.loop = true;
            _ambient.volume = 0.08f;
            _ambient.Stop();
            if (_atmosphere == null) _atmosphere = gameObject.AddComponent<AudioSource>();
            _atmosphere.playOnAwake = false;
            _atmosphere.loop = true;
            _atmosphere.volume = 0.05f;
            _atmosphere.Stop();

            DestroyStaleClips();
            _click = CreateTone("کلیک", 660f, 0.07f, 0.22f);
            _build = CreateTone("ساخت", 220f, 0.22f, 0.3f);
            _alert = CreateTone("هشدار", 110f, 0.45f, 0.42f);
            _quest = CreateTone("پاداش", 523f, 0.3f, 0.28f);
            _ambientClip = CreateAmbient();
            _dangerClip = CreateDanger();
            _birdsClip = CreateBirds();

            _ambient.clip = _ambientClip;
            _atmosphere.clip = _birdsClip;
            if (SoundEnabled) PlayAmbient();
        }

        public void SetEnabled(bool enabled)
        {
            SoundEnabled = enabled;
            if (enabled)
            {
                PlayAmbient();
            }
            else
            {
                if (_ambient != null) _ambient.Stop();
                if (_atmosphere != null) _atmosphere.Stop();
            }
        }

        public void SetDanger(bool danger)
        {
            DangerMode = danger;
            if (_ambient == null) return;
            if (danger) _ambient.clip = _dangerClip;
            else _ambient.clip = _ambientClip;
            if (SoundEnabled && !_ambient.isPlaying) _ambient.Play();
        }

        public void PlayClick()
        {
            Play(_click, 0.65f);
        }

        public void PlayBuild()
        {
            Play(_build, 0.8f);
        }

        public void PlayAlert()
        {
            Play(_alert, 1f);
            Vibrate();
        }

        public void PlayQuest()
        {
            Play(_quest, 0.7f);
        }

        public void SetVibrationEnabled(bool enabled)
        {
            VibrationEnabled = enabled;
        }

        public void Vibrate()
        {
            if (VibrationEnabled) Handheld.Vibrate();
        }

        private void PlayAmbient()
        {
            if (!SoundEnabled) return;
            if (_ambient != null && !_ambient.isPlaying) _ambient.Play();
            if (_atmosphere != null && !_atmosphere.isPlaying) _atmosphere.Play();
        }

        private void Play(AudioClip clip, float volume)
        {
            if (SoundEnabled && _effects != null && clip != null) _effects.PlayOneShot(clip, volume);
        }

        private void DestroyStaleClips()
        {
            if (_click != null) Destroy(_click);
            if (_build != null) Destroy(_build);
            if (_alert != null) Destroy(_alert);
            if (_quest != null) Destroy(_quest);
            if (_ambientClip != null) Destroy(_ambientClip);
            if (_dangerClip != null) Destroy(_dangerClip);
            if (_birdsClip != null) Destroy(_birdsClip);
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float volume)
        {
            const int sampleRate = 22050;
            int samples = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float envelope = Mathf.Sin(Mathf.PI * i / samples);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * envelope * volume;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateAmbient()
        {
            const int sampleRate = 11025;
            const float duration = 4f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("آوای آرام جزیره", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                // آکورد آرام و کند
                float chord = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.22f
                            + Mathf.Sin(2f * Mathf.PI * 164.8f * t) * 0.12f
                            + Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.06f;
                float wave = Mathf.Sin(2f * Mathf.PI * 0.25f * t) * 0.5f + 0.5f;
                data[i] = chord * (0.25f + wave * 0.15f);
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateDanger()
        {
            const int sampleRate = 11025;
            const float duration = 3f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("آوای خطر", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                // تپش پایین و عصبی
                float pulse = Mathf.Sin(2f * Mathf.PI * 2.4f * t) > 0f ? 1f : 0f;
                float drone = Mathf.Sin(2f * Mathf.PI * 82f * t) * 0.35f;
                data[i] = (drone + 0.12f) * pulse * 0.5f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateBirds()
        {
            const int sampleRate = 11025;
            const float duration = 6f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("آواز پرندگان", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                // چند توییتر کوتاه، بسیار کم‌صدا در پس‌زمینه
                float chirpA = Mathf.Sin(2f * Mathf.PI * (1800f + 400f * Mathf.Sin(2f * Mathf.PI * 0.7f * t)) * t) * (Mathf.Sin(2f * Mathf.PI * 4f * t) > 0.96f ? 1f : 0f);
                float chirpB = Mathf.Sin(2f * Mathf.PI * (1600f + 300f * Mathf.Sin(2f * Mathf.PI * 0.5f * t)) * t) * (Mathf.Sin(2f * Mathf.PI * 3f * t + 1f) > 0.97f ? 1f : 0f);
                data[i] = (chirpA + chirpB) * 0.02f;
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
