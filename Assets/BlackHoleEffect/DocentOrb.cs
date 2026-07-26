using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace BlackHoleEffect
{
    /// <summary>
    /// The docent: a fist-sized glowing orb that carries the narration through
    /// every MR scene. It is the spatialisation of the voice — the sound comes
    /// from where the orb is, it flies to whatever it is talking about, and its
    /// caption is a one-to-two-line chip, never a panel.
    ///
    /// Contract with scenes: call <see cref="Spawn"/> once, then feed it beats
    /// (<see cref="Say"/> / <see cref="PointAt"/>). The orb owns its own motion:
    /// idle position is beside the view — 35° off forward, slightly below eye —
    /// never in the middle of it, and all travel is spring-damped and speed-
    /// capped so the eye can follow. Poke pauses and resumes the current line.
    ///
    /// Audio reuses the NarrationManager clip convention
    /// (Resources/Narration/{lang}/{key}) through its own spatial source —
    /// NarrationManager itself stays untouched for the desktop builds. A beat
    /// whose clip does not exist yet degrades to a timed chip, so scenes can be
    /// scripted before their TTS pass has run.
    /// </summary>
    public class DocentOrb : MonoBehaviour
    {
        public enum Mode { Idle, Point, Speak, Wait }

        public Mode CurrentMode { get; private set; } = Mode.Idle;
        public bool IsSpeaking => source != null && source.isPlaying && !paused;
        public Camera viewer;

        // Idle station, relative to the viewer. Off-axis and slightly low:
        // a companion at your shoulder, not a spot in front of your nose.
        const float IdleAngle = -35f;      // deg from forward, + is right
        const float IdleDistance = 1.1f;
        const float IdleDrop = 0.15f;

        const float SpringOmega = 4f;      // critically damped
        const float MaxSpeed = 2.5f;       // m/s — the eye must be able to follow
        const float OrbRadius = 0.045f;

        Color theme = new Color(1f, 0.8f, 0.5f);
        Transform ball;
        Material ballMat;
        Light glow;
        TextMesh chip;
        LineRenderer beam;
        AudioSource source;

        Vector3 want, velocity;
        Transform pointTarget;
        float chipUntil;
        bool paused;
        float bobPhase;

        public static DocentOrb Spawn(Camera viewer, Color theme)
        {
            var go = new GameObject("Docent Orb");
            var orb = go.AddComponent<DocentOrb>();
            orb.viewer = viewer != null ? viewer : Camera.main;
            orb.theme = theme;

            // Arrive from ahead rather than popping into existence at the
            // station — the flight is what introduces "this thing is alive".
            if (orb.viewer != null)
            {
                var fwd = orb.viewer.transform.forward; fwd.y = 0f;
                fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
                go.transform.position = orb.viewer.transform.position + fwd * 2.2f;
            }
            return orb;
        }

        void Awake()
        {
            // Body: a small unlit sphere. URP/Unlit survives every build via
            // the ShaderKeep convention.
            var prim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(prim.GetComponent<Collider>());
            prim.name = "Ball";
            ball = prim.transform;
            ball.SetParent(transform, false);
            ball.localScale = Vector3.one * (OrbRadius * 2f);
            ballMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ballMat.color = theme;
            prim.GetComponent<MeshRenderer>().sharedMaterial = ballMat;

            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.type = LightType.Point;
            glow.range = 0.6f;
            glow.intensity = 0.6f;
            glow.color = theme;

            var chipGo = new GameObject("Chip");
            chipGo.transform.SetParent(transform, false);
            chipGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            chip = chipGo.AddComponent<TextMesh>();
            chip.font = BlackHoleUI.UIFont;
            chip.GetComponent<MeshRenderer>().sharedMaterial = BlackHoleUI.UIFont.material;
            chip.fontSize = 56;
            chip.characterSize = 0.0028f;
            chip.anchor = TextAnchor.LowerCenter;
            chip.alignment = TextAlignment.Center;
            chip.color = new Color(0.95f, 0.96f, 1f, 1f);
            chip.text = "";

            beam = gameObject.AddComponent<LineRenderer>();
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.startWidth = beam.endWidth = 0.004f;   // world metres — the LineRenderer lesson
            beam.material = ballMat;
            beam.startColor = new Color(theme.r, theme.g, theme.b, 0.85f);
            beam.endColor = new Color(theme.r, theme.g, theme.b, 0f);
            beam.enabled = false;

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 0.4f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.volume = 0.95f;

            // Poke = pause/resume. A trigger collider keeps the orb from
            // physically shoving tracked hands around.
            var col = gameObject.AddComponent<SphereCollider>();
            col.radius = OrbRadius * 1.6f;
            col.isTrigger = true;
            var poke = gameObject.AddComponent<XRSimpleInteractable>();
            poke.selectEntered.AddListener(_ => TogglePause());
        }

        void TogglePause()
        {
            if (source == null || source.clip == null) return;
            paused = !paused;
            if (paused) { source.Pause(); ShowChip(Loc.T("일시정지 — 다시 톡", "Paused — poke to resume",
                                                        "一時停止 — もう一度タップ", "已暂停 — 再点一下"), 600f); }
            else { source.UnPause(); chipUntil = 0f; }
        }

        /// <summary>Fly to a target and hold a light beam on it.</summary>
        public void PointAt(Transform target)
        {
            pointTarget = target;
            CurrentMode = target != null ? Mode.Point : Mode.Idle;
            beam.enabled = target != null;
        }

        /// <summary>
        /// Speak one beat: spatial clip by NarrationManager key convention,
        /// chip alongside. Missing clip → chip-only with a reading-time hold,
        /// so beats authored before their TTS pass still play through.
        /// </summary>
        public Coroutine Say(string clipKey, string chipText) =>
            StartCoroutine(SayRoutine(clipKey, chipText));

        IEnumerator SayRoutine(string clipKey, string chipText)
        {
            var prev = CurrentMode;
            CurrentMode = Mode.Speak;

            float hold;
            AudioClip clip = clipKey == null ? null
                : Resources.Load<AudioClip>("Narration/" + Loc.NarrationFolder + clipKey);
            if (clip != null)
            {
                source.Stop();
                paused = false;
                source.clip = clip;
                source.Play();
                hold = clip.length;
            }
            else
            {
                // Reading-speed fallback: 1.2 s floor + ~17 chars/s.
                hold = 1.2f + (chipText != null ? chipText.Length : 0) * 0.06f;
            }

            if (!string.IsNullOrEmpty(chipText)) ShowChip(chipText, hold + 0.6f);

            float t = 0f;
            while (t < hold + 0.35f)
            {
                if (!paused) t += Time.deltaTime;
                yield return null;
            }
            CurrentMode = prev == Mode.Speak ? Mode.Idle : prev;
        }

        public void ShowChip(string text, float seconds)
        {
            chip.text = text ?? "";
            chipUntil = Time.time + seconds;
        }

        void LateUpdate()
        {
            if (viewer == null) { viewer = Camera.main; if (viewer == null) return; }
            var eye = viewer.transform;

            // Where the orb wants to be.
            if (CurrentMode == Mode.Point && pointTarget != null)
            {
                // Beside the target, biased toward the viewer so the beam and
                // the object are both in view.
                var toEye = (eye.position - pointTarget.position).normalized;
                want = pointTarget.position + toEye * 0.3f + Vector3.up * 0.06f;
            }
            else
            {
                var fwd = eye.forward; fwd.y = 0f;
                fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
                var side = Quaternion.AngleAxis(IdleAngle, Vector3.up) * fwd;
                want = eye.position + side * IdleDistance + Vector3.down * IdleDrop;
            }

            // Critically damped spring with a hard speed cap.
            float dt = Time.deltaTime;
            var to = want - transform.position;
            velocity += to * (SpringOmega * SpringOmega) * dt;
            velocity -= velocity * (2f * SpringOmega) * dt;
            if (velocity.magnitude > MaxSpeed) velocity = velocity.normalized * MaxSpeed;
            transform.position += velocity * dt;

            // Idle bob + speech pulse.
            bobPhase += dt;
            float bob = Mathf.Sin(bobPhase * 1.7f) * 0.008f;
            float pulse = IsSpeaking ? 1f + Mathf.Sin(bobPhase * 11f) * 0.12f : 1f;
            ball.localPosition = new Vector3(0f, bob, 0f);
            ball.localScale = Vector3.one * (OrbRadius * 2f * pulse);
            glow.intensity = IsSpeaking ? 0.9f + Mathf.Sin(bobPhase * 11f) * 0.25f : 0.6f;

            if (beam.enabled && pointTarget != null)
            {
                beam.SetPosition(0, transform.position);
                beam.SetPosition(1, pointTarget.position);
            }

            // Chip: billboard, timed.
            if (chip.text.Length > 0)
            {
                if (Time.time > chipUntil) chip.text = "";
                else chip.transform.rotation =
                    Quaternion.LookRotation(chip.transform.position - eye.position);
            }
        }
    }
}
