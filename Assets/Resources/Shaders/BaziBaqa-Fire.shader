// آتش/دود/جرقه‌ی فاز ۳ (گام ۴). یک passِ CGPROGRAM/UnityCG ⇒ زیرِ URP و Built-in هر دو
// کامپایل می‌شود (ذرات به نور/ShadowCaster نیاز ندارند) و هیچ بافتِ خارجی نمی‌خواهد:
// شعله از دو لایه‌ی FBMِ جابه‌جا رویِ uvِ سازه‌ی ذره ساخته می‌شود.
// نامِ Hidden ⇒ نه در منویِ Shader و نه در Fallbackها گم می‌شود؛ MaterialLibrary آن را حل می‌کند.
Shader "Hidden/BaziBaqa/Fire"
{
    Properties
    {
        _BaziFireHot("Hot Color", Color) = (1.0, 0.86, 0.42, 1)
        _BaziFireCold("Cold Color", Color) = (0.86, 0.24, 0.05, 1)
        _BaziFireSmoke("Smoke Color", Color) = (0.16, 0.15, 0.15, 1)
        _BaziFireParams("Params (speed, turbulence, height falloff, intensity)", Vector) = (1.1, 0.65, 1.45, 3.2)
        _BaziFireMode("Mode (0 fire, 1 smoke, 2 spark)", Range(0, 2)) = 0
        [Toggle] _BaziFireWind("Use Global Wind", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True"
               "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }

        Cull Off ZWrite Off ZTest LEqual Fog { Mode Off }
        Blend SrcAlpha One

        Pass
        {
            Name "BaziBaqaFire"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            // ثابتِ جهانیِ باد: این فایل BaziBaqaCore.cginc را include نمی‌کند (سبک‌تر و
            // بی‌نیاز از هیچ وابستگی)، پس خودِ declaration همین‌جا نوشته می‌شود.
            float4  _BaziWindState;

            fixed4  _BaziFireHot;
            fixed4  _BaziFireCold;
            fixed4  _BaziFireSmoke;
            float4  _BaziFireParams;
            float   _BaziFireMode;
            float   _BaziFireWind;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float4 tint : COLOR1;
                float  up   : TEXCOORD1;
                float  gust : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.tint = v.color;
                o.up = v.uv.y;
                // بادِ جهانی (همان برداری که درخت‌ها می‌خوانند) لبه‌ی شعله را به جانب می‌بَرَد
                float gust = _BaziWindState.y * saturate(_BaziFireWind);
                o.gust = gust * (0.45 + 0.55 * v.uv.y);
                return o;
            }

            float BaziFireHash(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            float BaziFireNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = BaziFireHash(i);
                float b = BaziFireHash(i + float2(1, 0));
                float c = BaziFireHash(i + float2(0, 1));
                float d = BaziFireHash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float BaziFireFbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                [unroll]
                for (int o = 0; o < 4; ++o)
                {
                    sum += BaziFireNoise(p) * amp;
                    p = mul(p, float2x2(1.72, -1.26, 1.26, 1.72)) + float2(1.7, -3.1);
                    amp *= 0.5;
                }
                return sum;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * max(0.05, _BaziFireParams.x);
                float turb = _BaziFireParams.y;
                float falloff = max(0.05, _BaziFireParams.z);
                float intensity = max(0.05, _BaziFireParams.w);
                float mode = _BaziFireMode;

                // لبه‌ها را با نویز می‌لرزاند؛ هرچه بالاتر، بیشتر (زبانِ آتش)
                float2 warp = i.uv + float2(t * 0.31, -t * 0.86);
                float distort = (BaziFireFbm(warp * 2.4 + float2(i.gust * 1.6, 0.0)) - 0.5) * turb
                              * lerp(0.35, 1.35, i.up);
                float2 auv = float2(i.uv.x + distort, i.uv.y - t * 0.55);

                float n = BaziFireFbm(auv * float2(2.6, 1.9));
                float taper = saturate(1.0 - i.up * 0.86);
                float body = saturate(n * 1.55 - pow(i.up, falloff) * 0.92 - 0.06) * taper;

                fixed4 col;
                if (mode > 1.5)
                {
                    // جرقه: فقط هسته، بدونِ بدنه
                    float core = pow(saturate(1.0 - length(i.uv - 0.5) * 2.0), 2.2);
                    col = fixed4(_BaziFireHot.rgb * (core * 4.0), core * i.tint.a);
                }
                else if (mode > 0.5)
                {
                    // دود: نرم، کم‌رنگ، با افتِ شفافیت در ارتفاع
                    float smoke = saturate(n * 1.15 - 0.28) * (1.0 - i.up * 0.72);
                    col = fixed4(_BaziFireSmoke.rgb, smoke * 0.55 * i.tint.a);
                }
                else
                {
                    float heat = saturate(body * 2.1) * (1.0 - i.up * 0.55);
                    fixed3 fire = lerp(_BaziFireCold.rgb, _BaziFireHot.rgb, pow(heat, 0.62));
                    fire += _BaziFireHot.rgb * pow(saturate(heat * 1.2 - 0.4), 3.0) * 1.6;   // هسته‌ی سفید
                    col = fixed4(fire * intensity, saturate(body * 1.9) * i.tint.a);
                }
                return col * i.tint;
            }
            ENDCG
        }
    }
}
