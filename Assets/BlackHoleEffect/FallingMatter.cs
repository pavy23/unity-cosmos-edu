using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;              // Select*EventArgs
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRGrabInteractable

namespace BlackHoleEffect
{
    /// <summary>
    /// MR throwable: grab a glowing star-ball, toss it, and gravity pulls it
    /// into an orbit that decays into the hole — the disk flares as it feeds.
    /// The ball respawns at its shelf position a moment later.
    ///
    /// The ball is kinematic until it is thrown. Without that the shelf is not a
    /// shelf: gravity applies from the first frame, so the balls launch
    /// themselves at the hole and are gone before anyone reaches for one. (They
    /// then loop — consumed, respawned, off again — which reads as an ambient
    /// effect, not something you can pick up.)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FallingMatter : MonoBehaviour
    {
        public Transform hole;
        public MatterFlare flare;
        [Tooltip("Effective GM in m^3/s^2 — tuned for room-scale orbits.")]
        public float gm = 0.35f;

        Rigidbody rb;
        Vector3 spawnPos;
        Quaternion spawnRot;
        Vector3 baseScale;
        bool consumed;
        bool thrown;    // gravity only applies once someone has let go of it
        bool held;
        XRGrabInteractable grab;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            spawnPos = transform.position;
            spawnRot = transform.rotation;
            baseScale = transform.localScale;

            grab = GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.selectEntered.AddListener(OnGrabbed);
                grab.selectExited.AddListener(OnReleased);
            }
        }

        void OnDestroy()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        void OnGrabbed(SelectEnterEventArgs _) => held = true;

        /// <summary>Let go and it becomes a body in freefall. Note this leaves
        /// isKinematic alone — XRI owns that while grabbing (VelocityTracking
        /// needs a dynamic body, and it restores the flag on detach), so gating
        /// gravity on our own state is the only way not to fight it.</summary>
        void OnReleased(SelectExitEventArgs _)
        {
            held = false;
            thrown = true;
        }

        void FixedUpdate()
        {
            if (consumed || hole == null || rb.isKinematic || held) return;

            // On the shelf: stay on the shelf. Pin it rather than just skipping
            // gravity, so a knock from a passing hand cannot nudge it away either.
            if (!thrown)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                transform.SetPositionAndRotation(spawnPos, spawnRot);
                return;
            }

            Vector3 d = hole.position - transform.position;
            float r = Mathf.Max(d.magnitude, 0.05f);
            rb.AddForce(d / r * (gm / (r * r)), ForceMode.Acceleration);

            if (r < 1.6f * hole.lossyScale.x)
            {
                consumed = true;
                if (flare != null) flare.Trigger();
                StartCoroutine(ConsumeAndRespawn());
            }
        }

        IEnumerator ConsumeAndRespawn()
        {
            // Spiral shrink into the hole.
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                transform.localScale = baseScale * (1f - t / 0.5f);
                yield return null;
            }
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.localScale = Vector3.zero;

            yield return new WaitForSeconds(2.5f);

            transform.SetPositionAndRotation(spawnPos, spawnRot);
            transform.localScale = baseScale;
            consumed = false;
            thrown = false; // back on the shelf, waiting to be picked up again
        }
    }
}
