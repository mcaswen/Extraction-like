using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EnemyConfigMigrationTool
{
    private const string ConfigFolder = "Assets/SO/Enemies";
    private const string DefaultDeathLootPrefabGuid = "4971d3cf12ab63c4183454288ef16c4d";

    private static readonly List<EnemyMigrationEntry> Entries = new List<EnemyMigrationEntry>
    {
        new EnemyMigrationEntry(
            "8562dd5194f3dd7438edafa46e9e0a89",
            "Assets/SO/Enemies/SO_Enemy_BasicMelee.asset",
            typeof(EnemyBehaviorController),
            typeof(MeleeEnemyConfig)),
        new EnemyMigrationEntry(
            "260fb51eef7977e4299d42a43467ef33",
            "Assets/SO/Enemies/SO_Enemy_Ranged.asset",
            typeof(RangedEnemyBehaviorController),
            typeof(RangedEnemyConfig)),
        new EnemyMigrationEntry(
            "9cb4030f7647c174f94c0b6d322155d5",
            "Assets/SO/Enemies/SO_Enemy_ModernStrander.asset",
            typeof(ModernStranderBehaviorController),
            typeof(ModernStranderConfig)),
        new EnemyMigrationEntry(
            "bc3dbc01d8508334fb1851a55058382a",
            "Assets/SO/Enemies/SO_Enemy_TidalAberration.asset",
            typeof(TidalAberrationBehaviorController),
            typeof(TidalAberrationConfig)),
        new EnemyMigrationEntry(
            "fb9ea2911091c9043a261f4feb461700",
            "Assets/SO/Enemies/SO_Enemy_AncientStrander.asset",
            typeof(AncientStranderBehaviorController),
            typeof(AncientStranderConfig)),
        new EnemyMigrationEntry(
            "9f887a3dd0771e6428206bc84b5273b6",
            "Assets/SO/Enemies/SO_Enemy_AnchorSentinel.asset",
            typeof(AnchorSentinelBehaviorController),
            typeof(AnchorSentinelConfig)),
        new EnemyMigrationEntry(
            "a75329ef5f0ca7a42a88b4f3db576821",
            "Assets/SO/Enemies/SO_Enemy_HunterBoss.asset",
            typeof(HunterBossBehaviorController),
            typeof(HunterBossConfig))
    };

    [MenuItem("Tools/Enemies/Create Runtime Enemy Config Assets")]
    public static void CreateRuntimeEnemyConfigAssets()
    {
        EnsureFolder(ConfigFolder);

        for (int i = 0; i < Entries.Count; i++)
        {
            MigrateEntry(Entries[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Enemy config migration completed.");
    }

    private static void MigrateEntry(EnemyMigrationEntry entry)
    {
        string prefabPath = AssetDatabase.GUIDToAssetPath(entry.PrefabGuid);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Enemy config migration skipped missing prefab guid: {entry.PrefabGuid}");
            return;
        }

        MonoBehaviour behavior = prefab.GetComponent(entry.BehaviorType) as MonoBehaviour;
        if (behavior == null)
        {
            behavior = prefab.GetComponentInChildren(entry.BehaviorType, true) as MonoBehaviour;
        }

        if (behavior == null)
        {
            Debug.LogWarning($"Enemy config migration skipped {entry.PrefabPath}; no {entry.BehaviorType.Name} found.");
            return;
        }

        EnemyHealthController health = prefab.GetComponent<EnemyHealthController>();
        if (health == null)
        {
            health = prefab.GetComponentInChildren<EnemyHealthController>(true);
        }

        EnemyHealthConfigBase config = AssetDatabase.LoadAssetAtPath<EnemyHealthConfigBase>(entry.ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance(entry.ConfigType) as EnemyHealthConfigBase;
            AssetDatabase.CreateAsset(config, entry.ConfigPath);
        }

        SerializedObject configObject = new SerializedObject(config);
        SerializedObject behaviorObject = new SerializedObject(behavior);
        SerializedObject healthObject = health != null ? new SerializedObject(health) : null;

        SetString(configObject, "_enemyId", MakeEnemyId(entry.ConfigPath));
        SetString(configObject, "_displayName", prefab.name);
        CopyHealth(healthObject, configObject);
        EnsureDefaultDeathLoot(configObject, entry.ConfigType);

        if (entry.ConfigType == typeof(MeleeEnemyConfig))
        {
            CopyPatrolAndDetection(behaviorObject, configObject);
            CopyFloat(behaviorObject, "AttackRange", configObject, "_attackRange");
            CopyFloat(behaviorObject, "AttackDamage", configObject, "_attackDamage");
            CopyFloat(behaviorObject, "AttackInterval", configObject, "_attackInterval");
        }
        else if (entry.ConfigType == typeof(RangedEnemyConfig))
        {
            CopyPatrolAndDetection(behaviorObject, configObject);
            CopyObject(behaviorObject, "EnemyBulletPrefab", configObject, "_enemyBulletPrefab");
            CopyEnemyBulletValues(behaviorObject, configObject);
            CopyFloat(behaviorObject, "AttackRange", configObject, "_attackRange");
            CopyFloat(behaviorObject, "AttackInterval", configObject, "_attackInterval");
        }
        else if (entry.ConfigType == typeof(ModernStranderConfig))
        {
            CopyPatrolAndDetection(behaviorObject, configObject);
            CopyFloat(behaviorObject, "AttackRange", configObject, "_attackRange");
            CopyFloat(behaviorObject, "AttackInterval", configObject, "_attackInterval");
            CopyFloat(behaviorObject, "TentacleLatchDuration", configObject, "_tentacleLatchDuration");
            CopyFloat(behaviorObject, "TentacleHitboxWidth", configObject, "_tentacleHitboxWidth");
            CopyFloat(behaviorObject, "TentacleHitboxHeight", configObject, "_tentacleHitboxHeight");
            CopyFloat(behaviorObject, "LatchPullStrength", configObject, "_latchPullStrength");
            CopyFloat(behaviorObject, "CorrosionDamagePerSecond", configObject, "_corrosionDamagePerSecond");
            CopyFloat(behaviorObject, "CorrosionDuration", configObject, "_corrosionDuration");
            CopyFloat(behaviorObject, "CorrosionTickInterval", configObject, "_corrosionTickInterval");
            CopyFloat(behaviorObject, "InitialContactDamage", configObject, "_initialContactDamage");
            CopyObject(behaviorObject, "CorrosivePuddlePrefab", configObject, "_corrosivePuddlePrefab");
            CopyFloat(behaviorObject, "PuddleLifetime", configObject, "_puddleLifetime");
            CopyFloat(behaviorObject, "PuddleRadius", configObject, "_puddleRadius");
            CopyFloat(behaviorObject, "PuddleDamagePerSecond", configObject, "_puddleDamagePerSecond");
            CopyFloat(behaviorObject, "PuddleCorrosionDuration", configObject, "_puddleCorrosionDuration");
            CopyFloat(behaviorObject, "PuddleTickInterval", configObject, "_puddleTickInterval");
        }
        else if (entry.ConfigType == typeof(TidalAberrationConfig))
        {
            CopyPatrolAndDetection(behaviorObject, configObject);
            CopyFloat(behaviorObject, "MeleeAttackRange", configObject, "_meleeAttackRange");
            CopyFloat(behaviorObject, "MeleeAttackInterval", configObject, "_meleeAttackInterval");
            CopyFloat(behaviorObject, "MeleeLatchDuration", configObject, "_meleeLatchDuration");
            CopyFloat(behaviorObject, "MeleeContactDamage", configObject, "_meleeContactDamage");
            CopyFloat(behaviorObject, "SilenceDuration", configObject, "_silenceDuration");
            CopyFloat(behaviorObject, "ElectricTickDamagePerSecond", configObject, "_electricTickDamagePerSecond");
            CopyFloat(behaviorObject, "ElectricTickInterval", configObject, "_electricTickInterval");
            CopyFloat(behaviorObject, "MinimumRangedDistance", configObject, "_minimumRangedDistance");
            CopyFloat(behaviorObject, "RangedAttackRange", configObject, "_rangedAttackRange");
            CopyFloat(behaviorObject, "RangedAttackInterval", configObject, "_rangedAttackInterval");
            CopyFloat(behaviorObject, "WaterJetDuration", configObject, "_waterJetDuration");
            CopyFloat(behaviorObject, "WaterJetDamage", configObject, "_waterJetDamage");
            CopyFloat(behaviorObject, "WaterJetKnockbackStrength", configObject, "_waterJetKnockbackStrength");
            CopyFloat(behaviorObject, "WaterJetMaxDistance", configObject, "_waterJetMaxDistance");
        }
        else if (entry.ConfigType == typeof(AncientStranderConfig))
        {
            CopyPatrolAndDetection(behaviorObject, configObject);
            CopyFloat(behaviorObject, "MeleeAttackRange", configObject, "_meleeAttackRange");
            CopyFloat(behaviorObject, "MeleeAttackInterval", configObject, "_meleeAttackInterval");
            CopyFloat(behaviorObject, "MeleeAttackRadius", configObject, "_meleeAttackRadius");
            CopyFloat(behaviorObject, "MeleeDamage", configObject, "_meleeDamage");
            CopyFloat(behaviorObject, "MeleeVisualDuration", configObject, "_meleeVisualDuration");
            CopyFloat(behaviorObject, "MinimumRangedDistance", configObject, "_minimumRangedDistance");
            CopyFloat(behaviorObject, "RangedAttackRange", configObject, "_rangedAttackRange");
            CopyFloat(behaviorObject, "RangedAttackInterval", configObject, "_rangedAttackInterval");
            CopyFloat(behaviorObject, "BiteStrikeDuration", configObject, "_biteStrikeDuration");
            CopyFloat(behaviorObject, "BiteHitboxWidth", configObject, "_biteHitboxWidth");
            CopyFloat(behaviorObject, "BiteHitboxHeight", configObject, "_biteHitboxHeight");
            CopyFloat(behaviorObject, "BiteDamage", configObject, "_biteDamage");
        }
        else if (entry.ConfigType == typeof(AnchorSentinelConfig))
        {
            CopyFloat(behaviorObject, "DetectionRange", configObject, "_detectionRange");
            CopyFloat(behaviorObject, "LockDuration", configObject, "_lockDuration");
            CopyFloat(behaviorObject, "FiringDuration", configObject, "_firingDuration");
            CopyFloat(behaviorObject, "CooldownDuration", configObject, "_cooldownDuration");
            CopyFloat(behaviorObject, "ActiveRecoveryDuration", configObject, "_activeRecoveryDuration");
            CopyFloat(behaviorObject, "BeamDamagePerSecond", configObject, "_beamDamagePerSecond");
            CopyFloat(behaviorObject, "BeamTickInterval", configObject, "_beamTickInterval");
        }
        else if (entry.ConfigType == typeof(HunterBossConfig))
        {
            CopyFloat(behaviorObject, "DetectionRange", configObject, "_detectionRange");
            CopyFloat(behaviorObject, "LoseRange", configObject, "_loseRange");
            CopyFloat(behaviorObject, "ChaseSpeed", configObject, "_chaseSpeed");
            CopyFloat(behaviorObject, "MeleeAttackRange", configObject, "_meleeAttackRange");
            CopyFloat(behaviorObject, "MeleeAttackInterval", configObject, "_meleeAttackInterval");
            CopyFloat(behaviorObject, "MeleeAttackRadius", configObject, "_meleeAttackRadius");
            CopyFloat(behaviorObject, "MeleeDamage", configObject, "_meleeDamage");
            CopyFloat(behaviorObject, "MeleeKnockbackStrength", configObject, "_meleeKnockbackStrength");
            CopyFloat(behaviorObject, "MeleeVisualDuration", configObject, "_meleeVisualDuration");
            CopyObject(behaviorObject, "AnchorProjectilePrefab", configObject, "_anchorProjectilePrefab");
            CopyObject(behaviorObject, "VortexFieldPrefab", configObject, "_vortexFieldPrefab");
            CopyFloat(behaviorObject, "VortexTriggerDistance", configObject, "_vortexTriggerDistance");
            CopyFloat(behaviorObject, "VortexRadius", configObject, "_vortexRadius");
            CopyFloat(behaviorObject, "VortexChargeDuration", configObject, "_vortexChargeDuration");
            CopyFloat(behaviorObject, "VortexImmobilizeDuration", configObject, "_vortexImmobilizeDuration");
            CopyFloat(behaviorObject, "AnchorThrowSpeed", configObject, "_anchorThrowSpeed");
            CopyFloat(behaviorObject, "AnchorThrowDamage", configObject, "_anchorThrowDamage");
            CopyFloat(behaviorObject, "AnchorThrowKnockback", configObject, "_anchorThrowKnockback");
            CopyHunterAnchorProjectileValues(behaviorObject, configObject);
            CopyFloat(behaviorObject, "AnchorThrowCooldown", configObject, "_anchorThrowCooldown");
            CopyFloat(behaviorObject, "RageThreshold", configObject, "_rageThreshold");
            CopyFloat(behaviorObject, "RoarChargeDuration", configObject, "_roarChargeDuration");
            CopyFloat(behaviorObject, "RoarCooldown", configObject, "_roarCooldown");
            CopyFloat(behaviorObject, "RoarRange", configObject, "_roarRange");
            CopyFloat(behaviorObject, "RoarDamage", configObject, "_roarDamage");
            CopyLayerMask(behaviorObject, "CoverMask", configObject, "_coverMask");
            CopyFloat(behaviorObject, "CoverCheckHeight", configObject, "_coverCheckHeight");
        }

        configObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);

        SerializedProperty configReference = behaviorObject.FindProperty("_config");
        if (configReference != null)
        {
            configReference.objectReferenceValue = config;
            behaviorObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behavior);
        }

        if (healthObject != null)
        {
            SerializedProperty healthConfigReference = healthObject.FindProperty("_config");
            if (healthConfigReference != null)
            {
                healthConfigReference.objectReferenceValue = config;
                healthObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(health);
            }
        }

        PrefabUtility.SavePrefabAsset(prefab);
    }

    private static void CopyHealth(SerializedObject healthObject, SerializedObject configObject)
    {
        if (healthObject == null)
        {
            return;
        }

        CopyFloat(healthObject, "MaxHealth", configObject, "_maxHealth");
    }

    private static void EnsureDefaultDeathLoot(SerializedObject configObject, Type configType)
    {
        SerializedProperty deathLoot = configObject.FindProperty("_deathLoot");
        if (deathLoot == null)
        {
            return;
        }

        SerializedProperty spawn = deathLoot.FindPropertyRelative("_spawnLootContainerOnDeath");
        SerializedProperty prefab = deathLoot.FindPropertyRelative("_deathLootContainerPrefab");
        SerializedProperty offset = deathLoot.FindPropertyRelative("_deathLootSpawnOffset");

        if (spawn != null)
        {
            spawn.boolValue = true;
        }

        if (prefab != null && prefab.objectReferenceValue == null)
        {
            string path = AssetDatabase.GUIDToAssetPath(DefaultDeathLootPrefabGuid);
            prefab.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        if (offset != null && configType == typeof(AnchorSentinelConfig))
        {
            offset.vector3Value = new Vector3(0f, 0.25f, 0f);
        }
    }

    private static void CopyPatrolAndDetection(SerializedObject source, SerializedObject target)
    {
        CopyNestedFloat(source, "PatrolRadius", target, "_patrol", "_patrolRadius");
        CopyNestedFloat(source, "PatrolWaitTime", target, "_patrol", "_patrolWaitTime");
        CopyNestedFloat(source, "DetectionRange", target, "_detection", "_detectionRange");
        CopyNestedFloat(source, "LoseRange", target, "_detection", "_loseRange");
        SetNestedFloatIfPresent(target, "_detection", "_viewAngle", 360f);
        SetNestedLayerMaskIfPresent(target, "_detection", "_lineOfSightBlockMask", 1);
        SetNestedLayerMaskIfPresent(target, "_detection", "_groundMask", 1);
        SetNestedFloatIfPresent(target, "_detection", "_eyeHeight", 1.2f);
        SetNestedFloatIfPresent(target, "_detection", "_targetHeight", 1f);
    }

    private static void CopyEnemyBulletValues(SerializedObject behaviorObject, SerializedObject configObject)
    {
        SerializedProperty prefabProperty = behaviorObject.FindProperty("EnemyBulletPrefab");
        GameObject prefab = prefabProperty != null ? prefabProperty.objectReferenceValue as GameObject : null;
        EnemyBulletController bullet = prefab != null ? prefab.GetComponent<EnemyBulletController>() : null;
        if (bullet == null)
        {
            return;
        }

        SerializedObject bulletObject = new SerializedObject(bullet);
        CopyFloat(bulletObject, "MoveSpeed", configObject, "_bulletMoveSpeed");
        CopyFloat(bulletObject, "Damage", configObject, "_bulletDamage");
        CopyFloat(bulletObject, "LifeTime", configObject, "_bulletLifeTime");
    }

    private static void CopyHunterAnchorProjectileValues(SerializedObject behaviorObject, SerializedObject configObject)
    {
        SerializedProperty prefabProperty = behaviorObject.FindProperty("AnchorProjectilePrefab");
        GameObject prefab = prefabProperty != null ? prefabProperty.objectReferenceValue as GameObject : null;
        HunterBossAnchorProjectile projectile = prefab != null ? prefab.GetComponent<HunterBossAnchorProjectile>() : null;
        if (projectile == null)
        {
            return;
        }

        SerializedObject projectileObject = new SerializedObject(projectile);
        CopyFloat(projectileObject, "LifeTime", configObject, "_anchorProjectileLifeTime");
    }

    private static void CopyNestedFloat(SerializedObject source, string sourceName, SerializedObject target, string parentName, string childName)
    {
        SerializedProperty parent = target.FindProperty(parentName);
        SerializedProperty child = parent != null ? parent.FindPropertyRelative(childName) : null;
        SerializedProperty sourceProperty = source.FindProperty(sourceName);
        if (child != null && sourceProperty != null)
        {
            child.floatValue = sourceProperty.floatValue;
        }
    }

    private static void SetNestedFloatIfPresent(SerializedObject target, string parentName, string childName, float value)
    {
        SerializedProperty parent = target.FindProperty(parentName);
        SerializedProperty child = parent != null ? parent.FindPropertyRelative(childName) : null;
        if (child != null)
        {
            child.floatValue = value;
        }
    }

    private static void SetNestedLayerMaskIfPresent(SerializedObject target, string parentName, string childName, int value)
    {
        SerializedProperty parent = target.FindProperty(parentName);
        SerializedProperty child = parent != null ? parent.FindPropertyRelative(childName) : null;
        if (child != null)
        {
            child.intValue = value;
        }
    }

    private static void CopyFloat(SerializedObject source, string sourceName, SerializedObject target, string targetName)
    {
        SerializedProperty sourceProperty = source.FindProperty(sourceName);
        SerializedProperty targetProperty = target.FindProperty(targetName);
        if (sourceProperty != null && targetProperty != null)
        {
            targetProperty.floatValue = sourceProperty.floatValue;
        }
    }

    private static void CopyInt(SerializedObject source, string sourceName, SerializedObject target, string targetName)
    {
        SerializedProperty sourceProperty = source.FindProperty(sourceName);
        SerializedProperty targetProperty = target.FindProperty(targetName);
        if (sourceProperty != null && targetProperty != null)
        {
            targetProperty.intValue = sourceProperty.intValue;
        }
    }

    private static void CopyBool(SerializedObject source, string sourceName, SerializedObject target, string targetName)
    {
        SerializedProperty sourceProperty = source.FindProperty(sourceName);
        SerializedProperty targetProperty = target.FindProperty(targetName);
        if (sourceProperty != null && targetProperty != null)
        {
            targetProperty.boolValue = sourceProperty.boolValue;
        }
    }

    private static void CopyObject(SerializedObject source, string sourceName, SerializedObject target, string targetName)
    {
        SerializedProperty sourceProperty = source.FindProperty(sourceName);
        SerializedProperty targetProperty = target.FindProperty(targetName);
        if (sourceProperty != null && targetProperty != null)
        {
            targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
        }
    }

    private static void CopyVector3(SerializedObject source, string sourceName, SerializedObject target, string targetName)
    {
        SerializedProperty sourceProperty = source.FindProperty(sourceName);
        SerializedProperty targetProperty = target.FindProperty(targetName);
        if (sourceProperty != null && targetProperty != null)
        {
            targetProperty.vector3Value = sourceProperty.vector3Value;
        }
    }

    private static void CopyLayerMask(SerializedObject source, string sourceName, SerializedObject target, string targetName)
    {
        SerializedProperty sourceProperty = source.FindProperty(sourceName);
        SerializedProperty targetProperty = target.FindProperty(targetName);
        if (sourceProperty != null && targetProperty != null)
        {
            targetProperty.intValue = sourceProperty.intValue;
        }
    }

    private static void SetString(SerializedObject target, string propertyName, string value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string MakeEnemyId(string configPath)
    {
        return System.IO.Path.GetFileNameWithoutExtension(configPath)
            .Replace("SO_Enemy_", string.Empty)
            .Replace("-", "_")
            .ToLowerInvariant();
    }

    private sealed class EnemyMigrationEntry
    {
        public readonly string PrefabPath;
        public readonly string PrefabGuid;
        public readonly string ConfigPath;
        public readonly Type BehaviorType;
        public readonly Type ConfigType;

        public EnemyMigrationEntry(string prefabGuid, string configPath, Type behaviorType, Type configType)
        {
            PrefabGuid = prefabGuid;
            PrefabPath = prefabGuid;
            ConfigPath = configPath;
            BehaviorType = behaviorType;
            ConfigType = configType;
        }
    }
}
