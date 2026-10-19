using System;
using System.Collections;
using Unity.Profiling;

namespace AgentReproduction.Infrastructure
{
    /// <summary>
    /// 托管分配测量：用 Unity 的每帧 GC 分配计数器比较执行 body 的一帧与空闲帧。
    /// Unity 的 Mono 不实现 GC.GetAllocatedBytesForCurrentThread（恒为 0），Mono 已用堆增量也看不到小块分配。
    /// </summary>
    public static class ManagedAllocation
    {
        public const long Unavailable = -1;

        public static IEnumerator MeasureFrame(Action body, Action<long> onMeasured)
        {
            using (ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                yield return null;
                yield return null;
                if (!recorder.Valid)
                {
                    onMeasured(Unavailable);
                    yield break;
                }

                long idle = recorder.LastValue;
                body();
                yield return null;
                onMeasured(Math.Max(0, recorder.LastValue - idle));
            }
        }
    }
}
