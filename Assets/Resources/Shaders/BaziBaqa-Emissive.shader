// BaziBaqa-Emissive.shader — چشمه‌های نورِ صحنه (آتش، فانوس، چشم دشمن، بیکن برجک)
//
// این شیدر عمداً Unlit است: یک جسمِ نورانی نباید با نورِ صحنه تیره شود، ولی باید در
// Bloom دیده شود؛ پس رنگِ HDR (بزرگ‌تر از ۱) مستقیم نوشته می‌شود و Bloom آن را «می‌گیرد».
// سوسوی آتش از همان `_BaziFlicker` می‌آید که سمت C# روی متریال نوشته می‌شود (بدون هرگونه Update روی CPU).
Shader "BaziBaqa/Emissive"
{
    Properties
    {
        _BaziEmissiveColor("Emissive (RGB, A=Intensity)", Color) = (1, 1, 1, 1)
        [HDR]_BaziEmissiveHDR("HDR Boost", Color) = (1, 1, 1, 1)
        _BaziEmissiveMap("Emissive Mask (R)", 2D) = "white" {}
        _BaziFlicker("Flicker Amount", Range(0, 1)) = 0.0
        _BaziFlickerSpeed("Flicker Speed", Range(0.1, 12)) = 3.0
        _BaziNightBoost("Night Boost", Range(0, 4)) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode, 2)] _BaziSrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode, 2)] _BaziDstBlend("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode, 2)] _BaziCull("Cull", Float) = 2
        [HideInInspector] _BaziZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull [_BaziCull]
        ZWrite [_BaziZWrite]
        Blend [_BaziSrcBlend] [_BaziDstBlend]

        Pass
        {
            Name "BaziEmissiveForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EmissiveVert
            #pragma fragment EmissiveFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BaziBaqaCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaziEmissiveColor;
                float4 _BaziEmissiveHDR;
                float4 _BaziEmissiveMap_ST;
                float  _BaziFlicker;
                float  _BaziFlickerSpeed;
                float  _BaziNightBoost;
                float  _BaziZWrite;
                float  _BaziSrcBlend;
                float  _BaziDstBlend;
                float  _BaziCull;
            CBUFFER_END

            TEXTURE2D(_BaziEmissiveMap);  SAMPLER(sampler_BaziEmissiveMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings EmissiveVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaziEmissiveMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 EmissiveFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float mask = SAMPLE_TEXTURE2D(_BaziEmissiveMap, sampler_BaziEmissiveMap, input.uv).r;
                float3 color = _BaziEmissiveColor.rgb * _BaziEmissiveColor.a * _BaziEmissiveHDR.rgb * mask;

                // سوسو: نویزِ ارزان روی زمان، با فازِ جابه‌جا‌شده بر حسبِ جایِ شیء ⇒ دو شعله هم‌فاز نمی‌شوند
                if (_BaziFlicker > 0.0)
                {
                    float phase = _Time.y * _BaziFlickerSpeed + input.positionWS.x * 1.7 + input.positionWS.z * 2.3;
                    float flicker = 1.0 - _BaziFlicker * (0.5 + 0.5 * sin(phase) * cos(phase * 0.37));
                    color *= saturate(flicker);
                }

                // شب، چشمه‌ی نور را پررنگ‌تر و روز ملایم‌تر می‌کند
                color *= lerp(1.0, _BaziNightBoost, saturate(_BaziAtmosphere.x));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "BaziEmissiveDepth"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaziEmissiveColor;
                float4 _BaziEmissiveHDR;
                float4 _BaziEmissiveMap_ST;
                float  _BaziFlicker;
                float  _BaziFlickerSpeed;
                float  _BaziNightBoost;
                float  _BaziZWrite;
                float  _BaziSrcBlend;
                float  _BaziDstBlend;
                float  _BaziCull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target { return 0.0; }
            ENDHLSL
        }
    }

    // مسیر پشتیبانِ Built-in: همان رنگِ HDR بدون نورپردازی
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Cull [_BaziCull]
        ZWrite [_BaziZWrite]
        Blend [_BaziSrcBlend] [_BaziDstBlend]

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex BuiltInVert
            #pragma fragment BuiltInFrag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            float4 _BaziEmissiveColor;
            float4 _BaziEmissiveHDR;
            float4 _BaziEmissiveMap_ST;
            sampler2D _BaziEmissiveMap;
            float  _BaziFlicker;
            float  _BaziFlickerSpeed;
            float  _BaziNightBoost;
            float  _BaziZWrite;
            float  _BaziSrcBlend;
            float  _BaziDstBlend;
            float  _BaziCull;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            Varyings BuiltInVert(appdata_full input)
            {
                Varyings output;
                float3 positionWS = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.position = UnityObjectToClipPos(input.vertex.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaziEmissiveMap);
                output.positionWS = positionWS;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            fixed4 BuiltInFrag(Varyings input) : SV_Target
            {
                float mask = tex2D(_BaziEmissiveMap, input.uv).r;
                float3 color = _BaziEmissiveColor.rgb * _BaziEmissiveColor.a * _BaziEmissiveHDR.rgb * mask;
                if (_BaziFlicker > 0.0)
                {
                    float phase = _Time.y * _BaziFlickerSpeed + input.positionWS.x * 1.7 + input.positionWS.z * 2.3;
                    color *= saturate(1.0 - _BaziFlicker * (0.5 + 0.5 * sin(phase) * cos(phase * 0.37)));
                }
                fixed4 result = fixed4(color, 1.0);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
