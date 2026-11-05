// Keep the installed URP Lit lighting, normal mapping and depth passes intact.
// The only surface change is ink mixed into the reverse face's original albedo.
#define InitializeStandardLitSurfaceData InitializePaperWithoutInk
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData
TEXTURE2D(_InkMap); SAMPLER(sampler_InkMap);
float4 _InkRect;
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    InitializePaperWithoutInk(uv, surface);
    float2 inkUV = (float2(1-uv.x, uv.y) - _InkRect.xy) / _InkRect.zw;
    half4 ink = SAMPLE_TEXTURE2D(_InkMap, sampler_InkMap, inkUV);
    half inside = step(0,inkUV.x)*step(inkUV.x,1)*step(0,inkUV.y)*step(inkUV.y,1);
    surface.albedo = lerp(surface.albedo, ink.rgb, ink.a * inside);
}
