using System.Collections.Generic;
using UnityEngine;

namespace BlackHoleEffect
{
    /// <summary>
    /// Hangs the shared UI in the room for MR, bending the 1920x1080 layout onto a
    /// cylinder around the viewer.
    ///
    /// The layout itself is untouched — panels are still authored against the flat
    /// frame, with the middle left empty so the real hole sits in it. What changes
    /// is where a panel's canvas position lands in the room: its x becomes an angle
    /// around the viewer and its y a height, at a constant radius. So every panel
    /// is the same distance away, square-on, and legible.
    ///
    /// Why bother, when the flat frame worked: on a plane the corners recede (1.60 m
    /// centre, 2.17 m corner) and are seen at up to 42° of slant, and — the real
    /// problem — the arc and the text size are welded together. Narrowing the frame
    /// to bring the corners into a comfortable head turn shrank every glyph with it
    /// (the theory panel's fine print fell to 24 arcmin), so it had to be reverted.
    /// A cylinder separates the two: the radius fixes text size, the arc fixes where
    /// the panels sit, and they no longer trade against each other.
    ///
    /// Nothing here parents to the hole. It is two-hand scalable, and a parented
    /// canvas would scale the text along with it.
    /// </summary>
    [DisallowMultipleComponent]
    public class MRWorldCanvas : MonoBehaviour
    {
        [Tooltip("Left empty, the hole in the scene is found on first placement.")]
        public Transform target;
        public Camera viewer;

        [Tooltip("Ignore the target and ride in front of the viewer. The fall-in " +
                 "flies into the hole, and a frame centred there ends up inside " +
                 "the viewer's face — during the ride the panels follow instead.")]
        public bool followViewer;

        [Tooltip("Distance in front of the viewer when there is no target.")]
        public float fallbackDistance = 2f;

        [Tooltip("Drop the frame below the hole. Sustained upward gaze is the more " +
                 "tiring direction, and the panels sat at +17 deg.")]
        public float verticalDrop = 0.14f;   // ~5 deg at the 1.6 m viewing distance

        [Tooltip("How much of the horizon the full 1920px width wraps onto. The flat " +
                 "frame spread the outermost panels to 38 deg of yaw; 70 brings them " +
                 "to about 26 — inside an easy head turn — and because the radius is " +
                 "unchanged, the text stays exactly as large as it was.")]
        public float arcDegrees = 70f;

        [Tooltip("Degrees per second the arrangement swings to follow the viewer. " +
                 "Snapping it every frame makes the panels feel glued to the face.")]
        public float turnSpeed = 90f;

        // One slot per top-level panel, inserted between the canvas and the panel.
        // The panel keeps its own anchors and anchoredPosition — it is the slot that
        // gets moved onto the cylinder — so its authored layout is never disturbed
        // and nothing writes back into the values we read from it.
        readonly Dictionary<RectTransform, RectTransform> slots = new();

        void LateUpdate() => Place(Time.deltaTime * turnSpeed);

        /// <summary>Snap into place without easing (first frame).</summary>
        public void PlaceNow() => Place(360f);

        void Place(float maxTurnDegrees)
        {
            var cam = viewer != null ? viewer : Camera.main;
            if (cam == null) return;

            if (target == null && !followViewer)
            {
                var hole = FindAnyObjectByType<BlackHoleController>();
                if (hole != null) target = hole.transform;
            }

            transform.position = (target != null && !followViewer
                ? target.position
                : cam.transform.position + cam.transform.forward * fallbackDistance)
                + Vector3.down * verticalDrop;

            // Yaw only: an arrangement that pitches with the viewer's gaze reads as a
            // heads-up display stuck to the helmet, not as something in the room.
            var away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) return;

            var want = Quaternion.LookRotation(away.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, maxTurnDegrees);

            AdoptNewPanels();
            LayOutCylinder(cam);
        }

        /// <summary>Everything that gets built straight onto the canvas is a panel
        /// we own. It appears flat for the frame it is created in, then moves.</summary>
        void AdoptNewPanels()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i) as RectTransform;
                // The canvas's children are new panels plus the slots we already
                // made — skip our own, or we would keep nesting slots in slots.
                if (child == null || slots.ContainsKey(child)) continue;

                var slotGo = new GameObject("Slot — " + child.name) { hideFlags = HideFlags.DontSave };
                var slot = slotGo.AddComponent<RectTransform>();
                slot.SetParent(transform, false);
                // Same rect as the canvas, so the panel's anchors resolve to exactly
                // the same local position they did before it was reparented.
                slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = ((RectTransform)transform).sizeDelta;
                slot.anchoredPosition = Vector2.zero;
                slot.localScale = Vector3.one;

                child.SetParent(slot, false);
                child.localRotation = Quaternion.identity;
                slots[slot] = child;
            }

            // Panels are destroyed with their owners; their slots are ours to clear.
            var dead = new List<RectTransform>();
            foreach (var kv in slots)
                if (kv.Value == null || kv.Key == null) dead.Add(kv.Key);
            foreach (var k in dead)
            {
                if (k != null) Destroy(k.gameObject);
                slots.Remove(k);
            }
        }

        void LayOutCylinder(Camera cam)
        {
            float scale = transform.localScale.x;
            float halfWidthPx = ((RectTransform)transform).sizeDelta.x * 0.5f;
            if (halfWidthPx <= 0f || scale <= 0f) return;

            // Radius: the distance the flat frame's centre sat at, so the middle of
            // the layout lands exactly where it always did and only the wings bend.
            Vector3 eye = cam.transform.position;
            Vector3 flat = transform.position - eye;
            flat.y = 0f;
            float radius = flat.magnitude;
            if (radius < 0.05f) return;

            Vector3 basis = new Vector3(eye.x, transform.position.y, eye.z);

            foreach (var kv in slots)
            {
                var slot = kv.Key;
                var panel = kv.Value;
                if (slot == null || panel == null) continue;

                // The panel's own position in the authored layout. Read, never written.
                Vector3 local = panel.localPosition;
                float azimuth = local.x / halfWidthPx * (arcDegrees * 0.5f);

                Quaternion facing = transform.rotation * Quaternion.AngleAxis(azimuth, Vector3.up);
                Vector3 seat = basis + facing * Vector3.forward * radius + Vector3.up * (local.y * scale);

                // Place the slot so that the panel — sitting at `local` inside it —
                // lands on the cylinder, square-on to the viewer.
                slot.SetPositionAndRotation(seat - facing * (local * scale), facing);
            }
        }
    }
}
