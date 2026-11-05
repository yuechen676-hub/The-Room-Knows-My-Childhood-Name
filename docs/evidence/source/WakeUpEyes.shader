Shader "Room/Waking Eyes"
{
    Properties
    {
        _MainTex("Live room", 2D)="black" {}
        _Open("Eye opening", Float)=0
        _Blur("Focus blur", Float)=14
        _Exposure("Adapting to light", Float)=.55
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Open, _Blur, _Exposure;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.vertex=UnityObjectToClipPos(i.vertex); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target
            {
                float x=i.uv.x*2-1;
                float aperture=_Open*(.53-.22*pow(abs(x),1.6));
                float y=abs(i.uv.y-.5 + .003*sin(x*8)*saturate(1-_Open));
                float reveal=smoothstep(0,.012,aperture-y);
                if(reveal<=0) return half4(0,0,0,1);
                half3 color=0;
                float total=0;
                [unroll] for(int a=-1;a<=1;a++)
                [unroll] for(int b=-1;b<=1;b++)
                {
                    float weight=exp(-.5*(a*a+b*b));
                    color+=tex2Dlod(_MainTex,float4(saturate(i.uv+float2(a,b)*_MainTex_TexelSize.xy*_Blur*.35),0,_Blur*.28)).rgb*weight;
                    total+=weight;
                }
                color=color/total*_Exposure;
                return half4(color*reveal,1);
            }
            ENDHLSL
        }
    }
}
