using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI

namespace MilkyWay
{
    /// <summary>
    /// The nebulae &amp; clusters browser: steps through the specimens one at a
    /// time, gliding the camera to a three-quarter view of each and showing its
    /// museum label (name, facts, blurb). ◀ / ▶ or the on-card buttons move
    /// between objects; a slow drift keeps the parked view alive. The narrated
    /// "life of a star" tour will layer on top of this later.
    /// </summary>
    public class NebulaGallery : MonoBehaviour
    {
        public NebulaController controller;
        public BlackHoleEffect.CinematicOrbit orbit;
        public NebulaTour tour;
        public float glideDuration = 2.4f;

        bool tourActive;
        GameObject startBtn;
        int index = -1;
        float glideT = 1f;
        Vector3 fromPos, toPos, fromLook, toLook, curLook;

        RectTransform card;
        Text cardTitle, cardFacts, cardBody, cardCount;
        Text prevLabel, nextLabel;

        // Each specimen sits against a real region of the ESO Milky-Way panorama:
        // we rotate the equirect skybox to that region and dim it to taste, so no
        // two objects share the same background. Applied instantly per specimen
        // (the view is static — no camera drift).
        Material skyMat;

        void Start()
        {
            if (controller == null || controller.Count == 0) return;
            if (orbit != null) orbit.enabled = false;
            // Instance the skybox so we never write specimen params into the shared
            // asset (the StarfieldSkybox drift lesson).
            if (RenderSettings.skybox != null)
            {
                skyMat = new Material(RenderSettings.skybox);
                RenderSettings.skybox = skyMat;
            }
            LanguageSelect.CreateWidget();
            BuildNav();
            BuildStartButton();
            Loc.Changed -= Refresh; Loc.Changed += Refresh;
            Frame(0, instant: true);
        }

        Text startBtnLabel;
        void BuildStartButton()
        {
            if (tour == null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>() ?? Camera.main);
            var btn = BlackHoleUI.MakeButton(canvas.transform, "Life Tour Start", "",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -26f), new Vector2(300f, 40f),
                () => tour.StartTour());
            startBtn = btn.gameObject;
            startBtnLabel = btn.GetComponentInChildren<Text>();
            if (startBtnLabel != null)
            {
                startBtnLabel.color = BlackHoleUI.TitleGold;
                startBtnLabel.text = Loc.T("▶  별의 일생 투어", "▶  The Life of a Star",
                                           "▶  星の一生ツアー", "▶  恒星的一生");
            }
        }

        void ApplyBg(int i)
        {
            if (skyMat == null) return;
            var h = controller.Hero(i);
            Color tint = h.bgTint.maxColorComponent > 0f ? h.bgTint : new Color(0.34f, 0.34f, 0.38f);
            skyMat.SetFloat("_Rotation", h.bgRotation);
            skyMat.SetColor("_Tint", tint);
        }

        void OnDestroy() { Loc.Changed -= Refresh; }

        void BuildNav()
        {
            var nav = new GameObject("Scene Navigator").AddComponent<SceneNavigator>();
            nav.Init(new[]
            {
                new SceneNavigator.Dest { scene = "MilkyWayShowcase",
                    name = () => Loc.T("우리은하", "Milky Way", "天の川銀河", "银河系"), image = "TitleCards/card_galaxy" },
                new SceneNavigator.Dest { scene = "SolarSystemShowcase",
                    name = () => Loc.T("태양계", "Solar System", "太陽系", "太阳系"), image = "TitleCards/card_solar" },
                new SceneNavigator.Dest { scene = "BlackHoleShowcase",
                    name = () => Loc.T("블랙홀", "Black Hole", "ブラックホール", "黑洞"), image = "TitleCards/card_blackhole" },
            }, home: true, verticalLayout: true);
        }

        public void Next() { if (controller != null) Frame((index + 1) % controller.Count); }
        public void Prev() { if (controller != null) Frame((index - 1 + controller.Count) % controller.Count); }

        /// <summary>Glide to a specimen — the narrated tour drives the gallery
        /// through this, reusing all the framing / one-at-a-time / sky logic.</summary>
        public void ShowSpecimen(int i) { if (controller != null) Frame(i); }

