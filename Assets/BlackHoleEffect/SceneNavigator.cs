using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// The "go to another exhibit" affordance, kept deliberately apart from the
    /// control toolbar: a small cluster of screenshot thumbnails in the
    /// bottom-right corner, one per sibling exhibit, plus a home chip back to
    /// the title. Same visual language as the title cards (a masked photo with
    /// the name over it) but sized down so it never competes with the main
    /// content. Clicking a thumbnail loads that scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneNavigator : MonoBehaviour
    {
        public struct Dest
        {
            public string scene;
            public System.Func<string> name;
            public string image;   // Resources path, e.g. "TitleCards/card_galaxy"
        }

        const float CardW = 150f, CardH = 88f, Gap = 10f, Margin = 22f;

        readonly List<(Text label, System.Func<string> text)> localized = new();
        readonly List<GameObject> parts = new();
        int locVersion = -1;

        Dest[] dests;
        bool includeHome;
        bool vertical;

        public void Init(Dest[] destinations, bool home = true, bool verticalLayout = false)
        {
            dests = destinations;
            includeHome = home;
            vertical = verticalLayout;
            if (vertical) BuildVertical(); else Build();
        }

        void Update()
        {
            if (locVersion == Loc.Version) return;
            locVersion = Loc.Version;
            foreach (var (label, text) in localized)
                if (label != null) label.text = text();
        }

        public void SetVisible(bool on)
        {
            parts.RemoveAll(p => p == null);
            foreach (var p in parts) p.SetActive(on);
        }

        void Build()
        {
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);
            var anchor = new Vector2(1f, 0f);

            // Home chip along the bottom of the cluster.
            float y = Margin;
            if (includeHome)
            {
                float chipW = dests.Length * CardW + (dests.Length - 1) * Gap;
                var home = BlackHoleUI.MakeButton(canvas.transform, "Nav Home", "",
                    anchor, anchor, new Vector2(-Margin, y), new Vector2(chipW, 30f),
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScreen"));
                var hlabel = home.GetComponentInChildren<Text>();
                if (hlabel != null)
                {
                    hlabel.fontSize = 14;
                    System.Func<string> ht = () => Loc.T("↩  처음으로", "↩  Home", "↩  タイトル", "↩  首页");
                    localized.Add((hlabel, ht));
                    hlabel.text = ht();
                }
                parts.Add(home.gameObject);
                y += 30f + 8f;
            }

            // Thumbnails, right-aligned, laid left→right.
            float x = -Margin;
            for (int i = dests.Length - 1; i >= 0; i--)
            {
                BuildCard(canvas.transform, dests[i], anchor, new Vector2(x, y));
                x -= CardW + Gap;
            }

            // Small header above the cluster.
            var header = BlackHoleUI.MakeText(canvas.transform, "Nav Header", 13,
                BlackHoleUI.TextSecondary, TextAnchor.LowerRight, anchor, anchor,
                new Vector2(-Margin, y + CardH + 4f), new Vector2(360f, 20f));
            System.Func<string> headerT = () => Loc.T("다른 전시로", "Other exhibits", "他の展示へ", "其他展区");
            header.text = headerT();
            localized.Add((header, headerT));
            parts.Add(header.gameObject);
        }

        // Vertical variant: a column of thumbnails stacked in the BOTTOM-RIGHT
        // corner, growing upward. Used where a wide bottom panel (the nebula
        // gallery's fact card) would collide with a horizontal row but the corner
        // itself is free.
        void BuildVertical()
        {
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);
            var anchor = new Vector2(1f, 0f);   // bottom-right
            int n = dests.Length;
            const float HomeH = 30f;

            // Home chip flush in the corner.
            float cardsBottom = Margin;
            if (includeHome)
            {
                var home = BlackHoleUI.MakeButton(canvas.transform, "Nav Home", "",
                    anchor, anchor, new Vector2(-Margin, Margin), new Vector2(CardW, HomeH),
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScreen"));
                var hlabel = home.GetComponentInChildren<Text>();
                if (hlabel != null)
                {
                    hlabel.fontSize = 14;
                    System.Func<string> ht = () => Loc.T("↩  처음으로", "↩  Home", "↩  タイトル", "↩  首页");
                    localized.Add((hlabel, ht));
                    hlabel.text = ht();
                }
                parts.Add(home.gameObject);
                cardsBottom = Margin + HomeH + Gap;
            }

            // Thumbnails stacked upward; dests[0] on top.
            for (int i = 0; i < n; i++)
            {
                float yBottom = cardsBottom + (n - 1 - i) * (CardH + Gap);
                BuildCard(canvas.transform, dests[i], anchor, new Vector2(-Margin, yBottom));
            }

            // Header above the top card.
            float topY = cardsBottom + (n - 1) * (CardH + Gap) + CardH + 4f;
            var header = BlackHoleUI.MakeText(canvas.transform, "Nav Header", 13,
                BlackHoleUI.TextSecondary, TextAnchor.LowerRight, anchor, anchor,
                new Vector2(-Margin, topY), new Vector2(360f, 20f));
            System.Func<string> headerT = () => Loc.T("다른 전시로", "Other exhibits", "他の展示へ", "其他展区");
            header.text = headerT();
            localized.Add((header, headerT));
            parts.Add(header.gameObject);
        }

        void BuildCard(Transform canvas, Dest dest, Vector2 anchor, Vector2 pos)
        {
            var card = BlackHoleUI.MakePanel(canvas, "Nav Card " + dest.scene,
                anchor, anchor, pos, new Vector2(CardW, CardH), accentLine: false);
            var cardImg = card.GetComponent<Image>();
            cardImg.raycastTarget = true;
            Graphic hoverTarget = cardImg;

            var sprite = Resources.Load<Sprite>(dest.image);
            if (sprite != null)
            {
                card.gameObject.AddComponent<Mask>().showMaskGraphic = true;

                var photo = new GameObject("Photo", typeof(RectTransform), typeof(Image));
                var pr = (RectTransform)photo.transform;
                pr.SetParent(card, false);
                pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
                float aspect = sprite.rect.width / sprite.rect.height;
                pr.sizeDelta = new Vector2(CardH * aspect, CardH); // cover by height, clip sides
                var pimg = photo.GetComponent<Image>();
                pimg.sprite = sprite;
                pimg.preserveAspect = true;
                pimg.raycastTarget = false;
                pr.SetSiblingIndex(0);

                // Bottom scrim so the name reads over any thumbnail.
                var strip = new GameObject("Strip", typeof(RectTransform), typeof(Image));
                var sr = (RectTransform)strip.transform;
                sr.SetParent(card, false);
                sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f);
                sr.pivot = new Vector2(0.5f, 0f);
                sr.sizeDelta = new Vector2(0f, 34f); sr.anchoredPosition = Vector2.zero;
                var simg = strip.GetComponent<Image>();
                simg.color = new Color(0.02f, 0.03f, 0.06f, 0.72f);
                simg.raycastTarget = false;
                sr.SetSiblingIndex(1);
                hoverTarget = simg;
            }

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = hoverTarget;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.4f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.9f);
            btn.colors = colors;
            string scene = dest.scene;
            btn.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene(scene));

            var name = BlackHoleUI.MakeText(card, "Name", 15, BlackHoleUI.TitleGold,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(CardW - 10f, 22f), FontStyle.Bold);
            name.text = dest.name();
            localized.Add((name, dest.name));

            parts.Add(card.gameObject);
        }
    }
}
