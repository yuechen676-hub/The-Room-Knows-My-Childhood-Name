Shader "Room/Interaction White Outline"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+51" }
        Pass
        {
            Cull Front ZWrite Off ZTest LEqual
            Stencil { Ref 64 ReadMask 64 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; };
            V vert(A v)
            {
                V o;
                float3 world=TransformObjectToWorld(v.positionOS.xyz);
                float3 normal=TransformObjectToWorldNormal(v.normalOS);
                o.positionCS=TransformWorldToHClip(world);
                float4 tip=TransformWorldToHClip(world+normal*.01);
                float2 direction=tip.xy/max(abs(tip.w),.0001)-o.positionCS.xy/max(abs(o.positionCS.w),.0001);
                direction*= _ScreenParams.xy;
                direction/=max(length(direction),.0001);
                o.positionCS.xy+=direction*2.0*2.0/_ScreenParams.xy*o.positionCS.w;
                return o;
            }
            half4 frag(V i):SV_Target { return half4(.95,.95,.93,1); }
            ENDHLSL
        }
    }
}
