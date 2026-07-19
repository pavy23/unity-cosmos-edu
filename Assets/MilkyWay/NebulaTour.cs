using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager

namespace MilkyWay
{
    /// <summary>
    /// "The Life of a Star" — a narrated tour that threads the seven specimens
    /// of the nebulae &amp; clusters showcase into one story: a gas cloud gives
    /// birth to stars, stars gather into clusters, and at the end of their lives
    /// they scatter their gas back — as a planetary nebula, or a supernova
    /// remnant — which becomes the raw material of the next generation. A great
    /// cycle. The camera work, one-at-a-time rendering and per-specimen sky are
    /// all handled by NebulaGallery; this layer adds the narration, the subtitle
    /// card, and the authored order.
    /// </summary>
    public class NebulaTour : MonoBehaviour
    {
        public NebulaController controller;
        public NebulaGallery gallery;

        public bool Running { get; private set; }
        int step;

        RectTransform card;
        Text cardTitle, cardBody, cardFooter;

        struct Step { public int specimen; public string title, titleEn, titleJa, titleZh; }

        // Specimen order = the lifecycle, not the gallery's index order.
        static readonly Step[] Steps =
        {
            new Step { specimen = 0, title = "1. 별의 요람 — 성운", titleEn = "1. A Cradle of Stars — the Nebula",
                titleJa = "1. 星のゆりかご — 星雲", titleZh = "1. 恒星的摇篮——星云" },
            new Step { specimen = 1, title = "2. 차가운 먼지 속의 잉태", titleEn = "2. Conceived in Cold Dust",
                titleJa = "2. 冷たい塵の中の宿り", titleZh = "2. 在寒冷尘埃中孕育" },
            new Step { specimen = 2, title = "3. 갓 태어난 별들 — 성단", titleEn = "3. Newborn Stars — a Cluster",
                titleJa = "3. 生まれたての星々 — 星団", titleZh = "3. 新生的恒星——星团" },
            new Step { specimen = 3, title = "4. 태양 같은 별의 죽음", titleEn = "4. The Death of a Sun-like Star",
                titleJa = "4. 太陽のような星の死", titleZh = "4. 类太阳恒星之死" },
            new Step { specimen = 4, title = "5. 무거운 별의 폭발", titleEn = "5. The Explosion of a Massive Star",
                titleJa = "5. 重い星の爆発", titleZh = "5. 大质量恒星的爆发" },
            new Step { specimen = 5, title = "6. 살아남은 자들, 그리고 순환", titleEn = "6. The Survivors, and the Cycle",
                titleJa = "6. 生き残りたち、そして循環", titleZh = "6. 幸存者，以及循环" },
        };

