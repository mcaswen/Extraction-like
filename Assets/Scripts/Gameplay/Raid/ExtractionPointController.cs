using Gameplay.Agent.Core;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 撤离点触发器，负责向局内流程控制器汇报玩家或智能体是否处于有效撤离范围
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExtractionPointController : MonoBehaviour
{
    public string ExtractionPointName = "Extraction Point";
    public float ExtractionDurationSeconds = 3f;

    [Range(0.05f, 1f)]
    public float DetectionHorizontalScale = 1f;

    public float WorldPromptVerticalOffset = 0.9f;

    private sealed class PlayerPresence
    {
        public readonly string AgentId;
        public readonly HashSet<Collider> Colliders = new HashSet<Collider>();
        public bool Inside;
        public PlayerPresence(string agentId) { AgentId = agentId; }
    }
    private readonly Dictionary<string, PlayerPresence> _players = new Dictionary<string, PlayerPresence>();
    private readonly List<Collider> _expiredColliders = new List<Collider>();
    private readonly List<string> _emptyPlayers = new List<string>();

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
        if (_players.Count == 0) return;
        Bounds bounds = GetEffectiveBounds();
        _emptyPlayers.Clear();
        foreach (var pair in _players)
        {
            RefreshPlayerPresence(pair.Value, bounds);
            if (pair.Value.Colliders.Count == 0) _emptyPlayers.Add(pair.Key);
        }
        foreach (string id in _emptyPlayers) _players.Remove(id);
    }

    private void OnDisable()
    {
        foreach (var player in _players.Values) SetPlayerInsideActiveBounds(player, false);
        _players.Clear();
        _expiredColliders.Clear();
        _emptyPlayers.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TrackPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrackPlayer(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        string id = ResolveAgentId(other);
        if (!_players.TryGetValue(id, out var player)) return;
        player.Colliders.Remove(other);
        RefreshPlayerPresence(player, GetEffectiveBounds());
        if (player.Colliders.Count == 0) _players.Remove(id);
    }

    private void TrackPlayer(Collider other)
    {
        // Unity can deliver trigger callbacks to disabled behaviours.
        if (!isActiveAndEnabled || other == null || !other.CompareTag("Player")) return;
        string id = ResolveAgentId(other);
        if (!_players.TryGetValue(id, out var player))
        {
            player = new PlayerPresence(id);
            _players.Add(id, player);
        }
        player.Colliders.Add(other);
        RefreshPlayerPresence(player, GetEffectiveBounds());
    }

    /// <summary>
    /// 获取世界空间提示界面应显示的位置
    /// </summary>
    /// <returns>撤离点上方的世界坐标</returns>
    public Vector3 GetWorldPromptPosition()
    {
        Bounds effectiveBounds = GetEffectiveBounds();
        return new Vector3(
            effectiveBounds.center.x,
            effectiveBounds.max.y + WorldPromptVerticalOffset,
            effectiveBounds.center.z);
    }

    private void RefreshPlayerPresence(PlayerPresence player, Bounds effectiveBounds)
    {
        bool inside = false;
        _expiredColliders.Clear();
        foreach (var collider in player.Colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || !collider.CompareTag("Player"))
            {
                _expiredColliders.Add(collider);
                continue;
            }
            Vector3 position = collider.bounds.center;
            inside |= Mathf.Abs(position.x - effectiveBounds.center.x) <= effectiveBounds.extents.x &&
                      Mathf.Abs(position.z - effectiveBounds.center.z) <= effectiveBounds.extents.z;
        }
        foreach (var collider in _expiredColliders) player.Colliders.Remove(collider);
        SetPlayerInsideActiveBounds(player, inside);
    }

    private void SetPlayerInsideActiveBounds(PlayerPresence player, bool isInside)
    {
        if (player.Inside == isInside)
        {
            return;
        }

        player.Inside = isInside;
        if (!string.IsNullOrEmpty(player.AgentId))
            RaidFlowController.Instance?.SetAgentInsideExtractionPoint(player.AgentId, this, isInside);
        else
            RaidFlowController.Instance?.SetPlayerInsideExtractionPoint(this, isInside);
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
        // 只收缩水平面，保留原始高度范围以容纳不同角色胶囊体
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
