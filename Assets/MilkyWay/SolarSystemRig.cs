using System.Collections.Generic;
using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// The detailed solar system: the Sun (StarSurface), eight planets on the
    /// PlanetSurface shader with real axial tilts and relative day lengths,
    /// Saturn's rings, and the six classroom moons. Built entirely in code so
    /// it can be spawned anywhere at any scale — today as the zoom journey's
    /// departure prop, later as the stage of its own solar-system tour
    /// (GetBody() is that tour's camera-target API).
    ///
    /// Distances and sizes are NOT to scale — a real Neptune orbit is 1.5e-9
    /// kpc — and follow a legibility mapping instead (orbit ∝ √AU, size ∝
    /// √radius), the "model on the teacher's desk" honesty the journey's
    /// caption already states. Everything physical that CAN survive the
    /// mapping does: orbit ordering, Kepler period ratios, spin direction
    /// (Venus and Uranus retrograde), axial tilts, moon systems.
    /// </summary>
    public class SolarSystemRig : MonoBehaviour
    {
        struct BodyDef
        {
            public string name;
            public float au;            // real orbit, for Kepler ratios + mapping
            public float radiusEarths;  // real size, mapped to visual
            public float tiltDeg;       // real axial tilt (obliquity)
            public float dayHours;      // real rotation period, negative = retrograde
            public string tex;          // Resources/Planets albedo map (photoreal); null = procedural
            public string cloudTex;     // optional observed cloud layer (Earth)
            public string nightTex;     // optional night-side city lights (Earth)
            public System.Action<Material> paint;
            public MoonDef[] moons;
        }

        struct MoonDef
        {
            public string name;
            public float orbitPlanetRadii; // visual, in units of the planet's visual radius
            public float sizeRel;          // visual, relative to its planet's visual radius
            public string tex;             // observed map if we have one (the Moon)
            public Color color;
        }

        class Orbiter
        {
            public Transform pivot;    // orbit position (parent of tilt node)
            public Transform spinner;  // the sphere that spins around its tilted axis
            public float orbitRadius, orbitOmega, angle;
            public float spinDegPerSec;
        }

        // Everything SetRealism needs to re-map a planet on the fly.
        class PlanetRec
        {
            public Orbiter orbiter;
            public float au, radiusEarths;
            public Transform ring;         // Saturn only
            public LineRenderer line;
        }

        /// <summary>Overall footprint: Earth's orbit radius in world units.
        /// The default keeps the journey's original framing (Neptune ≈ 0.06 kpc).</summary>
        public float earthOrbit = 0.011f;
        /// <summary>Earth's visual radius in world units.</summary>
        public float earthSize = 0.0011f;
        /// <summary>Time multiplier on all orbits and spins. The journey runs
        /// at 1 (Mercury laps during the framing beat); a close-up tour dials
        /// it down so the planet being framed holds still enough to study.</summary>
        public float motionScale = 1f;

        readonly List<Orbiter> orbiters = new();
        readonly List<Material> mats = new();
        readonly List<LineRenderer> orbitLines = new();
        readonly Dictionary<string, Transform> bodies = new();
        readonly List<PlanetRec> planetRecs = new();
        readonly List<GameObject> moonPivots = new();
        Transform sunT;
        Mesh ringMesh, sphereMesh;

        // 1 AU = 23,455 Earth radii; Earth's radius in AU, for the true map.
        const float EarthRadiusAU = 4.2635e-5f;
        // 1 AU = 215 solar radii.
        const float SunRadiusAU = 1f / 215f;

        /// <summary>Blend between the exhibit's legibility mapping (0 —
        /// orbits ∝ √AU, sizes ∝ √radius, the "map on the teacher's desk")
        /// and the TRUE proportions (1 — orbits linear in AU, sizes at real
        /// scale, where every planet vanishes into a grain and even the Sun
        /// is nearly a point). Moons hide past 0.3: their legibility offsets
        /// have no honest true-scale counterpart at this footprint.</summary>
        public void SetRealism(float t)
        {
            t = Mathf.Clamp01(t);
            foreach (var rec in planetRecs)
            {
                float displayOrbit = earthOrbit * Mathf.Sqrt(rec.au);
                float trueOrbit = earthOrbit * rec.au;
                rec.orbiter.orbitRadius = Mathf.Lerp(displayOrbit, trueOrbit, t);

                float displaySize = earthSize * Mathf.Sqrt(rec.radiusEarths);
                float trueSize = earthOrbit * EarthRadiusAU * rec.radiusEarths;
                float s = Mathf.Lerp(displaySize, trueSize, t);
                rec.orbiter.spinner.localScale = Vector3.one * s;
                if (rec.ring != null) rec.ring.localScale = Vector3.one * (s / displaySize);

                if (rec.line != null)
                {
                    SetLineRadius(rec.line, rec.orbiter.orbitRadius);
                    // LineRenderer width is WORLD units — it ignores the
                    // transform scale that the positions ride on (found the
                    // hard way: at rig scale ×1000 the lines were sub-pixel
                    // everywhere). Scale it by hand, and widen further toward
                    // the true map's pull-back framing.
                    rec.line.widthMultiplier =
                        Mathf.Lerp(0.00045f, 0.0022f, t) * transform.lossyScale.x;
                }
            }
            if (sunT != null)
                sunT.localScale = Vector3.one * Mathf.Lerp(0.0035f, earthOrbit * SunRadiusAU, t);
            foreach (var m in moonPivots)
                if (m != null && m.activeSelf != (t < 0.3f)) m.SetActive(t < 0.3f);
        }

        static void SetLineRadius(LineRenderer line, float radius)
        {
            int n = line.positionCount;
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * Mathf.PI * 2f;
                line.SetPosition(k, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
        }

        /// <summary>Orbit guide lines read well from the journey's distance
        /// but become ribbons across a close-up frame — a tour hides them
        /// while it is parked at a planet.</summary>
        public void SetOrbitLinesVisible(bool visible)
        {
            foreach (var l in orbitLines)
                if (l != null) l.enabled = visible;
        }

        static Shader PlanetShader => Shader.Find("MilkyWay/PlanetSurface");
        static Shader RingShader => Shader.Find("MilkyWay/PlanetRing");

        /// <summary>Camera-target lookup for tours: "Sun", "Earth", "Moon",
        /// "Jupiter", "Io"… Returns null if unknown.</summary>
        public Transform GetBody(string name) =>
            bodies.TryGetValue(name, out var t) ? t : null;

        public static SolarSystemRig Spawn(Vector3 position, Transform parent = null)
        {
            var go = new GameObject("Solar System");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var rig = go.AddComponent<SolarSystemRig>();
            rig.Build();
            return rig;
        }

        // ------------------------------------------------------------------
        //  authoring table
        // ------------------------------------------------------------------

        static BodyDef[] Defs => new[]
        {
            new BodyDef { name = "Mercury", au = 0.39f, radiusEarths = 0.38f, tiltDeg = 0.03f, dayHours = 1407.6f, tex = "2k_mercury",
                paint = m => {
                    m.SetColor("_BaseColor", new Color(0.55f, 0.51f, 0.47f));
                    m.SetColor("_SecondColor", new Color(0.38f, 0.35f, 0.33f));
                    m.SetFloat("_Mottle", 0.85f);
                    m.SetFloat("_NoiseScale", 9f);
                } },
            new BodyDef { name = "Venus", au = 0.72f, radiusEarths = 0.95f, tiltDeg = 177.4f, dayHours = -5832.5f, tex = "2k_venus_atmosphere",
                paint = m => {
                    m.SetColor("_BaseColor", new Color(0.93f, 0.83f, 0.60f));
                    m.SetColor("_SecondColor", new Color(0.82f, 0.70f, 0.48f));
                    m.SetFloat("_Mottle", 0.2f);
                    m.SetFloat("_Clouds", 0.9f);
                    m.SetFloat("_NoiseScale", 4f);
                    m.SetColor("_RimColor", new Color(0.9f, 0.8f, 0.5f) * 0.35f);
                } },
            new BodyDef { name = "Earth", au = 1f, radiusEarths = 1f, tiltDeg = 23.4f, dayHours = 23.9f,
                tex = "2k_earth_daymap", cloudTex = "2k_earth_clouds", nightTex = "2k_earth_nightmap",
                moons = new[] { new MoonDef { name = "Moon", orbitPlanetRadii = 3.2f, sizeRel = 0.27f, tex = "2k_moon",
                                              color = new Color(0.62f, 0.61f, 0.59f) } },
                paint = m => {
                    m.SetColor("_OceanColor", new Color(0.03f, 0.13f, 0.38f));
                    m.SetColor("_SecondColor", new Color(0.19f, 0.33f, 0.10f));
                    m.SetColor("_PoleColor", new Color(0.93f, 0.96f, 1f));
                    m.SetFloat("_Continents", 1f);
                    // Sea level low enough that a hemisphere is never all
                    // ocean; ice starts at sin(lat) 0.94 ≈ 70° — polar caps,
                    // not a white bib over the temperate zone.
                    m.SetFloat("_SeaLevel", 0.48f);
                    m.SetFloat("_IceCap", 0.94f);
                    m.SetFloat("_Clouds", 0.42f);
                    m.SetFloat("_Mottle", 0f);
                    m.SetFloat("_NoiseScale", 5f);
                    m.SetColor("_RimColor", new Color(0.35f, 0.55f, 1f) * 0.35f);
                } },
            new BodyDef { name = "Mars", au = 1.52f, radiusEarths = 0.53f, tiltDeg = 25.2f, dayHours = 24.6f, tex = "2k_mars",
                paint = m => {
                    m.SetColor("_BaseColor", new Color(0.72f, 0.38f, 0.22f));
                    m.SetColor("_SecondColor", new Color(0.45f, 0.24f, 0.15f));
                    m.SetFloat("_Mottle", 0.6f);
                    m.SetFloat("_IceCap", 0.88f);
                    m.SetFloat("_NoiseScale", 7f);
                    m.SetColor("_RimColor", new Color(0.8f, 0.55f, 0.4f) * 0.15f);
                } },
            new BodyDef { name = "Jupiter", au = 5.2f, radiusEarths = 11.2f, tiltDeg = 3.1f, dayHours = 9.9f, tex = "2k_jupiter",
                moons = new[] {
                    new MoonDef { name = "Io",       orbitPlanetRadii = 1.9f, sizeRel = 0.09f, color = new Color(0.85f, 0.75f, 0.35f) },
                    new MoonDef { name = "Europa",   orbitPlanetRadii = 2.4f, sizeRel = 0.08f, color = new Color(0.80f, 0.78f, 0.72f) },
                    new MoonDef { name = "Ganymede", orbitPlanetRadii = 3.0f, sizeRel = 0.13f, color = new Color(0.60f, 0.56f, 0.50f) },
                    new MoonDef { name = "Callisto", orbitPlanetRadii = 3.7f, sizeRel = 0.12f, color = new Color(0.45f, 0.42f, 0.38f) },
                },
                paint = m => {
                    m.SetColor("_BaseColor", new Color(0.85f, 0.76f, 0.62f));
                    m.SetColor("_SecondColor", new Color(0.62f, 0.46f, 0.32f));
                    m.SetFloat("_BandFreq", 9f);
                    m.SetFloat("_BandWarp", 0.4f);
                    m.SetFloat("_Mottle", 0.12f);
                    m.SetFloat("_NoiseScale", 5f);
                    m.SetFloat("_Spot", 0.85f);
                    m.SetColor("_SpotColor", new Color(0.72f, 0.30f, 0.18f));
                } },
            new BodyDef { name = "Saturn", au = 9.54f, radiusEarths = 9.4f, tiltDeg = 26.7f, dayHours = 10.7f, tex = "2k_saturn",
                moons = new[] { new MoonDef { name = "Titan", orbitPlanetRadii = 3.4f, sizeRel = 0.14f,
                                              color = new Color(0.85f, 0.65f, 0.35f) } },
                paint = m => {
                    m.SetColor("_BaseColor", new Color(0.90f, 0.83f, 0.64f));
                    m.SetColor("_SecondColor", new Color(0.74f, 0.65f, 0.46f));
                    m.SetFloat("_BandFreq", 7f);
                    m.SetFloat("_BandWarp", 0.25f);
                    m.SetFloat("_Mottle", 0.08f);
                    m.SetFloat("_NoiseScale", 4f);
                } },
            new BodyDef { name = "Uranus", au = 19.2f, radiusEarths = 4.0f, tiltDeg = 97.8f, dayHours = -17.2f, tex = "2k_uranus",
                paint = m => {
                    m.SetColor("_BaseColor", new Color(0.62f, 0.85f, 0.88f));
                    m.SetColor("_SecondColor", new Color(0.52f, 0.76f, 0.82f));
                    m.SetFloat("_BandFreq", 4f);
                    m.SetFloat("_BandWarp", 0.15f);
                    m.SetFloat("_Mottle", 0.05f);
                    m.SetColor("_RimColor", new Color(0.5f, 0.8f, 0.85f) * 0.25f);
                } },
            new BodyDef { name = "Neptune", au = 30.1f, radiusEarths = 3.9f, tiltDeg = 28.3f, dayHours = 16.1f, tex = "2k_neptune",
                paint = m => {
                    m.SetColor("_BaseColor", new Color(0.20f, 0.35f, 0.85f));
                    m.SetColor("_SecondColor", new Color(0.12f, 0.24f, 0.62f));
                    m.SetFloat("_BandFreq", 5f);
                    m.SetFloat("_BandWarp", 0.3f);
                    m.SetFloat("_Mottle", 0.1f);
                    m.SetFloat("_Spot", 0.4f);
                    m.SetColor("_SpotColor", new Color(0.85f, 0.9f, 1f));
                    m.SetColor("_RimColor", new Color(0.3f, 0.45f, 1f) * 0.35f);
                } },
        };

        // ------------------------------------------------------------------
        //  construction
        // ------------------------------------------------------------------

        void Build()
        {
            // Close-up-grade sphere: the Unity primitive's silhouette turns
            // polygonal when a planet fills the frame.
            sphereMesh = BuildSphere(96, 48);

            BuildSun();
            var lineShader = Shader.Find("Sprites/Default");
            int index = 0;
            foreach (var def in Defs)
            {
                float orbit = earthOrbit * Mathf.Sqrt(def.au);
                float size = earthSize * Mathf.Sqrt(def.radiusEarths);

                var pivot = new GameObject(def.name).transform;
                pivot.SetParent(transform, false);

                // Tilt node: the spin axis leans by the real obliquity. Tilts
                // over 90° (Venus, Uranus) come out as the retrograde/rolling
                // rotations they really are.
                var tiltNode = new GameObject("Axis").transform;
                tiltNode.SetParent(pivot, false);
                tiltNode.localRotation = Quaternion.Euler(0f, 0f, def.tiltDeg);

                var mat = new Material(PlanetShader);
                mat.SetVector("_SunPos", transform.position);
                def.paint(mat);
                ApplyRealMap(mat, def.tex, def.cloudTex, def.nightTex);
                var sphere = MakeSphere("Surface", tiltNode, size, mat);
                mats.Add(mat);

                var orbiter = new Orbiter
                {
                    pivot = pivot,
                    spinner = sphere,
                    orbitRadius = orbit,
                    // Kepler ratios (ω ∝ a^-1.5), scaled so Mercury laps
                    // during the journey's framing beat.
                    orbitOmega = 0.55f / Mathf.Pow(def.au, 1.5f),
                    angle = index * 137.5f * Mathf.Deg2Rad,
                    // Relative day lengths survive; the scale makes Jupiter's
                    // 10-hour spin visible without strobing.
                    spinDegPerSec = 240f / def.dayHours,
                };
                orbiters.Add(orbiter);
                bodies[def.name] = pivot;

                Transform ring = null;
                if (def.name == "Saturn") ring = BuildRings(tiltNode, size, mat);
                if (def.moons != null) BuildMoons(def, pivot, size, index);

                var line = BuildOrbitLine(lineShader, orbit);
                planetRecs.Add(new PlanetRec
                {
                    orbiter = orbiter, au = def.au, radiusEarths = def.radiusEarths,
                    ring = ring, line = line,
                });
                index++;
            }
        }

        /// <summary>Swap a body onto its observed albedo map (NASA/USGS data
        /// via Solar System Scope, see Resources/Planets/ATTRIBUTION.txt).
        /// The paint lambda stays authored as the fallback: if the map is
        /// missing from Resources the planet is procedural, not magenta.
        /// A photograph already contains the ice caps, storms, continents
        /// and bands, so every procedural overlay that would double them up
        /// is switched off; lighting, rim atmosphere and clouds still run.</summary>
        static void ApplyRealMap(Material m, string tex, string cloudTex, string nightTex = null)
        {
            if (string.IsNullOrEmpty(tex)) return;
            var map = Resources.Load<Texture2D>("Planets/" + tex);
            if (map == null) return;

            var night = string.IsNullOrEmpty(nightTex) ? null : Resources.Load<Texture2D>("Planets/" + nightTex);
            if (night != null)
            {
                m.SetTexture("_NightTex", night);
                m.SetFloat("_NightStrength", 1f);
            }

            m.SetTexture("_MainTex", map);
            m.SetFloat("_TexStrength", 1f);
            m.SetFloat("_Mottle", 0f);
            m.SetFloat("_BandFreq", 0f);
            m.SetFloat("_Continents", 0f);
            m.SetFloat("_IceCap", 1.2f);
            m.SetFloat("_Spot", 0f);

            var clouds = string.IsNullOrEmpty(cloudTex) ? null : Resources.Load<Texture2D>("Planets/" + cloudTex);
            if (clouds != null)
            {
                m.SetTexture("_CloudTex", clouds);
                m.SetFloat("_UseCloudTex", 1f);
                m.SetFloat("_Clouds", 0.85f);
            }
            else
            {
                // No separate weather layer: the map is the whole look
                // (Venus's map IS its cloud deck), so procedural clouds off.
                m.SetFloat("_Clouds", 0f);
            }
        }

        Transform MakeSphere(string name, Transform parent, float radius, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * radius; // unit-RADIUS mesh
            go.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Unit-radius UV sphere, poles on +/-Y.</summary>
        static Mesh BuildSphere(int lonSegs, int latSegs)
        {
            var verts = new Vector3[(lonSegs + 1) * (latSegs + 1)];
            var tris = new int[lonSegs * latSegs * 6];
            for (int y = 0; y <= latSegs; y++)
            {
                float v = y / (float)latSegs;
                float phi = (v - 0.5f) * Mathf.PI;
                for (int x = 0; x <= lonSegs; x++)
                {
                    float theta = x / (float)lonSegs * Mathf.PI * 2f;
                    verts[y * (lonSegs + 1) + x] = new Vector3(
                        Mathf.Cos(phi) * Mathf.Cos(theta),
                        Mathf.Sin(phi),
                        Mathf.Cos(phi) * Mathf.Sin(theta));
                }
            }
            int t = 0;
            for (int y = 0; y < latSegs; y++)
                for (int x = 0; x < lonSegs; x++)
                {
                    int a = y * (lonSegs + 1) + x;
                    int b = a + lonSegs + 1;
                    // Winding so the OUTSIDE is the front face — get it
                    // backwards and Cull Back shows the far hemisphere's
                    // interior: a fully-lit "night side" that looks almost
                    // right. It shipped backwards at first, and only a
                    // day/night pixel probe caught it (night face read 3×
                    // brighter than day): from outside, a→b→a+1 must trace
                    // clockwise. Chirality check for the maps: Florida hooks
                    // toward Cuba, Africa sits EAST of Brazil.
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.normals = verts; // unit sphere: position == normal
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildSun()
        {
            // Same close-up-grade sphere as the planets: the primitive's
            // silhouette reads polygonal when the Sun fills a tour frame.
            // NOTE the unit-RADIUS mesh vs the primitive's 0.5 — the scale
            // below is half the old primitive scale for the same world size.
            var sun = new GameObject("Sun");
            sun.transform.SetParent(transform, false);
            sun.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            var sunMR = sun.AddComponent<MeshRenderer>();
            sunMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Small enough that Mercury's mapped orbit clears it comfortably —
            // with the √AU compression a bigger sun would swallow the inner
            // system.
            sun.transform.localScale = Vector3.one * 0.0035f;
            var starShader = Shader.Find("BlackHole/StarSurface");
            if (starShader != null)
            {
                var m = new Material(starShader);
                m.SetColor("_StarColor", new Color(2.6f, 2.2f, 1.4f)); // G-type warmth
                m.SetFloat("_Granulation", 0.45f);
                m.SetFloat("_GranScale", 8f);
                m.SetFloat("_SpotStrength", 0.22f);
                m.SetFloat("_CoronaBoost", 0.8f);
                // Observed photosphere (opt-in shader path; the black-hole
                // exhibits keep their procedural stars untouched).
                var sunMap = Resources.Load<Texture2D>("Planets/2k_sun");
                if (sunMap != null)
                {
                    m.SetTexture("_SurfaceTex", sunMap);
                    m.SetFloat("_SurfaceTexStrength", 0.85f);
                }
                sun.GetComponent<MeshRenderer>().sharedMaterial = m;
                mats.Add(m);
            }
            bodies["Sun"] = sun.transform;
            sunT = sun.transform;
        }

        void BuildMoons(BodyDef def, Transform planetPivot, float planetSize, int planetIndex)
        {
            for (int i = 0; i < def.moons.Length; i++)
            {
                var md = def.moons[i];
                var pivot = new GameObject(md.name).transform;
                pivot.SetParent(planetPivot, false);

                var mat = new Material(PlanetShader);
                mat.SetVector("_SunPos", transform.position);
                mat.SetColor("_BaseColor", md.color);
                mat.SetColor("_SecondColor", md.color * 0.6f);
                mat.SetFloat("_Mottle", 0.8f);
                mat.SetFloat("_NoiseScale", 11f);
                ApplyRealMap(mat, md.tex, null);
                var sphere = MakeSphere("Surface", pivot, planetSize * md.sizeRel, mat);
                mats.Add(mat);

                moonPivots.Add(pivot.gameObject);
                orbiters.Add(new Orbiter
                {
                    pivot = pivot,
                    spinner = sphere,
                    orbitRadius = planetSize * md.orbitPlanetRadii,
                    // Inner moons run faster — Kepler again, in miniature.
                    orbitOmega = 3.2f / Mathf.Pow(md.orbitPlanetRadii, 1.5f),
                    angle = (planetIndex * 3 + i) * 2.1f,
                    spinDegPerSec = 4f, // tidally locked in spirit; a slow turn
                });
                bodies[md.name] = pivot;
            }
        }

        Transform BuildRings(Transform tiltNode, float planetSize, Material planetMat)
        {
            var go = new GameObject("Rings");
            go.transform.SetParent(tiltNode, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            ringMesh = BuildAnnulus(planetSize * 1.25f, planetSize * 2.4f, 96);
            mf.sharedMesh = ringMesh;
            var mat = new Material(RingShader);
            mat.SetVector("_SunPos", transform.position);
            var strip = Resources.Load<Texture2D>("Planets/2k_saturn_ring_alpha");
            if (strip != null)
            {
                mat.SetTexture("_RingTex", strip);
                mat.SetFloat("_RingTexStrength", 1f);
            }
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mats.Add(mat);
            return go.transform;
        }

        static Mesh BuildAnnulus(float rIn, float rOut, int segments)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var norms = new Vector3[verts.Length];
            var tris = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * rIn;
                verts[i * 2 + 1] = dir * rOut;
                uvs[i * 2] = new Vector2(0f, 0f);
                uvs[i * 2 + 1] = new Vector2(1f, 0f);
                norms[i * 2] = norms[i * 2 + 1] = Vector3.up;
                if (i < segments)
                {
                    int b = i * 2, t = i * 6;
                    tris[t] = b; tris[t + 1] = b + 2; tris[t + 2] = b + 1;
                    tris[t + 3] = b + 1; tris[t + 4] = b + 2; tris[t + 5] = b + 3;
                }
            }
            var mesh = new Mesh { vertices = verts, uv = uvs, normals = norms, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        LineRenderer BuildOrbitLine(Shader lineShader, float radius)
        {
            var line = new GameObject("Orbit").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            // 256 segments: at the exhibit's close-up framings a 96-gon reads
            // as a polygon, not an orbit.
            const int N = 256;
            line.positionCount = N;
            line.numCornerVertices = 4;
            line.loop = true;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.00045f;
            line.material = new Material(lineShader);
            mats.Add(line.material);
            line.startColor = line.endColor = new Color(0.6f, 0.7f, 0.9f, 0.22f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int k = 0; k < N; k++)
            {
                float a = k / (float)N * Mathf.PI * 2f;
                line.SetPosition(k, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            orbitLines.Add(line);
            return line;
        }

        // ------------------------------------------------------------------

        bool lineWidthSynced;

        void Update()
        {
            // Spawn() applies the exhibit scale AFTER Build(), so world-space
            // line widths can only be fixed up once we are live.
            if (!lineWidthSynced)
            {
                lineWidthSynced = true;
                SetRealism(0f);
            }

            float dt = Time.deltaTime * motionScale;
            foreach (var o in orbiters)
            {
                o.angle += o.orbitOmega * dt;
                o.pivot.localPosition = new Vector3(Mathf.Cos(o.angle), 0f, Mathf.Sin(o.angle)) * o.orbitRadius;
                o.spinner.Rotate(0f, o.spinDegPerSec * dt, 0f, Space.Self);
            }
        }

        // Materials and the generated meshes are created per spawn; destroying
        // the GameObject does not free them.
        void OnDestroy()
        {
            foreach (var m in mats)
                if (m != null) Destroy(m);
            if (ringMesh != null) Destroy(ringMesh);
            if (sphereMesh != null) Destroy(sphereMesh);
        }
    }
}
