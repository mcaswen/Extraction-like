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
        private static readonly HashSet<int> SearchedResourceInstanceIds = new HashSet<int>();

        /// <summary>
        /// 标记一个资源对象已经被 Agent 搜索过
        /// </summary>
        /// <param name="resourceObject"></param>
        public static void MarkSearched(GameObject resourceObject)
        {
            if (resourceObject == null)
                return;

            SearchedResourceInstanceIds.Add(resourceObject.GetInstanceID());
        }

        /// <summary>
        /// 判断资源对象是否已经被 Agent 搜索过
        /// </summary>
        /// <param name="resourceObject"></param>
        /// <returns></returns>
        public static bool IsSearched(GameObject resourceObject)
        {
            return resourceObject != null &&
                   SearchedResourceInstanceIds.Contains(resourceObject.GetInstanceID());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnSubsystemRegistration()
        {
            SearchedResourceInstanceIds.Clear();
        }
    }
}
