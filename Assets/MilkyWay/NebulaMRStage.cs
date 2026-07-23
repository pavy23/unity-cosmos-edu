using System.Collections.Generic;
using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// The nebulae &amp; clusters exhibit as a room miniature: all six specimens
    /// live under one grabbable root and are shown one at a time — each is a
    /// full volumetric raymarch, and MR pays for it twice (once per eye).
    /// Prev/next swaps the hero in place; the fact card and menu live on the
    /// world frame (NebulaMRControls).
    /// </summary>
    public class NebulaMRStage : MonoBehaviour
    {
        [Tooltip("Specimen roots, in NebulaLibrary.Heroes order.")]
        public List<GameObject> specimens = new();

        public int Current { get; private set; }
        public NebulaLibrary.Hero CurrentHero => NebulaLibrary.Heroes[Current];

        Vector3 homePos;
        Quaternion homeRot;
        Vector3 homeScale;

        void Start()
        {
            homePos = transform.position;
            homeRot = transform.rotation;
            homeScale = transform.localScale;
            Show(0);
        }

        public void Show(int i)
        {
            if (specimens.Count == 0) return;
            Current = (i % specimens.Count + specimens.Count) % specimens.Count;
            for (int s = 0; s < specimens.Count; s++)
                if (specimens[s] != null) specimens[s].SetActive(s == Current);
        }

        public void Next() => Show(Current + 1);
        public void Prev() => Show(Current - 1);

        /// <summary>Undo whatever grabbing did to the miniature.</summary>
        public void ResetPose()
        {
            transform.position = homePos;
            transform.rotation = homeRot;
            transform.localScale = homeScale;
        }
    }
}
