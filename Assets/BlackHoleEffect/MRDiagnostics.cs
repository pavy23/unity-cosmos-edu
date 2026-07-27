using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace BlackHoleEffect
{
    /// <summary>
    /// TEMPORARY — delete once the Quest Link bring-up is settled.
    ///
    /// Dumps everything that decides whether an MR scene is actually being
    /// driven by a headset: which display subsystems run, which input devices
    /// are registered (a simulated HMD hiding among them is the thing we are
    /// hunting), what drives the camera, and whether that camera moves at all
    /// between samples. Grep the editor log for [MRDIAG].
    /// </summary>
    static class MRDiagnostics
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var go = new GameObject("MR Diagnostics");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        class Runner : MonoBehaviour
        {
            // Sampling at fixed times was useless: Link took ~12 s to come up and
            // both samples landed before it. Anchor to the event instead — first
            // sample the moment a headset reports in, second one long enough
            // after that head motion between them is unmistakable.
            const float GiveUpAt = 45f;
            const float SecondSampleAfter = 12f;

            float t, upAt;
            bool xrUp, triedAdd;
            int done;
            Vector3 firstCamPos;
            Quaternion firstCamRot;

            void Update()
            {
                t += Time.unscaledDeltaTime;

                if (!xrUp)
                {
                    bool live = XRRuntime.HmdActive();
                    if (!live && t < GiveUpAt) return;
                    xrUp = true;
                    upAt = t;
                    Debug.Log(live
                        ? $"[MRDIAG] headset came up at t={t:F1}s"
                        : $"[MRDIAG] no headset after {GiveUpAt:F0}s — sampling anyway");
                    Dump(true);
                    done = 1;
                    return;
                }

                if (done == 1 && t - upAt >= SecondSampleAfter) { Dump(false); done = 2; }

                // The decisive trace: the raw device pose next to what the camera
                // ended up with, once a second. If the device pose moves and the
                // camera does not, the break is in the driver, not the runtime.
                if (t - upAt <= TraceFor && t - lastTrace >= 1f) { lastTrace = t; Trace(); }
            }

            const float TraceFor = 15f;
            float lastTrace;

            void Trace()
            {
                var sb = new StringBuilder($"[MRDIAG] trace t={t:F1}");

                var devices = new List<InputDevice>();
                InputDevices.GetDevices(devices);
                foreach (var dev in devices)
                {
                    if ((dev.characteristics & InputDeviceCharacteristics.HeadMounted) == 0) continue;
                    dev.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked);
                    dev.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState state);
                    dev.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 eyePos);
                    dev.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion eyeRot);
                    sb.Append($" | legacy tracked={tracked} state={state} eyePos={eyePos} eyeEuler={eyeRot.eulerAngles}");
                }

                // Every HMD the Input System knows about, not just the one it
                // would hand out — two of them fighting is exactly the shape of
                // bug we are looking for.
                int hmds = 0;
                foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
                {
                    if (!(d is UnityEngine.InputSystem.XR.XRHMD h)) continue;
                    hmds++;
                    sb.Append($" | IS '{h.name}' added={h.added} tracked={h.isTracked.ReadValue()}" +
                              $" state={h.trackingState.ReadValue()}" +
                              $" pos={h.centerEyePosition.ReadValue()}" +
                              $" euler={h.centerEyeRotation.ReadValue().eulerAngles}");
                }
                if (hmds == 0) sb.Append(" | InputSystem XRHMD: NONE");

                var origin = Object.FindAnyObjectByType<XROrigin>();
                var cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
                if (cam != null)
                    sb.Append($" | camLocal={cam.transform.localPosition}" +
                              $" camEuler={cam.transform.localRotation.eulerAngles}");
                if (origin != null && origin.CameraFloorOffsetObject != null)
                    sb.Append($" | offsetLocal={origin.CameraFloorOffsetObject.transform.localPosition}");

                Debug.Log(sb.ToString());
            }

            void Dump(bool first)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[MRDIAG] ===== sample {done + 1} at t={t:F1}s, scene=" +
                              $"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} =====");

                sb.AppendLine($"[MRDIAG] XRRuntime.HmdActive={XRRuntime.HmdActive()}  " +
                              $"XRSettings.enabled={XRSettings.enabled}  " +
                              $"isDeviceActive={XRSettings.isDeviceActive}  " +
                              $"loadedDeviceName='{XRSettings.loadedDeviceName}'");

                var displays = new List<XRDisplaySubsystem>();
                SubsystemManager.GetSubsystems(displays);
                sb.AppendLine($"[MRDIAG] display subsystems: {displays.Count}");
                foreach (var d in displays)
                    sb.AppendLine($"[MRDIAG]   display running={d.running} " +
                                  $"passthrough={d.displayOpaque == false}");

                var sim = Object.FindAnyObjectByType<XRInteractionSimulator>();
                sb.AppendLine($"[MRDIAG] XRInteractionSimulator present={sim != null}" +
                              (sim != null ? $" (name='{sim.gameObject.name}', enabled={sim.enabled})" : ""));

                var devices = new List<InputDevice>();
                InputDevices.GetDevices(devices);
                sb.AppendLine($"[MRDIAG] XR input devices: {devices.Count}");
                foreach (var dev in devices)
                    sb.AppendLine($"[MRDIAG]   '{dev.name}' valid={dev.isValid} chars={dev.characteristics}");

                // Everything the Input System knows about, full stop. The legacy
                // XR path clearly sees the headset; the question is whether the
                // bridge that turns those into Input System devices runs at all.
                var isDevices = UnityEngine.InputSystem.InputSystem.devices;
                sb.AppendLine($"[MRDIAG] InputSystem devices: {isDevices.Count} " +
                              $"(supportedDevices filter: {UnityEngine.InputSystem.InputSystem.settings.supportedDevices.Count})");
                foreach (var d in isDevices)
                    sb.AppendLine($"[MRDIAG]   '{d.name}' layout='{d.layout}' iface='{d.description.interfaceName}' " +
                                  $"product='{d.description.product}' added={d.added} enabled={d.enabled}");
                bool hmdLayout = UnityEngine.InputSystem.InputSystem.LoadLayout("XRHMD") != null;
                bool openxrHmdLayout = UnityEngine.InputSystem.InputSystem.LoadLayout("OpenXRHmd") != null;
                sb.AppendLine($"[MRDIAG] layouts registered: XRHMD={hmdLayout} OpenXRHmd={openxrHmdLayout}");

                // The discriminator. Devices the native backend reported but that
                // no layout would claim land here. If the OpenXR headset shows up
                // in this list, it IS being reported and only the layout match is
                // failing. If the list is empty, nothing is being reported at all
                // and the native XR-to-InputSystem bridge is the broken part.
                var unsupported = new List<UnityEngine.InputSystem.Layouts.InputDeviceDescription>();
                int n = UnityEngine.InputSystem.InputSystem.GetUnsupportedDevices(unsupported);
                sb.AppendLine($"[MRDIAG] unsupported/unmatched device descriptions: {n}");
                foreach (var d in unsupported)
                {
                    sb.AppendLine($"[MRDIAG]   iface='{d.interfaceName}' product='{d.product}' " +
                                  $"manufacturer='{d.manufacturer}' deviceClass='{d.deviceClass}'");
                    if (d.interfaceName == null || !d.interfaceName.StartsWith("XR")) continue;

                    // The XR one is the whole ballgame — say everything about it.
                    var caps = d.capabilities ?? "";
                    sb.AppendLine($"[MRDIAG]     capabilities({caps.Length} chars)=" +
                                  caps.Substring(0, System.Math.Min(500, caps.Length)));

                    string matched;
                    try { matched = UnityEngine.InputSystem.InputSystem.TryFindMatchingLayout(d) ?? "<none>"; }
                    catch (System.Exception e) { matched = $"<threw {e.GetType().Name}: {e.Message}>"; }
                    sb.AppendLine($"[MRDIAG]     TryFindMatchingLayout => {matched}");

                    // The whole question in one line: what does that layout inherit
                    // from? Anything not descending from XRHMD cannot satisfy an
                    // <XRHMD>/... binding, which is every rig in the project.
                    try
                    {
                        var lay = UnityEngine.InputSystem.InputSystem.LoadLayout(matched);
                        if (lay == null) sb.AppendLine("[MRDIAG]     layout failed to load");
                        else
                        {
                            var bases = string.Join(" <- ", lay.baseLayouts);
                            sb.AppendLine($"[MRDIAG]     baseLayouts: [{bases}]  " +
                                          $"(empty == not an XRHMD, bindings cannot resolve)");
                            var names = new List<string>();
                            foreach (var ctl in lay.controls)
                            {
                                names.Add(ctl.name.ToString());
                                if (names.Count >= 14) break;
                            }
                            sb.AppendLine($"[MRDIAG]     controls[{lay.controls.Count}]: {string.Join(", ", names)}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        sb.AppendLine($"[MRDIAG]     LoadLayout threw {e.GetType().Name}: {e.Message}");
                    }

                    // Only once, and only on the first sample: if this succeeds the
                    // camera should start tracking, which both proves the diagnosis
                    // and hands us a workaround.
                    if (!first || triedAdd) continue;
                    triedAdd = true;
                    try
                    {
                        var dev = UnityEngine.InputSystem.InputSystem.AddDevice(d);
                        sb.AppendLine($"[MRDIAG]     AddDevice => created '{dev.name}' layout='{dev.layout}'");
                    }
                    catch (System.Exception e)
                    {
                        sb.AppendLine($"[MRDIAG]     AddDevice threw {e.GetType().Name}: {e.Message}");
                    }
                }

                var origin = Object.FindAnyObjectByType<XROrigin>();
                if (origin == null) sb.AppendLine("[MRDIAG] XROrigin: NONE");
                else
                    sb.AppendLine($"[MRDIAG] XROrigin='{Path(origin.transform)}' " +
                                  $"requestedMode={origin.RequestedTrackingOriginMode} " +
                                  $"currentMode={origin.CurrentTrackingOriginMode} " +
                                  $"cameraYOffset={origin.CameraYOffset} " +
                                  $"camera={(origin.Camera != null ? Path(origin.Camera.transform) : "NULL")}");

                var cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
                if (cam == null) sb.AppendLine("[MRDIAG] camera: NONE");
                else
                {
                    sb.AppendLine($"[MRDIAG] camera='{Path(cam.transform)}' " +
                                  $"worldPos={cam.transform.position} " +
                                  $"localPos={cam.transform.localPosition} " +
                                  $"stereo={cam.stereoEnabled} targetEye={cam.stereoTargetEye}");
                    sb.Append("[MRDIAG] camera components:");
                    foreach (var c in cam.GetComponents<Component>())
                    {
                        if (c == null) { sb.Append(" <MISSING SCRIPT>"); continue; }
                        sb.Append(' ').Append(c.GetType().Name);
                        if (c is Behaviour b && !b.enabled) sb.Append("(DISABLED)");
                    }
                    sb.AppendLine();

                    var tpd = cam.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                    if (tpd == null) sb.AppendLine("[MRDIAG] TrackedPoseDriver: NONE");
                    else
                    {
                        sb.AppendLine($"[MRDIAG] TrackedPoseDriver enabled={tpd.enabled} " +
                                      $"trackingType={tpd.trackingType} updateType={tpd.updateType} " +
                                      $"ignoreTrackingState={tpd.ignoreTrackingState}");
                        Describe(sb, "position", tpd.positionInput);
                        Describe(sb, "rotation", tpd.rotationInput);
                        Describe(sb, "trackingState", tpd.trackingStateInput);
                    }

                    if (first) { firstCamPos = cam.transform.position; firstCamRot = cam.transform.rotation; }
                    else
                        sb.AppendLine($"[MRDIAG] camera moved since sample 1: " +
                                      $"{Vector3.Distance(firstCamPos, cam.transform.position):F4} m, " +
                                      $"{Quaternion.Angle(firstCamRot, cam.transform.rotation):F1} deg " +
                                      "(near zero while you were moving your head == not tracking)");
                }

                var rig = BlackHoleUI.WorldRig;
                sb.AppendLine($"[MRDIAG] BlackHoleUI.WorldSpace={BlackHoleUI.WorldSpace} WorldRig={(rig != null)}" +
                              (rig != null
                                  ? $" target={(rig.target != null ? Path(rig.target) : "NULL <- rides the viewer")}" +
                                    $" followViewer={rig.followViewer}"
                                  : ""));

                var hands = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(hands);
                sb.AppendLine($"[MRDIAG] hand subsystems: {hands.Count}");
                foreach (var h in hands)
                    sb.AppendLine($"[MRDIAG]   running={h.running} " +
                                  $"leftTracked={h.leftHand.isTracked} rightTracked={h.rightHand.isTracked}");

                Debug.Log(sb.ToString());
            }

            // An action that exists but is disabled, or is bound to nothing, is
            // the quiet way a pose driver ends up holding the camera at identity.
            static void Describe(StringBuilder sb, string label, UnityEngine.InputSystem.InputActionProperty prop)
            {
                var a = prop.action;
                if (a == null) { sb.AppendLine($"[MRDIAG]   {label}: action NULL (reference={prop.reference})"); return; }
                sb.AppendLine($"[MRDIAG]   {label}: '{a.name}' enabled={a.enabled} " +
                              $"bindings={a.bindings.Count} controls={a.controls.Count} " +
                              $"activeControl={(a.activeControl != null ? a.activeControl.path : "none")} " +
                              $"phase={a.phase}");
            }

            static string Path(Transform tr)
            {
                var sb = new StringBuilder(tr.name);
                for (var p = tr.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
                return sb.ToString();
            }
        }
    }
}
