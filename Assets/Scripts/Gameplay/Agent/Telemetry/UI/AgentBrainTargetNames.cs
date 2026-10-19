#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>
    /// 群的显示名：场景里的地图绑定把群对象对应到地图节点，取"区域名 + 群类别 + 节点名尾部的字母"，例如"雨林 资源群 B"。
    /// 只读取绑定列表和地图定义，不调用会重建索引的查询。
    /// </summary>
    public sealed class AgentBrainTargetNames
    {
        private readonly Dictionary<GameplayTargetClusterAuthoringBase, string> _byCluster = new Dictionary<GameplayTargetClusterAuthoringBase, string>();
        private readonly Dictionary<string, string> _byNode = new Dictionary<string, string>(StringComparer.Ordinal);
        // 运行时生成的敌人群不在地图上，按它的敌人来源命名："区域 敌人群 字母"
        private readonly Dictionary<string, string> _enemiesBySourceId = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _enemiesBySourceName = new Dictionary<string, string>(StringComparer.Ordinal);
        private const string ActiveClusterSuffix = "_ActiveEnemyCluster";
        private MapGraphBindingAuthoring _binding;
        private float _nextSearchTime;

        /// <summary>地图节点的显示名；不认识时返回节点 ID</summary>
        public string Node(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return "—";
            EnsureBinding();
            return _byNode.TryGetValue(nodeId, out string label) ? label : nodeId;
        }

        /// <summary>目标对象的显示名：所在的群有地图节点时用节点名，否则用群的名字；敌人个体用它所属的敌人群，找不到时统称"敌人"</summary>
        public string Of(GameObject target)
        {
            if (target == null) return "—";
            EnsureBinding();
            var cluster = target.GetComponentInParent<GameplayTargetClusterAuthoringBase>();
            var enemy = cluster == null ? target.GetComponentInParent<global::EnemyHealthController>() : null;
            if (cluster == null)
            {
                if (enemy == null) return target.name;
                GameplayTargetRegistry registry = GameplayTargetRegistry.ActiveInstance;
                if (registry == null || !registry.TryFindEnemyClusterByEnemy(enemy, out ActiveEnemyClusterAuthoring enemies)) return "敌人";
                cluster = enemies;
            }

            if (_byCluster.TryGetValue(cluster, out string label)) return label;
            if (cluster is ActiveEnemyClusterAuthoring active)
            {
                if (enemy != null && active.TryGetSourceTargetIdForEnemy(enemy, out string sourceId) && _enemiesBySourceId.TryGetValue(sourceId, out label)) return label;
                string name = cluster.gameObject.name;
                if (name.EndsWith(ActiveClusterSuffix, StringComparison.Ordinal) &&
                    _enemiesBySourceName.TryGetValue(name.Substring(0, name.Length - ActiveClusterSuffix.Length), out label)) return label;
                return "敌人群";
            }

            return cluster.DisplayName;
        }

        public static string Label(MapGraphNodeDefinition node, string zoneName)
        {
            string kind = Kind(node.NodeKind);
            string suffix = Suffix(node.DisplayName);
            if (string.IsNullOrEmpty(kind)) return string.IsNullOrEmpty(zoneName) ? node.DisplayName : zoneName;
            return string.IsNullOrEmpty(zoneName) ? kind + suffix : zoneName + " " + kind + suffix;
        }

        private static string Kind(MapGraphNodeKind kind)
        {
            switch (kind)
            {
                case MapGraphNodeKind.Resource: return "资源群";
                case MapGraphNodeKind.EnemySource: return "敌人来源";
                case MapGraphNodeKind.ActiveEnemy: return "敌人群";
                case MapGraphNodeKind.Extraction: return "撤离点";
                case MapGraphNodeKind.Start: return "起点";
                default: return string.Empty;
            }
        }

        // 节点名形如 ResourceCluster_B 时取尾部的 B 区分同区域的同类群
        private static string Suffix(string displayName)
        {
            if (string.IsNullOrEmpty(displayName) || displayName.Length < 3) return string.Empty;
            char last = displayName[displayName.Length - 1];
            return displayName[displayName.Length - 2] == '_' && char.IsLetterOrDigit(last) ? " " + last : string.Empty;
        }

        private void EnsureBinding()
        {
            if (_binding != null || Time.unscaledTime < _nextSearchTime) return;
            _nextSearchTime = Time.unscaledTime + 1f;
            _binding = Object.FindObjectOfType<MapGraphBindingAuthoring>();
            _byCluster.Clear();
            _byNode.Clear();
            _enemiesBySourceId.Clear();
            _enemiesBySourceName.Clear();
            SO_MapGraphDefinition definition = _binding != null ? _binding.MapDefinition : null;
            if (definition == null) return;

            var enemyLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < definition.Nodes.Count; index++)
            {
                MapGraphNodeDefinition node = definition.Nodes[index];
                if (node == null) continue;
                string zone = ZoneName(definition, node.ZoneId);
                _byNode[node.NodeId] = Label(node, zone);
                if (node.NodeKind == MapGraphNodeKind.EnemySource)
                    enemyLabels[node.NodeId] = (string.IsNullOrEmpty(zone) ? string.Empty : zone + " ") + "敌人群" + Suffix(node.DisplayName);
            }

            IReadOnlyList<MapGraphTargetBinding> bindings = _binding.TargetBindings;
            for (int index = 0; index < bindings.Count; index++)
            {
                MapGraphTargetBinding binding = bindings[index];
                if (binding == null || !(binding.DirectTarget is GameplayTargetClusterAuthoringBase cluster)) continue;
                if (_byNode.TryGetValue(binding.NodeId, out string label)) _byCluster[cluster] = label;
                if (!enemyLabels.TryGetValue(binding.NodeId, out string enemies)) continue;
                _enemiesBySourceId[cluster.TargetId] = enemies;
                _enemiesBySourceName[cluster.gameObject.name] = enemies;
            }
        }

        private static string ZoneName(SO_MapGraphDefinition definition, string zoneId)
        {
            for (int index = 0; index < definition.Zones.Count; index++)
                if (definition.Zones[index] != null && definition.Zones[index].ZoneId == zoneId) return definition.Zones[index].DisplayName;
            return string.Empty;
        }
    }
}
#endif