        // Narration clips (Resources/Narration/{,en,ja,zh}/neb_life_N) are
        // generated from these lines — subtitle == voice. Public for the tooling.
        public static readonly string[] NarrationLines =
        {
            "별의 일생을 따라가 봅시다. 모든 것은 여기, 성운에서 시작됩니다. 오리온 대성운은 가스와 먼지의 거대한 구름 — 별들의 요람입니다. 중심의 갓 태어난 뜨거운 별들이 주변 수소를 달궈 분홍빛으로 빛나게 합니다.",
            "성운은 빛나기만 하는 게 아닙니다. 말머리 성운은 짙고 차가운 먼지 구름이 뒤편 붉은 빛을 가려 만든 실루엣입니다. 바로 이런 차가운 먼지의 중심에서, 중력이 가스를 끌어모아 새로운 별이 잉태됩니다.",
            "별은 홀로 태어나지 않습니다. 같은 구름에서 수백, 수천 개가 함께 태어나 성단을 이룹니다. 플레이아데스의 젊고 푸른 별들은 태어난 지 겨우 1억 년 — 우주의 시간으로는 갓난아기입니다. 주변의 푸른 안개는 그 별빛을 되반사하는 먼지입니다.",
            "긴 삶의 끝, 태양 정도의 별은 조용히 죽습니다. 연료가 떨어진 별이 바깥층을 부드럽게 벗어던지면, 고리 성운처럼 아름다운 껍질이 부풀어 오릅니다. 가운데 남은 뜨거운 핵 — 백색왜성 — 이 그 가스를 달궈 빛나게 하죠. 이렇게 흩어진 가스는 다시 우주 공간으로 퍼져, 언젠가 새 별의 재료가 됩니다.",
            "훨씬 무거운 별은 조용히 죽지 않습니다. 폭발합니다. 게 성운은 1054년 여러 문명이 대낮에도 보았다고 기록한 초신성의 잔해입니다. 오렌지빛 필라멘트는 흩날린 별의 잔해, 푸른 중심은 초당 서른 번 도는 중성자별이 뿜는 바람입니다.",
            "그리고 여기, 살아남은 자들이 있습니다. 오메가 센타우리는 120억 년 전 은하가 갓 태어날 무렵 함께 태어난 늙은 별 수백만 개의 무리입니다. 폭발한 별이 남긴 가스는 다시 성운이 되고, 그 성운에서 또 새 별이 태어납니다. 별의 일생은 이렇게 하나의 큰 순환입니다.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "Let's follow the life of a star. It all begins here, in a nebula. The Orion Nebula is a vast cloud of gas and dust — a nursery of stars. At its heart, hot newborn stars heat the surrounding hydrogen until it glows pink.",
            "Nebulae don't only glow. The Horsehead is a silhouette — a dense, cold cloud of dust blocking the red light behind it. And it is deep inside cold dust like this that gravity gathers gas together and conceives a new star.",
            "Stars are not born alone. Hundreds or thousands form together from the same cloud, making a cluster. The young blue stars of the Pleiades are just a hundred million years old — newborns, on cosmic timescales. The blue haze around them is dust reflecting their starlight.",
            "At the end of a long life, a star like the Sun dies quietly. Out of fuel, it gently sheds its outer layers, and a beautiful shell swells outward — like the Ring Nebula. The hot core left behind — a white dwarf — lights the gas up. This scattered gas drifts back into space, one day to become the material of new stars.",
            "A far heavier star does not die quietly — it explodes. The Crab Nebula is the wreckage of a supernova that many civilizations recorded in 1054 as visible even in daylight. The orange filaments are the scattered remains of the star; the blue centre is the wind of a neutron star spinning thirty times a second.",
            "And here are the survivors. Omega Centauri is a swarm of millions of old stars, born together twelve billion years ago when the galaxy itself was young. The gas left by exploded stars becomes a nebula again, and from that nebula new stars are born. The life of a star is one great cycle.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "星の一生をたどってみましょう。すべてはここ、星雲から始まります。オリオン大星雲はガスと塵の巨大な雲 — 星のゆりかごです。中心の生まれたばかりの高温の星が、周りの水素を熱してピンクに輝かせます。",
            "星雲は輝くだけではありません。馬頭星雲は、濃く冷たい塵の雲が背後の赤い光を遮ってできた影です。まさにこうした冷たい塵の中心で、重力がガスを集め、新しい星が宿されるのです。",
            "星は独りでは生まれません。同じ雲から数百、数千個が一緒に生まれ、星団をつくります。プレアデスの若く青い星々は、生まれてまだ1億年 — 宇宙の時間では赤ん坊です。周りの青い霧は、その星の光を反射する塵です。",
            "長い一生の終わり、太陽ほどの星は静かに死にます。燃料が尽きた星が外層をそっと脱ぎ捨てると、環状星雲のような美しい殻が膨らみます。中心に残った高温の核 — 白色矮星 — がそのガスを照らします。こうして散らばったガスは再び宇宙空間へ広がり、いつか新しい星の材料になります。",
            "はるかに重い星は静かには死にません — 爆発します。かに星雲は、1054年に多くの文明が真昼でも見えたと記録した超新星の残骸です。オレンジ色のフィラメントは飛び散った星の残骸、青い中心は毎秒30回転する中性子星が放つ風です。",
            "そしてここに、生き残った者たちがいます。オメガ星団は120億年前、銀河が生まれたばかりの頃に一緒に生まれた老いた星、数百万個の群れです。爆発した星が残したガスは再び星雲となり、その星雲からまた新しい星が生まれます。星の一生は、こうしてひとつの大きな循環なのです。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "让我们追随一颗恒星的一生。一切始于这里——星云。猎户座大星云是气体与尘埃的巨大云团——恒星的摇篮。中心刚诞生的炽热恒星加热周围的氢气，使其发出粉红色的光。",
            "星云不只会发光。马头星云是一道剪影——浓密而寒冷的尘埃云挡住了身后的红光。正是在这样寒冷的尘埃深处，引力将气体聚集，孕育出新的恒星。",
            "恒星不会独自诞生。数百甚至数千颗从同一片云中一同形成，组成星团。昴星团中年轻的蓝色恒星只有一亿岁——以宇宙的尺度看还是婴儿。它们周围的蓝雾，是反射着星光的尘埃。",
            "在漫长一生的尽头，像太阳这样的恒星静静地死去。燃料耗尽后，它轻轻抛出外层，一层美丽的壳膨胀开来——就像环状星云。残留在中心的炽热核心——白矮星——点亮了这些气体。这些散逸的气体重新飘回太空，终有一天会成为新恒星的原料。",
            "更重的恒星不会静静死去——它会爆炸。蟹状星云是1054年多个文明记录到白昼可见的超新星遗骸。橙色的丝状结构是恒星飞散的残骸，蓝色的中心是每秒自转三十次的中子星所吹出的风。",
            "而这里，是幸存者。半人马座ω星团是数百万颗古老恒星的集群，诞生于一百二十亿年前、银河系尚且年轻之时。爆炸的恒星留下的气体重新化为星云，又从那片星云中诞生出新的恒星。恒星的一生，就是这样一个宏大的循环。",
        };

