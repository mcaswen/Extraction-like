using System;
using System.Collections;
using NUnit.Framework;

namespace AgentReproduction.Infrastructure
{
    public static class RuntimeWait
    {
        public static IEnumerator Until(Func<bool> condition, string reason, double wallSeconds = 10)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(wallSeconds);
            while (!condition())
            {
                Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "Timed out: " + reason);
                yield return null;
            }
        }
    }
}
