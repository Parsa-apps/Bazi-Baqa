using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BaziBaqa.Tests
{
    /// <summary>
    /// تست‌هایِ زنده‌ی لایه‌ی گرافیک: آیا واقعاً در PlayMode متریال‌ها حل می‌شوند، Volume
    /// ساخته می‌شود، تغییرِ کیفیت اثر می‌گذارد و بازتولیدِ جهان متریالِ تازه نمی‌سازد.
    /// هیچ ادعایی روی متنِ کنسول نمی‌شود (یونیتی هر error را خودش شکست می‌شمارد) و همه‌ی
    /// شاخه‌ها هم در حالت URP و هم Built-in پاس می‌شوند؛ چون نصبِ URP Asset یک انتخابِ کاربر است.
    /// </summary>
    public class GraphicsPlayModeTests
    {
        [UnityTest]
        public IEnumerator GraphicsDirector_InstallsItselfAndReportsPipeline()
        {
            GraphicsDirector director = GraphicsDirector.Ensure();
            Assert.IsNotNull(director, "مدیرِ گرافیک نصب نشد");
            yield return null;

            string report = director.Report();
            Assert.IsFalse(string.IsNullOrEmpty(report), "گزارشِ وضعیت نباید خالی باشد");
            StringAssert.Contains("pipeline=", report);
            StringAssert.Contains("surface=", report);
            Debug.Log("GraphicsPlayMode: " + report);
        }

        [UnityTest]
        public IEnumerator VolumeStack_ActiveUnderUrp_OrCleanlySkipped()
        {
            GraphicsDirector director = GraphicsDirector.Ensure();
            yield return null;

            if (MaterialLibrary.IsUniversal)
            {
                Assert.IsNotNull(director.Volume, "با URP فعال، Rigِ حجم باید ساخته شود");
                Assert.IsTrue(director.Volume.IsActive, "استکِ سینمایی فعال نیست (Volume/Profile ساخته نشده)");
                Assert.IsNotNull(director.Volume.Profile.components, "پروفایلِ حجم بدون مؤلفه است");
            }
            else
            {
                // حالتِ پشتیبان: هیچ Volume ای نباید ساخته شود (کامپوننتِ بی‌مصرف روی Canvas صحنه)
                Assert.IsTrue(director.Volume == null || !director.Volume.IsActive,
                    "بی URP نباید Volume فعال باشد");
                Debug.Log("GraphicsPlayMode: URP فعال نیست؛ مسیرِ پشتیبانِ Built-in بررسی شد.");
            }
        }

        [UnityTest]
        public IEnumerator Materials_NeverResolveToErrorShader()
        {
            GraphicsProfile profile = GraphicsProfile.Load();
            Assert.IsNotNull(profile);

            Material tinted = MaterialLibrary.Tinted(new Color(0.4f, 0.5f, 0.6f), 0.2f);
            Material ground = MaterialLibrary.Surface(GroundStyle(), new Color(0.3f, 0.4f, 0.25f), 0f);
            Material emissive = MaterialLibrary.Emissive(new Color(1f, 0.6f, 0.2f), 2.4f);
            Material particle = MaterialLibrary.Particle(new Color(1f, 0.4f, 0.1f, 0.9f), true, false);

            yield return null;

            Shader[] shaders = { tinted != null ? tinted.shader : null, ground != null ? ground.shader : null,
                                 emissive != null ? emissive.shader : null, particle != null ? particle.shader : null };
            string[] labels = { "tinted", "ground", "emissive", "particle" };
            for (int i = 0; i < shaders.Length; i++)
            {
                Assert.IsNotNull(shaders[i], "متریال «" + labels[i] + "» شیدر ندارد ⇒ در صحنه ارغوانی می‌شود");
                Assert.AreNotEqual("Hidden/InternalErrorShader", shaders[i].name,
                    "متریال «" + labels[i] + "» به شیدرِ خطا افتاد: " + shaders[i].name);
            }
            Assert.Greater(tinted.renderQueue, 0, "renderQueue ست نشده");
            Assert.GreaterOrEqual(particle.renderQueue, 3000, "ذرات باید در Transparent رندر شوند");
            Assert.LessOrEqual(particle.renderQueue, 3200, "queueِ ذرات نباید وارد Overlay شود (روی UI می‌افتد)");
        }

        [UnityTest]
        public IEnumerator WorldRebuild_ReusesMaterialCache()
        {
            GameManager game = GameManager.Instance;
            Assert.IsNotNull(game, "این تست با صحنه‌ی Main اجرا می‌شود");
            Assert.IsNotNull(game.World, "تولیدکننده‌ی جهان نصب نیست");

            game.World.Generate(4242);
            yield return null;
            yield return null;
            int afterFirst = MaterialLibrary.CachedMaterialCount;
            Assert.Greater(afterFirst, 0, "جهان بدون متریالِ کش‌شده ساخته نشد");

            game.World.Generate(9901);
            yield return null;
            yield return null;
            int afterSecond = MaterialLibrary.CachedMaterialCount;
            Assert.LessOrEqual(afterSecond, afterFirst,
                "بازتولیدِ جهان متریالِ تازه ساخت (نشتیِ حافظه روی موبایل)؛ کش باید ضربه‌گیر باشد");

            Renderer[] renderers = game.World.TerrainRoot.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0, "زمین/آب رندرر ندارد");
            for (int i = 0; i < renderers.Length; i++)
            {
                Assert.IsNotNull(renderers[i].sharedMaterial, "رندرر بدون متریال: " + renderers[i].name);
                Assert.IsNotNull(renderers[i].sharedMaterial.shader, "متریال بدون شیدر: " + renderers[i].sharedMaterial.name);
            }
        }

        [UnityTest]
        public IEnumerator QualityChange_ReappliesGraphicsTier()
        {
            GraphicsDirector director = GraphicsDirector.Ensure();
            int before = RenderPipelineBridge.ApplyCount;
            int original = QualitySettings.GetQualityLevel();
            try
            {
                int target = original == 0 ? Mathf.Min(1, QualitySettings.names.Length - 1) : 0;
                QualitySettings.SetQualityLevel(target, true);
                yield return null;
                yield return null;

                Assert.AreEqual(target, RenderPipelineBridge.LastAppliedQuality,
                    "مدیرِ گرافیک تغییرِ کیفیت را ندید ⇒ تنظیماتِ بصری کهنه می‌ماند");
                Assert.Greater(RenderPipelineBridge.ApplyCount, before, "اعمالِ مجدد انجام نشد");
            }
            finally
            {
                QualitySettings.SetQualityLevel(original, true);
            }
        }

        [UnityTest]
        public IEnumerator WeatherAndTime_ChangeShaderGlobalsWithoutError()
        {
            GameManager game = GameManager.Instance;
            Assert.IsNotNull(game);
            WeatherSystem weather = game.Weather;
            Assert.IsNotNull(weather, "سامانه‌ی هوا در GameManager نیست");

            float ambientBefore = RenderSettings.ambientLight.grayscale;
            weather.SetWeather(WeatherType.Rain, false);
            yield return null;

            Assert.AreEqual(WeatherType.Rain, weather.Current, "اگر هوا عوض نشود، مه و خیسی هم درست نمی‌شود");
            Vector4 atmosphere = MaterialLibrary.AtmosphereState;
            Assert.GreaterOrEqual(atmosphere.z, 0f, "رطوبتِ جهانی نباید منفی باشد");
            Assert.LessOrEqual(atmosphere.z, 1f, "رطوبتِ جهانی نرمال‌سازی نشده");
            Debug.Log("GraphicsPlayMode: ambient before=" + ambientBefore.ToString("F3") + " atmosphere=" + atmosphere);

            weather.SetWeather(WeatherType.Clear, false);
            game.Clock.Advance(60f);   // شب ⇒ درخششِ پنجره‌ها و فانوس‌ها باید بالا برود
            yield return null;
            Assert.GreaterOrEqual(game.Clock.NormalizedTime, 0f);
            Assert.LessOrEqual(game.Clock.NormalizedTime, 1f);
        }


        // ============================== گام ۲: نورپردازیِ سینمایی ==============================

        private static void Settle(SkyLightingRig rig, int steps)
        {
            for (int i = 0; i < steps; i++) rig.ApplyNow();
        }

        [UnityTest]
        public IEnumerator SkyRig_NoonAndMidnight_FeelDifferent()
        {
            GraphicsDirector.Ensure();
            yield return null;
            SkyLightingRig rig = SkyLightingRig.Instance;
            Assert.IsNotNull(rig, "مدیرِ گرافیک باید SkyLightingRig را نصب کند");

            GameClock clock = GameManager.Instance != null ? GameManager.Instance.Clock : null;
            if (clock == null)
            {
                // بیرون از بازی هم Rig کار می‌کند (زمانِ موتور)؛ فقط مقایسه‌ی دقیق لازم دارد
                Assert.IsNotNull(rig.Report(), "گزارشِ Rig نباید null باشد");
                Assert.Pass("SkyLightingRig بدون GameManager هم فعال است؛ مقایسه‌ی روز/شب انجام نشد");
            }

            clock.Initialize(3, 0.5f);
            Settle(rig, 40);
            float noonNight = rig.NightAmount;
            float noonFog = rig.FogDensity;
            Color noonAmbient = RenderSettings.ambientSkyColor;
            float noonSun = RenderSettings.sun != null ? RenderSettings.sun.intensity : 0f;

            clock.Initialize(3, 0.01f);
            Settle(rig, 40);
            float nightNight = rig.NightAmount;
            float nightFog = rig.FogDensity;
            Color nightAmbient = RenderSettings.ambientSkyColor;
            float nightSun = RenderSettings.sun != null ? RenderSettings.sun.intensity : 0f;

            Assert.Greater(nightNight, noonNight + 0.4f, "شب باید از ظهر تاریک‌تر باشد");
            Assert.Greater(noonSun, nightSun, "نورِ خورشید در ظهر باید بیشتر از ماه باشد");
            Assert.Greater(noonAmbient.grayscale, nightAmbient.grayscale, "نورِ محیطیِ ظهر باید روشن‌تر باشد");
            Assert.Greater(nightFog, noonFog, "مه‌ِ شب غلیظ‌تر از ظهر است (حسِ سینمایی)");
            Assert.AreEqual(UnityEngine.Rendering.AmbientMode.Trilight, RenderSettings.ambientMode,
                "ambient باید Trilight باشد تا آسمان/افق/زمین رنگ‌هایِ جدا داشته باشند");
            Assert.IsTrue(RenderSettings.fog, "مه‌ی صحنه روشن است تا آسمان به زمین برسد");
            Assert.GreaterOrEqual(RenderSettings.fogDensity, 0f);
            Assert.LessOrEqual(RenderSettings.fogDensity, 0.12f, "چگالیِ مه باید به‌اندازه‌ی clampِ خودِ Rig بماند");

            // آسمان: یا شیدرِ رویه‌ای، یا رنگِ هم‌خانواده‌ی افق — هیچ‌وقت خالی/ارغوانی نه
            if (rig.UsesProceduralSky)
            {
                Assert.IsNotNull(RenderSettings.skybox, "اگر UsesProceduralSky است، متریالِ آسمان باید وصل باشد");
                Assert.AreEqual(UnityEngine.CameraClearFlags.Skybox, Camera.main != null ? Camera.main.clearFlags : UnityEngine.CameraClearFlags.Skybox);
            }
            else
            {
                Assert.IsNull(RenderSettings.skybox, "بدونِ شیدرِ آسمان نباید متریالِ شکسته‌ای وصل بماند");
                Assert.AreNotEqual(UnityEngine.CameraClearFlags.Skybox, Camera.main != null ? Camera.main.clearFlags : UnityEngine.CameraClearFlags.SolidColor,
                    "در حالتِ پشتیبان، پس‌زمینه SolidColor است");
            }

            // جهتِ خورشید باید واحد باشد (شیدرها با آن دیسک و هاله را می‌چرخانند)
            Assert.GreaterOrEqual(rig.SunDirection.sqrMagnitude, 0.98f);
            Assert.LessOrEqual(rig.SunDirection.sqrMagnitude, 1.02f);
            Debug.Log("GraphicsPlayMode: " + rig.Report());

            clock.Initialize(3, 0.5f);
            Settle(rig, 12);
        }

        [UnityTest]
        public IEnumerator SkyRig_WeatherResponseDimsSunAndThickensFog()
        {
            GraphicsDirector.Ensure();
            yield return null;
            SkyLightingRig rig = SkyLightingRig.Instance;
            Assert.IsNotNull(rig);
            GameManager game = GameManager.Instance;

            if (game != null && game.Weather != null)
            {
                if (game.Clock != null) game.Clock.Initialize(4, 0.5f);
                game.Weather.SetWeather(WeatherType.Clear, false);
                Settle(rig, 30);
                float clearFog = rig.FogDensity;
                float clearSun = RenderSettings.sun != null ? RenderSettings.sun.intensity : 0f;

                game.Weather.SetWeather(WeatherType.Storm, false);
                Settle(rig, 30);
                Assert.AreEqual(WeatherType.Storm, rig.Weather, "Rig باید از تغییرِ هوا باخبر شود");
                Assert.Greater(rig.FogDensity, clearFog, "طوفان مه را غلیظ‌تر می‌کند");
                Assert.LessOrEqual(RenderSettings.sun != null ? RenderSettings.sun.intensity : clearSun, clearSun,
                    "طوفان نورِ خورشید را کم می‌کند");
                Vector4 heightFog = MaterialLibrary.GetGlobalVector("_BaziHeightFog");
                Assert.GreaterOrEqual(heightFog.y, 0f, "خیسیِ جهانی در محدوده است");
                game.Weather.SetWeather(WeatherType.Clear, false);
                Settle(rig, 20);
            }
            else
            {
                rig.NotifyWeather(WeatherType.Rain, 0.5f);
                Settle(rig, 10);
                Assert.AreEqual(WeatherType.Rain, rig.Weather);
            }
        }

        [UnityTest]
        public IEnumerator SkyRig_LampBudgetIsRespectedAtNight()
        {
            GraphicsDirector director = GraphicsDirector.Ensure();
            yield return null;
            SkyLightingRig rig = SkyLightingRig.Instance;
            Assert.IsNotNull(rig);

            GameManager game = GameManager.Instance;
            if (game != null && game.Clock != null) game.Clock.Initialize(2, 0.02f);
            for (int i = 0; i < 20; i++)
            {
                rig.ApplyNow();
                yield return null;      // یک‌جا با زمانِ واقعی هم جلو می‌رویم تا بودجه بازسازی شود
            }

            int budget = GraphicsProfile.Load().Current.lampBudget;
            Assert.GreaterOrEqual(rig.LampsActive, 0);
            Assert.LessOrEqual(rig.LampsActive, budget,
                "چراغ‌هایِ روشن نباید از بودجه‌ی سطحِ کیفیت بیشتر شوند (هزینه‌ی نورِ اضافه در URP)");
            Assert.LessOrEqual(rig.LampCount, 32, "فهرستِ چراغ باید سقف داشته باشد تا allocation نکند");
            Assert.IsNotNull(director.Sky, "مدیرِ گرافیک باید خودِ Rig را هم نگه دارد");
        }

        private static MaterialLibrary.SurfaceStyle GroundStyle()
        {
            return MaterialLibrary.SurfaceStyle.Ground;
        }
    }
}
