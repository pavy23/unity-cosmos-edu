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
        }

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
                // LegacyRuntime is a dynamic font with OS fallback (Korean
                // renders fine). CreateDynamicFontFromOSFont does not
                // rasterize reliably in edit mode, so we avoid it.
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
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
                    Object.DestroyImmediate(c.gameObject);
            if (canvas != null) Object.DestroyImmediate(canvas.gameObject);

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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
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
