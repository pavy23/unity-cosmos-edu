using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MilkyWay
{
    /// <summary>
    /// Keyboard + mouse control for the solar-system exhibit.
    ///   F1 planet tour (N/B)  ·  F2 the scale truth  ·  F9 to the Milky Way
    ///   우클릭 회전 · 휠 줌(로그) · M 소리 · K 언어 · H 도움말
    /// Same orbit-layering trick as the other exhibits: re-sync from the
    /// transform each input frame so ambient drift and the user's drag
    /// compose instead of fighting.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SolarSystemControls : MonoBehaviour
    {
        public CinematicOrbit orbit;
        public SolarSystemTour tour;
        public ScaleTruth scaleTruth;
        public MilkyWayAudio audioScape;
        public SolarSystemStage stage;

        static readonly string[] PickableBodies =
            { "Sun", "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune" };

        static readonly string[] BodyNamesKo = { "태양", "수성", "금성", "지구", "화성", "목성", "토성", "천왕성", "해왕성" };
        static readonly string[] BodyNamesEn = { "Sun", "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune" };
        static readonly string[] BodyNamesJa = { "太陽", "水星", "金星", "地球", "火星", "木星", "土星", "天王星", "海王星" };
        static readonly string[] BodyNamesZh = { "太阳", "水星", "金星", "地球", "火星", "木星", "土星", "天王星", "海王星" };

        // hover affordance: a pulsing ring around the body under the cursor
        Transform hoverRing;
        Material hoverMat;
        Texture2D ringTex;
        RectTransform tipPanel;
        Text tipText;
        int hoverIndex = -1;

        bool AnyPlaying =>
            (tour != null && tour.Running) ||
            (scaleTruth != null && scaleTruth.IsPlaying);

        float distance, yaw, pitch;
        GameObject helpBar;
        Text help;
        bool showHelp = true;
        int helpLocVersion = -1;

        void Start()
        {
            if (Application.isPlaying) LanguageSelect.CreateWidget();
            BuildHelp();
        }

        void Update()
        {
            ReadHotkeys();
            if (!AnyPlaying)
                ReadMouse();
            // Clicking a planet zooms to its tour stop — from FREE FLIGHT
            // only. While zoomed in, the mouse belongs to rotating the view
            // (SolarSystemTour's drag-orbit), so picking is off and the
            // affordance ring hides.
            if (!AnyPlaying)
            {
                ReadPlanetClick();
            }
            else
            {
                hoverIndex = -1;
                if (hoverRing != null) hoverRing.gameObject.SetActive(false);
                if (tipPanel != null) tipPanel.gameObject.SetActive(false);
            }
            if (helpBar != null)
            {
                helpBar.SetActive(showHelp && !AnyPlaying);
                if (helpLocVersion != Loc.Version) { helpLocVersion = Loc.Version; UpdateHelpText(); }
            }
        }

        /// <summary>Screen-space picking — the rig's bodies carry no
        /// colliders, so we compare the pointer against each body's projected
        /// position and apparent radius (plus finger-friendly padding).
        /// Returns the PickableBodies index, or -1; out params locate the hit
        /// body for the hover ring.</summary>
        int PickBody(Vector2 pointer, out Vector3 bodyPos, out float bodyRadius)
        {
            bodyPos = Vector3.zero; bodyRadius = 0f;
            var rig = stage != null ? stage.Rig : null;
            if (rig == null) return -1;
            var cam = GetComponent<Camera>();

            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < PickableBodies.Length; i++)
            {
                var body = rig.GetBody(PickableBodies[i]);
                if (body == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(body.position);
                if (sp.z <= 0f) continue; // behind the camera
                float radius = 0.5f;
                foreach (var mr in body.GetComponentsInChildren<MeshRenderer>())
                    radius = Mathf.Max(radius, mr.bounds.extents.magnitude * 0.577f);
                float projected = radius / sp.z * (Screen.height * 0.5f) /
                                  Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float hitRadius = Mathf.Clamp(projected * 1.5f, 22f, 240f);
                float d = Vector2.Distance(pointer, new Vector2(sp.x, sp.y));
                if (d < hitRadius && d < bestDist)
                {
                    bestDist = d; best = i;
                    bodyPos = body.position; bodyRadius = radius;
                }
            }
            return best;
        }

        void ReadPlanetClick()
        {
            Vector2 pointer;
            bool clicked;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            pointer = mouse.position.ReadValue();
            clicked = mouse.leftButton.wasPressedThisFrame;
#else
            pointer = Input.mousePosition;
            clicked = Input.GetMouseButtonDown(0);
#endif
            hoverIndex = PickBody(pointer, out Vector3 bodyPos, out float bodyRadius);
            UpdateHoverAffordance(pointer, bodyPos, bodyRadius);

            if (!clicked || hoverIndex < 0 || tour == null) return;
            int index = SolarSystemTour.StopIndexOf(PickableBodies[hoverIndex]);
            if (index >= 0) tour.StartTourAt(index);
        }

        // ---- the hover affordance: a pulsing ring + a name tag -------------

        void UpdateHoverAffordance(Vector2 pointer, Vector3 bodyPos, float bodyRadius)
        {
            var cam = GetComponent<Camera>();
            if (hoverIndex >= 0)
            {
                EnsureHoverRing();
                hoverRing.gameObject.SetActive(true);
                hoverRing.position = bodyPos;
                // face the camera, sized comfortably outside the body,
                // breathing gently so it reads as alive/selectable
                float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4.2f);
                hoverRing.localScale = Vector3.one * bodyRadius * 3.1f * pulse;
                hoverRing.rotation = Quaternion.LookRotation(hoverRing.position - cam.transform.position);
                hoverMat.color = new Color(1f, 0.78f, 0.35f, 0.5f + 0.15f * Mathf.Sin(Time.time * 4.2f));

                EnsureTip();
                tipPanel.gameObject.SetActive(true);
                tipPanel.anchoredPosition = new Vector2(
                    pointer.x / Screen.width * 1920f - 960f + 16f,
                    pointer.y / Screen.height * 1080f - 540f + 34f);
                tipText.text = Loc.T(BodyNamesKo[hoverIndex], BodyNamesEn[hoverIndex],
                                     BodyNamesJa[hoverIndex], BodyNamesZh[hoverIndex]);
            }
            else
            {
                if (hoverRing != null) hoverRing.gameObject.SetActive(false);
                if (tipPanel != null) tipPanel.gameObject.SetActive(false);
            }
        }

        void EnsureHoverRing()
        {
            if (hoverRing != null) return;
            // a soft ring texture, drawn once
            const int S = 128;
            ringTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f - 0.5f, S / 2f - 0.5f)) / (S / 2f);
                    float band = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.055f, 2f));
                    px[y * S + x] = new Color(1f, 1f, 1f, band);
                }
            ringTex.SetPixels(px);
            ringTex.Apply();

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Hover Ring";
            Destroy(go.GetComponent<Collider>());
            hoverMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = ringTex, renderQueue = 3200 };
            go.GetComponent<MeshRenderer>().sharedMaterial = hoverMat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hoverRing = go.transform;
        }

        void EnsureTip()
        {
            if (tipPanel != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
            tipPanel = BlackHoleUI.MakePanel(canvas.transform, "Body Tip",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), Vector2.zero, new Vector2(150f, 40f),
                accentLine: false);
            tipText = BlackHoleUI.MakeText(tipPanel, "Text", 18, BlackHoleUI.TitleGold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 30f), FontStyle.Bold);
        }

        void ReadHotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame && tour != null)
            {
                if (tour.Running) tour.StopTour();
                else if (!AnyPlaying) tour.StartTour();
            }
            if (kb.f2Key.wasPressedThisFrame && scaleTruth != null)
            {
                if (scaleTruth.IsPlaying) scaleTruth.Abort();
                else if (!AnyPlaying) scaleTruth.Begin();
            }
            if (kb.f9Key.wasPressedThisFrame && !AnyPlaying)
                UnityEngine.SceneManagement.SceneManager.LoadScene("MilkyWayShowcase");
            if (tour != null && tour.Running)
            {
                if (kb.nKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) tour.Next();
                if (kb.bKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) tour.Prev();
            }
            if (kb.kKey.wasPressedThisFrame)
            {
                Loc.Cycle();
                if (tour != null) tour.OnLanguageChanged();
            }
            if (kb.mKey.wasPressedThisFrame && audioScape != null) audioScape.muted = !audioScape.muted;
            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
