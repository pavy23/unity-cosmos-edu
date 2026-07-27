using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace BlackHoleEffect
{
    /// <summary>
    /// One question, asked the same way everywhere: is this frame being drawn
    /// into a headset right now?
    ///
    /// The tempting shortcuts are both wrong here. <c>Application.isMobilePlatform</c>
    /// is true on Quest — it is an Android device — so every "phone" branch in
    /// the project would otherwise fire on the HMD. And an <c>XROrigin</c> in
    /// the scene only says the scene was *authored* for MR (that is what
    /// <see cref="BlackHoleUI.WorldSpace"/> asks); a desktop player opening an
    /// MR scene has one and no display.
    ///
    /// A *running* display subsystem is the honest signal, and it is what the
    /// title screen already used to decide whether to hand off to MRTitle.
    /// The editor's XR simulator never trips it: the simulator fakes input
    /// devices, not a display.
    /// </summary>
    public static class XRRuntime
    {
        static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        /// <summary>True while a headset display is actually presenting.</summary>
        public static bool HmdActive()
        {
            SubsystemManager.GetSubsystems(Displays);
            foreach (var d in Displays)
                if (d.running) return true;
            return false;
        }

        /// <summary>
        /// True as soon as a headset display exists at all, running or not — the
        /// earlier, coarser half of the same question.
        ///
        /// <see cref="HmdActive"/> is the honest answer to "am I being drawn into
        /// a headset", but it is late: measured over Quest Link the display sits
        /// at running=false for the first ~12 s of play mode. Anything that has
        /// to commit *before* the headset finishes booting — and get it right the
        /// first time, because the decision cannot be walked back — has to ask
        /// this one instead.
        ///
        /// It can afford to be asked at frame zero. XR Management runs
        /// InitializeLoaderSync at AfterAssembliesLoaded and StartSubsystems at
        /// BeforeSplashScreen, both strictly before AfterSceneLoad, and the
        /// display subsystem is created during that init. So by the time any
        /// RuntimeInitializeOnLoadMethod of ours runs, an empty list means the
        /// loader failed or was never configured: no headset is coming.
        /// </summary>
        public static bool HmdPresent()
        {
            SubsystemManager.GetSubsystems(Displays);
            return Displays.Count > 0;
        }
    }
}
