using UnityEngine;

namespace BlackHoleEffect
{
    /// <summary>
    /// Guided tour: steps through the physics of the scene one concept at a
    /// time — highlighting the matching label, triggering the matching demo,
    /// and showing narration text at the bottom of the view.
    /// G = start/stop, N/→ = next, B/← = previous (wired in DesktopControls).
    /// </summary>
    public class GuidedTour : MonoBehaviour
    {
        public BlackHoleAnnotations annotations;
        public BlackHolePhysicsPanel panel;
        public EinsteinRingDemo einsteinDemo;
        public PhotonLauncher launcher;
        public SpaghettificationDemo spaghetti;
        public RelativisticJets jets;
        public ObservationComparison comparison;

        public bool Running { get; private set; }

        /// <summary>Current step index — the theory panel mirrors it.</summary>
        public int CurrentStep => step;

        bool prevPanelShow, prevComparisonShow;
        int step;
        RectTransform card;
        UnityEngine.UI.Text cardTitle, cardBody, cardFooter;

        struct Step
        {
            public string title, titleEn, titleJa, titleZh;
            // Optional on-screen interaction hint (not spoken — the card body
            // itself is exactly the narration transcript).
            public string hint, hintEn, hintJa, hintZh;
            public int focus;                 // annotation index to highlight, -1 = all
            public System.Action<GuidedTour> enter;
        }

