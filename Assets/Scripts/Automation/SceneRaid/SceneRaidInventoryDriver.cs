#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Navigation;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    /// <summary>只模拟玩家背包操作，不选择目标、移动角色、推进搜索或提交撤离。</summary>
    public sealed class SceneRaidInventoryDriver : IDisposable
    {
        private sealed class Pending { public AgentResourceInteractionEvent fact; public long order; }
        [Serializable] private sealed class Trace
        { public string agent, commandId, resource, reason, item; public int amount; public float progress, duration; }
        private readonly Dictionary<string, Pending> _waiting = new Dictionary<string, Pending>();
        private readonly HashSet<int> _blockedItems = new HashSet<int>();
        private readonly SceneRaidEvidenceWriter _writer;
        private readonly SceneRaidIdentityMap _identity;
        private readonly SceneRaidInventoryLedger _ledger;
        private readonly Func<string, AgentResourceInteractionEvent?> _latest;
        private Pending _current;
        private InventoryScreenSessionContext _session;
        private LootBoxEntity _box;
        private float _previousScale, _previousFixed;
        private double _nextTick, _started, _nextSearchLog;
        private long _order;
        private int _focusFrame;
        private bool _hadPolicyBlock;
        private bool _triedSorting;
        public string BlockedReason { get; private set; }
        public int CompletedSessions { get; private set; }
        public int MutationRevision { get; private set; }
        private readonly HashSet<string> _completedCommands = new HashSet<string>();
        public bool HasCompletedSession(string commandId) => !string.IsNullOrEmpty(commandId) && _completedCommands.Contains(commandId);
        public readonly HashSet<string> ServedAgents = new HashSet<string>();
        public SceneRaidInventoryDriver(SceneRaidEvidenceWriter writer, SceneRaidIdentityMap identity,
            Func<string, AgentResourceInteractionEvent?> latest)
        {
            _writer = writer; _identity = identity; _latest = latest;
            _ledger = new SceneRaidInventoryLedger(writer);
            AgentResourceInteractionChannel.Published += Interaction;
        }
        private static bool Matches(AgentResourceInteractionEvent a, AgentResourceInteractionEvent b) =>
            a.AgentId == b.AgentId && a.CommandId == b.CommandId && a.Resource == b.Resource;
        private void Interaction(AgentResourceInteractionEvent value)
        {
            if (value.Stage != AgentResourceInteractionStage.WaitingForInventory)
            {
                if (_waiting.TryGetValue(value.AgentId, out var old) && Matches(old.fact, value)) _waiting.Remove(value.AgentId);
                return;
            }
            if (_current != null && Matches(_current.fact, value)) { _current.fact = value; return; }
            if (_waiting.TryGetValue(value.AgentId, out var pending) && Matches(pending.fact, value)) pending.fact = value;
            else _waiting[value.AgentId] = new Pending { fact = value, order = ++_order };
        }
        private bool Valid(Pending pending, out AgentRuntimeHandle agent)
        {
            agent = default;
            var fact = pending.fact;
            var latest = _latest(fact.AgentId);
            var registry = AgentRuntimeRegistry.ActiveInstance;
            if (!latest.HasValue || !Matches(latest.Value, fact) || latest.Value.Stage != AgentResourceInteractionStage.WaitingForInventory ||
                fact.Resource == null || !fact.Resource.activeInHierarchy || registry == null ||
                !registry.TryGetHandle(fact.AgentId, out agent) || !agent.IsAlive) return false;
            var active = agent.PawnRoot.DirectiveLifecycle.Active;
            if (!active.HasValue || active.Value.CommandId != fact.CommandId || active.Value.DirectiveType != AgentDirectiveType.Search) return false;
            float range = agent.PawnRoot.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.InteractionDistance);
            Vector3 delta = agent.ReadOnly.Position - fact.NavigationPosition; delta.y = 0;
            return delta.sqrMagnitude <= Mathf.Pow(Mathf.Max(AgentNavigationQuery.ArrivalTolerance, range) + 0.02f, 2);
        }
        public void Tick()
        {
            if (BlockedReason != null || _writer.WallSeconds < _nextTick) return;
            _nextTick = _writer.WallSeconds + 0.1;
            var screen = InventoryScreenController.Instance;
            if (screen == null) return;
            if (_current == null)
            {
                if (screen.IsInventoryOpen) throw new InvalidOperationException("Unexpected inventory session without a driver owner.");
                foreach (var pending in _waiting.Values.OrderBy(x => x.order).ThenBy(x => x.fact.AgentId).ToArray())
                {
                    _waiting.Remove(pending.fact.AgentId);
                    if (!Valid(pending, out _)) continue;
                    _current = pending; _started = _writer.WallSeconds; _focusFrame = Time.frameCount;
                    if (!AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(pending.fact.AgentId))
                        throw new InvalidOperationException("Could not focus the waiting agent.");
                    Log("focus", "Formal registry focus selected");
                    return;
                }
                return;
            }
            if (!Valid(_current, out _) || (_session != null && !screen.IsSessionContextActive(_session)))
            { Close("interaction_invalidated", false); return; }
            if (_writer.WallSeconds - _started > 90) throw new TimeoutException("Inventory driver exceeded its wall-clock deadline.");
            if (_session == null)
            {
                if (Time.frameCount <= _focusFrame || screen.ActiveInventoryAgentId != _current.fact.AgentId) return;
                if (screen.IsInventoryOpen) throw new InvalidOperationException("Inventory opened before the driver action.");
                _box = _current.fact.Resource.GetComponent<LootBoxEntity>();
                if (_box == null) { BlockedReason = "UnsupportedResource:" + _identity.Get(_current.fact.Resource); Close("unsupported", false); return; }
                _previousScale = Time.timeScale; _previousFixed = Time.fixedDeltaTime;
                _box.Interact();
                _session = screen.ActiveSessionContext;
                if (_session == null || !screen.IsInventoryOpen || screen.ActiveExternalGrid == null)
                    throw new InvalidOperationException("Formal LootBox.Interact did not open its inventory session.");
                _blockedItems.Clear();
                _hadPolicyBlock = false;
                _triedSorting = false;
                _ledger.Begin(_current.fact.AgentId, _identity.Get(_box), screen.ActiveExternalGrid.ExtractSaveData(), screen.BackpackGrid.ExtractSaveData());
                Log("opened", "Formal LootBox.Interact");
                return;
            }
            if (screen.ActiveInventoryAgentId != _current.fact.AgentId) throw new InvalidOperationException("Inventory ownership changed during a session.");
            var source = screen.ActiveExternalGrid;
            var items = source.ItemContainer.GetComponentsInChildren<DraggableItemUI>()
                .Where(x => x.CurrentGrid == source).OrderBy(x => x._originalGridIndex.y).ThenBy(x => x._originalGridIndex.x).ToArray();
            if (items.Length == 0) { Close("emptied", true); return; }
            var candidate = items.FirstOrDefault(x => !_blockedItems.Contains(x.GetInstanceID()));
            if (candidate == null)
            {
                var capacity = InventoryLootCapacityAssessment.Evaluate(screen);
                if (capacity == InventoryLootCapacity.CanTransferAfterSorting && !_triedSorting)
                {
                    _triedSorting = true;
                    screen.BackpackGrid.AutoSort();
                    _blockedItems.Clear();
                    _ledger.Check("sorted", source.ExtractSaveData(), screen.BackpackGrid.ExtractSaveData());
                    Log("sorted", "Formal backpack AutoSort");
                    return;
                }
                if (capacity == InventoryLootCapacity.CapacityBlocked)
                {
                    Close("capacity_requires_extraction", true);
                    return;
                }
                BlockedReason = (_hadPolicyBlock ? "InventoryRuleBlocked:" : "InventoryCapacityBlocked:") + _current.fact.AgentId;
                Close("all_candidates_blocked", true);
                return;
            }
            var beforeSource = source.ExtractSaveData();
            var beforePlayer = screen.BackpackGrid.ExtractSaveData();
            var data = candidate.ItemData;
            int amount = candidate.CurrentAmount;
            if (candidate.TryQuickTransfer(out var failure))
            {
                var afterSource = source.ExtractSaveData();
                var afterPlayer = screen.BackpackGrid.ExtractSaveData();
                long moved = SceneRaidInventoryLedger.Amount(beforeSource, data) - SceneRaidInventoryLedger.Amount(afterSource, data);
                if (moved <= 0 || moved > amount ||
                    SceneRaidInventoryLedger.Amount(afterPlayer, data) - SceneRaidInventoryLedger.Amount(beforePlayer, data) != moved)
                    throw new InvalidOperationException("Quick transfer source/destination delta mismatch.");
                _blockedItems.Clear();
                _ledger.Check("transferred", afterSource, afterPlayer);
                Log("transferred", "Formal quick transfer", candidate);
            }
            else if (failure == InventoryQuickTransferFailure.SearchPending)
            {
                if (_writer.WallSeconds >= _nextSearchLog) { _nextSearchLog = _writer.WallSeconds + 1; Log("searching", failure.ToString(), candidate); }
            }
            else if (failure == InventoryQuickTransferFailure.NoSpace || failure == InventoryQuickTransferFailure.GridPolicy)
            { _hadPolicyBlock |= failure == InventoryQuickTransferFailure.GridPolicy; _blockedItems.Add(candidate.GetInstanceID()); Log("itemBlocked", failure.ToString(), candidate); }
            else throw new InvalidOperationException("Unexpected quick transfer failure: " + failure);
        }
        private void Close(string reason, bool completed)
        {
            if (_current == null) return;
            var screen = InventoryScreenController.Instance;
            if (_session != null && screen != null && screen.IsSessionContextActive(_session))
            {
                var remaining = screen.ActiveExternalGrid.ExtractSaveData();
                var backpack = screen.BackpackGrid.ExtractSaveData();
                _ledger.Check("beforeClose", remaining, backpack);
                screen.CloseInventory();
                if (_box != null) SceneRaidInventoryLedger.RequireSame(remaining, _box.GetSavedItems(), "LootBox close writeback");
                SceneRaidInventoryLedger.RequireSame(backpack, screen.BackpackGrid.ExtractSaveData(), "Backpack close writeback");
                _ledger.Check("afterClose", _box != null ? _box.GetSavedItems() : remaining, screen.BackpackGrid.ExtractSaveData());
                if (!Mathf.Approximately(Time.timeScale, _previousScale) || !Mathf.Approximately(Time.fixedDeltaTime, _previousFixed))
                    throw new InvalidOperationException("Inventory did not restore simulation time.");
            }
            Log(_session != null ? "closed" : "canceled", reason);
            if (completed) { CompletedSessions++; ServedAgents.Add(_current.fact.AgentId); _completedCommands.Add(_current.fact.CommandId); }
            _current = null; _session = null; _box = null;
        }
        private void Log(string kind, string reason, DraggableItemUI item = null)
        {
            if (kind == "transferred" || kind == "closed" || kind == "sorted" || kind == "focus") MutationRevision++;
            _writer.Add("inventory." + kind,
            JsonUtility.ToJson(new Trace { agent = _current.fact.AgentId, commandId = _current.fact.CommandId,
                resource = _identity.Get(_current.fact.Resource), reason = reason, item = item != null ? item.ItemData.ItemID : "",
                amount = item != null ? item.CurrentAmount : 0, progress = item != null ? item.SearchProgressSeconds : 0,
                duration = item != null ? item.SearchDurationSeconds : 0 }));
        }
        public void Dispose()
        {
            AgentResourceInteractionChannel.Published -= Interaction;
            Close("runEnded", false);
        }
    }
}
#endif
