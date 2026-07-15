using UnityEngine;

namespace BlackHoleEffect
{
    /// <summary>
    /// Hangs the shared UI canvas in the room for MR: centred on the black hole
    /// so the empty middle of the 1920x1080 layout frames the real object and
    /// the panels sit around it, kept upright and turned toward the viewer.
    ///
    /// The canvas only copies the target's position — never parents to it. The
    /// hole is two-hand scalable, and a parented canvas would scale the text
    /// along with it.
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
                 "tiring direction, and the panels sat at +17 deg; this brings them " +
                 "to about +12 while the menu stays inside a comfortable look-down.")]
        public float verticalDrop = 0.14f;   // ~5 deg at the 1.6 m viewing distance

        [Tooltip("Turn each panel to face the viewer. The frame is one flat plane, " +
                 "so its corners are seen at up to 36 deg of slant — keystoned and " +
                 "harder to read. Facing them individually is most of what a curved " +
                 "layout would buy, without rebuilding every panel's placement.")]
        public bool facePanels = true;

        [Tooltip("Degrees per second the frame turns to face the viewer. Snapping " +
                 "it every frame makes the panels feel glued to the face.")]
        public float turnSpeed = 90f;

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

            // Yaw only: a frame that pitches with the viewer's gaze reads as a
            // heads-up display stuck to the helmet, not as an object in the room.
            var away = transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) return;

            var want = Quaternion.LookRotation(away.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, maxTurnDegrees);

            if (facePanels) FacePanels(cam);
        }

        /// <summary>
        /// Turn each panel toward the viewer where it stands. Layout still comes
        /// from the canvas — only the orientation changes — so a panel keeps its
        /// place in the frame but stops being read edge-on.
        ///
        /// Direct children only: everything nested (a panel's accent line, a
        /// button's label) rides along with its parent.
        /// </summary>
        void FacePanels(Camera cam)
        {
            foreach (Transform child in transform)
            {
                var away = child.position - cam.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-4f) continue;
                child.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }
        }
    }
}
