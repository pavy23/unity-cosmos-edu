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
        _NebulaType("Type (0 emit 1 refl 2 plan 3 snr 4 dark)", Range(0, 4)) = 0

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

        [Header(Dark nebula)]
        // Baked from the real photograph: R = dust silhouette, G = rim band.
        _DustMask("Dust Silhouette Mask", 2D) = "black" {}

        [Header(Quality)]
        _Steps("March Steps", Range(24, 160)) = 72
        // 1 = the cloud darkens what's behind it (a dark nebula silhouettes);
        // 0 = purely additive, so it blends into a bright star-field background
        // with no hard bounding-sphere edge (emission nebulae over the panorama).
        _OccludeBg("Occlude Background", Range(0, 1)) = 1
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
                float _Steps, _OccludeBg;
            CBUFFER_END

            TEXTURE2D(_DustMask);
            SAMPLER(sampler_DustMask);

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
            // viewAxis: object-space direction from the cloud to the camera. Used
            // by the planetary type to orient its torus face-on (a real hole).
            void nebulaMedium(float3 p, float3 viewAxis, out float3 emission, out float absorb)
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
                    float3 hot = float3(0.16, 0.10, 0.0) * _Radius;
                    // Keep the oxygen-rich cavity compact; a 0.9R falloff made
                    // most of an emission nebula read as green fog.
                    float ion = smoothstep(1.0, 0.0, length(p - hot) / (_Radius * 0.52));
                    ion *= ion;
                    float dust = saturate(neb_fbm(qw * 1.6 - 3.0) * 1.8 - 0.6);
                    // A dark dust lane biting across the cavity (Orion's "fish
                    // mouth"): a great-circle band where the position aligns with a
                    // plane, strongest toward the core.
                    float lane = smoothstep(0.14, 0.0,
                                    abs(dot(normalize(p + 1e-4), normalize(float3(-0.5, -0.18, 0.32)))))
                                 * smoothstep(1.0, 0.15, r);

                    float3 oiii = _Color2.rgb * float3(0.72, 0.88, 1.15); // blue-cyan core
                    float3 halpha = _Color1.rgb;                      // pink body
                    float3 sii = _Color1.rgb * float3(0.7, 0.42, 0.42); // deep red (less brown)
                    float3 c = lerp(halpha, oiii, saturate(ion * 2.2));
                    c = lerp(c, sii, saturate(dust * 0.7));

                    // A low-opacity body joins the detailed knots into broad
                    // wings without filling the whole bounding sphere.
                    float broad = edge * smoothstep(1.05, 0.05, r)
                                * (0.045 + 0.11 * fine) * _Density;
                    emission = c * (d + broad) * (0.42 + 2.0 * ion) * _Brightness;
                    emission *= (1.0 - 0.88 * lane);                   // carve the dark lane
                    absorb = (d * 0.65 + dust * _Density * 0.55 + lane * _Density * 1.45) * _DustStrength;
                }
                else if (_NebulaType < 1.5)
                {
                    // REFLECTION: cool dust in fine STRIATIONS scattering a cluster's
                    // blue light. Sparse and wispy with dark sky between the strands
                    // — the bright stars are the subject, the gas only a veil. The
                    // combed noise (tight across, loose along) makes the striae.
                    float streak = neb_ridged(float3(qw.x * 3.0, qw.y * 0.5, qw.z * 3.0) + 17.0);
                    float wisp = d * (0.12 + 1.7 * streak);
                    float lit = 1.0 / (0.25 + r * r * 5.0);
                    emission = _Color1.rgb * wisp * lit * _Brightness;
                    absorb = wisp * _DustStrength * 0.22;
                }
                else if (_NebulaType < 2.5)
                {
                    // PLANETARY: a genuine hollow RING, modelled as a TORUS whose axis
                    // points at the camera — so rays through the centre pass through
                    // the empty hole (a real dark centre, not a spherical shell's
                    // unavoidable double-crossing). Teal OIII on the inner rim, red
                    // Hα/NII on the outer, clumped by the gas.
                    float axial = dot(p, viewAxis);                    // along the axis
                    float3 radialVec = p - viewAxis * axial;           // in the ring plane
                    float radial = length(radialVec) / _Radius;        // 0..1
                    // Wobble the ring radius with position noise so it is not a
                    // perfect circle — M57 is lumpy and slightly oval.
                    float ringR = _ShellRadius * (1.0 + 0.2 * (neb_fbm(radialVec * 1.7 + 13.0) - 0.5) * 2.0);
                    float2 tv = float2(radial - ringR, axial / _Radius);
                    float torus = exp(-pow(length(tv) / _ShellThickness, 2.0));
                    torus *= (0.3 + 1.35 * fine);                      // clump / break the ring
                    float mixr = smoothstep(ringR - _ShellThickness * 1.4,
                                            ringR + _ShellThickness * 1.4, radial);
                    float3 inner = _Color2.rgb * float3(0.72, 0.82, 1.18);
                    float3 outer = _Color1.rgb * float3(0.72, 0.55, 0.62);
                    float3 ringCol = lerp(inner, outer, mixr);          // blue-cyan in -> red out
                    emission = ringCol * torus * _Brightness * 0.46;
                    // Faint OIII veil spanning the hole (M57's dim interior), but
                    // thin enough that the ring clearly dominates.
                    float veil = smoothstep(_ShellRadius + _ShellThickness, 0.0, radial)
                               * smoothstep(1.1, 0.2, abs(axial) / _Radius);
                    emission += inner * veil * 0.008 * _Brightness;
                    emission += float3(1.0, 0.97, 0.92) * 0.22 * exp(-r * r * 3600.0) * _Brightness; // white dwarf
                    absorb = torus * _DustStrength * 0.3;
                }
                else if (_NebulaType < 3.5)
                {
                    // SUPERNOVA REMNANT (Crab): a sparse ORANGE filament lace over a
                    // DIM blue synchrotron haze (the pulsar wind). Mostly empty space
                    // between the strands so it never fills into a solid ball. The
                    // oval shape comes from the object's non-uniform scale.
                    float bodyN = neb_fbm(qw * 1.3 + 2.0);
                    // A clearly BLUE, dim interior — the pulsar-wind synchrotron —
                    // so the gaps between filaments read blue, not white.
                    float synch = smoothstep(1.05, 0.1, r) * (0.10 + 0.42 * bodyN);
                    emission = float3(0.22, 0.62, 1.35) * synch * 0.38 * _Brightness;
                    // SPARSE orange filament lace: only the very sharpest ridges
                    // survive (high smoothstep floor), so wide dark/blue gaps open
                    // between thin strands. Green SII flecks on the brightest ridges.
                    float web = neb_ridged(qw * 3.0 + 8.0);
                    float fil = smoothstep(0.68, 0.92, web) * smoothstep(1.08, 0.12, r) * (0.35 + 0.65 * bodyN);
                    float3 filCol = lerp(_Color1.rgb * float3(1.0, 0.68, 0.42),
                                         float3(0.55, 1.1, 0.62), saturate((web - 0.9) * 5.0));
                    emission += filCol * fil * _Brightness * 1.8;
                    absorb = (synch * 0.1 + fil) * _DustStrength * 0.25;
                }
                else
                {
                    // DARK NEBULA (B33 / IC 434), staged as a deep diorama that the
                    // builder faces at the gallery camera (camera on -z): a red
                    // hydrogen curtain glowing at the BACK of the volume, and the
                    // horsehead in front of it. The silhouette is NOT modelled —
                    // it is traced from the actual DSS2 photograph and baked into
                    // _DustMask (R = dust, G = ionization rim), so the pillar has
                    // the real B33 profile: a lumpy dust knob, not a sculpted
                    // horse. The shader adds what the photo can't: thickness,
                    // noise-eroded edges, a glowing curtain behind, parallax.
                    float3 hp = p / _Radius;

                    // Photo-space lookup (canvas spans hp.xy ±0.77, nudged down so
                    // the pillar stays rooted in the photo's bank), domain-warped
                    // so the traced outline breaks into ragged drifting dust; the
                    // warp varies with z, so every depth slice tears differently.
                    // Kept small and high-frequency: edge fuzz, not shape drift —
                    // the outline's fidelity to the photograph IS the exhibit.
                    // Anisotropic mapping: y scaled harder = the pillar squats
                    // (it rendered too tall and spire-like at 1:1).
                    float2 warp = float2(neb_fbm(hp * 5.5 + 61.0), neb_fbm(hp * 5.5 - 17.0)) - 0.5;
                    float2 muv = saturate(hp.xy * float2(0.71, 0.9) + float2(0.5, 0.60) + warp * 0.03);
                    half4 mtex = SAMPLE_TEXTURE2D_LOD(_DustMask, sampler_DustMask, muv, 0);

                    // Extrude the photo silhouette through depth as an eroded slab.
                    // The pillar (plus the local mound baked under it) is ALL the
                    // volume renders: the photographic curtain behind carries the
                    // full-width bank and its bright rim, so a volumetric ridge
                    // would only fight it as a second horizon.
                    float zprof = exp(-pow(hp.z / 0.26, 2.0));
                    float horse = mtex.r * zprof;

                    float silhouette = saturate(horse);
                    // Interior texture from the photograph itself (B = the pillar's
                    // own mottled shading): denser dust where the photo is dark,
                    // faint warm glow where it is light — plus 3D clump noise.
                    float body = silhouette * (0.45 + 0.85 * base + 0.35 * fine)
                               * (1.25 - 0.6 * mtex.b) * _Density;
                    absorb = body * _DustStrength;
                    emission = _Color1.rgb * silhouette
                             * (0.012 + 0.05 * mtex.b + 0.02 * fine) * _Brightness;

                    // IC 434: the glowing curtain in the back half of the volume.
                    // Edge-faded inside the bounding sphere, brightest at the
                    // ionization front just above the ridge and fading upward,
                    // combed into vertical streamers (noise tight across x/z,
                    // stretched along y — the photographs' wind-blown look).
                    // Ragged, noise-torn outer fade (like the emission type's
                    // envelope) so the curtain ends organically, never as the
                    // bounding sphere's circle.
                    float cfade = smoothstep(1.06, 0.5,
                        length(hp * float3(0.78, 0.95, 1.0)) + (env - 0.5) * 0.75);
                    float slab = smoothstep(0.24, 0.55, hp.z) * cfade;
                    float streak = neb_ridged(float3(qw.x * 2.6, qw.y * 0.55, qw.z * 2.6) + 23.0);
                    float front = 0.3 + 0.7 * smoothstep(0.9, -0.25, hp.y);
                    // Roll off below the bank line: down there the photograph's
                    // dark dust carries the scene, and added red glow would shine
                    // through the pillar's translucent base as fog.
                    front *= smoothstep(-0.7, -0.22, hp.y);
                    float curtain = slab * front * (0.16 + 0.34 * base + 0.6 * streak);
                    // A LOW-weight depth layer only: the full-frame inpainted
                    // photograph behind the volume carries the curtain now; this
                    // just adds volumetric shimmer between photo and pillar.
                    float3 curtCol = _Color1.rgb * lerp(float3(1.06, 0.7, 0.75), float3(0.95, 0.9, 1.1),
                                                        smoothstep(-0.25, 0.8, hp.y));
                    emission += curtCol * curtain * _Brightness * 0.4;
                    absorb += slab * 0.04 * _Density;

                    // The ionization rim: the photo-baked band hugging the pillar's
                    // outline — but only on the BACK half (z > 0), so head-on rays
                    // bury it behind the dust and it emerges just where rays graze
                    // past the silhouette's edge.
                    float backHalf = smoothstep(0.0, 0.12, hp.z);
                    float rim = mtex.g * zprof * backHalf * (0.35 + 0.65 * fine);
                    emission += _Color2.rgb * rim * _Brightness * 0.8;
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
                // Object-space direction from the cloud centre to the camera; the
                // planetary torus orients its axis along this so it reads face-on.
                float3 viewAxis = normalize(ro);

                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 p = ro + rd * t;
                    float3 emission; float absorb;
                    nebulaMedium(p, viewAxis, emission, absorb);
                    col += trans * emission * dt;
                    trans *= exp(-absorb * dt * EXTINCT);
                    if (max(trans.r, max(trans.g, trans.b)) < 0.02) break;
                    t += dt;
                }

                float occlusion = (1.0 - dot(trans, float3(0.333, 0.334, 0.333))) * _OccludeBg;
                return half4(col, occlusion);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
