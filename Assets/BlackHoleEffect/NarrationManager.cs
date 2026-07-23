using System.Collections.Generic;
using UnityEngine;

namespace BlackHoleEffect
{
    /// <summary>
    /// Plays the pre-generated Korean narration clips (Resources/Narration/*)
    /// for the intro sequence and the guided tour, ducking the procedural
    /// soundscape while the voice is speaking. Created on demand — nothing
    /// needs to be wired in the scene.
    /// </summary>
    public class NarrationManager : MonoBehaviour
    {
        static NarrationManager instance;

        public static NarrationManager Instance
        {
            get
            {
                if (instance == null)
                {
                    // Plain runtime object — dies with play mode, no leaks.
                    var go = new GameObject("Narration");
                    instance = go.AddComponent<NarrationManager>();
                }
                return instance;
            }
        }

        AudioSource source;
        BlackHoleAudio ambience;
        float ambienceBaseVolume;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public bool IsSpeaking => source != null && source.isPlaying;

        void Awake()
        {
            instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.95f;
        }

        /// <summary>Plays Resources/Narration/{lang-folder}{key} and returns
        /// the clip length in seconds (0 when the clip is missing, so callers
        /// can fall back to their fixed timings).</summary>
        public float Play(string key)
        {
            string path = ClipPath(key);
            var clip = LoadClip(path);
            if (clip == null)
            {
                Debug.LogWarning($"Narration clip is missing: Resources/{path}");
                return 0f;
            }
            source.Stop();
            source.clip = clip;
            source.Play();
            return clip.length;
        }

        public void Stop()
        {
            if (source != null) source.Stop();
        }

        /// <summary>Loads and decompresses clips ahead of time. Resources.Load
        /// of a DecompressOnLoad mp3 stalls the main thread for a noticeable
        /// fraction of a frame — call this while the camera is still, so beats
        /// that fire mid-motion don't hitch.</summary>
        public void Preload(params string[] keys)
        {
            foreach (var key in keys)
            {
                var clip = LoadClip(ClipPath(key));
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                    clip.LoadAudioData();
            }
        }

        string ClipPath(string key) => "Narration/" + Loc.NarrationFolder + key;

        AudioClip LoadClip(string path)
        {
            if (clips.TryGetValue(path, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(path);
            if (clip != null) clips[path] = clip;
            return clip;
        }

        void Update()
        {
            if (ambience == null)
            {
                ambience = FindAnyObjectByType<BlackHoleAudio>();
                if (ambience != null) ambienceBaseVolume = ambience.volume;
                if (ambience == null) return;
            }
            float target = IsSpeaking ? ambienceBaseVolume * 0.35f : ambienceBaseVolume;
            ambience.volume = Mathf.MoveTowards(ambience.volume, target, Time.deltaTime * 0.4f);
        }
    }
}
