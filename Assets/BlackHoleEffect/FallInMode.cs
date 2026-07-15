using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// First-person fall into the event horizon (F3): the camera free-falls
    /// toward the hole while the shadow swells to fill the sky. Looking
    /// forward everything ends black — but the epilogue turns the view
    /// around: the outside universe stays visible, gathered into an
    /// ever-shrinking, blueshifting circle (aberration for an infalling
    /// observer). A stop button / Esc aborts at any time. Play mode only.
    /// </summary>
    public class FallInMode : MonoBehaviour
    {
        public Transform hole;
        public DesktopControls controls;
        public CinematicOrbit orbit;
        public float fallDuration = 14f;

        public bool IsFalling { get; private set; }

        // --- MR ride mode ----------------------------------------------------
        // In XR the headset owns the camera pose, so the fall moves the rig and
        // leaves the looking to the passenger. Rotation is NOT ported: LookAt and
        // the tilt schedule would fight head tracking, and winning that fight is
        // worse than losing it. The stages still frame the hole because the rig
        // travels straight down the radial line — and the shadow grows to fill
        // the sky either way.
        //
        // The trip is gentler than it sounds: 24 Rs is only 2.9 m at the room-
        // scale Rs of 12 cm, so the whole descent averages ~0.2 m/s. Schwarzschild
        // is scale-free, so the apparent size matches the desktop exactly.
        Unity.XR.CoreUtils.XROrigin xrOrigin;
        MRSpaceWindow spaceWindow;
        Vector3 savedRigPos;
        Quaternion savedRigRot;

        bool XRRide => xrOrigin != null;

        Text caption;
        RectTransform captionPanel;
        Button stopButton;
        Coroutine routine;
        Vector3 savedPos;
        Quaternion savedRot;
        Image skyImage;
        Transform skyBillboard;   // MR only: the circle's world anchor behind the viewer
        Image blackOverlay;
        Texture2D skyTex;
        float savedStarDensity, savedNebula;
        Material sky;                     // RenderSettings.skybox — kept in step with the quad's stars
        float savedSkyStar, savedSkyNebula;
        BlackHoleController holeController;
        float spiralAz;   // cumulative background drift, deg (matches the ambient orbit)
        float driftRate;  // deg/s — set from the ambient CinematicOrbit so the fall is unified with it
        Vector3 fallDir;  // live outward radial — where the outside universe still is

        public void Begin()
        {
            if (!Application.isPlaying || IsFalling || hole == null) return;
            xrOrigin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            spaceWindow = FindAnyObjectByType<MRSpaceWindow>();
            routine = StartCoroutine(Run());
        }

        /// <summary>
        /// Puts the viewer at a world point. On desktop that means the camera,
        /// aimed at the hole with the authored tilt. In XR it means the rig:
        /// MoveCameraToWorldLocation compensates for where the head currently
        /// sits inside it, and the aim is the passenger's business.
        /// </summary>
        void PlaceViewer(Vector3 worldPos, float tilt = 0f)
        {
            if (XRRide)
            {
                xrOrigin.MoveCameraToWorldLocation(worldPos);
                return;
            }
            transform.position = worldPos;
            transform.LookAt(hole.position);
            if (tilt != 0f) transform.Rotate(-tilt, 0f, 0f);
        }

        void Update()
        {
            if (!IsFalling) return;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Abort();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Abort();
#endif
        }

        /// <summary>Stop button / Esc: kill the coroutine and put the camera
        /// (and every overlay) back exactly where the fall started.</summary>
        public void Abort()
        {
            if (!IsFalling) return;
            if (routine != null) StopCoroutine(routine);
            NarrationManager.Instance.Stop();
            DestroySkyDisk();
            RestoreViewer();
            if (spaceWindow != null) spaceWindow.CloseNow();
            Finish();
        }

        /// <summary>Puts the viewer back where the fall found them — the rig in
        /// XR (the camera pose there is the headset's, not ours to restore).</summary>
        void RestoreViewer()
        {
            if (XRRide)
            {
                xrOrigin.transform.SetPositionAndRotation(savedRigPos, savedRigRot);
                return;
            }
            transform.position = savedPos;
            transform.rotation = savedRot;
        }

        void Finish()
        {
            HideCaption();
            ShowStop(false);
            // Never leave the screen black — a normal end or an Esc-abort both
            // route through here, including an abort mid-plunge.
            if (blackOverlay != null)
            {
                var bc = blackOverlay.color; bc.a = 0f; blackOverlay.color = bc;
                blackOverlay.gameObject.SetActive(false);
            }
            if (holeController != null)
            {
                holeController.starDensity = savedStarDensity;
                holeController.nebulaHaze = savedNebula;
                holeController.Apply();
            }
            // The skybox is a shared asset — hand its density back whatever the
            // ending. (Safe after MRSpaceWindow unassigns it: we hold the material.)
            RestoreSky();
            SetFrameFollowsViewer(false); // back onto the hole — both endings pass here
            if (controls != null) { controls.SetImmersive(false); controls.suspendCamera = false; }
            if (orbit != null) orbit.enabled = true; // the drift never stays off
            IsFalling = false;
        }

        /// <summary>MR: park the shared UI frame in front of the viewer for the
        /// ride, or hand it back to the hole afterwards. No-op on desktop.</summary>
        void SetFrameFollowsViewer(bool on)
        {
            var frame = BlackHoleUI.WorldRig;
            if (frame != null) frame.followViewer = on;
        }

        /// <summary>Moves the camera from rFrom to rTo (in Rs) over dur
        /// seconds with the given easing exponent, keeping the live r
        /// readout in the caption via the formatter.</summary>
        /// <summary>Plays fall_{i} in the active language; returns clip length.</summary>
        static float Narrate(int i) => NarrationManager.Instance.Play("fall_" + i);

        /// <summary>Unspoken gray live-distance line under the caption.</summary>
        static string RLine(float r) => "\n<color=#9AA3B5>r = " + r.ToString("0.0") + " Rs</color>";

        /// <summary>tiltFrom/tiltTo: upward camera pitch in degrees — near the
        /// horizon the only remaining light is overhead, so looking straight
        /// at the hole would show nothing but black for many seconds.
        /// onFrame: per-frame hook (r) for the black overlay fade. The
        /// background drift is a constant-rate azimuthal sweep (spiralAz,
        /// driftRate) shared across every stage so it matches the ambient orbit
        /// exactly — the direction-sampled sky only moves when the view
        /// rotates, so this keeps it alive without ever feeling faster than the
        /// exploration/merger views.</summary>
        IEnumerator Glide(float rFrom, float rTo, float dur, float ease,
                          Vector3 dir, float rs, System.Func<float, string> captionFor,
                          float tiltFrom = 0f, float tiltTo = 0f, System.Action<float> onFrame = null)
        {
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / dur);
                float r = Mathf.Lerp(rFrom, rTo, Mathf.Pow(u, ease));
                // Constant-rate drift about world-up, accumulated across all
                // stages. AngleAxis preserves |dir|, so the camera stays at
                // radius r*rs and the quad/leak geometry is untouched; applied
                // BEFORE LookAt so the hole stays centered.
                spiralAz += driftRate * Time.deltaTime;
                Vector3 dirNow = Quaternion.AngleAxis(spiralAz, Vector3.up) * dir;
                float tilt = Mathf.Lerp(tiltFrom, tiltTo, Mathf.SmoothStep(0f, 1f, u));
                tilt = Mathf.Min(tilt, SafeTiltCap(r)); // never let the frame run off the quad rim
                PlaceViewer(hole.position + dirNow * (r * rs), tilt);
                fallDir = dirNow;
                UpdateStarDensity(r);
                if (captionFor != null) Caption(captionFor(r));
                onFrame?.Invoke(r);
                yield return null;
            }
        }

        /// <summary>Star/nebula density ramps up only with DEPTH, not from the
        /// first frame: at the start (r ≥ 8) it matches the exploration view;
        /// past r ≈ 3 the shadow fills the sky and the only light left is the
        /// star cone behind, so the boost fades in to keep that from reading as
        /// a black screen.</summary>
        void UpdateStarDensity(float r)
        {
            if (holeController == null) return;
            float k = Mathf.InverseLerp(8f, 2f, r); // 0 at r≥8 (exploration), 1 at r≤2
            holeController.starDensity = Mathf.Lerp(savedStarDensity, 0.55f, k);
            holeController.nebulaHaze = Mathf.Lerp(savedNebula, 0.35f, k);
            holeController.Apply();
            // The skybox behind the quad carries its own star density, so boosting
            // only the quad makes its rectangle show up as a denser patch. The
            // desktop never caught this — at FOV 32° locked on the hole, the quad
            // covers the whole frame past r≈8. In MR the view is wide and the head
            // is free, so the rim is right there. Same fix as the merger.
            if (sky != null && sky.HasProperty("_StarDensity"))
            {
                sky.SetFloat("_StarDensity", holeController.starDensity);
                sky.SetFloat("_NebulaIntensity", holeController.nebulaHaze);
            }
        }

        void RestoreSky()
        {
            if (sky != null && sky.HasProperty("_StarDensity"))
            {
                sky.SetFloat("_StarDensity", savedSkyStar);
                sky.SetFloat("_NebulaIntensity", savedSkyNebula);
            }
        }

        /// <summary>Leaving play mode mid-fall kills the coroutine before Finish
        /// can run, and the boost would stay baked into the shared skybox asset —
        /// which has no [ExecuteAlways] owner to rewrite it. See the same guard on
        /// BinaryMergerCinematic.</summary>
        void OnDisable()
        {
            if (!IsFalling) return;
            RestoreSky();
        }

        /// <summary>Highest safe upward pitch (deg) at radius r: past the
        /// billboard quad's angular half-width atan(_ViewExtent/r), minus the
        /// half-FOV and a 4° margin, the frame runs off the quad and shows
        /// raw un-lensed skybox. Adapts to the live camera FOV.</summary>
        float SafeTiltCap(float r)
        {
            const float viewExtent = 15f; // matches BlackHoleRaymarch.mat _ViewExtent
            float halfFovV = 16f;
            var cam = GetComponent<Camera>();
            if (cam != null && cam.fieldOfView > 0f) halfFovV = cam.fieldOfView * 0.5f;
            float thetaMax = Mathf.Atan2(viewExtent, Mathf.Max(r, 0.01f)) * Mathf.Rad2Deg;
            return Mathf.Max(0f, thetaMax - halfFovV - 4f);
        }

        /// <summary>Establishing swoop: eases the camera from its live pose to
        /// the canonical fall pose (dirTo · rToRs) along an arc around the hole,
        /// so an off-nominal start angle never snaps on the first descent frame.
        /// LookAt(hole) at both ends keeps rotation continuous into Glide.</summary>
        IEnumerator Settle(Vector3 fromPos, Vector3 dirTo, float rToRs, float rs, float dur)
        {
            Vector3 offFrom = fromPos - hole.position;
            float rFrom = Mathf.Max(offFrom.magnitude, 1e-4f);
            Vector3 nFrom = offFrom / rFrom;
            float rTo = rToRs * rs;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                PlaceViewer(hole.position + Vector3.Slerp(nFrom, dirTo, u) * Mathf.Lerp(rFrom, rTo, u));
                yield return null;
            }
            PlaceViewer(hole.position + dirTo * rTo);
        }

        IEnumerator Run()
        {
            IsFalling = true;
            if (controls != null) { controls.SetImmersive(true); controls.suspendCamera = true; }
            if (orbit != null) orbit.enabled = false;
            ShowStop(true);

            // Save the exploration sky density; the boost is NOT applied up
            // front (that made the start look denser than the exploration
            // view) — UpdateStarDensity ramps it in with depth instead.
            holeController = hole.GetComponent<BlackHoleController>();
            if (holeController != null)
            {
                savedStarDensity = holeController.starDensity;
                savedNebula = holeController.nebulaHaze;
            }

            savedPos = transform.position;
            savedRot = transform.rotation;
            if (XRRide)
            {
                savedRigPos = xrOrigin.transform.position;
                savedRigRot = xrOrigin.transform.rotation;
            }
            float rs = hole.lossyScale.x;

            // Nothing to fall through in MR: the shader draws no starfield there
            // and cannot sample passthrough, so borrow the room for the ride.
            if (spaceWindow != null) yield return spaceWindow.Open(1.2f);
            // Read the sky AFTER that swap — in MR the starfield only becomes the
            // skybox once the room is gone.
            sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_StarDensity"))
            {
                savedSkyStar = sky.GetFloat("_StarDensity");
                savedSkyNebula = sky.GetFloat("_NebulaIntensity");
            }
            // The UI frame normally hangs on the hole — which we are about to fly
            // into. Let it ride in front of the passenger for the trip instead.
            SetFrameFollowsViewer(true);

            // Background drift is unified with the ambient orbit the fall
            // interrupts — same rate, so it never feels faster than the
            // exploration/merger views. Accumulates from 0 across every stage.
            driftRate = orbit != null ? orbit.orbitSpeed : 0.8f;
            spiralAz = 0f;

            // FIXED start pose so every fall opens from the same angle and
            // distance, no matter where the user had orbited/zoomed to. Only
            // the approach AZIMUTH follows their current heading — on the
            // axisymmetric disk that looks identical, and it avoids a jarring
            // sideways fly-around. Elevation and radius are constants: a steady
            // 15° above the disk plane at 24 Rs. Settle (below) swoops smoothly
            // from wherever they were to this pose.
            const float startElevDeg = 15f;
            const float startR = 24f;
            Vector3 raw = savedPos - hole.position;
            float r0Raw = raw.magnitude / rs;
            Vector3 rawDir = raw.normalized;
            Vector3 flat = new Vector3(raw.x, 0f, raw.z);
            float horiz = flat.magnitude;
            if (horiz < 1e-4f) flat = Vector3.forward; else flat /= horiz;
            float elev = startElevDeg * Mathf.Deg2Rad;
            Vector3 dir = (flat * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev)).normalized;
            float r0 = startR;
            float len;

            // --- Framing: say what this experience is about before moving.
            // Captions are the narration transcript (fall_0..fall_7); the
            // live r readout is an unspoken gray line, like the tour hints.
            len = Narrate(0);
            Caption(Loc.T(
                "만약 블랙홀 안으로 떨어진다면, 무엇을 보게 될까요?\n지금부터 카메라가 자유낙하를 시작합니다.",
                "What would you see if you fell into a black hole?\nThe camera now begins its free fall.",
                "もしブラックホールに落ちたら、何が見えるのでしょうか。\nこれからカメラが自由落下を始めます。",
                "如果掉进黑洞，你会看到什么？\n现在，镜头开始自由落体。"));
            // Ease into the canonical pose during the framing beat — but only
            // when the pose actually needs to change (gate on the real angular
            // + radial delta, NOT on raw elevation: a below-plane start reflects
            // to above-plane, which changes dir even when |elevRaw| is small,
            // and would otherwise snap on the first descent frame). The common
            // un-orbited start yields reframe=false, settleDur=0 → identical to
            // before. The wait is shortened by settleDur so narration stays in
            // sync (cap 2.2s < the 3.4s floor keeps the remainder positive).
            bool reframe = Vector3.Angle(rawDir, dir) > 1.5f || Mathf.Abs(r0Raw - r0) > 0.5f;
            float settleDur = reframe
                ? Mathf.Clamp(0.9f + 0.02f * (Mathf.Abs(r0Raw - r0) + Vector3.Angle(rawDir, dir)), 0f, 2.2f)
                : 0f;
            if (settleDur > 0f) yield return Settle(savedPos, dir, r0, rs, settleDur);
            // Drift-hold during the framing beat: r held, no tilt, but the
            // shared constant-rate drift keeps ticking so the fall never opens
            // on a frozen frame — motion begins on the first second, at the
            // exact rate of the ambient orbit it replaces.
            float holdDur = Mathf.Max(3.4f, len + 0.5f) - settleDur;
            yield return Glide(r0, r0, holdDur, 1f, dir, rs, null);

            // --- Staged descent: each stage owns one narrated line, and the
            // stage lasts at least as long as its voice clip.
            float mid = Mathf.Clamp(r0 * 0.55f, 4.6f, 8f);
            len = Narrate(1);
            yield return Glide(r0, mid, Mathf.Max(4.5f, len + 0.3f), 1.5f, dir, rs, r => Loc.T(
                "자유낙하가 시작됩니다. 아직은 평범한 우주입니다.",
                "Free fall begins. Space still looks ordinary out here.",
                "自由落下が始まります。まだ、ふつうの宇宙です。",
                "自由落体开始了。这里还是平常的宇宙。") + RLine(r));

            len = Narrate(2);
            yield return Glide(mid, 4.5f, Mathf.Max(3.5f, len + 0.3f), 1.15f, dir, rs, r => Loc.T(
                "원반이 하늘을 뒤덮기 시작합니다. 밖의 시간은 점점 빨라 보입니다.",
                "The disk begins to swallow the sky. Time outside appears to run faster and faster.",
                "円盤が空を覆いはじめます。外の時間はどんどん速く見えます。",
                "吸积盘开始遮蔽天空。外面的时间看起来越来越快。") + RLine(r));

            // From here the shadow outgrows the field of view fast, so the
            // camera tilts up to keep the shrinking bright rim on screen —
            // exactly what the caption describes.
            len = Narrate(3);
            yield return Glide(4.5f, 1.9f, Mathf.Max(3.8f, len + 0.3f), 1f, dir, rs, r => Loc.T(
                "조석력이 몸을 잡아 늘입니다. 그림자가 시야의 절반을 삼켰습니다.",
                "Tidal forces stretch your body. The shadow has swallowed half your view.",
                "潮汐力が体を引き伸ばします。影が視界の半分を呑み込みました。",
                "潮汐力开始拉伸你的身体。阴影已吞没一半视野。") + RLine(r), 0f, 35f);

            // Below r ≈ 3 the only light left is the (boosted) starfield in
            // the shrinking cone behind us — keep sweeping backward so stars
            // stay in frame instead of staring into pure shadow.
            len = Narrate(4);
            yield return Glide(1.9f, 1.05f, Mathf.Max(3.2f, len + 0.3f), 0.9f, dir, rs, r => Loc.T(
                "마지막 빛의 고리가 머리 위로 좁혀듭니다.",
                "The last ring of light closes in overhead.",
                "最後の光のリングが頭上で狭まっていきます。",
                "最后的光环在头顶收拢。") + RLine(r), 35f, 62f);
            yield return new WaitForSeconds(0.6f); // brief hold at the brink

            len = Narrate(5);
            Caption(Loc.T(
                "사건의 지평선을 통과했습니다.\n바깥 우주로는 어떤 신호도 보낼 수 없습니다.",
                "We have crossed the event horizon.\nNo signal can ever reach the outside universe again.",
                "事象の地平面を通過しました。\n外の宇宙へは、もうどんな信号も送れません。",
                "我们已越过事件视界。\n再也无法向外面的宇宙发出任何信号。"));
            // Swing back toward the hole while plunging: the ring band
            // collapses and the view goes genuinely, completely black at the
            // exact moment the horizon-crossed caption lands. Two guarantees
            // of true black here: (1) tilt is capped by SafeTiltCap so the
            // frame never runs off the raymarch quad's rim into raw un-lensed
            // skybox (which would be exactly wrong inside the horizon), and
            // (2) a full-screen black overlay fades in over r∈[1.0,0.65] —
            // physically, the forward view inside the horizon IS black.
            EnsureBlackOverlay();
            yield return Glide(1.05f, 0.35f, 1.6f, 1f, dir, rs, null, 62f, 45f,
                onFrame: r => SetBlack(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.0f, 0.65f, r))));
            // Hold the darkness — this IS the physics — then the looking-back
            // window rises small and dim while the horizon line finishes.
            yield return new WaitForSeconds(2.2f);
            EnsureSkyImage();
            skyImage.gameObject.SetActive(true);
            if (skyBillboard != null) skyBillboard.gameObject.SetActive(true); // a repeat fall re-arms it
            PlaceSkyBillboard();
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float k = t / 1.1f;
                float size = Mathf.Lerp(24f, 300f, Mathf.SmoothStep(0f, 1f, k));
                skyImage.rectTransform.sizeDelta = new Vector2(size, size);
                skyImage.color = new Color(1f, 0.98f, 0.92f, 0.9f * k);
                PlaceSkyBillboard();
                yield return null;
            }
            yield return new WaitForSeconds(Mathf.Max(0f, len - 1.6f - 2.2f - 1.1f));

            // --- Epilogue: turn around. The view "forward" (toward the
            // singularity) really is black — but looking back, the outside
            // universe never disappears: aberration gathers the whole sky
            // into a shrinking, blueshifting circle of light.
            len = Narrate(6);
            Caption(Loc.T(
                "뒤를 돌아보면, 바깥 우주는 사라지지 않습니다.\n온 하늘이 점점 좁아지는 푸른 빛의 원 안으로 모여듭니다.",
                "Looking back, the outside universe never vanishes.\nThe whole sky gathers into a shrinking, blueshifting circle of light.",
                "後ろを振り返ると、外の宇宙は消えていません。\n空全体が、狭まっていく青い光の円の中に集まって見えます。",
                "回头看，外面的宇宙并没有消失。\n整个天空聚成一个越来越小、越来越蓝的光圈。"));
            float lookBack = Mathf.Max(7f, len + 0.4f);
            for (float t = 0f; t < lookBack; t += Time.deltaTime)
            {
                float k = t / lookBack;
                float size = Mathf.Lerp(300f, 56f, Mathf.Pow(k, 1.2f));
                skyImage.rectTransform.sizeDelta = new Vector2(size, size);
                Color cs = Color.Lerp(new Color(1f, 0.98f, 0.92f), new Color(0.45f, 0.65f, 1f), k);
                cs.a = 0.9f;
                skyImage.color = cs;
                PlaceSkyBillboard();
                yield return null;
            }

            // The last point of light dies while physics has its final word —
            // the closing line is never spoken over a totally empty screen.
            len = Narrate(7);
            Caption(Loc.T(
                "이 안에서는 모든 미래의 경로가 중심 특이점을 향합니다.\n여기까지가 물리학이 말할 수 있는 전부입니다.",
                "In here, every future path leads to the central singularity.\nThis is as far as physics can speak.",
                "この中では、あらゆる未来の経路が中心の特異点へ向かいます。\nここから先は、物理学が語れる限界です。",
                "在这里，所有未来的路径都通向中心奇点。\n物理学能讲述的，到此为止。"));
            float die = Mathf.Max(4.5f, len + 0.5f);
            for (float t = 0f; t < die; t += Time.deltaTime)
            {
                float k = t / die;
                float size = Mathf.Lerp(56f, 8f, k);
                skyImage.rectTransform.sizeDelta = new Vector2(size, size);
                Color c = Color.Lerp(new Color(0.45f, 0.65f, 1f), new Color(0.25f, 0.35f, 0.85f), k);
                c.a = 0.9f * Mathf.Pow(1f - k, 1.3f);
                skyImage.color = c;
                PlaceSkyBillboard();
                yield return null;
            }
            skyImage.gameObject.SetActive(false);

            Caption(Loc.T(
                "체험이 끝났습니다 — 원래 위치로 돌아갑니다.",
                "The experience is over — returning to where we started.",
                "体験はこれで終わりです — 元の位置に戻ります。",
                "体验结束——回到原来的位置。"));
            yield return new WaitForSeconds(1.8f);

            RestoreViewer();
            // Give the room back only after the viewer is home, so the fade
            // covers the jump instead of landing on top of it.
            if (spaceWindow != null) yield return spaceWindow.Close(1.2f);
            Finish();
        }

        // ---------------- inside-horizon black overlay ----------------
        /// <summary>Full-screen black Image on the UI canvas (which draws over
        /// the raymarch quad). Guarantees a genuinely black forward view at and
        /// after the horizon crossing regardless of quad-rim geometry.</summary>
        void EnsureBlackOverlay()
        {
            if (blackOverlay != null) return;
            // Sorting order 90 puts it under the UI frame (100) but still over
            // the raymarch quad, which is transparent-queued and writes no depth
            // — so draw order here is decided by sorting, not by distance, and
            // the captions and stop button survive the blackout.
            blackOverlay = BlackHoleUI.MakeFullViewOverlay(GetComponent<Camera>(), "FallIn Black", 90);
            if (blackOverlay == null) return;
            blackOverlay.color = new Color(0f, 0f, 0f, 0f);
            // Desktop shares one canvas, where order is sibling order: to the back.
            if (!BlackHoleUI.WorldSpace) blackOverlay.transform.SetSiblingIndex(0);
        }

        void SetBlack(float a)
        {
            if (blackOverlay == null) EnsureBlackOverlay();
            blackOverlay.gameObject.SetActive(true);
            var c = blackOverlay.color; c.a = Mathf.Clamp01(a); blackOverlay.color = c;
        }

        // ---------------- looking-back sky circle ----------------
        void EnsureSkyImage()
        {
            if (skyImage != null) return;
            var go = new GameObject("FallIn Sky") { hideFlags = HideFlags.DontSave };

            if (BlackHoleUI.WorldSpace)
            {
                // MR earns the caption its literal meaning: the outside universe
                // is left in the direction we fell FROM, so the circle sits out
                // there in the world and the passenger has to turn around to find
                // it. Sorts over the blackout (90); when it is behind the viewer
                // it is simply outside the frustum, so nothing leaks forward.
                var cam = GetComponent<Camera>();
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = cam;
                canvas.sortingOrder = 100;
                var crt = (RectTransform)go.transform;
                crt.sizeDelta = new Vector2(1000f, 1000f);
                crt.localScale = Vector3.one * 0.001f; // 300 "px" ≈ 0.3 m at 1.5 m ≈ the desktop's 9°
                skyBillboard = go.transform;

                var child = new GameObject("Disk") { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(go.transform, false);
                var rt2 = child.AddComponent<RectTransform>();
                rt2.anchorMin = rt2.anchorMax = rt2.pivot = new Vector2(0.5f, 0.5f);
                rt2.anchoredPosition = Vector2.zero;
                skyImage = child.AddComponent<Image>();
            }
            else
            {
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                go.transform.SetParent(canvas.transform, false);
                // Draw order (back → front): black overlay, looking-back circle,
                // caption/stop (created earlier, higher indices). The circle must
                // sit ABOVE the opaque black overlay or it would be hidden.
                if (blackOverlay != null) { blackOverlay.transform.SetSiblingIndex(0); go.transform.SetSiblingIndex(1); }
                else go.transform.SetSiblingIndex(0);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                skyImage = go.AddComponent<Image>();
            }

            skyImage.raycastTarget = false;
            var tex = SkyTexture();
            skyImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>MR: hold the circle out along the outward radial (the way we
        /// came) and keep it facing the viewer.</summary>
        void PlaceSkyBillboard()
        {
            if (skyBillboard == null) return;
            var cam = GetComponent<Camera>();
            if (cam == null || fallDir == Vector3.zero) return;
            skyBillboard.position = cam.transform.position + fallDir * 1.5f;
            skyBillboard.rotation = Quaternion.LookRotation(skyBillboard.position - cam.transform.position, Vector3.up);
        }

        void DestroySkyDisk()
        {
            if (skyImage != null) skyImage.gameObject.SetActive(false);
            // MR keeps the circle on its own world anchor, one level up.
            if (skyBillboard != null) skyBillboard.gameObject.SetActive(false);
        }

        /// <summary>Soft "compressed starry sky" disk: a bright core with a
        /// speckle of stars, dying out well inside the quad.</summary>
        Texture2D SkyTexture()
        {
            if (skyTex != null) return skyTex;
            const int dim = 128;
            skyTex = new Texture2D(dim, dim, TextureFormat.RGBA32, false);
            float c = (dim - 1) * 0.5f;
            var rng = new System.Random(7);
            for (int y = 0; y < dim; y++)
            for (int x = 0; x < dim; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float core = Mathf.Exp(-d * d * 3.2f);
                float rim = Mathf.Exp(-Mathf.Abs(d - 0.72f) * 14f) * 0.5f; // bright compressed rim
                float star = (float)rng.NextDouble() < 0.02f && d < 0.75f ? 0.65f : 0f;
                float a = Mathf.Clamp01((core + rim + star) * Mathf.Clamp01((0.85f - d) * 8f));
                skyTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            skyTex.Apply();
            return skyTex;
        }

        // ---------------- UI ----------------
        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "FallIn Stop", Abort);
            }
            stopButton.gameObject.SetActive(on);
            if (on)
                stopButton.GetComponentInChildren<Text>().text =
                    Loc.T("중단 ■", "Stop ■", "中止 ■", "停止 ■");
        }

        void Caption(string text)
        {
            if (caption == null)
            {
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "FallIn Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(860f, 100f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 21, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 84f));
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            captionPanel.gameObject.SetActive(true);
            caption.text = text;
        }

        void HideCaption()
        {
            if (captionPanel != null) captionPanel.gameObject.SetActive(false);
        }
    }
}
