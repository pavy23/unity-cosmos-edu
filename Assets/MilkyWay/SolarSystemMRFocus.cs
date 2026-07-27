using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager

namespace MilkyWay
{
    /// <summary>
    /// Tap a planet in the orrery and hold it at arm's length.
    ///
    /// The room miniature puts Neptune's orbit at 1.2 m, which makes the
    /// planets inside it 5-15 mm across — big enough to see that they are
    /// there, far too small to look AT. The tour solved that by flying a
    /// desktop camera to each one in turn, which MR cannot do: the camera is
    /// the visitor's head. So the planet comes to them instead. Aim at one,
    /// pull the trigger, and the orrery fades out while that single body rises
    /// to 25 cm across, 60 cm from the eye — the distance you would hold a
    /// grapefruit at — turning on its own axis, with its numbers beside it.
    ///
    /// Built at runtime by <see cref="SolarSystemMRControls"/> rather than
    /// authored in the scene: the bodies it needs to make tappable do not exist
    /// until <see cref="SolarSystemRig"/> has built them.
    /// </summary>
    [DisallowMultipleComponent]
    public class SolarSystemMRFocus : MonoBehaviour
    {
        public SolarSystemMRStage stage;
        public SolarSystemMRControls controls;
        public Camera viewer;

        [Tooltip("How far in front of the eye the focused body floats. 0.6 m is " +
                 "reading distance — close enough to fill the view, past the " +
                 "~0.4 m where the eyes start to strain converging.")]
        public float distance = 0.6f;

        [Tooltip("Diameter of the focused body, in metres. Every body is shown " +
                 "at the SAME size: this card is about what a world looks like, " +
                 "and true relative scale is the 진짜 크기 sequence's job.")]
        public float diameter = 0.25f;

        [Tooltip("Seconds the orrery takes to fade out and back.")]
        public float fadeSeconds = 0.4f;

        [Tooltip("Degrees per second the focused body turns. Slow — fast enough " +
                 "to read as a globe, slow enough to look at one feature.")]
        public float spinDegPerSec = 9f;

        [Tooltip("Minimum world radius of a planet's tap target. Mercury's own " +
                 "sphere is about 5 mm at exhibit scale; a hand ray cannot hold " +
                 "steady on that, so the invisible collider is grown to this.")]
        public float pickRadius = 0.022f;

        public bool Focused => focusRoot != null;

        Transform focusRoot;
        RectTransform card;
        Text cardTitle, cardFacts, cardBody;
        Text backLabel;
        GameObject backButton;
        Coroutine fade;
        int shownIndex = -1;
        readonly List<GameObject> pickers = new();

        void Start()
        {
            if (viewer == null) viewer = Camera.main;
            BuildPickers();
        }

        void Update()
        {
            if (focusRoot == null) return;
            focusRoot.Rotate(0f, spinDegPerSec * Time.deltaTime, 0f, Space.Self);
            // The card and the body are placed once, not tracked: a body that
            // slid around with every head turn would be a HUD, and you could
            // never walk around it to see the far side.
        }

        // ---------------- tap targets ---------------------------------------

        /// <summary>One invisible sphere per body, parented to the body's own
        /// visual so it orbits with it. Added in Start, deliberately after the
        /// stage's XRGrabInteractable has collected its colliders in Awake —
        /// otherwise these would become extra grab handles for the whole
        /// orrery and tapping a planet would pick the exhibit up.</summary>
        void BuildPickers()
        {
            if (stage == null || stage.Rig == null) return;

            foreach (var key in SolarSystemMRStage.BodyKeys)
            {
                var visual = stage.Rig.GetBodyVisual(key);
                if (visual == null) continue;

                var go = new GameObject("Pick — " + key);
                go.transform.SetParent(visual, false);

                // The visual's lossyScale IS its world radius, so a collider of
                // local radius 1 hugs the body. Grow it only where the body is
                // smaller than a hand ray can reliably hold.
                float worldRadius = Mathf.Max(visual.lossyScale.x, 1e-5f);
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = Mathf.Max(1f, pickRadius / worldRadius);
                sc.isTrigger = true;

                var interactable = go.AddComponent<XRSimpleInteractable>();
                interactable.colliders.Clear();
                interactable.colliders.Add(sc);
                string k = key; // capture per body, not the loop variable
                interactable.selectEntered.AddListener(_ => Focus(k));

                pickers.Add(go);
            }
        }

