// BaziBaqa-ScreenSpaceAO.shader — مه‌اینکِ محیطی (SSAO) برای Renderer Feature
//
// سه Pass در یک فایل و یک SubShader، با `HLSLINCLUDE` مشترک (تا ساختار ورودی/خروجی یکی بماند):
//   0 = تولید AO از عمقِ صحنه (مارپیچِ Golden-angle؛ بدون بازسازیِ ماتریسی ⇒ ارزان و پایدار)
//   1 = نرم‌کردنِ پنج‌ضربی (box روی ۵ نمونه) ⇒ حذفِ نویزِ دانه‌دانه
//   2 = ضربِ AO روی رنگِ صحنه
//
// ورودی‌ها فقط دو چیزند: `_CameraDepthTexture` (که URP با پشتیبانیِ depth تولید می‌کند) و
// `_BaziAOParams` (که MaterialLibrary از روی نمایه‌ی کیفیت جهانی می‌کند). هیچ فایلِ تنظیماتِ
// دستی و هیچ شیدرِ گرافِ بصری لازم نیست ⇒ روی ماشینِ CI هم قابل‌اعتبارسنجی است.
Shader "Hidden/BaziBaqa/ScreenSpaceAO"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _BaziAOTexture("Ambient Occlusion", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 3.0

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // ثابت‌هایِ جهانیِ این افکت؛ بیرون از UnityPerMaterial چون از MaterialPropertyBlock
        // نمی‌آیند (الگوی خودِ URP برای بلوکِ مه: یک CBUFFER جدا + SetGlobalVector).
        float4 _BaziAOParams;      // x=intensity, y=radius(world), z=samples, w=enabled
        float4 _MainTex_TexelSize;   // Blit آن را خودش پر می‌کند؛ یک‌جا اعلام می‌شود تا همه Passها ببینند

        struct ScreenAttributes
        {
            float4 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
        };

        struct ScreenVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
        };

        ScreenVaryings ScreenVert(ScreenAttributes input)
        {
            ScreenVaryings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }
        ENDHLSL

        // ---------------------------------------------------------------- 0: تولید AO
        Pass
        {
            Name "BaziAOGenerate"

            HLSLPROGRAM
            #pragma vertex ScreenVert
            #pragma fragment AOGenerateFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float AOGenerateFrag(ScreenVaryings input) : SV_Target
            {
                if (_BaziAOParams.w <= 0.5)
                {
                    return 1.0;
                }

                float raw = SampleSceneDepth(input.uv);
                float eye = LinearEyeDepth(raw, _ZBufferParams);
                if (eye <= 0.0001)
                {
                    return 1.0;      // آسمان/پس‌زمینه ⇒ بدون انسداد
                }

                float samples = max(1.0, _BaziAOParams.z);
                float radius = max(0.01, _BaziAOParams.y);
                // شعاعِ رویِ صفحه با دورشدن کوچک می‌شود ⇒ خطایِ AO در دوردست دیده نمی‌شود
                float radiusPixels = (radius / max(0.05, eye)) * _ScreenParams.y * 0.5;
                float2 texel = 1.0 / max(float2(1.0, 1.0), _ScreenParams.xy);

                float occlusion = 0.0;
                const float goldenAngle = 2.39996323;
                for (float i = 0.0; i < samples; i += 1.0)
                {
                    float angle = i * goldenAngle + input.uv.x * 11.0 + input.uv.y * 7.0;
                    float t = (i + 1.0) / samples;
                    float2 sampleUV = saturate(input.uv + float2(cos(angle), sin(angle)) * sqrt(t) * radiusPixels * texel);

                    float sampleEye = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
                    float delta = eye - sampleEye;                                   // مثبت ⇒ نمونه جلوتر است
                    float inFront = saturate((delta / max(0.02, radius)) * 4.0);
                    float notBehind = saturate(1.0 - abs(delta) / max(0.05, radius * 3.0));
                    occlusion += inFront * notBehind;
                }

                return saturate(1.0 - saturate(occlusion / samples) * saturate(_BaziAOParams.x));
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- 1: نرم‌کردن
        Pass
        {
            Name "BaziAOBlur"

            HLSLPROGRAM
            #pragma vertex ScreenVert
            #pragma fragment AOBlurFrag

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);

            float AOBlurFrag(ScreenVaryings input) : SV_Target
            {
                float2 texel = max(float2(1e-5, 1e-5), _MainTex_TexelSize.xy) * 1.5;
                float total = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                total += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texel.x, 0.0)).r;
                total += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(texel.x, 0.0)).r;
                total += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0.0, texel.y)).r;
                total += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0.0, texel.y)).r;
                return total * 0.2;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- 2: ترکیب
        Pass
        {
            Name "BaziAOCombine"

            HLSLPROGRAM
            #pragma vertex ScreenVert
            #pragma fragment AOCombineFrag

            TEXTURE2D(_MainTex);         SAMPLER(sampler_MainTex);
            TEXTURE2D(_BaziAOTexture);   SAMPLER(sampler_BaziAOTexture);

            float4 AOCombineFrag(ScreenVaryings input) : SV_Target
            {
                float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                if (_BaziAOParams.w <= 0.5)
                {
                    return float4(color, 1.0);
                }
                float ao = SAMPLE_TEXTURE2D(_BaziAOTexture, sampler_BaziAOTexture, input.uv).r;
                // AO در این معماری *بعد از* نورپردازی ضرب می‌شود، پس وزنش ملایم است
                // تا فلزِ روشن کدرِ پلاستیکی نشود؛ جبرانِ آن را Bloom و ColorAdjustments می‌کنند.
                color *= lerp(1.0, ao, saturate(_BaziAOParams.x) * 0.72);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
