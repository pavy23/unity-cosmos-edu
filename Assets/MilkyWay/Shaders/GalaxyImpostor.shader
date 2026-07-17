Shader "MilkyWay/GalaxyImpostor"
{
    // Tens of thousands of galaxies in one draw call — the GalaxyStars trick
    // an octave up: CosmicWebField bakes every galaxy into a single mesh of
    // billboard quads, and each quad draws a little procedural galaxy sprite
    // (spiral / elliptical / irregular, chosen and oriented by its random).
    //
    // Additive, queue 2850: in front of nothing, behind everything — so the
    // Milky Way's dust lanes correctly extinct the galaxies seen through the
    // disk, exactly like the background stars.
    Properties
    {
        _Brightness("Brightness", Range(0, 4)) = 1.0
        _MinPixels("Pixel Floor", Range(0.5, 6)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-150" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "GalaxyImpostor"
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
                float _Brightness, _MinPixels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // galaxy CENTRE (same for all 4 corners)
                float4 color : COLOR;           // type tint
                float2 corner : TEXCOORD0;      // quad corner in [-1, 1]
                float2 sizeRand : TEXCOORD1;    // x = world size (kpc), y = per-galaxy random
                float2 pixelFloor : TEXCOORD2;  // x = per-galaxy minimum pixels: cluster
                                                // members stay RESOLVED fuzzy galaxies
                                                // (the JWST read) while the web stays points
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : TEXCOORD0;       // tint * energy
                float3 uvRand : TEXCOORD1;      // xy = sprite uv, z = random
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centerWS = TransformObjectToWorld(v.positionOS.xyz);
                float dist = length(_WorldSpaceCameraPos - centerWS);

                // Pixel floor with energy conservation — the star shader's
                // lesson: enlarge a sub-pixel sprite to stay visible, dim it
                // by the area ratio so 40,000 of them don't wash the sky grey.
                float sizeReal = v.sizeRand.x;
                // abs() is load-bearing (GalaxyStars learned this): when URP
                // renders into an intermediate RT (post-processing on, or any
                // RenderTexture) the projection is y-flipped and _m11 is
                // NEGATIVE — max(neg, 1) = 1 turned worldPerPixel into
                // dist*2, every quad into a 4-radian billboard, and energy
                // into ~1e-8: the whole field went black in the built game
                // while looking fine in nothing at all.
                float m11 = max(abs(UNITY_MATRIX_P._m11), 1e-3); // cot(fov/2)
                float worldPerPixel = dist * 2.0 / (m11 * _ScreenParams.y);
                float minPixels = max(v.pixelFloor.x, 0.5) * _MinPixels * 0.5;
                float sizeDraw = max(sizeReal, minPixels * worldPerPixel);
                float energy = (sizeReal * sizeReal) / (sizeDraw * sizeDraw);

                float4 viewPos = mul(UNITY_MATRIX_V, float4(centerWS, 1.0));
                viewPos.xy += v.corner * sizeDraw * 0.5;
                o.positionHCS = mul(UNITY_MATRIX_P, viewPos);

                // Tint in rgb, energy in a — the core term needs the energy
                // factor too, or sub-pixel spirals keep full-brightness cores
                // and the sky washes grey again.
                o.color = float4(v.color.rgb, energy);
                o.uvRand = float3(v.corner, v.sizeRand.y);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float rand = i.uvRand.z;
                float r1 = frac(rand * 7.31);
                float r2 = frac(rand * 13.77);
                float r3 = frac(rand * 3.7);

                // Orient: random position angle, then an inclination squash.
                float ang = rand * 6.2831853;
                float c = cos(ang), s = sin(ang);
                float2 uv = float2(i.uvRand.x * c - i.uvRand.y * s,
                                   i.uvRand.x * s + i.uvRand.y * c);

                float3 col;
                if (r3 < 0.58)
                {
                    // Spiral: inclined exponential disk, a two-arm swirl, a
                    // warm core. log(r) arms — the same curve the big galaxy
                    // uses, in miniature.
                    uv.y /= (0.30 + 0.70 * r1);
                    float r = length(uv);
                    float theta = atan2(uv.y, uv.x);
                    float prof = exp(-r * 3.4);
                    float arms = 1.0 + 0.8 * cos(2.0 * theta - 5.2 * log(r + 0.04))
                               * smoothstep(0.06, 0.3, r);
                    float core = exp(-r * r * 26.0);
                    col = i.color.rgb * prof * arms + float3(1.0, 0.88, 0.72) * core;
                }
                else if (r3 < 0.87)
                {
                    // Elliptical: smooth, rounder, redder (the tint handles
                    // the colour; the profile is just a soft ball of old light).
                    uv.y /= (0.55 + 0.45 * r1);
                    float r = length(uv);
                    col = i.color.rgb * exp(-pow(r, 1.3) * 3.6) * 1.25;
                }
                else
                {
                    // Irregular: two offset clumps, no symmetry to speak of.
                    float2 o1 = float2(r1 - 0.5, r2 - 0.5) * 0.5;
                    float b = exp(-dot(uv - o1, uv - o1) * 10.0)
                            + 0.7 * exp(-dot(uv + o1 * 0.8, uv + o1 * 0.8) * 14.0);
                    col = i.color.rgb * b * 0.9;
                }

                // The quad edge must be invisible or the billboards read as
                // tiles: everything above already decays, this just guarantees it.
                float edge = saturate(1.0 - dot(i.uvRand.xy, i.uvRand.xy));
                return half4(col * edge * _Brightness * i.color.a, 1.0);
            }
            ENDHLSL
        }
    }
}
