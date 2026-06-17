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

            float rangeScale = ResolveRangeScale(context);
            runner.PlayPrototypeIceFrostAssaultVisual(context.Position, context.Forward, radius * rangeScale, angleDegrees);
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

            float rangeScale = ResolveRangeScale(context);
            runner.PlayPrototypeIceWinterVisual(center, radius * rangeScale);
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

            float rangeScale = ResolveRangeScale(context);
            runner.PlayPrototypeEarthWallVisual(center, forward, length * rangeScale, width * rangeScale, height, durationSeconds);
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

            float rangeScale = ResolveRangeScale(context);
            runner.PlayPrototypeEarthQuakeVisual(center, radius * rangeScale, durationSeconds);
            return true;
        }

        private static float ResolveRangeScale(AgentCombatSkillContext context)
        {
            return Mathf.Max(0.01f, context.PrototypeSkillVfxRangeScale);
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
