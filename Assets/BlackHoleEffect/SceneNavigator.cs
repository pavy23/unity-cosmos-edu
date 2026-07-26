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
    ///
    /// The corner is shared with the control bar, so this asks
    /// <see cref="BlackHoleUI.BottomClaimedAbove"/> what the bar took and stacks
    /// above it rather than assuming the room is free — it was not, on any
    /// screen, and on a phone it was not even close.
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
        const float HomeH = 30f, ChipH = 34f;

        // How much overlap with the control bar counts as none. The bar's panel
        // carries 18 px of horizontal padding plus a rounded corner, so a card
        // edge intruding that far hides nothing and needs no reflow. Above it,
        // the card starts covering buttons and the cluster stacks instead.
        const float OverlapSlack = 40f;

        readonly List<(Text label, System.Func<string> text)> localized = new();
        readonly List<GameObject> parts = new();
        int locVersion = -1;

        Dest[] dests;
        bool includeHome;
        bool vertical;

        // Phone: the cluster hides behind a corner chip (see Init).
        bool collapsible, expanded, visible = true;
        RectTransform cluster;    // the part the chip toggles
        GameObject chip;
        Text chipLabel;
        float baseY;              // cluster bottom, clear of whatever claimed the corner
        float clusterTop;         // top of the header line, set while building

        public void Init(Dest[] destinations, bool home = true, bool verticalLayout = false)
        {
            dests = destinations;
            includeHome = home;

            // A phone cannot afford a permanent switcher. The horizontal cluster
            // is 470 reference px beside an 896-wide control bar inside a
            // 1280-wide canvas: they overlapped by 300 px, which is exactly the
            // UI-printed-over-UI the phone screenshots showed. So collapse to a
            // corner chip that expands over a scrim, and stack the cards
            // vertically while open so they cover as little of the exhibit as
            // a phone screen allows.
            collapsible = BlackHoleUI.IsPhone;
            vertical = verticalLayout || collapsible;

            float right = BlackHoleUI.CanvasRefSize.x - Margin;
            float clusterW = vertical ? CardW : dests.Length * CardW + (dests.Length - 1) * Gap;

            if (collapsible)
            {
                float chipY = Margin + BlackHoleUI.BottomClaimedAbove(
                    right - CardW + OverlapSlack, right);
                baseY = chipY + ChipH + 8f;
                BuildCluster();
                BuildChip(chipY);
            }
            else
            {
                baseY = Margin + BlackHoleUI.BottomClaimedAbove(
                    right - clusterW + OverlapSlack, right);
                BuildCluster();
                // Claim the whole thing, header included, so anything that lays
                // out along the bottom after us — including the world-space
                // annotation labels — stays clear of it.
                BlackHoleUI.ClaimBottom(new Rect(
                    right - clusterW, baseY, clusterW, clusterTop - baseY));
            }
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
            visible = on;
            Apply();
        }

        void Apply()
        {
            if (collapsible)
            {
                if (chip != null) chip.SetActive(visible);
                if (cluster != null) cluster.gameObject.SetActive(visible && expanded);
                return;
            }
            parts.RemoveAll(p => p == null);
            foreach (var p in parts) p.SetActive(visible);
        }

        // × (U+00D7), not ✕ (U+2715): the Dingbats cross is absent from the
        // bundled Noto Sans KR and draws as an empty button on WebGL.
        string ChipText() => expanded
            ? Loc.T("×  닫기", "×  Close", "×  閉じる", "×  关闭")
            : Loc.T("전시 바꾸기  ▴", "Exhibits  ▴", "展示を選ぶ  ▴", "切换展区  ▴");

        void Toggle()
        {
            expanded = !expanded;
            Apply();
            if (chipLabel != null) chipLabel.text = ChipText();
        }

        /// <summary>The chip never hides with the sheet — it is the only way
        /// back once collapsed.</summary>
        void BuildChip(float chipY)
        {
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);
            var anchor = new Vector2(1f, 0f);
            var btn = BlackHoleUI.MakeButton(canvas.transform, "Nav Chip", "",
                anchor, anchor, new Vector2(-Margin, chipY), new Vector2(CardW, ChipH), Toggle);
            chip = btn.gameObject;
            chipLabel = btn.GetComponentInChildren<Text>();
            if (chipLabel != null)
            {
                chipLabel.fontSize = 14;
                localized.Add((chipLabel, ChipText));
                chipLabel.text = ChipText();
            }
            BlackHoleUI.ClaimBottom(new Rect(
                BlackHoleUI.CanvasRefSize.x - Margin - CardW, chipY, CardW, ChipH));
            Apply();
        }

        /// <summary>Full-canvas container so the cards keep their canvas-relative
        /// anchors while still toggling as one unit.</summary>
        void BuildCluster()
        {
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);
            var go = new GameObject("Nav Cluster", typeof(RectTransform))
                { hideFlags = HideFlags.DontSave };
            cluster = (RectTransform)go.transform;
            cluster.SetParent(canvas.transform, false);
            cluster.anchorMin = Vector2.zero;
            cluster.anchorMax = Vector2.one;
            cluster.offsetMin = cluster.offsetMax = Vector2.zero;

            if (vertical) BuildVertical(); else Build();

            // Open sheets dim what is behind them, so the cards read as a layer
            // rather than as more buttons, and a tap aimed at a card that misses
            // closes the sheet instead of hitting the control bar underneath.
            if (collapsible) BuildScrim();
        }

        void BuildScrim()
        {
            var go = new GameObject("Nav Scrim", typeof(RectTransform), typeof(Image), typeof(Button))
                { hideFlags = HideFlags.DontSave };
            var rt = (RectTransform)go.transform;
            rt.SetParent(cluster, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.01f, 0.015f, 0.03f, 0.72f);
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Toggle);
            rt.SetSiblingIndex(0);
        }

        void Build()
        {
            var anchor = new Vector2(1f, 0f);

            // Home chip along the bottom of the cluster.
            float y = baseY;
            if (includeHome)
            {
                float chipW = dests.Length * CardW + (dests.Length - 1) * Gap;
                var home = BlackHoleUI.MakeButton(cluster, "Nav Home", "",
                    anchor, anchor, new Vector2(-Margin, y), new Vector2(chipW, HomeH),
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
                y += HomeH + 8f;
            }

            // Thumbnails, right-aligned, laid left→right.
            float x = -Margin;
            for (int i = dests.Length - 1; i >= 0; i--)
            {
                BuildCard(cluster, dests[i], anchor, new Vector2(x, y));
                x -= CardW + Gap;
            }

            BuildHeader(anchor, y + CardH + 4f);
        }

        // Vertical variant: a column of thumbnails stacked in the BOTTOM-RIGHT
        // corner, growing upward. Used where a wide bottom panel (the nebula
        // gallery's fact card) would collide with a horizontal row but the corner
        // itself is free — and on phones, where a row never fits.
        void BuildVertical()
        {
            var anchor = new Vector2(1f, 0f);   // bottom-right
            int n = dests.Length;

            // Home chip at the foot of the column.
            float cardsBottom = baseY;
            if (includeHome)
            {
                var home = BlackHoleUI.MakeButton(cluster, "Nav Home", "",
                    anchor, anchor, new Vector2(-Margin, baseY), new Vector2(CardW, HomeH),
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
                cardsBottom = baseY + HomeH + Gap;
            }

            // Thumbnails stacked upward; dests[0] on top.
            for (int i = 0; i < n; i++)
            {
                float yBottom = cardsBottom + (n - 1 - i) * (CardH + Gap);
                BuildCard(cluster, dests[i], anchor, new Vector2(-Margin, yBottom));
            }

            BuildHeader(anchor, cardsBottom + (n - 1) * (CardH + Gap) + CardH + 4f);
        }

        void BuildHeader(Vector2 anchor, float y)
        {
            var header = BlackHoleUI.MakeText(cluster, "Nav Header", 13,
                BlackHoleUI.TextSecondary, TextAnchor.LowerRight, anchor, anchor,
                new Vector2(-Margin, y), new Vector2(360f, 20f));
            System.Func<string> headerT = () => Loc.T("다른 전시로", "Other exhibits", "他の展示へ", "其他展区");
            header.text = headerT();
            localized.Add((header, headerT));
            parts.Add(header.gameObject);
            clusterTop = y + 20f;
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
