Shader "MilkyWay/PlanetSurface"
{
    // One procedural shader for every solar-system body — rocky worlds, gas
    // giants, and Earth — selected by parameter strengths rather than
    // variants, so the SolarSystemRig can author each planet as a material.
    //
    // Lighting is computed here (Lambert toward _SunPos with a wrap term and
    // an ambient floor) because the galaxy scene has no Unity light: the Sun
    // in the rig is an emissive prop, not a Light component.
    Properties
    {
        [Header(Real map (equirectangular))]
        _MainTex("Albedo Map", 2D) = "black" {}
        _TexStrength("Map Strength (0 = procedural)", Range(0, 1)) = 0.0
        _CloudTex("Cloud Map (Earth)", 2D) = "black" {}
        _UseCloudTex("Use Cloud Map", Range(0, 1)) = 0.0
        _NightTex("Night Lights Map (Earth)", 2D) = "black" {}
        _NightStrength("Night Lights Strength", Range(0, 2)) = 0.0

        _BaseColor("Base Colour", Color) = (0.5, 0.5, 0.5, 1)
        _SecondColor("Second Colour (bands / land)", Color) = (0.7, 0.6, 0.45, 1)
        _OceanColor("Ocean Colour", Color) = (0.06, 0.18, 0.42, 1)
        _PoleColor("Pole / Ice Colour", Color) = (0.92, 0.96, 1.0, 1)
        _RimColor("Atmosphere Rim (HDR)", Color) = (0, 0, 0, 0)

        [Header(Surface pattern)]
        _NoiseScale("Noise Scale", Float) = 6.0
        _Mottle("Mottling", Range(0, 1)) = 0.35
        _BandFreq("Band Frequency (gas giants)", Float) = 0.0
        _BandWarp("Band Turbulence", Range(0, 1)) = 0.35
        _Continents("Continents", Range(0, 1)) = 0.0
        _SeaLevel("Sea Level", Range(0, 1)) = 0.55
        _IceCap("Ice Cap Latitude (1.2 = off)", Range(0, 1.2)) = 1.2
        _Clouds("Cloud Cover", Range(0, 1)) = 0.0
        _Spot("Storm Spot", Range(0, 1)) = 0.0
        _SpotColor("Storm Spot Colour", Color) = (0.75, 0.35, 0.2, 1)

        [Header(Atmosphere flow (gas giants))]
        _FlowSpeed("Flow Speed (vortex cycles/sec)", Float) = 0.0
        _FlowJets("Flow Jet Count", Float) = 8.0
        _FlowShear("Flow Shear (longitude/sec)", Float) = 0.0
        _FlowBase("Flow Base Drift (longitude/sec)", Float) = 0.0
        _Vortex("Vortex Strength (radians)", Float) = 0.0
        _VortexUV("Vortex Centre (u, v)", Vector) = (0.371, 0.385, 0, 0)
        _VortexRadius("Vortex Radius (uv)", Float) = 0.055

        [Header(Lighting)]
        _SunPos("Sun Position (world)", Vector) = (0, 0, 0, 0)
        _Ambient("Ambient Floor", Range(0, 1)) = 0.12
        _Glow("Self Glow", Range(0, 2)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PlanetSurface"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _SecondColor, _OceanColor, _PoleColor, _RimColor, _SpotColor;
                float4 _SunPos;
                float _NoiseScale, _Mottle, _BandFreq, _BandWarp;
                float _Continents, _SeaLevel, _IceCap, _Clouds, _Spot;
                float _Ambient, _Glow;
                float _TexStrength, _UseCloudTex, _NightStrength;
                float4 _VortexUV;
                float _FlowSpeed, _FlowJets, _FlowShear, _FlowBase, _Vortex, _VortexRadius;
            CBUFFER_END

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);
            TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);

            // Equirectangular lookup from the object-space surface direction.
            // The atan2 longitude wraps at ±π, which makes the u derivative
            // jump across the seam and mip selection fetch the smallest mip —
            // a one-pixel column of mud down the sphere. Classic fix: build a
            // second candidate with the seam rotated half a turn away and let
            // each pixel use whichever coordinate is locally continuous.
            half3 SampleEquirect(TEXTURE2D_PARAM(tex, samp), float3 os, float lonOffset)
            {
                float v = asin(clamp(os.y, -1.0, 1.0)) / 3.14159265 + 0.5;
                float lon = atan2(os.z, os.x) / 6.2831853; // -0.5 .. 0.5
                float uA = frac(lon + lonOffset);
                float uB = frac(lon + lonOffset + 0.5) - 0.5;
                float2 uv = fwidth(uA) <= fwidth(uB) ? float2(uA, v) : float2(uB, v);
                return SAMPLE_TEXTURE2D(tex, samp, uv).rgb;
            }

            // Seam-safe sample from an UNBOUNDED longitude coordinate — same
            // two-candidate fwidth trick as SampleEquirect, needed here too
            // because the flow offset makes the wrap point drift across the
            // planet (a moving one-pixel mud column otherwise).
            half3 SampleLonV(TEXTURE2D_PARAM(tex, samp), float2 lv)
            {
                float uA = frac(lv.x);
                float uB = frac(lv.x + 0.5) - 0.5;
                float2 uv = fwidth(uA) <= fwidth(uB) ? float2(uA, lv.y) : float2(uB, lv.y);
                return SAMPLE_TEXTURE2D(tex, samp, uv).rgb;
            }

            // Rotate (lon, v) around the vortex centre, falling off smoothly
            // to zero at the radius — the Great Red Spot's swirl.
            float2 Swirl(float2 p, float2 c, float ang)
            {
                float2 d = p - c;
                d.x -= round(d.x); // shortest way around the longitude wrap
                float r = saturate(1.0 - length(d) / max(_VortexRadius, 1e-4));
                float a = ang * r * r * (3.0 - 2.0 * r);
                float cs = cos(a), sn = sin(a);
                return c + float2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
            }

            float pl_hash(float3 p)
            {
                // NOT the product hash the galaxy shaders use: that one is
                // symmetric under coordinate permutation, which on a sphere
                // becomes a visible mirror plane across x = z (kaleidoscope
                // bands on Jupiter). The dot() breaks the symmetry.
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            float pl_vnoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(pl_hash(i), pl_hash(i + float3(1, 0, 0)), f.x),
                         lerp(pl_hash(i + float3(0, 1, 0)), pl_hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(pl_hash(i + float3(0, 0, 1)), pl_hash(i + float3(1, 0, 1)), f.x),
                         lerp(pl_hash(i + float3(0, 1, 1)), pl_hash(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            float pl_fbm(float3 p)
            {
                float v = 0.0, a = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += a * pl_vnoise(p);
                    p = p * 2.13 + 11.7;
                    a *= 0.5;
                }
                return v * 1.067; // renormalise the 4-octave sum toward [0,1]
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Texture in OBJECT space so the pattern rides the planet's
                // spin (the rig rotates the transform); a unit sphere's
                // object position doubles as its surface normal.
                float3 os = normalize(i.positionOS);
                float lat = os.y;

                float3 col = _BaseColor.rgb;

                // Gas-giant bands: latitude stripes, turbulence-warped so the
                // edges curl instead of running ruler-straight.
                if (_BandFreq > 0.01)
                {
                    float warp = (pl_fbm(os * _NoiseScale) - 0.5) * _BandWarp;
                    float band = sin((lat + warp) * _BandFreq * 3.14159);
                    col = lerp(col, _SecondColor.rgb, band * 0.5 + 0.5);
                }

                // Rocky mottling — maria, craters-at-a-distance, dust regions.
                float mottle = pl_fbm(os * _NoiseScale * 1.7);
                col = lerp(col, col * (0.55 + 0.9 * mottle), _Mottle);

                // Continents rise out of the ocean above the sea level.
                if (_Continents > 0.01)
                {
                    float h = pl_fbm(os * _NoiseScale);
                    float land = smoothstep(_SeaLevel, _SeaLevel + 0.05, h);
                    float3 landCol = lerp(_SecondColor.rgb * 0.85, _SecondColor.rgb * 1.15,
                                          pl_fbm(os * _NoiseScale * 3.1));
                    col = lerp(_OceanColor.rgb, landCol, land * _Continents);
                }

                // Polar ice.
                float ice = smoothstep(_IceCap, _IceCap + 0.08, abs(lat));
                col = lerp(col, _PoleColor.rgb, ice);

                // A great oval storm, fixed in the planet's own frame.
                if (_Spot > 0.01)
                {
                    float2 spotUV = float2(atan2(os.z, os.x) * 0.6, (lat + 0.32) * 2.4);
                    float d = length(spotUV - float2(0.55, 0.0));
                    col = lerp(col, _SpotColor.rgb, _Spot * smoothstep(0.16, 0.05, d));
                }

                // A real observed albedo map replaces everything procedural
                // above (bands, mottling, continents, ice, storms are all in
                // the photograph already); clouds and lighting still apply.
                if (_TexStrength > 0.001)
                {
                    half3 mapCol;
                    if (_FlowSpeed > 0.0001)
                    {
                        // The atmosphere MOVES — but a photograph cannot be
                        // advected forever: unbounded differential shear
                        // grinds every feature (the Red Spot first) into
                        // smeared stripes within minutes. So all DISTORTING
                        // motion (jet shear, vortex swirl) lives in two
                        // half-phase samples that each displace only a few
                        // seconds' worth before resetting at zero weight,
                        // while the uniform base drift — which distorts
                        // nothing — scrolls forever.
                        float v = asin(clamp(os.y, -1.0, 1.0)) / 3.14159265 + 0.5;
                        float lon = atan2(os.z, os.x) / 6.2831853;
                        float baseLon = lon + _FlowBase * _Time.y;
                        float shear = _FlowShear * sin(lat * _FlowJets * 3.14159);
                        float latV = sin((_VortexUV.y - 0.5) * 3.14159);
                        float shearV = _FlowShear * sin(latV * _FlowJets * 3.14159);

                        float t = _Time.y * _FlowSpeed;
                        float win = 1.0 / max(_FlowSpeed, 1e-3);
                        float ph0 = frac(t) - 0.5, ph1 = frac(t + 0.5) - 0.5;

                        float2 lv0 = float2(baseLon + shear * ph0 * win, v);
                        float2 lv1 = float2(baseLon + shear * ph1 * win, v);
                        if (_Vortex > 0.001)
                        {
                            // Each phase's swirl centre sits where the spot is
                            // in THAT phase's displaced map.
                            float vx = _VortexUV.x + _FlowBase * _Time.y;
                            lv0 = Swirl(lv0, float2(vx + shearV * ph0 * win, _VortexUV.y), _Vortex * ph0);
                            lv1 = Swirl(lv1, float2(vx + shearV * ph1 * win, _VortexUV.y), _Vortex * ph1);
                        }
                        half3 c0 = SampleLonV(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), lv0);
                        half3 c1 = SampleLonV(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), lv1);
                        mapCol = lerp(c0, c1, abs(ph0 * 2.0));
                    }
                    else
                        mapCol = SampleEquirect(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), os, 0.0);
                    col = lerp(col, mapCol, _TexStrength);
                }

                // Clouds drift slowly relative to the surface.
                if (_Clouds > 0.01)
                {
                    float cl;
                    if (_UseCloudTex > 0.5)
                    {
                        // The observed cloud map, slid in longitude so the
                        // weather moves relative to the ground.
                        cl = SampleEquirect(TEXTURE2D_ARGS(_CloudTex, sampler_CloudTex), os, _Time.y * 0.004).r;
                    }
                    else
                    {
                        // Threshold above the fbm mean, or a thin haze veils the
                        // whole sphere and washes the surface colours pastel.
                        cl = smoothstep(0.57, 0.75, pl_fbm(os * _NoiseScale * 0.9 + float3(_Time.y * 0.015, 0, 0)));
                    }
                    col = lerp(col, float3(0.95, 0.96, 0.98), _Clouds * cl);
                }

                // Sun-lit Lambert with a wrap term; ambient floor keeps the
                // night side readable against a dark galaxy backdrop.
                float3 N = normalize(i.normalWS);
                float3 L = normalize(_SunPos.xyz - i.positionWS);
                float ndl = saturate((dot(N, L) + 0.18) / 1.18);
                float light = _Ambient + (1.0 - _Ambient) * ndl;

                // Atmosphere: a fresnel rim, only on the lit side.
                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
                float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                float3 rim = _RimColor.rgb * fres * (0.25 + 0.75 * ndl);

                // City lights emerge as the sun sets: emissive, night side
                // only, fading out through the terminator. Squaring the map
                // keeps JPEG's near-black noise floor from hazing the oceans.
                float3 night = 0.0;
                if (_NightStrength > 0.001)
                {
                    float3 lights = SampleEquirect(TEXTURE2D_ARGS(_NightTex, sampler_NightTex), os, 0.0);
                    night = lights * lights * 2.0 * _NightStrength * (1.0 - smoothstep(0.0, 0.30, ndl));
                }

                return half4(col * light * (1.0 + _Glow) + rim + night, 1.0);
            }
            ENDHLSL
        }
    }
}
