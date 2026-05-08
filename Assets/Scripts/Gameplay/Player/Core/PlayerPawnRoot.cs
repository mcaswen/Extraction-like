using UnityEngine;
using Core.BehaviorTree.Blackboard;
using Gameplay.Player.Data;
using Gameplay.Player.Interfaces;
using Gameplay.Player.SO;

namespace Gameplay.Player.Core
{
    /// <summary>
    /// 主角实体总入口。
    /// 当前阶段负责承载最小身体事实，并桥接 Brain 与干预层。
    /// </summary>
    public sealed class PlayerPawnRoot : MonoBehaviour, IPlayerReadOnly, IPlayerCommandReceiver
    {
        [Header("Data")]
        [SerializeField] private PlayerPawnConfig _pawnConfig;

        private int _currentHealth;

        private PlayerBrainController _brainController;
        private PlayerInterventionController _interventionController;

        public Transform CachedTransform => transform;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;

        public PlayerMacroStateId CurrentMacroStateId =>
            _brainController != null ? _brainController.CurrentMacroStateId : PlayerMacroStateId.None;

        public string CurrentMacroStateName => CurrentMacroStateId.ToString();

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _pawnConfig != null ? _pawnConfig.MaxHealth : 0;
        public float HealthRatio => MaxHealth <= 0 ? 0f : (float)_currentHealth / MaxHealth;
        public bool IsDead => _currentHealth <= 0;

        public BehaviorBlackboard Blackboard =>
            _brainController != null ? _brainController.Blackboard : null;

        private void Awake()
        {
            if (_pawnConfig == null)
            {
                Debug.LogError("PlayerPawnRoot 缺少 PlayerPawnConfig 配置。", this);
                enabled = false;
                return;
            }

            _currentHealth = _pawnConfig.MaxHealth;

            _brainController = new PlayerBrainController(this);
            _interventionController = new PlayerInterventionController(_brainController.Blackboard);

            InitializeBlackboardFacts(Time.timeAsDouble);
            _brainController.Start(Time.timeAsDouble);
        }

        private void Update()
        {
            double timeSeconds = Time.timeAsDouble;

            // 每帧先把身体层事实同步给 Brain
            SyncBodyFactsToBlackboard(timeSeconds);

            // 驱动自主 Brain 更新
            _brainController.Tick(Time.deltaTime, timeSeconds);
        }

        /// <summary>
        /// 使主角受到伤害
        /// </summary>
        /// <param name="damageRequest"></param>
        public void ApplyDamage(DamageRequest damageRequest)
        {
            if (IsDead)
                return;

            _currentHealth = Mathf.Max(0, _currentHealth - Mathf.Max(0, damageRequest.DamageAmount));
            SyncBodyFactsToBlackboard(Time.timeAsDouble);
        }

        /// <summary>
        /// 设置主角是否感知到敌人
        /// </summary>
        /// <param name="hasVisibleEnemy"></param>
        public void SetVisibleEnemy(bool hasVisibleEnemy)
        {
            _brainController.SetFact(
                PlayerBlackboardKeys.HasVisibleEnemy,
                hasVisibleEnemy,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 设置主角当前是否有可搜索资源点
        /// </summary>
        /// <param name="hasResourceTarget"></param>
        public void SetHasResourceTarget(bool hasResourceTarget)
        {
            _brainController.SetFact(
                PlayerBlackboardKeys.HasResourceTarget,
                hasResourceTarget,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 设置主角当前是否有可交互目标
        /// </summary>
        /// <param name="hasInteractableTarget"></param>
        public void SetHasInteractableTarget(bool hasInteractableTarget)
        {
            _brainController.SetFact(
                PlayerBlackboardKeys.HasInteractableTarget,
                hasInteractableTarget,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 设置主角当前是否应该撤离
        /// </summary>
        /// <param name="shouldExtract"></param>
        public void SetShouldExtract(bool shouldExtract)
        {
            _brainController.SetFact(
                PlayerBlackboardKeys.ShouldExtract,
                shouldExtract,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 提交一个玩家干预请求。
        /// 当前阶段只做缓存，不在 Pawn Root 中解释业务。
        /// </summary>
        /// <param name="directiveRequest"></param>
        public void SubmitDirective(PlayerDirectiveRequest directiveRequest)
        {
            _interventionController.SubmitDirective(directiveRequest, Time.timeAsDouble);
        }

        /// <summary>
        /// 清除当前待处理的玩家干预请求。
        /// </summary>
        public void ClearDirective()
        {
            _interventionController.ClearDirective(Time.timeAsDouble);
        }

        // 初始化 Brain 启动所需的默认事实，
        // 避免状态机第一帧读取到未初始化的黑板值
        private void InitializeBlackboardFacts(double timeSeconds)
        {
            _brainController.SetFact(PlayerBlackboardKeys.PlayerIsDead, false, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.PlayerHealthRatio, 1f, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.HasVisibleEnemy, false, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.HasResourceTarget, false, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.HasInteractableTarget, false, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.ShouldExtract, false, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.NeedRecovery, false, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.HasPendingDirective, false, timeSeconds);
        }

        // 将 Pawn 身体层事实同步给 Brain
        private void SyncBodyFactsToBlackboard(double timeSeconds)
        {
            bool isDead = IsDead;
            bool needRecovery = !isDead && HealthRatio <= _pawnConfig.LowHealthRecoveryThreshold;

            _brainController.SetFact(PlayerBlackboardKeys.PlayerIsDead, isDead, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.PlayerHealthRatio, HealthRatio, timeSeconds);
            _brainController.SetFact(PlayerBlackboardKeys.NeedRecovery, needRecovery, timeSeconds);
        }
    }
}