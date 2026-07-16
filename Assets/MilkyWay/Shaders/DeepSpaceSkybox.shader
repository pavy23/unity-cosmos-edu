Shader "MilkyWay/DeepSpaceSkybox"
{
    // The sky OUTSIDE a galaxy: black, a thin scatter of foreground stars and
    // a few faint smudges of distant galaxies. Deliberately not the black-hole
    // project's skybox — that one paints its own Milky-Way band (we ARE the
    // Milky Way here, as geometry).
    Properties
    {
        _StarDensity("Star Density", Range(0.0, 1.0)) = 0.35
        _GalaxyCount("Distant Galaxies", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _StarDensity, _GalaxyCount;
            CBUFFER_END

            float ds_hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            // Point stars from a direction-space grid: one candidate per cell,
            // shown only when the ray passes within its tiny angular radius.
            float3 pointStars(float3 rd, float cells, float threshold, float sizeMul)
            {
                float3 g = rd * cells;
                float3 cell = floor(g);
                float3 col = 0.0;
                float h = ds_hash(cell);
                if (h < threshold)
                {
                    float3 centre = cell + 0.5 + (float3(ds_hash(cell + 1.3),
                                                          ds_hash(cell + 2.7),
                                                          ds_hash(cell + 4.1)) - 0.5) * 0.7;
                    float d = length(g - centre);
                    float radius = 0.045 * sizeMul * (0.5 + ds_hash(cell + 6.9));
                    float star = exp(-d * d / (radius * radius));
                    float warm = ds_hash(cell + 9.2);
                    float3 tint = lerp(float3(0.75, 0.82, 1.0), float3(1.0, 0.88, 0.72), warm);
                    col = star * tint * (0.25 + 0.75 * ds_hash(cell + 12.4));
                }
                return col;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 rd = normalize(i.dir);

                // Two star layers: sparse-bright and denser-faint.
                float3 col = pointStars(rd, 34.0, _StarDensity * 0.4, 1.2) * 0.8
                           + pointStars(rd + 17.0, 61.0, _StarDensity * 0.7, 0.8) * 0.35;

                // Distant galaxies: rare elongated smudges, barely above black.
                float3 g = rd * 9.0;
                float3 cell = floor(g);
                float h = ds_hash(cell + 42.0);
                if (h < _GalaxyCount * 0.16)
                {
                    float3 centre = cell + 0.5;
                    float3 d = g - centre;
                    // random elongation axis per cell
                    float3 axis = normalize(float3(ds_hash(cell + 1.1), ds_hash(cell + 2.2), ds_hash(cell + 3.3)) - 0.5);
                    float along = dot(d, axis);
                    float across = length(d - axis * along);
                    float body = exp(-(along * along * 14.0 + across * across * 90.0));
                    float warm = ds_hash(cell + 5.5);
                    col += body * lerp(float3(0.045, 0.05, 0.075), float3(0.07, 0.06, 0.045), warm);
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
