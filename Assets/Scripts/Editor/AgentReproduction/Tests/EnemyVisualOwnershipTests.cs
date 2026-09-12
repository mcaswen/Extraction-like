using System.Collections;
using AgentReproduction.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class EnemyVisualOwnershipTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator StaticFallbackNeverRewritesBodyTransform()
        {
            var body = World.Cube("Body mesh", new Vector3(2, 4, 3), Vector3.one);
            var owner = body.AddComponent<EnemyHealthController>();
            Vector3 initial = body.transform.position;
            var animation = new EnemyAnimatorDriver(owner);
            Assert.That(body.transform.position, Is.EqualTo(initial), "Visual alignment cannot move the gameplay root.");
            yield return null;
            body.transform.position = new Vector3(8, 3, -5);
            body.transform.rotation = Quaternion.Euler(0, 72, 0);
            Vector3 expectedPosition = body.transform.position;
            Quaternion expectedRotation = body.transform.rotation;
            animation.TriggerAttack();
            animation.SetSpeed(3.5f);
            animation.LateUpdate();
            Assert.That(body.transform.position, Is.EqualTo(expectedPosition));
            Assert.That(body.transform.rotation, Is.EqualTo(expectedRotation));
            Assert.That(body.transform.localScale, Is.EqualTo(Vector3.one));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator DedicatedChildVisualStillAnimates()
        {
            var body = World.Root("Moving body");
            var visual = World.Cube("Visual", Vector3.up, Vector3.one);
            visual.transform.SetParent(body.transform, false);
            var owner = body.AddComponent<EnemyHealthController>();
            var animation = new EnemyAnimatorDriver(owner);
            yield return null;
            body.transform.position = new Vector3(8, 3, -5);
            Vector3 expectedPosition = body.transform.position;
            Vector3 oldScale = visual.transform.localScale;
            animation.TriggerAttack();
            animation.SetSpeed(3.5f);
            Assert.That(body.transform.position, Is.EqualTo(expectedPosition));
            Assert.That(visual.transform.localScale, Is.Not.EqualTo(oldScale), "The independent visual must still animate.");
            ContractCompleted = true;
        }
    }
}
