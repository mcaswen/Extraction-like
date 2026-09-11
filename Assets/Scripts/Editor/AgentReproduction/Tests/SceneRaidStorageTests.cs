using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidStorageTests : ReproductionTestFixture
    {
        private sealed class IsolatedSave : IDisposable
        {
            public readonly string Path = System.IO.Path.Combine(Application.persistentDataPath, "player_storage.json");
            private readonly byte[] _original;
            public IsolatedSave()
            {
                Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
                Assert.That(Application.productName, Does.StartWith("AgentRepro_"));
                _original = File.Exists(Path) ? File.ReadAllBytes(Path) : null;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                if (_original != null) File.Delete(Path);
                InventoryItemDatabase.SetRuntimeDatabase(null);
            }
            public void Dispose()
            {
                if (_original == null) File.Delete(Path);
                else File.WriteAllBytes(Path, _original);
                InventoryItemDatabase.SetRuntimeDatabase(null);
            }
        }
        private PlayerStorageService Storage()
        {
            var storage = World.Root("Storage fixture").AddComponent<PlayerStorageService>();
            storage.Configure(6, 10, 1);
            storage.LoadForAgent("StorageProbe");
            return storage;
        }
        private static List<ContainerItemSaveData> Items(params InventoryItemData[] data) => data.Select(x =>
            new ContainerItemSaveData { ItemData = x, RuntimeItemId = Guid.NewGuid().ToString("N"), Amount = 1 }).ToList();
        private static void CheckLayout(PlayerStorageService storage)
        {
            for (int page = 0; page < storage.PageCount; page++)
            {
                var occupied = new HashSet<Vector2Int>();
                foreach (var item in storage.GetPageItems(page))
                {
                    int width = item.IsRotated ? item.ItemData.Height : item.ItemData.Width;
                    int height = item.IsRotated ? item.ItemData.Width : item.ItemData.Height;
                    for (int x = item.X; x < item.X + width; x++)
                        for (int y = item.Y; y < item.Y + height; y++)
                        {
                            Assert.That(x, Is.InRange(0, storage.Columns - 1));
                            Assert.That(y, Is.InRange(0, storage.Rows - 1));
                            Assert.That(occupied.Add(new Vector2Int(x, y)), Is.True, "Stored items overlap.");
                        }
                }
            }
        }

        [UnityTest]
        public IEnumerator EverySceneLootDefinitionRoundTripsThroughStorage()
        {
            using (var save = new IsolatedSave())
            {
                var rules = Resources.Load<SceneResourceLootRuleSet>("Loot/SO_SceneResourceLootRuleSet");
                Assert.That(rules, Is.Not.Null);
                var definitions = rules.LootTable.Select(x => x.ItemData).Distinct().ToArray();
                Assert.That(definitions.Length, Is.GreaterThan(0));
                foreach (var data in definitions)
                {
                    Assert.That(InventoryItemDatabase.TryResolve(data.ItemID, out var restored), Is.True, data.name);
                    Assert.That(restored, Is.SameAs(data));
                }
                var storage = Storage();
                var items = Items(definitions);
                Assert.That(storage.TryAppendItemsToAgentStorage("StorageProbe", items, out int count, out int value), Is.True);
                Assert.That(count, Is.EqualTo(items.Count));
                Assert.That(value, Is.EqualTo(definitions.Sum(x => Mathf.Max(0, x.SellPrice))));
                Object.DestroyImmediate(storage.gameObject);
                storage = Storage(); // 新实例强制从真实 JSON 回读。
                var restoredItems = Enumerable.Range(0, storage.PageCount).SelectMany(storage.GetPageItems).ToList();
                Assert.That(restoredItems.Select(x => x.RuntimeItemId), Is.EquivalentTo(items.Select(x => x.RuntimeItemId)));
                Assert.That(restoredItems.Select(x => x.ItemData), Is.EquivalentTo(definitions));
                Assert.That(restoredItems.Sum(x => x.Amount), Is.EqualTo(count));
                CheckLayout(storage);
                ContractCompleted = true;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnknownInputRejectsWholeAppendWithoutChangingSavedItems()
        {
            using (var save = new IsolatedSave())
            {
                var known = Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase").Items.First(x => x.IncludeInRuntimeDatabase);
                var storage = Storage();
                Assert.That(storage.TryAppendItemsToAgentStorage("StorageProbe", Items(known), out _, out _), Is.True);
                string before = File.ReadAllText(save.Path);
                var unknown = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
                unknown.ItemID = "UnregisteredStorageProbe"; unknown.Width = 1; unknown.Height = 1;
                Assert.That(storage.TryAppendItemsToAgentStorage("StorageProbe", Items(known, unknown), out int count, out int value), Is.False);
                Assert.That(count, Is.Zero); Assert.That(value, Is.Zero);
                Assert.That(File.ReadAllText(save.Path), Is.EqualTo(before));
                Assert.That(Enumerable.Range(0, storage.PageCount).SelectMany(storage.GetPageItems).Count(), Is.EqualTo(1));
                ContractCompleted = true;
            }
            yield return null;
        }

        public static string[] BadPages = { "Unknown", "Overlap", "Blocked" };
        [UnityTest]
        public IEnumerator ExistingPageDoesNotExposeUnavailableCells([ValueSource(nameof(BadPages))] string defect)
        {
            using (var save = new IsolatedSave())
            {
                var known = Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase").Items.First(x =>
                    x.IncludeInRuntimeDatabase && x.Width == 1 && x.Height == 1);
                var storage = Storage();
                storage.SetPageRuntimeState(0, defect == "Blocked" ? new List<ContainerItemSaveData>() :
                    Items(defect == "Overlap" ? new[] { known, known } : new[] { known }),
                    defect == "Blocked" ? new List<ContainerCellStateSaveData> { new ContainerCellStateSaveData { X = 0, Y = 0, State = GridState.Blocked } } : null);
                storage.Save();
                if (defect == "Unknown")
                {
                    string json = File.ReadAllText(save.Path).Replace(known.ItemID, "MissingOldStorageItem");
                    File.WriteAllText(save.Path, json);
                    Object.DestroyImmediate(storage.gameObject);
                    storage = Storage();
                }
                Assert.That(storage.TryAppendItemsToAgentStorage("StorageProbe", Items(known), out int count, out _), Is.True);
                Assert.That(count, Is.EqualTo(1));
                if (defect == "Blocked")
                {
                    var added = storage.GetPageItems(0).Single();
                    Assert.That(new Vector2Int(added.X, added.Y), Is.Not.EqualTo(Vector2Int.zero));
                    Assert.That(storage.GetPageCellStates(0).Single().State, Is.EqualTo(GridState.Blocked));
                }
                else
                {
                    Assert.That(storage.GetPageItems(1).Count, Is.EqualTo(1), "Invalid old page must remain untouched; append on a clean page.");
                    if (defect == "Unknown") Assert.That(File.ReadAllText(save.Path), Does.Contain("MissingOldStorageItem"));
                    else Assert.That(storage.GetPageItems(0).Count, Is.EqualTo(2));
                }
                ContractCompleted = true;
            }
            yield return null;
        }
    }
}
