using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Agent 已处理资源运行时记录
    /// 用于避免资源行为树反复选择同一个已经确认过的箱子或掉落物
    /// </summary>
    public static class AgentSearchedResourceRegistry
    {
        private static readonly Dictionary<int, long> SearchedResourceRevisions = new Dictionary<int, long>();
        public static long Revision { get; private set; }

        /// <summary>
        /// 标记一个资源对象已经被 Agent 搜索过
        /// </summary>
        /// <param name="resourceObject"></param>
        public static void MarkSearched(GameObject resourceObject)
        {
            if (resourceObject == null)
                return;

            SearchedResourceRevisions[resourceObject.GetInstanceID()] = ++Revision;
        }

        /// <summary>
        /// 判断资源对象是否已经被 Agent 搜索过
        /// </summary>
        /// <param name="resourceObject"></param>
        /// <returns></returns>
        public static bool IsSearched(GameObject resourceObject)
        {
            return resourceObject != null &&
                   SearchedResourceRevisions.ContainsKey(resourceObject.GetInstanceID());
        }

        /// <summary>任何 Agent 在这轮命令发出后处理过箱子，都共享同一个完成事实。</summary>
        public static bool WasSearchedAfter(GameObject resourceObject, long revision)
            => resourceObject != null && SearchedResourceRevisions.TryGetValue(resourceObject.GetInstanceID(), out long completed)
                && completed > revision;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnSubsystemRegistration()
        {
            SearchedResourceRevisions.Clear();
            Revision = 0;
        }
    }
}
