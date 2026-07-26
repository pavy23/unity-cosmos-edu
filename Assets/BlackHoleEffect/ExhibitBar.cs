using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// Builds the desktop control bar as one contained panel with the actions
    /// grouped by kind: each category is a labelled row (a gold caption on the
    /// left, its buttons to the right), so a visitor reads the controls as a
    /// short list of themes rather than a wall of buttons. Scene navigation is
    /// deliberately NOT here — it lives in <see cref="SceneNavigator"/>.
    /// </summary>
    public static class ExhibitBar
    {
        public struct Group
        {
            public System.Func<string> label;
            public (System.Func<string> text, UnityEngine.Events.UnityAction act)[] items;
        }

        // BtnH/RowPitch were 33/39. A landscape phone shows this bar at about
        // 0.64 CSS px per authored px, which made a 33-high button 21 px tall
        // — half the 44 px touch minimum. 40 gets it to ~26 without pushing a
        // four-row bar past a third of a 412-tall viewport; a true 44 px
        // target needs a different (paged) bar on phones.
        const float LabelW = 78f, BtnW = 124f, BtnH = 40f, Gap = 6f, RowPitch = 46f;
        const float PadX = 18f, PadTop = 16f, PadBottom = 12f;
        const float BottomY = 18f, EdgeMargin = 22f;

        public static (GameObject panel, List<(Text label, System.Func<string> text)> localized)
            Build(Transform canvas, Group[] groups)
        {
            var localized = new List<(Text, System.Func<string>)>();

            // Wrap rather than overflow. The bar used to size itself to its widest
            // group unconditionally: six buttons is 774 px, which fits inside the
            // 1920 desktop frame and does not fit the 1280 phone one. Chunk each
            // group into as many lines as the canvas actually has room for, so the
            // bar can never be wider than the screen — however long a translation
            // runs, however many buttons a scene adds later.
            var canvasSize = BlackHoleUI.CanvasRefSize;
            float roomForItems = canvasSize.x - 2f * EdgeMargin - 2f * PadX - LabelW - 8f;
            int perLine = Mathf.Max(1, Mathf.FloorToInt((roomForItems + Gap) / (BtnW + Gap)));

            int lines = 0;
            float maxRow = 0f;
            foreach (var g in groups)
            {
                int onLine = Mathf.Min(g.items.Length, perLine);
                float w = onLine * BtnW + (onLine - 1) * Gap;
                if (w > maxRow) maxRow = w;
                lines += (g.items.Length + perLine - 1) / perLine;
            }
            float pw = PadX + LabelW + 8f + maxRow + PadX;
            float ph = PadTop + lines * RowPitch + PadBottom;

            var panelRt = BlackHoleUI.MakePanel(canvas, "Control Bar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BottomY),
                new Vector2(pw, ph));
            var panel = panelRt.gameObject;
            var tl = new Vector2(0f, 1f);

            int line = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                float rowY = -(PadTop + line * RowPitch);

                // Caption sits on the group's first line; wrapped lines are
                // indented under it with no repeat, so the grouping still reads.
                var cap = BlackHoleUI.MakeText(panelRt, "Cat " + i, 14, BlackHoleUI.TitleGold,
                    TextAnchor.MiddleLeft, tl, tl, new Vector2(PadX, rowY),
                    new Vector2(LabelW, BtnH), FontStyle.Bold);
                cap.text = g.label();
                localized.Add((cap, g.label));

                float x = PadX + LabelW + 8f;
                for (int k = 0; k < g.items.Length; k++)
                {
                    if (k > 0 && k % perLine == 0)
                    {
                        line++;
                        rowY = -(PadTop + line * RowPitch);
                        x = PadX + LabelW + 8f;
                    }
                    var (text, act) = g.items[k];
                    var btn = BlackHoleUI.MakeButton(panelRt, "Btn " + text(), text(),
                        tl, tl, new Vector2(x, rowY), new Vector2(BtnW, BtnH), act);
                    var label = btn.GetComponentInChildren<Text>();
                    if (label != null) { label.fontSize = 14; localized.Add((label, text)); }
                    x += BtnW + Gap;
                }
                line++;
            }

            // Tell the rest of the UI what we took, so the exhibit switcher and
            // the world-space annotation labels can lay out around the bar
            // instead of printing through it.
            BlackHoleUI.ClaimBottom(new Rect(
                (canvasSize.x - pw) * 0.5f, BottomY, pw, ph));

            return (panel, localized);
        }
    }
}
