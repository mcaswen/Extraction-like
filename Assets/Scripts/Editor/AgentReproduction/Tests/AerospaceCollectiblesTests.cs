using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using ExtractionLike.Aerospace;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace AgentReproduction.Tests
{
    public sealed class AerospaceCollectiblesTests : ReproductionTestFixture
    {
        AerospaceCollectionRuntime Runtime()
        {
            PlayerPrefs.DeleteKey(AerospaceCollectionRuntime.ArchiveKey); // This fixture asserts the isolated test product first.
            var runtime = World.Root("Aerospace integration fixture").AddComponent<AerospaceCollectionRuntime>();
            runtime.ReserveOnStart = false; return runtime;
        }
        InventoryItemData Part(string code) => Resources.Load<InventoryItemData>("Aerospace/Items/" + code);
        static DraggableItemUI Source(InventoryScreenController screen, string code) => screen.ActiveExternalGrid.ItemContainer
            .GetComponentsInChildren<DraggableItemUI>().Single(v => v.CurrentGrid == screen.ActiveExternalGrid && AerospaceCatalog.CodeFor(v.ItemData) == code);
        static IEnumerator WaitFor(Func<bool> ready, float seconds = 12)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!ready() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(ready(), Is.True, "Bounded wait timed out.");
        }
        void OpenParts(InventoryScreenController screen, params string[] codes)
        {
            screen.OpenInventorySession(new InventoryScreenSessionContext { DisplayName = "Aerospace integration search", ExternalColumns = 6, ExternalRows = 2,
                ExternalItems = codes.Select((code, i) => new ContainerItemSaveData { ItemData = Part(code), Amount = 1, X = i, Y = 0,
                    RuntimeItemId = Guid.NewGuid().ToString("N"), RequiresSearch = true, IsSearched = false, SearchDurationSeconds = .12f }).ToList() });
        }
        LootBoxEntity[] PacingBoxes(int count)
        {
            var ordinary = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            ordinary.ItemID = "pacing-ordinary"; ordinary.Width = ordinary.Height = 1;
            var boxes = Enumerable.Range(0, count).Select(i =>
            {
                var box = World.Root("Pacing box " + i).AddComponent<LootBoxEntity>();
                box.transform.position = new Vector3(4 + i * 2, 0, 0);
                box.ContainerColumns = 6; box.ContainerRows = 3; box.UseBoardGameResourceRules = false;
                box.SaveRuntimeState(new List<ContainerItemSaveData> { new ContainerItemSaveData {
                    ItemData = ordinary, Amount = 1, X = 2, Y = 1, IsSearched = true, RuntimeItemId = "ordinary-" + i
                } }, new List<ContainerCellStateSaveData>());
                return box;
            }).ToArray();
            TargetFactory.ResourceBoxes(World, boxes);
            return boxes;
        }

        [UnityTest]
        public IEnumerator SearchMilestonesFitSmallMapsAndKeepTheNormalThreeBoxCadence()
        {
            CollectionAssert.AreEqual(new[] { 1, 4, 7, 10, 13 }, AerospaceCollectionRuntime.BuildSearchMilestones(50));
            CollectionAssert.AreEqual(new[] { 1, 3, 5, 7, 9 }, AerospaceCollectionRuntime.BuildSearchMilestones(9));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, AerospaceCollectionRuntime.BuildSearchMilestones(5));
            Assert.That(AerospaceCollectionRuntime.BuildSearchMilestones(4), Is.Empty);
            for (int total = 5; total <= 40; total++)
            {
                int[] schedule = AerospaceCollectionRuntime.BuildSearchMilestones(total);
                Assert.That(schedule.Length, Is.EqualTo(5)); Assert.That(schedule[0], Is.EqualTo(1));
                Assert.That(schedule.Distinct().Count(), Is.EqualTo(5));
                Assert.That(schedule.Last(), Is.LessThanOrEqualTo(Math.Min(13, total)));
                var gaps = schedule.Skip(1).Select((value, i) => value - schedule[i]).ToArray();
                Assert.That(gaps.Max() - gaps.Min(), Is.LessThanOrEqualTo(1));
            }
            yield return null; ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator FirstOpenedBoxIsGuaranteedThenSharedSearchesOneFourSevenTenThirteen()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero); AgentFactory.Create(World, "2", new Vector3(0, 0, 4));
            var screen = InventoryFactory.Create(World); var boxes = PacingBoxes(16); var runtime = Runtime();
            yield return null;
            Assert.That(runtime.TryReserveAll(), Is.True);
            Assert.That(runtime.PlacedPartCount, Is.Zero, "Preflight must not put every part in nearby boxes.");
            foreach (var box in boxes) { box.GetSavedItems(); box.CreateInventorySessionContext(); }
            Assert.That(runtime.SearchedBoxCount, Is.Zero, "Read-only previews cannot consume the first-search guarantee.");
            // Reverse the authoring order: the first actual search, not the nearest preselected box, wins.
            boxes = boxes.Reverse().ToArray();
            for (int i = 0; i < boxes.Length; i++)
            {
                Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(i % 2 == 0 ? "1" : "2"), Is.True);
                yield return null;
                screen.OpenLootBox(boxes[i]);
                bool expected = i <= 12 && i % 3 == 0;
                var parts = screen.ActiveExternalGrid.ExtractSaveData().Where(v => AerospaceCatalog.CodeFor(v.ItemData) != null).ToArray();
                Assert.That(parts.Length, Is.EqualTo(expected ? 1 : 0), "New box number " + (i + 1));
                if (expected)
                {
                    Assert.That(AerospaceCatalog.CodeFor(parts[0].ItemData), Is.EqualTo("R0" + (1 + i / 3)));
                    Assert.That(parts[0].RequiresSearch && !parts[0].IsSearched, Is.True);
                }
                Assert.That(runtime.SearchedBoxCount, Is.EqualTo(i + 1));
                screen.CloseInventory();
                screen.OpenLootBox(boxes[i]); screen.CloseInventory();
                Assert.That(runtime.PrepareSearchedBox(boxes[i]), Is.False);
                Assert.That(runtime.SearchedBoxCount, Is.EqualTo(i + 1), "Reopens and the other Agent must not advance the cadence.");
                Assert.That(boxes[i].ContainerRows, Is.EqualTo(expected ? 4 : 3));
                Assert.That(boxes[i].GetSavedItems().Single(v => v.RuntimeItemId.StartsWith("ordinary-", StringComparison.Ordinal)).Amount, Is.EqualTo(1));
            }
            Assert.That(runtime.PlacedPartCount, Is.EqualTo(5)); Assert.That(runtime.CollectedCount, Is.Zero);
            Assert.That(runtime.Reservations.Select(r => r.box).Distinct().Count(), Is.EqualTo(5));
            Assert.That(PlayerPrefs.GetInt(AerospaceCollectionRuntime.ArchiveKey, 0), Is.Zero);
            Assert.That(runtime.GetComponent<AerospaceScienceUI>().IsOpen, Is.False);
            Assert.That(runtime.GetComponent<AudioSource>().isPlaying, Is.False);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator RealAgentArrivalPlacesFirstPartBeforeInventoryAndReopenDoesNotAdvance()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var screen = InventoryFactory.Create(World); var boxes = PacingBoxes(13); var runtime = Runtime();
            yield return null; Assert.That(runtime.TryReserveAll(), Is.True);
            var cluster = UnityEngine.Object.FindObjectOfType<ResourceClusterAuthoring>();
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(cluster, "1", out _), Is.True);
            yield return WaitFor(() => runtime.SearchedBoxCount == 1);
            Assert.That(screen.IsInventoryOpen, Is.False);
            var first = runtime.Reservations[0].box;
            Assert.That(first, Is.Not.Null); Assert.That(runtime.PlacedPartCount, Is.EqualTo(1));
            Assert.That(first.GetSavedItems().Count(v => AerospaceCatalog.CodeFor(v.ItemData) == "R01"), Is.EqualTo(1));
            screen.OpenLootBox(first);
            Assert.That(Source(screen, "R01"), Is.Not.Null); Assert.That(runtime.SearchedBoxCount, Is.EqualTo(1));
            Assert.That(Source(screen, "R01").TryQuickTransfer(out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(InventoryQuickTransferFailure.SearchPending));
            screen.CloseInventory(); ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator InsufficientReachableBoxesDoNotPartiallyInjectOrConsumeSearches()
        {
            TestNavMeshBuilder.Flat(World); AgentFactory.Create(World, "1", Vector3.zero);
            var boxes = PacingBoxes(5); boxes[4].transform.position = new Vector3(1000, 0, 1000);
            var runtime = Runtime(); yield return null;
            Assert.That(runtime.TryReserveAll(), Is.False);
            Assert.That(runtime.PrepareSearchedBox(boxes[0]), Is.False);
            Assert.That(runtime.IsReady, Is.False); Assert.That(runtime.SearchedBoxCount, Is.Zero);
            Assert.That(runtime.Reservations, Is.Empty);
            Assert.That(boxes.Sum(b => b.GetSavedItems().Count(i => AerospaceCatalog.CodeFor(i.ItemData) != null)), Is.Zero);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator DestroyedPlacedBoxDoesNotDuplicateItsPartOrRestartTheCadence()
        {
            TestNavMeshBuilder.Flat(World); AgentFactory.Create(World, "1", Vector3.zero);
            var boxes = PacingBoxes(13); var runtime = Runtime(); yield return null;
            Assert.That(runtime.PrepareSearchedBox(boxes[0]), Is.True);
            UnityEngine.Object.DestroyImmediate(boxes[0].gameObject);
            for (int i = 1; i < 4; i++) runtime.PrepareSearchedBox(boxes[i]);
            Assert.That(runtime.PlacedPartCount, Is.EqualTo(2));
            Assert.That(boxes[3].GetSavedItems().Count(i => AerospaceCatalog.CodeFor(i.ItemData) == "R02"), Is.EqualTo(1));
            Assert.That(runtime.Reservations[0].placed, Is.True);
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator ReservedLootPreservesOrdinaryContentsAndIsIdempotent()
        {
            var box = World.Root("Existing resource box").AddComponent<LootBoxEntity>(); box.ContainerRows = 3; box.ContainerColumns = 6;
            var ordinary = World.Own(ScriptableObject.CreateInstance<InventoryItemData>()); ordinary.ItemID = "ordinary";
            box.SaveRuntimeState(new List<ContainerItemSaveData> { new ContainerItemSaveData { ItemData = ordinary, Amount = 4, X = 2, Y = 1, RuntimeItemId = "keep-me" } }, new List<ContainerCellStateSaveData>());
            AerospaceCollectionRuntime.AddReservedLoot(box, Part("R01")); AerospaceCollectionRuntime.AddReservedLoot(box, Part("R01"));
            Assert.That(box.ContainerRows, Is.EqualTo(4)); var items = box.GetSavedItems(); Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items.Single(i => i.RuntimeItemId == "keep-me").Amount, Is.EqualTo(4));
            var added = items.Single(i => i.ItemData == Part("R01")); Assert.That(added.Y, Is.EqualTo(3));
            Assert.That(added.RequiresSearch && !added.IsSearched, Is.True); Assert.That(added.SearchDurationSeconds, Is.EqualTo(2.1f));
            yield return null; ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator FiveRealSearchTransfersOpenCorrectModelsAndPreserveInventoryPause()
        {
            TestNavMeshBuilder.Flat(World); AgentFactory.Create(World, "1", Vector3.zero);
            var screen = InventoryFactory.Create(World); var runtime = Runtime(); var ui = runtime.GetComponent<AerospaceScienceUI>();
            yield return null; Time.timeScale = 4;
            string[] codes = { "R01", "R02", "R03", "R04", "R05" }; OpenParts(screen, codes);
            Assert.That(Source(screen, "R01").TryQuickTransfer(out var pending), Is.False); Assert.That(pending, Is.EqualTo(InventoryQuickTransferFailure.SearchPending));
            Assert.That(runtime.CollectedCount, Is.Zero); Assert.That(ui.IsOpen, Is.False);
            yield return new WaitForSecondsRealtime(.65f);
            foreach (string code in codes)
            {
                Assert.That(Source(screen, code).TryQuickTransfer(out var failure), Is.True, code + ": " + failure);
                Assert.That(runtime.IsUnlocked(code), Is.True);
                if (code == "R01" && TestRunContext.Load().graphics) { yield return null; yield return CaptureUI(ui, "R01_discovery_transition"); }
                yield return WaitFor(() => ui.IsOpen && !ui.IsTransitioning);
                Assert.That(ui.CurrentCode, Is.EqualTo(code)); Assert.That(ui.Stage.Ready, Is.True);
                Assert.That(ui.Stage.HotspotCount, Is.EqualTo(4)); Assert.That(ui.Stage.CurrentModel.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(AerospaceUiInputGate.BlocksGameplayInput, Is.True); Assert.That(Time.timeScale, Is.Zero);
                if (TestRunContext.Load().graphics) yield return CaptureUI(ui, code + "_assembled");
                ui.ShowDetail(1); ui.Stage.SetMode("exploded"); yield return null; Assert.That(ui.Stage.Mode, Is.EqualTo("exploded"));
                ui.Stage.Orbit(new Vector2(60, 35)); ui.Stage.Zoom(1); ui.Stage.ToggleSurface();
                if (code == "R04") { ui.Stage.ToggleFold(); Assert.That(ui.Stage.Folded, Is.True); }
                if (code == "R03")
                {
                    ui.ModelSpecialAction(); Assert.That(ui.Stage.Study, Is.True); yield return new WaitForSecondsRealtime(.15f);
                    if (TestRunContext.Load().graphics) yield return CaptureUI(ui, "R03_teaching_element");
                    ui.ModelSpecialAction(); Assert.That(ui.Stage.Study, Is.False);
                }
                if (code == "R05")
                {
                    ui.ModelSpecialAction(); ui.ModelSpecialAction(); ui.ModelSpecialAction(); Assert.That(ui.Stage.RelationPhase, Is.EqualTo(2));
                    yield return new WaitForSecondsRealtime(.2f);
                    if (TestRunContext.Load().graphics) yield return CaptureUI(ui, "R05_connection_relation");
                }
                ui.Close(); Assert.That(Time.timeScale, Is.Zero); Assert.That(screen.IsInventoryOpen, Is.True);
            }
            Assert.That(runtime.CollectedCount, Is.EqualTo(5)); Assert.That(screen.ActiveExternalGrid.ExtractSaveData(), Is.Empty);
            Assert.That(screen.BackpackGrid.ExtractSaveData().Count(i => AerospaceCatalog.CodeFor(i.ItemData) != null), Is.EqualTo(5));
            // Repeated transfer into the same bag doesn't replay the discovery or inflate progress.
            var owned = screen.BackpackGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().First(i => AerospaceCatalog.CodeFor(i.ItemData) == "R01");
            Assert.That(owned.TryQuickTransfer(out _), Is.True); Assert.That(owned.TryQuickTransfer(out _), Is.True);
            yield return null; Assert.That(ui.IsOpen, Is.False); Assert.That(runtime.CollectedCount, Is.EqualTo(5));
            screen.CloseInventory(); Assert.That(Time.timeScale, Is.EqualTo(4));
            ui.Open("R03", false); yield return WaitFor(() => !ui.IsTransitioning); ui.Close(); Assert.That(Time.timeScale, Is.EqualTo(4));
            Assert.That(PlayerPrefs.GetInt(AerospaceCollectionRuntime.ArchiveKey), Is.EqualTo(31));
            UnityEngine.Object.DestroyImmediate(runtime.gameObject);
            var nextRun = World.Root("Next round archive fixture").AddComponent<AerospaceCollectionRuntime>(); nextRun.ReserveOnStart = false;
            Assert.That(nextRun.CollectedCount, Is.Zero);
            foreach (string code in codes) Assert.That(nextRun.IsUnlocked(code), Is.True, "Archive survives runtime replacement, unlike round progress.");
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator FullBackpackDoesNotUnlockAndDragCommitUsesTheSameHook()
        {
            TestNavMeshBuilder.Flat(World); AgentFactory.Create(World, "1", Vector3.zero);
            var screen = InventoryFactory.Create(World); var runtime = Runtime(); yield return null;
            OpenParts(screen, "R02"); yield return new WaitForSecondsRealtime(.65f);
            var peg = World.Own(ScriptableObject.CreateInstance<InventoryItemData>()); peg.ItemID = "capacity-probe"; peg.Width = peg.Height = 1;
            var grid = screen.BackpackGrid.GetGridController(); var fill = new List<DraggableItemUI>();
            for (int y = 0; y < grid.Rows; y++) for (int x = 0; x < grid.Columns; x++) if (grid.IsSpaceAvailable(x, y, 1, 1)) fill.Add(InventoryItemFactory.Instance.SpawnItemInGrid(peg, screen.BackpackGrid, x, y, 1));
            var source = Source(screen, "R02"); Assert.That(source.TryQuickTransfer(out var failure), Is.False); Assert.That(failure, Is.EqualTo(InventoryQuickTransferFailure.NoSpace));
            Assert.That(runtime.CollectedCount, Is.Zero); Assert.That(runtime.IsUnlocked("R02"), Is.False);
            Assert.That(screen.ActiveExternalGrid.ExtractSaveData().Count, Is.EqualTo(1));
            var removed = fill[0]; var position = removed._originalGridIndex; grid.RemoveItem(removed, position.x, position.y, false); UnityEngine.Object.DestroyImmediate(removed.gameObject);
            // Exercise the actual drag-commit method after the usual source-grid detach.
            source.CurrentGrid.GetGridController().RemoveItem(source, source._originalGridIndex.x, source._originalGridIndex.y, false);
            var method = typeof(DraggableItemUI).GetMethod("TryPlaceInEmptySpace", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)method.Invoke(source, new object[] { screen.BackpackGrid, grid, position, 1, 1 }), Is.True);
            Assert.That(runtime.CollectedCount, Is.EqualTo(1)); yield return null;
            runtime.GetComponent<AerospaceScienceUI>().Close(); screen.CloseInventory(); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator TwoAgentsKeepSeparateBagsButShareArchiveAndModalRestoresOnDisable()
        {
            TestNavMeshBuilder.Flat(World); AgentFactory.Create(World, "1", Vector3.zero); AgentFactory.Create(World, "2", new Vector3(4, 0, 0));
            var screen = InventoryFactory.Create(World); var runtime = Runtime(); var ui = runtime.GetComponent<AerospaceScienceUI>(); yield return null;
            foreach (var entry in new[] { new[] { "1", "R04" }, new[] { "2", "R05" } })
            {
                Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(entry[0]), Is.True); yield return null;
                OpenParts(screen, entry[1]); yield return new WaitForSecondsRealtime(.65f);
                Assert.That(Source(screen, entry[1]).TryQuickTransfer(out _), Is.True); yield return null;
                ui.Close(); screen.CloseInventory();
            }
            Assert.That(runtime.CollectedCount, Is.EqualTo(2));
            AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("1"); yield return null;
            Assert.That(screen.BackpackGrid.ExtractSaveData().Any(i => i.ItemData == Part("R04")), Is.True);
            Assert.That(screen.BackpackGrid.ExtractSaveData().Any(i => i.ItemData == Part("R05")), Is.False);
            ui.Open("R05", false); Assert.That(Time.timeScale, Is.Zero); ui.enabled = false;
            Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(ui.IsOpen, Is.False); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator QueuedDiscoveriesAndEarlyCloseNeverLeaveTheGamePaused()
        {
            TestNavMeshBuilder.Flat(World); AgentFactory.Create(World, "1", Vector3.zero);
            var screen = InventoryFactory.Create(World); var runtime = Runtime(); var ui = runtime.GetComponent<AerospaceScienceUI>(); yield return null;
            OpenParts(screen, "R01", "R02"); yield return new WaitForSecondsRealtime(.65f);
            Assert.That(Source(screen, "R01").TryQuickTransfer(out _), Is.True);
            Assert.That(Source(screen, "R02").TryQuickTransfer(out _), Is.True); yield return null;
            Assert.That(ui.CurrentCode, Is.EqualTo("R01")); Assert.That(ui.IsTransitioning, Is.True); ui.Close(); yield return null;
            Assert.That(ui.CurrentCode, Is.EqualTo("R02")); ui.Close(); screen.CloseInventory();
            Assert.That(Time.timeScale, Is.EqualTo(1)); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(ui.IsOpen, Is.False); Assert.That(ui.Stage.Ready, Is.False);
            var sound = runtime.GetComponent<AudioSource>(); Assert.That(sound.clip, Is.Not.Null);
            Assert.That(sound.clip.length, Is.EqualTo(.84f).Within(.002f)); Assert.That(sound.spatialBlend, Is.Zero);
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator DiscoveryAudioHasHeadroomMonoCompatibilityAndAnIndependentNonSpatialVoice()
        {
            var runtime = Runtime(); runtime.enabled = false;
            var sound = runtime.GetComponent<AudioSource>();
            Assert.That(sound.clip, Is.Not.Null);
            Assert.That(sound.volume, Is.EqualTo(.85f).Within(.001f));
            Assert.That(sound.spatialBlend, Is.Zero);
            Assert.That(sound.ignoreListenerPause, Is.True);
            Assert.That(sound.priority, Is.EqualTo(64));
            Assert.That(sound.dopplerLevel, Is.Zero);
            Assert.That(sound.playOnAwake || sound.loop, Is.False);
            Assert.That(sound.clip.frequency, Is.EqualTo(48000));
            Assert.That(sound.clip.channels, Is.EqualTo(2));
            Assert.That(sound.clip.length, Is.EqualTo(.84f).Within(.002f));
            sound.clip.LoadAudioData();
            yield return WaitFor(() => sound.clip.loadState == AudioDataLoadState.Loaded);
            var samples = new float[sound.clip.samples * sound.clip.channels];
            Assert.That(sound.clip.GetData(samples, 0), Is.True);
            Assert.That(samples.Max(v => Mathf.Abs(v)), Is.InRange(.64f, .66f), "Use the approved weighted precision-lock master with its original headroom.");
            double stereoEnergy = samples.Average(v => (double)v * v);
            Assert.That(Math.Sqrt(stereoEnergy), Is.InRange(.043, .047), "Check the selected short contact cue, not the rejected musical resonance.");
            double lateEnergy = samples.Skip(24000).Sum(v => (double)v * v);
            Assert.That(lateEnergy / (stereoEnergy * samples.Length), Is.LessThan(.002), "The contact must not become a long ringing note.");
            double monoEnergy = 0;
            for (int i = 0; i < samples.Length; i += 2)
            {
                double mono = (samples[i] + samples[i + 1]) * .5;
                monoEnergy += mono * mono;
            }
            monoEnergy /= sound.clip.samples;
            Assert.That(monoEnergy / stereoEnergy, Is.GreaterThan(.90), "A single speaker must retain the discovery body and tone.");
            Assert.That(samples.Take(3840).Max(v => Mathf.Abs(v)), Is.GreaterThan(.02f), "Contact must begin in the first 40 ms.");
            Assert.That(Math.Abs(samples.Average(v => (double)v)), Is.LessThan(.001));
            Assert.That(Mathf.Abs(samples[0]) + Mathf.Abs(samples[1]) + Mathf.Abs(samples[samples.Length - 1]) + Mathf.Abs(samples[samples.Length - 2]), Is.LessThan(.002f));
            float listenerVolume = AudioListener.volume;
            runtime.RecordSuccessfulPickup(Part("R01"), "1");
            runtime.RecordSuccessfulPickup(Part("R02"), "1");
            runtime.RecordSuccessfulPickup(Part("R02"), "1");
            Assert.That(runtime.CollectedCount, Is.EqualTo(2));
            Assert.That(runtime.GetComponents<AudioSource>().Length, Is.EqualTo(1));
            Assert.That(AudioListener.volume, Is.EqualTo(listenerVolume), "Pickup must never raise the game's master volume.");
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator InventoryClosingBehindModalRestoresUnderlyingSpeedNotZero()
        {
            TestNavMeshBuilder.Flat(World); AgentFactory.Create(World, "1", Vector3.zero);
            var screen = InventoryFactory.Create(World); var runtime = Runtime(); yield return null; Time.timeScale = 3;
            OpenParts(screen, "R01"); yield return new WaitForSecondsRealtime(.65f);
            Assert.That(Source(screen, "R01").TryQuickTransfer(out _), Is.True); yield return null;
            var ui = runtime.GetComponent<AerospaceScienceUI>(); Assert.That(ui.IsOpen, Is.True);
            screen.CloseInventory(); ui.Close(); Assert.That(Time.timeScale, Is.EqualTo(3)); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator FormalSceneContainsAllFiveReachablePartsWithoutSceneOrNavigationChanges()
        {
            string originalHash = Hash(AerospaceCollectionRuntime.FormalScene);
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(AerospaceCollectionRuntime.FormalScene, new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone) yield return null;
            yield return WaitFor(() => AerospaceCollectionRuntime.Instance != null && AerospaceCollectionRuntime.Instance.IsReady, 24);
            var runtime = AerospaceCollectionRuntime.Instance;
            Assert.That(runtime.Reservations.Count, Is.EqualTo(5)); Assert.That(runtime.Reservations.Select(r => r.code).Distinct().Count(), Is.EqualTo(5));
            var agents = AgentRuntimeRegistry.ActiveInstance.RegisteredAgents.ToArray(); Assert.That(agents.Length, Is.EqualTo(2));
            Assert.That(runtime.EligibleBoxCount, Is.GreaterThanOrEqualTo(13));
            Assert.That(runtime.SearchedBoxCount, Is.Zero);
            Assert.That(runtime.PlacedPartCount, Is.Zero);
            var boxes = UnityEngine.Object.FindObjectsOfType<ResourceClusterAuthoring>()
                .SelectMany(c => c.ResourceMembers.Select(m => new { cluster = c, box = m.EntityObject != null ? m.EntityObject.GetComponent<LootBoxEntity>() : null }))
                .Where(p => p.box != null && p.box.isActiveAndEnabled && !p.box.IsResourcePointLooted &&
                    AerospaceCollectionRuntime.TryReach(p.cluster, p.box, agents, out _, out _))
                .Select(p => p.box).Distinct().Take(13).ToArray();
            Assert.That(boxes.Length, Is.EqualTo(13));
            var before = NavMesh.CalculateTriangulation();
            var screen = InventoryScreenController.Instance;
            var ui = runtime.GetComponent<AerospaceScienceUI>();
            var startingParts = screen.BackpackGrid.ExtractSaveData().Where(i => AerospaceCatalog.CodeFor(i.ItemData) != null).ToArray();
            CollectionAssert.AreEquivalent(AerospaceCatalog.Load().models.Select(p => p.code), startingParts.Select(i => AerospaceCatalog.CodeFor(i.ItemData)));
            Assert.That(startingParts.All(i => i.Amount == 1 && i.IsSearched), Is.True);
            Assert.That(runtime.CollectedCount, Is.EqualTo(5)); Assert.That(ui.IsOpen, Is.False);
            Time.timeScale = 0; // Freeze only this validation harness while exercising the real container UI.
            for (int i = 0; i < boxes.Length; i++)
            {
                screen.OpenLootBox(boxes[i]);
                Assert.That(runtime.SearchedBoxCount, Is.EqualTo(i + 1));
                Assert.That(runtime.PlacedPartCount, Is.EqualTo(1 + i / 3));
                if (i % 3 == 0)
                {
                    string code = "R0" + (1 + i / 3);
                    var r = runtime.Reservations.Single(v => v.code == code);
                    Assert.That(AerospaceCollectionRuntime.TryReach(r.cluster, r.box, agents, out _, out _), Is.True);
                    yield return WaitFor(() => Source(screen, code).IsSearched, 24);
                    // Search state completes before the reveal animation releases the item.
                    yield return new WaitForSecondsRealtime(.5f);
                    Assert.That(Source(screen, code).TryQuickTransfer(out _), Is.True); yield return null;
                    ui.Open(code, false); // Starting parts already unlocked these entries; repeat pickups don't queue discovery.
                    yield return WaitFor(() => ui.IsOpen && !ui.IsTransitioning);
                    Assert.That(ui.CurrentCode, Is.EqualTo(code)); Assert.That(ui.Stage.Ready, Is.True);
                    if (i == 0 && TestRunContext.Load().graphics) yield return CaptureUI(ui, "FormalScene_R01");
                    ui.Close();
                }
                screen.CloseInventory();
            }
            Assert.That(runtime.CollectedCount, Is.EqualTo(5));
            Assert.That(runtime.Reservations.Select(r => r.box).Distinct().Count(), Is.EqualTo(5));
            var after = NavMesh.CalculateTriangulation(); Assert.That(after.vertices, Is.EqualTo(before.vertices)); Assert.That(after.indices, Is.EqualTo(before.indices));
            Assert.That(Hash(AerospaceCollectionRuntime.FormalScene), Is.EqualTo(originalHash));
            foreach (var nav in UnityEngine.Object.FindObjectsOfType<NavMeshAgent>()) nav.gameObject.SetActive(false);
            ContractCompleted = true;
        }
        static string Hash(string file)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(file)));
        }
        static IEnumerator CaptureUI(AerospaceScienceUI ui, string name)
        {
            if (!name.EndsWith("discovery_transition", StringComparison.Ordinal))
                yield return new WaitForSecondsRealtime(.3f);
            var canvas = ui.GetComponentInChildren<Canvas>();
            var transforms = canvas.GetComponentsInChildren<Transform>(true); var layers = transforms.Select(t => t.gameObject.layer).ToArray();
            var cameraObject = new GameObject("Evidence camera"); var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AerospaceInspectionCamera>();
            camera.transform.position = new Vector3(0, -200, 0); camera.cullingMask = 1 << 30; camera.depth = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.012f, .024f, .034f); camera.nearClipPlane = .01f; camera.farClipPlane = 5;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.volumeLayerMask = 0;
            var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
            foreach (var t in transforms) t.gameObject.layer = 30;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); yield return null; yield return null; yield return null;
            var old = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply(); RenderTexture.active = old;
            string folder = System.IO.Path.Combine(TestRunContext.Load().outputPath, "screenshots"); System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) transforms[i].gameObject.layer = layers[i];
            camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
