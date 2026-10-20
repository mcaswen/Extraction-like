using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using Gameplay.Targets.Authoring;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ExtractionLike.Aerospace
{
    /// <summary>Scene-local loot injection. Never edits a scene, navmesh, ordinary loot or an Agent's decisions.</summary>
    public sealed class AerospaceCollectionRuntime : MonoBehaviour
    {
        public const string FormalScene = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        // Deprecated preference identifiers, retained only for compatibility regression
        // checks. Collection/entry state is scene-local; never read or write these keys.
        public const string ArchiveKey = "ASTRA_ScienceArchive_v1";
        public const string DiscoverySeenKey = "AerospaceScience.DiscoverySeen.v1";
        public const KeyCode GrantShortcut = KeyCode.F8;
        public static AerospaceCollectionRuntime Instance { get; private set; }
        public const int PartCount = 5;
        public const int PreferredSearchStride = 3;
        public const string GridFinCode = "R04";
        // Rainforest's southwest resource node, directly connected to its extraction node.
        // Use the map's stable direct binding: prefab runtime TargetIds may be regenerated.
        public const string GridFinMapNodeId = "cluster_e351d521b0b236a2292a54c00835880b";
        public sealed class Reservation { public string code; public int searchNumber; public bool placed; public LootBoxEntity box; public ResourceClusterAuthoring cluster; public ResourceClusterAuthoring requiredCluster; public Vector3 approach; public string agentId; }
        public readonly List<Reservation> Reservations = new List<Reservation>();
        private readonly Dictionary<LootBoxEntity, Reservation> eligibleBoxes = new Dictionary<LootBoxEntity, Reservation>();
        private readonly HashSet<LootBoxEntity> searchedBoxes = new HashSet<LootBoxEntity>();
        private InventoryItemData[] reservedItems;
        private int[] searchMilestones;
        private readonly HashSet<string> collected = new HashSet<string>();
        private readonly HashSet<string> discoveriesSeen = new HashSet<string>();
        private sealed class DiscoveryRequest { public string code; public Rect? origin; }
        private readonly Queue<DiscoveryRequest> pending = new Queue<DiscoveryRequest>();
        private readonly HashSet<string> queuedDiscoveries = new HashSet<string>();
        private readonly HashSet<string> recoveredNaturally = new HashSet<string>();
        private readonly Dictionary<string, HashSet<string>> grantedParts = new Dictionary<string, HashSet<string>>();
        private bool grantingParts;
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
        // A new runtime (new game/scene or entering Play again) always starts locked.
        // Only successful backpack acquisition records collection, never opening J.
        public bool IsUnlocked(string code) => collected.Contains(code);
        public bool HasSeenDiscovery(string code) => discoveriesSeen.Contains(code);
        public bool NeedsDiscovery(string code) => code != null && recoveredNaturally.Contains(code) && !HasSeenDiscovery(code);
        public void CompleteDiscovery(string code)
        {
            if (IsUnlocked(code)) discoveriesSeen.Add(code);
        }

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
            if (!IsReady) { Status = "搜索点核验未通过，请查看 Console"; Debug.LogError("[Aerospace] Cannot prepare five parts: check reachable boxes, the rainforest grid-fin node binding and item assets. No inaccessible or physical props were created."); }
        }

        /// <summary>Silent Shift+F8 demonstration shortcut; each Agent can receive each part once per run.</summary>
        public bool TryGrantPartsToCurrentBackpack()
        {
            var inventory = InventoryScreenController.Instance;
            if (inventory == null || inventory.BackpackGrid == null || InventoryItemFactory.Instance == null ||
                inventory.UsesCustomPlayerInventory || string.IsNullOrEmpty(inventory.ActiveInventoryAgentId) ||
                RaidLocked || ui.IsOpen || DraggableItemUI.CurrentlyDraggedItem != null) return false;
            var catalog = AerospaceCatalog.Load();
            if (catalog?.models == null || catalog.models.Length != PartCount) return false;
            string agentId = inventory.ActiveInventoryAgentId;
            if (!grantedParts.TryGetValue(agentId, out var granted))
                grantedParts.Add(agentId, granted = new HashSet<string>());
            var owned = new HashSet<string>(inventory.BackpackGrid.ExtractSaveData().Select(i => AerospaceCatalog.CodeFor(i.ItemData)));
            grantingParts = true;
            try
            {
                foreach (var part in catalog.models)
                {
                    if (granted.Contains(part.code)) continue;
                    var item = Resources.Load<InventoryItemData>("Aerospace/Items/" + part.code);
                    if (item == null) { Debug.LogError("[Aerospace] Missing part asset: " + part.code); continue; }
                    if (!owned.Contains(part.code) && !inventory.TryPickupItem(item, 1)) continue;
                    granted.Add(part.code);
                    RecordSuccessfulPickup(item, agentId);
                }
            }
            finally { grantingParts = false; }
            return granted.Count == PartCount;
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
            if (!TryResolveGridFinCluster(out var gridFinCluster) ||
                (gridFinCluster != null && !candidates.Values.Any(c => c.cluster == gridFinCluster))) return false;
            var catalog = AerospaceCatalog.Load();
            if (catalog == null || catalog.models == null || catalog.models.Length != PartCount) return false;
            var items = catalog.models.Select(p => Resources.Load<InventoryItemData>("Aerospace/Items/" + p.code)).ToArray();
            if (items.Any(i => i == null) || catalog.models.Select(p => p.code).Distinct().Count() != PartCount) return false;
            searchMilestones = BuildSearchMilestones(candidates.Count);
            foreach (var candidate in candidates) eligibleBoxes.Add(candidate.Key, candidate.Value);
            reservedItems = items;
            for (int i = 0; i < PartCount; i++)
            {
                Reservations.Add(new Reservation { code = catalog.models[i].code, searchNumber = searchMilestones[i],
                    requiredCluster = catalog.models[i].code == GridFinCode ? gridFinCluster : null });
            }
            IsReady = true;
            Status = gridFinCluster != null ? "栅格舵固定雨林搜索点 · 其余样件分批投放" : "首搜保底 · 沿实际探索路线分批投放";
            Debug.Log("[Aerospace] Prepared five distinct parts for new-box searches " + string.Join(",", searchMilestones) +
                " across " + EligibleBoxCount + " reachable boxes; grid fin node=" +
                (gridFinCluster != null ? GridFinMapNodeId : "unbound test/other scene") + "; no NavMesh/AI changes.");
            return true;
        }

        private bool TryResolveGridFinCluster(out ResourceClusterAuthoring cluster)
        {
            cluster = null;
            var bindings = FindObjectsOfType<MapGraphBindingAuthoring>()
                .Where(b => b.gameObject.scene == gameObject.scene)
                .SelectMany(b => b.TargetBindings)
                .Where(b => b != null && b.NodeId == GridFinMapNodeId).ToArray();
            // The formal map must never silently put R04 elsewhere if its binding breaks.
            if (bindings.Length == 0) return gameObject.scene.path != FormalScene;
            if (bindings.Length != 1) return false;
            cluster = bindings[0].DirectTarget as ResourceClusterAuthoring;
            return cluster != null && cluster.isActiveAndEnabled && cluster.gameObject.scene == gameObject.scene;
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

            // First actual search in the designated group owns R04, even on the first
            // search of a run. No preview, preflight or other group can consume it.
            int index = Reservations.FindIndex(r => !r.placed && r.requiredCluster != null && r.requiredCluster == candidate.cluster);
            if (index < 0)
            {
                index = Reservations.FindIndex(r => !r.placed && r.requiredCluster == null);
                if (index < 0 || SearchedBoxCount < searchMilestones[PlacedPartCount]) return false;
            }

            var reservation = Reservations[index];
            AddReservedLoot(box, reservedItems[index]);
            reservation.placed = true;
            reservation.searchNumber = SearchedBoxCount;
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
            Instance.RecordSuccessfulPickup(item.ItemData, screen.ActiveInventoryAgentId, NormalizedPickupRect(item.transform as RectTransform));
        }
        public static void NotifyWorldPickup(InventoryItemData item)
        {
            var screen = InventoryScreenController.Instance;
            if (Instance != null && screen != null && !screen.UsesCustomPlayerInventory) Instance.RecordSuccessfulPickup(item, screen.ActiveInventoryAgentId);
        }
        // Normalized screen coordinates survive a resolution change while a discovery is queued.
        public static Rect? NormalizedPickupRect(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy || Screen.width <= 0 || Screen.height <= 0) return null;
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            foreach (var corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner) / new Vector2(Screen.width, Screen.height);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            if (max.x <= 0 || min.x >= 1 || max.y <= 0 || min.y >= 1 || (max-min).sqrMagnitude < .000001f) return null;
            return Rect.MinMaxRect(Mathf.Clamp01(min.x), Mathf.Clamp01(min.y), Mathf.Clamp01(max.x), Mathf.Clamp01(max.y));
        }
        public void RecordSuccessfulPickup(InventoryItemData item, string agentId, Rect? origin = null)
        {
            string code = AerospaceCatalog.CodeFor(item);
            if (code == null || string.IsNullOrEmpty(agentId)) return;
            bool firstThisRound = collected.Add(code);
            // One dedicated voice: rapid successful pickups retrigger instead of stacking
            // several full-level impacts. Never change global or ordinary-loot audio volume.
            // The manual grant unlocks the archive without opening five discovery windows.
            if (!grantingParts)
            {
                recoveredNaturally.Add(code);
                if (firstThisRound && discovery.clip != null) discovery.Play();
                // A successful transfer owns discovery in this run. Old saved progress
                // and opening the archive cannot consume it in a later run.
                if (NeedsDiscovery(code) && queuedDiscoveries.Add(code))
                    pending.Enqueue(new DiscoveryRequest { code = code, origin = origin });
            }
            Debug.Log("[Aerospace] " + agentId + " recovered " + code + "; collection " + collected.Count + "/5.");
        }
        private void Update()
        {
            if (pending.Count > 0 && !ui.IsOpen && DraggableItemUI.CurrentlyDraggedItem == null && !RaidLocked)
            {
                // Manual inspection can have completed the same pending discovery.
                while (pending.Count > 0 && HasSeenDiscovery(pending.Peek().code))
                    queuedDiscoveries.Remove(pending.Dequeue().code);
                if (pending.Count > 0)
                {
                    var request = pending.Dequeue();
                    ui.Open(request.code, true, request.origin);
                    // Keep the deduplication guard until this presentation is acknowledged.
                    if (ui.IsOpen) return; // Do not consume the triggering frame's archive shortcut again.
                }
            }
            if (TypingInInputField()) return;
            if (Input.GetKeyDown(GrantShortcut) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                TryGrantPartsToCurrentBackpack();
            if (Input.GetKeyDown(KeyCode.J) && !RaidLocked && DraggableItemUI.CurrentlyDraggedItem == null)
            { if (ui.IsOpen) ui.Close(); else ui.Open(null, false); }
        }
        private static bool TypingInInputField()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && (selected.GetComponentInParent<InputField>() != null || selected.GetComponentInParent<TMP_InputField>() != null);
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
