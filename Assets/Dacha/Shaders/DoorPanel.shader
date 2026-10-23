// Exit door panel: the whole front face is an LED screen (R/G/B subpixels,
// same look as the status screen) showing a filled door shape. The shape is
// placed by _IconCenter in the panel's own space, so on a split door each half
// shows its part. Put it on a cube; the -Z face is the front.
Shader "Dacha/DoorPanel"
{
    Properties
    {
        _BaseColor ("Background", Color) = (0.006, 0.006, 0.008, 1)
        [HDR] _IconColor ("Door Shape Color", Color) = (0.45, 0.47, 0.5, 1)
        _IconCenter ("Door Shape Center (m, panel space)", Vector) = (0, 0, 0, 0)
        _IconSize ("Door Shape Size (m)", Vector) = (3.9, 7.2, 0, 0)
        _PixelSize ("LED Pixel Size (m)", Float) = 0.14
        _SubpixelStrength ("RGB Subpixel Strength", Range(0, 1)) = 1
        _OffPixelGlow ("Unlit Pixel Glow", Range(0, 0.1)) = 0.004
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Panel"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _IconColor;
                float4 _IconCenter;
                float4 _IconSize;
                float _PixelSize;
                half _SubpixelStrength;
                half _OffPixelGlow;
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
                float2 meters : TEXCOORD0; // position on the front face relative to the panel center, in meters
                float front : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float4x4 m = GetObjectToWorldMatrix();
                o.meters = v.positionOS.xy * float2(length(m._m00_m10_m20), length(m._m01_m11_m21));
                o.front = v.normalOS.z < -0.5 ? 1 : 0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 col = _BaseColor.rgb;
                if (i.front < 0.5) return half4(col, 1);

                // LED grid anchored on the shape center so pixels line up across door halves.
                float2 p = i.meters - _IconCenter.xy;
                float2 grid = p / _PixelSize;
                float2 f = frac(grid);
                float2 pixelCenter = (floor(grid) + 0.5) * _PixelSize;

                // Filled door shape.
                float2 halfSize = _IconSize.xy * 0.5;
                float inside = all(abs(pixelCenter) < halfSize) ? 1 : 0;
                half3 content = _IconColor.rgb * inside;

                float sub = f.x * 3.0;
                float3 stripe = float3(sub < 1.0, sub >= 1.0 && sub < 2.0, sub >= 2.0);
                float s = frac(sub);
                float gaps = smoothstep(0.0, 0.15, s) * smoothstep(1.0, 0.85, s)
                           * smoothstep(0.0, 0.12, f.y) * smoothstep(1.0, 0.88, f.y);
                float3 mask = stripe * gaps * 3.0;
                // Blend to the average once subpixels get too small on screen (no shimmer).
                float footprint = max(fwidth(grid.x), fwidth(grid.y));
                mask = lerp(mask, 0.75, smoothstep(0.1, 0.3, footprint));
                mask = lerp(1.0, mask, _SubpixelStrength);

                col += (content + _OffPixelGlow) * mask;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
