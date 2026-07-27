using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace BlackHoleEffect
{
    /// <summary>
    /// MR gesture toy: open your left palm and a miniature black hole
    /// (Rs = 2 cm) materializes floating above it; make a fist and it winks
    /// out. Uses XR Hands joint data; degrades gracefully without tracking.
    ///
    /// Off until asked for. An open left hand is not a gesture, it is the
    /// resting state of a hand — visitors point, steady themselves, and reach
    /// for the menu with it, and a black hole popping out of the palm every
    /// time is startling the first time and in the way after that. So the
    /// summon is opt-in from the MR menu, and a session that never touches it
    /// never sees it.
    /// </summary>
    public class PalmMiniBlackHole : MonoBehaviour
    {
        public Material holeMaterial;
        public XROrigin xrOrigin;
        [Tooltip("Average fingertip-to-palm distance (m) above which the hand counts as open.")]
        public float openThreshold = 0.085f;

        [Tooltip("Off by default — an open palm is a resting hand, not a request. " +
                 "The MR menu's 손바닥 블랙홀 button arms it.")]
        public bool armed;

        /// <summary>Whether the gesture is live. Setting it false retires the
        /// mini immediately rather than leaving it hanging until the next
        /// closed fist.</summary>
        public bool Armed
        {
            get => armed;
            set
            {
                armed = value;
                if (!armed) Retire();
            }
        }

        /// <summary>Flip the gesture on or off; returns the new state so a menu
        /// can report it.</summary>
        public bool ToggleArmed() => Armed = !armed;

        const string MiniName = "Palm Mini Black Hole";

        XRHandSubsystem hands;
        GameObject mini;
        float visibility; // smoothed 0..1

        void Awake() => SweepStrays();

        /// <summary>
        /// Collect minis abandoned by earlier sessions.
        ///
        /// Every summon before this fix made a HideAndDontSave root, and those
        /// survive both a scene load and leaving play mode in the editor —
        /// they accumulate one per session until a domain reload, hidden from
        /// the hierarchy so nobody can select them. Resources.FindObjectsOfTypeAll
        /// is the only API that sees DontSave objects at all, which is why the
        /// canvas and the post-FX volumes are swept the same way.
        /// </summary>
        static void SweepStrays()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.name != MiniName) continue;
                // Ours is parented; a stray is a root. And it must actually
                // carry the flag that made it a stray — FindObjectsOfTypeAll
                // reaches assets too, and Destroy on an asset throws.
                if (t.parent != null) continue;
                if ((t.gameObject.hideFlags & HideFlags.DontSave) == 0) continue;
                Destroy(t.gameObject);
            }
        }

        static readonly XRHandJointID[] Tips =
        {
            XRHandJointID.IndexTip, XRHandJointID.MiddleTip,
            XRHandJointID.RingTip, XRHandJointID.LittleTip
        };

        void Update()
        {
            // Before the subsystem lookup: an unarmed session should not be
            // paying for hand joint queries at all.
            if (!armed) return;

            if (hands == null)
            {
                var list = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(list);
                if (list.Count > 0) hands = list[0];
                if (hands == null) return;
            }

            bool open = false;
            Pose palmPose = default;
            var hand = hands.leftHand;
            if (hand.isTracked && hand.GetJoint(XRHandJointID.Palm).TryGetPose(out palmPose))
            {
                float total = 0f;
                int counted = 0;
                foreach (var id in Tips)
                {
                    if (hand.GetJoint(id).TryGetPose(out var tipPose))
                    {
                        total += Vector3.Distance(tipPose.position, palmPose.position);
                        counted++;
                    }
                }
                open = counted >= 3 && (total / counted) > openThreshold;
            }

            visibility = Mathf.MoveTowards(visibility, open ? 1f : 0f, Time.deltaTime * 4f);

            if (visibility <= 0.01f)
            {
                Retire();
                return;
            }

            EnsureMini();
            mini.SetActive(true);

            // Session space -> world space via the XR origin.
            Vector3 palmWorld = palmPose.position;
            Vector3 upWorld = palmPose.rotation * Vector3.up;
            if (xrOrigin != null)
            {
                palmWorld = xrOrigin.transform.TransformPoint(palmPose.position);
                upWorld = xrOrigin.transform.TransformDirection(palmPose.rotation * Vector3.up);
            }

            mini.transform.position = palmWorld + upWorld * 0.12f;
            mini.transform.rotation = Quaternion.Euler(0f, Time.time * 12f, -4f);
            mini.transform.localScale = Vector3.one * (0.02f * visibility); // Rs = 2 cm fully open
        }

        /// <summary>Hide the mini and forget the fade-in. Disarming mid-summon
        /// must not leave it half-visible, and must not have it reappear at
        /// whatever alpha it was at when the gesture is armed again.</summary>
        void Retire()
        {
            visibility = 0f;
            if (mini != null) mini.SetActive(false);
        }

        void EnsureMini()
        {
            if (mini != null) return;
            mini = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mini.name = MiniName;

            // Parented, and NOT DontSave. It used to be a HideAndDontSave root,
            // which is a scene-load survivor AND hidden in the hierarchy: once
            // a session had summoned one, it outlived the black hole scene,
            // followed the visitor into the galaxy and the nebulae, and sat
            // there frozen at the last palm pose with nothing left alive to
            // move or hide it — invisible in the hierarchy, so unkillable by
            // hand. Riding this transform means it dies with the exhibit.
            mini.transform.SetParent(transform, false);
            Destroy(mini.GetComponent<Collider>());
            if (holeMaterial != null)
                mini.GetComponent<MeshRenderer>().sharedMaterial = holeMaterial;
        }
    }
}
