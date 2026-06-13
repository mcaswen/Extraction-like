using Gameplay.Agent.Core;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ExtractionPointController : MonoBehaviour
{
    public string ExtractionPointName = "Extraction Point";
    public float ExtractionDurationSeconds = 3f;

    [Range(0.05f, 1f)]
    public float DetectionHorizontalScale = 1f;

    public float WorldPromptVerticalOffset = 0.9f;

    private Collider _playerCollider;
    private string _playerAgentId;
    private bool _isPlayerInsideActiveBounds;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void Update()
    {
        if (_playerCollider != null)
        {
            RefreshPlayerPresence();
        }
    }

    private void OnDisable()
    {
        SetPlayerInsideActiveBounds(false);
        _playerCollider = null;
        _playerAgentId = string.Empty;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        _playerCollider = other;
        _playerAgentId = ResolveAgentId(other);
        RefreshPlayerPresence();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (_playerCollider != other)
        {
            SetPlayerInsideActiveBounds(false);
            _playerCollider = other;
        }

        _playerAgentId = ResolveAgentId(other);
        RefreshPlayerPresence();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (_playerCollider == other)
        {
            SetPlayerInsideActiveBounds(false);
            _playerCollider = null;
            _playerAgentId = string.Empty;
            return;
        }
    }

    public Vector3 GetWorldPromptPosition()
    {
        Bounds effectiveBounds = GetEffectiveBounds();
        return new Vector3(
            effectiveBounds.center.x,
            effectiveBounds.max.y + WorldPromptVerticalOffset,
            effectiveBounds.center.z);
    }

    private void RefreshPlayerPresence()
    {
        SetPlayerInsideActiveBounds(IsPlayerInsideActiveBounds());
    }

    private void SetPlayerInsideActiveBounds(bool isInside)
    {
        if (_isPlayerInsideActiveBounds == isInside)
        {
            return;
        }

        _isPlayerInsideActiveBounds = isInside;
        if (!string.IsNullOrEmpty(_playerAgentId))
            RaidFlowController.Instance?.SetAgentInsideExtractionPoint(_playerAgentId, this, isInside);
        else
            RaidFlowController.Instance?.SetPlayerInsideExtractionPoint(this, isInside);
    }

    private bool IsPlayerInsideActiveBounds()
    {
        if (_playerCollider == null)
        {
            return false;
        }

        Bounds effectiveBounds = GetEffectiveBounds();
        Vector3 playerPosition = _playerCollider.bounds.center;
        return Mathf.Abs(playerPosition.x - effectiveBounds.center.x) <= effectiveBounds.extents.x &&
               Mathf.Abs(playerPosition.z - effectiveBounds.center.z) <= effectiveBounds.extents.z;
    }

    private static string ResolveAgentId(Collider collider)
    {
        if (collider == null)
            return string.Empty;

        AgentPawnRoot agent = collider.GetComponentInParent<AgentPawnRoot>();
        return agent != null ? agent.AgentIdValue : string.Empty;
    }

    private Bounds GetEffectiveBounds()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger == null)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds worldBounds = trigger.bounds;
        float horizontalScale = Mathf.Clamp(DetectionHorizontalScale, 0.05f, 1f);
        Vector3 effectiveSize = worldBounds.size;
        effectiveSize.x *= horizontalScale;
        effectiveSize.z *= horizontalScale;
        return new Bounds(worldBounds.center, effectiveSize);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.25f);
        Collider trigger = GetComponent<Collider>();
        if (trigger is BoxCollider boxCollider)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.85f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);

            Gizmos.matrix = Matrix4x4.identity;
            Bounds effectiveBounds = GetEffectiveBounds();
            Gizmos.color = new Color(0.08f, 1f, 0.45f, 0.95f);
            Gizmos.DrawWireCube(effectiveBounds.center, effectiveBounds.size);
            return;
        }

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}
