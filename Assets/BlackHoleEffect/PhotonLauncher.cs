using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlackHoleEffect
{
    /// <summary>
    /// Educational geodesic launcher: fires photons past the black hole and
    /// draws their bent trajectories, integrating the same Schwarzschild
    /// null-geodesic ODE the shader uses (units GM = c = 1, Rs = 2).
    ///
    /// Geodesics are planar, so each trajectory lives in a 2D plane through
    /// the hole. That plane is re-oriented toward the camera every frame —
    /// otherwise it becomes edge-on (and hidden behind the raymarch quad)
    /// when you orbit, and the lines seem to vanish. A glowing photon head
    /// travels along each trail while it is revealed.
    /// </summary>
    [ExecuteAlways]
    public class PhotonLauncher : MonoBehaviour
    {
        public Transform blackHole;
        [Tooltip("Impact parameter in Rs for single shots. Critical value is ~2.6 Rs.")]
        [Range(0.5f, 8f)] public float impactParameter = 2.6f;
        [Range(3, 11)] public int sweepCount = 7;
        public float launchDistanceRs = 11f;
        [Tooltip("Photon travel animation speed in play mode (sim units/second).")]
        public float animationSpeed = 22f;
        public int maxSteps = 6000;
        public Color escapeColor = new Color(0.45f, 0.9f, 1f, 0.9f);
        public Color captureColor = new Color(1f, 0.4f, 0.3f, 0.9f);

        class Trail
        {
            public LineRenderer line;
            public List<Vector2> simPoints;   // 2D geodesic-plane coordinates
            public Vector3[] worldBuffer;
            public float revealed;            // points revealed so far (play mode)
            public bool captured;
            public Transform head;            // glowing photon "spark"
        }

        /// <summary>True while any fired trajectory is on screen — lets the
        /// theory panel pick the geodesic card during free exploration.</summary>
        public bool HasTrails => trails.Count > 0;

        readonly List<Trail> trails = new List<Trail>();
        Material escapeMat, captureMat, headMat;
        Texture2D headTex;

        [ContextMenu("Fire One")]
        public void FireOne() => Fire(impactParameter);

        [ContextMenu("Fire Sweep")]
        public void FireSweep()
        {
            ClearTrails();
            for (int i = 0; i < sweepCount; i++)
                Fire(Mathf.Lerp(1.4f, 4.6f, i / (float)(sweepCount - 1)));

            // Tell the user WHAT this shows (suppressed during the tour,
            // which narrates the same thing itself).
            if (Application.isPlaying)
                ExplainCard.Show(
                    Loc.T("빛의 휘어짐 — 광자 궤적", "Bending of Light — Photon Paths",
                          "光の湾曲 — 光子の軌跡", "光线弯曲 — 光子轨迹"),
                    Loc.T("광자들이 블랙홀 곁을 지나며 궤적이 휩니다. 가까이 지날수록 더 크게 휘고, 임계거리(약 2.6 Rs)보다 안쪽을 지나는 광자는 붉게 표시되며 탈출하지 못하고 포획됩니다.",
                          "Photons curve as they pass the black hole — the closer, the sharper the bend. Photons passing inside the critical distance (about 2.6 Rs) turn red and are captured.",
                          "光子はブラックホールのそばを通ると軌跡が曲がります。近いほど大きく曲がり、臨界距離（約2.6 Rs）より内側を通る光子は赤く表示され、脱出できずに捕獲されます。",
                          "光子经过黑洞时轨迹被弯曲——离得越近，弯得越厉害。经过临界距离（约2.6 Rs）以内的光子显示为红色，无法逃脱而被捕获。"));
        }

        public void ToggleSweep()
        {
            if (trails.Count > 0) ClearTrails();
            else FireSweep();
        }

        [ContextMenu("Clear Trails")]
        public void ClearTrails()
        {
            foreach (var t in trails)
                if (t.line != null) DestroyImmediate(t.line.gameObject);
            trails.Clear();
        }

        void OnDisable() => ClearTrails();

        void Update()
        {
            if (!Application.isPlaying) return;
            // Space toggles: fire a sweep, press again to clear.
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) ToggleSweep();
#else
            if (Input.GetKeyDown(KeyCode.Space)) ToggleSweep();
#endif
            if (blackHole == null || trails.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;

            // Shared camera-facing basis for every trail, refreshed per frame
            // so the geodesic plane always faces the viewer.
            float rs = blackHole.lossyScale.x;
            Vector3 center = blackHole.position;
            GetBasis(cam, center, out Vector3 right, out Vector3 up, out Vector3 toCam);

            foreach (var t in trails)
            {
                if (t.line == null || t.simPoints == null) continue;

                // Progressive reveal so the photon visibly travels.
                bool travelling = t.revealed < t.simPoints.Count;
                if (travelling)
                    t.revealed = Mathf.Min(t.simPoints.Count, t.revealed + animationSpeed * Time.deltaTime);
                int n = Mathf.Max(2, (int)t.revealed);

                for (int i = 0; i < n; i++)
                {
                    Vector2 p = t.simPoints[i];
                    t.worldBuffer[i] = center + (right * p.x + up * p.y) * rs * 0.5f + toCam * 0.8f * rs;
                }
                t.line.positionCount = n;
                for (int i = 0; i < n; i++) t.line.SetPosition(i, t.worldBuffer[i]);

                // The photon itself: a glowing spark riding the trail head.
                if (t.head != null)
                {
                    bool show = travelling && n >= 2;
                    t.head.gameObject.SetActive(show);
                    if (show)
                    {
                        t.head.position = t.worldBuffer[n - 1];
                        t.head.rotation = cam.transform.rotation;
                        t.head.localScale = Vector3.one * (0.22f * rs * (1f + 0.15f * Mathf.Sin(Time.time * 21f)));
                    }
                }
            }
        }

        static void GetBasis(Camera cam, Vector3 center, out Vector3 right, out Vector3 up, out Vector3 toCam)
        {
            toCam = (cam.transform.position - center).normalized;
            Vector3 upRef = Mathf.Abs(toCam.y) > 0.98f ? Vector3.right : Vector3.up;
            right = Vector3.Cross(upRef, -toCam).normalized;
            up = Vector3.Cross(-toCam, right);
        }

        /// <summary>Fire a photon with the given impact parameter (Rs units)
        /// in the camera-facing plane through the hole.</summary>
        public void Fire(float bRs)
        {
            if (blackHole == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            // Simulation in the 2D plane (Schwarzschild geodesics are planar).
            Vector2 p = new Vector2(-launchDistanceRs * 2f, bRs * 2f);
            Vector2 v = Vector2.right;
            float h2 = (p.x * v.y - p.y * v.x); h2 *= h2;

            var pts = new List<Vector2>(512);
            bool captured = false;
            for (int i = 0; i < maxSteps; i++)
            {
                float r = p.magnitude;
                if (r < 2.02f) { captured = true; break; }
                if (r > launchDistanceRs * 2.6f && Vector2.Dot(p, v) > 0f) break;
                float dt = Mathf.Clamp(0.09f * (r - 1.9f), 0.015f, 0.3f);
                float r5 = r * r * r * r * r;
                v += -1.5f * h2 * dt / r5 * p;
                p += v * dt;
                if ((i & 1) == 0) pts.Add(p);
            }
            if (pts.Count < 2) return;

            EnsureMaterials();
            float rs = blackHole.lossyScale.x;
            Vector3 center = blackHole.position;

            var go = new GameObject("Photon Trail b=" + bRs.ToString("0.0") + (captured ? " (captured)" : " (escaped)"))
            { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.material = captured ? captureMat : escapeMat;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.widthMultiplier = 0.09f * Mathf.Max(rs, 0.1f);
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.45f), new Keyframe(0.8f, 1f), new Keyframe(1f, 1.25f));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.useWorldSpace = true;
            line.startColor = line.endColor = Color.white; // the shader shapes the gradient

            var trail = new Trail
            {
                line = line,
                simPoints = pts,
                worldBuffer = new Vector3[pts.Count],
                captured = captured,
            };

            bool instant = !Application.isPlaying;
            if (instant)
            {
                // Edit mode: no per-frame Update, draw fully with current camera.
                GetBasis(cam, center, out Vector3 right, out Vector3 up, out Vector3 toCam);
                line.positionCount = pts.Count;
                for (int i = 0; i < pts.Count; i++)
                    line.SetPosition(i, center + (right * pts[i].x + up * pts[i].y) * rs * 0.5f + toCam * 0.8f * rs);
                trail.revealed = pts.Count;
            }
            else
            {
                line.positionCount = 2;
                trail.revealed = 2;
                trail.head = MakeHead(go.transform, captured);
            }

            trails.Add(trail);
        }

        Transform MakeHead(Transform parent, bool captured)
        {
            var head = GameObject.CreatePrimitive(PrimitiveType.Quad);
            head.name = "Photon";
            head.hideFlags = HideFlags.HideAndDontSave;
            DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(parent, false);
            var mr = head.GetComponent<MeshRenderer>();
            mr.sharedMaterial = headMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock();
            Color c = captured ? captureColor : escapeColor;
            mpb.SetColor("_Tint", new Color(c.r * 3f, c.g * 3f, c.b * 3f, 1f));
            mr.SetPropertyBlock(mpb);
            return head.transform;
        }

        void EnsureMaterials()
        {
            if (escapeMat == null)
            {
                escapeMat = new Material(Shader.Find("BlackHole/PhotonTrail")) { hideFlags = HideFlags.HideAndDontSave };
                escapeMat.SetColor("_Tint", new Color(escapeColor.r * 2.6f, escapeColor.g * 2.6f, escapeColor.b * 2.6f, 1f));
            }
            if (captureMat == null)
            {
                captureMat = new Material(Shader.Find("BlackHole/PhotonTrail")) { hideFlags = HideFlags.HideAndDontSave };
                captureMat.SetColor("_Tint", new Color(captureColor.r * 2.6f, captureColor.g * 2.6f, captureColor.b * 2.6f, 1f));
                captureMat.SetFloat("_PulseSpeed", 7f);
            }
            if (headMat == null)
            {
                headMat = new Material(Shader.Find("BlackHole/JetParticle")) { hideFlags = HideFlags.HideAndDontSave };
                headMat.SetTexture("_BaseMap", HeadTexture());
            }
        }

        Texture2D HeadTexture()
        {
            if (headTex != null) return headTex;
            const int dim = 64;
            headTex = new Texture2D(dim, dim, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float c = (dim - 1) * 0.5f;
            for (int y = 0; y < dim; y++)
            for (int x = 0; x < dim; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Exp(-d * d * 5f) + 0.35f * Mathf.Exp(-d * d * 1.4f);
                headTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
            headTex.Apply();
            return headTex;
        }

    }
}
