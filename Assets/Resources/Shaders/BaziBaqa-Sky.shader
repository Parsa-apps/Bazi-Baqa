// آسمانِ رویه‌ایِ فاز ۳ (نورپردازی سینمایی). بی‌نیاز از هر فایلی جز خودِ شیدر:
// گرادیانِ افق، دیسکِ خورشید/ماه، ستاره‌ها، ابرهای لایه‌ای و غبارِ افق.
//
// چرا material روی RenderSettings.skybox و نه یک مشِ Dome؟ یونیتی هندسه‌ی آسمان را خودش
// می‌کِشَد و همین مسیر برای دوربینِ ارتوفونِ ایزومتریکِ بازی هم درست کار می‌کند؛ ما فقط
// «جهتِ دید» را از خودِ positionِ صفحه‌بردار می‌سازیم (نه از uv/normal) تا با هر
// projection ( perspective/ortho ) و هر aspect درست بماند.
//
// مسیرِ پشتیبان: اگر کامپایل نشود، SkyLightingRig دوربین را روی SolidColor می‌گذارد و
// همان رنگِ افق را می‌دهد ⇒ هیچ‌وقت آسمانِ سیاه/ارغوانی نمی‌بینیم.
Shader "Hidden/BaziBaqa/Sky"
{
    Properties
    {
        _BaziSkyZenith("Zenith", Color) = (0.16, 0.36, 0.62, 1)
        _BaziSkyHorizon("Horizon", Color) = (0.62, 0.66, 0.68, 1)
        _BaziSkyGround("Ground Haze", Color) = (0.20, 0.22, 0.22, 1)
        _BaziSkySunColor("Sun Color", Color) = (1.0, 0.86, 0.66, 1)
        _BaziSkySunDir("Sun Direction", Vector) = (0.3, 0.6, 0.4, 0)
        _BaziSkySunSize("Sun Size (cos threshold)", Range(0.9, 0.9999)) = 0.9985
        _BaziSkyNight("Night Amount", Range(0, 1)) = 0
        _BaziSkyCloud("Cloud Amount", Range(0, 1)) = 0.25
        _BaziSkyCloudLevel("Cloud Level", Range(0.02, 0.9)) = 0.22
        _BaziSkyDust("Dust", Range(0, 1)) = 0
        _BaziSkyExposure("Exposure", Range(0.1, 4)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox"
               "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }

        Cull Off ZWrite Off ZTest Always Fog { Mode Off }

        Pass
        {
            Name "BaziBaqaSky"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _BaziSkyZenith;
            fixed4 _BaziSkyHorizon;
            fixed4 _BaziSkyGround;
            fixed4 _BaziSkySunColor;
            float4  _BaziSkySunDir;
            half    _BaziSkySunSize;
            half    _BaziSkyNight;
            half    _BaziSkyCloud;
            half    _BaziSkyCloudLevel;
            half    _BaziSkyDust;
            half    _BaziSkyExposure;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float3 ray   : TEXCOORD0;   // جهتِ جهان به سمتِ این پیکسل
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 posCS = UnityObjectToClipPos(v.vertex);
                o.pos = posCS;
                // نقطه‌ای روی صفحه‌ی دور (برای ortho هم درست: xy/w در [-1,1]) و سپس به فضای دید/جهان
                float3 ndc = float3(posCS.xy / max(abs(posCS.w), 1e-5), 1.0);
                // در فضای دید چشم همیشه در مبدأ است؛ پس برای perspective و ortho یک فرمول درست است
                float3 farVS = mul(unity_MatrixInvP, float4(ndc, 1.0)).xyz;
                float3 dirVS = normalize(farVS);
                o.ray = mul((float3x3)unity_MatrixInvV, dirVS);
                return o;
            }

            // نویزِ مقدارِ رویه‌ایِ همانِ BaziBaqaCore.hlsl (این‌جا include نمی‌شود چون
            // آن فایل داخل CBUFFER/URP نوشته شده و این pass با UnityCG کار می‌کند).
            float BaziSkyHash(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            float BaziSkyNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = BaziSkyHash(i);
                float b = BaziSkyHash(i + float2(1, 0));
                float c = BaziSkyHash(i + float2(0, 1));
                float d = BaziSkyHash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float BaziSkyFbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                [unroll]
                for (int o = 0; o < 4; ++o)
                {
                    sum += BaziSkyNoise(p) * amp;
                    p = mul(p, float2x2(1.62, -1.18, 1.18, 1.62)) + float2(3.1, -1.7);
                    amp *= 0.5;
                }
                return sum;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.ray);
                float up = dir.y;

                // گرادیانِ عمودی: افق ← رأس، و زیرِ افق مه‌ِ زمین
                float tUp = saturate(up * 2.0 + 0.06);
                fixed3 sky = lerp(_BaziSkyHorizon.rgb, _BaziSkyZenith.rgb, pow(tUp, 0.62));
                float under = saturate(-up * 6.0);
                sky = lerp(sky, _BaziSkyGround.rgb, under);

                // خورشید/ماه: یک دیسک + هاله؛ در شب همان دیسک ماه می‌شود (سردتر و کوچک‌تر)
                float3 sunDir = normalize(_BaziSkySunDir.xyz);
                float cosA = dot(dir, sunDir);
                half sizeBias = lerp(_BaziSkySunSize, _BaziSkySunSize * 0.86 + 0.13, _BaziSkyNight);
                float disc = smoothstep(sizeBias, sizeBias + 0.0016, cosA);
                float glow = pow(saturate(cosA), lerp(180.0, 46.0, _BaziSkyNight));
                float occluded = 1.0 - saturate(_BaziSkyCloud * 1.15);
                fixed3 discColor = lerp(_BaziSkySunColor.rgb, fixed3(0.86, 0.9, 1.0), _BaziSkyNight);
                sky += discColor * disc * lerp(6.0, 2.4, _BaziSkyNight) * occluded;
                sky += discColor * glow * lerp(0.75, 0.22, _BaziSkyNight) * saturate(0.35 - under * 2.0);

                // ستاره‌ها: شبکه‌ی سلولی + سوسویِ زمانی؛ فقط شب و فقط بالایِ افق
                float starField = 0.0;
                if (_BaziSkyNight > 0.02)
                {
                    float2 suv = dir.xz / max(abs(dir.y) + 0.28, 0.05) * 7.5;
                    float2 si = floor(suv * 3.0);
                    float star = step(0.9955, BaziSkyHash(si));
                    float twinkle = 0.55 + 0.45 * sin(_Time.y * 2.1 + BaziSkyHash(si + 7.3) * 24.0);
                    starField = star * twinkle * saturate(up * 3.0)
                              * (1.0 - saturate(_BaziSkyCloud * 1.4)) * _BaziSkyNight;
                }
                sky += fixed3(0.85, 0.9, 1.0) * starField * 1.6;

                // ابرها: دو فرودِ fbm روی صفحه‌ی افقیِ مجازی؛ لبه‌ها با نورِ پشتِ ابر روشن می‌شوند
                if (_BaziSkyCloud > 0.01 && up > 0.004)
                {
                    float2 cuv = dir.xz / max(up, 0.06) * 0.32;
                    float drift = _Time.y * 0.010;
                    float c = BaziSkyFbm(cuv + float2(drift, drift * 0.31));
                    float c2 = BaziSkyFbm(cuv * 2.7 + float2(-drift * 1.7, drift * 0.6));
                    float cover = saturate((c * 0.75 + c2 * 0.35) * lerp(1.9, 2.9, _BaziSkyCloud)
                                          - lerp(1.05, 0.55, saturate(_BaziSkyCloud)));
                    cover *= saturate(up * 6.0) * saturate(1.15 - up * 0.9);
                    float lit = saturate(cosA * 0.5 + 0.5);
                    fixed3 cloudLow = lerp(fixed3(0.62, 0.66, 0.70), fixed3(0.10, 0.12, 0.17), _BaziSkyNight);
                    fixed3 cloudHigh = lerp(fixed3(1.0, 0.98, 0.94), fixed3(0.34, 0.40, 0.52), _BaziSkyNight);
                    cloudHigh = lerp(cloudHigh, _BaziSkySunColor.rgb, saturate(lit * lit * (1.0 - _BaziSkyNight)));
                    sky = lerp(sky, lerp(cloudLow, cloudHigh, lit), cover * 0.88);
                }

                // غبارِ افق (باد/خشکی) + نورِ سطحیِ شب ⇒ رنگی که با مه‌ِ صحنه هم‌خانواده است
                float horizonBand = pow(1.0 - saturate(abs(up) * 2.4), 3.0);
                sky = lerp(sky, lerp(sky, fixed3(0.66, 0.56, 0.42), 0.7), horizonBand * _BaziSkyDust);
                sky = lerp(sky, sky * 0.35 + fixed3(0.03, 0.05, 0.09), _BaziSkyNight * 0.25);

                return fixed4(max(sky, 0.0) * _BaziSkyExposure, 1.0);
            }
            ENDCG
        }
    }
}
