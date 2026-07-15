using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// Persistent language selector: a small button in the top-right corner
    /// showing the current language; clicking it drops down the four
    /// options. Always available (K still cycles), hidden while a cinematic
    /// owns that corner with its skip/stop button.
    /// </summary>
    public static class LanguageSelect
    {
        static Button mainButton;
        static Text mainLabel;
        static RectTransform dropdown;

        /// <summary>
        /// Hand rays need a bigger target than a mouse does. At the desktop's
        /// 150x40 this widget measured 1.5° tall in the headset — under the ~3°
        /// a ray can reliably hold once hand jitter is counted — so the whole
        /// thing scales up in MR. Desktop is unchanged (factor 1).
        /// </summary>
        static float S => BlackHoleUI.WorldSpace ? 2.5f : 1f;

        static readonly (Loc.Lang lang, string label)[] Options =
        {
            (Loc.Lang.Korean,   "한국어"),
            (Loc.Lang.English,  "English"),
            (Loc.Lang.Japanese, "日本語"),
            (Loc.Lang.Chinese,  "中文"),
        };

        /// <summary>Where the widget hangs. Desktop: the top-right corner. MR: top
        /// centre — scaled up for hand rays it no longer fits that corner beside
        /// the theory panel, and a permanent control has no business at 41° off
        /// axis anyway.</summary>
        static Vector2 Anchor => BlackHoleUI.WorldSpace ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
        static Vector2 Offset => BlackHoleUI.WorldSpace ? new Vector2(0f, -26f) : new Vector2(-26f, -26f);

        public static void CreateWidget()
        {
            if (mainButton != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);
            mainButton = BlackHoleUI.MakeButton(canvas.transform, "Language Button", "",
                Anchor, Anchor, Offset, new Vector2(150f * S, 40f * S),
                ToggleDropdown);
            mainLabel = mainButton.GetComponentInChildren<Text>();
            UpdateLabel();
            Loc.Changed -= UpdateLabel;   // statics can survive play sessions
            Loc.Changed += UpdateLabel;
        }

        /// <summary>Cinematics reuse this corner for their skip/stop button.</summary>
        public static void SetVisible(bool on)
        {
            if (mainButton != null) mainButton.gameObject.SetActive(on);
            if (!on) CloseDropdown();
        }

        static void UpdateLabel()
        {
            if (mainLabel != null) mainLabel.text = Loc.DisplayName + "  ▾";
        }

        static void ToggleDropdown()
        {
            if (dropdown != null && dropdown.gameObject.activeSelf) { CloseDropdown(); return; }
            if (dropdown == null) BuildDropdown();
            dropdown.gameObject.SetActive(true);
        }

        static void CloseDropdown()
        {
            if (dropdown != null) dropdown.gameObject.SetActive(false);
        }

        static void BuildDropdown()
        {
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);
            dropdown = BlackHoleUI.MakePanel(canvas.transform, "Language Dropdown",
                Anchor, Anchor, new Vector2(Offset.x, -72f * S),
                new Vector2(150f * S, (4f + Options.Length * 46f) * S),
                accentLine: false);
            for (int i = 0; i < Options.Length; i++)
            {
                var lang = Options[i].lang;
                BlackHoleUI.MakeButton(dropdown, "Lang " + Options[i].label, Options[i].label,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, (-4f - i * 46f) * S),
                    new Vector2(138f * S, 42f * S),
                    () =>
                    {
                        Loc.SetLanguage(lang);
                        var controls = Object.FindAnyObjectByType<DesktopControls>();
                        if (controls != null) controls.RefreshLanguage();
                        CloseDropdown();
                    });
            }
        }
    }
}
