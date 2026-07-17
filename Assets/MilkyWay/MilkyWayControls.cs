using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MilkyWay
{
    /// <summary>
    /// Keyboard + mouse control for the Milky Way showcase.
    ///   F1  zoom journey (Sun → whole galaxy)
    ///   우클릭 드래그 회전 · 휠 줌(로그) · K 언어 · H 도움말
    /// The orbit-layering trick is the black-hole exhibit's: re-sync from the
    /// transform each input frame so the ambient drift and the user's drag
    /// compose instead of fighting.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MilkyWayControls : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;
        public ZoomJourney journey;
        public NightSkyConnection nightSky;
        public AndromedaCollision andromeda;
        public MilkyWayTour tour;
        public CosmicZoomOut cosmicZoom;
        public SolarSystemTour solarTour;

        bool AnyPlaying =>
            (journey != null && journey.IsPlaying) ||
            (nightSky != null && nightSky.IsPlaying) ||
            (andromeda != null && andromeda.IsPlaying) ||
            (tour != null && tour.Running) ||
            (cosmicZoom != null && cosmicZoom.IsPlaying) ||
            (solarTour != null && solarTour.Running);

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
            if (helpBar != null)
            {
                helpBar.SetActive(showHelp && !AnyPlaying);
                if (helpLocVersion != Loc.Version) { helpLocVersion = Loc.Version; UpdateHelpText(); }
            }
        }

        void ReadHotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame && journey != null && !AnyPlaying) journey.Begin();
            if (kb.f2Key.wasPressedThisFrame && nightSky != null && !AnyPlaying) nightSky.Begin();
            if (kb.f3Key.wasPressedThisFrame && andromeda != null && !AnyPlaying) andromeda.Begin();
            if (kb.f5Key.wasPressedThisFrame && cosmicZoom != null && !AnyPlaying) cosmicZoom.Begin();
            if (kb.f4Key.wasPressedThisFrame && tour != null)
            {
                if (tour.Running) tour.StopTour();
                else if (!AnyPlaying) tour.StartTour();
            }
            if (kb.f6Key.wasPressedThisFrame && solarTour != null)
            {
                if (solarTour.Running) solarTour.StopTour();
                else if (!AnyPlaying) solarTour.StartTour();
            }
            if (tour != null && tour.Running)
            {
                if (kb.nKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) tour.Next();
                if (kb.bKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) tour.Prev();
            }
            if (solarTour != null && solarTour.Running)
            {
                if (kb.nKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) solarTour.Next();
                if (kb.bKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) solarTour.Prev();
            }
            if (kb.kKey.wasPressedThisFrame)
            {
                Loc.Cycle();
                if (tour != null) tour.OnLanguageChanged();
                if (solarTour != null) solarTour.OnLanguageChanged();
            }
            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
#else
            if (Input.GetKeyDown(KeyCode.F1) && journey != null && !AnyPlaying) journey.Begin();
            if (Input.GetKeyDown(KeyCode.F2) && nightSky != null && !AnyPlaying) nightSky.Begin();
            if (Input.GetKeyDown(KeyCode.F3) && andromeda != null && !AnyPlaying) andromeda.Begin();
            if (Input.GetKeyDown(KeyCode.F5) && cosmicZoom != null && !AnyPlaying) cosmicZoom.Begin();
            if (Input.GetKeyDown(KeyCode.F4) && tour != null)
            {
                if (tour.Running) tour.StopTour();
                else if (!AnyPlaying) tour.StartTour();
            }
            if (Input.GetKeyDown(KeyCode.F6) && solarTour != null)
            {
                if (solarTour.Running) solarTour.StopTour();
                else if (!AnyPlaying) solarTour.StartTour();
            }
            if (tour != null && tour.Running)
            {
                if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow)) tour.Next();
                if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.LeftArrow)) tour.Prev();
            }
            if (solarTour != null && solarTour.Running)
            {
                if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow)) solarTour.Next();
                if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.LeftArrow)) solarTour.Prev();
            }
            if (Input.GetKeyDown(KeyCode.K))
            {
                Loc.Cycle();
                if (tour != null) tour.OnLanguageChanged();
                if (solarTour != null) solarTour.OnLanguageChanged();
            }
            if (Input.GetKeyDown(KeyCode.H)) showHelp = !showHelp;
#endif
        }

        void SyncFromTransform()
        {
            Vector3 offset = transform.position; // galaxy sits at the origin
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
                if (Mathf.Abs(scroll) > 10f) scroll /= 120f; // Windows notches
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
                pitch = Mathf.Clamp(pitch + dy * 0.25f, -80f, 80f);
            }
            if (zooming) distance *= Mathf.Pow(0.86f, scroll); // log zoom
            distance = Mathf.Clamp(distance, 2f, 110f);

            float pr = pitch * Mathf.Deg2Rad, yr = yaw * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), Mathf.Cos(yr) * Mathf.Cos(pr));
            transform.position = dir * distance;
            transform.LookAt(Vector3.zero);
        }

        void BuildHelp()
        {
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
            var bar = BlackHoleUI.MakePanel(canvas.transform, "MW Help Bar",
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
                Key("F1") + "줌 여행   " + Key("F2") + "밤하늘   " + Key("F3") + "안드로메다   " + Key("F4") + "은하 투어   " + Key("F5") + "우주 줌아웃   " + Key("F6") + "태양계 투어   " + Key("우클릭") + "회전   " + Key("휠") + "줌   " + Key("K") + "언어   " + Key("H") + "도움말",
                Key("F1") + "zoom journey   " + Key("F2") + "night sky   " + Key("F3") + "Andromeda   " + Key("F4") + "galaxy tour   " + Key("F5") + "cosmic zoom-out   " + Key("F6") + "solar system   " + Key("R-drag") + "orbit   " + Key("wheel") + "zoom   " + Key("K") + "language   " + Key("H") + "help",
                Key("F1") + "ズームの旅   " + Key("F2") + "夜空   " + Key("F3") + "アンドロメダ   " + Key("F4") + "銀河ツアー   " + Key("F5") + "宇宙ズームアウト   " + Key("F6") + "太陽系ツアー   " + Key("右ドラッグ") + "回転   " + Key("ホイール") + "ズーム   " + Key("K") + "言語   " + Key("H") + "ヘルプ",
                Key("F1") + "缩放之旅   " + Key("F2") + "夜空   " + Key("F3") + "仙女座   " + Key("F4") + "星系导览   " + Key("F5") + "宇宙缩放   " + Key("F6") + "太阳系之旅   " + Key("右键拖动") + "旋转   " + Key("滚轮") + "缩放   " + Key("K") + "语言   " + Key("H") + "帮助");
        }
    }
}
