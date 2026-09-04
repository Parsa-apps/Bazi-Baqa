// BaziBaqa-Surface.shader — شیدرِ سطحِ اصلی جهان (زمین، سنگ، تنه، ساختمان، پوشش گیاهی)
//
// سه چیز این شیدر را از حالت «مکعب رنگی» فازهای قبل بیرون می‌آورد:
//   1) نقشه‌ی جزئیات با tiling در فضای جهان ⇒ زمین، تکرارِ محسوسِ UV مش را ندارد.
//   2) یک ماسکِ ۴کاناله (زبری / AO / رطوبت / پوشش) ⇒ یک نمونه‌برداری، چهار اثر فیزیکی.
//   3) باد روی GPU، مه ارتفاعی، آسیبِ بصری و درخششِ شب؛ هرکدام با keywordِ محلی، پس
//      متریالی که ویژگی را روشن نکرده هیچ هزینه‌ای روی GPU نمی‌پردازد (شرطِ اندروید میان‌رده).
//
// دو SubShader: اولی برای URP، دومی پشتیبان برای Built-in ⇒ بازی در هر دو حالت درست دیده می‌شود.
Shader "BaziBaqa/Surface"
{
    Properties
    {
        _BaziColor("Color", Color) = (1, 1, 1, 1)
        [HDR]_BaziEmissionColor("Night Emissive (RGB, A=Intensity)", Color) = (0, 0, 0, 0)

        _BaziBaseMap("Base Map (RGB)", 2D) = "white" {}
        _BaziNormalMap("Normal Map (RGB, Linear)", 2D) = "grey" {}
        _BaziMaskMap("Mask Map (R=Rough, G=AO, B=Moist, A=Coverage)", 2D) = "white" {}
        _BaziDetailMap("Detail Map (RGB)", 2D) = "white" {}
        _BaziDetailScale("Detail World Scale", Range(0.1, 20)) = 1.6
        _BaziDetailBlend("Detail Blend", Range(0, 1)) = 0.35

        _BaziRoughness("Roughness", Range(0, 1)) = 0.85
        _BaziMetallic("Metallic", Range(0, 1)) = 0.0
        _BaziNormalScale("Normal Scale", Range(0, 3)) = 1.0
        _BaziOcclusionStrength("Occlusion Strength", Range(0, 1)) = 0.75
        _BaziMoistDarken("Wet Darkening", Range(0, 1)) = 0.4
        _BaziSmoothnessBoost("Wet Smoothness Boost", Range(0, 1)) = 0.55

        _BaziDamage("Damage Amount", Range(0, 1)) = 0.0
        _BaziDamageMap("Damage Mask (R=Crack, G=Soot, B=Chip, A=Alpha)", 2D) = "black" {}
        _BaziDamageColor("Damage Color", Color) = (0.06, 0.05, 0.045, 1)

        _BaziWindStrength("Wind Response", Range(0, 1)) = 0.0
        _BaziWindFlex("Wind Height Power", Range(0.2, 6)) = 2.0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.35
        _BaziShadowBias("Shadow Normal Offset", Range(0, 0.2)) = 0.02

        [Toggle(_BAZI_DETAIL_ON)] _UseDetail("Use Detail Map", Float) = 1
        [Toggle(_BAZI_WIND_ON)] _UseWind("Enable Wind", Float) = 0
        [Toggle(_BAZI_ALPHA_CLIP_ON)] _UseAlphaClip("Alpha Clip (Foliage)", Float) = 0
        [Toggle(_BAZI_DAMAGE_ON)] _UseDamage("Use Damage Mask", Float) = 0
        [Toggle(_BAZI_EMISSIVE_ON)] _UseEmissive("Night Emissive", Float) = 0
        [Toggle(_BAZI_VERTEX_COLOR_ON)] _UseVertexColor("Tint By Vertex Color", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode, 2)] _BaziCull("Cull Mode", Float) = 2
    }

    // =========================================================================
    // مسیر URP — Unity 2022.3 / URP 14
    // =========================================================================
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull [_BaziCull]

        // ------------------------------------------------------------------ Forward
        Pass
        {
            Name "BaziForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BaziVert
            #pragma fragment BaziFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _BAZI_DETAIL_ON
            #pragma shader_feature_local _ _BAZI_WIND_ON
            #pragma shader_feature_local _ _BAZI_ALPHA_CLIP_ON
            #pragma shader_feature_local _ _BAZI_DAMAGE_ON
            #pragma shader_feature_local _ _BAZI_EMISSIVE_ON
            #pragma shader_feature_local _ _BAZI_VERTEX_COLOR_ON

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BaziBaqaCore.hlsl"

            // همه‌ی ثابت‌های متریال داخل UnityPerMaterial ⇒ سازگاری با SRP Batcher.
            // این بلوک در هر سه Pass دقیقاً یکسان است (تفاوتِ چیدمان = خروج از SRP Batcher).
            CBUFFER_START(UnityPerMaterial)
                float4 _BaziColor;
                float4 _BaziEmissionColor;
                float4 _BaziBaseMap_ST;
                float4 _BaziNormalMap_ST;
                float4 _BaziMaskMap_ST;
                float4 _BaziDetailMap_ST;
                float4 _BaziDamageMap_ST;
                float4 _BaziDamageColor;
                float  _BaziDetailScale;
                float  _BaziDetailBlend;
                float  _BaziRoughness;
                float  _BaziMetallic;
                float  _BaziNormalScale;
                float  _BaziOcclusionStrength;
                float  _BaziMoistDarken;
                float  _BaziSmoothnessBoost;
                float  _BaziDamage;
                float  _BaziWindStrength;
                float  _BaziWindFlex;
                float  _Cutoff;
                float  _BaziShadowBias;
            CBUFFER_END

            TEXTURE2D(_BaziBaseMap);      SAMPLER(sampler_BaziBaseMap);
            TEXTURE2D(_BaziNormalMap);    SAMPLER(sampler_BaziNormalMap);
            TEXTURE2D(_BaziMaskMap);      SAMPLER(sampler_BaziMaskMap);
            TEXTURE2D(_BaziDetailMap);    SAMPLER(sampler_BaziDetailMap);
            TEXTURE2D(_BaziDamageMap);    SAMPLER(sampler_BaziDamageMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                float2 detailUV    : TEXCOORD4;
                half4  vertexColor : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings BaziVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;

                #ifdef _BAZI_WIND_ON
                float windStrength = _BaziWindStrength * _BaziWindState.x;
                BaziApplyWind(positionOS, normalOS, windStrength, _BaziWindFlex);
                #endif

                float3 positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaziBaseMap);
                output.detailUV = positionWS.xz / max(0.05, _BaziDetailScale);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.vertexColor = input.color;
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                return output;
            }

            half4 BaziFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;
                float3 base = SAMPLE_TEXTURE2D(_BaziBaseMap, sampler_BaziBaseMap, uv).rgb;

                #ifdef _BAZI_DETAIL_ON
                // tiling در فضای جهان؛ با این کارِ کوچک، زمینِ ۶۴×۴۴ دیگر «کاغذدیواری» نمی‌شود
                float3 detail = SAMPLE_TEXTURE2D(_BaziDetailMap, sampler_BaziDetailMap, input.detailUV).rgb;
                base = lerp(base, base * (detail * 1.7 - 0.35), saturate(_BaziDetailBlend));
                #endif

                float4 mask = SAMPLE_TEXTURE2D(_BaziMaskMap, sampler_BaziMaskMap, uv);

                #ifdef _BAZI_ALPHA_CLIP_ON
                clip(mask.a - _Cutoff);
                #endif

                #ifdef _BAZI_VERTEX_COLOR_ON
                // فقط مش‌هایی رنگِ زیست‌بوم دارند این کلید را روشن می‌کنند (زمینِ WorldGenerator)
                base *= saturate(input.vertexColor.rgb * 1.4);
                #endif

                // ---- نرمال با TBNِ ساخته‌شده از خودِ نرمال (بدون نیاز به tangent در مش) ----
                float3 normalWS = normalize(input.normalWS);
                float3 packedNormal = SAMPLE_TEXTURE2D(_BaziNormalMap, sampler_BaziNormalMap, uv).rgb;
                float3 tangentNormal = BaziDecodeNormal(packedNormal) * float3(_BaziNormalScale, _BaziNormalScale, 1.0);
                float3 referenceAxis = abs(normalWS.y) > 0.9 ? float3(1.0, 0.0, 0.0) : float3(0.0, 1.0, 0.0);
                float3 worldTangent = normalize(cross(referenceAxis, normalWS));
                float3 worldBitangent = cross(normalWS, worldTangent);
                normalWS = normalize(worldTangent * tangentNormal.x + worldBitangent * tangentNormal.y + normalWS * tangentNormal.z);

                // ---- چهار اثر از یک نمونه‌برداری ----
                float moisture = saturate(mask.b + _BaziAtmosphere.z);   // باران همه‌جا را خیس می‌کند
                float roughness = saturate(mask.r * _BaziRoughness * (1.0 - moisture * 0.72) + 0.035);
                float ao = lerp(1.0, mask.g, _BaziOcclusionStrength);
                float metallic = saturate(_BaziMetallic);
                base *= lerp(1.0, 1.0 - _BaziMoistDarken, moisture);
                base = lerp(base, base * 0.9 + float3(0.055, 0.048, 0.036), saturate(_BaziAtmosphere.w) * 0.5);   // گردوغبارِ هوا

                #ifdef _BAZI_DAMAGE_ON
                float4 damage = SAMPLE_TEXTURE2D(_BaziDamageMap, sampler_BaziDamageMap, uv);
                float damageAmount = saturate(_BaziDamage) * saturate(damage.a + 0.2);
                base = lerp(base, _BaziDamageColor.rgb * (0.35 + damage.g * 0.75), saturate(damageAmount * (damage.r * 0.85 + damage.b * 0.7)));
                roughness = saturate(roughness + damageAmount * 0.3);
                metallic *= 1.0 - saturate(damageAmount * 0.7);
                ao = lerp(ao, 0.4, saturate(damage.r * damageAmount));
                #endif

                float3 albedo = base * _BaziColor.rgb;
                float3 diffuseColor = albedo * (1.0 - metallic);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);

                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lighting = 0.0;

                float ndl = saturate(dot(normalWS, mainLight.direction));
                float mainSpec = BaziSpecularGGX(normalWS, viewDirection, mainLight.direction, roughness, mainLight.color);
                lighting += (diffuseColor + mainSpec * lerp(0.04, 1.0, metallic))
                          * mainLight.color * ndl * mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                #if defined(_ADDITIONAL_LIGHTS)
                uint additionalCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalCount; ++lightIndex)
                {
                    Light additional = GetAdditionalLight(lightIndex, input.positionWS);
                    float additionalNdl = saturate(dot(normalWS, additional.direction));
                    float additionalSpec = BaziSpecularGGX(normalWS, viewDirection, additional.direction, roughness, additional.color);
                    lighting += (diffuseColor + additionalSpec * lerp(0.04, 1.0, metallic))
                              * additional.color * additionalNdl * additional.distanceAttenuation * additional.shadowAttenuation;
                }
                #endif

                // ---- نور محیطی + بازتابِ ارزان از SH (بدون وابستگی به probe box) ----
                float3 reflected = reflect(-viewDirection, normalWS);
                float ambientScale = _BaziAtmosphere.y > 0.001 ? _BaziAtmosphere.y : 1.0;
                float3 ambient = SampleSH(normalWS) * diffuseColor * ao * ambientScale;
                float smoothness = saturate(1.0 - roughness);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirection)), 5.0);
                float specularEnvironment = saturate(lerp(0.04, 1.0, saturate(fresnel + moisture * 0.55)) * smoothness * ao);
                ambient += SampleSH(reflected) * lerp(0.04, albedo, metallic) * specularEnvironment * ambientScale;

                float3 color = lighting + ambient;

                #ifdef _BAZI_EMISSIVE_ON
                float nightGlow = saturate(_BaziAtmosphere.x);
                color += _BaziEmissionColor.rgb * _BaziEmissionColor.a * lerp(0.12, 1.0, nightGlow);
                #endif

                color = BaziApplyHeightFog(color, input.positionWS);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ ShadowCaster
        Pass
        {
            Name "BaziShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BaziShadowVert
            #pragma fragment BaziShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _BAZI_ALPHA_CLIP_ON
            #pragma shader_feature_local _ _BAZI_WIND_ON

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BaziBaqaCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaziColor;
                float4 _BaziEmissionColor;
                float4 _BaziBaseMap_ST;
                float4 _BaziNormalMap_ST;
                float4 _BaziMaskMap_ST;
                float4 _BaziDetailMap_ST;
                float4 _BaziDamageMap_ST;
                float4 _BaziDamageColor;
                float  _BaziDetailScale;
                float  _BaziDetailBlend;
                float  _BaziRoughness;
                float  _BaziMetallic;
                float  _BaziNormalScale;
                float  _BaziOcclusionStrength;
                float  _BaziMoistDarken;
                float  _BaziSmoothnessBoost;
                float  _BaziDamage;
                float  _BaziWindStrength;
                float  _BaziWindFlex;
                float  _Cutoff;
                float  _BaziShadowBias;
            CBUFFER_END

            TEXTURE2D(_BaziMaskMap);      SAMPLER(sampler_BaziMaskMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings BaziShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                // جابه‌جایی در راستای نرمال ⇒ حذف acne روی مش‌های کم‌جزئیات (مکعب/استوانه)
                float3 positionOS = input.positionOS.xyz + input.normalOS * _BaziShadowBias;
                #ifdef _BAZI_WIND_ON
                float3 normalOS = input.normalOS;
                BaziApplyWind(positionOS, normalOS, _BaziWindStrength * _BaziWindState.x, _BaziWindFlex);
                #endif
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaziMaskMap);
                return output;
            }

            half4 BaziShadowFrag(Varyings input) : SV_Target
            {
                #ifdef _BAZI_ALPHA_CLIP_ON
                clip(SAMPLE_TEXTURE2D(_BaziMaskMap, sampler_BaziMaskMap, input.uv).a - _Cutoff);
                #endif
                return 0.0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ DepthOnly (برای AO/DoF/بارانِ عمق‌دار)
        Pass
        {
            Name "BaziDepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BaziDepthVert
            #pragma fragment BaziDepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _BAZI_ALPHA_CLIP_ON
            #pragma shader_feature_local _ _BAZI_WIND_ON

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BaziBaqaCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaziColor;
                float4 _BaziEmissionColor;
                float4 _BaziBaseMap_ST;
                float4 _BaziNormalMap_ST;
                float4 _BaziMaskMap_ST;
                float4 _BaziDetailMap_ST;
                float4 _BaziDamageMap_ST;
                float4 _BaziDamageColor;
                float  _BaziDetailScale;
                float  _BaziDetailBlend;
                float  _BaziRoughness;
                float  _BaziMetallic;
                float  _BaziNormalScale;
                float  _BaziOcclusionStrength;
                float  _BaziMoistDarken;
                float  _BaziSmoothnessBoost;
                float  _BaziDamage;
                float  _BaziWindStrength;
                float  _BaziWindFlex;
                float  _Cutoff;
                float  _BaziShadowBias;
            CBUFFER_END

            TEXTURE2D(_BaziMaskMap);      SAMPLER(sampler_BaziMaskMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings BaziDepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionOS = input.positionOS.xyz;
                #ifdef _BAZI_WIND_ON
                float3 normalOS = input.normalOS;
                BaziApplyWind(positionOS, normalOS, _BaziWindStrength * _BaziWindState.x, _BaziWindFlex);
                #endif
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaziMaskMap);
                return output;
            }

            half4 BaziDepthFrag(Varyings input) : SV_Target
            {
                #ifdef _BAZI_ALPHA_CLIP_ON
                clip(SAMPLE_TEXTURE2D(_BaziMaskMap, sampler_BaziMaskMap, input.uv).a - _Cutoff);
                #endif
                return 0.0;
            }
            ENDHLSL
        }
    }

    // =========================================================================
    // مسیر پشتیبان — Built-in Render Pipeline (وقتی URP Asset هنوز نصب نشده)
    // =========================================================================
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull [_BaziCull]

        CGPROGRAM
        #pragma target 3.0
        #pragma surface BaziBuiltInSurf Lambert vertex:BaziBuiltInVert fullforwardshadows addshadow finalcolor:BaziBuiltInFinal
        #pragma multi_compile_fog
        #pragma shader_feature _BAZI_DETAIL_ON
        #pragma shader_feature _BAZI_WIND_ON
        #pragma shader_feature _BAZI_DAMAGE_ON
        #pragma shader_feature _BAZI_EMISSIVE_ON
        #pragma shader_feature _BAZI_VERTEX_COLOR_ON

        #include "BaziBaqaCore.cginc"

        float4 _BaziColor;
        float4 _BaziEmissionColor;
        sampler2D _BaziBaseMap;
        sampler2D _BaziNormalMap;
        sampler2D _BaziMaskMap;
        sampler2D _BaziDetailMap;
        sampler2D _BaziDamageMap;
        float4 _BaziBaseMap_ST;
        float4 _BaziNormalMap_ST;
        float4 _BaziMaskMap_ST;
        float4 _BaziDamageMap_ST;
        float  _BaziDetailScale;
        float  _BaziDetailBlend;
        float  _BaziRoughness;
        float  _BaziMetallic;
        float  _BaziNormalScale;
        float  _BaziOcclusionStrength;
        float  _BaziMoistDarken;
        float  _BaziDamage;
        float4 _BaziDamageColor;
        float  _BaziWindStrength;
        float  _BaziWindFlex;
        float  _Cutoff;

        struct Input
        {
            float2 uv_BaziBaseMap;
            float2 uv_BaziNormalMap;
            float2 uv_BaziMaskMap;
            float2 uv_BaziDamageMap;
            float3 worldPos;
            float4 color;        // رنگِ زیست‌بومِ نوشته‌شده روی مشِ زمین
        };

        void BaziBuiltInVert(inout appdata_full v, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
            #ifdef _BAZI_WIND_ON
            float3 normalOS = v.normal;
            BaziApplyWind(v.vertex.xyz, normalOS, _BaziWindStrength * _BaziWindState.x, _BaziWindFlex);
            v.normal = normalOS;
            #endif
        }

        void BaziBuiltInSurf(Input IN, inout SurfaceOutput o)
        {
            float3 base = tex2D(_BaziBaseMap, IN.uv_BaziBaseMap).rgb;
            float4 mask = tex2D(_BaziMaskMap, IN.uv_BaziMaskMap);
            #ifdef _BAZI_DETAIL_ON
            float2 detailUV = IN.worldPos.xz / max(0.05, _BaziDetailScale);
            float3 detail = tex2D(_BaziDetailMap, detailUV).rgb;
            base = lerp(base, base * (detail * 1.7 - 0.35), saturate(_BaziDetailBlend));
            #endif
            #ifdef _BAZI_VERTEX_COLOR_ON
            base *= saturate(IN.color.rgb * 1.4);
            #endif
            #ifdef _BAZI_DAMAGE_ON
            float4 damage = tex2D(_BaziDamageMap, IN.uv_BaziDamageMap);
            float damageAmount = saturate(_BaziDamage) * saturate(damage.a + 0.2);
            base = lerp(base, _BaziDamageColor.rgb * (0.35 + damage.g * 0.75), saturate(damageAmount * (damage.r * 0.85 + damage.b * 0.7)));
            #endif
            float moisture = saturate(mask.b + _BaziAtmosphere.z);
            base *= lerp(1.0, 1.0 - _BaziMoistDarken, moisture) * _BaziColor.rgb;
            o.Albedo = base;
            o.Alpha = 1.0;
            o.Gloss = saturate(1.0 - mask.r * _BaziRoughness) * (1.0 + moisture * 0.35);
            o.Specular = lerp(0.12, 0.6, saturate(_BaziMetallic + moisture * 0.4));
            o.Normal = BaziDecodeNormalRGB(tex2D(_BaziNormalMap, IN.uv_BaziNormalMap).rgb) * float3(_BaziNormalScale, _BaziNormalScale, 1.0);
            #ifdef _BAZI_EMISSIVE_ON
            o.Emission = _BaziEmissionColor.rgb * _BaziEmissionColor.a * lerp(0.12, 1.0, saturate(_BaziAtmosphere.x));
            #else
            o.Emission = 0.0;
            #endif
        }

        void BaziBuiltInFinal(Input IN, SurfaceOutput o, inout fixed4 color)
        {
            color.rgb = BaziApplyHeightFog(color.rgb, IN.worldPos);
        }
        ENDCG
    }

    Fallback "Legacy Shaders/Diffuse"
}
