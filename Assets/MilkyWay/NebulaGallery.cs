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
        public float glideDuration = 2.4f;

        int index = -1;
        float glideT = 1f;
        Vector3 fromPos, toPos, fromLook, toLook, curLook;

        RectTransform card;
        Text cardTitle, cardFacts, cardBody, cardCount;
        Text prevLabel, nextLabel;

        // Each specimen carries its own sky (the real setting it sits in — a dense
        // Milky-Way star cloud for the in-plane nebulae, a sparse halo field with
        // distant galaxies for the globular). We drive a private skybox instance
        // and crossfade it as the camera glides between objects.
        struct BgCfg { public float density, galaxies, band; public Color tint, bandColor; public Vector3 bandAxis; }
        Material skyMat;
        BgCfg bgFrom, bgTo;

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
            Loc.Changed -= Refresh; Loc.Changed += Refresh;
            Frame(0, instant: true);
        }

        BgCfg Cfg(int i)
        {
            var h = controller.Hero(i);
            Color tint = h.bgTint.a > 0f || h.bgTint.maxColorComponent > 0f ? h.bgTint : Color.white;
            Color bandCol = h.bgBandColor.maxColorComponent > 0f ? h.bgBandColor : new Color(0.5f, 0.4f, 0.32f);
            Vector3 axis = h.bgBandAxis == Vector3.zero ? Vector3.up : h.bgBandAxis;
            return new BgCfg { density = h.bgDensity, galaxies = h.bgGalaxies, band = h.bgBand,
                               tint = tint, bandColor = bandCol, bandAxis = axis };
        }

        void ApplyBg(BgCfg a, BgCfg b, float u)
        {
            if (skyMat == null) return;
            skyMat.SetFloat("_StarDensity", Mathf.Lerp(a.density, b.density, u));
            skyMat.SetFloat("_GalaxyCount", Mathf.Lerp(a.galaxies, b.galaxies, u));
            skyMat.SetFloat("_BandStrength", Mathf.Lerp(a.band, b.band, u));
            skyMat.SetColor("_Tint", Color.Lerp(a.tint, b.tint, u));
            skyMat.SetColor("_BandColor", Color.Lerp(a.bandColor, b.bandColor, u));
            skyMat.SetVector("_BandAxis", Vector3.Slerp(a.bandAxis, b.bandAxis, u));
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

            // Crossfade the sky from wherever it is now to this specimen's setting.
            bgFrom = index >= 0 && skyMat != null ? bgTo : Cfg(i);
            bgTo = Cfg(i);
            if (instant) { bgFrom = bgTo; ApplyBg(bgTo, bgTo, 1f); }

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
                // Slow parallax drift around the framed object.
                transform.RotateAround(toLook, Vector3.up, 1.4f * Time.deltaTime);
                curLook = toLook;
            }

            // Crossfade the background in step with the camera glide.
            ApplyBg(bgFrom, bgTo, Mathf.SmoothStep(0f, 1f, glideT));

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
