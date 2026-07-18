Shader "MilkyWay/NebulaVolume"
{
    // Volumetric nebula, raymarched through a bounding sphere on an inflated
    // cube (front-culled, so it works from outside AND with the camera inside).
    // One shader, three looks selected by _NebulaType:
    //   0  emission (HII)  — turbulent ionized hydrogen, pink Hα + teal OIII
    //   1  reflection      — cool dust lit blue by an embedded star
    //   2  planetary       — a thin glowing shell around a white-dwarf core
    // Same scaffolding as MilkyWay/GalaxyVolume (object-space march, premultiplied
    // (emission, occlusion), chromatic dust extinction, target 3.5 for WebGL).
    Properties
    {
        [Header(Type)]
        _NebulaType("Type (0 emit 1 refl 2 planetary)", Range(0, 2)) = 0

        [Header(Light)]
        _Brightness("Brightness", Range(0.0, 8.0)) = 2.4
        [HDR] _Color1("Colour A (Halpha / dust)", Color) = (1.0, 0.35, 0.45, 1)
        [HDR] _Color2("Colour B (OIII / core)", Color) = (0.35, 0.9, 0.85, 1)

        [Header(Structure)]
        _Radius("Cloud Radius (obj units)", Range(1.0, 20.0)) = 8.0
        _Density("Density", Range(0.0, 3.0)) = 1.0
        _NoiseScale("Noise Scale", Range(0.05, 1.5)) = 0.32
        _Filament("Filament Warp", Range(0.0, 3.0)) = 1.4
        _Threshold("Wisp Threshold", Range(0.0, 0.9)) = 0.42
        _DustStrength("Dust Extinction", Range(0.0, 6.0)) = 1.2

        [Header(Planetary shell)]
        _ShellRadius("Shell Radius (0..1)", Range(0.1, 0.95)) = 0.62
        _ShellThickness("Shell Thickness", Range(0.02, 0.4)) = 0.14

        [Header(Quality)]
        _Steps("March Steps", Range(24, 160)) = 72
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "NebulaVolume"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _NebulaType, _Brightness;
                float4 _Color1, _Color2;
                float _Radius, _Density, _NoiseScale, _Filament, _Threshold, _DustStrength;
                float _ShellRadius, _ShellThickness;
                float _Steps;
            CBUFFER_END

            // ---- value noise + fbm -------------------------------------------
            float neb_hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }
            float neb_vnoise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = neb_hash(i + float3(0,0,0));
                float n100 = neb_hash(i + float3(1,0,0));
                float n010 = neb_hash(i + float3(0,1,0));
                float n110 = neb_hash(i + float3(1,1,0));
                float n001 = neb_hash(i + float3(0,0,1));
                float n101 = neb_hash(i + float3(1,0,1));
                float n011 = neb_hash(i + float3(0,1,1));
                float n111 = neb_hash(i + float3(1,1,1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }
            float neb_fbm(float3 p)
            {
                float a = 0.5, sum = 0.0;
                [unroll]
                for (int o = 0; o < 5; o++) { sum += a * neb_vnoise(p); p *= 2.03; a *= 0.5; }
                return sum; // ~0..1
            }
            // Ridged fbm: sharp bright creases — the fine filaments and tendrils
            // that make a real nebula read as gas rather than smoke.
            float neb_ridged(float3 p)
            {
                float a = 0.5, sum = 0.0, prev = 1.0;
                [unroll]
                for (int o = 0; o < 4; o++)
                {
                    float n = 1.0 - abs(2.0 * neb_vnoise(p) - 1.0);
                    n *= n; n *= prev; prev = n;
                    sum += n * a; p *= 2.17; a *= 0.55;
                }
                return saturate(sum);
            }

            // ---- the medium --------------------------------------------------
            void nebulaMedium(float3 p, out float3 emission, out float absorb)
            {
                emission = 0.0; absorb = 0.0;
                float r = length(p) / _Radius;         // 0 at core .. 1 at edge
                if (r > 1.05) return;

                float3 q = p * _NoiseScale;
                // Domain warp for organic, filamentary structure.
                float3 w = float3(neb_fbm(q + 11.3), neb_fbm(q - 7.1), neb_fbm(q + 3.7));
                float3 qw = q + (w - 0.5) * _Filament;
                float base = neb_fbm(qw);
                // Ragged silhouette: a low-frequency noise pushes the fade radius
                // in and out so the cloud is not a clean ball.
                float env = neb_fbm(q * 0.5 + 21.0);
                float edge = smoothstep(1.0, 0.3, r + (env - 0.5) * 0.8);
                // Contrast curve + fine ridged filaments = multi-scale gas.
                base = saturate((base - _Threshold) / (1.0 - _Threshold));
                float fine = neb_ridged(qw * 3.0 + 5.0);
                float d = pow(base, 1.6) * (0.45 + 0.95 * fine) * edge * _Density;

                if (_NebulaType < 0.5)
                {
                    // EMISSION, modelled on the visible spectral lines: an OFFSET
                    // hot core (like Orion's Trapezium) ionizes the gas into teal
                    // OIII, fading through pink Hα to deep-red SII in the shielded
                    // dusty clumps — which also extinct hardest, cutting dark lanes.
                    float3 hot = float3(0.18, 0.12, 0.0) * _Radius;
                    float ion = smoothstep(0.95, 0.0, length(p - hot) / _Radius);
                    float dust = saturate(neb_fbm(qw * 1.6 - 3.0) * 1.7 - 0.55);

                    float3 oiii = _Color2.rgb;                         // teal-green core
                    float3 halpha = _Color1.rgb;                      // pink body
                    float3 sii = _Color1.rgb * float3(0.72, 0.5, 0.5); // deep red (less brown)
                    float3 c = lerp(halpha, oiii, saturate(ion * 1.6));
                    c = lerp(c, sii, saturate(dust * 0.6));

                    emission = c * d * (0.5 + 1.4 * ion) * _Brightness;
                    absorb = (d + dust * _Density * 0.7) * _DustStrength;
                }
                else if (_NebulaType < 1.5)
                {
                    // REFLECTION: cool dust scattering a central star's blue light,
                    // brighter close in (1/r^2-ish), barely self-absorbing.
                    float lit = 1.0 / (0.15 + r * r * 7.0);
                    emission = _Color1.rgb * d * lit * _Brightness;
                    absorb = d * _DustStrength * 0.3;
                }
                else
                {
                    // PLANETARY: a thin shell reads as a limb-brightened RING in
                    // projection — a ray grazing the shell tangentially picks up
                    // far more of it than one through the middle. OIII teal on the
                    // inner edge, Hα red on the outer. A faint white-dwarf spark
                    // sits at the centre (a point, not a glow).
                    float shell = exp(-pow((r - _ShellRadius) / _ShellThickness, 2.0));
                    float dd = shell * (0.55 + 0.45 * d);
                    float mixr = smoothstep(_ShellRadius - _ShellThickness,
                                            _ShellRadius + _ShellThickness, r);
                    emission = lerp(_Color2.rgb, _Color1.rgb, mixr) * dd * _Brightness;
                    // Faint OIII haze filling the interior (M57's teal centre),
                    // fading out past the ring; textured by the same gas.
                    float interior = smoothstep(_ShellRadius + _ShellThickness, 0.0, r);
                    emission += _Color2.rgb * interior * (0.12 + 0.35 * d) * _Brightness;
                    emission += float3(1.0, 0.95, 0.9) * 0.5 * exp(-r * r * 900.0) * _Brightness; // white dwarf
                    absorb = dd * _DustStrength * 0.4;
                }
            }

            // ---- ray vs bounding sphere --------------------------------------
            bool sphereSegment(float3 ro, float3 rd, float rad, out float t0, out float t1)
            {
                float b = dot(ro, rd);
                float c = dot(ro, ro) - rad * rad;
                float disc = b * b - c;
                t0 = 0.0; t1 = 0.0;
                if (disc <= 0.0) return false;
                float sq = sqrt(disc);
                t0 = -b - sq; t1 = -b + sq;
                if (t1 <= 0.0) return false;
                t0 = max(t0, 0.0);
                return true;
            }

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 objPos = v.positionOS.xyz * 2.0 * (_Radius * 1.06);
                o.posOS = objPos;
                o.positionHCS = TransformObjectToHClip(objPos);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 ro = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rd = normalize(i.posOS - ro);

                float t0, t1;
                if (!sphereSegment(ro, rd, _Radius * 1.02, t0, t1)) return half4(0, 0, 0, 0);

                int steps = (int)_Steps;
                float dt = (t1 - t0) / steps;
                float jitter = neb_hash(float3(i.positionHCS.xy, 7.0));
                float t = t0 + dt * jitter;

                float3 col = 0.0;
                const float3 EXTINCT = float3(0.55, 1.0, 1.7); // dust reddens
                float3 trans = 1.0;

                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 p = ro + rd * t;
                    float3 emission; float absorb;
                    nebulaMedium(p, emission, absorb);
                    col += trans * emission * dt;
                    trans *= exp(-absorb * dt * EXTINCT);
                    if (max(trans.r, max(trans.g, trans.b)) < 0.02) break;
                    t += dt;
                }

                float occlusion = 1.0 - dot(trans, float3(0.333, 0.334, 0.333));
                return half4(col, occlusion);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
