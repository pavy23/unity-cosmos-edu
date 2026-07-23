using System.Collections.Generic;
using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// Bright embedded stars for a nebula — the single thing that separates a
    /// real astrophoto from a CGI cloud. A handful of brilliant stars with
    /// diffraction spikes (the ionizing cluster: Orion's Trapezium, the
    /// Pleiades' blue giants) plus a scatter of fainter field stars, all
    /// additive billboards facing the camera. Phase-0 prototype.
    /// </summary>
    public class NebulaStars : MonoBehaviour
    {
        // Serialized config: the showcase builder fills these and flips
        // configureOnStart so the stars build themselves at runtime. (The
        // prototype instead calls Configure() directly each preset change.)
        public bool configureOnStart;
        public Color cfgBright = Color.white, cfgField = Color.white;
        public int cfgBrightN, cfgFieldN;
        public float cfgRadius = 8f, cfgCoreRadius = 1f;

        // Gentle scintillation — real skies are never frozen. Kept subtle so it
        // reads as atmosphere, not blinking lights.
        public float twinkleAmount = 1f;

        Texture2D spikeTex, dotTex;
        Material spikeMat, dotMat;
        readonly List<Transform> stars = new();
        readonly List<float> baseScale = new();
        readonly List<Color> baseColor = new();
        readonly List<Material> starMats = new();
        readonly List<float> twinklePhase = new();
        readonly List<float> twinkleSpeed = new();
        Camera cam;

        void Awake()
        {
            spikeTex = BuildSpike(160);
            dotTex = BuildDot(64);
            spikeMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = spikeTex, renderQueue = 3200 };
            dotMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = dotTex, renderQueue = 3200 };
        }

        void Start()
        {
            if (configureOnStart)
                Configure(cfgBright, cfgField, cfgBrightN, cfgFieldN, cfgRadius, cfgCoreRadius);
        }

        /// <summary>Place a fresh set of stars. brightCount brilliant spiked
        /// stars near the core, plus fieldCount faint dots through the volume.</summary>
        public void Configure(Color brightTint, Color fieldTint, int brightCount,
            int fieldCount, float radius, float coreRadius)
        {
            Clear();
            var rng = new System.Random(91);

            for (int i = 0; i < brightCount; i++)
            {
                // Tight central knot (the ionizing cluster). Small, sharp cores —
                // the diffraction spikes give them presence, not raw size.
                Vector3 p = RandBall(rng) * coreRadius * (0.25f + 0.75f * (float)rng.NextDouble());
                float size = radius * (0.05f + 0.03f * (float)rng.NextDouble());
                Color c = brightTint * (0.9f + 0.5f * (float)rng.NextDouble());
                MakeStar(p, size, c, spikeMat);
            }
            for (int i = 0; i < fieldCount; i++)
            {
                Vector3 p = RandBall(rng) * radius * Mathf.Pow((float)rng.NextDouble(), 0.5f);
                float size = radius * (0.012f + 0.03f * Mathf.Pow((float)rng.NextDouble(), 3f));
                bool spiky = rng.NextDouble() < 0.12;
                Color c = Color.Lerp(fieldTint, brightTint, (float)rng.NextDouble())
                          * (0.6f + 0.9f * (float)rng.NextDouble());
                MakeStar(p, size, c, spiky ? spikeMat : dotMat);
            }
        }

        void MakeStar(Vector3 localPos, float size, Color c, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "NebStar";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            var mr = go.GetComponent<MeshRenderer>();
            var m = new Material(mat) { color = c };
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            stars.Add(go.transform);
            baseScale.Add(size);
            baseColor.Add(c);
            starMats.Add(m);
            // Uncorrelated phases/rates so the field shimmers, never pulses in sync.
            twinklePhase.Add((float)(new System.Random(stars.Count * 7919 + 13).NextDouble()) * Mathf.PI * 2f);
            twinkleSpeed.Add(1.2f + (stars.Count * 0.37f % 1f) * 2.6f);
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float time = Time.time;
            for (int i = 0; i < stars.Count; i++)
            {
                var t = stars[i];
                if (t == null) continue;
                // Face the camera (billboard). Scale gently with distance so the
                // spikes read at any range.
                t.rotation = cam.transform.rotation;

                if (twinkleAmount > 0f)
                {
                    float s = Mathf.Sin(time * twinkleSpeed[i] + twinklePhase[i]);
                    t.localScale = Vector3.one * baseScale[i] * (1f + 0.10f * s * twinkleAmount);
                    starMats[i].color = baseColor[i] * (1f + 0.18f * s * twinkleAmount);
                }
            }
        }

        Vector3 RandBall(System.Random rng)
        {
            float z = 2f * (float)rng.NextDouble() - 1f;
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rxy = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(Mathf.Cos(a) * rxy, z, Mathf.Sin(a) * rxy);
        }

        // Bright core + four diffraction spikes + a soft halo.
        static Texture2D BuildSpike(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float rr = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Exp(-rr * rr * 90f);           // tight bright point
                    float halo = Mathf.Exp(-rr * rr * 6f) * 0.16f;
                    // Thin, long diffraction spikes (a 4-point cross).
                    float sh = Mathf.Exp(-Mathf.Abs(dy) * 160f) * Mathf.Exp(-Mathf.Abs(dx) * 1.6f);
                    float sv = Mathf.Exp(-Mathf.Abs(dx) * 160f) * Mathf.Exp(-Mathf.Abs(dy) * 1.6f);
                    float a = Mathf.Clamp01(core + halo + (sh + sv) * 0.5f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D BuildDot(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float rr = dx * dx + dy * dy;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Exp(-rr * 7f))));
                }
            t.Apply();
            return t;
        }

        void Clear()
        {
            foreach (var t in stars) if (t != null) Destroy(t.gameObject);
            stars.Clear(); baseScale.Clear(); baseColor.Clear();
            starMats.Clear(); twinklePhase.Clear(); twinkleSpeed.Clear();
        }

        void OnDestroy()
        {
            Clear();
            if (spikeTex != null) Destroy(spikeTex);
            if (dotTex != null) Destroy(dotTex);
            if (spikeMat != null) Destroy(spikeMat);
            if (dotMat != null) Destroy(dotMat);
        }
    }
}
