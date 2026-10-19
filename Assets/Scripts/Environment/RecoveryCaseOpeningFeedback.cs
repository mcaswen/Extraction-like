using UnityEngine;

namespace ExtractionLike.Environment
{
    /// <summary>One mechanical opening per case per scene. No loot, colliders or navigation changes.</summary>
    [DisallowMultipleComponent]
    public sealed class RecoveryCaseOpeningFeedback : MonoBehaviour
    {
        public const float OpeningDuration = 1.08f;
        [SerializeField] Transform[] lids = new Transform[0];
        [SerializeField] Transform[] leftLocks = new Transform[0];
        [SerializeField] Transform[] rightLocks = new Transform[0];
        [SerializeField] Renderer[] signals = new Renderer[0];
        [SerializeField] AudioSource mechanicalAudio;
        Vector3[] lidPositions, leftPositions, rightPositions;
        Quaternion[] lidRotations;
        MaterialPropertyBlock signalBlock;
        bool initialized, opening, open;
        float age;
        LootBoxEntity owner;
        float nextSignalCheck;
        public Color SignalEmission { get; private set; }

        public bool IsOpen => open;
        public bool IsOpening => opening;
        public int OpeningCount { get; private set; }

        public void Configure(Transform[] lidPivots, Transform[] leftPivots, Transform[] rightPivots,
            Renderer[] signalRenderers, AudioSource audio)
        {
            lids = lidPivots; leftLocks = leftPivots; rightLocks = rightPivots;
            signals = signalRenderers; mechanicalAudio = audio;
            initialized = false;
        }

        // Undressed / legacy containers retain their existing immediate interaction.
        public static bool EnsureOpen(GameObject container)
        {
            if (container == null) return true;
            var feedback = container.GetComponentInChildren<RecoveryCaseOpeningFeedback>();
            return feedback == null || !feedback.isActiveAndEnabled || feedback.BeginOpening();
        }

        public bool BeginOpening()
        {
            Initialize();
            if (open || lids.Length == 0) return true;
            if (!opening)
            {
                opening = true; age = 0; OpeningCount++;
                ApplyPose(0);
                if (mechanicalAudio != null && mechanicalAudio.clip != null) mechanicalAudio.Play();
            }
            return false;
        }

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            lidPositions = Positions(lids); leftPositions = Positions(leftLocks); rightPositions = Positions(rightLocks);
            lidRotations = new Quaternion[lids.Length];
            for (int i = 0; i < lids.Length; i++) lidRotations[i] = lids[i] != null ? lids[i].localRotation : Quaternion.identity;
            signalBlock = new MaterialPropertyBlock();
            owner = GetComponentInParent<LootBoxEntity>();
        }
        void Start() { Initialize(); RefreshRestingSignal(); }
        void RefreshRestingSignal()
        {
            // Read only the public state. Never enumerate/generate loot to decorate a box.
            bool empty = owner != null && owner.IsResourcePointLooted;
            Color color = empty ? new Color(.09f,.13f,.15f) : new Color(.10f,.73f,.94f);
            ApplySignal(color, empty ? .08f : open ? 1.15f : .48f);
        }
        void ApplySignal(Color color, float intensity)
        {
            SignalEmission = color * intensity;
            foreach (var renderer in signals) if (renderer != null)
            {
                renderer.GetPropertyBlock(signalBlock);
                signalBlock.SetColor("_BaseColor", color);
                signalBlock.SetColor("_EmissionColor", SignalEmission);
                renderer.SetPropertyBlock(signalBlock);
            }
        }
        static Vector3[] Positions(Transform[] values)
        {
            var result = new Vector3[values.Length];
            for (int i = 0; i < values.Length; i++) if (values[i] != null) result[i] = values[i].localPosition;
            return result;
        }
        void Update()
        {
            if (!opening)
            {
                if (Time.unscaledTime >= nextSignalCheck)
                { Initialize(); RefreshRestingSignal(); nextSignalCheck = Time.unscaledTime + .2f; }
                return;
            }
            // Inventory may pause game time; the mechanical action must never strand the search gate.
            age = Mathf.Min(OpeningDuration, age + Time.unscaledDeltaTime);
            ApplyPose(age);
            if (age >= OpeningDuration) { opening = false; open = true; RefreshRestingSignal(); }
        }
        void OnDisable()
        {
            // A disabled visual must not leave a pending interaction waiting forever.
            if (opening) { ApplyPose(OpeningDuration); opening = false; open = true; }
            if (mechanicalAudio != null) mechanicalAudio.Stop();
        }
        static float Smooth(float a, float b, float time) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, time));
        void ApplyPose(float seconds)
        {
            float release = Smooth(.03f, .17f, seconds);
            float lift = Smooth(.18f, .31f, seconds);
            float swing = Smooth(.28f, .94f, seconds);
            float settle = seconds <= .94f ? 0 : Mathf.Sin(Mathf.InverseLerp(.94f, OpeningDuration, seconds) * Mathf.PI) * .65f;
            for (int i = 0; i < lids.Length; i++) if (lids[i] != null)
            {
                lids[i].localPosition = lidPositions[i] + Vector3.up * (.10f * lift);
                lids[i].localRotation = lidRotations[i] * Quaternion.AngleAxis(74f * swing + settle, Vector3.right);
            }
            MoveLocks(leftLocks, leftPositions, -1, release);
            MoveLocks(rightLocks, rightPositions, 1, release);
            // Restrained internal illumination; no realtime point lights or shared-material mutation.
            float illumination = .65f + Smooth(.25f, .65f, seconds) * 1.05f;
            illumination += Mathf.Sin(Mathf.Clamp01(seconds / OpeningDuration) * Mathf.PI) * .75f;
            ApplySignal(new Color(.10f, .73f, .94f), illumination);
        }
        static void MoveLocks(Transform[] locks, Vector3[] bases, float side, float release)
        {
            for (int i = 0; i < locks.Length; i++) if (locks[i] != null)
                locks[i].localPosition = bases[i] + new Vector3(side * .035f, -.14f, 0) * release;
        }
#if UNITY_EDITOR
        // Deterministic preview for saved-frame verification; never changes the interaction state.
        public void PreviewPose(float seconds) { Initialize(); ApplyPose(Mathf.Clamp(seconds, 0, OpeningDuration)); }
#endif
    }
}
