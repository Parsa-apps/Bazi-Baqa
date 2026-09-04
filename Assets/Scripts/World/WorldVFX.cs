using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// افکت‌های بصری رویه‌ای که بدون Asset خارجی کار می‌کنند: آتش اردوگاه، دود، زبانه‌ی جرقّه و
    /// درخشش آب. متریال هر Emitter از <see cref="MaterialLibrary"/> گرفته می‌شود، پس هم در
    /// Built-in و هم زیر URP درست دیده می‌شود؛ پارامترهای نورِ صحنه (Bloom/AO) از
    /// GraphicsDirector روی همان متریال‌ها اعمال می‌شود.
    ///
    /// برای جلوگیری از نشتی حافظه هنگام «بازی جدید» یا بازتولید جهان، همه‌ی Emitterها در فهرستی
    /// نگه‌داری می‌شوند و با Clear() نابود می‌شوند تا هیچ آبجکت اضافه‌ای در WorldRoot باقی نماند.
    /// </summary>
    public sealed class WorldVFX : MonoBehaviour
    {
        private readonly List<GameObject> _emitters = new List<GameObject>();

        public void Initialize(Vector3 homePosition)
        {
            // اگر درخواست بازتولید شد، ابتدا Emitterهای قبلی را آزاد می‌کنیم تا انباشته نشوند.
            Clear();
            CreateEmitter(WorldParts.Fire, homePosition + new Vector3(0f, 0.9f, 0f), new Color(1f, 0.55f, 0.12f, 0.9f), 14f, 0.12f, 18f, 0.9f, 0.5f);
            CreateEmitter(WorldParts.Smoke, homePosition + new Vector3(0f, 1.4f, 0f), new Color(0.32f, 0.32f, 0.34f, 0.5f), 5f, 0.4f, 0.9f, 0.7f, 0.3f);
            CreateEmitter(WorldParts.Spark, homePosition + new Vector3(0f, 1.1f, 0f), new Color(1f, 0.8f, 0.3f, 0.8f), 3f, 0.05f, 22f, 1.6f, 0.1f);
            CreateEmitter(WorldParts.WaterShimmer, homePosition + new Vector3(0f, 6f, 0f), new Color(0.6f, 0.85f, 1f, 0.35f), 6f, 0.02f, 0.2f, 14f, 0.15f);
        }

        /// <summary>همه‌ی Emitterها را نابود می‌کند و حافظه را آزاد می‌سازد.</summary>
        public void Clear()
        {
            for (int i = 0; i < _emitters.Count; i++)
            {
                if (_emitters[i] != null) Destroy(_emitters[i]);
            }
            _emitters.Clear();
        }

        private void CreateEmitter(string name, Vector3 position, Color color, float rate, float size, float speed, float lifetime, float gravity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 140;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 26f;
            shape.radius = 0.25f;

            ParticleSystem.ColorOverLifetimeModule colorLife = ps.colorOverLifetime;
            colorLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 0.4f), new GradientColorKey(new Color(color.r, color.g, color.b, 0f), 1f) },
                new[] { new GradientAlphaKey(color.a * 0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            colorLife.color = grad;

            // متریال و تنظیماتِ رندرر؛ پیش از این renderer هیچ متریالی نداشت (ذره‌ی ارغوانی).
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();
            bool additive = name != WorldParts.Smoke;
            Material material = MaterialLibrary.Particle(color, additive, false);
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = additive ? 0.4f : 0f;   // جرقه/آتش روی دود بنشیند
            MaterialLibrary.SetParticleFlicker(material, name == WorldParts.Fire ? 0.28f : (name == WorldParts.Spark ? 0.5f : 0f),
                                                name == WorldParts.Fire ? 5.2f : 8.5f);

            ps.Play();
            _emitters.Add(go);
        }

        private void OnDestroy()
        {
            // هنگام حذف مؤلفه، همه‌ی Emitterها را آزاد می‌کنیم.
            Clear();
        }
    }
}
