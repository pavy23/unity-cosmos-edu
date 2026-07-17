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
            // Clicking a planet zooms to its tour stop — from free flight OR
            // mid-tour (jumping between stops). Not during the scale truth.
            if (scaleTruth == null || !scaleTruth.IsPlaying)
                ReadPlanetClick();
            if (helpBar != null)
            {
                helpBar.SetActive(showHelp && !AnyPlaying);
                if (helpLocVersion != Loc.Version) { helpLocVersion = Loc.Version; UpdateHelpText(); }
            }
        }

        /// <summary>Screen-space picking — the rig's bodies carry no
        /// colliders, so we compare the click against each body's projected
        /// position and apparent radius (plus finger-friendly padding).</summary>
        void ReadPlanetClick()
        {
            Vector2 click;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            click = mouse.position.ReadValue();
#else
            if (!Input.GetMouseButtonDown(0)) return;
            click = Input.mousePosition;
#endif
            var rig = stage != null ? stage.Rig : null;
            if (rig == null || tour == null) return;
            var cam = GetComponent<Camera>();

            string best = null;
            float bestDist = float.MaxValue;
            foreach (var name in PickableBodies)
            {
                var body = rig.GetBody(name);
                if (body == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(body.position);
                if (sp.z <= 0f) continue; // behind the camera
                float radius = 0.5f;
                foreach (var mr in body.GetComponentsInChildren<MeshRenderer>())
                    radius = Mathf.Max(radius, mr.bounds.extents.magnitude * 0.577f);
                float projected = radius / sp.z * (Screen.height * 0.5f) /
                                  Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float hitRadius = Mathf.Clamp(projected * 1.5f, 22f, 240f);
                float d = Vector2.Distance(click, new Vector2(sp.x, sp.y));
                if (d < hitRadius && d < bestDist) { bestDist = d; best = name; }
            }
            if (best == null) return;
            int index = SolarSystemTour.StopIndexOf(best);
            if (index >= 0) tour.StartTourAt(index);
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