        public void StartTour()
        {
            if (Running || controller == null || gallery == null) return;
            Running = true;
            step = 0;
            gallery.SetTourActive(true);
            ApplyStep();
        }

        public void StopTour()
        {
            if (!Running) return;
            Running = false;
            NarrationManager.Instance.Stop();
            if (card != null) card.gameObject.SetActive(false);
            // Hand control back to the gallery at the specimen we ended on.
            gallery.SetTourActive(false);
            gallery.ShowSpecimen(Steps[step].specimen);
        }

        public void Toggle() { if (Running) StopTour(); else StartTour(); }
        public void Next() { if (Running && step < Steps.Length - 1) { step++; ApplyStep(); } }
        public void Prev() { if (Running && step > 0) { step--; ApplyStep(); } }
        public void OnLanguageChanged() { if (Running) ApplyStep(); }

        void ApplyStep()
        {
            var s = Steps[step];
            gallery.ShowSpecimen(s.specimen);   // camera + one-at-a-time + sky
            if (Application.isPlaying) NarrationManager.Instance.Play("neb_life_" + step);

            EnsureCard();
            card.gameObject.SetActive(true);
            cardTitle.text = Loc.T(s.title, s.titleEn, s.titleJa, s.titleZh);
            cardBody.text = Loc.T(NarrationLines[step], NarrationLinesEn[step],
                                  NarrationLinesJa[step], NarrationLinesZh[step]);
            cardFooter.text = Loc.T("→ 다음    ← 이전    Esc 종료",
                                    "→ Next    ← Prev    Esc End",
                                    "→ 次へ    ← 前へ    Esc 終了",
                                    "→ 下一步    ← 上一步    Esc 结束")
                            + "                        " + (step + 1) + " / " + Steps.Length;
        }

        void Update()
        {
            if (!Running) return;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) { StopTour(); return; }
                if (kb.rightArrowKey.wasPressedThisFrame || kb.nKey.wasPressedThisFrame) Next();
                if (kb.leftArrowKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame) Prev();
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape)) { StopTour(); return; }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.N)) Next();
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.B)) Prev();
#endif
        }

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>() ?? Camera.main);

            card = BlackHoleUI.MakePanel(canvas.transform, "Nebula Tour Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1120f, 232f));

            cardTitle = BlackHoleUI.MakeText(card, "Title", 28, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -18f), new Vector2(1060f, 38f), FontStyle.Bold);

            cardBody = BlackHoleUI.MakeText(card, "Body", 20, BlackHoleUI.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -62f), new Vector2(1060f, 130f));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            cardFooter = BlackHoleUI.MakeText(card, "Footer", 15, BlackHoleUI.TextSecondary, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 12f), new Vector2(1060f, 22f));

            // On-card Prev / Next, mirroring the gallery buttons.
            BlackHoleUI.MakeButton(card, "TourPrev", "◀",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-250f, 12f), new Vector2(60f, 34f), Prev);
            BlackHoleUI.MakeButton(card, "TourNext", "▶",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-180f, 12f), new Vector2(60f, 34f), Next);
            BlackHoleUI.MakeButton(card, "TourEnd", "✕",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-110f, 12f), new Vector2(60f, 34f), StopTour);
        }

        void OnDisable() { if (Running) StopTour(); }
    }
}
