Shader "MilkyWay/GalaxyVolume"
{
    // Volumetric Milky Way: logarithmic spiral arms, dust lanes, bar/bulge and
    // HII star-forming regions, raymarched through an analytic bounding
    // ellipsoid. Rendered on an inflated cube with front faces culled so the
    // volume works from OUTSIDE (overview shots) and from INSIDE (standing at
    // the Sun, the disk becomes the night-sky band) with the same code path.
    //
    // Units: 1 object-space unit = 1 kpc. The visible disk reaches ~16 kpc,
    // the Sun sits at 8.2 kpc — real Milky Way proportions.
    Properties
    {
        [Header(Light)]
        _Brightness("Overall Brightness", Range(0.0, 8.0)) = 2.2
        _BulgeBoost("Bulge Brightness", Range(0.0, 4.0)) = 1.4
        _HiiStrength("HII Regions (pink)", Range(0.0, 3.0)) = 1.0
        _YoungStrength("Young Blue Arms", Range(0.0, 3.0)) = 1.0

        [Header(Structure)]
        _DustStrength("Dust Extinction", Range(0.0, 6.0)) = 2.6
        _ArmWidth("Arm Width (kpc)", Range(0.2, 2.5)) = 0.9
        _PitchTan("Arm Pitch tan(12.5deg)", Range(0.1, 0.45)) = 0.2217
        _Clumpiness("Cloud Clumpiness", Range(0.0, 1.5)) = 0.85

        [Header(Quality)]
        _Steps("March Steps", Range(24, 160)) = 80
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "GalaxyVolume"
            Tags { "LightMode" = "UniversalForward" }
            // Premultiplied: the march produces (emitted light, occlusion).
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Front   // back faces: valid when the camera is inside the volume

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Brightness, _BulgeBoost, _HiiStrength, _YoungStrength;
                float _DustStrength, _ArmWidth, _PitchTan, _Clumpiness;
                float _Steps;
            CBUFFER_END

            // Bounding ellipsoid radii (kpc): hugs disk + bulge + a little halo.
            static const float3 BOUNDS = float3(17.0, 4.0, 17.0);
            static const float BAR_ANGLE = 0.4712;     // 27 deg to the Sun-centre line
            static const float DISK_SCALE = 2.8;       // exponential scale length
            static const float R_MAX = 16.0;

            // ---------------- noise (self-contained) ----------------
            float mw_hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float mw_vnoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(mw_hash(i + float3(0, 0, 0)), mw_hash(i + float3(1, 0, 0)), f.x),
                         lerp(mw_hash(i + float3(0, 1, 0)), mw_hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(mw_hash(i + float3(0, 0, 1)), mw_hash(i + float3(1, 0, 1)), f.x),
                         lerp(mw_hash(i + float3(0, 1, 1)), mw_hash(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            float mw_fbm(float3 p)
            {
                float v = 0.0, a = 0.5;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    v += a * mw_vnoise(p);
                    p = p * 2.13 + 11.7;
                    a *= 0.5;
                }
                return v;
            }

            // ---------------- galaxy model ----------------
            // Angular offset of the arm pattern at radius r (log spiral).
            float armTheta(float r)
            {
                return log(max(r, 0.5) / 3.6) / _PitchTan;
            }

            // How strongly point (r, theta) sits on an arm. thetaShift slides
            // the pattern (dust lanes hug the arms' inner, trailing edge).
            // Two major arms plus two weaker minor arms, all one log-spiral family.
            float armFactor(float r, float theta, float thetaShift, float widthScale)
            {
                float t = theta - armTheta(r) + thetaShift;
                float w = _ArmWidth * widthScale * (0.7 + 0.055 * r); // arms broaden outward
                // major pair (period pi)
                float dMaj = abs(frac(t / 6.2831853 * 2.0 + 0.5) - 0.5) * 3.14159265 * r;
                float major = exp(-dMaj * dMaj / (w * w));
                // minor pair, offset a quarter turn, tighter and dimmer
                float dMin = abs(frac(t / 6.2831853 * 2.0) - 0.5) * 3.14159265 * r;
                float minor = 0.5 * exp(-dMin * dMin / (w * w * 0.7));
                return major + minor;
            }

            // Emitted light + extinction of the interstellar medium at p.
            void galaxyMedium(float3 p, out float3 emission, out float absorb)
            {
                float r = length(p.xz);
                float theta = atan2(p.z, p.x);

                // Outer edge: nothing to see past R_MAX.
                float edge = 1.0 - smoothstep(R_MAX * 0.82, R_MAX * 1.05, r);

                // -------- old stellar disk: smooth, warm, dynamically cold --------
                // Thin: at the Sun's radius h must stay near 0.4 kpc, or the sky
                // seen from inside turns into uniform grey fog instead of a band
                // over black poles (the night-sky view is the constraint here).
                float hDisk = 0.22 + 0.02 * r;
                float vertDisk = exp(-(p.y * p.y) / (hDisk * hDisk));
                float disk = exp(-r / DISK_SCALE) * vertDisk * edge;

                // -------- bar + boxy bulge (rotated 27 deg) --------
                float cb = cos(BAR_ANGLE), sb = sin(BAR_ANGLE);
                float3 pb = float3(p.x * cb - p.z * sb, p.y, p.x * sb + p.z * cb);
                float bulgeR = sqrt(pb.x * pb.x / (3.4 * 3.4)
                                  + pb.y * pb.y / (1.15 * 1.15)
                                  + pb.z * pb.z / (1.55 * 1.55));
                float bulge = exp(-bulgeR * bulgeR * 2.0);

                // -------- spiral arms --------
                float arms = armFactor(r, theta, 0.0, 1.0);
                // Arms emerge from the bar ends GRADUALLY. A short onset ramp
                // switched them on at full strength right where exp(-r/scale)
                // peaks, which drew two bright arcs hugging the bulge like
                // parentheses — the innermost winding igniting all at once.
                float armRegion = smoothstep(3.2, 6.5, r) * edge;
                arms *= armRegion;

                float vertThin = exp(-(p.y * p.y) / (0.18 * 0.18 + 0.0009 * r * r));
                float vertDust = exp(-(p.y * p.y) / (0.19 * 0.19 + 0.0006 * r * r));

                // Most samples on a top-down ray sit in nearly empty space above
                // and below the disk — skip the seven noise evaluations there.
                float potential = disk + bulge + arms * (vertThin + vertDust);
                if (potential < 1e-4)
                {
                    emission = float3(0, 0, 0);
                    absorb = 0.0;
                    return;
                }

                // Giant molecular clouds give the arms their patchiness.
                float clumps = mw_fbm(p * float3(0.9, 2.2, 0.9));
                float clumpy = lerp(1.0, clumps * 1.9, _Clumpiness);

                // -------- young blue population: tight to the arms --------
                // Same exponential radial law as the disk. Giving the arms
                // their own shallower falloff made them an independent light
                // source ~70x the local disk at the Sun's radius — from inside,
                // the first kiloparsec of every sightline glowed and the whole
                // sky washed grey. Arms are a MODULATION of the disk, not a
                // second galaxy laid on top.
                float young = arms * arms * vertThin * exp(-r / DISK_SCALE) * clumpy;

                // -------- HII star-forming knots: sparse, pink, on the arms ----
                // Same radial decline as the young stars: without it the blue
                // fades first and the outer arms turn into pure pink beads.
                float knots = mw_vnoise(p * float3(2.6, 5.0, 2.6) + 31.7);
                float hii = arms * vertThin * pow(saturate(knots), 6.0) * 5.0
                          * smoothstep(3.5, 5.0, r) * exp(-r / 8.0) * edge;

                // -------- dust: thin lane hugging the arms' inner edge --------
                // Two placement rules matter: optical depth ~3+ (real lanes are
                // near-opaque), and a SMALL angular offset. At 0.16 rad the lane
                // sat over a kpc outside the bright ridge — invisible against
                // the dark inter-arm gap. 0.07 rad keeps it ON the ridge's edge.
                float dustLane = armFactor(r, theta, 0.07, 0.7);
                float filaments = mw_fbm(p * float3(1.6, 3.6, 1.6) + 7.3);
                float dust = 5.0 * dustLane * vertDust * armRegion
                           * saturate(filaments * 1.6 - 0.25)
                           * exp(-r / 9.0);
                // a diffuse dust floor through the whole inner disk
                dust += 0.6 * vertDust * exp(-r / 4.0) * saturate(filaments * 1.3 - 0.15) * edge;

                // -------- compose --------
                const float3 OLD_COL   = float3(1.00, 0.86, 0.62);   // K-giant warmth
                const float3 BULGE_COL = float3(1.00, 0.76, 0.45);
                const float3 YOUNG_COL = float3(0.50, 0.66, 1.00);   // OB associations
                const float3 HII_COL   = float3(1.00, 0.42, 0.54);   // H-alpha pink

                // The luminous gas of the arms — this, not the star points, is
                // what makes a galaxy photo read as glowing spiral structure.
                // Contrast lives in the RATIO of these two terms: dimming the
                // axisymmetric disk glow is what makes the spiral pop.
                float armGlow = arms * vertDisk * exp(-r / DISK_SCALE) * 1.3 * clumpy;

                emission = OLD_COL   * (disk * 0.32)
                         + BULGE_COL * (bulge * _BulgeBoost)
                         + OLD_COL   * armGlow
                         + YOUNG_COL * (young * 2.4 * _YoungStrength)
                         + HII_COL   * (hii * _HiiStrength);
                emission *= _Brightness * 0.16;

                absorb = dust * _DustStrength;
            }

            // ---------------- bounds ----------------
            // Ray vs the bounding ellipsoid; false when the ray misses.
            bool ellipsoidSegment(float3 ro, float3 rd, out float t0, out float t1)
            {
                float3 o = ro / BOUNDS;
                float3 d = rd / BOUNDS;
                float a = dot(d, d);
                float b = 2.0 * dot(o, d);
                float c = dot(o, o) - 1.0;
                float disc = b * b - 4.0 * a * c;
                t0 = 0.0; t1 = 0.0;
                if (disc <= 0.0) return false;
                float sq = sqrt(disc);
                t0 = (-b - sq) / (2.0 * a);
                t1 = (-b + sq) / (2.0 * a);
                if (t1 <= 0.0) return false;   // entirely behind the camera
                t0 = max(t0, 0.0);             // camera inside: start at the eye
                return true;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Inflate the unit cube to the bounding box of the ellipsoid.
                float3 objPos = v.positionOS.xyz * 2.0 * (BOUNDS * 1.02);
                o.posOS = objPos;
                o.positionHCS = TransformObjectToHClip(objPos);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // March in object space: the galaxy can be moved, rotated and
                // scaled by its transform (the Andromeda encounter will need it).
                float3 roWS = _WorldSpaceCameraPos;
                float3 ro = TransformWorldToObject(roWS);
                float3 rd = normalize(i.posOS - ro);

                float t0, t1;
                if (!ellipsoidSegment(ro, rd, t0, t1)) return half4(0, 0, 0, 0);

                int steps = (int)_Steps;
                float dt = (t1 - t0) / steps;
                // Stochastic start hides banding on the thin dust lanes. STATIC
                // per pixel — keyed to time it would crawl visibly every frame.
                float jitter = mw_hash(float3(i.positionHCS.xy, 7.0));
                float t = t0 + dt * jitter;

                float3 col = 0.0;
                // Chromatic transmittance: interstellar dust absorbs blue light
                // far more than red, so what shines through a lane is reddened.
                // This one line is why the lanes read brown instead of grey.
                const float3 EXTINCT = float3(0.55, 1.0, 1.7);
                float3 trans = 1.0;

                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 p = ro + rd * t;
                    float3 emission;
                    float absorb;
                    galaxyMedium(p, emission, absorb);

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
}
