// Faceted, translucent crystal for URP. Self-lit with a fake studio
// reflection so it reads well in dark scenes without any scene lighting.
Shader "Dacha/Crystal"
{
    Properties
    {
        _Color ("Bright Color", Color) = (0.78, 0.86, 0.92, 1)
        _DeepColor ("Deep Color", Color) = (0.10, 0.13, 0.17, 1)
        _EdgeColor ("Edge Glow", Color) = (0.70, 0.88, 1.00, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.7
        _Brightness ("Brightness", Range(0, 3)) = 1.1
        _FacetContrast ("Facet Contrast", Range(0, 1)) = 0.6
        _FresnelPower ("Edge Glow Falloff", Range(0.5, 8)) = 3
        _HighlightStrength ("Highlight Strength", Range(0, 5)) = 1.2
        _HighlightSharpness ("Highlight Sharpness", Range(4, 256)) = 48
        _FrostScale ("Frost Scale", Float) = 12
        _FrostStrength ("Frost Strength", Range(0, 1)) = 0.2
        _InnerFacets ("Inner Facets", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _DeepColor;
            half4 _EdgeColor;
            half _Opacity;
            half _Brightness;
            half _FacetContrast;
            half _FresnelPower;
            half _HighlightStrength;
            half _HighlightSharpness;
            half _FrostScale;
            half _FrostStrength;
            half _InnerFacets;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float fogFactor : TEXCOORD1;
            // Constant across each triangle so every facet is perfectly flat.
            nointerpolation float3 normalWS : TEXCOORD2;
            nointerpolation float3 normalOS : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.fogFactor = ComputeFogFactor(o.positionCS.z);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.normalOS = normalize(v.normalOS);
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

        half4 Shade(Varyings i, half layer)
        {
            // Flat per-facet normal from the mesh (inner facets face inward).
            float3 n = normalize(i.normalWS);
            float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
            if (dot(n, v) < 0) n = -n;

            // Fake studio environment (bright above, deep below), with every
            // facet pushed toward its own shade so they read as a cut gem.
            float3 r = reflect(-v, n);
            float envT = smoothstep(0.15, 0.85, r.y * 0.5 + 0.5);
            // Object-space so a facet keeps its shade when the sensor rotates.
            float facet = Hash13(floor(i.normalOS * 20.0 + 0.5));
            half3 col = lerp(_DeepColor.rgb, _Color.rgb, lerp(envT, facet, _FacetContrast));

            // Frosted texture.
            float frost = Noise3(i.positionWS * _FrostScale) * 0.65 + Noise3(i.positionWS * _FrostScale * 2.7) * 0.35;
            col *= 1.0 - _FrostStrength + _FrostStrength * 2.0 * frost;
            col *= _Brightness;

            // Edge glow and two studio highlights.
            float fresnel = pow(1.0 - saturate(dot(n, v)), _FresnelPower);
            float spec = pow(saturate(dot(r, normalize(float3(0.4, 0.8, 0.45)))), _HighlightSharpness)
                       + 0.6 * pow(saturate(dot(r, normalize(float3(-0.6, 0.35, 0.7)))), _HighlightSharpness);
            col += _EdgeColor.rgb * fresnel;
            col += spec * _HighlightStrength;

            half alpha = saturate(_Opacity + fresnel * 0.35 + spec * 0.5);
            col *= layer;
            alpha *= layer;
            col = MixFog(col, i.fogFactor);
            return half4(col, alpha);
        }
        ENDHLSL

        // Inner (back) facets, seen through the front.
        Pass
        {
            Name "InnerFacets"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            half4 frag(Varyings i) : SV_Target { return Shade(i, _InnerFacets); }
            ENDHLSL
        }

        // Outer facets.
        Pass
        {
            Name "OuterFacets"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            half4 frag(Varyings i) : SV_Target { return Shade(i, 1.0); }
            ENDHLSL
        }
    }
}
