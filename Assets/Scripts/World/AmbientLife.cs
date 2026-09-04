using System.Collections.Generic;
using UnityEngine;

namespace BaziBaqa
{
    /// <summary>
    /// محیط زنده‌ی سبک: درختان و بوته‌ها به‌آرامی تکان می‌خورند و چند پرنده در آسمان این‌سو و آن‌سو
    /// می‌روند. این مؤلفه با کمترین امکانات و بدون Asset خارجی، حس طبیعی بودن را می‌سازد و
    /// روی دستگاه‌های متوسط هم روان است.
    /// </summary>
    public sealed class AmbientLife : MonoBehaviour
    {
        private readonly List<Transform> _swaying = new List<Transform>();
        private readonly List<Transform> _birds = new List<Transform>();
        private readonly List<float> _birdSpeeds = new List<float>();
        private readonly List<float> _birdPhases = new List<float>();

        private Vector3 _sceneCenter = Vector3.zero;
        private Material _birdMaterial;

        public void RegisterTree(Transform tree)
        {
            if (tree != null) _swaying.Add(tree);
        }

        public void Initialize()
        {
            _birdMaterial = CreateBirdMaterial();
            SpawnBirds();
        }

        public void Clear()
        {
            for (int i = 0; i < _birds.Count; i++)
            {
                if (_birds[i] != null) Destroy(_birds[i].gameObject);
            }
            _birds.Clear();
            _swaying.Clear();
            _birdSpeeds.Clear();
            _birdPhases.Clear();
        }

        private void Update()
        {
            float time = Time.time;
            for (int i = 0; i < _swaying.Count; i++)
            {
                if (_swaying[i] == null) continue;
                _swaying[i].localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(time * 0.9f + i) * 2.4f);
            }
            for (int i = 0; i < _birds.Count; i++)
            {
                if (_birds[i] == null) continue;
                _birds[i].localPosition = BirdPosition(time, _birdPhases[i], _birdSpeeds[i]);
                _birds[i].localEulerAngles = new Vector3(0f, Mathf.Sin(time * 0.6f + i) * 12f, 0f);
            }
        }

        private Vector3 BirdPosition(float time, float phase, float speed)
        {
            float x = Mathf.Sin(time * speed + phase) * WorldGenerator.WorldWidth * 0.34f;
            float z = _sceneCenter.z + Mathf.Cos(time * speed * 0.7f + phase) * WorldGenerator.WorldDepth * 0.22f;
            float y = 9f + Mathf.Sin(time * 0.8f + phase) * 1.2f;
            return new Vector3(x, y, z);
        }

        private void SpawnBirds()
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject bird = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bird.name = WorldParts.Bird + i;
                bird.transform.SetParent(transform, false);
                Renderer renderer = bird.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = _birdMaterial;
                bird.transform.localScale = new Vector3(0.28f, 0.12f, 0.16f);
                _birds.Add(bird.transform);
                _birdSpeeds.Add(Random.Range(0.05f, 0.11f));
                _birdPhases.Add(Random.Range(0f, Mathf.PI * 2f));
            }
        }

        private Material CreateBirdMaterial()
        {
            // پیش از این Shader.Find("Standard") بود؛ زیر URP ارغوانی می‌شد. حالا از
            // MaterialLibrary می‌آید که هر دو خطِ رندر را می‌شناسد.
            return MaterialLibrary.Tinted(new Color(0.08f, 0.1f, 0.13f), 0.1f);
        }
    }
}