#else
            if (Input.GetKeyDown(KeyCode.F1) && tour != null)
            {
                if (tour.Running) tour.StopTour();
                else if (!AnyPlaying) tour.StartTour();
            }
            if (Input.GetKeyDown(KeyCode.F2) && scaleTruth != null)
            {
                if (scaleTruth.IsPlaying) scaleTruth.Abort();
                else if (!AnyPlaying) scaleTruth.Begin();
            }
            if (Input.GetKeyDown(KeyCode.F9) && !AnyPlaying)
                UnityEngine.SceneManagement.SceneManager.LoadScene("MilkyWayShowcase");
            if (tour != null && tour.Running)
            {
                if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow)) tour.Next();
                if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.LeftArrow)) tour.Prev();
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                Loc.Cycle();
                if (tour != null) tour.OnLanguageChanged();
            }
            if (Input.GetKeyDown(KeyCode.M) && audioScape != null) audioScape.muted = !audioScape.muted;
            if (Input.GetKeyDown(KeyCode.H)) showHelp = !showHelp;
#endif
        }

        void SyncFromTransform()
        {
            Vector3 offset = transform.position; // the Sun sits at the origin
            distance = offset.magnitude;
            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(distance, 0.001f), -1f, 1f)) * Mathf.Rad2Deg;
        }

        void ReadMouse()
        {
            float dx = 0f, dy = 0f, scroll = 0f;
            bool dragging = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                dragging = mouse.rightButton.isPressed;
                var d = mouse.delta.ReadValue();
                dx = d.x; dy = d.y;
                scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 10f) scroll /= 120f;
            }
