using System;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Temporary bridge for testing the code-generated player elemental prototype VFX
    /// on the current agent skill pipeline.
    /// </summary>
    internal static class AgentPrototypeSkillVfxBridge
    {
        private const string IceFrostAssaultSkillId = "ice_frost_assault";
        private const string IceWinterfallSkillId = "ice_winterfall";
        private const string EarthStoneWallSkillId = "earth_stone_wall";
        private const string EarthQuakeFieldSkillId = "earth_quake_field";

        public static bool TryPlayFrostAssault(
            AgentCombatSkillContext context,
            AgentCombatSkillConfigBase config,
            float radius,
            float angleDegrees)
        {
            if (!ShouldPlay(context, config, IceFrostAssaultSkillId, out PlayerElementalSkillVfx runner))
                return false;

            runner.PlayPrototypeIceFrostAssaultVisual(context.Position, context.Forward, radius, angleDegrees);
            return true;
        }

        public static bool TryPlayWinterfall(
            AgentCombatSkillContext context,
            AgentCombatSkillConfigBase config,
            Vector3 center,
            float radius)
        {
            if (!ShouldPlay(context, config, IceWinterfallSkillId, out PlayerElementalSkillVfx runner))
                return false;

            runner.PlayPrototypeIceWinterVisual(center, radius);
            return true;
        }

        public static bool TryPlayStoneWall(
            AgentCombatSkillContext context,
            AgentCombatSkillConfigBase config,
            Vector3 center,
            Vector3 forward,
            float length,
            float width,
            float height,
            float durationSeconds)
        {
            if (!ShouldPlay(context, config, EarthStoneWallSkillId, out PlayerElementalSkillVfx runner))
                return false;

            runner.PlayPrototypeEarthWallVisual(center, forward, length, width, height, durationSeconds);
            return true;
        }

        public static bool TryPlayQuakeField(
            AgentCombatSkillContext context,
            AgentCombatSkillConfigBase config,
            Vector3 center,
            float radius,
            float durationSeconds)
        {
            if (!ShouldPlay(context, config, EarthQuakeFieldSkillId, out PlayerElementalSkillVfx runner))
                return false;

            runner.PlayPrototypeEarthQuakeVisual(center, radius, durationSeconds);
            return true;
        }

        private static bool ShouldPlay(
            AgentCombatSkillContext context,
            AgentCombatSkillConfigBase config,
            string expectedSkillId,
            out PlayerElementalSkillVfx runner)
        {
            runner = null;
            if (!context.PlayPrototypeSkillVfx ||
                context.CasterTransform == null ||
                config == null ||
                !string.Equals(config.SkillId, expectedSkillId, StringComparison.Ordinal))
            {
                return false;
            }

            runner = context.CasterTransform.GetComponent<PlayerElementalSkillVfx>();
            if (runner == null)
            {
                runner = context.CasterTransform.gameObject.AddComponent<PlayerElementalSkillVfx>();
            }

            runner.SetKeyboardPreviewEnabled(false);
            return true;
        }
    }
}
