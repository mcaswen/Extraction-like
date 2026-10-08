using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ExtractionLike.Aerospace
{
    /// <summary>Scene-local loot injection. Never edits a scene, navmesh, ordinary loot or an Agent's decisions.</summary>
    public sealed class AerospaceCollectionRuntime : MonoBehaviour
    {
        public const string FormalScene = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        public const string ArchiveKey = "ASTRA_ScienceArchive_v1";
        public static AerospaceCollectionRuntime Instance { get; private set; }
        public const int PartCount = 5;
        public const int PreferredSearchStride = 3;
        public sealed class Reservation { public string code; public int searchNumber; public bool placed; public LootBoxEntity box; public ResourceClusterAuthoring cluster; public Vector3 approach; public string agentId; }
        public readonly List<Reservation> Reservations = new List<Reservation>();
        private readonly Dictionary<LootBoxEntity, Reservation> eligibleBoxes = new Dictionary<LootBoxEntity, Reservation>();
        private readonly HashSet<LootBoxEntity> searchedBoxes = new HashSet<LootBoxEntity>();
        private InventoryItemData[] reservedItems;
        private readonly HashSet<string> collected = new HashSet<string>();
        private readonly Queue<string> pending = new Queue<string>();
        private AerospaceScienceUI ui;
        private AudioSource discovery;
        public int CollectedCount => collected.Count;
        public int SearchedBoxCount => searchedBoxes.Count;
        public int EligibleBoxCount => eligibleBoxes.Count;
        public int PlacedPartCount => Reservations.Count(r => r.placed);
        public bool IsReady { get; private set; }
        [NonSerialized] public bool ReserveOnStart = true;
        public string Status { get; private set; } = "正在核验搜索点…";
        public bool HasCollected(string code) => collected.Contains(code);
        public bool IsUnlocked(string code) => (PlayerPrefs.GetInt(ArchiveKey, 0) & Bit(code)) != 0;
        private static int Bit(string code) => code != null && code.Length == 3 && code[2] >= '1' && code[2] <= '5' ? 1 << (code[2] - '1') : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != FormalScene || Instance != null) return;
            var go = new GameObject("Aerospace_Collection_And_Science");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<AerospaceCollectionRuntime>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            discovery = gameObject.AddComponent<AudioSource>();
            discovery.playOnAwake = false; discovery.spatialBlend = 0; discovery.ignoreListenerPause = true; discovery.volume = .85f;
            discovery.priority = 64; discovery.dopplerLevel = 0;
            discovery.clip = Resources.Load<AudioClip>("Aerospace/RareDiscovery");
            ui = gameObject.AddComponent<AerospaceScienceUI>();
            ui.Initialize(this);
        }
        private IEnumerator Start()
        {
            if (!ReserveOnStart) yield break;
            yield return null;
            for (int attempt = 0; attempt < 30 && !IsReady; attempt++)
            {
                if (TryReserveAll()) break;
                yield return new WaitForSecondsRealtime(.5f);
            }
            if (!IsReady) { Status = "搜索点核验未通过，请查看 Console"; Debug.LogError("[Aerospace] Cannot prepare five parts: fewer than five reachable boxes or missing item assets. No inaccessible or physical props were created."); }
        }

        /// <summary>Reserve a five-part schedule, not loot in the nearest five boxes.</summary>
        public bool TryReserveAll()
        {
            if (IsReady) return true;
            var registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry == null) return false;
            var agents = registry.RegisteredAgents.Where(a => a.IsAlive && a.PawnRoot.NavMeshAgent != null && a.PawnRoot.NavMeshAgent.isOnNavMesh).ToArray();
            if (agents.Length == 0) return false;
            var clusters = FindObjectsOfType<ResourceClusterAuthoring>().Where(c => c.gameObject.scene == gameObject.scene && c.isActiveAndEnabled)
                .OrderBy(c => agents.Min(a => (a.CachedTransform.position - c.transform.position).sqrMagnitude)).ThenBy(c => c.name, StringComparer.Ordinal).ToArray();
            var candidates = new Dictionary<LootBoxEntity, Reservation>();
            var usedBoxes = new HashSet<LootBoxEntity>();
            foreach (var cluster in clusters)
            {
                foreach (var member in cluster.ResourceMembers)
                {
                    var box = member?.EntityObject != null ? member.EntityObject.GetComponent<LootBoxEntity>() : null;
                    if (box == null || box.gameObject.scene != gameObject.scene || !box.isActiveAndEnabled ||
                        usedBoxes.Contains(box) || box.IsResourcePointLooted || AgentSearchedResourceRegistry.IsSearched(box.gameObject)) continue;
                    if (!TryReach(cluster, box, agents, out var point, out var agentId)) continue;
                    usedBoxes.Add(box);
                    candidates.Add(box, new Reservation { box = box, cluster = cluster, approach = point, agentId = agentId });
                }
            }
            // Atomic preflight: don't partially alter boxes if assets/navigation are incomplete.
            if (candidates.Count < PartCount) return false;
            var catalog = AerospaceCatalog.Load();
            if (catalog == null || catalog.models == null || catalog.models.Length != PartCount) return false;
            var items = catalog.models.Select(p => Resources.Load<InventoryItemData>("Aerospace/Items/" + p.code)).ToArray();
            if (items.Any(i => i == null) || catalog.models.Select(p => p.code).Distinct().Count() != PartCount) return false;
            int[] milestones = BuildSearchMilestones(candidates.Count);
            foreach (var candidate in candidates) eligibleBoxes.Add(candidate.Key, candidate.Value);
            reservedItems = items;
            for (int i = 0; i < PartCount; i++)
            {
                Reservations.Add(new Reservation { code = catalog.models[i].code, searchNumber = milestones[i] });
            }
            IsReady = true;
            Status = "首搜保底 · 沿实际探索路线分批投放";
            Debug.Log("[Aerospace] Prepared five distinct parts for new-box searches " + string.Join(",", milestones) +
                " across " + EligibleBoxCount + " reachable boxes; no NavMesh/AI changes.");
            return true;
        }

        public static int[] BuildSearchMilestones(int reachableBoxCount)
        {
            if (reachableBoxCount < PartCount) return Array.Empty<int>();
            int last = Math.Min(reachableBoxCount, 1 + PreferredSearchStride * (PartCount - 1));
            // A smaller future map still fits all five parts; normal maps use 1,4,7,10,13.
            return Enumerable.Range(0, PartCount).Select(i => 1 + i * (last - 1) / (PartCount - 1)).ToArray();
        }

        /// <summary>
        /// Called only on actual arrival/search or opening a box, never by loot previews,
        /// AI target scoring, navigation checks or GetSavedItems. Both agents share this ledger.
        /// </summary>
        public bool PrepareSearchedBox(LootBoxEntity box)
        {
            if (box == null || !box.isActiveAndEnabled || box.gameObject.scene != gameObject.scene ||
                box.IsResourcePointLooted || AgentSearchedResourceRegistry.IsSearched(box.gameObject)) return false;
            if (!IsReady && !TryReserveAll()) return false;
            if (!eligibleBoxes.TryGetValue(box, out var candidate) || !searchedBoxes.Add(box)) return false;

            int index = Reservations.FindIndex(r => !r.placed);
            if (index < 0 || SearchedBoxCount < Reservations[index].searchNumber) return false;

            var reservation = Reservations[index];
            AddReservedLoot(box, reservedItems[index]);
            reservation.placed = true;
            reservation.box = box;
            reservation.cluster = candidate.cluster;
            reservation.approach = candidate.approach;
            reservation.agentId = candidate.agentId;
            Debug.Log("[Aerospace] Search " + SearchedBoxCount + ": placed " + reservation.code + " in " + box.name +
                "; discovery/unlock still requires a successful backpack transfer.");
            return true;
        }
        public static bool TryReach(ResourceClusterAuthoring cluster, LootBoxEntity box, AgentRuntimeHandle[] agents, out Vector3 point, out string agentId)
        {
            var positions = new List<Vector3>(); var path = new NavMeshPath();
            foreach (var agent in agents)
            {
                var nav = agent.PawnRoot.NavMeshAgent;
                if (nav == null || !nav.isOnNavMesh || !cluster.TryCopyNavigationApproachCandidates(box.gameObject, nav.transform.position, positions)) continue;
                foreach (var candidate in positions)
                    if (nav.CalculatePath(candidate, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 0)
                    { point = candidate; agentId = agent.AgentId.Value; return true; }
            }
            point = default; agentId = null; return false;
        }
        public static void AddReservedLoot(LootBoxEntity box, InventoryItemData item)
        {
            var contents = box.GetSavedItems();
            if (contents.Any(c => c.ItemData != null && c.ItemData.ItemID == item.ItemID)) return;
            var cells = box.GetSavedCellStates();
            int row = box.ContainerRows;
            // An additional runtime row preserves every generated ordinary item and blocked cell.
            box.ContainerRows = row + 1;
            contents.Add(new ContainerItemSaveData { ItemData = item, Amount = 1, RuntimeItemId = Guid.NewGuid().ToString("N"), X = 0, Y = row,
                RequiresSearch = true, IsSearched = false, SearchDurationSeconds = 2.1f });
            box.SaveRuntimeState(contents, cells);
            if (!box.BoxName.Contains("航天")) box.BoxName += " · 航天样件";
        }
        public static void NotifyTransfer(DraggableItemUI item, InventoryUIController source, InventoryUIController destination)
        {
            var screen = InventoryScreenController.Instance;
            if (Instance == null || item == null || screen == null || screen.UsesCustomPlayerInventory || !screen.IsInventoryOpen ||
                source == null || source != screen.ActiveExternalGrid || destination != screen.BackpackGrid || !item.IsSearched) return;
            Instance.RecordSuccessfulPickup(item.ItemData, screen.ActiveInventoryAgentId);
        }
        public static void NotifyWorldPickup(InventoryItemData item)
        {
            var screen = InventoryScreenController.Instance;
            if (Instance != null && screen != null && !screen.UsesCustomPlayerInventory) Instance.RecordSuccessfulPickup(item, screen.ActiveInventoryAgentId);
        }
        public void RecordSuccessfulPickup(InventoryItemData item, string agentId)
        {
            string code = AerospaceCatalog.CodeFor(item);
            if (code == null || string.IsNullOrEmpty(agentId) || !collected.Add(code)) return;
            PlayerPrefs.SetInt(ArchiveKey, PlayerPrefs.GetInt(ArchiveKey, 0) | Bit(code)); PlayerPrefs.Save();
            // One dedicated voice: rapid successful pickups retrigger instead of stacking
            // several full-level impacts. Never change global or ordinary-loot audio volume.
            if (discovery.clip != null) discovery.Play();
            pending.Enqueue(code);
            Debug.Log("[Aerospace] " + agentId + " recovered " + code + "; collection " + collected.Count + "/5.");
        }
        private void Update()
        {
            if (pending.Count > 0 && !ui.IsOpen && DraggableItemUI.CurrentlyDraggedItem == null && !RaidLocked)
                ui.Open(pending.Dequeue(), true);
            if (Input.GetKeyDown(KeyCode.J) && !RaidLocked && DraggableItemUI.CurrentlyDraggedItem == null)
            { if (ui.IsOpen) ui.Close(); else ui.Open(null, false); }
        }
        public static bool RaidLocked => RaidFlowController.Instance != null && RaidFlowController.Instance.IsInputLocked;
        public static void OpenOwnedItem(DraggableItemUI item)
        {
            var screen = InventoryScreenController.Instance;
            string code = item != null ? AerospaceCatalog.CodeFor(item.ItemData) : null;
            if (Instance != null && code != null && screen != null && item.CurrentGrid == screen.BackpackGrid && Instance.IsUnlocked(code))
                Instance.ui.Open(code, false);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