        /// <summary>Tour takes over: hide the browse card and the start button and
        /// stop reading the arrow keys; the tour drives instead.</summary>
        public void SetTourActive(bool on)
        {
            tourActive = on;
            if (card != null) card.gameObject.SetActive(!on);
            if (startBtn != null) startBtn.SetActive(!on);
        }

        void Frame(int i, bool instant = false)
        {
            index = i;
            // Only the framed specimen renders — each nebula is a full-screen
            // raymarch, so showing all six at once is a heavy, needless load (and
            // lets neighbours bleed into the shot). Activate one, hide the rest.
            for (int k = 0; k < controller.Count; k++)
            {
                var rk = controller.Root(k);
                if (rk != null) rk.gameObject.SetActive(k == i);
            }
            var t = controller.Root(i);
            float radius = controller.Radius(i);

            // A sun-lit three-quarter view: back-and-up-and-to-the-side.
            Vector3 dir = new Vector3(0.35f, 0.32f, -1f).normalized;
            toPos = t.position + dir * radius * controller.Hero(i).framing;
            toLook = t.position;

            fromPos = transform.position;
            fromLook = curLook;
            glideT = instant ? 1f : 0f;
            if (instant) { transform.position = toPos; transform.LookAt(toLook); curLook = toLook; }

            // Snap the sky to this specimen's region of the panorama.
            ApplyBg(i);

            EnsureCard();
            Refresh();
        }

        void Update()
        {
            if (controller == null || controller.Count == 0) return;

            if (glideT < 1f)
            {
                glideT = Mathf.Min(1f, glideT + Time.deltaTime / Mathf.Max(glideDuration, 0.1f));
                float u = Mathf.SmoothStep(0f, 1f, glideT);
                transform.position = Vector3.Lerp(fromPos, toPos, u);
                curLook = Vector3.Lerp(fromLook, toLook, u);
                transform.LookAt(curLook);
            }
            else
            {
                // Held static — no drift (each specimen is a clean, still view).
                curLook = toLook;
            }

            if (tourActive) return;   // the tour owns navigation while it runs
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame) Next();
                if (kb.leftArrowKey.wasPressedThisFrame) Prev();
            }
#else
            if (Input.GetKeyDown(KeyCode.RightArrow)) Next();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Prev();
#endif
        }

        void Refresh()
        {
            if (startBtnLabel != null)
                startBtnLabel.text = Loc.T("▶  별의 일생 투어", "▶  The Life of a Star",
                                           "▶  星の一生ツアー", "▶  恒星的一生");
            if (card == null || index < 0) return;
            var h = controller.Hero(index);
            cardTitle.text = h.name();
            cardFacts.text = h.facts();
            cardBody.text = h.blurb();
            cardCount.text = (index + 1) + " / " + controller.Count;
            if (prevLabel != null) prevLabel.text = Loc.T("◀ 이전", "◀ Prev", "◀ 前へ", "◀ 上一个");
            if (nextLabel != null) nextLabel.text = Loc.T("다음 ▶", "Next ▶", "次へ ▶", "下一个 ▶");
        }

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>() ?? Camera.main);

            card = BlackHoleUI.MakePanel(canvas.transform, "Nebula Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1120f, 240f));

            cardTitle = BlackHoleUI.MakeText(card, "Title", 30, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -18f), new Vector2(860f, 40f), FontStyle.Bold);
            cardFacts = BlackHoleUI.MakeText(card, "Facts", 17, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -62f), new Vector2(1060f, 24f));
            cardBody = BlackHoleUI.MakeText(card, "Body", 20, BlackHoleUI.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -92f), new Vector2(1060f, 130f));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            cardCount = BlackHoleUI.MakeText(card, "Count", 15, BlackHoleUI.TextSecondary, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 14f), new Vector2(120f, 22f));

            prevLabel = BlackHoleUI.MakeButton(card, "Prev", "",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 14f), new Vector2(140f, 40f), Prev)
                .GetComponentInChildren<Text>();
            nextLabel = BlackHoleUI.MakeButton(card, "Next", "",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-180f, 14f), new Vector2(140f, 40f), Next)
                .GetComponentInChildren<Text>();
        }
    }
}
