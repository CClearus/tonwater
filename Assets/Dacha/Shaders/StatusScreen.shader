// Exit-door status screen: one panel with an outer frame split into cells, each
// showing a cross (not done, red) or tick (done, blue). Rendered as an LED
// screen: content is drawn on a grid of LED pixels made of R, G, B subpixels.
// Put it on a cube; the -Z face is the screen.
Shader "Dacha/StatusScreen"
{
    Properties
    {
        [HDR] _NotDoneColor ("Not Done Color", Color) = (2.2, 0.02, 0.02, 1)
        [HDR] _DoneColor ("Done Color", Color) = (0.15, 0.7, 2.4, 1)
        _FillColor ("Background", Color) = (0.006, 0.006, 0.008, 1)
        _CellCount ("Cell Count", Range(1, 8)) = 3
        _FrameWidth ("Frame Width (m)", Float) = 0.07
        _IconSize ("Icon Size", Range(0.2, 1)) = 0.55
        _IconWidth ("Icon Line Width (m)", Float) = 0.07
        _PixelsHigh ("LED Pixels (vertical)", Float) = 40
        _SubpixelStrength ("RGB Subpixel Strength", Range(0, 1)) = 1
        _OffPixelGlow ("Unlit Pixel Glow", Range(0, 0.1)) = 0.012
        [HideInInspector] _Done0 ("Done 0-3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Done1 ("Done 4-7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Power0 ("Power 0-3", Vector) = (1, 1, 1, 1)
        [HideInInspector] _Power1 ("Power 4-7", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Screen"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _NotDoneColor;
                half4 _DoneColor;
                half4 _FillColor;
                float _CellCount;
                float _FrameWidth;
                float _IconSize;
                float _IconWidth;
                float _PixelsHigh;
                half _SubpixelStrength;
                half _OffPixelGlow;
                float4 _Done0;
                float4 _Done1;
                float4 _Power0;
                float4 _Power1;
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
                float2 meters : TEXCOORD0;   // position on the screen face, in meters
                float2 size : TEXCOORD1;     // screen width/height in meters
                float front : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float4x4 m = GetObjectToWorldMatrix();
                o.size = float2(length(m._m00_m10_m20), length(m._m01_m11_m21));
                o.meters = (v.positionOS.xy + 0.5) * o.size;
                o.front = v.normalOS.z < -0.5 ? 1 : 0;
                return o;
            }

            float CellValue(float4 a, float4 b, int i)
            {
                float4 v = i < 4 ? a : b;
                int k = i & 3;
                return k == 0 ? v.x : k == 1 ? v.y : k == 2 ? v.z : v.w;
            }

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // What the screen shows at a point (sampled once per LED pixel).
            half3 Content(float2 pos, float2 size)
            {
                int n = max(1, (int)round(_CellCount));
                float cw = size.x / n;
                int ci = clamp((int)floor(pos.x / cw), 0, n - 1);
                float done = CellValue(_Done0, _Done1, ci);
                float power = CellValue(_Power0, _Power1, ci);
                half3 color = lerp(_NotDoneColor.rgb, _DoneColor.rgb, saturate(done)) * power;

                // Outer frame and dividers (each cell draws its own half of a divider).
                float lx = pos.x - ci * cw;
                float edge = min(min(lx, cw - lx), min(pos.y, size.y - pos.y));
                float frame = edge < _FrameWidth ? 1 : 0;

                // Cross or tick, centered in the cell.
                float iconSize = min(cw, size.y) * _IconSize;
                float2 p = (float2(lx, pos.y) - float2(cw, size.y) * 0.5) / iconSize + 0.5;
                float d = done > 0.5
                    ? min(SegmentDistance(p, float2(0.18, 0.52), float2(0.40, 0.28)),
                          SegmentDistance(p, float2(0.40, 0.28), float2(0.84, 0.76)))
                    : min(SegmentDistance(p, float2(0.2, 0.2), float2(0.8, 0.8)),
                          SegmentDistance(p, float2(0.2, 0.8), float2(0.8, 0.2)));
                float icon = d < _IconWidth / iconSize ? 1 : 0;

                return color * saturate(frame + icon);
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (i.front < 0.5) return half4(_FillColor.rgb, 1); // sides/back: plain dark

                float pitch = i.size.y / _PixelsHigh;
                float2 grid = i.meters / pitch;
                float2 f = frac(grid);
                float2 pixelCenter = (floor(grid) + 0.5) * pitch;
                half3 content = Content(pixelCenter, i.size);

                // R, G, B vertical stripes with dark gaps, plus a gap between rows.
                float sub = f.x * 3.0;
                float3 stripe = float3(sub < 1.0, sub >= 1.0 && sub < 2.0, sub >= 2.0);
                float s = frac(sub);
                float gaps = smoothstep(0.0, 0.15, s) * smoothstep(1.0, 0.85, s)
                           * smoothstep(0.0, 0.12, f.y) * smoothstep(1.0, 0.88, f.y);
                float3 mask = stripe * gaps * 3.0;

                // Subpixel stripes are a third of an LED pixel wide; once they get
                // smaller than ~2 screen pixels, blend to their average to avoid shimmer.
                float footprint = max(fwidth(grid.x), fwidth(grid.y));
                mask = lerp(mask, 0.75, smoothstep(0.1, 0.3, footprint));
                mask = lerp(1.0, mask, _SubpixelStrength);

                half3 col = _FillColor.rgb + (content + _OffPixelGlow) * mask;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
