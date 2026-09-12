using Gameplay.Targets.Runtime;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>将一次直接受击通知已注册的同群存活成员，不造成伤害或递归扩散。</summary>
internal static class EnemyClusterCombatAlert
{
    public static void Notify(EnemyHealthController victim, Transform attacker)
    {
        var registry = GameplayTargetRegistry.ActiveInstance;
        if (victim == null || registry == null || !EnemyCombatTargetBinding.TryCreate(attacker, out var binding) ||
            !registry.TryFindEnemyClusterByEnemy(victim, out var cluster) || !cluster.isActiveAndEnabled)
            return;

        // 使用调用局部的池化列表：嵌套伤害也不会覆盖外层通知快照。
        var members = ListPool<EnemyHealthController>.Get();
        var receivers = ListPool<IEnemyCombatAlertReceiver>.Get();
        try
        {
            cluster.CopyAliveEnemiesTo(members);
            foreach (var member in members)
            {
                if (member == null || member == victim || !member.IsAlive) continue;
                member.GetComponents(receivers);
                foreach (var receiver in receivers)
                {
                    if (receiver is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                    receiver.NotifyCombatAlert(binding.Target);
                }
                receivers.Clear();
            }
        }
        finally
        {
            ListPool<IEnemyCombatAlertReceiver>.Release(receivers);
            ListPool<EnemyHealthController>.Release(members);
        }
    }
}