        void SetPickersActive(bool on)
        {
            pickers.RemoveAll(p => p == null);
            foreach (var p in pickers) p.SetActive(on);
        }

        // ---------------- focus / release ------------------------------------

        public void Focus(string key)
        {
            if (stage == null || stage.Rig == null) return;
            if (Focused) return;
            // The tour owns the view while it runs — it is flying the highlight
            // ring around the orrery this would fade out from under it.
            if (controls != null && controls.tour != null && controls.tour.Running) return;

            int index = System.Array.IndexOf(SolarSystemMRStage.BodyKeys, key);
            if (index < 0) return;

            var visual = stage.Rig.GetBodyVisual(key);
            if (visual == null) return;

            // Clone the AXIS node rather than the sphere: Saturn's rings hang
            // off the axis beside the surface, and a Saturn close-up without
            // the rings is the one body everybody came for, wrong.
            //
            // Except for the Sun, whose visual IS a direct child of the rig —
            // it has no axis node — so climbing to the parent there would clone
            // the entire solar system, orbits and all, into the visitor's face.
            var rigRoot = stage.Rig.transform;
            var source = visual.parent != null && visual.parent != rigRoot ? visual.parent : visual;
            var clone = Instantiate(source.gameObject);
            clone.name = "Focus — " + key;
            StripInteraction(clone.transform);

            focusRoot = clone.transform;
            focusRoot.SetParent(null, false);

            // Scale so the SPHERE — not the ring span — measures `diameter`.
            float sourceRadius = Mathf.Max(visual.lossyScale.x, 1e-5f);
            float ratio = (diameter * 0.5f) / sourceRadius;
            focusRoot.localScale = source.lossyScale * ratio;

            var eye = viewer != null ? viewer : Camera.main;
            if (eye != null)
            {
                Vector3 forward = eye.transform.forward;
                focusRoot.position = eye.transform.position + forward * distance;
                // Upright in the ROOM, not in the head: tilting the globe with
                // the viewer's gaze is the thing that makes a held object read
                // as a sticker on the visor.
                focusRoot.rotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(forward, Vector3.up).normalized, Vector3.up);
                RelightFor(focusRoot, eye);
            }

            stage.SetLabelsVisible(false);
            stage.ClearHighlight();
            SetPickersActive(false);
            if (controls != null) controls.SetMenuVisible(false);