        // The narration clips (Resources/Narration/tour_N and Narration/en/
        // tour_N) are generated from these lines — spoken versions of the card
        // bodies without the on-screen key hints. Public so tooling can
        // regenerate the audio.
        public static readonly string[] NarrationLines =
        {
            "블랙홀 여행에 오신 것을 환영합니다. 지금 보고 있는 모든 것은 아인슈타인의 일반상대성이론으로 계산된 것입니다. 빛이 실제로 휘어지는 경로를 따라가며, 하나씩 살펴봅시다.",
            "가운데 검은 원은 빛조차 탈출할 수 없는 영역입니다. 실제 그림자는 사건의 지평선보다 2.6배 크게 보입니다. 중력이 주변의 빛까지 삼키기 때문입니다.",
            "그림자 가장자리의 밝은 테두리는, 빛이 블랙홀 주위를 몇 바퀴나 돌다가 간신히 빠져나온 것입니다. 이곳에서 빛은 슈바르츠실트 반지름의 1.5배 되는 곳에서 원 궤도를 돕니다.",
            "빛나는 원반은 블랙홀로 빨려 들어가는 뜨거운 가스입니다. 원반 위아래로 보이는 고리는, 블랙홀 뒤쪽 원반의 빛이 휘어져 보이는 중력 렌즈 효과입니다.",
            "원반의 한쪽이 더 밝은 이유는, 가스가 광속의 절반으로 회전하고 있어서, 다가오는 쪽의 빛이 상대론적으로 증폭되기 때문입니다.",
            "왼쪽 위의 두 시계를 보세요. 관찰자는 블랙홀에서 멀리 떨어진 안전한 곳의 우리이고, 탐사선은 블랙홀 바로 옆까지 내려간 무인 탐사선입니다. 블랙홀 가까이에서는 시간이 느리게 흘러서, 밖에서 한 시간이 지나는 동안 탐사선의 시계는 24분밖에 가지 않습니다.",
            "블랙홀 뒤의 별 하나가 좌우로 움직이고 있습니다. 별이 정확히 뒤에 올 때, 빛이 사방으로 휘어져 완전한 고리가 됩니다. 이것이 아인슈타인 링입니다.",
            "광자들을 발사했습니다. 블랙홀에 가까이 지나갈수록 궤적이 크게 휘고, 임계 거리 안쪽의 광자는 붉은색으로 표시되며 포획됩니다. 빛의 궤도는 언제나 블랙홀 중심을 지나는 하나의 평면 안에 있습니다.",
            "떨어지는 작은 별을 보세요. 머리와 발에 걸리는 중력의 차이, 즉 조석력 때문에 길게 늘어나며 국수처럼 찢어집니다. 과학자들은 이것을 스파게티화라고 부릅니다.",
            "일부 블랙홀은 삼킨 물질의 일부를 자기장으로 감아 올려, 양극으로 뿜어냅니다. 이 제트는 거의 광속으로 수천 광년을 날아갑니다.",
            "이 모든 현상이 단 하나의 방정식, 측지선 방정식에서 나옵니다. 이제 자유롭게 탐험해 보세요.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "Welcome to the black hole tour. Everything you see here is computed from Einstein's general theory of relativity. Let's follow the paths that light actually bends along, one step at a time.",
            "The dark circle in the center is the region from which not even light can escape. The shadow looks 2.6 times larger than the event horizon itself, because gravity swallows the light passing nearby as well.",
            "The bright rim at the edge of the shadow is light that circled the black hole several times before barely escaping. Here, light orbits in a circle at one and a half times the Schwarzschild radius.",
            "The glowing disk is hot gas spiraling into the black hole. The rings above and below it are gravitational lensing — light from the far side of the disk, bent over and under the hole.",
            "One side of the disk looks brighter because the gas rotates at half the speed of light, and the light from the approaching side is relativistically amplified.",
            "Look at the two clocks in the upper left. The observer is us, watching from a safe distance. The probe is an unmanned spacecraft sent right next to the black hole. Time flows more slowly near the black hole — while one hour passes outside, the probe's clock ticks only twenty-four minutes.",
            "A single star is moving back and forth behind the black hole. When it lines up exactly behind, its light bends around in every direction and becomes a complete ring. This is an Einstein ring.",
            "Photons away! The closer a photon passes to the black hole, the more its path bends. Photons inside the critical distance are shown in red and captured. A light ray's orbit always stays within a single plane through the center of the black hole.",
            "Watch the little falling star. The difference in gravity between its near and far side — the tidal force — stretches it out like a noodle. Scientists call this spaghettification.",
            "Some black holes wind part of the matter they swallow into magnetic fields and blast it out from their poles. These jets fly for thousands of light-years at nearly the speed of light.",
            "All of these phenomena come from a single equation — the geodesic equation. Now, explore freely.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "ブラックホールツアーへようこそ。いまご覧のすべては、アインシュタインの一般相対性理論で計算されたものです。光が実際に曲がる道筋をたどりながら、ひとつずつ見ていきましょう。",
            "中央の黒い円は、光さえ脱出できない領域です。実際の影は事象の地平面より2.6倍大きく見えます。重力が周囲の光まで呑み込むからです。",
            "影のふちの明るいリングは、光がブラックホールの周りを何周も回ってから、ようやく抜け出したものです。ここで光はシュヴァルツシルト半径の1.5倍の円軌道を回ります。",
            "輝く円盤は、ブラックホールに吸い込まれていく高温のガスです。円盤の上下に見えるリングは、後ろ側の円盤の光が曲げられて見える重力レンズ効果です。",
            "円盤の片側が明るいのは、ガスが光速の半分で回転していて、近づいてくる側の光が相対論的に増幅されるからです。",
            "左上のふたつの時計を見てください。観測者は、ブラックホールから遠く離れた安全な場所にいる私たちです。探査機は、ブラックホールのすぐそばまで降りた無人機です。ブラックホールの近くでは時間がゆっくり流れ、外で1時間たつ間に、探査機の時計は24分しか進みません。",
            "ブラックホールの後ろで、ひとつの星が左右に動いています。星がちょうど真後ろに来るとき、光が四方に曲げられて完全なリングになります。これがアインシュタインリングです。",
            "光子を発射しました。ブラックホールの近くを通るほど軌跡は大きく曲がり、臨界距離の内側の光子は赤色で示され、捕獲されます。光の軌道はつねに、ブラックホールの中心を通るひとつの平面の中にあります。",
            "落ちていく小さな星を見てください。頭と足にかかる重力の差、つまり潮汐力によって、麺のように引き伸ばされて裂けていきます。科学者はこれをスパゲッティ化と呼びます。",
            "一部のブラックホールは、呑み込んだ物質の一部を磁場で巻き上げ、両極から噴き出します。このジェットはほぼ光速で、数千光年を飛んでいきます。",
            "これらすべての現象は、たったひとつの方程式、測地線方程式から生まれます。さあ、自由に探検してみてください。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "欢迎来到黑洞之旅。您现在看到的一切，都是根据爱因斯坦的广义相对论计算出来的。让我们沿着光真实弯曲的路径，一步步来探索。",
            "中央的黑色圆区，是连光都无法逃脱的区域。实际看到的阴影比事件视界本身大2.6倍，因为引力连从旁边经过的光也一并吞噬。",
            "阴影边缘明亮的光环，是绕着黑洞转了好几圈才勉强逃出的光。在这里，光沿着1.5倍史瓦西半径的圆轨道运行。",
            "发光的圆盘是正被吸入黑洞的炽热气体。圆盘上下方的光环，是黑洞后方圆盘的光被弯曲后呈现的引力透镜效应。",
            "圆盘一侧显得更亮，是因为气体以一半光速旋转，朝我们而来一侧的光被相对论性地增强了。",
            "请看左上角的两个时钟。观察者是我们，在远离黑洞的安全之处；探测器是下降到黑洞近旁的无人飞船。黑洞附近时间流逝更慢——外面过去一小时，探测器的时钟只走二十四分钟。",
            "黑洞后方有一颗星正在左右移动。当它恰好来到正后方时，光被向四面八方弯曲，形成完整的圆环。这就是爱因斯坦环。",
            "光子已发射！光子经过黑洞越近，轨迹弯曲得越厉害；临界距离以内的光子显示为红色，被捕获。光的轨道永远位于一个通过黑洞中心的平面之内。",
            "请看那颗坠落的小星。头脚之间的引力之差——潮汐力——把它像面条一样拉长撕裂。科学家称之为“面条化”。",
            "有些黑洞会把吞下物质的一部分用磁场卷起，从两极喷出。这些喷流以接近光速的速度，飞行数千光年。",
            "所有这些现象，都源自同一个方程——测地线方程。现在，自由探索吧。",
        };

