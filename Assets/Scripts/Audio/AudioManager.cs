using UnityEngine;

namespace BaziBaqa
{
    public sealed class AudioManager : MonoBehaviour
    {
        public bool SoundEnabled { get; private set; } = true;
        public bool VibrationEnabled { get; private set; } = true;

        private AudioSource _effects;
        private AudioSource _ambient;
        private AudioClip _click;
        private AudioClip _build;
        private AudioClip _alert;
        private AudioClip _ambientClip;

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
            if (_click != null) Destroy(_click);
            if (_build != null) Destroy(_build);
            if (_alert != null) Destroy(_alert);
            if (_ambientClip != null) Destroy(_ambientClip);
            _click = CreateTone("کلیک", 660f, 0.07f, 0.22f);
            _build = CreateTone("ساخت", 220f, 0.22f, 0.3f);
            _alert = CreateTone("هشدار", 110f, 0.45f, 0.42f);
            _ambientClip = CreateAmbient();
            _ambient.clip = _ambientClip;
            if (SoundEnabled) _ambient.Play();
        }

        public void SetEnabled(bool enabled)
        {
            SoundEnabled = enabled;
            if (_ambient == null) return;
            if (enabled)
            {
                if (!_ambient.isPlaying) _ambient.Play();
            }
            else
            {
                _ambient.Stop();
            }
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

        public void SetVibrationEnabled(bool enabled)
        {
            VibrationEnabled = enabled;
        }

        public void Vibrate()
        {
            if (VibrationEnabled) Handheld.Vibrate();
        }

        private void Play(AudioClip clip, float volume)
        {
            if (SoundEnabled && _effects != null && clip != null) _effects.PlayOneShot(clip, volume);
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
                float chord = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.22f + Mathf.Sin(2f * Mathf.PI * 164.8f * t) * 0.12f;
                float wave = Mathf.Sin(2f * Mathf.PI * 0.25f * t) * 0.5f + 0.5f;
                data[i] = chord * (0.25f + wave * 0.15f);
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
