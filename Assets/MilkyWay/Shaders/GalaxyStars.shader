Shader "MilkyWay/GalaxyStars"
{
    // One hundred thousand stars in a single draw call: GalaxyStarField bakes
    // them into one mesh of quads (centre position + corner offset in UV0,
    // colour and size per vertex), and this shader billboards each quad.
    //
    // Additive and drawn BEFORE the galaxy volume (queue 2900 vs 3000), so the
    // volume's dust attenuates the starlight behind it — approximately, by the
    // whole ray's transmittance, which is the right look for a disk seen from
    // outside and near-exact from inside where nearby space is clear.
    Properties
    {
        _StarBrightness("Star Brightness", Range(0.0, 6.0)) = 1.6
        _SizeScale("Size Scale", Range(0.2, 4.0)) = 1.0
        [Header(Camera proximity)]
        _NearFade("Fade Radius (kpc)", Range(0.005, 1.0)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "GalaxyStars"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _StarBrightness, _SizeScale, _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // star CENTRE (same for all 4 corners)
                float4 color : COLOR;           // temperature colour, a = luminosity jitter
                float2 corner : TEXCOORD0;      // quad corner in [-1, 1]
                float2 sizeRand : TEXCOORD1;    // x = world size (kpc), y = per-star random
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : TEXCOORD0;
                float2 corner : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 centreWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 toCam = _WorldSpaceCameraPos - centreWS;
                float dist = length(toCam);

                // Billboard basis in view space.
                float3 fwd = toCam / max(dist, 1e-5);
                float3 upRef = abs(fwd.y) > 0.98 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 right = normalize(cross(upRef, fwd));
                float3 up = cross(fwd, right);

                // World size clamped from BOTH ends. Floor: ~1.5 px, or the star
                // flickers as it rasterises below a pixel. Ceiling: a fixed
                // ANGULAR size — stars have world sizes up to 0.07 kpc, and from
                // inside the galaxy a nearby one would otherwise fill 10 degrees
                // of sky as a giant snowball. Outside views never hit the cap.
                float sizeReal = min(v.sizeRand.x * _SizeScale, dist * 0.0045);
                float m11 = max(abs(UNITY_MATRIX_P._m11), 1e-3);   // cot(fov/2)
                float worldPerPixel = 2.0 * dist / (m11 * _ScreenParams.y);
                float size = max(sizeReal, worldPerPixel * 1.5);
                // Energy conservation: when a sub-pixel star is inflated to the
                // 1.5 px floor its area grows, so its flux must shrink by the
                // same ratio. Without this, tens of thousands of clamped far
                // stars each over-contribute and the whole sky washes grey.
                float energy = (sizeReal * sizeReal) / (size * size);

                // Approaching a star, fade it out instead of flying through a
                // huge glowing quad (the fly-through case of the zoom journey).
                float near = saturate(dist / _NearFade - 1.0);

                float3 ws = centreWS + (right * v.corner.x + up * v.corner.y) * size;
                o.positionHCS = TransformWorldToHClip(ws);
                o.color = float4(v.color.rgb * (0.35 + 1.3 * v.color.a) * near * energy, v.sizeRand.y);
                o.corner = v.corner;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float d2 = dot(i.corner, i.corner);
                // Tight gaussian only. An in-sprite halo looked nice per star,
                // but thousands of overlapping halos summed into a grey fog on
                // the sky from inside the disk — bloom supplies the glow now.
                float a = exp(-d2 * 6.0);
                a *= saturate(1.0 - d2);       // hard zero at the quad edge
                return half4(i.color.rgb * a * _StarBrightness, 0.0);
            }
            ENDHLSL
        }
    }
}
