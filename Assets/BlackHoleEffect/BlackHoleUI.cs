using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// Shared UI factory for the showcase overlays: one screen-space-camera
    /// canvas (CanvasScaler handles any aspect ratio), procedurally generated
    /// rounded-rect / circle sprites, and a dark + gold theme that matches the
    /// black hole. Everything is DontSave — nothing pollutes the scene file.
    ///
    /// In MR the same canvas switches to world space (see <see cref="WorldSpace"/>).
    /// The layout needs no changes: it is authored against a 1920x1080 frame with
    /// the hole centred and panels hugging the edges, so hanging that frame in the
    /// room around the real hole leaves the middle transparent for passthrough and
    /// arranges the panels around the object.
    /// </summary>
    public static class BlackHoleUI
    {
        /// <summary>
        /// Build UI as a world-space canvas (MR) instead of a screen overlay.
        ///
        /// Resolved on first use rather than assigned at startup: two UI owners
        /// (the physics panel and the observation comparison) build from OnEnable,
        /// which interleaves with everyone else's Awake, so no script can reliably
        /// set a flag "first". An XR rig in the scene is the honest signal, and by
        /// the time any UI is built the scene is fully loaded.
        /// </summary>
        public static bool WorldSpace
        {
            get
            {
                if (worldSpaceOverride.HasValue) return worldSpaceOverride.Value;
                // Deliberately not cached: the editor swaps scenes without a domain
                // reload, so a result computed in the desktop scene would follow us
                // into the MR one. Only canvas and button construction read this,
                // so the scan cost never lands in a frame loop.
                return Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>() != null;
            }
            set => worldSpaceOverride = value;
        }

        static bool? worldSpaceOverride;

        /// <summary>
        /// Width of the 1920px-wide frame once hung in the room.
        ///
        /// Do not narrow this to pull the corner panels inward. It was tried at
        /// 2.2 m: corners came in from 42° to 36°, but the canvas scales uniformly,
        /// so every glyph shrank with it and the theory panel's fine print fell to
        /// 24 arcmin. Six degrees of head turn is not worth that — and the slant
        /// that actually hurt legibility is fixed by facing the panels instead
        /// (MRWorldCanvas.facePanels).
        ///
        /// At 2.6 m the smallest text measures ~33 arcmin. For scale: 14 px on a
        /// 1080p monitor at 60 cm is ~22 arcmin, so this is already half again
        /// larger than what the desktop build asks people to read.
        /// </summary>
        public static float WorldWidthMeters = 2.6f;

        /// <summary>
        /// How much bigger reading text is in MR than at a desk.
        ///
        /// The frame is 1920 px hung 2.6 m wide about 1.6 m away, so an authored
        /// pixel is 1.35 mm and a 20 px body line subtends about one degree.
        /// That is fine for Latin and not fine for the language this exhibit is
        /// written in: a Korean syllable block packs two or three strokes into
        /// the space a Latin letter uses, and at one degree they fill in and the
        /// text turns to grey texture. 1.4x puts a line near 1.4 deg, which is
        /// where the blocks come apart again.
        ///
        /// This is NOT WorldWidthMeters. Widening the frame would scale the text
        /// and the layout together, and the layout is the half that cannot move:
        /// x becomes an angle on MRWorldCanvas's cylinder and the widest button
        /// row already spends the 70 deg comfort budget. Height is free — y is
        /// not bent — so reading text grows downward into space nothing is
        /// competing for. Use <see cref="ReadingSize"/> for the font and
        /// <see cref="ReadingY"/> for every vertical measurement that stacks
        /// with it: card heights, row heights, and the offsets between them.
        /// Widths and x offsets stay authored.
        /// </summary>
        public const float MRReadingScale = 1.4f;

        /// <summary>Font size for reading text — enlarged in MR, authored size
        /// on desktop.</summary>
        public static int ReadingSize(int desktopSize) =>
            WorldSpace ? Mathf.RoundToInt(desktopSize * MRReadingScale) : desktopSize;

        /// <summary>A vertical measurement belonging to reading text — a card
        /// height, a text rect height, or the offset that stacks the next row
        /// below it. Grows with the font so the rows never close up.</summary>
        public static float ReadingY(float desktopY) =>
            WorldSpace ? desktopY * MRReadingScale : desktopY;

        /// <summary>
        /// The MR frame's rig — null on desktop, or before any UI is built.
        ///
        /// Ask here rather than FindAnyObjectByType: the canvas is DontSave so it
        /// never lands in the scene file, and DontSave objects are invisible to
        /// the Find APIs. We hold the reference anyway.
        /// </summary>
        public static MRWorldCanvas WorldRig => canvas != null ? canvas.GetComponent<MRWorldCanvas>() : null;

        // Statics survive "Enter Play Mode without domain reload".
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            worldSpaceOverride = null;
            canvas = null;
            bottomClaims.Clear();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

#if UNITY_EDITOR
        /// <summary>Tear the DontSave canvas down while play mode is still
        /// alive. Left to linger into edit mode, the next session's sweep
        /// destroys it under a FRESH domain — and an MR canvas's
        /// TrackedDeviceGraphicRaycaster then throws KeyNotFound from
        /// OnDisable (XRI's static registry no longer knows it). Unity logs
        /// that exception internally, so no try/catch at the sweep can
        /// silence it; destroying in-session is the only clean path. Player
        /// builds never reload the domain between scenes, so they are safe.</summary>
        static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode && canvas != null)
                Object.DestroyImmediate(canvas.gameObject);
        }
