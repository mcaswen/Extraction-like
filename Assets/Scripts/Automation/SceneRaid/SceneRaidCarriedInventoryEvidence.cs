#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace AnomalySearch.Automation.SceneRaid
{
    /// <summary>独立读取携带物，不切焦点、不同步/创建业务快照，不调用会关闭会话的结算查询。</summary>
    public static class SceneRaidCarriedInventoryEvidence
    {
        [Serializable] public sealed class Record
        {
            public int schemaVersion = 1;
            public string agent, source, reason;
            public bool available;
            public SceneRaidItemEvidence[] backpack, equipped;
        }
        private static readonly FieldInfo Snapshots = typeof(InventoryScreenController)
            .GetField("_inventorySnapshotsByAgentId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        private static readonly string[] Equipment = { "HeadItem", "BodyItem", "FaceItem", "HeadphoneItem", "TotemAItem", "TotemBItem" };

        public static Record Capture(InventoryScreenController screen, string agent)
        {
            var row = new Record { agent = agent, backpack = Array.Empty<SceneRaidItemEvidence>(), equipped = Array.Empty<SceneRaidItemEvidence>() };
            if (screen == null) { row.reason = "InventoryNotReady"; return row; }
            if (string.IsNullOrWhiteSpace(agent)) throw new ArgumentException("Carried inventory requires an agent.");
            if (screen.ActiveInventoryAgentId == agent)
            {
                if (screen.UsesCustomPlayerInventory || screen.BackpackGrid == null)
                { row.reason = "LiveCharacterInventoryUnavailable"; return row; }
                row.backpack = SceneRaidItemEvidence.Flatten(screen.BackpackGrid.ExtractSaveData());
                row.equipped = SceneRaidItemEvidence.Equipped(screen);
                row.source = "ActiveCharacterUI";
            }
            else
            {
                if (Snapshots == null || !(Snapshots.GetValue(screen) is IDictionary snapshots))
                    throw new MissingFieldException("Inventory snapshot schema changed.");
                if (!snapshots.Contains(agent) || snapshots[agent] == null)
                { row.reason = "MissingCharacterSnapshot"; return row; }
                object snapshot = snapshots[agent];
                var bag = Read<ContainerItemSaveData>(snapshot, "BackpackItem");
                row.backpack = SceneRaidItemEvidence.Flatten(bag != null ? bag.InternalItems : Read<List<ContainerItemSaveData>>(snapshot, "BackpackLooseItems"));
                var equipped = new List<ContainerItemSaveData>();
                foreach (string name in Equipment)
                {
                    var item = Read<ContainerItemSaveData>(snapshot, name);
                    if (item != null) equipped.Add(item);
                }
                row.equipped = SceneRaidItemEvidence.Flatten(equipped);
                row.source = "StoredCharacterSnapshot";
            }
            row.available = true; row.reason = "Captured";
            return row;
        }

        private static T Read<T>(object owner, string name)
        {
            string key = owner.GetType().FullName + "." + name;
            if (!Fields.TryGetValue(key, out var field))
            {
                field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
                if (field == null || !typeof(T).IsAssignableFrom(field.FieldType))
                    throw new MissingFieldException("Unexpected character snapshot field: " + key);
                Fields.Add(key, field);
            }
            return (T)field.GetValue(owner);
        }
    }
}
#endif
