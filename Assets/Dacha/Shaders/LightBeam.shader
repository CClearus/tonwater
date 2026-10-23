// Fake volumetric light cone (a "god ray"). Additive and soft: fades at the
// cone's edges, near the tip and toward the far end, with faint drifting dust.
Shader "Dacha/LightBeam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.96, 0.9, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.06
        _EdgeSoftness ("Edge Softness", Range(0.5, 8)) = 2.5
        _TipFade ("Tip Fade", Range(0.001, 0.5)) = 0.08
        _EndFade ("End Fade", Range(0.001, 1)) = 0.35
        _DustStrength ("Dust Strength", Range(0, 1)) = 0.35
        _DustScale ("Dust Scale", Float) = 1.5
        _DustSpeed ("Dust Speed", Float) = 0.15
        [Header(Own Beam)]
        _LookPower ("Look-Up Sharpness", Range(1, 32)) = 6
        _LookMin ("Visibility When Not Looking Up", Range(0, 1)) = 0
        _OwnStrength ("Own Beam Strength", Range(0, 1)) = 0.3
        [Header(Near Camera)]
        _NearFadeStart ("Near Fade Start (m)", Float) = 0.5
        _NearFadeEnd ("Near Fade End (m)", Float) = 5
        [HideInInspector] _Brightness ("Brightness", Float) = 1
        [HideInInspector] _IsOwnBeam ("Is Own Beam", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgeSoftness;
                half _TipFade;
                half _EndFade;
                half _DustStrength;
                float _DustScale;
                float _DustSpeed;
                half _LookPower;
                half _LookMin;
                half _OwnStrength;
                float _NearFadeStart;
                float _NearFadeEnd;
                half _Brightness;
                half _IsOwnBeam;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float along : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.along = v.uv.y; // 0 at the light, 1 at the far end
                return o;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash13(i), Hash13(i + float3(1, 0, 0)), f.x),
                         lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), f.x),
                         lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // Facing the camera = middle of the beam (bright); grazing = edge (dark).
                half facing = pow(abs(dot(normalize(i.normalWS), v)), _EdgeSoftness);
                half along = smoothstep(0.0, _TipFade, i.along) * (1.0 - smoothstep(1.0 - _EndFade, 1.0, i.along));
                // Brighter near the light.
                along *= lerp(1.0, 0.45, i.along);

                float3 dustPos = i.positionWS * _DustScale + float3(0, -_Time.y * _DustSpeed, _Time.y * _DustSpeed * 0.5);
                half dust = lerp(1.0, Noise3(dustPos) * 1.6, _DustStrength);

                // Fade the beam right in front of the camera (no fog in your face).
                half nearFade = smoothstep(_NearFadeStart, _NearFadeEnd, distance(i.positionWS, _WorldSpaceCameraPos));

                // From inside a cone (your own beam, or standing in a teammate's) the beam
                // surrounds you, so only show it when looking up toward the light.
                // The mesh is a unit cone (radius = z), so test the camera in object space.
                float3 camOS = TransformWorldToObject(_WorldSpaceCameraPos);
                half inside = smoothstep(-0.03, 0.03, camOS.z - length(camOS.xy)) * step(0.0, camOS.z) * step(camOS.z, 1.0);
                half surround = max(inside, _IsOwnBeam);
                float3 lightPos = TransformObjectToWorld(float3(0, 0, 0));
                float3 camForward = -UNITY_MATRIX_V[2].xyz;
                half look = pow(saturate(dot(camForward, normalize(lightPos - _WorldSpaceCameraPos))), _LookPower);
                half ownFade = lerp(1.0, lerp(_LookMin, 1.0, look) * _OwnStrength, surround);

                half3 col = _Color.rgb * (_Intensity * _Brightness * facing * along * dust * nearFade * ownFade);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
