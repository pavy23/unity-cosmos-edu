using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// The point-star half of the hybrid galaxy: samples ~120k stars from the
    /// same structural model the volume shader raymarches (identical spiral
    /// geometry, bar angle and scale lengths — the constants must not drift
    /// apart) and bakes them into ONE mesh of billboard quads, drawn additively
    /// in a single call by MilkyWay/GalaxyStars.
    ///
    /// Four populations, each with its own spatial law and colour mix:
    ///   thin disk (old, warm) · spiral arms (young, blue) ·
    ///   bar/bulge (older, orange) · halo (ancient, sparse, dim).
    /// </summary>
    [ExecuteAlways]
    public class GalaxyStarField : MonoBehaviour
    {
        [Tooltip("Total baked stars. 120k ≈ 480k verts — one draw call, fine on a laptop GPU.")]
        [Range(10000, 400000)] public int starCount = 120000;
        public int seed = 7;
        public Material material;

        // Shared with GalaxyVolume.shader — keep in lockstep with the HLSL.
        const float PitchTan = 0.2217f;      // tan(12.5°)
        const float ArmR0 = 3.6f;
        const float BarAngle = 0.4712f;      // 27°
        const float DiskScale = 2.8f;
        const float RMax = 16f;

        GameObject holder;
        int builtCount = -1, builtSeed = -1;

        // The bake is DETERMINISTIC in (starCount, seed) — and every clone of
        // the main galaxy (four zoo specimens in a single frame, Andromeda)
        // uses the same pair. One shared mesh replaces N identical 480k-vert
        // synchronous bakes (the F8 stall) and the per-clone meshes the old
        // teardown quietly leaked. Refcounted so the last field standing
        // frees it; a field with a DIFFERENT config just bakes privately.
        static Mesh cachedMesh;
        static int cachedCount = -1, cachedSeed = -1, cachedUsers;
        bool usingCache;

        void OnEnable() => Build();
        void OnDisable() => Teardown();

        void Update()
        {
            // Rebuild when inspector values change (editor tuning).
            if (builtCount != starCount || builtSeed != seed) Build();
        }

        void Teardown()
        {
            if (usingCache)
            {
                usingCache = false;
                if (--cachedUsers <= 0)
                {
                    if (cachedMesh != null) DestroyImmediate(cachedMesh);
                    cachedMesh = null; cachedCount = cachedSeed = -1; cachedUsers = 0;
                }
            }
            else if (holder != null)
            {
                var mf = holder.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) DestroyImmediate(mf.sharedMesh);
            }
            if (holder != null) DestroyImmediate(holder);
            holder = null;
            builtCount = builtSeed = -1;
        }

        void Build()
        {
            Teardown();
            // HideAndDontSave children survive domain reloads while our field
            // reference resets — sweep MY stale copy (the BH-project lesson).
            // Only under this transform: a global name sweep would destroy the
            // live mesh of every OTHER star field the moment a second galaxy
            // is instantiated (the Andromeda encounter does exactly that).
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t != null && t.name == "Galaxy Stars Mesh"
                    && (t.hideFlags & HideFlags.HideAndDontSave) != 0
                    && t.parent == transform)
                    DestroyImmediate(t.gameObject);

            builtCount = starCount;
            builtSeed = seed;

            holder = new GameObject("Galaxy Stars Mesh") { hideFlags = HideFlags.HideAndDontSave };
            holder.transform.SetParent(transform, false);
            var mf = holder.AddComponent<MeshFilter>();
            var mr = holder.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            if (cachedMesh != null && cachedCount == starCount && cachedSeed == seed)
            {
                mf.sharedMesh = cachedMesh;
                cachedUsers++;
                usingCache = true;
            }
            else if (cachedMesh == null)
            {
                mf.sharedMesh = cachedMesh = BakeMesh();
                cachedCount = starCount; cachedSeed = seed; cachedUsers = 1;
                usingCache = true;
            }
            else
            {
                mf.sharedMesh = BakeMesh(); // different config: private mesh
                usingCache = false;
            }
        }

        Mesh BakeMesh()
        {
            int n = starCount;
            var rng = new System.Random(seed);

            var verts = new Vector3[n * 4];
            var cols = new Color32[n * 4];
            var uv0 = new Vector2[n * 4];   // quad corner
            var uv1 = new Vector2[n * 4];   // (world size, per-star random)
            var idx = new int[n * 6];

            for (int s = 0; s < n; s++)
            {
                float pick = (float)rng.NextDouble();
                Vector3 p;
                Color c;
                if (pick < 0.42f) SampleDisk(rng, out p, out c);
                else if (pick < 0.72f) SampleArms(rng, out p, out c);
                else if (pick < 0.92f) SampleBulge(rng, out p, out c);
                else SampleHalo(rng, out p, out c);

                // Luminosity function: almost all faint, a rare bright few —
                // the heavy tail is what makes a starfield sparkle. Restrained:
                // the stars are the glitter, the volume glow is the structure.
                float u = (float)rng.NextDouble();
                float size = 0.014f + 0.06f * Mathf.Pow(u, 6f);
                float lum = 0.25f + 0.75f * Mathf.Pow((float)rng.NextDouble(), 2f);
                c.a = lum;

                int v = s * 4;
                verts[v] = verts[v + 1] = verts[v + 2] = verts[v + 3] = p;
                var c32 = (Color32)c;
                cols[v] = cols[v + 1] = cols[v + 2] = cols[v + 3] = c32;
                uv0[v] = new Vector2(-1, -1);
                uv0[v + 1] = new Vector2(1, -1);
                uv0[v + 2] = new Vector2(1, 1);
                uv0[v + 3] = new Vector2(-1, 1);
                float rand = (float)rng.NextDouble();
                uv1[v] = uv1[v + 1] = uv1[v + 2] = uv1[v + 3] = new Vector2(size, rand);

                int t = s * 6;
                idx[t] = v; idx[t + 1] = v + 2; idx[t + 2] = v + 1;
                idx[t + 3] = v; idx[t + 4] = v + 3; idx[t + 5] = v + 2;
            }

            var mesh = new Mesh
            {
                name = "Galaxy Stars",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.vertices = verts;
            mesh.colors32 = cols;
            mesh.uv = uv0;
            mesh.uv2 = uv1;
            mesh.triangles = idx;
            // The quads billboard in the vertex shader; give culling the true extent.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(44f, 12f, 44f));
            return mesh;
        }

        // ---------------- populations ----------------

        void SampleDisk(System.Random rng, out Vector3 p, out Color c)
        {
            float r = ExpRadius(rng, DiskScale, RMax);
            float th = (float)rng.NextDouble() * Mathf.PI * 2f;
            float h = 0.30f + 0.045f * r;
            p = new Vector3(Mathf.Cos(th) * r, Gauss(rng) * h * 0.7f, Mathf.Sin(th) * r);
            c = Palette(rng, DiskMix);
        }

        void SampleArms(System.Random rng, out Vector3 p, out Color c)
        {
            // Radius weighted toward the inner arms. SHIFTED exponential, not a
            // clamp: Max(3.5, exp) piled every inner draw onto exactly r = 3.5,
            // which drew a razor-sharp ring of blue stars hugging the bulge.
            float r = 3.5f + ExpRadius(rng, 4.5f, RMax - 3.5f);
            // Two major arms (0, π); minor pair (±π/2) carries fewer stars.
            float offset = rng.NextDouble() < 0.7
                ? (rng.NextDouble() < 0.5 ? 0f : Mathf.PI)
                : (rng.NextDouble() < 0.5 ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f);
            float armTh = Mathf.Log(r / ArmR0) / PitchTan + offset;
            // Scatter as an ARC distance, so arms stay equally crisp at all radii.
            float th = armTh + Gauss(rng) * (0.55f / r);
            float h = 0.16f + 0.01f * r;
            p = new Vector3(Mathf.Cos(th) * r, Gauss(rng) * h, Mathf.Sin(th) * r);
            c = Palette(rng, ArmMix);
        }

        void SampleBulge(System.Random rng, out Vector3 p, out Color c)
        {
            // Triaxial bar, rotated 27° like the volume's.
            var e = new Vector3(Gauss(rng) * 1.7f, Gauss(rng) * 0.55f, Gauss(rng) * 0.75f);
            float cb = Mathf.Cos(-BarAngle), sb = Mathf.Sin(-BarAngle);
            p = new Vector3(e.x * cb - e.z * sb, e.y, e.x * sb + e.z * cb);
            // Tens of thousands of additive points pile up in a couple of kpc —
            // dim the class or the core nukes to white.
            var col = Palette(rng, BulgeMix);
            c = col * 0.72f;
        }

        void SampleHalo(System.Random rng, out Vector3 p, out Color c)
        {
            // ρ ∝ r^-2.5 between 2 and 22 kpc, isotropic — ancient and faint.
            // Enclosed count ∝ ∫ r²·r^-2.5 dr ∝ √r, so invert on √r.
            float u = (float)rng.NextDouble();
            float sq = Mathf.Sqrt(2f) + u * (Mathf.Sqrt(22f) - Mathf.Sqrt(2f));
            float r = sq * sq;
            var dir = new Vector3(Gauss(rng), Gauss(rng), Gauss(rng)).normalized;
            p = dir * r;
            var col = Palette(rng, HaloMix);
            c = col * 0.55f; // metal-poor and far: dim them as a class
            c.a = col.a;
        }

        // ---------------- helpers ----------------

        /// <summary>Exponential-disk radius via inverse CDF, truncated at rMax.</summary>
        static float ExpRadius(System.Random rng, float scale, float rMax)
        {
            float u = (float)rng.NextDouble();
            float cap = 1f - Mathf.Exp(-rMax / scale);
            return -scale * Mathf.Log(1f - u * cap);
        }

        static float Gauss(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1))
                         * System.Math.Cos(2.0 * System.Math.PI * u2));
        }

        // Stellar palettes (linear-ish RGB by temperature class).
        static readonly Color B = new Color(0.62f, 0.72f, 1.00f);
        static readonly Color A = new Color(0.90f, 0.94f, 1.00f);
        static readonly Color F = new Color(1.00f, 0.97f, 0.90f);
        static readonly Color G = new Color(1.00f, 0.90f, 0.72f);
        static readonly Color K = new Color(1.00f, 0.78f, 0.52f);
        static readonly Color M = new Color(1.00f, 0.55f, 0.35f);

        static readonly (Color col, float w)[] DiskMix = { (F, 0.20f), (G, 0.35f), (K, 0.35f), (M, 0.10f) };
        static readonly (Color col, float w)[] ArmMix = { (B, 0.45f), (A, 0.35f), (F, 0.20f) };
        static readonly (Color col, float w)[] BulgeMix = { (G, 0.25f), (K, 0.45f), (M, 0.30f) };
        static readonly (Color col, float w)[] HaloMix = { (G, 0.20f), (K, 0.50f), (M, 0.30f) };

        static Color Palette(System.Random rng, (Color col, float w)[] mix)
        {
            float pick = (float)rng.NextDouble();
            foreach (var (col, w) in mix)
            {
                if (pick < w)
                {
                    // small hue jitter so a class is not one flat colour
                    float j = 0.92f + 0.16f * (float)rng.NextDouble();
                    return new Color(col.r * j, col.g, col.b * (2f - j), 1f);
                }
                pick -= w;
            }
            return mix[mix.Length - 1].col;
        }
    }
}
