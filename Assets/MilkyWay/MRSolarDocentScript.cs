namespace MilkyWay
{
    /// <summary>
    /// The docent's script for the rebuilt MR solar exhibit ("light crossing
    /// your room" — Docs/MR-scene-designs.ko.md §2). Twelve beats: orrery play,
    /// the map's confession, the walk to Earth, and light crossing the room.
    ///
    /// Data only, and deliberately in the NarrationLines* shape so
    /// Tools/generate_mr_solar_narration.py can bake the audio the same way
    /// every other narration is baked (subtitle == voice stays the exhibit
    /// convention for the VOICE; the chips are the short form the MR design
    /// allows on screen).
    ///
    /// The baked clips land in Assets/MilkyWay/Audio/NarrationMR/ — NOT under
    /// Resources. Resources ships with every platform including the web build,
    /// and these clips are headset-only; the P2 solar scene will reference
    /// them directly, which keeps them out of every other player.
    /// </summary>
    public static class MRSolarDocentScript
    {
        public const string ClipPrefix = "mr_sol_";

        public static readonly string[] NarrationLines =
        {
            "우리 동네 태양계입니다. 행성을 집어서 귀에 대 보세요. 행성마다 자기만의 소리를 가지고 있어요.",
            "태양을 톡 치면 시간이 빨라집니다. 수성이 네 바퀴를 도는 동안, 해왕성은 거의 제자리인 걸 보세요.",
            "그런데 사실, 이 지도는 거짓말을 하고 있습니다. 크기도, 거리도요. 이제 진짜를 보여드릴게요.",
            "지금 이 방이 태양과 지구 사이, 1천문단위가 되었습니다. 태양은 저 벽의 작은 공이에요.",
            "이 축척에서 지구는 소금 알갱이만 합니다. 태양 반대쪽으로 걸어가서, 한번 찾아보세요.",
            "찾으셨네요. 저 0.3밀리미터 알갱이 위에 인류 전부가 살고 있습니다. 그리고 바로 옆 1센티미터, 저것이 달 — 인류가 가 본 가장 먼 곳입니다.",
            "그리고 그 사이를 보세요. 아무것도 없습니다. 태양계는 사실, 거의 완벽하게 텅 빈 공간이에요.",
            "이제 우주에서 가장 빠른 것을 배웅해 봅시다. 방금, 태양에서 빛이 출발했습니다.",
            "이 축척에서 빛은 1초에 8밀리미터를 갑니다. 옆에서 나란히 걸어 보세요. 당신이 훨씬 빠릅니다.",
            "지구 도착. 실제로는 8분 19초가 걸렸습니다. 당신이 맞는 아침 햇살은, 언제나 8분 전의 태양입니다.",
            "해왕성까지는 어떨까요? 이 벽 너머로 백 미터 — 빛으로도 4시간 10분이 걸립니다.",
            "빛이 느린 게 아닙니다. 우주가 넓은 것입니다. 오늘 가져가실 한 문장은, 이것이면 충분해요.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "This is our neighbourhood — the solar system. Pick up a planet and hold it to your ear. Every planet has a sound of its own.",
            "Tap the Sun and time speeds up. Watch Mercury lap four times while Neptune barely moves.",
            "But the truth is, this map has been lying to you — about size, and about distance. Let me show you the real thing.",
            "This room has just become one astronomical unit — the distance from the Sun to the Earth. The Sun is that little ball on the wall.",
            "At this scale, the Earth is the size of a grain of salt. Walk to the far side from the Sun and try to find it.",
            "There it is. All of humanity lives on that grain, a third of a millimetre across. And one centimetre beside it, the Moon — the farthest any human has ever travelled.",
            "Now look at everything in between. Nothing. The solar system is, in truth, almost perfectly empty space.",
            "Now let's see off the fastest thing in the universe. A ray of light has just left the Sun.",
            "At this scale, light travels eight millimetres per second. Walk alongside it — you are much faster.",
            "Arrival at Earth. In reality, that took eight minutes and nineteen seconds. The morning sunlight on your face is always the Sun of eight minutes ago.",
            "And Neptune? A hundred metres beyond this wall — four hours and ten minutes, even at the speed of light.",
            "Light is not slow. Space is vast. If you take one sentence home today, let it be that one.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "私たちのご近所、太陽系です。惑星をつかんで耳に当ててみてください。惑星にはそれぞれの音があります。",
            "太陽をタップすると時間が速くなります。水星が4周する間、海王星はほとんど動きません。",
            "でも実は、この地図は嘘をついています。大きさも、距離もです。本当の姿をお見せしましょう。",
            "今、この部屋が太陽と地球の間、1天文単位になりました。太陽はあの壁の小さなボールです。",
            "この縮尺では、地球は塩の粒ほどです。太陽の反対側へ歩いて、探してみてください。",
            "見つけましたね。あの0.3ミリの粒の上に、人類のすべてが住んでいます。そしてすぐ隣の1センチ、あれが月 — 人類が行った最も遠い場所です。",
            "そして、その間を見てください。何もありません。太陽系は実は、ほとんど完全に空っぽの空間なのです。",
            "では、宇宙で最も速いものを見送りましょう。たった今、太陽から光が出発しました。",
            "この縮尺では、光は1秒に8ミリ進みます。並んで歩いてみてください。あなたのほうがずっと速いです。",
            "地球に到着。実際には8分19秒かかりました。あなたが浴びる朝の日差しは、いつも8分前の太陽です。",
            "海王星まではどうでしょう。この壁の向こう100メートル — 光でも4時間10分かかります。",
            "光が遅いのではありません。宇宙が広いのです。今日持ち帰る一文は、これで十分です。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "这是我们的家园——太阳系。拿起一颗行星，放到耳边听听。每颗行星都有自己的声音。",
            "点一下太阳，时间就会加速。看，水星转了四圈，海王星几乎没动。",
            "但说实话，这张地图一直在撒谎——大小和距离都是。现在让我给你看真实的样子。",
            "现在，这个房间变成了一个天文单位——太阳到地球的距离。太阳就是墙上那个小球。",
            "在这个比例下，地球只有一粒盐那么大。走到太阳对面，找找看吧。",
            "找到了。全人类都住在那粒0.3毫米的颗粒上。旁边一厘米处是月球——人类到过的最远的地方。",
            "再看看它们之间。什么都没有。太阳系其实几乎是完全空旷的空间。",
            "现在，让我们送一送宇宙中最快的东西。刚才，一道光从太阳出发了。",
            "在这个比例下，光每秒只走八毫米。和它并肩走走看——你比它快多了。",
            "到达地球。实际上，这用了8分19秒。你沐浴的晨光，永远是8分钟前的太阳。",
            "那海王星呢？在这面墙外一百米——即使是光，也要走4小时10分钟。",
            "不是光太慢，而是宇宙太大。今天要带走的一句话，这一句就够了。",
        };

        // The on-screen short form (Docs/MR-scene-designs.ko.md §0.2: chips are
        // 2 lines x ~18 chars; the voice carries the full sentence). Kept next
        // to the voice lines so the two cannot drift apart unseen.
        public static readonly string[] ChipLines =
        {
            "행성을 귀에 대 보세요",
            "태양을 톡 — 시간 가속",
            "이 지도는 거짓말입니다",
            "이 방 = 1 AU",
            "지구를 찾아보세요",
            "지구 0.3 mm — 달 1 cm",
            "태양계 = 거의 빈 공간",
            "빛, 출발",
            "빛: 초속 8 mm",
            "실제 8분 19초",
            "해왕성: 빛으로 4시간",
            "우주가 넓은 것입니다",
        };

        public static readonly string[] ChipLinesEn =
        {
            "Hold a planet\nto your ear",
            "Tap the Sun —\ntime speeds up",
            "This map lies",
            "This room = 1 AU",
            "Find the Earth",
            "Earth 0.3 mm —\nMoon 1 cm away",
            "Almost empty space",
            "Light departs",
            "Light: 8 mm per second",
            "8 min 19 s, for real",
            "Neptune: 4 h by light",
            "Space is vast",
        };

        public static readonly string[] ChipLinesJa =
        {
            "惑星を耳に当ててみて",
            "太陽をタップ — 時間加速",
            "この地図は嘘",
            "この部屋 = 1 AU",
            "地球を探して",
            "地球 0.3 mm — 月 1 cm",
            "ほぼ空っぽの空間",
            "光、出発",
            "光:秒速 8 mm",
            "実際は8分19秒",
            "海王星:光で4時間",
            "宇宙が広いのです",
        };

        public static readonly string[] ChipLinesZh =
        {
            "把行星放到耳边",
            "点太阳 — 时间加速",
            "这张地图在撒谎",
            "这个房间 = 1 AU",
            "找找地球",
            "地球0.3毫米 — 月球1厘米",
            "几乎全是虚空",
            "光出发了",
            "光:每秒8毫米",
            "实际8分19秒",
            "海王星:光行4小时",
            "是宇宙太大",
        };
    }
}
