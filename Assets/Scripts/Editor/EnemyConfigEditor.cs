using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyConfigBase), true)]
public sealed class EnemyConfigEditor : Editor
{
    private static readonly HashSet<string> CommonDisplayedProperties = new HashSet<string>
    {
        "m_Script",
        "_enemyId",
        "_displayName",
        "_maxHealth",
        "_deathLoot",
        "_patrol",
        "_detection"
    };

    private static bool _showAdvanced;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawBasicAndLootSection();
        DrawPatrolSection();
        DrawDetectionSection();
        DrawEnemySpecificSection();
        DrawAdvancedSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBasicAndLootSection()
    {
        DrawSectionHeader("Basic And Loot");
        DrawProperty("_displayName", "Display Name");

        SerializedProperty maxHealth = serializedObject.FindProperty("_maxHealth");
        if (maxHealth != null)
        {
            EditorGUILayout.PropertyField(maxHealth, new GUIContent("Max Health"));
        }

        SerializedProperty deathLoot = serializedObject.FindProperty("_deathLoot");
        if (deathLoot != null)
        {
            DrawRelativeProperty(deathLoot, "_spawnLootContainerOnDeath", "Spawn Loot On Death");
            DrawRelativeProperty(deathLoot, "_deathLootContainerPrefab", "Death Loot Container");
        }
    }

    private void DrawPatrolSection()
    {
        SerializedProperty patrol = serializedObject.FindProperty("_patrol");
        if (patrol == null)
        {
            return;
        }

        DrawSectionHeader("Patrol");
        DrawRelativeProperty(patrol, "_patrolMode", "Patrol Mode");
        DrawRelativeProperty(patrol, "_patrolRadius", "Patrol Radius");
        DrawRelativeProperty(patrol, "_patrolWaitTime", "Wait Time");
    }

    private void DrawDetectionSection()
    {
        SerializedProperty detection = serializedObject.FindProperty("_detection");
        if (detection == null)
        {
            return;
        }

        DrawSectionHeader("Detection");
        DrawRelativeProperty(detection, "_awarenessPreset", "Awareness Preset");
        DrawRelativeProperty(detection, "_detectionRange", "Detection Range");
        DrawRelativeProperty(detection, "_viewAngle", "View Angle");
        DrawRelativeProperty(detection, "_loseRange", "Lose Range");
    }

    private void DrawEnemySpecificSection()
    {
        switch (target)
        {
            case MeleeEnemyConfig:
                DrawMeleeEnemySection();
                break;
            case RangedEnemyConfig:
                DrawRangedEnemySection();
                break;
            case ModernStranderConfig:
                DrawModernStranderSection();
                break;
            case AncientStranderConfig:
                DrawAncientStranderSection();
                break;
            case TidalAberrationConfig:
                DrawTidalAberrationSection();
                break;
            case AnchorSentinelConfig:
                DrawAnchorSentinelSection();
                break;
            case HunterBossConfig:
                DrawHunterBossSection();
                break;
        }
    }

    private void DrawMeleeEnemySection()
    {
        DrawSectionHeader("Attack");
        DrawProperty("_attackRange", "Attack Range");
        DrawProperty("_attackDamage", "Attack Damage");
        DrawProperty("_attackInterval", "Attack Interval");
    }

    private void DrawRangedEnemySection()
    {
        DrawSectionHeader("Attack");
        DrawProperty("_attackRange", "Attack Range");
        DrawProperty("_bulletDamage", "Bullet Damage");
        DrawProperty("_attackInterval", "Attack Interval");
        DrawProperty("_bulletMoveSpeed", "Bullet Speed");
        DrawProperty("_bulletLifeTime", "Bullet Lifetime");
    }

    private void DrawModernStranderSection()
    {
        DrawSectionHeader("Tentacle Skill");
        DrawProperty("_attackRange", "Tentacle Range");
        DrawProperty("_directDamageCounterAttackRange", "Direct Hit Counter Range");
        DrawProperty("_attackInterval", "Tentacle Cooldown");
        DrawProperty("_tentacleLatchDuration", "Latch Duration");
        DrawProperty("_corrosionDamagePerSecond", "Corrosion DPS");
        DrawProperty("_corrosionDuration", "Corrosion Duration");
    }

    private void DrawAncientStranderSection()
    {
        DrawSectionHeader("Melee Sweep");
        DrawProperty("_meleeAttackRange", "Melee Range");
        DrawProperty("_meleeDamage", "Melee Damage");
        DrawProperty("_meleeAttackInterval", "Melee Cooldown");

        DrawSectionHeader("Fishbone Bite");
        DrawProperty("_minimumRangedDistance", "Bite Min Distance");
        DrawProperty("_rangedAttackRange", "Bite Max Distance");
        DrawProperty("_biteDamage", "Bite Damage");
        DrawProperty("_rangedAttackInterval", "Bite Cooldown");
    }

    private void DrawTidalAberrationSection()
    {
        DrawSectionHeader("Electric Melee");
        DrawProperty("_meleeAttackRange", "Melee Range");
        DrawProperty("_meleeContactDamage", "Melee Damage");
        DrawProperty("_meleeAttackInterval", "Melee Cooldown");
        DrawProperty("_silenceDuration", "Silence Duration");

        DrawSectionHeader("Water Jet");
        DrawProperty("_rangedAttackRange", "Water Jet Range");
        DrawProperty("_waterJetDamage", "Water Jet Damage");
        DrawProperty("_waterJetKnockbackStrength", "Knockback Strength");
        DrawProperty("_rangedAttackInterval", "Water Jet Cooldown");
    }

