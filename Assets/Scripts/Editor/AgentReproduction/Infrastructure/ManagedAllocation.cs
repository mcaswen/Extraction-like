using System;
using UnityEngine.Profiling;

namespace AgentReproduction.Infrastructure
{
    /// <summary>
    /// 托管分配测量。Unity 的 Mono 不实现 GC.GetAllocatedBytesForCurrentThread（恒为 0），
    /// 这里用 Mono 已用堆的增量，期间发生过回收就重测。
    /// </summary>
    public static class ManagedAllocation
    {
        public const long Inconclusive = -1;

        public static long Measure(Action body, int attempts = 5)
        {
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int collections = GC.CollectionCount(0);
                long before = Profiler.GetMonoUsedSizeLong();
                body();
                long after = Profiler.GetMonoUsedSizeLong();
                if (GC.CollectionCount(0) == collections)
                    return Math.Max(0, after - before);
            }

            return Inconclusive;
        }
    }
}
