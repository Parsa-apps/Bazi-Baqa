// BaziBaqaCore.hlsl — کمک‌تابعی مشترکِ همه‌ی شیدرهای URP پروژه «سرزمین بقا»
//
// قوانینی که اینجا رعایت شده (و دلایلشان):
//  * هیچ `UnityCG.cginc` یا `LSVF` وارد نمی‌شود؛ URP با آن‌ها کامپایل نمی‌شود و شیدر به رنگ ارغوانی (magenta) درمی‌آید.
//  * همه‌ی ثابت‌هایِ متریال داخل `CBUFFER_START(UnityPerMaterial)` می‌نشینند تا SRP Batcher
//    فعال بماند (Draw Call پایین؛ شرطِ اجرای روان روی اندروید میان‌رده).
//  * ثابت‌های جهانی (باد، مه ارتفاعی، درخشش شب) در `CBUFFER_START(BaziGlobals)` هستند و
//    با `Shader.SetGlobal*` از سمت C# نوشته می‌شوند؛ اگر سیستمی نصب نباشد مقدار صفر است
//    و صحنه فقط ساده‌تر رندر می‌شود (هیچ‌وقت نمی‌شکند).
#ifndef BAZIBAQA_CORE_HLSL
#define BAZIBAQA_CORE_HLSL

// ---- ثابت‌های جهانی -------------------------------------------------------
CBUFFER_START(BaziGlobals)
    // x = شدت، y = بسامد، z = فازِ محلی، w = توانِ ارتفاع (سفتیِ تنه در برابر شاخه)
    float4 _BaziWindState;
    // x = چگالی، y = افتِ ارتفاعی، z = سقفِ ارتفاع مه، w = ارتفاعِ کفِ زمین
    float4 _BaziHeightFog;
    float4 _BaziFogTint;
    // x = ضریب درخشش شب (پنجره/فالس/آب)، y = مقیاس نور محیطی، z = خیسیِ سطح‌ها (باران)،
    // w = غبارِ معلق. نویسنده‌ی این چهار عدد فقط SkyLightingRig است.
    float4 _BaziAtmosphere;
CBUFFER_END

// ---- ابزارهای کوچک و پایدار ----------------------------------------------
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

float2 BaziHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float3 BaziDecodeNormal(float3 packed)
{
    // بافت‌های نرمالِ پروژه در فضای RGBِ خطی و با قرارداد OpenGL (+Y) ذخیره شده‌اند؛
    // خودمان باز می‌کنیم تا به `UnpackNormal` و قالب پلتفرم (DXT5nm) وابسته نباشیم.
    float3 normal = packed * 2.0 - 1.0;
    normal.z = sqrt(saturate(1.0 - dot(normal.xy, normal.xy)));
    return normalize(normal);
}

// ---- باد روی سطوح زنده ----------------------------------------------------
// `windMask` از خودِ متریال می‌آید (۰ برای سنگ/ساختمان، ۱ برای شاخ‌وبرگ).
void BaziApplyWind(inout float3 positionOS, inout float3 normalOS, float windMask)
{
    float strength = _BaziWindState.x * windMask;
    if (strength <= 0.0001)
    {
        return;
    }
    float heightWeight = pow(saturate(positionOS.y * 0.5 + 0.5), max(0.1, _BaziWindState.w));
    float phase = (positionOS.x + positionOS.z) * _BaziWindState.y + _Time.y + _BaziWindState.z;
    positionOS.x += sin(phase) * strength * heightWeight;
    positionOS.z += cos(phase * 0.73) * strength * 0.62 * heightWeight;
    positionOS.y -= abs(sin(phase)) * strength * 0.16 * heightWeight;
    normalOS.xz += float2(cos(phase), -sin(phase * 0.73)) * strength * 0.35 * heightWeight;
}

// ---- مه ارتفاعی (چیزی که حسِ «سینمایی» را بیشتر از Bloom می‌سازد) --------
float BaziHeightFogAmount(float3 positionWS)
{
    float span = max(0.001, _BaziHeightFog.z - _BaziHeightFog.w);
    float heightFade = saturate(1.0 - (positionWS.y - _BaziHeightFog.w) / span);
    float distance = length(positionWS - _WorldSpaceCameraPos);
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

// ---- اسپکیولارِ فشرده (GGX سبک، بدون وابستگی به API درون‌یِ URP) --------
float BaziSpecularGGX(float3 normalWS, float3 viewDirWS, float3 lightDirWS, float roughness, float3 lightColor)
{
    float3 halfVector = normalize(lightDirWS + viewDirWS);
    float noh = saturate(dot(normalWS, halfVector));
    float nom = saturate(dot(normalWS, viewDirWS));
    float a = max(0.002, roughness * roughness);
    float a2 = a * a;
    float d = noh * noh * (a2 - 1.0) + 1.0;
    float distribution = a2 / max(0.0001, (UNITY_PI * d * d));
    float visibility = 0.25 / max(0.0001, a + 0.5);
    return distribution * visibility * saturate(noh) * saturate(nom) * lightColor.r;
}

// ---- پراکندگیِ نورِ غروب در مه (Rayleigh ساده و ارزان) -------------------
float3 BaziSkyAmbient(float3 normalWS, float3 skyTint, float3 groundTint, float ambientScale)
{
    float up = saturate(normalWS.y * 0.5 + 0.5);
    return lerp(groundTint, skyTint, up) * ambientScale;
}

#endif // BAZIBAQA_CORE_HLSL