            shownIndex = index;
            ShowCard(key, index);
            NarrationManager.Instance.Play("mw_sol_" + index);

            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeOrrery(false));
        }

        public void Release()
        {
            if (!Focused) return;

            NarrationManager.Instance.Stop();
            Destroy(focusRoot.gameObject);
            focusRoot = null;
            shownIndex = -1;
            HideCard();

            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeOrrery(true));
        }

        IEnumerator FadeOrrery(bool back)
        {
            var rig = stage != null ? stage.Rig : null;
            if (rig == null) yield break;

            // A scale fade, not an alpha one: the planet materials are opaque
            // and the orbit lines additive, so there is no single alpha to
            // drive. Shrinking the whole rig toward its own centre reads as
            // "put away" and costs one transform write a frame.
            float from = back ? 0f : 1f;
            float to = back ? 1f : 0f;
            Vector3 full = Vector3.one * stage.rigScale;

            if (back) rig.gameObject.SetActive(true);

            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(from, to, t / Mathf.Max(fadeSeconds, 1e-4f));
                rig.transform.localScale = full * Mathf.Max(u, 1e-4f);
                yield return null;
            }
            rig.transform.localScale = full * Mathf.Max(to, 1e-4f);

            if (!back) rig.gameObject.SetActive(false);
            else
            {
                rig.transform.localScale = full;
                stage.SetLabelsVisible(true);
                SetPickersActive(true);
                if (controls != null) controls.SetMenuVisible(true);
            }
            fade = null;
        }

        /// <summary>The clone must not be tappable, grabbable, or a physics
        /// body — it is a picture of a planet, not one.</summary>
        static void StripInteraction(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var i in root.GetComponentsInChildren<XRSimpleInteractable>(true)) Destroy(i);
            foreach (var g in root.GetComponentsInChildren<XRGrabInteractable>(true)) Destroy(g);
        }

        /// <summary>
        /// Move the shader's sun to suit the new position.
        ///
        /// PlanetSurface lights from a world-space _SunPos baked when the rig
        /// was built, out at the orrery's centre. A body lifted to 60 cm in
        /// front of the visitor's face is metres away from that point and comes
        /// out flatly back-lit or entirely in shadow. Put a stand-in sun up and
        /// to the viewer's left instead — the three-quarter key light every
        /// planetary photograph is printed with — and the terminator falls
        /// across the visible face where it shows the sphere off.
        /// </summary>
        void RelightFor(Transform root, Camera eye)
        {
            Vector3 keyLight = root.position
                               + eye.transform.right * -1.4f
                               + eye.transform.up * 0.9f
                               - eye.transform.forward * 0.8f;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                // .material, not .sharedMaterial: the clone must not write the
                // stand-in sun back into the orrery's own materials.
                foreach (var m in r.materials)
                    if (m != null && m.HasProperty(SunPosId)) m.SetVector(SunPosId, keyLight);
            }
        }

        static readonly int SunPosId = Shader.PropertyToID("_SunPos");

        // ---------------- the card -------------------------------------------

        void ShowCard(string key, int index)
        {
            EnsureCard();
            card.gameObject.SetActive(true);
            backButton.SetActive(true);
            RefreshCard(key, index);
        }

        void HideCard()
        {
            if (card != null) card.gameObject.SetActive(false);
            if (backButton != null) backButton.SetActive(false);
        }

        /// <summary>Re-read every string. Called on language change too — the
        /// card can be open across a language switch.</summary>
        public void RefreshCard()
        {
            if (shownIndex < 0) return;
            RefreshCard(SolarSystemMRStage.BodyKeys[shownIndex], shownIndex);
        }

        void RefreshCard(string key, int index)
        {
            if (cardTitle == null) return;
            cardTitle.text = SolarSystemMRStage.BodyName(key);
            cardFacts.text = Loc.T(SolarSystemTour.Facts[index], SolarSystemTour.FactsEn[index],
                                   SolarSystemTour.FactsJa[index], SolarSystemTour.FactsZh[index]);
            cardBody.text = Loc.T(SolarSystemTour.NarrationLines[index], SolarSystemTour.NarrationLinesEn[index],
                                  SolarSystemTour.NarrationLinesJa[index], SolarSystemTour.NarrationLinesZh[index]);
            if (backLabel != null)
                backLabel.text = Loc.T("돌아가기", "Back", "戻る", "返回");
        }

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(viewer != null ? viewer : Camera.main);

            // Below the body, not beside it. A side column reads naturally on a
            // flat screen, but MRWorldCanvas bends x into yaw: a 620-wide panel
            // in the left column sits its far edge 45 deg off-axis, so reading
            // it means turning away from the planet it describes. Height is
            // free — y is not bent — so the card goes under the globe, where
            // both are in one glance.
            card = BlackHoleUI.MakePanel(canvas.transform, "Planet Focus Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f),
                new Vector2(860f, BlackHoleUI.ReadingY(320f)));

            cardTitle = BlackHoleUI.MakeText(card, "Name", BlackHoleUI.ReadingSize(34), BlackHoleUI.TitleGold,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, BlackHoleUI.ReadingY(-20f)), new Vector2(804f, BlackHoleUI.ReadingY(46f)),
                FontStyle.Bold);

            cardFacts = BlackHoleUI.MakeText(card, "Facts", BlackHoleUI.ReadingSize(18), BlackHoleUI.Accent,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, BlackHoleUI.ReadingY(-70f)), new Vector2(804f, BlackHoleUI.ReadingY(52f)));
            cardFacts.horizontalOverflow = HorizontalWrapMode.Wrap;

            cardBody = BlackHoleUI.MakeText(card, "Blurb", BlackHoleUI.ReadingSize(20), BlackHoleUI.TextPrimary,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, BlackHoleUI.ReadingY(-128f)), new Vector2(804f, BlackHoleUI.ReadingY(176f)));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Its own top-level panel, not a child of the card: MRWorldCanvas
            // seats each top-level child on the cylinder independently, and a
            // button riding inside the card would be seated by the card's
            // centre and end up behind it.
            var btn = BlackHoleUI.MakeButton(canvas.transform, "Planet Focus Back",
                Loc.T("돌아가기", "Back", "戻る", "返回"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(224f, 84f), Release);
            backButton = btn.gameObject;
            backLabel = btn.GetComponentInChildren<Text>();
        }

        void OnDestroy()
        {
            if (focusRoot != null) Destroy(focusRoot.gameObject);
        }
    }
}
