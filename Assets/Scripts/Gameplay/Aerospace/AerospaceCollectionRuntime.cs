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
        public sealed class Reservation { public string code; public LootBoxEntity box; public ResourceClusterAuthoring cluster; public Vector3 approach; public string agentId; }
        public readonly List<Reservation> Reservations = new List<Reservation>();
        private readonly HashSet<string> collected = new HashSet<string>();
        private readonly Queue<string> pending = new Queue<string>();
        private AerospaceScienceUI ui;
        private AudioSource discovery;
        public int CollectedCount => collected.Count;
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
            if (!IsReady) { Status = "搜索点核验未通过，请查看 Console"; Debug.LogError("[Aerospace] Cannot guarantee five reachable parts: " + Reservations.Count + "/5. No inaccessible or physical props were created."); }
        }

        public bool TryReserveAll()
        {
            if (IsReady) return true;
            var registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry == null) return false;
            var agents = registry.RegisteredAgents.Where(a => a.IsAlive && a.PawnRoot.NavMeshAgent != null && a.PawnRoot.NavMeshAgent.isOnNavMesh).ToArray();
            if (agents.Length == 0) return false;
            var clusters = FindObjectsOfType<ResourceClusterAuthoring>().Where(c => c.gameObject.scene == gameObject.scene && c.isActiveAndEnabled)
                .OrderBy(c => agents.Min(a => (a.CachedTransform.position - c.transform.position).sqrMagnitude)).ThenBy(c => c.name, StringComparer.Ordinal).ToArray();
            var candidates = new List<Reservation>();
            var usedBoxes = new HashSet<LootBoxEntity>();
            foreach (var existing in Reservations) usedBoxes.Add(existing.box);
            // Prefer different resource clusters, then allow more than one member of a reachable cluster.
            for (int pass = 0; pass < 2 && candidates.Count + Reservations.Count < 5; pass++)
            foreach (var cluster in clusters)
            {
                if (pass == 0 && Reservations.Any(r => r.cluster == cluster)) continue;
                foreach (var member in cluster.ResourceMembers)
                {
                    var box = member?.EntityObject != null ? member.EntityObject.GetComponent<LootBoxEntity>() : null;
                    if (box == null || !box.isActiveAndEnabled || usedBoxes.Contains(box) || box.IsResourcePointLooted) continue;
                    if (!TryReach(cluster, box, agents, out var point, out var agentId)) continue;
                    candidates.Add(new Reservation { box = box, cluster = cluster, approach = point, agentId = agentId });
                    usedBoxes.Add(box);
                    if (pass == 0 || candidates.Count + Reservations.Count >= 5) break;
                }
                if (candidates.Count + Reservations.Count >= 5) break;
            }
            // Atomic preflight: don't partially alter boxes if assets/navigation are incomplete.
            if (candidates.Count + Reservations.Count < 5) return false;
            var catalog = AerospaceCatalog.Load();
            var items = catalog.models.Select(p => Resources.Load<InventoryItemData>("Aerospace/Items/" + p.code)).ToArray();
            if (items.Any(i => i == null)) return false;
            foreach (var candidate in candidates)
            {
                int index = Reservations.Count;
                if (index >= 5) break;
                candidate.code = catalog.models[index].code;
                AddReservedLoot(candidate.box, items[index]);
                Reservations.Add(candidate);
            }
            IsReady = Reservations.Count == 5;
            Status = IsReady ? "五种零件已保底投放 · 沿用原搜索箱" : "正在核验搜索点…";
            Debug.Log("[Aerospace] Reserved " + Reservations.Count + " distinct parts in reachable existing containers; no NavMesh/AI changes.");
            return IsReady;
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
