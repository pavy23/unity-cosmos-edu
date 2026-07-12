using UnityEngine;

namespace BlackHoleEffect
{
    /// <summary>
    /// Educational overlay: world-space labels with leader lines pointing at
    /// the physically meaningful features of the black hole image (shadow,
    /// photon ring, ISCO, Doppler beaming). Labels are placed in the plane
    /// through the hole perpendicular to the view direction, so they track the
    /// apparent (lensed) positions no matter where the camera orbits.
    /// Children are HideAndDontSave — nothing is written into the scene file.
    /// </summary>
    [ExecuteAlways]
    public class BlackHoleAnnotations : MonoBehaviour
    {
        public enum Difficulty { Elementary, Middle, High }

        public bool showLabels = true;
        [Tooltip("Label depth: 초등 / 중등 / 고등")]
        public Difficulty difficulty = Difficulty.Middle;
        [Tooltip("-1 = show all labels; 0..3 = highlight a single label (guided tour).")]
        public int focusIndex = -1;
        [Tooltip("Offset toward the camera in Rs so labels never z-fight the effect quad.")]
        public float depthBiasRs = 1.6f;
        public Color textColor = new Color(0.92f, 0.94f, 1f, 1f);
        public Color lineColor = new Color(1f, 1f, 1f, 0.5f);

        struct Entry
        {
            public string text;
            public Vector2 anchorDirRs;  // direction * radius in Rs, camera-facing plane
            public Vector2 labelDirRs;
            public TextMesh label;
            public LineRenderer line;
        }

        Entry[] entries;
        Material lineMat;
        Difficulty appliedDifficulty = Difficulty.Middle;
        int appliedLocVersion = -1;

        static readonly (Vector2 anchor, Vector2 label)[] Defs =
        {
            (new Vector2(0f, 2.6f),      new Vector2(0f, 5.2f)),
            (new Vector2(-1.9f, -1.9f),  new Vector2(-4.4f, -4.2f)),
            (new Vector2(4.0f, -0.2f),   new Vector2(6.2f, -3.6f)),
            // Doppler beaming: the approaching (bright) side is screen-RIGHT
            // (disk velocity +cross(ŷ, r̂) in the camera-facing basis), and
            // the old upper-left spot sat on top of the physics panel.
            (new Vector2(5.4f, 0.6f),    new Vector2(6.6f, 3.4f)),
        };

        // Per-difficulty label text (index-aligned with Defs).
        static readonly string[][] Texts =
        {
            new[] // 초등
            {
                "빛도 빠져나올 수 없는\n검은 구멍이에요",
                "블랙홀 둘레를 도는\n빛의 고리",
                "빨려 들어가는\n뜨거운 가스",
                "이쪽 가스가 더 빨리\n다가와서 더 밝아요",
            },
            new[] // 중등
            {
                "사건의 지평선 그림자\nEvent Horizon Shadow",
                "광자 고리 r = 1.5 Rs\nPhoton Ring",
                "강착원반 안쪽 가장자리 ISCO r = 3 Rs\nInner Stable Orbit",
                "도플러 비밍 — 접근하는 쪽이 더 밝음\nDoppler Beaming",
            },
            new[] // 고등
            {
                "사건의 지평선 그림자 — 겉보기 반지름 2.6 Rs\n(임계 충돌계수 b = 3√3·GM/c²)",
                "광자 구면 r = 1.5 Rs — 빛의 불안정 원궤도\nPhoton Sphere",
                "ISCO r = 3 Rs — 궤도 속도 0.5c\n안쪽에선 안정 궤도가 존재하지 않음",
                "상대론적 비밍 I ∝ δ³\n접근측 청색편이 · 후퇴측 적색편이",
            },
        };

        static readonly string[][] TextsEn =
        {
            new[] // Elementary
            {
                "A black hole —\nnot even light can escape",
                "A ring of light\ncircling the hole",
                "Hot gas being\npulled in",
                "This side moves toward us\nfaster, so it looks brighter",
            },
            new[] // Middle
            {
                "Event Horizon Shadow",
                "Photon Ring  r = 1.5 Rs",
                "Disk inner edge — ISCO  r = 3 Rs\nInnermost Stable Circular Orbit",
                "Doppler Beaming —\napproaching side is brighter",
            },
            new[] // High
            {
                "Event horizon shadow — apparent radius 2.6 Rs\n(critical impact parameter b = 3√3·GM/c²)",
                "Photon sphere r = 1.5 Rs —\nunstable circular orbit of light",
                "ISCO r = 3 Rs — orbital speed 0.5c\nno stable orbits inside",
                "Relativistic beaming I ∝ δ³\nblueshifted approaching · redshifted receding",
            },
        };

        static readonly string[][] TextsJa =
        {
            new[] // 初級
            {
                "光さえ逃げられない\n黒い穴です",
                "ブラックホールの周りを回る\n光のリング",
                "吸い込まれていく\n熱いガス",
                "こちら側のガスは速く\n近づくので明るく見えます",
            },
            new[] // 中級
            {
                "事象の地平面の影\nEvent Horizon Shadow",
                "光子リング r = 1.5 Rs\nPhoton Ring",
                "円盤の内縁 ISCO r = 3 Rs\nInnermost Stable Orbit",
                "ドップラービーミング —\n近づく側が明るい",
            },
            new[] // 上級
            {
                "事象の地平面の影 — 見かけ半径 2.6 Rs\n(臨界衝突パラメータ b = 3√3·GM/c²)",
                "光子球 r = 1.5 Rs —\n光の不安定な円軌道",
                "ISCO r = 3 Rs — 軌道速度 0.5c\n内側に安定軌道は存在しない",
                "相対論的ビーミング I ∝ δ³\n接近側は青方偏移・後退側は赤方偏移",
            },
        };

