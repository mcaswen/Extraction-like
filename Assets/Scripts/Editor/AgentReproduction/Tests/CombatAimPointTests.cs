using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using Gameplay.Perception;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class CombatAimPointTests : ReproductionTestFixture
    {
        public static string[] Shapes = { "Capsule", "Sphere", "Box", "Mesh" };
        private bool _originalSync;
        [UnityTest]
        public IEnumerator SameFramePoseUsesCurrentColliderCenterWithoutPhysicsSync([ValueSource(nameof(Shapes))] string shape)
        {
            _originalSync = Physics.autoSyncTransforms; Physics.autoSyncTransforms = false;
            var root = World.Root("Moving body");
            var body = World.Cube("Body", Vector3.zero, Vector3.one);
            body.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            Vector3 local = new Vector3(.2f, -.057932537f, .3f);
            Collider collider;
            if (shape == "Capsule") { var c = body.AddComponent<CapsuleCollider>(); c.center = local; collider = c; }
            else if (shape == "Sphere") { var c = body.AddComponent<SphereCollider>(); c.center = local; collider = c; }
            else if (shape == "Box") { var c = body.AddComponent<BoxCollider>(); c.center = local; collider = c; }
            else
            {
                var mesh = World.Own(Object.Instantiate(body.GetComponent<MeshFilter>().sharedMesh));
                var vertices = mesh.vertices; for (int i = 0; i < vertices.Length; i++) vertices[i] += local;
                mesh.vertices = vertices; mesh.RecalculateBounds();
                var c = body.AddComponent<MeshCollider>(); c.sharedMesh = mesh; collider = c;
            }
            Physics.SyncTransforms();
            root.transform.SetPositionAndRotation(new Vector3(5, 3.13f, 2), Quaternion.Euler(0, 35, 0));
            root.transform.localScale = new Vector3(3, 2, 4);
            var expected = body.transform.TransformPoint(local);
            var observed = CombatAimPointResolver.Resolve(root.transform);
            CaseArtifactWriter.Trace("same-frame-center", "shape=" + shape + "; bounds=" + collider.bounds.center +
                "; expected=" + expected + "; observed=" + observed);
            Assert.That(Vector3.Distance(observed, expected), Is.LessThan(.0001f));
            ContractCompleted = true; yield break;
        }
        [UnityTearDown] public IEnumerator RestoreSyncSetting()
        { Physics.autoSyncTransforms = _originalSync; yield break; }
    }
}
