using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class HarnessSmokeTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator PlayModeNavigationPhysicsAndIsolation()
        {
            Assert.That(Application.isPlaying, Is.True);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource {
                shape=NavMeshBuildSourceShape.Box, size=new Vector3(20,0.2f,20),
                transform=Matrix4x4.TRS(new Vector3(0,-0.1f,0), Quaternion.identity, Vector3.one), area=0
            }};
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.zero, new Vector3(24,8,24)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            NavMesh.AddNavMeshData(data);
            Assert.That(NavMesh.SamplePosition(Vector3.zero, out NavMeshHit sample, 1, NavMesh.AllAreas), Is.True);
            NavMeshPath path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(sample.position, new Vector3(4,0,0), NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0,1,4);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(new Vector3(0,1,0), Vector3.forward, out RaycastHit hit, 8), Is.True);
            Assert.That(hit.collider.gameObject, Is.EqualTo(wall));
            CaseArtifactWriter.Trace("navigation-physics", "Complete path; obstacle ray hits fixture wall; isolated Play Mode.");
            TestRunContext run = TestRunContext.Load();
            if (run.faultProbe == "Assertion") Assert.Fail("Intentional runner assertion probe; gameplay is not under test.");
            if (run.faultProbe == "Timeout" && run.repeat == 1)
            {
                CaseArtifactWriter.Trace("timeout-probe", "Waiting for owned-process watchdog.");
                while (true) yield return null;
            }
            yield return null;
            Object.Destroy(data);
        }
    }
}
