// Camera-facing light flare. Each camera sees it turned toward itself, and it
// brightens as the camera looks straight at it. Uses a procedural glow, or
// the Texture (e.g. a white light PNG) when Use Texture is on.
Shader "Dacha/LightFlare"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.97, 0.92, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2
        _Size ("Size (m)", Float) = 6
        [Toggle] _UseTexture ("Use Texture", Float) = 0
        _MainTex ("Texture (white light PNG)", 2D) = "white" {}
        _RayStrength ("Ray Strength", Range(0, 2)) = 0.6
        _Spin ("Spin Speed", Float) = 0.05
        _LookPower ("Look-At Sharpness", Range(1, 64)) = 8
        _MinBrightness ("Brightness When Not Looking", Range(0, 1)) = 0.15
        _PushToCamera ("Push Toward Camera (m)", Float) = 2
        [HideInInspector] _Visibility ("Visibility", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Flare"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always // never clipped by the ceiling; FlareOcclusion hides it behind walls
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
                float _Size;
                half _UseTexture;
                float4 _MainTex_ST;
                half _RayStrength;
                float _Spin;
                half _LookPower;
                half _MinBrightness;
                float _PushToCamera;
                half _Visibility;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float look : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Billboard: build the quad in view space around the object's center.
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 toFlare = normalize(centerWS - _WorldSpaceCameraPos);
                centerWS -= toFlare * _PushToCamera; // keep it from clipping into the ceiling
                float3 centerVS = TransformWorldToView(centerWS);

                float a = _Time.y * _Spin * 6.2831853;
                float2 corner = v.positionOS.xy;
                corner = float2(corner.x * cos(a) - corner.y * sin(a), corner.x * sin(a) + corner.y * cos(a));
                o.positionCS = TransformWViewToHClip(centerVS + float3(corner * _Size, 0));
                o.uv = v.uv;

                // Brighter the more directly the camera looks at it.
                float3 camForward = -UNITY_MATRIX_V[2].xyz;
                o.look = lerp(_MinBrightness, 1.0, pow(saturate(dot(camForward, toFlare)), _LookPower));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 col;
                if (_UseTexture > 0.5)
                {
                    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(i.uv, _MainTex));
                    col = tex.rgb * tex.a;
                }
                else
                {
                    float2 p = i.uv * 2.0 - 1.0;
                    float d = length(p);
                    half core = smoothstep(0.22, 0.0, d);
                    half halo = exp(-d * 5.0) * 0.6;
                    half rays = (pow(saturate(1.0 - abs(p.y) * 30.0), 2.0) * saturate(1.0 - abs(p.x))
                               + pow(saturate(1.0 - abs(p.x) * 30.0), 2.0) * saturate(1.0 - abs(p.y))) * _RayStrength;
                    col = (core + halo + rays) * saturate(1.0 - d);
                }
                return half4(col * _Color.rgb * _Intensity * i.look * _Visibility, 1);
            }
            ENDHLSL
        }
    }
}
