using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// افکت‌های بصری رویه‌ای که بدون Asset خارجی کار می‌کنند: آتش اردوگاه، دود، زبانه‌ی جرقّه و
    /// درخشش آب. این مؤلفه در Built-in Render Pipeline هم به‌درستی اجرا می‌شود و سطح گرافیکی را
    /// بدون هزینه‌ی سنگین بالا می‌برد. (پاس Bloom/AO در Editor با URP تکمیل می‌شود.)
    /// </summary>
    public sealed class WorldVFX : MonoBehaviour
    {
        private ParticleSystem _fire;
        private ParticleSystem _smoke;
        private ParticleSystem _embers;
        private ParticleSystem _shimmer;
        private Transform _home;

        public void Initialize(Vector3 homePosition)
        {
            _home = transform;
            _fire = CreateEmitter("آتش", homePosition + new Vector3(0f, 0.9f, 0f), new Color(1f, 0.55f, 0.12f, 0.9f), 14f, 0.12f, 18f, 0.9f, 0.5f);
            _smoke = CreateEmitter("دود", homePosition + new Vector3(0f, 1.4f, 0f), new Color(0.32f, 0.32f, 0.34f, 0.5f), 5f, 0.4f, 0.9f, 0.7f, 0.3f);
            _embers = CreateEmitter("جرقه", homePosition + new Vector3(0f, 1.1f, 0f), new Color(1f, 0.8f, 0.3f, 0.8f), 3f, 0.05f, 22f, 1.6f, 0.1f);
            _shimmer = CreateEmitter("درخشش آب", homePosition + new Vector3(0f, 6f, 0f), new Color(0.6f, 0.85f, 1f, 0.35f), 6f, 0.02f, 0.2f, 14f, 0.15f);
        }

        private ParticleSystem CreateEmitter(string name, Vector3 position, Color color, float rate, float size, float speed, float lifetime, float gravity)
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
            ps.Play();
            return ps;
        }
    }
}
