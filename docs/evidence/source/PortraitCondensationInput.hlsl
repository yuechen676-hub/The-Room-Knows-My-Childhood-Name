#define InitializeStandardLitSurfaceData InitializeDryPortrait
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData
float _MistAmount, _RainProgress;

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    InitializeDryPortrait(uv, surface);
    float2 face = (uv-float2(.388,.719))/float2(.115,.095);
    half faceMist = exp(-dot(face,face)*1.25);
    half mist = _MistAmount * saturate(.075 + faceMist*.88);
    half trace = 0, glint = 0;
    for (int drop = 0; drop < 3; drop++)
    {
        float x = drop == 0 ? .367 : drop == 1 ? .419 : .655;
        float top = drop == 2 ? .85 : .735;
        float head = top - _RainProgress * (drop == 1 ? .32 : .43);
        float curve = x + sin(uv.y*29 + drop*3)*.002;
        float tail = smoothstep(head-.008,head+.005,uv.y) * (1-smoothstep(top-.015,top+.01,uv.y));
        tail *= smoothstep(0,.1,_RainProgress);
        float dx = abs(uv.x-curve);
        trace += (1-smoothstep(.0015,.0045,dx))*tail;
        glint += (1-smoothstep(.0004,.0012,abs(uv.x-curve-.003)))*tail*.13;
    }
    trace = saturate(trace);
    float radius = .025 * mist;
    half3 blurred = 0;
    for(int tap=0; tap<12; tap++)
    {
        float angle = tap*2.399963;
        float2 offset = float2(cos(angle),sin(angle)) * sqrt((tap+.5)/12.0)*radius;
        blurred += SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv+offset).rgb;
    }
    blurred = blurred/12 * _BaseColor.rgb;
    half3 fogged = lerp(blurred,half3(.60,.57,.50),mist*.58);
    surface.albedo = lerp(surface.albedo,fogged,saturate(_MistAmount));
    half3 clear = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv+float2(.002*trace,0)).rgb * _BaseColor.rgb;
    surface.albedo = lerp(surface.albedo,clear,trace*.7) + glint;
    surface.smoothness = lerp(surface.smoothness,.48,_MistAmount*.45 + trace*.55);
}