#endif

        // Theme. The two colours that carry text contrast are firmer in MR: a
        // panel there is composited over whatever the room happens to be, which
        // can be a bright window, and the desktop's 0.86 backing plus dim grey
        // secondary text has no margin for that.
        public static Color PanelBg => WorldSpace
            ? new Color(0.02f, 0.03f, 0.055f, 0.95f)
            : new Color(0.03f, 0.045f, 0.075f, 0.86f);
        public static readonly Color Accent = new Color(1f, 0.76f, 0.42f, 0.95f);
        public static readonly Color TextPrimary = new Color(0.91f, 0.93f, 0.96f, 1f);
        public static Color TextSecondary => WorldSpace
            ? new Color(0.78f, 0.82f, 0.88f, 1f)
            : new Color(0.62f, 0.66f, 0.74f, 1f);
        public static readonly Color TitleGold = new Color(1f, 0.8f, 0.5f, 1f);

        static Canvas canvas;
        static Sprite rounded;
        static Sprite circle;
        static Font font;

        public static Font UIFont
        {
            get
            {
                // Bundled Noto Sans KR first: WebGL has NO OS font fallback,
                // so every Korean/Japanese/Chinese glyph the exhibit draws
                // must come from a font we ship (OFL, see Fonts/LICENSE-OFL).
                if (font == null)
                    font = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
                // LegacyRuntime is a dynamic font with OS fallback (Korean
                // renders fine on desktop). CreateDynamicFontFromOSFont does
                // not rasterize reliably in edit mode, so we avoid it.
                //
                // Say so loudly: this fallback keeps the desktop player working
                // but silently empties every Korean, Japanese and Chinese label
                // in a web build, and a blank label reads as a layout bug rather
                // than a missing asset. Twice now that has cost a debugging
                // session, so make the cause audible instead of guessable.
                if (font == null)
                {
                    Debug.LogError("Resources/Fonts/NotoSansKR-Regular is missing — " +
                                   "falling back to LegacyRuntime. CJK text will be blank " +
                                   "on WebGL, which has no OS font fallback.");
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

        // ---- Bottom-edge layout budget -------------------------------------
        //
        // The bottom of the screen is contested: the control bar sits centred on
        // it, the exhibit switcher claims the right corner, and the annotation
        // labels hang wherever the physics puts them. All three were authored in
        // absolute reference pixels against different anchors, so whether they
        // collided was arithmetic nobody was checking — and the arithmetic was
        // always tight. On the 1920 desktop frame the bar's right edge cleared
        // the switcher's left edge by 20 px; on the phone reference (1280, so the
        // text stays legible) the same two want 1390 px and overlap by 300.
        //
        // Fix the class of bug, not the instance: whoever takes bottom space says
        // so, and whoever lays out afterwards asks instead of assuming. Rects are
        // in canvas reference units, origin bottom-left. Cleared with the canvas,
        // since every claimant is parented to it.
        static readonly List<Rect> bottomClaims = new List<Rect>();

        public static void ClaimBottom(Rect refRect) => bottomClaims.Add(refRect);

        /// <summary>Top edge (reference px above the bottom) of everything
        /// claimed that overlaps the horizontal span [xMin, xMax], or 0 when
        /// that span is free. Ask before placing anything along the bottom.</summary>
        public static float BottomClaimedAbove(float xMin, float xMax)
        {
            float top = 0f;
            foreach (var r in bottomClaims)
                if (r.xMax > xMin && r.xMin < xMax && r.yMax > top) top = r.yMax;
            return top;
        }

        /// <summary>Logical canvas size in reference units — the frame everything
        /// is laid out inside, and the only honest basis for a layout decision.</summary>
        public static Vector2 CanvasRefSize
        {
            get
            {
                // MR hangs an authored 1920x1080 frame in the room at a fixed
                // physical size; nothing about it is fitted to a screen.
                if (WorldSpace) return new Vector2(1920f, 1080f);

                // Reproduce the CanvasScaler's arithmetic rather than read the
                // canvas rect. Deliberately: all UI is built from Start, and at
                // that point the rect still reports raw pixels because the scaler
                // has not applied its factor yet. Trusting it made the control
                // bar lay itself out against a 735-wide frame that was really
                // 1280 — it wrapped into seven rows and ate the screen.
                //
                // Not the reference resolution either. That is not the frame: the
                // desktop web template letterboxes to the authored 1.6 aspect,
                // not 16:9, so the real frame is ~1821x1138 and every decision
                // taken against 1920x1080 is off by about a hundred pixels.
                Vector2 refRes = IsPhone ? new Vector2(1280f, 720f) : new Vector2(1920f, 1080f);
                float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
                // matchWidthOrHeight = 0.5 → the geometric mean of the two ratios.
                float scale = Mathf.Sqrt((sw / refRes.x) * (sh / refRes.y));
                return scale > 0.0001f ? new Vector2(sw / scale, sh / scale) : refRes;
            }
        }

        static int isPhoneCache = -1;

        /// <summary>Is this a phone-sized screen? One source of truth.
        ///
        /// On the web the WebGL template already made this call — it sized the
        /// canvas on it — so we take its answer verbatim, passed in as a launch
        /// argument. Asking Unity separately (Application.isMobilePlatform) let
        /// the two disagree: the template would fill the viewport for a phone
        /// while the canvas kept the 1920 desktop reference, which is exactly
        /// the unreadably-small UI the mobile work exists to fix. Native
        /// players have no template, so there Unity is the authority.</summary>
        public static bool IsPhone
        {
            get
            {
                if (isPhoneCache >= 0) return isPhoneCache == 1;
                bool phone;
#if UNITY_WEBGL && !UNITY_EDITOR
                phone = false;
                foreach (var arg in System.Environment.GetCommandLineArgs())
                    if (arg == "-mobile") { phone = true; break; }
#else
                phone = Application.isMobilePlatform;
#endif
                isPhoneCache = phone ? 1 : 0;
                return phone;
            }
        }

        public static Canvas EnsureCanvas(Camera cam)
        {
            if (cam == null) cam = Camera.main;
            if (canvas != null && canvas.worldCamera != null) return canvas;

            // DontSave canvases survive scene rebuilds and play-mode entry
            // while their camera does not — sweep every stale copy, otherwise
            // orphan UI (e.g. an uncontrollable help bar) stays on screen.
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
                if (c != null && c.name == "BlackHole UI Canvas" && c != canvas)
                    SweepStaleCanvas(c.gameObject);
            if (canvas != null) SweepStaleCanvas(canvas.gameObject);

            // Fresh canvas, so every bottom-edge claim died with the old one.
            bottomClaims.Clear();

            var go = new GameObject("BlackHole UI Canvas") { hideFlags = HideFlags.DontSave };
            canvas = go.AddComponent<Canvas>();
            canvas.worldCamera = cam != null ? cam : Camera.main;
            var refCam = canvas.worldCamera;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();

            if (WorldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                var crt = (RectTransform)go.transform;
                crt.sizeDelta = new Vector2(1920f, 1080f);
                crt.localScale = Vector3.one * (WorldWidthMeters / 1920f);
                // Constant pixel size: ScaleWithScreenSize is meaningless once the
                // canvas has a physical size. Text sharpness comes from the extra
                // pixels-per-unit instead — the panels sit within arm's reach.
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.dynamicPixelsPerUnit = 3f;
                var rig = go.AddComponent<MRWorldCanvas>();
                rig.viewer = refCam;
                rig.PlaceNow();
                return canvas;
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            // Just past the near plane: screen-space-camera UI is depth-tested
            // against opaque geometry, and during the fall-in the camera gets
            // closer than 1 unit to the raymarch quad — at planeDistance 1 the
            // quad would occlude every caption right when they matter most.
            canvas.planeDistance = refCam != null ? Mathf.Max(refCam.nearClipPlane * 1.5f, 0.15f) : 0.15f;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // One knob for the whole UI: the scaler multiplies every authored
            // size — text, buttons, panels, offsets — by the same factor, so
            // the phone fix is a smaller reference, never per-widget font
            // edits (those would grow the text out of its panels).
            //
            // 1280x720 makes everything 1.5x larger than the 1920 desktop
            // reference. On a landscape phone (canvas ~915 CSS px wide) that
            // puts body text near 13 CSS px and still leaves the widest card
            // (1120 ref px) inside the viewport. Any lower and the cards
            // overflow; any higher and the text is unreadable.
            scaler.referenceResolution = IsPhone ? new Vector2(1280f, 720f)
                                                 : new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Destroy a stale canvas without letting XRI take the sweep
        /// down: a canvas that carried a TrackedDeviceGraphicRaycaster in a
        /// previous session throws KeyNotFound from the raycaster's OnDisable
        /// (its static registry was reset by the domain reload and no longer
        /// knows the canvas). Strip the raycaster under a catch first.</summary>
        static void SweepStaleCanvas(GameObject go)
        {
            foreach (var rc in go.GetComponentsInChildren<TrackedDeviceGraphicRaycaster>(true))
            {
                try { Object.DestroyImmediate(rc); }
                catch (System.Exception) { /* registry desync — the component is gone regardless */ }
            }
            try { Object.DestroyImmediate(go); }
            catch (System.Exception) { }
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (rounded == null) rounded = BuildRounded(64, 14);
                return rounded;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (circle == null) circle = BuildCircle(64);
                return circle;
            }
        }

        public static RectTransform MakePanel(Transform parent, string name,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, bool accentLine = true)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = PanelBg;
            img.raycastTarget = false;

            if (accentLine)
            {
                var line = new GameObject("Accent") { hideFlags = HideFlags.DontSave };
                line.transform.SetParent(go.transform, false);
                var lrt = line.AddComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 1f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, 0f);
                lrt.sizeDelta = new Vector2(-28f, 2f);
                var limg = line.AddComponent<Image>();
                limg.color = Accent;
                limg.raycastTarget = false;
            }
            return rt;
        }

        public static Text MakeText(Transform parent, string name, int size, Color color,
            TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 rectSize,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = rectSize;
            var t = go.AddComponent<Text>();
            t.font = UIFont;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.25f;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Clickable rounded button (used by cinematic skip/stop).
        /// Lazily adds the GraphicRaycaster + EventSystem the rest of the UI
        /// never needed — everything else is display-only.</summary>
        public static Button MakeButton(Transform parent, string name, string label,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            EnsureInteraction();

            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.09f, 0.12f, 0.18f, 0.92f);
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.35f, 1.5f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            var t = MakeText(go.transform, "Label", 17, TitleGold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size.x - 10f, size.y - 6f), FontStyle.Bold);
            t.text = label;
            return btn;
        }

        /// <summary>◀ / ▶ / ✕ on a tour card's lower-right corner — the touch
        /// and mouse equivalent of the arrow keys, so web and mobile visitors
        /// can step a narrated tour without a keyboard. Same placement on every
        /// exhibit's card (the nebula tour set the pattern).</summary>
        public static void MakeTourNav(RectTransform card,
            UnityEngine.Events.UnityAction prev,
            UnityEngine.Events.UnityAction next,
            UnityEngine.Events.UnityAction end)
        {
            // Touch-sized (48 tall clears the 44 px minimum once the canvas
            // scaler shrinks it on a phone) and glyphs the bundled pan-CJK
            // font actually has: ✕ (U+2715, Dingbats) is NOT in Noto Sans KR
            // and rendered as an empty button — × (U+00D7) is.
            var br = new Vector2(1f, 0f);
            MakeButton(card, "TourPrev", "◀", br, br, new Vector2(-260f, 12f), new Vector2(76f, 48f), prev);
            MakeButton(card, "TourNext", "▶", br, br, new Vector2(-176f, 12f), new Vector2(76f, 48f), next);
            MakeButton(card, "TourEnd", "×", br, br, new Vector2(-92f, 12f), new Vector2(76f, 48f), end);
        }

        /// <summary>
        /// The stop/skip control every cinematic puts on screen — the way out of
        /// a narrated experience that has taken over the view.
        ///
        /// Desktop keeps it as a corner chip, where the mouse reaches it instantly.
        /// MR cannot: in the corner of the world frame it measured 1.5° tall and
        /// 42° off-axis — under the ~2° a hand ray can reliably hit, and behind a
        /// head turn. That is the control a passenger wants when the fall is making
        /// them queasy, so in MR it is larger and sits along the bottom centre.
        /// </summary>
        public static Button MakeCinematicButton(Camera cam, string name,
            UnityEngine.Events.UnityAction onClick)
        {
            var canvas = EnsureCanvas(cam);
            if (WorldSpace)
                return MakeButton(canvas.transform, name, "",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 26f), new Vector2(300f, 96f), onClick);
            return MakeButton(canvas.transform, name, "",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-26f, -26f), new Vector2(170f, 44f), onClick);
        }

        static void EnsureInteraction()
        {
            // Hand/controller rays are tracked devices, not a mouse: they need
            // XRI's raycaster + input module. A plain GraphicRaycaster with
            // InputSystemUIInputModule leaves every MR button dead.
            if (canvas != null)
            {
                if (WorldSpace)
                {
                    if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                        canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                }
                else if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            // Exactly ONE EventSystem must survive this. Several piled up will
            // fight over the input pipeline and every button and hotkey goes dead
            // — we have shipped that bug once already (ten of them, from DontSave
            // copies leaking across play sessions while EventSystem.current reset
            // to null on each domain reload). So: never trust a count, always sweep.
            //
            // Ours is not the only candidate. The XR rig brings its own EventSystem
            // in DontDestroyOnLoad, and it is already the right one — adopt it
            // rather than adding a rival next to it.
            UnityEngine.EventSystems.EventSystem live = null;
            var mine = new System.Collections.Generic.List<UnityEngine.EventSystems.EventSystem>();
            foreach (var e in Resources.FindObjectsOfTypeAll<UnityEngine.EventSystems.EventSystem>())
            {
                if (e == null) continue;
                bool isOurs = e.gameObject.name == "BlackHole EventSystem";
                // An invalid scene means a prefab asset, or a DontSave orphan left
                // behind by an earlier play session. Ours get swept; assets ignored.
                if (!e.gameObject.scene.IsValid())
                {
                    if (isOurs) Object.DestroyImmediate(e.gameObject);
                    continue;
                }
                if (isOurs) mine.Add(e);
                else if (live == null && HasMatchingModule(e)) live = e;
                else if (!WorldSpace && e.GetComponent<XRUIInputModule>() != null)
                {
                    // The XR rig parks its EventSystem in DontDestroyOnLoad, so
                    // returning from an MR scene to a desktop one (the title
                    // screen) strands it here with the wrong module — a rival
                    // that spams "2 event systems" and can steal clicks. With
                    // no XR origin in the scene it is provably residue; strip
                    // the components, leave the foreign GameObject shell alone.
                    Object.DestroyImmediate(e.GetComponent<UnityEngine.EventSystems.BaseInputModule>());
                    Object.DestroyImmediate(e);
                }
            }

            if (live != null)
            {
                // Someone else's EventSystem already drives this mode: stand down.
                foreach (var e in mine) Object.DestroyImmediate(e.gameObject);
                return;
            }

            foreach (var e in mine)
            {
                // A survivor built for the other input mode (the desktop scene was
                // opened earlier this editor session) carries the wrong module, and
                // would silently kill every button. Treat it as stale.
                if (live == null && HasMatchingModule(e)) live = e;
                else Object.DestroyImmediate(e.gameObject);
            }
            if (live != null) return;

            var es = new GameObject("BlackHole EventSystem") { hideFlags = HideFlags.DontSave };
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            if (WorldSpace)
            {
                es.AddComponent<XRUIInputModule>();
                return;
            }
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        static bool HasMatchingModule(UnityEngine.EventSystems.EventSystem es)
        {
            bool xr = es.GetComponent<XRUIInputModule>() != null;
            return WorldSpace == xr;
        }

        /// <summary>
        /// An overlay that fills the whole view — a flash, a blackout, anything
        /// meant to hide everything at once.
        ///
        /// Stretching a child across the shared canvas achieves that on desktop,
        /// where the canvas IS the screen. In MR the shared canvas is a finite
        /// frame hanging in the room, so the same code shows a "full-screen" white
        /// flash as a rectangle floating in space, with the room visible around it.
        /// There, the overlay is head-locked at the near plane instead.
        /// </summary>
        public static Image MakeFullViewOverlay(Camera cam, string name, int sortingOrder = 150)
        {
            if (cam == null) cam = Camera.main;

            if (!WorldSpace)
            {
                var canvas = EnsureCanvas(cam);
                var flat = new GameObject(name) { hideFlags = HideFlags.DontSave };
                flat.transform.SetParent(canvas.transform, false);
                var frt = flat.AddComponent<RectTransform>();
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = frt.offsetMax = Vector2.zero;
                var flatImg = flat.AddComponent<Image>();
                flatImg.color = Color.clear;
                flatImg.raycastTarget = false;
                return flatImg;
            }

            if (cam == null) return null;
            var holder = new GameObject(name) { hideFlags = HideFlags.DontSave };
            holder.transform.SetParent(cam.transform, false);
            holder.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(cam.nearClipPlane * 2f, 0.06f));
            holder.transform.localRotation = Quaternion.identity;

            var hc = holder.AddComponent<Canvas>();
            hc.renderMode = RenderMode.WorldSpace;
            hc.worldCamera = cam;
            hc.sortingOrder = sortingOrder;
            var hrt = (RectTransform)holder.transform;
            hrt.sizeDelta = new Vector2(1000f, 1000f);
            hrt.localScale = Vector3.one * 0.004f; // 4x4 m at ~6 cm — past any headset FOV

            var img = new GameObject("Fill") { hideFlags = HideFlags.DontSave }.AddComponent<Image>();
            img.transform.SetParent(holder.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.color = Color.clear;
            img.raycastTarget = false;
            return img;
        }

        public static Image MakeImage(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Sprite BuildRounded(int dim, int radius)
        {
            var tex = new Texture2D(dim, dim, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < dim; y++)
            for (int x = 0; x < dim; x++)
            {
                float a = 1f;
                int cx = Mathf.Min(x, dim - 1 - x);
                int cy = Mathf.Min(y, dim - 1 - y);
                if (cx < radius && cy < radius)
                {
                    float d = Mathf.Sqrt((radius - cx) * (radius - cx) + (radius - cy) * (radius - cy));
                    a = Mathf.Clamp01(radius - d + 0.5f);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, dim, dim), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Sprite BuildCircle(int dim)
        {
            var tex = new Texture2D(dim, dim, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float c = (dim - 1) * 0.5f;
            float rOut = c - 1f, rIn = c - 4f;
            for (int y = 0; y < dim; y++)
            for (int x = 0; x < dim; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float ring = Mathf.Clamp01(rOut - d + 0.5f) * Mathf.Clamp01(d - rIn + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, dim, dim), new Vector2(0.5f, 0.5f), 100f);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
