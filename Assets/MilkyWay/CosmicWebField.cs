using System.Collections.Generic;
using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// The universe beyond the Milky Way, baked into one mesh of impostor
    /// quads (the GalaxyStarField technique, one octave up): the authored
    /// Local Group, the Virgo cluster, a scatter of smaller clusters, and
    /// ~40k field galaxies threaded along cosmic-web filaments.
    ///
    /// Filaments come from the classic level-set trick: a point survives when
    /// TWO independent noise fields are both near their mid-value — each
    /// condition alone selects a curved sheet, their intersection selects the
    /// curved LINES where the sheets cross. Density peaks where filaments
    /// meet become clusters for free.
    ///
    /// Units stay kpc like everything else in the exhibit: 1 Mpc = 1000.
    /// Coordinates are galaxy-object space (the parent galaxy root may spin).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CosmicWebField : MonoBehaviour
    {
        [Tooltip("Field galaxies threaded on the web (clusters come extra).")]
        public int webCount = 52000;
        public int seed = 77;

        /// <summary>Direction the zoom-out recedes along — the showpieces
        /// (M31, Virgo) sit in the OPPOSITE hemisphere so the camera keeps
        /// them in frame while backing away. CosmicZoomOut reads this.</summary>
        public static readonly Vector3 CameraDir = new Vector3(0.25f, 0.60f, -0.76f).normalized;

        public static readonly Vector3 M31Pos = new Vector3(-0.42f, -0.35f, 0.84f).normalized * 770f;
        public static readonly Vector3 VirgoPos = new Vector3(-0.10f, -0.55f, 0.83f).normalized * 16500f;

        Mesh mesh;
        Material mat;
        bool built;

        struct G { public Vector3 p; public float size; public Color tint; public float minPx; public float boost; }

        static readonly Color SpiralTint = new Color(0.70f, 0.76f, 1.00f);
        static readonly Color EllipTint = new Color(1.00f, 0.82f, 0.62f);
        static readonly Color IrregTint = new Color(0.62f, 0.82f, 1.00f);

        public void SetBrightness(float b)
        {
            if (mat != null) mat.SetFloat("_Brightness", b);
        }

        /// <summary>Build on first use — ~0.2 s of noise sampling that ambient
        /// mode never pays for. Idempotent.</summary>
        public void EnsureBuilt()
        {
            if (built) return;
            built = true;

            var rng = new System.Random(seed);
            var list = new List<G>(webCount + 4000);

            AddLocalGroup(rng, list);
            // Virgo: elliptical-rich, sized so the flyby reads as a WALL of
            // resolved galaxies (JWST-cluster look), not a star swarm — at
            // ~22,000 kpc viewing distance the members need tens of kpc each,
            // and cluster cores really do host such giants.
            AddCluster(rng, list, VirgoPos, 1100f, 1600, 75f, 0.75f);
            AddWeb(rng, list);

            BakeMesh(list);

            mat = new Material(Shader.Find("MilkyWay/GalaxyImpostor"));
            mat.SetFloat("_Brightness", 0f); // the experience fades it in
            GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ------------------------------------------------------------------

        void AddLocalGroup(System.Random rng, List<G> list)
        {
            // The two big spirals' companions and the dwarf swarm. M31 is
            // authored slightly larger than the Milky Way — as observed.
            list.Add(new G { p = M31Pos, size = 46f, tint = SpiralTint * 1.15f, minPx = 9f, boost = 5f });
            list.Add(new G { p = M31Pos + new Vector3(120f, -60f, 140f), size = 18f, tint = SpiralTint, minPx = 6f, boost = 3f }); // M33
            list.Add(new G { p = new Vector3(28f, -22f, 34f), size = 9f, tint = IrregTint, minPx = 5f, boost = 2f });   // LMC
            list.Add(new G { p = new Vector3(38f, -30f, 22f), size = 6f, tint = IrregTint, minPx = 5f, boost = 2f });   // SMC
            for (int i = 0; i < 34; i++)
            {
                // Dwarf spheroidals: faint, tiny, everywhere.
                Vector3 dir = RandDir(rng);
                float d = 60f + 700f * (float)rng.NextDouble();
                list.Add(new G { p = dir * d, size = 2f + 3f * (float)rng.NextDouble(),
                                 tint = EllipTint * 0.35f, minPx = 3.5f });
            }
        }

        void AddCluster(System.Random rng, List<G> list, Vector3 centre, float sigma,
                        int members, float bcgSize, float ellipFraction)
        {
            // A JWST cluster frame is a size HIERARCHY: one enormous central
            // halo, a handful of giant ellipticals tens of pixels across,
            // a crowd of mid-size fuzz, and a dusting of faint members. The
            // per-galaxy pixel floor is what carries the hierarchy to screen.
            // Photographic exposure: real cluster frames STRETCH the halos —
            // the boost channel lifts the big members the way a long exposure
            // does, instead of letting energy conservation bury them.
            list.Add(new G { p = centre, size = bcgSize, tint = EllipTint * 1.5f, minPx = 46f, boost = 14f });
            int giants = Mathf.Max(8, members / 130);
            for (int i = 0; i < giants; i++)
            {
                Vector3 p = centre + Gaussian3(rng) * sigma * 0.45f; // giants sink to the core
                list.Add(new G
                {
                    p = p,
                    size = 45f + 40f * (float)rng.NextDouble(),
                    tint = EllipTint * (1.1f + 0.4f * (float)rng.NextDouble()),
                    minPx = 22f + 16f * (float)rng.NextDouble(),
                    boost = 8f + 5f * (float)rng.NextDouble(),
                });
            }
            for (int i = 0; i < members; i++)
            {
                Vector3 p = centre + Gaussian3(rng) * sigma;
                bool ellip = rng.NextDouble() < ellipFraction; // dense cores age their galaxies
                list.Add(new G
                {
                    p = p,
                    size = 9f + 42f * Mathf.Pow((float)rng.NextDouble(), 1.8f),
                    tint = (ellip ? EllipTint : SpiralTint) * (0.65f + 0.6f * (float)rng.NextDouble()),
                    minPx = 5f + 18f * Mathf.Pow((float)rng.NextDouble(), 2.2f),
                    boost = 3f + 3f * (float)rng.NextDouble(),
                });
            }
        }

        void AddWeb(System.Random rng, List<G> list)
        {
            const float rMin = 15000f, rMax = 195000f;
            // Wavelength tuned for the DEEP-FIELD HOLD at 180,000 kpc: strands
            // ~50,000 kpc apart give a handful of bold ropes across the frame.
            // The old 34,000 packed in so many thin strands that, projected
            // through the whole ball, they averaged into featureless grain.
            const float noiseScale = 1f / 50000f;
            int placed = 0, clustersLeft = 14;
            // Rejection-sample the double level set. The acceptance window
            // widens with radius so the far field stays populated even though
            // the shells grow as r².
            while (placed < webCount)
            {
                Vector3 p = RandDir(rng) * (rMin + (rMax - rMin) * Mathf.Pow((float)rng.NextDouble(), 0.55f));
                float n1 = Fbm(p * noiseScale, 3);
                float n2 = Fbm(p * noiseScale + new Vector3(31.7f, 7.3f, 19.1f), 3);
                // Narrow acceptance = SHARP strands. The old 0.045-0.075 window
                // smeared the same head-count into fat, low-contrast bands that
                // read as random static from the deep-field hold — the whole
                // "spider's web" claim of the narration was invisible.
                float w = 0.028f + 0.022f * (p.magnitude / rMax);
                if (Mathf.Abs(n1 - 0.5f) > w || Mathf.Abs(n2 - 0.5f) > w) continue;

                float rnd = (float)rng.NextDouble();
                bool ellip = rnd < 0.3f;
                // How close this point sits to the strand's SPINE (both level
                // sets dead-centre). Core galaxies glow harder and bigger, so
                // each filament reads as a luminous rope with fuzzy edges
                // instead of a uniform dust of equal points.
                float core = 1f - Mathf.Clamp01(
                    (Mathf.Abs(n1 - 0.5f) + Mathf.Abs(n2 - 0.5f)) / (w * 1.2f));
                list.Add(new G
                {
                    p = p,
                    size = (8f + 30f * Mathf.Pow((float)rng.NextDouble(), 2f)) * (1f + 0.6f * core),
                    tint = (ellip ? EllipTint : rnd > 0.9f ? IrregTint : SpiralTint)
                         * (0.5f + 0.6f * (float)rng.NextDouble()),
                    minPx = 2.6f + 1.8f * core,
                    boost = 1.8f + 2.4f * core,
                });
                placed++;

                // Where both fields sit dead-centre the filaments cross —
                // drop a cluster there. Emergent large-scale structure.
                if (clustersLeft > 0 && Mathf.Abs(n1 - 0.5f) < 0.006f && Mathf.Abs(n2 - 0.5f) < 0.006f)
                {
                    clustersLeft--;
                    AddCluster(rng, list, p, 900f + 600f * (float)rng.NextDouble(),
                               120 + rng.Next(160), 34f, 0.6f);
                }
            }
        }

        // ------------------------------------------------------------------

        void BakeMesh(List<G> list)
        {
            int n = list.Count;
            var verts = new Vector3[n * 4];
            var cols = new Color[n * 4];
            var corners = new Vector2[n * 4];
            var sizeRand = new Vector2[n * 4];
            var pixelFloor = new Vector2[n * 4];
            var tris = new int[n * 6];

            for (int i = 0; i < n; i++)
            {
                var g = list[i];
                float rand = (float)((i * 2654435761u % 1000000u) / 1000000.0);
                for (int k = 0; k < 4; k++)
                {
                    int v = i * 4 + k;
                    verts[v] = g.p;
                    cols[v] = g.tint;
                    corners[v] = new Vector2((k & 1) * 2 - 1, (k >> 1) * 2 - 1);
                    sizeRand[v] = new Vector2(g.size, rand);
                    pixelFloor[v] = new Vector2(Mathf.Max(g.minPx, 1.5f), Mathf.Max(g.boost, 1f));
                }
                int t = i * 6, b = i * 4;
                tris[t] = b; tris[t + 1] = b + 2; tris[t + 2] = b + 1;
                tris[t + 3] = b + 1; tris[t + 4] = b + 2; tris[t + 5] = b + 3;
            }

            mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, sizeRand);
            mesh.SetUVs(2, pixelFloor);
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 400000f);
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        // ---------------- CPU value noise (deterministic) -----------------

        static float Hash(int x, int y, int z)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
        }

        static float VNoise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = p.x - x, fy = p.y - y, fz = p.z - z;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float c000 = Hash(x, y, z), c100 = Hash(x + 1, y, z);
            float c010 = Hash(x, y + 1, z), c110 = Hash(x + 1, y + 1, z);
            float c001 = Hash(x, y, z + 1), c101 = Hash(x + 1, y, z + 1);
            float c011 = Hash(x, y + 1, z + 1), c111 = Hash(x + 1, y + 1, z + 1);
            return Mathf.Lerp(
                Mathf.Lerp(Mathf.Lerp(c000, c100, fx), Mathf.Lerp(c010, c110, fx), fy),
                Mathf.Lerp(Mathf.Lerp(c001, c101, fx), Mathf.Lerp(c011, c111, fx), fy), fz);
        }

        static float Fbm(Vector3 p, int octaves)
        {
            float v = 0f, a = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                v += a * VNoise(p);
                norm += a;
                p = p * 2.17f + new Vector3(11.3f, 17.7f, 23.1f);
                a *= 0.5f;
            }
            return v / norm;
        }

        static Vector3 RandDir(System.Random rng)
        {
            // Marsaglia would be prettier; rejection is clearer.
            while (true)
            {
                var v = new Vector3((float)rng.NextDouble() * 2f - 1f,
                                    (float)rng.NextDouble() * 2f - 1f,
                                    (float)rng.NextDouble() * 2f - 1f);
                float m = v.sqrMagnitude;
                if (m > 0.001f && m < 1f) return v / Mathf.Sqrt(m);
            }
        }

        static Vector3 Gaussian3(System.Random rng)
        {
            float G() {
                double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
                return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1))
                             * System.Math.Cos(2.0 * System.Math.PI * u2));
            }
            return new Vector3(G(), G(), G());
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (mat != null) Destroy(mat);
        }
    }
}
