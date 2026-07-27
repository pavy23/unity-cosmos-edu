using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// A star cluster as a baked billboard point cloud, rendered with the same
    /// MilkyWay/GalaxyStars shader the galaxy uses. Two shapes: an OPEN cluster
    /// (loose, young blue-white, a few hundred stars) and a GLOBULAR cluster
    /// (a dense old sphere, thousands of yellow-red stars, King-like central
    /// concentration). Phase-0 prototype for the nebulae &amp; clusters exhibit.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ClusterField : MonoBehaviour
    {
        public enum Kind { Open, Globular }
        public Kind kind = Kind.Globular;
        public Material material;
        public int starCount = 4000;
        public float radius = 6f;
        public int seed = 7;

        Mesh mesh;

        // Baked once and kept. The MR exhibit shows one specimen at a time by
        // toggling the others inactive, so an OnEnable/OnDisable pair that
        // rebuilt and threw away the mesh re-baked 9,000 stars — 36,000 verts
        // across five arrays, on the main thread — every time the visitor
        // pressed next past this one. On a headset that stall is not a hitch,
        // it is a dropped frame the compositor has to cover for.
        void OnEnable()
        {
            if (mesh == null) Build();
        }

        void Build()
        {
            if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;
            if (kind == Kind.Open) { starCount = Mathf.Min(starCount, 600); }
            mesh = Bake();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        Mesh Bake()
        {
            int n = starCount;
            var rng = new System.Random(seed);
            var verts = new Vector3[n * 4];
            var cols = new Color32[n * 4];
            var uv0 = new Vector2[n * 4];
            var uv1 = new Vector2[n * 4];
            var idx = new int[n * 6];

            for (int s = 0; s < n; s++)
            {
                // Radial profile: globular concentrates hard toward the core,
                // open is a loose gaussian ball.
                double u = rng.NextDouble();
                float r = kind == Kind.Globular
                    ? radius * Mathf.Pow((float)u, 1.8f)          // dense core
                    : radius * (0.35f + 0.65f * (float)u);        // looser
                Vector3 dir = RandomOnSphere(rng);
                Vector3 p = dir * r;
                if (kind == Kind.Open) p.y *= 0.8f; // mild flattening

                // Colour: globular = old (yellow/orange/red giants) with a few hot
                // blue stragglers; open = young (blue/white). Saturated deliberately
                // so the warm tone survives additive HDR bloom + tonemapping.
                Color c = kind == Kind.Globular
                    ? Palette(rng, new[] {
                        (new Color(1.7f, 1.35f, 0.78f), 0.42f), // yellow
                        (new Color(1.85f, 1.0f, 0.42f), 0.34f), // orange
                        (new Color(1.9f, 0.62f, 0.32f), 0.16f), // red giant
                        (new Color(1.05f, 1.35f, 2.0f), 0.08f) })// blue straggler
                    : Palette(rng, new[] {
                        (new Color(1.3f, 1.5f, 2.0f), 0.45f),  // blue
                        (new Color(1.7f, 1.75f, 1.9f), 0.4f),  // blue-white
                        (new Color(1.9f, 1.7f, 1.4f), 0.15f) });// white-yellow

                float size = 0.02f + 0.05f * Mathf.Pow((float)rng.NextDouble(), 6f);
                float lum = 0.35f + 0.65f * Mathf.Pow((float)rng.NextDouble(), 2f);
                c.a = lum;

                int v = s * 4;
                verts[v] = verts[v + 1] = verts[v + 2] = verts[v + 3] = p;
                var c32 = (Color32)c;
                cols[v] = cols[v + 1] = cols[v + 2] = cols[v + 3] = c32;
                uv0[v] = new Vector2(-1, -1); uv0[v + 1] = new Vector2(1, -1);
                uv0[v + 2] = new Vector2(1, 1); uv0[v + 3] = new Vector2(-1, 1);
                float rand = (float)rng.NextDouble();
                uv1[v] = uv1[v + 1] = uv1[v + 2] = uv1[v + 3] = new Vector2(size, rand);

                int t = s * 6;
                idx[t] = v; idx[t + 1] = v + 2; idx[t + 2] = v + 1;
                idx[t + 3] = v; idx[t + 4] = v + 3; idx[t + 5] = v + 2;
            }

            var m = new Mesh
            {
                name = "Cluster " + kind,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            m.vertices = verts; m.colors32 = cols; m.uv = uv0; m.uv2 = uv1; m.triangles = idx;
            m.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.4f);
            return m;
        }

        static Vector3 RandomOnSphere(System.Random rng)
        {
            float z = 2f * (float)rng.NextDouble() - 1f;
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rxy = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(Mathf.Cos(a) * rxy, z, Mathf.Sin(a) * rxy);
        }

        static Color Palette(System.Random rng, (Color col, float w)[] mix)
        {
            float pick = (float)rng.NextDouble();
            foreach (var (col, w) in mix) { if (pick < w) return col; pick -= w; }
            return mix[mix.Length - 1].col;
        }

        // Not OnDisable: being hidden is not being finished with. The mesh is
        // HideAndDontSave, so nothing else will collect it — this is the only
        // place it goes.
        void OnDestroy()
        {
            if (mesh != null) { DestroyImmediate(mesh); mesh = null; }
        }
    }
}