        static readonly Step[] Steps =
        {
            new Step { focus = -1,
                title = "블랙홀 여행에 오신 것을 환영합니다", titleEn = "Welcome to the Black Hole Tour",
                titleJa = "ブラックホールツアーへようこそ", titleZh = "欢迎来到黑洞之旅" },
            new Step { focus = 0,
                title = "1. 사건의 지평선 그림자", titleEn = "1. The Event Horizon Shadow",
                titleJa = "1. 事象の地平面の影", titleZh = "1. 事件视界的阴影" },
            new Step { focus = 1,
                title = "2. 광자 고리", titleEn = "2. The Photon Ring",
                titleJa = "2. 光子リング", titleZh = "2. 光子环" },
            new Step { focus = 2,
                title = "3. 강착원반과 ISCO", titleEn = "3. Accretion Disk & ISCO",
                titleJa = "3. 降着円盤とISCO", titleZh = "3. 吸积盘与ISCO" },
            new Step { focus = 3,
                title = "4. 도플러 비밍", titleEn = "4. Doppler Beaming",
                titleJa = "4. ドップラービーミング", titleZh = "4. 多普勒聚束",
                hint = "1 키로 원반 색을 바꿔보세요", hintEn = "Press 1 to change disk colors",
                hintJa = "1キーで円盤の色を変えられます", hintZh = "按1键可切换吸积盘颜色" },
            new Step { focus = -1,
                title = "5. 중력 시간 지연", titleEn = "5. Gravitational Time Dilation",
                titleJa = "5. 重力による時間の遅れ", titleZh = "5. 引力时间膨胀",
                enter = t => { if (t.panel != null) { t.panel.show = true; t.panel.probeDistanceRs = 1.2f; t.panel.RefreshText(); } } },
            new Step { focus = -1,
                title = "6. 아인슈타인 링", titleEn = "6. The Einstein Ring",
                titleJa = "6. アインシュタインリング", titleZh = "6. 爱因斯坦环",
                hint = "A/D로 직접 움직여보세요", hintEn = "Move it yourself with A/D",
                hintJa = "A/Dキーで自分で動かせます", hintZh = "用A/D键亲自移动它",
                enter = t => { if (t.einsteinDemo != null) { t.einsteinDemo.active = true; t.einsteinDemo.autoSweep = true; } } },
            new Step { focus = -1,
                title = "7. 빛의 궤적", titleEn = "7. Light Trajectories",
                titleJa = "7. 光の軌跡", titleZh = "7. 光的轨迹",
                hint = "Space: 발사 / 지우기", hintEn = "Space: fire / clear",
                hintJa = "Space: 発射 / 消去", hintZh = "空格键：发射 / 清除",
                enter = t => { if (t.launcher != null) t.launcher.FireSweep(); } },
            new Step { focus = -1,
                title = "8. 스파게티화", titleEn = "8. Spaghettification",
                titleJa = "8. スパゲッティ化", titleZh = "8. 面条化",
                enter = t => { if (t.spaghetti != null) t.spaghetti.active = true; } },
            new Step { focus = -1,
                title = "9. 상대론적 제트", titleEn = "9. Relativistic Jets",
                titleJa = "9. 相対論的ジェット", titleZh = "9. 相对论性喷流",
                enter = t => { if (t.jets != null) t.jets.active = true; } },
            new Step { focus = -1,
                title = "여행 끝!", titleEn = "Tour Complete!",
                titleJa = "ツアー終了！", titleZh = "旅程结束！",
                enter = t => { if (t.annotations != null) { t.annotations.showLabels = true; t.annotations.focusIndex = -1; } } },
        };

