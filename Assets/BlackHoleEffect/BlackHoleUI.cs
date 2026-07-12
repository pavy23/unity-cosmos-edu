using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// Shared UI factory for the showcase overlays: one screen-space-camera
    /// canvas (CanvasScaler handles any aspect ratio), procedurally generated
    /// rounded-rect / circle sprites, and a dark + gold theme that matches the
    /// black hole. Everything is DontSave — nothing pollutes the scene file.
    /// </summary>
    public static class BlackHoleUI
    {
        // Theme
        public static readonly Color PanelBg = new Color(0.03f, 0.045f, 0.075f, 0.86f);
        public static readonly Color Accent = new Color(1f, 0.76f, 0.42f, 0.95f);
        public static readonly Color TextPrimary = new Color(0.91f, 0.93f, 0.96f, 1f);
        public static readonly Color TextSecondary = new Color(0.62f, 0.66f, 0.74f, 1f);
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
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam != null ? cam : Camera.main;
            // Just past the near plane: screen-space-camera UI is depth-tested
            // against opaque geometry, and during the fall-in the camera gets
            // closer than 1 unit to the raymarch quad — at planeDistance 1 the
            // quad would occlude every caption right when they matter most.
            var refCam = canvas.worldCamera;
            canvas.planeDistance = refCam != null ? Mathf.Max(refCam.nearClipPlane * 1.5f, 0.15f) : 0.15f;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
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

        static void EnsureInteraction()
        {
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            var es = new GameObject("BlackHole EventSystem") { hideFlags = HideFlags.DontSave };
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
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
