using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds chase and boss indicator sprite bindings to enemy pawn prefabs.
/// </summary>
public static class EnemyChaseIndicatorPrefabBinder
{
    private const string ChaseSpritePath = "Assets/Art/Sprites/UI/Attack_range_Enemy_lock-on_indicator_Boss_symbol/IMG_0583.PNG";
    private const string BossSpritePath = "Assets/Art/Sprites/UI/Attack_range_Enemy_lock-on_indicator_Boss_symbol/IMG_0606.PNG";
    private const string EnemyPawnFolder = "Assets/Prefabs/Enemy/Pawn";
    private const string HunterBossPrefabPath = "Assets/Prefabs/Enemy/Pawn/Boss/Pfb_Enemy_HunterBoss.prefab";
    private const string ChaseCanvasName = "ChaseIndicatorCanvas";
    private const string ChaseImageName = "ChaseIndicatorImage";
    private const string BossCanvasName = "BossIndicatorCanvas";
    private const string BossImageName = "BossIndicatorImage";

    [MenuItem("Tools/Enemy/Bind Chase Indicators")]
    public static void BindAllEnemyPawnPrefabs()
    {
        Sprite chaseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChaseSpritePath);
        if (chaseSprite == null)
        {
            Debug.LogError($"Could not load chase indicator sprite at {ChaseSpritePath}.");
            return;
        }

        Sprite bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossSpritePath);
        if (bossSprite == null)
        {
            Debug.LogError($"Could not load boss indicator sprite at {BossSpritePath}.");
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPawnFolder });
        int updatedCount = 0;
        int skippedCount = 0;
        int bossIndicatorCount = 0;

        for (int index = 0; index < prefabGuids.Length; index++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!HasSupportedChaseState(prefabRoot))
                {
                    skippedCount++;
                    continue;
                }

                EnemyChaseIndicatorController chaseIndicator = GetOrAddIndicator(
                    prefabRoot,
                    EnemyChaseIndicatorController.IndicatorVisibilityMode.ChaseOnly,
                    ChaseCanvasName);
                chaseIndicator.ConfigureIndicator(
                    chaseSprite,
                    EnemyChaseIndicatorController.IndicatorVisibilityMode.ChaseOnly,
                    ChaseCanvasName,
                    ChaseImageName,
                    new Vector3(0f, 2.1f, 0f),
                    new Vector2(56f, 56f),
                    0.01f,
                    1.05f,
                    10);

                if (prefabPath == HunterBossPrefabPath)
                {
                    EnemyChaseIndicatorController bossIndicator = GetOrAddIndicator(
                        prefabRoot,
                        EnemyChaseIndicatorController.IndicatorVisibilityMode.Always,
                        BossCanvasName);
                    bossIndicator.ConfigureIndicator(
                        bossSprite,
                        EnemyChaseIndicatorController.IndicatorVisibilityMode.Always,
                        BossCanvasName,
                        BossImageName,
                        new Vector3(0f, 2.85f, 0f),
                        new Vector2(64f, 64f),
                        0.01f,
                        1.65f,
                        11);
                    bossIndicatorCount++;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                updatedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Bound chase indicators on {updatedCount} enemy prefab(s), boss indicators on {bossIndicatorCount} prefab(s); skipped {skippedCount} prefab(s).");
    }

    private static bool HasSupportedChaseState(GameObject prefabRoot)
    {
        return prefabRoot.GetComponentInChildren<EnemyBehaviorController>(true) != null ||
               prefabRoot.GetComponentInChildren<RangedEnemyBehaviorController>(true) != null ||
               prefabRoot.GetComponentInChildren<ModernStranderBehaviorController>(true) != null ||
               prefabRoot.GetComponentInChildren<TidalAberrationBehaviorController>(true) != null ||
               prefabRoot.GetComponentInChildren<AncientStranderBehaviorController>(true) != null ||
               prefabRoot.GetComponentInChildren<HunterBossBehaviorController>(true) != null;
    }

    private static EnemyChaseIndicatorController GetOrAddIndicator(
        GameObject prefabRoot,
        EnemyChaseIndicatorController.IndicatorVisibilityMode visibilityMode,
        string canvasObjectName)
    {
        EnemyChaseIndicatorController[] indicators =
            prefabRoot.GetComponents<EnemyChaseIndicatorController>();
        for (int index = 0; index < indicators.Length; index++)
        {
            EnemyChaseIndicatorController indicator = indicators[index];
            if (indicator != null && indicator.MatchesBinding(visibilityMode, canvasObjectName))
            {
                return indicator;
            }
        }

        for (int index = 0; index < indicators.Length; index++)
        {
            EnemyChaseIndicatorController indicator = indicators[index];
            if (indicator != null && indicator.VisibilityMode == visibilityMode)
            {
                return indicator;
            }
        }

        return prefabRoot.AddComponent<EnemyChaseIndicatorController>();
    }
}