        public void StartTour()
        {
            Running = true;
            step = 0;
            // Start from a completely clean stage: every overlay disappears,
            // then each step brings in only what it is talking about.
            if (panel != null) { prevPanelShow = panel.show; }
            if (comparison != null) { prevComparisonShow = comparison.show; }
            EnsureNarration();
            ApplyStep();
        }

        public void StopTour()
        {
            Running = false;
            NarrationManager.Instance.Stop();
            if (card != null) card.gameObject.SetActive(false);
            ResetDemos();
            if (annotations != null) { annotations.focusIndex = -1; annotations.showLabels = true; }
            if (panel != null) { panel.show = prevPanelShow; panel.RefreshText(); }
            if (comparison != null) { comparison.show = prevComparisonShow; comparison.Refresh(); }
        }

        public void Next() { if (Running && step < Steps.Length - 1) { step++; ApplyStep(); } }
        public void Prev() { if (Running && step > 0) { step--; ApplyStep(); } }

        void ApplyStep()
        {
            ResetDemos();
            var s = Steps[step];
            // Clean stage each step; the step's own content appears alone.
            if (panel != null) { panel.show = false; panel.RefreshText(); }
            if (comparison != null) { comparison.show = false; comparison.Refresh(); }
            if (annotations != null)
            {
                annotations.showLabels = s.focus >= 0;
                annotations.focusIndex = s.focus;
            }
            s.enter?.Invoke(this);
            if (panel != null) panel.RefreshText();
            if (Application.isPlaying) NarrationManager.Instance.Play("tour_" + step);
            EnsureNarration();
            card.gameObject.SetActive(true);
            cardTitle.text = Loc.T(s.title, s.titleEn, s.titleJa, s.titleZh);

            // The card body IS the narration transcript (subtitle = voice);
            // interaction hints are a separate, unspoken gray line.
            string body = Loc.T(NarrationLines[step], NarrationLinesEn[step],
                                NarrationLinesJa[step], NarrationLinesZh[step]);
            string hint = Loc.T(s.hint, s.hintEn, s.hintJa, s.hintZh);
            cardBody.text = body + (string.IsNullOrEmpty(hint)
                ? "" : "\n<color=#9AA3B5>(" + hint + ")</color>");

            cardFooter.text = Loc.T("N 다음    B 이전    G 종료    X 수식",
                                    "N Next    B Prev    G End    X Math",
                                    "N 次へ    B 前へ    G 終了    X 数式",
                                    "N 下一步    B 上一步    G 结束    X 公式")
                            + "                                  " + (step + 1) + " / " + Steps.Length;
        }

        /// <summary>Re-applies the current step after a language toggle so the
        /// card text and narration voice switch immediately.</summary>
        public void OnLanguageChanged()
        {
            if (Running) ApplyStep();
        }

        void ResetDemos()
        {
            if (einsteinDemo != null) einsteinDemo.active = false;
            if (spaghetti != null) spaghetti.active = false;
            if (jets != null) jets.active = false;
            // Photon trails linger otherwise — each step starts on a clean stage.
            if (launcher != null) launcher.ClearTrails();
        }

        void EnsureNarration()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());

            // Tall enough for the full narration transcript (up to 4 wrapped
            // lines) plus the hint line and footer.
            card = BlackHoleUI.MakePanel(canvas.transform, "Tour Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(920f, 218f));

            cardTitle = BlackHoleUI.MakeText(card, "Title", 26, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(860f, 34f), FontStyle.Bold);

            cardBody = BlackHoleUI.MakeText(card, "Body", 20, BlackHoleUI.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -58f), new Vector2(860f, 118f));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            cardFooter = BlackHoleUI.MakeText(card, "Footer", 15, BlackHoleUI.TextSecondary, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 12f), new Vector2(860f, 22f));
        }
    }
}
