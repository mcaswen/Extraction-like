using System;
using System.Collections.Generic;
using Gameplay.Targets.Authoring;
using UnityEngine;

/// <summary>
/// Scene enemy tier rules used by enemy source clusters.
/// </summary>
[CreateAssetMenu(fileName = "SO_SceneEnemyTierRuleSet", menuName = "Raid/Scene Enemy Tier Rule Set")]
public sealed class SceneEnemyTierRuleSet : ScriptableObject
{
    [SerializeField] private List<SceneEnemyTierRuleDefinition> _tierRules =
        new List<SceneEnemyTierRuleDefinition>
        {
            new SceneEnemyTierRuleDefinition(SceneEnemyDangerTier.Low, 1f),
            new SceneEnemyTierRuleDefinition(SceneEnemyDangerTier.Medium, 1.2f),
            new SceneEnemyTierRuleDefinition(SceneEnemyDangerTier.High, 1.4f)
        };

    public float ResolveMaxHealthMultiplier(SceneEnemyDangerTier dangerTier)
    {
        SceneEnemyDangerTier resolvedTier = ResolveConcreteTier(dangerTier);
        if (_tierRules != null)
        {
            for (int i = 0; i < _tierRules.Count; i++)
            {
                SceneEnemyTierRuleDefinition rule = _tierRules[i];
                if (rule != null && rule.DangerTier == resolvedTier)
                    return rule.MaxHealthMultiplier;
            }
        }

        return ResolveDefaultMaxHealthMultiplier(resolvedTier);
    }

    private void OnValidate()
    {
        _tierRules ??= new List<SceneEnemyTierRuleDefinition>();
        for (int i = 0; i < _tierRules.Count; i++)
        {
            _tierRules[i]?.Validate();
        }
    }

    private static SceneEnemyDangerTier ResolveConcreteTier(SceneEnemyDangerTier dangerTier)
    {
        return dangerTier == SceneEnemyDangerTier.Inherit
            ? SceneEnemyDangerTier.Low
            : dangerTier;
    }

    private static float ResolveDefaultMaxHealthMultiplier(SceneEnemyDangerTier dangerTier)
    {
        switch (ResolveConcreteTier(dangerTier))
        {
            case SceneEnemyDangerTier.Medium:
                return 1.2f;
            case SceneEnemyDangerTier.High:
                return 1.4f;
            default:
                return 1f;
        }
    }
}

[Serializable]
public sealed class SceneEnemyTierRuleDefinition
{
    [SerializeField] private SceneEnemyDangerTier _dangerTier = SceneEnemyDangerTier.Low;
    [SerializeField, Min(0.01f)] private float _maxHealthMultiplier = 1f;

    public SceneEnemyTierRuleDefinition(SceneEnemyDangerTier dangerTier, float maxHealthMultiplier)
    {
        _dangerTier = dangerTier;
        _maxHealthMultiplier = Mathf.Max(0.01f, maxHealthMultiplier);
    }

    public SceneEnemyDangerTier DangerTier => _dangerTier;
    public float MaxHealthMultiplier => Mathf.Max(0.01f, _maxHealthMultiplier);

    public void Validate()
    {
        if (_dangerTier == SceneEnemyDangerTier.Inherit)
            _dangerTier = SceneEnemyDangerTier.Low;

        _maxHealthMultiplier = Mathf.Max(0.01f, _maxHealthMultiplier);
    }
}
