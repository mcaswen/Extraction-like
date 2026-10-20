using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// 已处理资源运行时记录，所有 Agent 共享
    /// 玩家打开过的箱子关闭后即登记，不管剩没剩东西，行为树都不会再选它
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
