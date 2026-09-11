#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    public static class SceneRaidPersistenceEvidence
    {
        [Serializable] private sealed class Definitions
        {
            public int schemaVersion = 1;
            public string runId;
            public int columns, rows;
            public SceneRaidItemEvidence[] items;
        }
        public static void CaptureWarehouse(string output, string stage)
        {
            string source = Path.Combine(Application.persistentDataPath, "player_storage.json");
            File.WriteAllText(Path.Combine(output, "warehouse-" + stage + ".json"),
                File.Exists(source) ? File.ReadAllText(source) : "{\"Version\":1,\"Players\":[]}");
        }
        public static void CaptureDefinitions(string output, string runId)
        {
            var database = Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase");
            if (database == null) throw new InvalidOperationException("Missing runtime inventory database.");
            var storage = PlayerStorageService.Instance;
            var definitions = new Definitions { runId = runId, columns = storage != null ? storage.Columns : 6,
                rows = storage != null ? storage.Rows : 10,
                items = SceneRaidItemEvidence.Flatten(database.Items.Where(x => x != null && x.IncludeInRuntimeDatabase)
                    .Select(x => new ContainerItemSaveData { ItemData = x, Amount = 1 })) };
            File.WriteAllText(Path.Combine(output, "item-definitions.json"), JsonUtility.ToJson(definitions, true));
        }
    }
}
#endif