        static readonly string[][] TextsZh =
        {
            new[] // 初级
            {
                "连光都逃不出去的\n黑色洞口",
                "绕着黑洞转的\n光之环",
                "正被吸入的\n炽热气体",
                "这一侧的气体快速朝我们\n而来，所以更亮",
            },
            new[] // 中级
            {
                "事件视界的阴影\nEvent Horizon Shadow",
                "光子环 r = 1.5 Rs\nPhoton Ring",
                "吸积盘内缘 ISCO r = 3 Rs\n最内稳定圆轨道",
                "多普勒聚束 —\n靠近的一侧更亮",
            },
            new[] // 高级
            {
                "事件视界阴影 — 视半径 2.6 Rs\n(临界碰撞参数 b = 3√3·GM/c²)",
                "光子球 r = 1.5 Rs —\n光的不稳定圆轨道",
                "ISCO r = 3 Rs — 轨道速度 0.5c\n其内不存在稳定轨道",
                "相对论性聚束 I ∝ δ³\n靠近侧蓝移 · 远离侧红移",
            },
        };

        static string LabelText(Difficulty d, int i)
            => Loc.T(Texts[(int)d][i], TextsEn[(int)d][i], TextsJa[(int)d][i], TextsZh[(int)d][i]);

        void OnEnable() => BuildChildren();
        void OnDisable() => DestroyChildren();

        void BuildChildren()
        {
            DestroyChildren();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lineMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };

            entries = new Entry[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                var go = new GameObject("Annotation — " + i) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);

                var label = go.AddComponent<TextMesh>();
                label.font = font;
                label.fontSize = 64;
                label.characterSize = 0.045f;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.color = textColor;
                label.text = LabelText(difficulty, i);
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var lineGo = new GameObject("Leader") { hideFlags = HideFlags.HideAndDontSave };
                lineGo.transform.SetParent(go.transform, false);
                var line = lineGo.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.startWidth = line.endWidth = 1f; // actual width via widthMultiplier
                line.material = lineMat;
                line.startColor = line.endColor = lineColor;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.useWorldSpace = true;

                entries[i] = new Entry
                {
                    text = LabelText(difficulty, i),
                    anchorDirRs = Defs[i].anchor,
                    labelDirRs = Defs[i].label,
                    label = label,
                    line = line
                };
            }
        }

        void DestroyChildren()
        {
            if (entries == null) return;
            foreach (var e in entries)
                if (e.label != null) DestroyImmediate(e.label.gameObject);
            if (lineMat != null) DestroyImmediate(lineMat);
            entries = null;
        }

        void LateUpdate() => Reposition();

        /// <summary>Places labels/lines from the current camera; callable from
        /// editor tooling since edit-mode LateUpdate is not guaranteed to run
        /// before an on-demand render.</summary>
        public void Reposition()
        {
            if (entries == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            if (appliedDifficulty != difficulty || appliedLocVersion != Loc.Version)
            {
                appliedDifficulty = difficulty;
                appliedLocVersion = Loc.Version;
                for (int i = 0; i < entries.Length; i++)
                    if (entries[i].label != null)
                        entries[i].label.text = LabelText(difficulty, i);
            }

            bool visible = showLabels;
            float rs = transform.lossyScale.x;
            Vector3 center = transform.position;
            Vector3 toCam = (cam.transform.position - center).normalized;
            // Camera-facing basis (matches the billboard quad orientation).
            Vector3 upRef = Mathf.Abs(toCam.y) > 0.98f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(upRef, -toCam).normalized;
            Vector3 up = Vector3.Cross(-toCam, right);
            Vector3 bias = toCam * depthBiasRs * rs;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.label == null) continue;
                bool thisVisible = visible && (focusIndex < 0 || focusIndex == i);
                e.label.gameObject.SetActive(thisVisible);
                if (!thisVisible) continue;

                // During the guided tour (focus mode) the narration card owns
                // the bottom of the screen — mirror low labels above the
                // equator so label text never overlaps the card subtitles.
                Vector2 labelDir = e.labelDirRs;
                if (focusIndex >= 0 && labelDir.y < 0f) labelDir.y = -labelDir.y;

                Vector3 anchor = center + (right * e.anchorDirRs.x + up * e.anchorDirRs.y) * rs + bias;
                Vector3 labelPos = center + (right * labelDir.x + up * labelDir.y) * rs + bias;

                // Scale text/lines with the hole so labels work from room-scale
                // MR (Rs ~ 0.1 m) up to the showcase scene (Rs = 0.5 m).
                e.label.characterSize = 0.09f * rs;
                e.line.widthMultiplier = 0.03f * rs;
                e.label.transform.position = labelPos;
                e.label.transform.rotation = Quaternion.LookRotation(-toCam, up);
                e.line.SetPosition(0, anchor);
                e.line.SetPosition(1, labelPos + (anchor - labelPos).normalized * rs * 1.1f);
            }
        }
    }
}
