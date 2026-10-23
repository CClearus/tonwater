// Soft additive glow for wide LineRenderers: bright along the middle of the
// line, fading to nothing at its edges. Use as a halo behind a thin glow line.
Shader "Dacha/GlowSoft"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.6, 0.02, 0.02, 1)
        _Intensity ("Intensity", Float) = 1
        _Falloff ("Edge Falloff", Range(0.5, 6)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GlowSoft"
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
                half _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // LineRenderer: uv.y runs 0..1 across the line's width.
                half across = 1.0 - abs(i.uv.y * 2.0 - 1.0);
                half3 col = _Color.rgb * i.color.rgb * _Intensity * pow(saturate(across), _Falloff);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
