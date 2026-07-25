using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MilkyWay
{
    /// <summary>
    /// Pointer control for the Milky Way showcase. The nine experiences are
    /// toolbar buttons (MilkyWayToolbar); here: right-drag / one-finger drag
    /// to orbit, wheel or pinch to zoom (log), ← → to step a running tour.
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
        public RotationCurveLab rotationLab;
        public GalaxyZoo zoo;
        public SgrACrossover sgrA;
        public MilkyWayAudio audioScape;

        bool AnyPlaying =>
            (journey != null && journey.IsPlaying) ||
            (nightSky != null && nightSky.IsPlaying) ||
            (andromeda != null && andromeda.IsPlaying) ||
            (tour != null && tour.Running) ||
            (cosmicZoom != null && cosmicZoom.IsPlaying) ||
            (solarTour != null && solarTour.Running) ||
            (rotationLab != null && rotationLab.IsPlaying) ||
            (zoo != null && zoo.Running) ||
            (sgrA != null && sgrA.IsPlaying);

        float distance, yaw, pitch;
        bool immersive;

        void Start()
        {
            if (Application.isPlaying) LanguageSelect.CreateWidget();
            // The K-key language handler used to refresh a running tour's card;
            // now that language changes come from the LanguageSelect widget,
            // relay them the same way.
            Loc.Changed -= OnLocChanged;
            Loc.Changed += OnLocChanged;
        }

        void OnDestroy() { Loc.Changed -= OnLocChanged; }

        void OnLocChanged()
        {
            if (tour != null) tour.OnLanguageChanged();
            if (solarTour != null) solarTour.OnLanguageChanged();
            if (zoo != null) zoo.OnLanguageChanged();
        }

        void Update()
        {
            // Esc leaves immersive view (the toolbar that would toggle it is hidden).
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && immersive) SetImmersive(false);
#else
            if (Input.GetKeyDown(KeyCode.Escape) && immersive) SetImmersive(false);
#endif
            ReadTourNav();
            if (!AnyPlaying)
                ReadMouse();
        }

        // ---- toolbar entry points (click-only UI; guards centralized here) ---
        public bool Busy => AnyPlaying;
        public bool Immersive => immersive;
        public void SetImmersive(bool on)
        {
            immersive = on;
            LanguageSelect.SetVisible(!on);
            if (on) ImmersiveHint.Show(() => SetImmersive(false)); else ImmersiveHint.Hide();
        }
        public void PlayJourney() { if (journey != null && !AnyPlaying) journey.Begin(); }
        public void PlayNightSky() { if (nightSky != null && !AnyPlaying) nightSky.Begin(); }
        public void PlayAndromeda() { if (andromeda != null && !AnyPlaying) andromeda.Begin(); }
        public void ToggleTour() { if (tour == null) return; if (tour.Running) tour.StopTour(); else if (!AnyPlaying) tour.StartTour(); }
        public void PlayCosmicZoom() { if (cosmicZoom != null && !AnyPlaying) cosmicZoom.Begin(); }
        public void ToggleSolarTour() { if (solarTour == null) return; if (solarTour.Running) solarTour.StopTour(); else if (!AnyPlaying) solarTour.StartTour(); }
        public void ToggleRotationLab() { if (rotationLab == null) return; if (rotationLab.IsPlaying) rotationLab.Abort(); else if (!AnyPlaying) rotationLab.Begin(); }
        public void ToggleZoo() { if (zoo == null) return; if (zoo.Running) zoo.StopZoo(); else if (!AnyPlaying) zoo.StartZoo(); }
        public void PlaySgrA() { if (sgrA != null && !AnyPlaying) sgrA.Begin(); }
        public void ToggleMute() { if (audioScape != null) audioScape.muted = !audioScape.muted; }
        public static void LoadScene(string s) => UnityEngine.SceneManagement.SceneManager.LoadScene(s);

        /// <summary>The only key left is the arrows, to step whichever tour is
        /// running — everything else is a toolbar button (see MilkyWayToolbar)
        /// so WebGL never collides with browser shortcuts.</summary>
        void ReadTourNav()
        {
            bool next = false, prev = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            next = kb.rightArrowKey.wasPressedThisFrame;
            prev = kb.leftArrowKey.wasPressedThisFrame;
#else
            next = Input.GetKeyDown(KeyCode.RightArrow);
            prev = Input.GetKeyDown(KeyCode.LeftArrow);
#endif
            if (!next && !prev) return;
            if (tour != null && tour.Running) { if (next) tour.Next(); else tour.Prev(); }
            else if (solarTour != null && solarTour.Running) { if (next) solarTour.Next(); else solarTour.Prev(); }
            else if (zoo != null && zoo.Running) { if (next) zoo.Next(); else zoo.Prev(); }
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
            // Mobile browsers: one-finger drag orbits, two-finger pinch zooms.
            if (TouchOrbit.Dragging)
            {
                dragging = true;
                dx += TouchOrbit.DragDelta.x; dy += TouchOrbit.DragDelta.y;
            }
            scroll += TouchOrbit.PinchNotches;
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


    }
}
