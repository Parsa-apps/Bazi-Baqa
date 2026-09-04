// شیشه‌ایِ مدرِ رابط (فاز ۳، گام ۶). بی‌نیاز از هر Asset: صافِ مات با نوارِ نور، لبه‌ی
// نئونی و گوشه‌ی گرد، همه رویه‌ای.
//
// چرا CGPROGRAM و UI/Default؟ UGUI متریال‌ها را بیرون از خطِ لوله‌ی URP می‌فرستد (Canvas
// Renderer) و قراردادِ کلیپِ مستطیلی (UNITY_UI_CLIP_RECT) در هر دو خطِ رندر یکی است؛
// همان چیزی که RectMask2D و اسکرول‌لیست‌هایِ ما لازم دارد.
//
// چرا یک متریالِ مشترک برای همه‌ی پنل‌ها؟ هر متریالِ تازه یعنی یک batch جدا روی موبایل؛
// پس رنگِ هر پنل از Image.color (رنگِ رأس) می‌آید و فقط جاروبِ نور سراسری است.
Shader "Hidden/BaziBaqa/UIGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaziGlassParams ("Glass (frost, edgeWidth, cornerRadius, cornerNorm)", Vector) = (0.35, 0.02, 0.06, 0.08)
        _BaziGlassTint ("Glass Tint", Color) = (0.12, 0.34, 0.42, 1)
        _BaziGlassEdge ("Edge Light (rgb, strength)", Color) = (0.55, 0.95, 1, 0.85)
        _BaziGlassSweep ("Sweep (phase, width, tilt, amount)", Vector) = (0, 0.16, 0.42, 0.35)
        _BaziGlassNoise ("Noise (scale, strength, gloss, _)", Vector) = (12, 0.05, 0.25, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB

        Pass
        {
            Name "BaziBaqaUIGlass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ UNITY_UI_CLIP_RECT
            #pragma multi_compile _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _BaziGlassParams;
            fixed4 _BaziGlassTint;
            fixed4 _BaziGlassEdge;
            float4 _BaziGlassSweep;
            float4 _BaziGlassNoise;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 local : TEXCOORD2;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                // مختصاتِ محلیِ تصویرِ UI بین ۰ و ۱ است؛ گوشه‌ی گرد و جاروب از همین‌جا می‌آیند
                o.local = v.texcoord;
                o.color = v.color;
                return o;
            }

            float BaziHash12 (float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // فاصله‌ی signed از مستطیلِ گرد؛ نصفِ اندازه در واحدِ ارتفاعِ خودِ پنل
            float BaziRoundSdf (float2 uv, float radius)
            {
                float2 half_ = 0.5 - radius;
                float2 d = abs(uv - 0.5) - half_;
                return min(max(d.x, d.y), 0.0) + max(length(max(d, 0.0)), 0.0) - radius;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tint = i.color;
                fixed4 baseTex = tex2D(_MainTex, i.uv);

                float radius = saturate(_BaziGlassParams.z) * 0.49;
                float sdf = BaziRoundSdf(i.local, radius);
                // ۱) برشِ گوشه‌ها: هر چه از مرکز دورتر، محوِ یک‌پیکسلی (آنتی‌آلیس با fwidth)
                float aa = max(fwidth(sdf), 0.0005);
                float shape = saturate(0.5 - sdf / aa) * baseTex.a;

                // ۲) شیشه: کمی شیری‌بودنِ عمودی + noiseِ ریزِ مات (بی‌بافت، بی‌حافظه)
                float frost = BaziHash12(floor(i.local * _BaziGlassNoise.x * 64.0)) - 0.5;
                float gloss = smoothstep(0.0, 1.0, 1.0 - i.local.y) * _BaziGlassNoise.z;

                // ۳) لبه‌ی نئونی: باریکه‌ای درست داخلِ مرز (edgeWidth با واحدِ ارتفاع)
                float edgeWidth = max(_BaziGlassParams.y, 0.001);
                float edge = saturate(1.0 - abs(sdf + _BaziGlassParams.w) / edgeWidth);

                // ۴) جاروبِ نور: نواری مورب که با فازِ سراسری روی پنل لغزش می‌کند
                float diagonal = i.local.x + i.local.y * _BaziGlassSweep.z;
                float wrapped = frac(diagonal - _BaziGlassSweep.x);
                float halfWidth = max(_BaziGlassSweep.y * 0.5, 0.001);
                float sweep = saturate(1.0 - abs(wrapped - 0.5) / halfWidth);
                sweep *= sweep;

                fixed3 glass = lerp(_BaziGlassTint.rgb, fixed3(1.0, 1.0, 1.0), frost * _BaziGlassNoise.y + gloss * 0.5);
                fixed3 col = baseTex.rgb * tint.rgb * glass;
                col += _BaziGlassEdge.rgb * (_BaziGlassEdge.a * edge);
                col += _BaziGlassEdge.rgb * (sweep * _BaziGlassSweep.w);
                float alpha = tint.a * saturate(0.72 + edge * 0.28 + sweep * 0.3) * shape;

                #ifdef UNITY_UI_CLIP_RECT
                tint.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif

                fixed4 outColor = fixed4(col, saturate(alpha));
                #ifdef UNITY_UI_ALPHACLIP
                clip (outColor.a - 0.001);
                #endif
                return outColor;
            }
            ENDCG
        }
    }

    // اگر URP نصب نبود یا کامپایل نشد، همان تصویرِ سادهٔ UI می‌ماند ⇒ پنل هرگز
    // ارغوانی/نامرئی نمی‌شود (فقط جلوه‌ی شیشه‌ای کم می‌شود).
    Fallback "UI/Default"
}