#else
            dragging = Input.GetMouseButton(1);
            dx = Input.GetAxis("Mouse X") * 12f;
            dy = Input.GetAxis("Mouse Y") * 12f;
            scroll = Input.mouseScrollDelta.y;
#endif
            bool zooming = !Mathf.Approximately(scroll, 0f);
            if (!dragging && !zooming) return;

            SyncFromTransform();
            if (dragging)
            {
                yaw += dx * 0.25f;
                pitch = Mathf.Clamp(pitch + dy * 0.25f, -85f, 85f);
            }
            if (zooming) distance *= Mathf.Pow(0.86f, scroll);
            // From Mercury's doorstep to well past the display map's Neptune.
            distance = Mathf.Clamp(distance, 6f, 900f);

            float pr = pitch * Mathf.Deg2Rad, yr = yaw * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), Mathf.Cos(yr) * Mathf.Cos(pr));
            transform.position = dir * distance;
            transform.LookAt(Vector3.zero);
        }

        void BuildHelp()
        {
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
            var bar = BlackHoleUI.MakePanel(canvas.transform, "Sol Help Bar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1240f, 40f),
                accentLine: false);
            helpBar = bar.gameObject;
            help = BlackHoleUI.MakeText(bar, "Text", 15, BlackHoleUI.TextSecondary, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 32f));
            UpdateHelpText();
        }

        void OnDestroy()
        {
            if (hoverMat != null) Destroy(hoverMat);
            if (ringTex != null) Destroy(ringTex);
        }

        static string Key(string k) => "<color=#FFC46E>" + k + "</color> ";

        void UpdateHelpText()
        {
            if (help == null) return;
            help.text = Loc.T(
                Key("클릭") + "행성 줌인   " + Key("F1") + "행성 투어(N/B)   " + Key("F2") + "진짜 크기   " + Key("F9") + "우리은하 전시로   " + Key("우클릭") + "회전   " + Key("휠") + "줌   " + Key("M") + "소리   " + Key("K") + "언어   " + Key("H") + "도움말",
                Key("Click") + "zoom to planet   " + Key("F1") + "planet tour(N/B)   " + Key("F2") + "the true scale   " + Key("F9") + "to the Milky Way   " + Key("R-drag") + "orbit   " + Key("wheel") + "zoom   " + Key("M") + "sound   " + Key("K") + "language   " + Key("H") + "help",
                Key("クリック") + "惑星ズーム   " + Key("F1") + "惑星ツアー(N/B)   " + Key("F2") + "本当の縮尺   " + Key("F9") + "天の川展示へ   " + Key("右ドラッグ") + "回転   " + Key("ホイール") + "ズーム   " + Key("M") + "音   " + Key("K") + "言語   " + Key("H") + "ヘルプ",
                Key("点击") + "缩放到行星   " + Key("F1") + "行星导览(N/B)   " + Key("F2") + "真实比例   " + Key("F9") + "去银河系展区   " + Key("右键拖动") + "旋转   " + Key("滚轮") + "缩放   " + Key("M") + "声音   " + Key("K") + "语言   " + Key("H") + "帮助");
        }
    }
}