    private void DrawAnchorSentinelSection()
    {
        DrawSectionHeader("Beam Attack");
        DrawProperty("_detectionRange", "Detection Range");
        DrawProperty("_lockDuration", "Lock Duration");
        DrawProperty("_firingDuration", "Firing Duration");
        DrawProperty("_cooldownDuration", "Cooldown Duration");
        DrawProperty("_beamDamagePerSecond", "Beam DPS");
    }

    private void DrawHunterBossSection()
    {
        DrawSectionHeader("Movement");
        DrawProperty("_detectionRange", "Detection Range");
        DrawProperty("_loseRange", "Lose Range");
        DrawProperty("_chaseSpeed", "Chase Speed");

        DrawSectionHeader("Melee Sweep");
        DrawProperty("_meleeAttackRange", "Melee Range");
        DrawProperty("_meleeDamage", "Melee Damage");
        DrawProperty("_meleeAttackInterval", "Melee Cooldown");

        DrawSectionHeader("Vortex And Throw");
        DrawProperty("_vortexTriggerDistance", "Vortex Trigger Distance");
        DrawProperty("_vortexTriggerMeleeCount", "Vortex Trigger Melee Count");
        DrawProperty("_vortexRadius", "Vortex Radius");
        DrawProperty("_anchorThrowDamage", "Anchor Throw Damage");

        DrawSectionHeader("Roar And Rage");
        DrawProperty("_rageThreshold", "Rage Threshold");
        DrawProperty("_roarRange", "Roar Range");
        DrawProperty("_roarDamage", "Roar Damage");
        DrawProperty("_rageShieldMaxHealthRatio", "Rage Shield Ratio");
    }

    private void DrawAdvancedSection()
    {
        EditorGUILayout.Space(8f);
        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced / Technical", true);
        if (!_showAdvanced)
        {
            return;
        }

        EditorGUI.indentLevel++;
        DrawProperty("_enemyId", "Enemy Id");

        SerializedProperty deathLoot = serializedObject.FindProperty("_deathLoot");
        if (deathLoot != null)
        {
            DrawRelativeProperty(deathLoot, "_deathLootSpawnOffset", "Death Loot Spawn Offset");
        }

        SerializedProperty detection = serializedObject.FindProperty("_detection");
        if (detection != null)
        {
            DrawRelativeProperty(detection, "_lineOfSightBlockMask", "Line Of Sight Block Mask");
            DrawRelativeProperty(detection, "_groundMask", "Ground Mask");
            DrawRelativeProperty(detection, "_eyeHeight", "Eye Height");
            DrawRelativeProperty(detection, "_targetHeight", "Target Height");
        }

        DrawUndisplayedProperties();
        EditorGUI.indentLevel--;
    }

    private void DrawUndisplayedProperties()
    {
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (CommonDisplayedProperties.Contains(iterator.name) || IsSpecificDisplayedProperty(iterator.name))
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }
    }

    private bool IsSpecificDisplayedProperty(string propertyName)
    {
        switch (target)
        {
            case MeleeEnemyConfig:
                return propertyName == "_attackRange" ||
                       propertyName == "_attackDamage" ||
                       propertyName == "_attackInterval";
            case RangedEnemyConfig:
                return propertyName == "_attackRange" ||
                       propertyName == "_bulletDamage" ||
                       propertyName == "_attackInterval" ||
                       propertyName == "_bulletMoveSpeed" ||
                       propertyName == "_bulletLifeTime";
            case ModernStranderConfig:
                return propertyName == "_attackRange" ||
                       propertyName == "_directDamageCounterAttackRange" ||
                       propertyName == "_attackInterval" ||
                       propertyName == "_tentacleLatchDuration" ||
                       propertyName == "_corrosionDamagePerSecond" ||
                       propertyName == "_corrosionDuration";
            case AncientStranderConfig:
                return propertyName == "_meleeAttackRange" ||
                       propertyName == "_meleeDamage" ||
                       propertyName == "_meleeAttackInterval" ||
                       propertyName == "_minimumRangedDistance" ||
                       propertyName == "_rangedAttackRange" ||
                       propertyName == "_biteDamage" ||
                       propertyName == "_rangedAttackInterval";
            case TidalAberrationConfig:
                return propertyName == "_meleeAttackRange" ||
                       propertyName == "_meleeContactDamage" ||
                       propertyName == "_meleeAttackInterval" ||
                       propertyName == "_silenceDuration" ||
                       propertyName == "_rangedAttackRange" ||
                       propertyName == "_waterJetDamage" ||
                       propertyName == "_waterJetKnockbackStrength" ||
                       propertyName == "_rangedAttackInterval";
            case AnchorSentinelConfig:
                return propertyName == "_detectionRange" ||
                       propertyName == "_lockDuration" ||
                       propertyName == "_firingDuration" ||
                       propertyName == "_cooldownDuration" ||
                       propertyName == "_beamDamagePerSecond";
            case HunterBossConfig:
                return propertyName == "_detectionRange" ||
                       propertyName == "_loseRange" ||
                       propertyName == "_chaseSpeed" ||
                       propertyName == "_meleeAttackRange" ||
                       propertyName == "_meleeDamage" ||
                       propertyName == "_meleeAttackInterval" ||
                       propertyName == "_vortexTriggerDistance" ||
                       propertyName == "_vortexTriggerMeleeCount" ||
                       propertyName == "_vortexRadius" ||
                       propertyName == "_anchorThrowDamage" ||
                       propertyName == "_rageThreshold" ||
                       propertyName == "_roarRange" ||
                       propertyName == "_roarDamage" ||
                       propertyName == "_rageShieldMaxHealthRatio";
            default:
                return false;
        }
    }

    private void DrawSectionHeader(string label)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    private void DrawProperty(string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private static void DrawRelativeProperty(SerializedProperty parent, string propertyName, string label)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label));
    }
}
