using UnityEngine;
using UnityEngine.XR;

namespace BlackHoleEffect
{
    /// <summary>
    /// The number the whole optimisation plan hangs on, visible inside the
    /// headset: frame time and missed-frame rate, plus what MRPerformance
    /// actually applied. Development builds only (MRPerformance gates it), so
    /// release players carry none of this.
    ///
    /// Deliberately dumb: a TextMesh, no canvas, no XRStats. Frame-time EMA
    /// from unscaled delta is enough to tell "within budget" from "not", and
    /// the deep numbers (GPU stage timings) belong to OVR Metrics / RenderDoc,
    /// not to an in-app HUD.
    /// </summary>
    public class MRMetricsHUD : MonoBehaviour
    {
        const float Distance = 1.15f;      // m ahead of the eye
        const float DropAngle = 24f;       // deg below gaze — read on purpose,
                                           // never in the way
        const float Smoothing = 6f;        // follow lerp speed

        TextMesh text;
        float emaMs = 1000f / 72f;
        int frames, missed;
        float refresh;

        void Start()
        {
            var go = new GameObject("HUD Text");
            go.transform.SetParent(transform, false);
            text = go.AddComponent<TextMesh>();
            text.font = BlackHoleUI.UIFont;
            text.GetComponent<MeshRenderer>().sharedMaterial = BlackHoleUI.UIFont.material;
            text.fontSize = 48;
            text.characterSize = 0.0032f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;

            DontDestroyOnLoad(gameObject);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || text == null) return;

            float dt = Time.unscaledDeltaTime;
            emaMs = Mathf.Lerp(emaMs, dt * 1000f, 0.06f);

            refresh = XRDevice.refreshRate > 1f ? XRDevice.refreshRate : MRPerformance.TargetHz;
            frames++;
            if (dt > 1.5f / refresh) missed++;

            // 2 Hz refresh over a rolling window keeps the text readable.
            if (frames >= (int)(refresh * 0.5f))
            {
                float missPct = 100f * missed / frames;
                text.text =
                    $"{refresh:0} Hz   {emaMs:0.0} ms   miss {missPct:0.0}%\n" +
                    $"fov {(MRPerformance.FoveationSet ? "on" : "off")}   " +
                    $"72req {(MRPerformance.RefreshRateSet ? "ok" : "no")}   " +
                    $"caps {MRPerformance.CappedRenderers}";
                text.color = missPct < 2f ? new Color(0.5f, 1f, 0.55f)
                           : missPct < 8f ? new Color(1f, 0.86f, 0.4f)
                                          : new Color(1f, 0.45f, 0.4f);
                frames = 0;
                missed = 0;
            }

            // Yaw-follow below the gaze; pitch comes from the fixed drop so
            // reading it is a deliberate glance down, not an occlusion.
            var fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();
            var want = cam.transform.position
                     + Quaternion.AngleAxis(DropAngle, cam.transform.right) * fwd * Distance;
            transform.position = Vector3.Lerp(transform.position, want,
                                              1f - Mathf.Exp(-Smoothing * dt));
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
