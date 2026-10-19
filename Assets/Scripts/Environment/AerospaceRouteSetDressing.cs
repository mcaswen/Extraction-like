using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionLike.Environment
{
    /// <summary>Saved art manifest only. Does not update, search, spawn or alter navigation.</summary>
    public sealed class AerospaceRouteSetDressing : MonoBehaviour
    {
        [Serializable] public sealed class OriginalRendererState
        {
            public Material[] materials;
            public ShadowCastingMode shadowCasting;
        }
        [Serializable] public sealed class CaseAppearance
        {
            public LootBoxEntity container;
            public Renderer[] originalRenderers;
            public bool[] originalEnabled;
            public OriginalRendererState[] originalStates;
            public GameObject replacement;
            public Bounds originalLocalBounds;
            [Range(.35f, 1f)] public float visualScale = 1f;
        }
        [Serializable] public sealed class RoutePlacement
        {
            public string edgeId;
            public string purpose;
            public Transform assembly;
            public float footprintRadius;
        }
        public CaseAppearance[] cases = Array.Empty<CaseAppearance>();
        public RoutePlacement[] placements = Array.Empty<RoutePlacement>();
        public int coveredEdges;
        public string originalNavigationHash;
        public int originalColliderCount;
        public int originalAgentCount;
    }
}
