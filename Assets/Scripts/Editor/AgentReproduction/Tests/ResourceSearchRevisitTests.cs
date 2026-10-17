using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ResourceSearchRevisitTests : ReproductionTestFixture
    {
        public static bool[] RouteModes = { false, true };

        private LootBoxEntity Box(string name, Vector3 position, bool partialSearch = false)
        {
            var box = World.Root(name).AddComponent<LootBoxEntity>();
            box.transform.position = position;
            var items = new List<ContainerItemSaveData>();
            for (int i = 0; i < 2; i++)
            {
                var item = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
                item.ItemID = name + i; item.Width = item.Height = 1;
                items.Add(new ContainerItemSaveData { ItemData = item, Amount = 1, X = i,
                    RequiresSearch = partialSearch && i == 1, IsSearched = !partialSearch || i == 0,
                    SearchDurationSeconds = 30 });
            }
            box.SaveRuntimeState(items, new List<ContainerCellStateSaveData>());
            return box;
        }

        private static DraggableItemUI[] Contents(InventoryScreenController screen)
            => screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>()
                .Where(x => x.CurrentGrid == screen.ActiveExternalGrid).ToArray();

        private static bool Finished(AgentPawnRoot agent, bool routes)
            => routes ? agent.RouteSnapshot.HasRoute && !agent.RouteSnapshot.IsActive && !agent.RouteSnapshot.HasPendingRequest
                : !agent.DirectiveLifecycle.Active.HasValue;

        private static void Order(ResourceClusterAuthoring cluster, AgentPawnRoot agent, bool routes)
        {
            var dispatcher = new AgentTargetCommandDispatcher();
            if (routes)
                Assert.That(dispatcher.TrySubmitClusterRoute(cluster, agent.AgentIdValue).Stage,
                    Is.EqualTo(AgentRouteStage.Planning));
            else Assert.That(dispatcher.TrySubmitClusterCommand(cluster, agent.AgentIdValue, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator PartialCloseIsSharedAndEitherAgentCanRevisit([ValueSource(nameof(RouteModes))] bool routes)
        {
            TestNavMeshBuilder.Flat(World);
            var first = AgentFactory.Create(World, "1", new Vector3(-4, 0, -3), 8, false, false);
            var second = AgentFactory.Create(World, "2", new Vector3(-4, 0, 3), 8, false, false);
            var screen = InventoryFactory.Create(World);
            var box = Box("Shared partial", Vector3.right * 6, true);
            box.gameObject.AddComponent<BoxCollider>().size = new Vector3(6, 2, 6);
            var empty = Box("Already empty", Vector3.right * 3);
            empty.SaveRuntimeState(new List<ContainerItemSaveData>(), new List<ContainerCellStateSaveData>());
            var cluster = TargetFactory.ResourceBoxes(World, empty, box);
            var binding = MapRouteFactory.Bind(World, new[] { cluster }, new[] { Vector3.zero });
            if (routes)
            {
                var installer = World.Root("Shared resource routes").AddComponent<Gameplay.Raid.RaidMapCommandInstaller>();
                installer.Configure(binding);
            }
            var waiting = new Dictionary<string, GameObject>();
            void Observe(AgentResourceInteractionEvent value)
            {
                if (value.Stage == AgentResourceInteractionStage.WaitingForInventory) waiting[value.AgentId] = value.Resource;
                else waiting.Remove(value.AgentId);
            }
            AgentResourceInteractionChannel.Published += Observe;
            try
            {
                yield return null;
                Time.timeScale = 4;
                Order(cluster, first, routes); Order(cluster, second, routes);
                yield return RuntimeWait.Until(() => waiting.Count == 2, "both agents waiting at shared box");
                Assert.That(waiting.Values.All(x => x == box.gameObject), Is.True, "Empty members must be skipped.");
                Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("1"), Is.True);
                yield return null;
                box.Interact();
                Assert.That(screen.IsInventoryOpen, Is.True);
                yield return RuntimeWait.Until(() => Contents(screen).Any(x => x.SearchProgressSeconds > 0.1f), "real partial reveal");
                Assert.That(Contents(screen).Single(x => x.IsInteractionReady).TryQuickTransfer(out _), Is.True);
                screen.CloseInventory();
                yield return RuntimeWait.Until(() => Finished(first, routes) && Finished(second, routes), "shared completion releases both agents");
                Assert.That(cluster.HasBeenCompleted, Is.True);
                Assert.That(cluster.HasRemainingLoot, Is.True);
                Assert.That(cluster.HasPendingResources(), Is.False, "Autonomous search shares the completed state.");
                Assert.That(box.ResourceState, Is.EqualTo(SceneResourceStateType.PartiallySearched));
                var saved = box.GetSavedItems().Single();
                Assert.That(saved.IsSearched, Is.False);
                Assert.That(saved.SearchProgressSeconds, Is.GreaterThan(0));
                var resolver = new MapGraphRouteTargetResolver(binding);
                Assert.That(resolver.TryGetFacts("n0", out var facts), Is.True);
                Assert.That(facts.Status, Is.EqualTo(AgentRouteTargetStatus.Completed));
                Assert.That(facts.HasRemainingLoot, Is.True);

                Order(cluster, second, routes);
                yield return RuntimeWait.Until(() => waiting.TryGetValue("2", out var resource) && resource == box.gameObject,
                    "second agent explicitly revisits the remaining box");
                Assert.That(AgentManualDirectiveLock.ShouldHoldManualResourceDirective(second.DirectiveLifecycle.Active.Value), Is.True);
                Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2"), Is.True);
                yield return null;
                box.Interact();
                yield return null;
                Assert.That(Contents(screen).Single().SearchProgressSeconds, Is.GreaterThanOrEqualTo(saved.SearchProgressSeconds),
                    "Revisit shares the first agent's reveal progress.");
                Assert.That(screen.ActiveSessionContext.AgentId, Is.EqualTo("2"));
                screen.CloseInventory();
                yield return RuntimeWait.Until(() => Finished(second, routes), "closing again finishes only this new visit");
                Assert.That(box.GetSavedItems().Count, Is.EqualTo(1));
                Assert.That(Finished(first, routes), Is.True);
                ContractCompleted = true;
            }
            finally
            {
                AgentReproduction.Reporting.CaseArtifactWriter.Trace("shared-final",
                    "first=" + JsonUtility.ToJson(AnomalySearch.Automation.SceneRaid.Commands.SceneRaidRouteEvidence.Capture(first)) +
                    "; second=" + JsonUtility.ToJson(AnomalySearch.Automation.SceneRaid.Commands.SceneRaidRouteEvidence.Capture(second)));
                AgentResourceInteractionChannel.Published -= Observe;
            }
        }

        [UnityTest]
        public IEnumerator PartialCloseLeavesGroupAndOnlyAnExplicitFinalTargetReopensIt()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.left * 3, 8, false, false);
            var screen = InventoryFactory.Create(World);
            var first = Box("Leftover", Vector3.right * 5);
            var second = Box("To empty", Vector3.right * 13);
            var cluster = TargetFactory.ResourceBoxes(World, first, second);
            var defeated = EnemyFactory.Passive(World, Vector3.right * 25);
            var end = TargetFactory.Enemies(World, defeated);
            Object.DestroyImmediate(defeated.gameObject);
            var binding = MapRouteFactory.Bind(World, new GameplayTargetClusterAuthoringBase[] { cluster, end },
                new[] { Vector3.right * 3, Vector3.right * 25 }, MapRouteFactory.Chain(2));
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding, agent));
            GameObject waiting = null;
            void Observe(AgentResourceInteractionEvent value)
                => waiting = value.Stage == AgentResourceInteractionStage.WaitingForInventory ? value.Resource : null;
            AgentResourceInteractionChannel.Published += Observe;
            try
            {
                yield return null; Time.timeScale = 4;
                agent.TrySubmitRoute(new AgentRouteRequest("n1", AgentRouteSource.Player, agent.AgentId, "through-group"));
                yield return RuntimeWait.Until(() => waiting == first.gameObject, "first box on route");
                first.Interact(); yield return null;
                Assert.That(Contents(screen)[0].TryQuickTransfer(out _), Is.True);
                screen.CloseInventory();
                yield return RuntimeWait.Until(() => waiting == second.gameObject, "partial close walks to next box");
                Assert.That(agent.Position.x, Is.GreaterThan(10));
                second.Interact(); yield return null;
                foreach (var item in Contents(screen)) Assert.That(item.TryQuickTransfer(out _), Is.True);
                screen.CloseInventory();
                yield return RuntimeWait.Until(() => Finished(agent, true), "leave processed group for route destination");
                Assert.That(agent.RouteSnapshot.Stage, Is.EqualTo(AgentRouteStage.Completed));
                Assert.That(agent.Position.x, Is.GreaterThan(23));
                Assert.That(first.GetSavedItems().Count, Is.EqualTo(1));
                Assert.That(second.IsResourcePointLooted, Is.True);

                Order(cluster, agent, true);
                yield return RuntimeWait.Until(() => waiting == first.gameObject, "explicit final target revisits remaining loot");
                first.Interact(); yield return null;
                screen.CloseInventory();
                yield return RuntimeWait.Until(() => Finished(agent, true), "leave again without taking last item");
                Assert.That(second.GetSavedItems(), Is.Empty);

                // 当前锚点是已处理资源群，新路线的中间节点不能因有余物而再次等待背包。
                waiting = null;
                agent.TrySubmitRoute(new AgentRouteRequest("n1", AgentRouteSource.Player, agent.AgentId, "pass-again"));
                yield return RuntimeWait.Until(() => Finished(agent, true), "cross processed resource without reopening");
                Assert.That(agent.RouteSnapshot.Stage, Is.EqualTo(AgentRouteStage.Completed));
                Assert.That(agent.Position.x, Is.GreaterThan(23));
                Assert.That(waiting, Is.Null);
                Assert.That(first.GetSavedItems().Count, Is.EqualTo(1));
                ContractCompleted = true;
            }
            finally { AgentResourceInteractionChannel.Published -= Observe; }
        }

        [UnityTest]
        public IEnumerator ReplanningAndRetaliationPreserveTheCurrentSearchPass()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var screen = InventoryFactory.Create(World);
            var first = Box("Processed first", Vector3.right * 5);
            var second = Box("Pending second", Vector3.right * 20);
            var cluster = TargetFactory.ResourceBoxes(World, first, second);
            var binding = MapRouteFactory.Bind(World, new[] { cluster }, new[] { Vector3.right * 3 });
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding, agent));
            GameObject waiting = null;
            void Observe(AgentResourceInteractionEvent value)
                => waiting = value.Stage == AgentResourceInteractionStage.WaitingForInventory ? value.Resource : null;
            AgentResourceInteractionChannel.Published += Observe;
            try
            {
                yield return null; Time.timeScale = 4;
                Order(cluster, agent, true);
                yield return RuntimeWait.Until(() => waiting == first.gameObject, "first visit");
                first.Interact(); yield return null;
                screen.CloseInventory();
                yield return RuntimeWait.Until(() => AgentSearchedResourceRegistry.IsSearched(first.gameObject), "shared first completion");
                var pass = agent.RouteSnapshot.Request.ResourceSearch;
                long version = agent.RouteSnapshot.RouteVersion;
                agent.ConfigureRoutes(MapRouteFactory.Environment(binding, agent, 2));
                yield return RuntimeWait.Until(() => agent.RouteSnapshot.RouteVersion > version && waiting == second.gameObject,
                    "replan skips processed first box");
                Assert.That(agent.RouteSnapshot.Request.ResourceSearch, Is.SameAs(pass));
                var attacker = EnemyFactory.Passive(World, agent.Position + Vector3.forward * 5);
                agent.TakeCombatDamage(10, agent.Position, Vector3.back, attacker.gameObject);
                Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue, Is.True);
                Assert.That(agent.DirectiveLifecycle.SuspendedDirective.Value.ResourceSearch, Is.SameAs(pass));
                Object.DestroyImmediate(attacker.gameObject);
                yield return RuntimeWait.Until(() => agent.DirectiveLifecycle.Active?.DirectiveType == AgentDirectiveType.Search &&
                    waiting == second.gameObject, "retaliation resumes second box");
                Assert.That(agent.DirectiveLifecycle.Active.Value.ResourceSearch, Is.SameAs(pass));
                second.Interact(); yield return null;
                screen.CloseInventory();
                yield return RuntimeWait.Until(() => Finished(agent, true), "both closed boxes finish root despite leftovers");
                Assert.That(agent.RouteSnapshot.Stage, Is.EqualTo(AgentRouteStage.Completed));
                Assert.That(first.GetSavedItems().Count, Is.EqualTo(2));
                Assert.That(second.GetSavedItems().Count, Is.EqualTo(2));
                ContractCompleted = true;
            }
            finally { AgentResourceInteractionChannel.Published -= Observe; }
        }
    }
}
