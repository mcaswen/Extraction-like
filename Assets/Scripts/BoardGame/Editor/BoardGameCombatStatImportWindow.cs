using BoardGame.Config;
using BoardGame.Runtime;
using UnityEditor;
using UnityEngine;

namespace BoardGame.Editor
{
    /// <summary>
    /// 战斗数值导入窗口
    /// 把策划提供的 AI 敌人和 Boss 数值写回 RuleSet
    /// </summary>
    public sealed class BoardGameCombatStatImportWindow : EditorWindow
    {
        private const float LabelColumnWidth = 72f;
        private const float ValueColumnWidth = 72f;

        private SO_BoardGame_RuleSet _ruleSet;

        [MenuItem("Tools/BoardGame/Combat Stat Import Tool")]
        private static void OpenWindow()
        {
            GetWindow<BoardGameCombatStatImportWindow>("Combat Stat Import");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Combat Stat Import", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Write the planner-provided AI enemy and boss combat values into a BoardGame RuleSet asset\n" +
                "This tool only updates HP Attack Defense and combat templates\n" +
                "Movement Search Extract Loot and Progression settings stay unchanged",
                MessageType.Info);

            _ruleSet = (SO_BoardGame_RuleSet)EditorGUILayout.ObjectField(
                "Rule Set Asset",
                _ruleSet,
                typeof(SO_BoardGame_RuleSet),
                false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Rule Set"))
                {
                    _ruleSet = Selection.activeObject as SO_BoardGame_RuleSet;
                    GUI.FocusControl(null);
                }

                using (new EditorGUI.DisabledScope(_ruleSet == null))
                {
                    if (GUILayout.Button("Apply Planner Combat Preset"))
                    {
                        ImportCombatStats();
                    }
                }
            }

            EditorGUILayout.Space();
            DrawPreviewTable();
        }

        /// <summary>
        /// 把当前预设写入 RuleSet 资产
        /// </summary>
        private void ImportCombatStats()
        {
            if (_ruleSet == null)
            {
                EditorUtility.DisplayDialog("Import Failed", "Please assign a rule set asset first", "OK");
                return;
            }

            Undo.RecordObject(_ruleSet, "Import BoardGame Combat Stats");
            _ruleSet.ApplyPlannerCombatPreset();
            EditorUtility.SetDirty(_ruleSet);
            AssetDatabase.SaveAssets();
            Selection.activeObject = _ruleSet;

            EditorUtility.DisplayDialog(
                "Import Complete",
                "Planner combat values were written to the rule set asset",
                "OK");
        }

        /// <summary>
        /// 显示即将导入的策划战斗数值预览
        /// </summary>
        private static void DrawPreviewTable()
        {
            BoardAgentStatDefinition agentStats = BoardGamePlannerPresetConfig.CreateAgentStats();
            BoardCombatRuleDefinition combatRules = BoardGamePlannerPresetConfig.CreateCombatRules();

            EditorGUILayout.LabelField("Planner Combat Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Type", EditorStyles.miniBoldLabel, GUILayout.Width(LabelColumnWidth));
                GUILayout.Label("HP", EditorStyles.miniBoldLabel, GUILayout.Width(ValueColumnWidth));
                GUILayout.Label("ATK", EditorStyles.miniBoldLabel, GUILayout.Width(ValueColumnWidth));
                GUILayout.Label("DEF", EditorStyles.miniBoldLabel, GUILayout.Width(ValueColumnWidth));
            }

            DrawStatRow("AI", agentStats.MaxHealth, agentStats.Attack, agentStats.Defense);

            DrawEnemyRow(combatRules, BoardDangerTier.Low, "LOW");
            DrawEnemyRow(combatRules, BoardDangerTier.Medium, "MEDIUM");
            DrawEnemyRow(combatRules, BoardDangerTier.High, "HIGH");

            BoardBossStatDefinition bossDefinition = combatRules.BossDefinition;
            DrawStatRow("BOSS", bossDefinition.MaxHealth, bossDefinition.Attack, bossDefinition.Defense);
        }

        /// <summary>
        /// 显示一行敌人数值
        /// </summary>
        private static void DrawEnemyRow(
            BoardCombatRuleDefinition combatRules,
            BoardDangerTier dangerTier,
            string label)
        {
            BoardEnemyStatDefinition definition = FindEnemyDefinition(combatRules, dangerTier);

            if (definition == null)
            {
                DrawStatRow(label, 0, 0, 0);
                return;
            }

            DrawStatRow(label, definition.MaxHealth, definition.Attack, definition.Defense);
        }

        /// <summary>
        /// 从战斗规则里查找指定危险等级的敌人模板
        /// </summary>
        private static BoardEnemyStatDefinition FindEnemyDefinition(
            BoardCombatRuleDefinition combatRules,
            BoardDangerTier dangerTier)
        {
            if (combatRules == null || combatRules.EnemyDefinitions == null)
            {
                return null;
            }

            foreach (BoardEnemyStatDefinition definition in combatRules.EnemyDefinitions)
            {
                if (definition != null && definition.DangerTier == dangerTier)
                {
                    return definition;
                }
            }

            return null;
        }

        /// <summary>
        /// 绘制预览表的一行
        /// </summary>
        private static void DrawStatRow(string label, int maxHealth, int attack, int defense)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(LabelColumnWidth));
                GUILayout.Label(maxHealth.ToString(), GUILayout.Width(ValueColumnWidth));
                GUILayout.Label(attack.ToString(), GUILayout.Width(ValueColumnWidth));
                GUILayout.Label(defense.ToString(), GUILayout.Width(ValueColumnWidth));
            }
        }
    }
}
