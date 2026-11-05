// Procedural night sky for The Room Knows My Childhood Name.
// Replaces the third-party AllSky equirect panorama so the sky can be driven
// per narrative stage. Every layer is generated from the view direction; there
// are no sky textures. Layer order is gradient -> horizon glow -> stars ->
// moon -> clouds -> rain veil, so clouds correctly occlude stars and the moon.
Shader "Room/Procedural Night Sky"
{
    Properties
    {
        [Header(Gradient)]
        _HorizonColor ("Horizon", Color) = (0.22, 0.14, 0.11, 1)
        _ZenithColor ("Zenith", Color) = (0.035, 0.05, 0.11, 1)
        _GroundColor ("Below horizon", Color) = (0.02, 0.018, 0.016, 1)
        _HorizonHeight ("Horizon height", Range(-0.5, 0.5)) = 0
        _HorizonSoftness ("Horizon softness", Range(0.01, 1)) = 0.35
        _HorizonFalloff ("Horizon falloff", Range(0.2, 4)) = 1.2

        [Header(Horizon glow)]
        [HDR] _GlowColor ("Glow colour", Color) = (0.30, 0.18, 0.10, 1)
        _GlowIntensity ("Glow intensity", Range(0, 4)) = 0.35
        _GlowHeight ("Glow height", Range(0.02, 1)) = 0.18
        _GlowFalloff ("Glow falloff", Range(0.5, 6)) = 2
        _GlowDirection ("Glow direction", Vector) = (0, 0, 1, 0)
        _GlowDirectionality ("Glow directionality", Range(0, 8)) = 0

        [Header(Stars)]
        _StarDensity ("Density", Range(0, 1)) = 0.55
        _StarBrightness ("Brightness", Range(0, 4)) = 1.6
        _StarSize ("Size", Range(0.5, 4)) = 1
        _StarTwinkle ("Twinkle", Range(0, 1)) = 0.35
        _StarTwinkleSpeed ("Twinkle speed", Range(0, 8)) = 2
        [HDR] _StarColor ("Colour", Color) = (0.85, 0.88, 1, 1)

        [Header(Moon)]
        _MoonDirection ("Direction", Vector) = (0.3, 0.45, 0.84, 0)
        _MoonSize ("Angular size", Range(0.005, 0.2)) = 0.055
        _MoonPhase ("Phase (0 new, 1 full)", Range(0, 1)) = 1
        [HDR] _MoonColor ("Colour", Color) = (1, 0.96, 0.88, 1)
        _MoonIntensity ("Intensity", Range(0, 6)) = 2.2
        _MoonSurface ("Surface detail", Range(0, 1)) = 0.35
        [HDR] _MoonHaloColor ("Halo colour", Color) = (0.70, 0.72, 0.80, 1)
        _MoonHaloSize ("Halo size", Range(0.02, 1.5)) = 0.35
        _MoonHaloIntensity ("Halo intensity", Range(0, 3)) = 0.55

        [Header(Clouds)]
        _CloudCoverage ("Coverage", Range(0, 1)) = 0.32
        _CloudSoftness ("Softness", Range(0.02, 1)) = 0.22
        _CloudScale ("Scale", Range(0.1, 8)) = 1
        _CloudSpeed ("Speed", Range(0, 0.2)) = 0.006
        _CloudDrift ("Drift direction", Vector) = (1, 0.25, 0, 0)
        _CloudColor ("Shadow colour", Color) = (0.10, 0.10, 0.13, 1)
        _CloudLitColor ("Lit colour", Color) = (0.55, 0.53, 0.50, 1)
        _CloudLighting ("Moon lighting", Range(0, 1)) = 0.8
        _CloudOpacity ("Opacity", Range(0, 1)) = 0.85

        [Header(Rain veil)]
        _RainAmount ("Amount", Range(0, 1)) = 0
        _RainColor ("Colour", Color) = (0.115, 0.12, 0.135, 1)

        [Header(Output)]
        _Exposure ("Exposure", Range(0, 4)) = 1
        _SkyTint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half4 _GroundColor;
                float _HorizonHeight;
                float _HorizonSoftness;
                float _HorizonFalloff;

                half4 _GlowColor;
                float _GlowIntensity;
                float _GlowHeight;
                float _GlowFalloff;
                float4 _GlowDirection;
                float _GlowDirectionality;

                float _StarDensity;
                float _StarBrightness;
                float _StarSize;
                float _StarTwinkle;
                float _StarTwinkleSpeed;
                half4 _StarColor;

                float4 _MoonDirection;
                float _MoonSize;
                float _MoonPhase;
                half4 _MoonColor;
                float _MoonIntensity;
                float _MoonSurface;
                half4 _MoonHaloColor;
                float _MoonHaloSize;
                float _MoonHaloIntensity;

                float _CloudCoverage;
                float _CloudSoftness;
                float _CloudScale;
                float _CloudSpeed;
                float4 _CloudDrift;
                half4 _CloudColor;
                half4 _CloudLitColor;
                float _CloudLighting;
                float _CloudOpacity;

                float _RainAmount;
                half4 _RainColor;

                float _Exposure;
                half4 _SkyTint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            // Cheap hashes. Constants are the usual "hash without sine" set: they
            // only need to decorrelate, not to be statistically rigorous.
            float Hash21(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            // Four octaves is the measured sweet spot for this scene: the sky is
            // only visible through the window, but the planar mirrors re-render
            // it, so every octave is paid for more than once.
            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }
                return value;
            }

            // Stars live in a 3D cell grid sampled on the unit sphere. Each cell
            // either holds one jittered star or none, so density is a threshold
            // rather than a brightness fade.
            float StarField(float3 direction)
            {
                float3 p = direction * 220.0;
                float3 cell = floor(p);
                float3 local = frac(p) - 0.5;

                float pick = Hash31(cell);
                float exists = step(1.0 - _StarDensity, pick);

                float3 jitter = float3(Hash31(cell + 13.7), Hash31(cell + 41.3), Hash31(cell + 73.9)) - 0.5;
                float distance = length(local - jitter * 0.7);
                float core = saturate(1.0 - distance / max(0.09 * _StarSize, 1e-4));

                float star = exists * pow(core, 6.0);
                float phase = Hash31(cell + 5.5) * 6.2831853;
                star *= lerp(1.0, 0.45 + 0.55 * sin(_Time.y * _StarTwinkleSpeed + phase), _StarTwinkle);
                return star;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // The skybox mesh's object space is the view direction; the
                // object-to-world rotation carries RenderSettings skybox rotation.
                output.directionWS = TransformObjectToWorldDir(input.positionOS.xyz, false);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.directionWS);
                float height = direction.y;

                // --- Gradient -------------------------------------------------
                float softness = max(_HorizonSoftness, 1e-4);
                float above = pow(saturate((height - _HorizonHeight) / softness), _HorizonFalloff);
                float below = saturate((_HorizonHeight - height) / softness);

                float3 colour = _HorizonColor.rgb;
                colour = lerp(colour, _ZenithColor.rgb, above);
                colour = lerp(colour, _GroundColor.rgb, below);

                // --- Horizon glow --------------------------------------------
                float band = pow(saturate(1.0 - abs(height - _HorizonHeight) / max(_GlowHeight, 1e-4)), _GlowFalloff);
                float3 flatDirection = normalize(float3(direction.x, 0, direction.z) + 1e-5);
                float3 flatGlow = normalize(float3(_GlowDirection.x, 0, _GlowDirection.z) + 1e-5);
                // Directionality 0 leaves the band omnidirectional.
                float facing = pow(saturate(dot(flatDirection, flatGlow) * 0.5 + 0.5), _GlowDirectionality);
                colour += _GlowColor.rgb * _GlowIntensity * band * facing;

                // --- Stars ----------------------------------------------------
                float skyMask = smoothstep(_HorizonHeight, _HorizonHeight + 0.10, height);
                colour += _StarColor.rgb * _StarBrightness * StarField(direction) * skyMask;

                // --- Moon -----------------------------------------------------
                float3 moonDirection = normalize(_MoonDirection.xyz + 1e-5);
                float3 moonRight = normalize(cross(float3(0, 1, 0), moonDirection) + float3(1e-5, 0, 0));
                float3 moonUp = cross(moonDirection, moonRight);
                float alongMoon = dot(direction, moonDirection);

                float2 moonUV = float2(dot(direction, moonRight), dot(direction, moonUp)) / max(_MoonSize, 1e-4);
                float moonRadius = length(moonUV);
                float disc = (1.0 - smoothstep(0.88, 1.0, moonRadius)) * step(0.0, alongMoon);

                // Terminator is the projection of the lit hemisphere's edge, so
                // phase 0 is fully dark, 0.5 is half lit and 1 is fully lit.
                float phaseK = _MoonPhase * 2.0 - 1.0;
                float terminator = phaseK * sqrt(saturate(1.0 - moonUV.y * moonUV.y));
                float lit = smoothstep(terminator + 0.10, terminator - 0.10, moonUV.x);
                // Two scales of blotching: broad maria plus finer mottling. The
                // disc has to stay below 1 for any of it to survive, because the
                // room's post stack has tonemapping set to None and clips hard.
                float maria = Fbm(moonUV * 1.1 + 7.0);
                float mottle = Fbm(moonUV * 4.0 + 23.0);
                float surface = 1.0 - _MoonSurface * (0.62 * maria + 0.22 * mottle);
                colour += _MoonColor.rgb * _MoonIntensity * disc * lit * surface;

                float angle = acos(clamp(alongMoon, -1.0, 1.0));
                float halo = pow(saturate(1.0 - angle / max(_MoonHaloSize, 1e-4)), 3.0);
                colour += _MoonHaloColor.rgb * _MoonHaloIntensity * halo;

                // --- Clouds ---------------------------------------------------
                // Project the direction onto a plane above the horizon. The clamp
                // on the denominator keeps the projection finite at grazing angles.
                float denominator = max(height - _HorizonHeight, 0.20);
                float2 cloudUV = (direction.xz / denominator) * _CloudScale;
                cloudUV += _Time.y * _CloudSpeed * normalize(_CloudDrift.xy + 1e-5);

                float shape = Fbm(cloudUV);
                float coverage = saturate((shape - (1.0 - _CloudCoverage)) / max(_CloudSoftness, 1e-4));
                coverage *= smoothstep(_HorizonHeight + 0.10, _HorizonHeight + 0.40, height);

                float moonNear = pow(saturate(alongMoon * 0.5 + 0.5), 6.0);
                float3 cloudColour = lerp(_CloudColor.rgb, _CloudLitColor.rgb, moonNear * _CloudLighting);
                colour = lerp(colour, cloudColour, coverage * _CloudOpacity);

                // --- Rain veil ------------------------------------------------
                // Atmospheric wash only. Raindrops belong on the window glass,
                // not in a skybox, where they would sit at infinity with no parallax.
                float veil = saturate(1.0 - abs(height) * 0.35);
                colour = lerp(colour, _RainColor.rgb, _RainAmount * veil);

                return half4(colour * _Exposure * _SkyTint.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
