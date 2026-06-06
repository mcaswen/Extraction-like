using System.Collections.Generic;
using Gameplay.Targets.Data;
using UnityEngine;

namespace Gameplay.Targets.Authoring
{
    /// <summary>
    /// 活跃敌人群目标配置
    /// 只表达已经存在于场景中的敌人，不再承担出生点来源语义
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActiveEnemyClusterAuthoring : GameplayTargetClusterAuthoringBase
    {
        [Header("Manual Enemy Members")]
        [SerializeField] private List<GameplayTargetEntityMember> _initialEnemies =
            new List<GameplayTargetEntityMember>();

        private readonly List<GameplayTargetEntityMember> _runtimeEnemies =
            new List<GameplayTargetEntityMember>();

        private readonly Dictionary<global::EnemyHealthController, string> _sourceTargetIdsByRuntimeEnemy =
            new Dictionary<global::EnemyHealthController, string>();

        public override GameplayTargetKind TargetKind => GameplayTargetKind.Enemy;
        protected override string IdPrefix => "ActiveEnemyCluster";
        protected override bool RefreshStateEveryFrame => true;
        protected override bool RefreshRangeEveryFrame => true;
        protected override bool HideRangeWhenCompleted => true;

        public IReadOnlyList<GameplayTargetEntityMember> InitialEnemies => _initialEnemies;
        public bool HasRegisteredEnemy => CountRegisteredEnemies() > 0;

        /// <summary>
        /// 复用敌人来源群的范围显示配置，为运行时生成的活跃敌人群创建独立 LineRenderer
        /// </summary>
        /// <param name="sourceCluster"></param>
        /// <param name="sourceLineRenderer"></param>
        public void AttachRangeLineRendererFromSource(
            GameplayTargetClusterAuthoringBase sourceCluster,
            LineRenderer sourceLineRenderer)
        {
            AttachRangeLineRendererFromTemplate(sourceCluster, sourceLineRenderer);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _initialEnemies ??= new List<GameplayTargetEntityMember>();
        }

        protected override void OnEnable()
        {
            _initialEnemies ??= new List<GameplayTargetEntityMember>();
            base.OnEnable();
        }

        /// <summary>
        /// 手动注册一个场景中已经存在的敌人
        /// </summary>
        /// <param name="enemy"></param>
        public void RegisterSceneEnemy(global::EnemyHealthController enemy)
        {
            RegisterEnemy(enemy, _initialEnemies, "SceneEnemy");
        }

        /// <summary>
        /// 注册一个出生点运行时生成的敌人
        /// </summary>
        /// <param name="enemy"></param>
        public void RegisterSpawnedEnemy(global::EnemyHealthController enemy)
        {
            RegisterSpawnedEnemy(enemy, string.Empty);
        }

        /// <summary>
        /// 注册一个出生点运行时生成的敌人，并记录它来自哪个敌人来源群
        /// </summary>
        /// <param name="enemy"></param>
        /// <param name="sourceTargetId"></param>
        public void RegisterSpawnedEnemy(
            global::EnemyHealthController enemy,
            string sourceTargetId)
        {
            RegisterEnemy(enemy, _runtimeEnemies, "RuntimeEnemy", sourceTargetId);
        }

        /// <summary>
        /// 判断敌人实例是否属于当前敌人群
        /// </summary>
        /// <param name="enemy"></param>
        /// <returns></returns>
        public bool ContainsEnemy(global::EnemyHealthController enemy)
        {
            if (enemy == null)
                return false;

            return ContainsEnemy(_initialEnemies, enemy) || ContainsEnemy(_runtimeEnemies, enemy);
        }

        /// <summary>
        /// 查询运行时敌人对应的敌人来源群 TargetId
        /// </summary>
        /// <param name="enemy"></param>
        /// <param name="sourceTargetId"></param>
        /// <returns></returns>
        public bool TryGetSourceTargetIdForEnemy(
            global::EnemyHealthController enemy,
            out string sourceTargetId)
        {
            if (enemy != null &&
                _sourceTargetIdsByRuntimeEnemy.TryGetValue(enemy, out sourceTargetId) &&
                !string.IsNullOrWhiteSpace(sourceTargetId))
            {
                return true;
            }

            sourceTargetId = string.Empty;
            return false;
        }

        /// <summary>
        /// 获取离 Agent 最近的存活敌人
        /// </summary>
        /// <param name="agentPosition"></param>
        /// <param name="enemy"></param>
        /// <returns></returns>
        public bool TryGetNearestAliveEnemy(
            Vector3 agentPosition,
            out global::EnemyHealthController enemy)
        {
            enemy = null;
            float nearestDistanceSqr = float.MaxValue;

            FindNearestAliveEnemy(_initialEnemies, agentPosition, ref nearestDistanceSqr, ref enemy);
            FindNearestAliveEnemy(_runtimeEnemies, agentPosition, ref nearestDistanceSqr, ref enemy);
            return enemy != null;
        }

        /// <summary>
        /// 标记群内某个敌人已经接战
        /// </summary>
        /// <param name="enemy"></param>
        public void MarkEnemyTouched(global::EnemyHealthController enemy)
        {
            GameplayTargetEntityMember member = FindEnemyMember(enemy);
            if (member == null)
                return;

            member.MarkTouched();
            MarkTouched();
            RefreshRuntimeState();
        }

        /// <summary>
        /// 标记群内某个敌人已经被消灭
        /// </summary>
        /// <param name="enemy"></param>
        public void MarkEnemyCompleted(global::EnemyHealthController enemy)
        {
            GameplayTargetEntityMember member = FindEnemyMember(enemy);
            if (member == null)
                return;

            member.MarkTouched();
            member.MarkCompleted();
            RefreshRuntimeState();
        }

        protected override void CollectMemberPositions(List<Vector3> memberPositions)
        {
            CollectAliveEnemyPositions(_initialEnemies, memberPositions);
            CollectAliveEnemyPositions(_runtimeEnemies, memberPositions);
        }

        protected override void RefreshRuntimeState()
        {
            EnsureMemberIds();
            RemoveMissingRuntimeEnemies();

            bool hasAnyEnemy = false;
            bool hasTouchedEnemy = false;
            bool hasIncompleteEnemy = false;

            RefreshEnemyMemberStates(_initialEnemies, ref hasAnyEnemy, ref hasTouchedEnemy, ref hasIncompleteEnemy);
            RefreshEnemyMemberStates(_runtimeEnemies, ref hasAnyEnemy, ref hasTouchedEnemy, ref hasIncompleteEnemy);

            SetAggregatedState(hasTouchedEnemy, hasAnyEnemy && !hasIncompleteEnemy);
        }

        [ContextMenu("Generate Missing Member Ids")]
        private void GenerateMissingMemberIds()
        {
            EnsureMemberIds();
        }

        // 所有敌人注册入口共用同一套去重和刷新流程
        private void RegisterEnemy(
            global::EnemyHealthController enemy,
            List<GameplayTargetEntityMember> members,
            string idLabel,
            string sourceTargetId = "")
        {
            if (enemy == null || members == null)
                return;

            if (ContainsEnemy(enemy))
            {
                RememberRuntimeEnemySource(enemy, sourceTargetId);
                return;
            }

            string entityId = $"{TargetId}_{idLabel}_{enemy.GetInstanceID()}";
            members.Add(new GameplayTargetEntityMember(entityId, enemy.gameObject));
            RememberRuntimeEnemySource(enemy, sourceTargetId);
            RefreshRuntimeState();
            RefreshRangeShape();
        }

        private void RememberRuntimeEnemySource(
            global::EnemyHealthController enemy,
            string sourceTargetId)
        {
            if (enemy == null || string.IsNullOrWhiteSpace(sourceTargetId))
                return;

            _sourceTargetIdsByRuntimeEnemy[enemy] = sourceTargetId.Trim();
        }

        // 补齐初始敌人成员 ID，运行时生成敌人会使用实例 ID 生成临时成员 ID
        private void EnsureMemberIds()
        {
            if (_initialEnemies == null)
                return;

            for (int i = 0; i < _initialEnemies.Count; i++)
            {
                _initialEnemies[i]?.EnsureEntityId(TargetId, i);
            }
        }

        // 先查场景初始敌人，再查运行时生成敌人
        private GameplayTargetEntityMember FindEnemyMember(global::EnemyHealthController enemy)
        {
            GameplayTargetEntityMember member = FindEnemyMember(_initialEnemies, enemy);
            return member ?? FindEnemyMember(_runtimeEnemies, enemy);
        }

        // 在指定成员列表中查找敌人对应的实体成员
        private static GameplayTargetEntityMember FindEnemyMember(
            List<GameplayTargetEntityMember> members,
            global::EnemyHealthController enemy)
        {
            if (members == null || enemy == null)
                return null;

            for (int i = 0; i < members.Count; i++)
            {
                GameplayTargetEntityMember member = members[i];
                if (member != null &&
                    member.TryGetComponent(out global::EnemyHealthController memberEnemy) &&
                    memberEnemy == enemy)
                {
                    return member;
                }
            }

            return null;
        }

        // 判断指定成员列表中是否包含敌人
        private static bool ContainsEnemy(
            List<GameplayTargetEntityMember> members,
            global::EnemyHealthController enemy)
        {
            return FindEnemyMember(members, enemy) != null;
        }

        // 从成员列表中选出最近的存活敌人
        private static void FindNearestAliveEnemy(
            List<GameplayTargetEntityMember> members,
            Vector3 agentPosition,
            ref float nearestDistanceSqr,
            ref global::EnemyHealthController nearestEnemy)
        {
            if (members == null)
                return;

            for (int i = 0; i < members.Count; i++)
            {
                GameplayTargetEntityMember member = members[i];
                if (!TryGetAliveEnemy(member, out global::EnemyHealthController enemy))
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, enemy.transform.position);
                if (distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestEnemy = enemy;
                nearestDistanceSqr = distanceSqr;
            }
        }

        // 动态范围优先使用存活敌人的位置
        private static void CollectAliveEnemyPositions(
            List<GameplayTargetEntityMember> members,
            List<Vector3> memberPositions)
        {
            if (members == null)
                return;

            for (int i = 0; i < members.Count; i++)
            {
                if (TryGetAliveEnemy(members[i], out global::EnemyHealthController enemy))
                    memberPositions.Add(enemy.transform.position);
            }
        }

        // 根据敌人当前状态回填成员接触和完成状态
        private static void RefreshEnemyMemberStates(
            List<GameplayTargetEntityMember> members,
            ref bool hasAnyEnemy,
            ref bool hasTouchedEnemy,
            ref bool hasIncompleteEnemy)
        {
            if (members == null)
                return;

            for (int i = 0; i < members.Count; i++)
            {
                GameplayTargetEntityMember member = members[i];
                if (member == null)
                    continue;

                hasAnyEnemy = true;
                RefreshMemberCompletionFromEnemy(member);
                hasTouchedEnemy |= member.HasBeenTouched;
                hasIncompleteEnemy |= !member.HasBeenCompleted;
            }
        }

        // 敌人死亡或对象被销毁时，成员视为完成
        private static void RefreshMemberCompletionFromEnemy(GameplayTargetEntityMember member)
        {
            if (member == null || member.HasBeenCompleted)
                return;

            if (!TryGetEnemy(member, out global::EnemyHealthController enemy))
            {
                if (Application.isPlaying)
                    member.MarkCompleted();

                return;
            }

            if (!enemy.IsAlive)
                member.MarkCompleted();
        }

        // 运行时敌人成员保留完成状态，只清掉异常空成员
        private void RemoveMissingRuntimeEnemies()
        {
            for (int i = _runtimeEnemies.Count - 1; i >= 0; i--)
            {
                GameplayTargetEntityMember member = _runtimeEnemies[i];
                if (member == null)
                {
                    _runtimeEnemies.RemoveAt(i);
                    PruneRuntimeEnemySourceMap();
                }
            }
        }

        private void PruneRuntimeEnemySourceMap()
        {
            if (_sourceTargetIdsByRuntimeEnemy.Count == 0)
                return;

            List<global::EnemyHealthController> staleEnemies = null;
            foreach (KeyValuePair<global::EnemyHealthController, string> pair in _sourceTargetIdsByRuntimeEnemy)
            {
                if (pair.Key != null && ContainsEnemy(_runtimeEnemies, pair.Key))
                    continue;

                staleEnemies ??= new List<global::EnemyHealthController>();
                staleEnemies.Add(pair.Key);
            }

            if (staleEnemies == null)
                return;

            for (int i = 0; i < staleEnemies.Count; i++)
                _sourceTargetIdsByRuntimeEnemy.Remove(staleEnemies[i]);
        }

        private static bool TryGetAliveEnemy(
            GameplayTargetEntityMember member,
            out global::EnemyHealthController enemy)
        {
            if (!TryGetEnemy(member, out enemy))
                return false;

            return enemy.IsAlive;
        }

        private static bool TryGetEnemy(
            GameplayTargetEntityMember member,
            out global::EnemyHealthController enemy)
        {
            enemy = null;
            return member != null && member.TryGetComponent(out enemy) && enemy != null;
        }

        private int CountRegisteredEnemies()
        {
            int count = 0;
            count += _initialEnemies != null ? _initialEnemies.Count : 0;
            count += _runtimeEnemies.Count;
            return count;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }
    }
}
