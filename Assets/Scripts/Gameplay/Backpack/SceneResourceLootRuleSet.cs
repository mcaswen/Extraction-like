using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared loot rules for scene resource points.
/// Resource boxes use this table to pick concrete items after rolling tier rarity.
/// </summary>
[CreateAssetMenu(fileName = "SO_SceneResourceLootRuleSet", menuName = "Raid/Scene Resource Loot Rule Set")]
public sealed class SceneResourceLootRuleSet : ScriptableObject
{
    [Header("Loot Table")]
    [SerializeField] private List<LootGenerationEntry> _lootTable = new List<LootGenerationEntry>();

    [Header("Low Tier")]
    [SerializeField] private Vector2Int _lowTierRollCount = new Vector2Int(1, 3);
    [SerializeField] private Vector3 _lowTierCommonUncommonRareWeights = new Vector3(45f, 35f, 20f);

    [Header("Medium Tier")]
    [SerializeField] private Vector2Int _mediumTierRollCount = new Vector2Int(2, 4);
    [SerializeField] private Vector4 _mediumTierCommonUncommonRareEpicWeights = new Vector4(10f, 40f, 35f, 15f);

    [Header("High Tier")]
    [SerializeField] private Vector2Int _highTierRollCount = new Vector2Int(3, 5);
    [SerializeField] private Vector4 _highTierUncommonRareEpicLegendaryWeights = new Vector4(30f, 30f, 25f, 15f);

    public IReadOnlyList<LootGenerationEntry> LootTable => _lootTable;
    public bool HasLootTable => _lootTable != null && _lootTable.Count > 0;

    public int ResolveRollCount(SceneResourceTier resourceTier)
    {
        Vector2Int range = resourceTier switch
        {
            SceneResourceTier.Low => _lowTierRollCount,
            SceneResourceTier.Medium => _mediumTierRollCount,
            SceneResourceTier.High => _highTierRollCount,
            _ => _lowTierRollCount
        };

        int min = Mathf.Max(0, Mathf.Min(range.x, range.y));
        int max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return Random.Range(min, max + 1);
    }

    public bool TryRollRarity(
        SceneResourceTier resourceTier,
        System.Func<ItemRarity, bool> hasLootEntryForRarity,
        out ItemRarity rarity)
    {
        rarity = ItemRarity.Common;
        float commonWeight = 0f;
        float uncommonWeight = 0f;
        float rareWeight = 0f;
        float epicWeight = 0f;
        float legendaryWeight = 0f;

        switch (resourceTier)
        {
            case SceneResourceTier.Low:
                commonWeight = _lowTierCommonUncommonRareWeights.x;
                uncommonWeight = _lowTierCommonUncommonRareWeights.y;
                rareWeight = _lowTierCommonUncommonRareWeights.z;
                break;
            case SceneResourceTier.Medium:
                commonWeight = _mediumTierCommonUncommonRareEpicWeights.x;
                uncommonWeight = _mediumTierCommonUncommonRareEpicWeights.y;
                rareWeight = _mediumTierCommonUncommonRareEpicWeights.z;
                epicWeight = _mediumTierCommonUncommonRareEpicWeights.w;
                break;
            case SceneResourceTier.High:
                uncommonWeight = _highTierUncommonRareEpicLegendaryWeights.x;
                rareWeight = _highTierUncommonRareEpicLegendaryWeights.y;
                epicWeight = _highTierUncommonRareEpicLegendaryWeights.z;
                legendaryWeight = _highTierUncommonRareEpicLegendaryWeights.w;
                break;
        }

        return TryPickAvailableRarity(
            hasLootEntryForRarity,
            commonWeight,
            uncommonWeight,
            rareWeight,
            epicWeight,
            legendaryWeight,
            out rarity);
    }

    private static bool TryPickAvailableRarity(
        System.Func<ItemRarity, bool> hasLootEntryForRarity,
        float commonWeight,
        float uncommonWeight,
        float rareWeight,
        float epicWeight,
        float legendaryWeight,
        out ItemRarity rarity)
    {
        rarity = ItemRarity.Common;
        float totalWeight = 0f;
        totalWeight += HasCandidate(hasLootEntryForRarity, ItemRarity.Common) ? Mathf.Max(0f, commonWeight) : 0f;
        totalWeight += HasCandidate(hasLootEntryForRarity, ItemRarity.Uncommon) ? Mathf.Max(0f, uncommonWeight) : 0f;
        totalWeight += HasCandidate(hasLootEntryForRarity, ItemRarity.Rare) ? Mathf.Max(0f, rareWeight) : 0f;
        totalWeight += HasCandidate(hasLootEntryForRarity, ItemRarity.Epic) ? Mathf.Max(0f, epicWeight) : 0f;
        totalWeight += HasCandidate(hasLootEntryForRarity, ItemRarity.Legendary) ? Mathf.Max(0f, legendaryWeight) : 0f;

        if (totalWeight <= Mathf.Epsilon)
        {
            return false;
        }

        float roll = Random.Range(0f, totalWeight);
        float cursor = 0f;

        return TryAdvanceRarityRoll(hasLootEntryForRarity, ItemRarity.Common, commonWeight, roll, ref cursor, out rarity) ||
               TryAdvanceRarityRoll(hasLootEntryForRarity, ItemRarity.Uncommon, uncommonWeight, roll, ref cursor, out rarity) ||
               TryAdvanceRarityRoll(hasLootEntryForRarity, ItemRarity.Rare, rareWeight, roll, ref cursor, out rarity) ||
               TryAdvanceRarityRoll(hasLootEntryForRarity, ItemRarity.Epic, epicWeight, roll, ref cursor, out rarity) ||
               TryAdvanceRarityRoll(hasLootEntryForRarity, ItemRarity.Legendary, legendaryWeight, roll, ref cursor, out rarity);
    }

    private static bool TryAdvanceRarityRoll(
        System.Func<ItemRarity, bool> hasLootEntryForRarity,
        ItemRarity candidateRarity,
        float weight,
        float roll,
        ref float cursor,
        out ItemRarity rarity)
    {
        rarity = ItemRarity.Common;
        if (weight <= 0f || !HasCandidate(hasLootEntryForRarity, candidateRarity))
        {
            return false;
        }

        cursor += weight;
        if (roll > cursor)
        {
            return false;
        }

        rarity = candidateRarity;
        return true;
    }

    private static bool HasCandidate(System.Func<ItemRarity, bool> hasLootEntryForRarity, ItemRarity rarity)
    {
        return hasLootEntryForRarity != null && hasLootEntryForRarity(rarity);
    }
}
