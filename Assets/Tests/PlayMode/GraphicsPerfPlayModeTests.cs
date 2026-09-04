using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// آزمونِ زنده‌ی بودجه‌ها (گام ۷): کالر فقط رندر را خاموش می‌کند و برمی‌گرداند، استخرِ
    /// افکت سقف را نمی‌شکند، و کشِ متریال با بارِ کار بالاتر نمی‌رود (نشتیِ material = مرگِ
    /// روی موبایلِ متوسط).
    /// </summary>
    public class GraphicsPerfPlayModeTests
    {
        private static GameObject MakeVisibleCube(string name, Vector3 position)
        {
            GameObject cube = new GameObject(name);
            MeshFilter filter = cube.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            MeshRenderer renderer = cube.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialLibrary.Surface(MaterialLibrary.SurfaceStyle.Rock, new Color(0.5f, 0.5f, 0.5f), 0f);
            cube.transform.position = position;
            return cube;
        }

        [UnityTest]
        public IEnumerator Culler_IsInstalledAndNeverHidesNearbyObjects()
        {
            GraphicsDirector director = GraphicsDirector.Ensure();
            yield return null;
            DistanceCuller culler = director.Cull;
            Assert.IsNotNull(culler, "بودجه‌ی دید باید توسط مدیرِ گرافیک نصب شود");
            Assert.GreaterOrEqual(culler.CullDistance, 8f, "فاصله‌ی کالینگ از نمایه می‌آید");
            Assert.GreaterOrEqual(culler.ShadowCasterDistance, 1f);
            Assert.LessOrEqual(culler.ShadowCasterDistance, culler.CullDistance, "سایه نباید از دید دورتر برود");

            GameObject cameraObject = new GameObject("CullTestCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;

            // مبنا موقعیتِ خودِ Camera.main است (اگر دوربینِ دیگری در صحنهٔ تست باشد، همان
            // دوربین را کالر می‌بیند، نه دوربینِ ما)
            Vector3 cameraPosition = Camera.main != null ? Camera.main.transform.position : cameraObject.transform.position;
            GameObject near = MakeVisibleCube("NearCube", cameraPosition + new Vector3(0f, 0f, 4f));
            GameObject far = MakeVisibleCube("FarCube", cameraPosition + new Vector3(0f, 0f, 900f));
            Assert.IsTrue(culler.Track(near), "شیء نزدیک باید ثبت شود");
            Assert.IsTrue(culler.Track(far), "شیء دور باید ثبت شود");

            culler.ScanNow();
            MeshRenderer nearRenderer = near.GetComponent<MeshRenderer>();
            MeshRenderer farRenderer = far.GetComponent<MeshRenderer>();
            Assert.IsTrue(nearRenderer.enabled, "نزدیک باید دیده شود");
            Assert.IsFalse(farRenderer.enabled, "آن‌سویِ بودجه‌ی دید نباید رندر شود");
            Assert.GreaterOrEqual(culler.HiddenCount, 1);

            culler.RestoreAll();
            Assert.IsTrue(farRenderer.enabled, "بازگردانیِ دستی همه‌چیز را نمایان می‌کند");

            culler.ClearTracked();
            Assert.AreEqual(0, culler.TrackedTargets);
            UnityEngine.Object.Destroy(near);
            UnityEngine.Object.Destroy(far);
            UnityEngine.Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator Culler_RestoresEverythingWhenDisabled()
        {
            GraphicsDirector director = GraphicsDirector.Ensure();
            yield return null;
            DistanceCuller culler = director.Cull;
            if (culler == null) yield break;

            GameObject cameraObject = new GameObject("CullDisableCamera");
            cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            Vector3 cameraPosition = Camera.main != null ? Camera.main.transform.position : cameraObject.transform.position;
            GameObject far = MakeVisibleCube("FarCube2", cameraPosition + new Vector3(0f, 0f, 900f));
            culler.Track(far);
            culler.ScanNow();
            MeshRenderer renderer = far.GetComponent<MeshRenderer>();
            Assert.IsFalse(renderer.enabled, "قبل از خاموش‌شدنِ مدیر، شیء دور مخفی است");

            culler.enabled = false;
            Assert.IsTrue(renderer.enabled, "خاموش‌شدنِ کالر باید همه‌چیز را برگرداند (هیچ شیئی گم نمی‌شود)");

            culler.enabled = true;
            culler.ClearTracked();
            UnityEngine.Object.Destroy(far);
            UnityEngine.Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator VfxPool_NeverExceedsItsCapacity()
        {
            GraphicsDirector director = GraphicsDirector.Ensure();
            yield return null;
            VfxDirector vfx = director.Vfx;
            Assert.IsNotNull(vfx, "مدیرِ افکت باید نصب باشد");

            Vector3 origin = Vector3.zero;
            for (int i = 0; i < 30; i++)
            {
                vfx.PlayImpact(origin + new Vector3(i * 0.5f, 0f, 0f), new Color(1f, 0.6f, 0.2f));
                vfx.PlaySplash(origin + new Vector3(-i * 0.5f, 0f, 0f));
                if (i % 6 == 0) yield return null;
            }

            Assert.LessOrEqual(vfx.ActiveEffects, vfx.PoolCapacity,
                "افکتِ فعال نباید از ظرفیتِ استخر بیشتر شود (وگرنه Instantiate در هر ضربه داریم)");
            Assert.GreaterOrEqual(vfx.PooledEffects, 0);
            Assert.GreaterOrEqual(vfx.PlayedCount, 0);
            StringAssert.Contains("vfx", director.Report(), "گزارشِ مدیرِ گرافیک باید خطِ افکت‌ها را داشته باشد");
        }

        [UnityTest]
        public IEnumerator MaterialCache_DoesNotGrowWithRepeatedRequests()
        {
            MaterialLibrary.ResetCache();
            yield return null;

            Material first = MaterialLibrary.Surface(MaterialLibrary.SurfaceStyle.Ground, new Color(0.3f, 0.4f, 0.25f), 0f);
            int afterFirst = MaterialLibrary.CachedMaterialCount;
            for (int i = 0; i < 200; i++)
            {
                Material again = MaterialLibrary.Surface(MaterialLibrary.SurfaceStyle.Ground, new Color(0.3f, 0.4f, 0.25f), 0f);
                Assert.AreSame(first, again, "همان تینت باید همان متریالِ کش‌شده را بدهد");
            }
            Assert.AreEqual(afterFirst, MaterialLibrary.CachedMaterialCount, "۲۰۰ درخواست نباید ۲۰۰ متریال بسازد");
            Assert.IsNotNull(first);

            // و با تینتِ متفاوت فقط یک متریالِ تازه (کنترل‌شده) ساخته می‌شود
            MaterialLibrary.Surface(MaterialLibrary.SurfaceStyle.Ground, new Color(0.31f, 0.4f, 0.25f), 0f);
            Assert.LessOrEqual(MaterialLibrary.CachedMaterialCount, afterFirst + 2);
        }
    }
}
