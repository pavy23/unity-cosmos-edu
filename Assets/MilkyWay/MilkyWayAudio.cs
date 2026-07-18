using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// Procedural galactic soundscape — the black-hole exhibit's streaming-
    /// clip technique with an opposite temperament. Where the hole is HEAVY
    /// (B♭ minor, a sub-bass drone 57-octaves-up from Perseus), the galaxy
    /// is ETHEREAL:
    ///   * D Lydian — the raised-fourth mode that floats instead of
    ///     resolving; chords built to share tones so the crossfades hover.
    ///   * Everything an octave or two higher than the hole's register, with
    ///     a slow chorus wobble on the pad detune (glassy, not massive).
    ///   * Starlight glints: frequent, short pentatonic sparkles, each one
    ///     panned to its own random spot in the stereo field.
    ///   * A faint stardust air (soft high-passed noise) drifting slowly
    ///     left-right on an LFO instead of the hole's Doppler-sided hiss.
    /// No audio assets; M toggles mute (wired in MilkyWayControls).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MilkyWayAudio : MonoBehaviour
    {
        [Range(0f, 1f)] public float volume = 0.2f;
        public bool muted;

        const int SampleRate = 48000;
        const double Tau = 2.0 * System.Math.PI;

        // D Lydian world: Dmaj7, E/D (the Lydian chord), Bm7, Gmaj7 — no
        // dominant anywhere, so the progression circles without landing.
        static readonly double[][] Chords =
        {
            new[] { 73.42, 110.00, 146.83, 185.00, 277.18 }, // D2 A2 D3 F#3 C#4
            new[] { 82.41, 123.47, 164.81, 207.65, 246.94 }, // E2 B2 E3 G#3 B3
            new[] { 73.42, 123.47, 185.00, 246.94, 293.66 }, // D2 B2 F#3 B3 D4
            new[] { 98.00, 146.83, 196.00, 246.94, 369.99 }, // G2 D3 G3 B3 F#4
        };
        // High pentatonic glints: D5 E5 F#5 A5 B5 C#6.
        static readonly double[] GlintNotes = { 587.33, 659.25, 739.99, 880.00, 987.77, 1108.73 };
        const double ChordSeconds = 20.0;
        const double FadeSeconds = 9.0;

        AudioSource source;
        readonly double[] phaseA = new double[10];
        readonly double[] phaseB = new double[10];
        double dronePhase1, dronePhase2, glassPhase;
        double chorusLfo, panLfo;
        double chordTimer;
        int chordIndex;
        float airState;
        uint rng = 0x2545F491;

        // glint state (two overlapping voices so dense sparkles never cut off)
        readonly double[] glintPhase = new double[2];
        readonly double[] glintEnv = new double[2];
        readonly double[] glintFreq = new double[2];
        readonly float[] glintPan = new float[2];
        int glintVoice;
        double glintCountdown = 2.5;

        float NextRand()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / (float)0x1000000;
        }

        void Awake()
        {
            source = GetComponent<AudioSource>();
            // WebGL can't run the procedural callback — leave the source
            // clip-less there rather than warn and play silence.
            if (!BlackHoleEffect.ProceduralAudio.Supported) return;
            var clip = AudioClip.Create("GalacticSoundscape", SampleRate, 2, SampleRate, true, OnRead);
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
        }

        void Start()
        {
            if (Application.isPlaying && source.clip != null) source.Play();
        }

        void Update()
        {
            if (source != null) { source.mute = muted; source.volume = 1f; }
        }

        void OnRead(float[] data)
        {
            int frames = data.Length / 2;
            double dt = 1.0 / SampleRate;

            var cur = Chords[chordIndex];
            var nxt = Chords[(chordIndex + 1) % Chords.Length];

            for (int f = 0; f < frames; f++)
            {
                // ---- chord scheduling with equal-power crossfade ----
                chordTimer += dt;
                double fade = (chordTimer - (ChordSeconds - FadeSeconds)) / FadeSeconds;
                fade = fade < 0 ? 0 : (fade > 1 ? 1 : fade);
                float gainCur = Mathf.Cos((float)fade * Mathf.PI * 0.5f);
                float gainNxt = Mathf.Sin((float)fade * Mathf.PI * 0.5f);
                if (chordTimer >= ChordSeconds)
                {
                    chordTimer = 0;
                    chordIndex = (chordIndex + 1) % Chords.Length;
                    cur = Chords[chordIndex];
                    nxt = Chords[(chordIndex + 1) % Chords.Length];
                    for (int v = 0; v < 10; v++) { phaseA[v] = phaseB[v]; phaseB[v] = 0; }
                }

                // ---- pads with a slow chorus wobble on the detune ----
                chorusLfo += Tau * 0.11 * dt;
                double detune = 1.005 + 0.002 * System.Math.Sin(chorusLfo);
                float pad = 0f;
                for (int v = 0; v < 5; v++)
                {
                    phaseA[v * 2] += Tau * cur[v] * dt;
                    phaseA[v * 2 + 1] += Tau * cur[v] * detune * dt;
                    pad += gainCur * 0.5f * ((float)System.Math.Sin(phaseA[v * 2]) + (float)System.Math.Sin(phaseA[v * 2 + 1]));
                    phaseB[v * 2] += Tau * nxt[v] * dt;
                    phaseB[v * 2 + 1] += Tau * nxt[v] * detune * dt;
                    pad += gainNxt * 0.5f * ((float)System.Math.Sin(phaseB[v * 2]) + (float)System.Math.Sin(phaseB[v * 2 + 1]));
                }
                pad *= 0.085f;

                // ---- D drone, light-footed: fundamental + a glassy 3rd
                // harmonic instead of the hole's chest-weight octave ----
                dronePhase1 += Tau * 73.42 * dt;
                dronePhase2 += Tau * 146.83 * dt;
                glassPhase += Tau * 440.00 * dt; // A4, a floating fifth high above
                float breathe = 0.7f + 0.3f * (float)System.Math.Sin(chorusLfo * 0.5);
                float drone = (0.34f * (float)System.Math.Sin(dronePhase1)
                             + 0.14f * (float)System.Math.Sin(dronePhase2)
                             + 0.05f * (float)System.Math.Sin(glassPhase)) * breathe * 0.5f;

                // ---- stardust air: soft filtered noise, drifting L<->R ----
                float white = NextRand() * 2f - 1f;
                airState += 0.10f * (white - airState);
                panLfo += Tau * 0.023 * dt;
                float airPan = 0.5f + 0.4f * (float)System.Math.Sin(panLfo);
                float air = airState * 0.075f;

                // ---- starlight glints: frequent, short, individually panned --
                glintCountdown -= dt;
                if (glintCountdown <= 0)
                {
                    glintCountdown = 2.0 + NextRand() * 5.0;
                    glintVoice = 1 - glintVoice;
                    glintFreq[glintVoice] = GlintNotes[(int)(NextRand() * GlintNotes.Length) % GlintNotes.Length];
                    glintEnv[glintVoice] = 1.0;
                    glintPhase[glintVoice] = 0;
                    glintPan[glintVoice] = 0.2f + 0.6f * NextRand();
                }
                float glintL = 0f, glintR = 0f;
                for (int g = 0; g < 2; g++)
                {
                    if (glintEnv[g] <= 0.001) continue;
                    glintPhase[g] += Tau * glintFreq[g] * dt;
                    glintEnv[g] *= 1.0 - 1.5 * dt; // ~2.5 s decay, quicker than the hole's
                    float s = (float)(System.Math.Sin(glintPhase[g]) * glintEnv[g]) * 0.04f;
                    glintL += s * (1f - glintPan[g]) * 2f;
                    glintR += s * glintPan[g] * 2f;
                }

                float mono = (pad + drone) * volume;
                float airV = air * volume;
                data[f * 2] = mono + glintL * volume + airV * (1f - airPan) * 2f;
                data[f * 2 + 1] = mono + glintR * volume + airV * airPan * 2f;
            }
        }
    }
}
