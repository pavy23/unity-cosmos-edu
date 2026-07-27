using System.Collections.Generic;
using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// The nebulae &amp; clusters exhibit as a room miniature: all six specimens
    /// live under one root and are shown one at a time — each is a full
    /// volumetric raymarch, and MR pays for it twice (once per eye).
    ///
    /// A vitrine, not an instrument. The visitor does not pick this exhibit up
    /// — six volumetric specimens have no reason to be carried around, and a
    /// grab with no Reset button strands the case wherever it lands. What the
    /// visitor DOES get is the pair of verbs that matter: step through the
    /// specimens by hand, or hand the room over to the "별의 일생" tour and let
    /// it drive. The dwell timer only fills the gaps between them, so an
    /// unattended case still cycles instead of freezing on one specimen.
    /// </summary>
    public class NebulaMRStage : MonoBehaviour
    {
        [Tooltip("Specimen roots, in NebulaLibrary.Heroes order.")]
        public List<GameObject> specimens = new();

        [Tooltip("Seconds each specimen holds the case before the next one. Long " +
                 "enough to read the label through — the blurbs run 3-4 lines — " +
                 "and to walk around the miniature once. Zero disables the " +
                 "unattended cycle entirely.")]
        public float dwellSeconds = 20f;

        /// <summary>Let the dwell timer advance the case. The tour turns this
        /// off while it runs: it decides which specimen each of its steps
        /// shows, and a timer firing underneath it would swap the specimen out
        /// from under the narration mid-sentence.</summary>
        public bool AutoCycle { get; set; } = true;

        public int Current { get; private set; }
        public NebulaLibrary.Hero CurrentHero => NebulaLibrary.Heroes[Current];

        float held;

        void Start()
        {
            Show(0);
        }

        void Update()
        {
            if (!AutoCycle || specimens.Count < 2 || dwellSeconds <= 0f) return;
            held += Time.deltaTime;
            if (held < dwellSeconds) return;
            Next();
        }

        public void Show(int i)
        {
            if (specimens.Count == 0) return;
            Current = (i % specimens.Count + specimens.Count) % specimens.Count;
            // Every path through here restarts the dwell — a visitor who just
            // pressed 다음 has earned a full reading window, not the remainder
            // of the previous specimen's.
            held = 0f;
            for (int s = 0; s < specimens.Count; s++)
                if (specimens[s] != null) specimens[s].SetActive(s == Current);
        }

        public void Next() => Show(Current + 1);
        public void Prev() => Show(Current - 1);
    }
}
