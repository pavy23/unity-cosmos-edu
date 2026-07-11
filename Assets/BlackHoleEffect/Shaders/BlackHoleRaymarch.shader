// Physically-motivated Schwarzschild black hole rendered by geodesic raymarching
// on a single camera-facing quad.
//
//   * Null geodesics integrated with a jittered leapfrog (kick-drift-kick).
//   * Thin emissive disk sheet + volumetric disk "atmosphere" slab.
//   * Blackbody radiation colors from a Kelvin temperature profile T ∝ r^-3/4.
//   * Correct relativistic shift: brightness ∝ (δ·g)³, temperature ∝ δ·g,
//     where δ is the Doppler factor and g the gravitational redshift.
//
// Conventions:
//   * GameObject uniform scale = Schwarzschild radius (Rs) in world metres.
//   * Simulation units GM = c = 1: Rs = 2, photon sphere r = 3, ISCO r = 6.
Shader "BlackHole/RaymarchedBlackHole"
{
    Properties
    {
        [Header(Accretion Disk)]
        _DiskInner("Inner Radius (Rs)", Range(1.3, 6.0)) = 2.9
        _DiskOuter("Outer Radius (Rs)", Range(4.0, 14.0)) = 8.6
        _DiskBrightness("Brightness (HDR)", Range(0.0, 40.0)) = 9.5
        _DiskTemp("Temperature", Range(0.5, 1.8)) = 1.06
        _FlowSpeed("Flow Speed", Range(0.0, 3.0)) = 0.9
        _Doppler("Doppler Beaming", Range(0.0, 1.0)) = 0.72
        _DiskDetail("Turbulence Detail", Range(1.0, 14.0)) = 4.2
        _DiskContrast("Turbulence Contrast", Range(0.0, 1.0)) = 0.3
        _DiskTint("Disk Tint", Color) = (1, 1, 1, 1)
        _HazeStrength("Atmosphere Haze", Range(0.0, 1.5)) = 0.18

        [Header(Background)]
        _StarDensity("Star Density", Range(0.0, 2.0)) = 0.12
        _NebulaIntensity("Nebula Haze", Range(0.0, 1.0)) = 0.15

        [Header(Kerr Spin)]
        _Spin("Spin a (M units, 0 = Schwarzschild)", Range(0.0, 0.998)) = 0

        [Header(Binary Merger)]
        [Toggle] _BinaryOn("Binary Mode (two lensing centers)", Float) = 0
        _Hole1Pos("Hole 1 Position (sim units)", Vector) = (0, 0, 0, 0)
        _Hole2Pos("Hole 2 Position (sim units)", Vector) = (8, 0, 0, 0)
        _Hole1Mass("Hole 1 Mass (M units)", Float) = 1
        _Hole2Mass("Hole 2 Mass (M units)", Float) = 0.8

        [Header(Quality)]
        _Steps("Raymarch Steps", Range(64, 384)) = 220
        _ViewExtent("Simulation Extent (Rs)", Range(10.0, 30.0)) = 15.0

        [Header(Mixed Reality)]
        [Toggle] _MRMode("MR Passthrough (escaped rays transparent)", Float) = 0
        [Toggle] _ZWrite("Depth Write (off for multi-hole MR)", Float) = 1

        [Header(Einstein Ring Demo)]
        [Toggle] _DemoStarOn("Demo Star Enabled", Float) = 0
        _DemoStarDir("Demo Star Direction (object space)", Vector) = (0, 0, 1, 0)
        _DemoStarSize("Demo Star Size", Range(0.0002, 0.01)) = 0.0007
        _DemoStarBrightness("Demo Star Brightness", Range(0, 120)) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite [_ZWrite]
        // Premultiplied blending: space mode outputs alpha 1 (opaque), MR mode
        // outputs alpha = disk opacity so passthrough shows through escaped rays.
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "BlackHoleRaymarch"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "StarFunctions.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _DiskInner, _DiskOuter, _DiskBrightness, _DiskTemp;
                float _FlowSpeed, _Doppler, _DiskDetail, _DiskContrast;
                float4 _DiskTint;
                float _HazeStrength;
                float _StarDensity, _NebulaIntensity;
                float _Spin;
                float _BinaryOn;
                float4 _Hole1Pos, _Hole2Pos;
                float _Hole1Mass, _Hole2Mass;
                float _Steps, _ViewExtent, _MRMode;
                float4 _DemoStarDir;
                float _DemoStarOn, _DemoStarSize, _DemoStarBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 posWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Billboard the quad toward the camera, sized to the simulation extent.
                float3 centerWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float scaleWS = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 camWS = _WorldSpaceCameraPos;
                float3 fwd = normalize(centerWS - camWS);
                float3 upRef = abs(fwd.y) > 0.98 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 right = normalize(cross(upRef, fwd));
                float3 up = cross(fwd, right);
                float halfExtent = _ViewExtent * scaleWS;
                float3 ws = centerWS + (v.positionOS.x * right + v.positionOS.y * up) * halfExtent * 2.0;

                o.posWS = ws;
                o.positionHCS = TransformWorldToHClip(ws);
                return o;
            }

            // Kelvin -> linear RGB, classic analytic fit of the Planckian locus.
            float3 blackbodyRGB(float T)
            {
                T = clamp(T, 1200.0, 12000.0);
                float3 c;
                c.r = 56100000.0 * pow(T, -1.5) + 148.0;
                c.g = T > 6500.0 ? 35200000.0 * pow(T, -1.5) + 184.0
                                 : 100.04 * log(T) - 623.6;
                c.b = 194.18 * log(T) - 1448.6;
                c = clamp(c, 0.0, 255.0) / 255.0;
                if (T < 2000.0) c *= T / 2000.0; // fade very cool material out
                // Cinematic saturation boost: physical blackbody colors are
                // fairly pale at these temperatures and ACES desaturates
                // brights further, so push away from the luma axis.
                float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = max(lerp(luma.xxx, c, 1.45), 0.0);
                return c;
            }

            // Bright point star for the Einstein ring demo.
            float3 demoStar(float3 rd)
            {
                if (_DemoStarOn < 0.5) return float3(0, 0, 0);
                float d = 1.0 - dot(rd, normalize(_DemoStarDir.xyz));
                float core = smoothstep(_DemoStarSize, _DemoStarSize * 0.25, d);
                float halo = exp(-d / (_DemoStarSize * 3.0)) * 0.08;
                return (core + halo) * _DemoStarBrightness * float3(0.85, 0.92, 1.0);
            }

            // Relativistic shift factor at a disk point: Doppler δ combined
            // with gravitational redshift g. hp in sim units, velN = marching
            // direction (photon travels disk -> camera along -velN).
            float relShift(float3 hp, float rSim, float3 velN)
            {
                float g = sqrt(max(1.0 - 2.0 / rSim, 0.03));
                float beta = min(rsqrt(max(rSim - 2.0, 1.2)), 0.7);
                float3 tang = normalize(cross(float3(0, 1, 0), hp));
                float dopp = 1.0 / max(1.0 - _Doppler * beta * dot(tang, -velN), 0.35);
                // _Doppler also scales how much of the redshift we show, so the
                // slider remains a single "relativity strength" control.
                return lerp(1.0, g, _Doppler) * dopp;
            }

            float diskTemperatureK(float rRs)
            {
                return 4700.0 * _DiskTemp * pow(_DiskInner / rRs, 0.75);
            }

            // Shade one crossing of the thin disk sheet.
            float3 shadeDisk(float3 hp, float3 velN, out float alpha)
            {
                alpha = 0.0;
                float rSim = length(hp.xz);
                float r = rSim * 0.5; // in Rs
                if (r < _DiskInner || r > _DiskOuter)
                    return float3(0.0, 0.0, 0.0);

                float x = (r - _DiskInner) / (_DiskOuter - _DiskInner);
                float density = smoothstep(0.0, 0.06, x)
                              * exp(-3.0 * x)
                              * (1.0 - smoothstep(0.82, 1.0, x));

                // Keplerian differential rotation shears the noise into
                // streaks; +ωt so the pattern moves along the same direction
                // as the Doppler velocity (+cross(ŷ, r̂)).
                float ang = atan2(hp.z, hp.x);
                float omega = 4.2 * _FlowSpeed / pow(rSim, 1.5);
                float phase = ang + _Time.y * omega;
                float2 nuv = float2(log2(rSim) * _DiskDetail,
                                    (phase + log2(rSim) * 1.2) * 1.4);
                float n = bh_fbm2(nuv);
                float streak = lerp(1.0 - _DiskContrast, 1.0 + 1.6 * _DiskContrast, n);
                density *= streak;

                float shift = relShift(hp, rSim, velN);
                float boost = min(shift * shift * shift, 5.0);
                float3 c = blackbodyRGB(diskTemperatureK(r) * shift);

                // Fade opacity out with brightness so a "disk off" state
                // (lens demo) doesn't leave a black occluding silhouette.
                alpha = saturate(density * 1.8) * 0.92 * saturate(_DiskBrightness);
                return c * _DiskTint.rgb * density * boost * _DiskBrightness;
            }

            // Volumetric "atmosphere" above/below the disk sheet: a flaring
            // slab of faint gas that softens the paper-thin look.
            void sampleHaze(float3 p, float3 velN, float dt, inout float3 col, inout float trans)
            {
                if (_HazeStrength <= 0.001) return;
                float rSim = length(p.xz);
                float r = rSim * 0.5;
                if (r < _DiskInner * 0.85 || r > _DiskOuter * 1.05) return;

                float H = 0.05 * rSim + 0.08;      // flaring slab half-height (sim units)
                float ay = abs(p.y);
                if (ay > H) return;

                float x = saturate((r - _DiskInner) / (_DiskOuter - _DiskInner));
                float vertical = exp(-(ay * ay) / (H * H * 0.25));
                float radial = exp(-2.8 * x) * smoothstep(0.0, 0.1, x);

                float ang = atan2(p.z, p.x);
                float phase = ang + _Time.y * 4.2 * _FlowSpeed / pow(max(rSim, 3.0), 1.5);
                float n = bh_vnoise2(float2(log2(max(rSim, 1.0)) * 3.2, phase * 1.6));

                float dens = _HazeStrength * 0.22 * vertical * radial * (0.45 + 0.8 * n);
                if (dens <= 0.0005) return;

                float shift = relShift(p, max(rSim, 2.4), velN);
                float boost = min(shift * shift * shift, 4.0);
                float3 c = blackbodyRGB(diskTemperatureK(max(r, _DiskInner)) * 0.8 * shift);

                float a = saturate(dens * dt) * saturate(_DiskBrightness);
                col += trans * c * _DiskTint.rgb * dens * dt * boost * _DiskBrightness * 0.4;
                trans *= 1.0 - a * 0.4;
            }

            // ================= Kerr (rotating) black hole =================
            // Kerr-Schild form: g_μν = η_μν + 2H l_μ l_ν — Cartesian and
            // horizon-penetrating, so the same raymarch structure works.
            // Simulation units GM = c = M = 1 (Rs = 2M = 2); spin axis = +y
            // (the disk normal). Photons integrate the Hamiltonian
            //   H(x,p) = ½ η^{μν}p_μ p_ν − Φ(x),  Φ = H(x)·(l·p)²,
            // with conserved p_t = −1, so only Φ needs position derivatives
            // (taken numerically — cheap and robust).

            // Boyer-Lindquist-like radius from Cartesian position:
            // r⁴ − (R²−a²) r² − a² y² = 0.
            float ksRadius(float3 p, float a)
            {
                float b = dot(p, p) - a * a;
                return sqrt(max(0.5 * (b + sqrt(b * b + 4.0 * a * a * p.y * p.y)), 1e-6));
            }

            // Spatial part of the Kerr-Schild null vector l (spin along +y).
            float3 ksL(float3 p, float r, float a)
            {
                float d = r * r + a * a;
                return float3((r * p.x + a * p.z) / d, p.y / max(r, 1e-4), (r * p.z - a * p.x) / d);
            }

            // Φ(x) for a photon with spatial momentum q and energy E = 1:
            // Φ = H (1 + l·q)², H = M r³ / (r⁴ + a² y²).
            float kerrPhi(float3 p, float3 q, float a)
            {
                float r = ksRadius(p, a);
                float Hk = r * r * r / (r * r * r * r + a * a * p.y * p.y);
                float lp = 1.0 + dot(ksL(p, r, a), q);
                return Hk * lp * lp;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Object space respects the GameObject's rotation and scale;
                // multiply by 2 => simulation units where Rs = 2.
                float4x4 w2o = GetWorldToObjectMatrix();
                float3 ro = mul(w2o, float4(_WorldSpaceCameraPos, 1.0)).xyz * 2.0;
                float3 px = mul(w2o, float4(i.posWS, 1.0)).xyz * 2.0;
                float3 rd = normalize(px - ro);

                float R = _ViewExtent * 2.0;
                float R2 = R * R;

                // Fade lensing near the simulation boundary to hide the seam
                // against the (un-lensed) skybox at the quad edge.
                float3 rel = ro;
                float tca = -dot(rel, rd);
                float b2 = dot(rel, rel) - tca * tca;
                float edgeFade = 1.0 - smoothstep(0.55 * R2, 0.92 * R2, b2);

                if (b2 >= R2)
                {
                    if (_MRMode > 0.5) return half4(0, 0, 0, 0);
                    // Starfield in WORLD direction so it lines up seamlessly
                    // with the skybox outside the quad (the hole object is
                    // tilted — sampling in object space rotates the sky and
                    // makes the quad show up as a square patch when zoomed
                    // out). The demo star stays in object space by design;
                    // its wide halo is faded by edgeFade, otherwise the glow
                    // fills the whole quad and reads as a bright square box.
                    float3 rdWS = normalize(i.posWS - _WorldSpaceCameraPos);
                    return half4(bh_starField(rdWS, _StarDensity, _NebulaIntensity) + demoStar(rd) * edgeFade, 1.0);
                }

                // Advance to the simulation sphere, with per-pixel jitter that
                // decorrelates step boundaries into fine grain instead of bands.
                float jitter = bh_hash21(i.positionHCS.xy + frac(_Time.y) * 61.7);
                float3 p = ro;
                float tEnter = tca - sqrt(R2 - b2);
                if (tEnter > 0.0) p += rd * tEnter;

                float3 vel = rd;
                float3 col = float3(0.0, 0.0, 0.0);
                float trans = 1.0;
                bool captured = false;
                int steps = (int)_Steps;

                if (_BinaryOn > 0.5)
                {
                    // ------- Binary path: superposed Schwarzschild bending -------
                    // No analytic two-body metric exists; far from either
                    // horizon the deflections superpose accurately, and only
                    // the last moments before contact are approximate. Each
                    // hole contributes −(3/2) m·h² r̂/r⁴ with its own
                    // instantaneous angular momentum h = |(x−c)×v|.
                    float m1 = _Hole1Mass, m2 = _Hole2Mass;
                    float3 c1 = _Hole1Pos.xyz, c2 = _Hole2Pos.xyz;

                    float3 rel1 = p - c1;
                    float3 rel2 = p - c2;
                    float d0 = min(length(rel1) - 1.9 * m1, length(rel2) - 1.9 * m2);
                    p += vel * clamp(0.14 * d0, 0.03, 0.45) * jitter;

                    [loop]
                    for (int s = 0; s < steps; s++)
                    {
                        rel1 = p - c1;
                        rel2 = p - c2;
                        float r1 = length(rel1);
                        float r2b = length(rel2);

                        if (r1 < 2.02 * m1 || r2b < 2.02 * m2) { captured = true; break; }
                        if (dot(p, p) > R2 * 1.15 && dot(p, vel) > 0.0) break;

                        float dt = clamp(0.14 * min(r1 - 1.9 * m1, r2b - 1.9 * m2), 0.03, 0.45);

                        float3 h1 = cross(rel1, vel);
                        float3 h2b = cross(rel2, vel);
                        float3 acc = -1.5 * m1 * dot(h1, h1) * rel1 / max(pow(r1, 5.0), 1e-4)
                                   - 1.5 * m2 * dot(h2b, h2b) * rel2 / max(pow(r2b, 5.0), 1e-4);

                        float3 vHalf = vel + acc * (dt * 0.5);
                        float3 pn = p + vHalf * dt;

                        float3 rel1n = pn - c1;
                        float3 rel2n = pn - c2;
                        float3 h1n = cross(rel1n, vHalf);
                        float3 h2n = cross(rel2n, vHalf);
                        float3 accN = -1.5 * m1 * dot(h1n, h1n) * rel1n / max(pow(length(rel1n), 5.0), 1e-4)
                                    - 1.5 * m2 * dot(h2n, h2n) * rel2n / max(pow(length(rel2n), 5.0), 1e-4);
                        vel = vHalf + accN * (dt * 0.5);

                        // Circumbinary disk: the shading is still centred on
                        // the barycenter (origin) — the pair orbits inside
                        // the cavity it has carved.
                        sampleHaze((p + pn) * 0.5, normalize(vel), dt, col, trans);
                        if (p.y * pn.y < 0.0)
                        {
                            float tc = p.y / (p.y - pn.y);
                            float3 hp = lerp(p, pn, tc);
                            float alpha;
                            float3 dcol = shadeDisk(hp, normalize(vel), alpha);
                            col += trans * dcol;
                            trans *= 1.0 - alpha;
                        }
                        if (trans < 0.03) break;
                        p = pn;
                    }
                }
                else if (_Spin > 0.0005)
                {
                    // ---------- Kerr path: Kerr-Schild Hamiltonian ----------
                    // Negative sign: the hole must co-rotate with the disk
                    // gas (whose Doppler velocity is +cross(ŷ, r̂), i.e.
                    // clockwise seen from +y), so the shadow flattens on the
                    // approaching, Doppler-bright side — as in real images.
                    float a = -_Spin;
                    float rHor = 1.0 + sqrt(max(1.0 - a * a, 0.0)); // r+ (= 2 at a = 0)

                    // Null-normalize the momentum for E = 1: solve the
                    // quadratic |q|² − 1 − 2H(1 + c1·s)² = 0 for scale s.
                    float r0 = ksRadius(p, a);
                    float H0 = r0 * r0 * r0 / (r0 * r0 * r0 * r0 + a * a * p.y * p.y);
                    float c1 = dot(ksL(p, r0, a), rd);
                    float qa = 1.0 - 2.0 * H0 * c1 * c1;
                    float qb = -4.0 * H0 * c1;
                    float qc = -(1.0 + 2.0 * H0);
                    float qs = (-qb + sqrt(max(qb * qb - 4.0 * qa * qc, 0.0))) / max(2.0 * qa, 1e-4);
                    float3 q = rd * qs;

                    p += rd * clamp(0.14 * (r0 - rHor), 0.03, 0.45) * jitter;

                    [loop]
                    for (int s = 0; s < steps; s++)
                    {
                        float r = ksRadius(p, a);
                        if (r < rHor + 0.02) { captured = true; break; }
                        if (dot(p, p) > R2 * 1.15 && dot(p, vel) > 0.0) break;

                        float dt = clamp(0.14 * (r - rHor * 0.95), 0.03, 0.45);

                        // Kick: dq/dλ = +∇Φ (forward-difference gradient).
                        float e = max(0.004 * r, 0.002);
                        float phi0 = kerrPhi(p, q, a);
                        float3 grad;
                        grad.x = kerrPhi(p + float3(e, 0, 0), q, a) - phi0;
                        grad.y = kerrPhi(p + float3(0, e, 0), q, a) - phi0;
                        grad.z = kerrPhi(p + float3(0, 0, e), q, a) - phi0;
                        q += grad * (dt / e);

                        // Drift: dx/dλ = q − 2H (1 + l·q) l.
                        float Hk = r * r * r / (r * r * r * r + a * a * p.y * p.y);
                        float3 l = ksL(p, r, a);
                        vel = q - 2.0 * Hk * (1.0 + dot(l, q)) * l;
                        float3 pn = p + vel * dt;

                        // Same disk sheet + atmosphere shading as the
                        // Schwarzschild path.
                        sampleHaze((p + pn) * 0.5, normalize(vel), dt, col, trans);
                        if (p.y * pn.y < 0.0)
                        {
                            float tc = p.y / (p.y - pn.y);
                            float3 hp = lerp(p, pn, tc);
                            float alpha;
                            float3 dcol = shadeDisk(hp, normalize(vel), alpha);
                            col += trans * dcol;
                            trans *= 1.0 - alpha;
                        }
                        if (trans < 0.03) break;
                        p = pn;
                    }
                }
                else
                {
                    // ---------- Schwarzschild fast path (a = 0) ----------
                    float3 hVec = cross(p, vel);
                    float h2 = dot(hVec, hVec);

                    float r2 = dot(p, p);
                    float r = sqrt(r2);
                    float dt0 = clamp(0.14 * (r - 1.9), 0.03, 0.45);
                    p += vel * dt0 * jitter; // sub-step offset

                    // Leapfrog (velocity Verlet): one acceleration eval per step.
                    float3 acc = -1.5 * h2 * p / max(pow(dot(p, p), 2.5), 1e-4);

                    [loop]
                    for (int s = 0; s < steps; s++)
                    {
                        r2 = dot(p, p);
                        r = sqrt(r2);

                        if (r < 2.02) { captured = true; break; }
                        if (r2 > R2 * 1.15 && dot(p, vel) > 0.0) break;

                        float dt = clamp(0.14 * (r - 1.9), 0.03, 0.45);

                        // Kick-drift-kick.
                        float3 vHalf = vel + acc * (dt * 0.5);
                        float3 pn = p + vHalf * dt;
                        float3 accN = -1.5 * h2 * pn / max(pow(dot(pn, pn), 2.5), 1e-4);
                        vel = vHalf + accN * (dt * 0.5);
                        acc = accN;

                        // Volumetric disk atmosphere along the segment midpoint.
                        sampleHaze((p + pn) * 0.5, normalize(vel), dt, col, trans);

                        // Thin-sheet crossing (exact position by interpolation).
                        if (p.y * pn.y < 0.0)
                        {
                            float tc = p.y / (p.y - pn.y);
                            float3 hp = lerp(p, pn, tc);
                            float alpha;
                            float3 dcol = shadeDisk(hp, normalize(vel), alpha);
                            col += trans * dcol;
                            trans *= 1.0 - alpha;
                        }
                        if (trans < 0.03) break;
                        p = pn;
                    }
                }

                float alphaOut = 1.0;
                if (!captured && trans > 0.03)
                {
                    if (_MRMode > 0.5)
                    {
                        alphaOut = 1.0 - trans; // room shows through escaped rays
                    }
                    else
                    {
                        float3 escapeDir = normalize(lerp(rd, normalize(vel), edgeFade));
                        // World direction for the sky (matches the skybox);
                        // object direction for the Einstein-ring demo star.
                        // Demo-star halo fades with edgeFade so the quad
                        // boundary never shows as a box.
                        float3 escWS = normalize(mul((float3x3)GetObjectToWorldMatrix(), escapeDir));
                        col += trans * (bh_starField(escWS, _StarDensity, _NebulaIntensity)
                                        + demoStar(escapeDir) * edgeFade);
                    }
                }

                return half4(col, alphaOut);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
