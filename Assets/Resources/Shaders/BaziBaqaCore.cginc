// BaziBaqaCore.cginc — کمک‌تابعیِ مسیرِ پشتیبان (Built-in Render Pipeline)
//
// چرا فایل جدا؟ همان ظاهرِ مسیر URP باید وقتی کاربر هنوز «نصب URP Asset» را اجرا نکرده
// است هم درست دیده شود؛ پس هر شیدرِ پروژه دو SubShader دارد: یکی با تگ
// UniversalPipeline (HLSL) و دیگری همین مسیرِ CG.
//
// محدودیت آگاهانه: این مسیر از مدل نورپردازیِ Lambert built-in استفاده می‌کند و
// بازتابِ محیطیِ کامل ندارد؛ یعنی کمی تخت‌تر، ولی هیچ‌وقت ارغوانی نمی‌شود.
// نامِ ثابت‌های جهانی با مسیر URP یکی است ⇒ `Shader.SetGlobalVector` هر دو را همزمان تغذیه می‌کند.
#ifndef BAZIBAQA_CORE_CGINC
#define BAZIBAQA_CORE_CGINC

#include "UnityCG.cginc"

// x = شدت، y = بسامد، z = فاز، w = توانِ ارتفاع (سفتیِ تنه در برابر شاخه)
float4 _BaziWindState;
// x = چگالی، y = افتِ ارتفاعی، z = سقفِ ارتفاع مه، w = ارتفاعِ کفِ زمین
float4 _BaziHeightFog;
float4 _BaziFogTint;
// x = شب، y = مقیاس نور محیطی، z = رطوبتِ عمومی (باران)، w = گردوغبار
float4 _BaziAtmosphere;

float BaziLuminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float BaziHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float3 BaziDecodeNormalRGB(float3 packed)
{
    float3 normal = packed * 2.0 - 1.0;
    normal.z = sqrt(saturate(1.0 - dot(normal.xy, normal.xy)));
    return normalize(normal);
}

// باد روی سطوح زنده؛ `heightPower` از متریال می‌آید (نویسه‌ی CBUFFER خواندنی است و
// نوشتن روی آن مجاز نیست، پس پارامتر صریح می‌گیریم).
void BaziApplyWind(inout float3 positionOS, inout float3 normalOS, float strength, float heightPower)
{
    if (strength <= 0.0001)
    {
        return;
    }
    float heightWeight = pow(saturate(positionOS.y * 0.5 + 0.5), max(0.1, heightPower));
    float phase = (positionOS.x + positionOS.z) * _BaziWindState.y + _Time.y + _BaziWindState.z;
    positionOS.x += sin(phase) * strength * heightWeight;
    positionOS.z += cos(phase * 0.73) * strength * 0.62 * heightWeight;
    positionOS.y -= abs(sin(phase)) * strength * 0.16 * heightWeight;
    normalOS.xz += float2(cos(phase), -sin(phase * 0.73)) * strength * 0.35 * heightWeight;
}

float BaziHeightFogAmount(float3 positionWS)
{
    float span = max(0.001, _BaziHeightFog.z - _BaziHeightFog.w);
    float heightFade = saturate(1.0 - (positionWS.y - _BaziHeightFog.w) / span);
    float distance = length(positionWS - GetWorldSpaceCameraPos());
    float distanceFade = 1.0 - exp(-_BaziHeightFog.x * distance);
    return saturate(distanceFade * heightFade * heightFade);
}

float3 BaziApplyHeightFog(float3 color, float3 positionWS)
{
    if (_BaziHeightFog.x <= 0.0001)
    {
        return color;
    }
    return lerp(color, _BaziFogTint.rgb, BaziHeightFogAmount(positionWS));
}

#endif // BAZIBAQA_CORE_CGINC
