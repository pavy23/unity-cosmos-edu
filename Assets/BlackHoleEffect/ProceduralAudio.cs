namespace BlackHoleEffect
{
    /// <summary>
    /// Whether procedural streaming <see cref="UnityEngine.AudioClip"/>s (built
    /// with a PCMReaderCallback) actually run on this platform. WebGL never
    /// fires the callback — it only logs "Creating streamed audio clips is not
    /// supported on this platform" and plays silence — so the exhibit skips its
    /// synthesized soundscapes and chirp there. Narration mp3s are ordinary
    /// clips and still play.
    /// </summary>
    public static class ProceduralAudio
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public const bool Supported = false;
#else
        public const bool Supported = true;
#endif
    }
}
