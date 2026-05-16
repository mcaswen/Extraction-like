using UnityEngine;

public sealed class EnemySuspicionSensor : MonoBehaviour
{
    [Header("Hearing")]
    [SerializeField, Min(0.1f)]
    private float _hearingMultiplier = 1f;

    [SerializeField, Range(0f, 1f)]
    private float _minimumAcceptedStrength = 0.18f;

    [SerializeField, Min(0f)]
    private float _stimulusCooldown = 0.25f;

    [SerializeField]
    private LayerMask _occlusionMask = 1;

    [SerializeField, Range(0f, 1f)]
    private float _occludedStrengthMultiplier = 0.65f;

    [SerializeField, Min(0f)]
    private float _earHeight = 1.1f;

    private EnemySuspicionRecord _strongestRecord;
    private bool _hasRecord;
    private float _lastAcceptedTime = -999f;

    public bool HasActiveRecord => _hasRecord && Time.time <= _strongestRecord.ExpireTime;
    public EnemySuspicionRecord StrongestRecord => _strongestRecord;

    private void OnEnable()
    {
        EnemySuspicionStimulusBus.StimulusRaised += HandleStimulusRaised;
    }

    private void OnDisable()
    {
        EnemySuspicionStimulusBus.StimulusRaised -= HandleStimulusRaised;
    }

    public bool TryConsumeRecord(out EnemySuspicionRecord record)
    {
        record = default;
        if (!HasActiveRecord)
        {
            _hasRecord = false;
            return false;
        }

        record = _strongestRecord;
        _hasRecord = false;
        return true;
    }

    public void Clear()
    {
        _hasRecord = false;
    }

    private void HandleStimulusRaised(EnemySuspicionStimulus stimulus)
    {
        if (!isActiveAndEnabled || stimulus.Source == transform)
        {
            return;
        }

        Vector3 toStimulus = stimulus.Position - transform.position;
        toStimulus.y = 0f;
        float distance = toStimulus.magnitude;
        float effectiveRadius = Mathf.Max(0.1f, stimulus.Radius * _hearingMultiplier);
        if (distance > effectiveRadius)
        {
            return;
        }

        if (Time.time < _lastAcceptedTime + _stimulusCooldown)
        {
            return;
        }

        float distanceFactor = 1f - Mathf.Clamp01(distance / effectiveRadius);
        float strength = stimulus.Strength * Mathf.Lerp(0.35f, 1f, distanceFactor);
        if (IsOccluded(stimulus.Position))
        {
            strength *= _occludedStrengthMultiplier;
        }

        if (strength < _minimumAcceptedStrength)
        {
            return;
        }

        Vector3 estimatedPosition = ApplyUncertainty(stimulus.Position, stimulus.UncertaintyRadius, strength);
        EnemySuspicionRecord record = new EnemySuspicionRecord(
            stimulus.Type,
            estimatedPosition,
            stimulus.Position,
            strength,
            Time.time + stimulus.Duration,
            stimulus.Source);

        if (!_hasRecord || strength >= _strongestRecord.Strength || Time.time > _strongestRecord.ExpireTime)
        {
            _strongestRecord = record;
            _hasRecord = true;
            _lastAcceptedTime = Time.time;
        }
    }

    private bool IsOccluded(Vector3 stimulusPosition)
    {
        if (_occlusionMask.value == 0)
        {
            return false;
        }

        Vector3 origin = transform.position + Vector3.up * _earHeight;
        Vector3 target = stimulusPosition + Vector3.up * _earHeight;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        return Physics.Raycast(
            origin,
            direction / distance,
            distance,
            _occlusionMask,
            QueryTriggerInteraction.Ignore);
    }

    private static Vector3 ApplyUncertainty(Vector3 position, float uncertaintyRadius, float strength)
    {
        float effectiveRadius = uncertaintyRadius * (1f - Mathf.Clamp01(strength));
        if (effectiveRadius <= 0.05f)
        {
            return position;
        }

        Vector2 randomOffset = Random.insideUnitCircle * effectiveRadius;
        return position + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (!HasActiveRecord)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.62f, 0.08f, 1f);
        Gizmos.DrawWireSphere(_strongestRecord.EstimatedPosition, Mathf.Lerp(0.2f, 0.8f, _strongestRecord.Strength));
        Gizmos.DrawLine(transform.position + Vector3.up * _earHeight, _strongestRecord.EstimatedPosition + Vector3.up * _earHeight);
    }
}
