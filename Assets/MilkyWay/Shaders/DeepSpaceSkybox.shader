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
        [HDR] _Tint("Star Tint", Color) = (1, 1, 1, 1)
        // A soft Milky-Way band, only for specimens that really sit in the
        // galactic plane (Orion, Horsehead, Crab). Zero elsewhere.
        _BandStrength("Band Strength", Range(0.0, 2.0)) = 0.0
        [HDR] _BandColor("Band Colour", Color) = (0.5, 0.42, 0.34, 1)
        _BandAxis("Band Pole Axis", Vector) = (0, 1, 0, 0)
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
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _StarDensity, _GalaxyCount;
                float4 _Tint, _BandColor, _BandAxis;
                float _BandStrength;
            CBUFFER_END

            float ds_hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // Smooth value noise + fbm, for the band's star-cloud mottling and
            // dark dust lanes.
            float ds_vnoise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = ds_hash(i + float3(0,0,0)), n100 = ds_hash(i + float3(1,0,0));
                float n010 = ds_hash(i + float3(0,1,0)), n110 = ds_hash(i + float3(1,1,0));
                float n001 = ds_hash(i + float3(0,0,1)), n101 = ds_hash(i + float3(1,0,1));
                float n011 = ds_hash(i + float3(0,1,1)), n111 = ds_hash(i + float3(1,1,1));
                float nx00 = lerp(n000, n100, f.x), nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x), nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }
            float ds_fbm(float3 p)
            {
                float a = 0.5, s = 0.0;
                [unroll]
                for (int o = 0; o < 4; o++) { s += a * ds_vnoise(p); p *= 2.07; a *= 0.5; }
                return s;
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
                float3 col = (pointStars(rd, 34.0, _StarDensity * 0.4, 1.2) * 0.8
                           + pointStars(rd + 17.0, 61.0, _StarDensity * 0.7, 0.8) * 0.35) * _Tint.rgb;

                // A soft Milky-Way band for in-plane specimens: a glowing great
                // circle (perpendicular to the pole axis), clumped into star
                // clouds by fbm and cut by darker dust lanes. Faint by design.
                if (_BandStrength > 0.001)
                {
                    float h = dot(rd, normalize(_BandAxis.xyz));       // 0 at the plane
                    float prof = exp(-h * h / 0.035);                  // a stripe, not a wash
                    float clouds = ds_fbm(rd * 6.0 + 3.0);            // bright star clouds
                    float lanes = smoothstep(0.30, 0.60, ds_fbm(rd * 4.0 + 20.0)); // dust
                    float band = prof * (0.12 + 0.6 * clouds) * (1.0 - 0.8 * lanes);
                    col += _BandColor.rgb * band * _BandStrength;
                }

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
