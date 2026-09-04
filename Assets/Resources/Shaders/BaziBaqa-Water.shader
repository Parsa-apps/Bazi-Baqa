// آبِ فاز ۳ (گام ۴): موجِ دو-سینوسی، شکستِ نورِ ارزان، بازتابِ آسمان با fresnel،
// کف/رقصِ سطح با نویزِ رویه‌ای و پاسخ به باران. هیچ RenderTexture و هیچ Reflection Probe
// لازم ندارد (رویِ اندروید میان‌رده گران است) و عمقِ آب را از ارتفاعِ خودِ رأس‌ها می‌گیرد.
//
// مسیرِ پشتیبان: زیرِ Built-in، `Fallback "BaziBaqa/Surface"` مسئول است؛ آن شیدر هر دو
// خطِ رندر را دارد، پس آبِ بدونِ موج بهتر از آبِ ارغوانی است.
Shader "BaziBaqa/Water"
{
    Properties
    {
        _BaziWaterDeep("Deep Color", Color) = (0.02, 0.10, 0.18, 1)
        _BaziWaterShallow("Shallow Color", Color) = (0.10, 0.34, 0.40, 1)
        _BaziWaterSky("Sky Tint", Color) = (0.42, 0.56, 0.68, 1)
        _BaziWaterWave("Wave (speed, amp, steepness, foam)", Vector) = (0.55, 0.09, 1.6, 0.35)
        _BaziWaterScroll("Detail Scroll (x, y, rain scale, sparkle)", Vector) = (0.03, -0.021, 1, 0.6)
        [Normal] _BaziWaterDetail("Detail Normal", 2D) = "bump" {}
        _BaziWaterSmoothness("Smoothness", Range(0.05, 1)) = 0.82
        [Enum(UnityEngine.Rendering.CullMode, 2)] _BaziCull("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-1" "RenderType" = "Transparent" "IgnoreProjector" = "True"
               "DisableBatching" = "True" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "BaziBaqaWaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_BaziCull]
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BaziBaqaCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaziWaterDeep;
                float4 _BaziWaterShallow;
                float4 _BaziWaterSky;
                float4 _BaziWaterWave;
                float4 _BaziWaterScroll;
                float4 _BaziWaterDetail_ST;
                float  _BaziWaterSmoothness;
                float  _BaziCull;
            CBUFFER_END

            TEXTURE2D(_BaziWaterDetail);  SAMPLER(sampler_BaziWaterDetail);

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 uv          : TEXCOORD1;
                float  waveHeight  : TEXCOORD2;
                float3 normalWS    : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
            };

            // موج = جمعِ دو سینوس با جهاتِ متفاوت؛ شیبِ تحلیلی ⇒ نرمالِ بی‌نقص بدونِ normal map
            float3 BaziWaterSurface(float3 positionWS, float time, out float3 normalWS, out float height)
            {
                float speed = _BaziWaterWave.x;
                float amp = _BaziWaterWave.y;
                float steep = max(0.05, _BaziWaterWave.z);
                float2 dirA = normalize(float2(1.0, 0.32));
                float2 dirB = normalize(float2(-0.44, 1.0));
                float phaseA = dot(positionWS.xz, dirA) * steep + time * speed;
                float phaseB = dot(positionWS.xz, dirB) * steep * 1.7 - time * speed * 1.31;
                float slopeA = cos(phaseA) * amp * steep;
                float slopeB = cos(phaseB) * amp * steep * 1.7;
                height = sin(phaseA) * amp + sin(phaseB) * amp * 0.62;
                normalWS = normalize(float3(-slopeA - slopeB * 0.62, 1.0, -slopeA * 0.32 - slopeB));
                return positionWS + float3(0.0, height, 0.0);
            }

            v2f vert(appdata v)
            {
                v2f o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                float3 normalWS;
                float height;
                float3 displaced = BaziWaterSurface(positionInputs.positionWS, _Time.y, normalWS, height);
                positionInputs = GetVertexPositionInputs(v.positionOS.xyz + float3(0.0, height, 0.0));
                o.positionHCS = positionInputs.positionCS;
                o.positionWS = displaced;
                o.normalWS = normalWS;
                o.uv = v.uv;
                o.waveHeight = height;
                o.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float time = _Time.y;
                float rain = saturate(_BaziAtmosphere.z);        // باران ⇒ ریزموج و کفِ بیشتر
                float foamAmount = saturate(_BaziWaterWave.w);
                float wetness = saturate(_BaziAtmosphere.z);

                // نویزِ جزئیات (از بافتِ DetailNoise یا رویه‌ای؛ هر دو حالت ممکن است)
                float2 detailUV = i.positionWS.xz * lerp(0.28, 0.72, rain)
                                + _BaziWaterScroll.xy * time * lerp(1.0, 2.3, rain);
                float detail = SAMPLE_TEXTURE2D(_BaziWaterDetail, sampler_BaziWaterDetail, detailUV).b;
                float ripple = BaziHash21(floor(detailUV * 42.0)) * 0.5 + detail * 0.5;

                float3 normalWS = normalize(i.normalWS + float3((ripple - 0.5) * 0.35, 0.0, (detail - 0.5) * 0.35));
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);   // بدونِ وابستگی به helper های URP
                float3 lightDir = GetMainLight().direction;
                float3 lightColor = GetMainLight().color;

                // fresnel: رو به افق، آسمان را منعکس می‌کند
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDir)), 5.0);
                float night = saturate(_BaziAtmosphere.x);
                half3 body = lerp(_BaziWaterDeep.rgb, _BaziWaterShallow.rgb,
                                   saturate(0.35 + i.waveHeight * 2.2 + ripple * 0.25));
                half3 reflected = lerp(_BaziWaterSky.rgb, _BaziWaterSky.rgb * 0.22, night);
                half3 albedo = lerp(body, reflected, saturate(fresnel * 1.15 + 0.06));

                // موج‌شکنیِ نور + اسپارک (نقطه‌هایِ درخشانِ ظهر) و کف
                float3 halfVec = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(normalWS, halfVec)), lerp(24.0, 220.0, _BaziWaterSmoothness));
                float sparkle = pow(saturate(ripple * 1.6 - 0.52), 6.0) * lerp(0.5, 3.2, _BaziWaterSmoothness)
                              * saturate(1.0 - night * 1.4);
                float crest = saturate((i.waveHeight / max(0.001, _BaziWaterWave.y * 1.62)) - 0.55);
                float foam = saturate(crest * foamAmount * 2.2 + ripple * foamAmount * 0.55 + rain * 0.12);

                half3 color = albedo * lerp(0.34, 1.0, night * 0.55) * (0.6 + 0.4 * saturate(dot(normalWS, lightDir)));
                color += lightColor * (spec * lerp(0.55, 1.5, _BaziWaterSmoothness) + sparkle * 0.6);
                color = lerp(color, half3(0.86, 0.94, 0.98), foam * 0.7);
                color += lightColor * wetness * 0.02;

                color = BaziApplyHeightFog(color, i.positionWS);
                color = MixFog(color, i.fogFactor);
                return half4(color, lerp(0.82, 0.97, saturate(foam + fresnel * 0.4)));
            }
            ENDHLSL
        }
    }
    Fallback "BaziBaqa/Surface"
}
