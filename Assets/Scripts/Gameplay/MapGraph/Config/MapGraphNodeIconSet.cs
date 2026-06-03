using System;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>
    /// 抽象图节点图标语义类型
    /// 复刻旧桌游节点图标规则，但不依赖 Obsolete BoardGame 类型
    /// </summary>
    public enum MapGraphNodeIconKind
    {
        None = 0,
        Start = 1,
        Resource = 2,
        Enemy = 3,
        Boss = 4,
        Extraction = 5
    }

    /// <summary>
    /// 抽象图资源等级
    /// </summary>
    public enum MapGraphResourceTier
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    /// <summary>
    /// 抽象图敌人危险等级
    /// </summary>
    public enum MapGraphDangerTier
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    /// <summary>
    /// 节点图标配置
    /// 图标选择规则与旧桌游 BoardGameNodeIconSet 保持一致
    /// </summary>
    [Serializable]
    public sealed class MapGraphNodeIconSet
    {
        [Header("Start Icon")]
        [SerializeField] private Sprite _startIcon;

        [Header("Resource Icons")]
        [SerializeField] private Sprite _resourceLowIcon;
        [SerializeField] private Sprite _resourceMediumIcon;
        [SerializeField] private Sprite _resourceHighIcon;

        [Header("Enemy Icons")]
        [SerializeField] private Sprite _enemyLowIcon;
        [SerializeField] private Sprite _enemyMediumIcon;
        [SerializeField] private Sprite _enemyHighIcon;

        [Header("Boss Icon")]
        [SerializeField] private Sprite _bossIcon;

        [Header("Extract Icon")]
        [SerializeField] private Sprite _extractIcon;

        public Sprite GetIcon(
            MapGraphNodeIconKind iconKind,
            MapGraphResourceTier resourceTier,
            MapGraphDangerTier dangerTier)
        {
            switch (iconKind)
            {
                case MapGraphNodeIconKind.Start:
                    return _startIcon;

                case MapGraphNodeIconKind.Resource:
                    switch (resourceTier)
                    {
                        case MapGraphResourceTier.Low:
                            return _resourceLowIcon;
                        case MapGraphResourceTier.Medium:
                            return _resourceMediumIcon;
                        case MapGraphResourceTier.High:
                            return _resourceHighIcon;
                        default:
                            return null;
                    }

                case MapGraphNodeIconKind.Enemy:
                    switch (dangerTier)
                    {
                        case MapGraphDangerTier.Low:
                            return _enemyLowIcon;
                        case MapGraphDangerTier.Medium:
                            return _enemyMediumIcon;
                        case MapGraphDangerTier.High:
                            return _enemyHighIcon;
                        default:
                            return null;
                    }

                case MapGraphNodeIconKind.Boss:
                    return _bossIcon;

                case MapGraphNodeIconKind.Extraction:
                    return _extractIcon;

                default:
                    return null;
            }
        }
    }
}
