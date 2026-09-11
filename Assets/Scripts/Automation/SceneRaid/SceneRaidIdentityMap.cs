#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System.Collections.Generic;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    // 场景加载时冻结的键可以连接 Editor 审计；动态对象在 P1 增加出生来源。
    public sealed class SceneRaidIdentityMap
    {
        private readonly Dictionary<int, string> _keys = new Dictionary<int, string>();
        public static string HierarchyPath(Transform value)
        {
            if (value == null) return "";
            string part = value.name + "[" + value.GetSiblingIndex() + "]";
            return value.parent == null ? value.gameObject.scene.path + "/" + part : HierarchyPath(value.parent) + "/" + part;
        }
        public string Get(Component value) => value == null ? "" : Get(value.gameObject);
        public string Get(GameObject value)
        {
            if (value == null) return "";
            int id = value.GetInstanceID();
            if (!_keys.TryGetValue(id, out string key))
            {
                key = HierarchyPath(value.transform);
                _keys.Add(id, key);
            }
            return key;
        }
    }
}
#endif
