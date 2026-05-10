using UnityEngine;

namespace Gameplay.SkillEffect
{
    /// <summary>
    /// Skill Effect 层级工具
    /// 统一处理子弹和技能表现物的 Layer 设置，避免技能物互相阻挡
    /// </summary>
    public static class SkillEffectLayerUtility
    {
        public const string SkillEffectLayerName = "Skill Effect";

        private const int MissingLayerCacheValue = -2;

        private static int _cachedLayer = MissingLayerCacheValue;

        public static int Layer => ResolveLayer();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureLayerCollisionOnLoad()
        {
            ConfigureLayerCollision();
        }

        /// <summary>
        /// 将目标物体和所有子物体设置为 Skill Effect 层
        /// </summary>
        /// <param name="rootObject"></param>
        public static void ApplyToRoot(GameObject rootObject)
        {
            if (rootObject == null || !TryGetLayer(out int skillEffectLayer))
                return;

            ConfigureLayerCollision();
            SetLayerRecursively(rootObject.transform, skillEffectLayer);
        }

        /// <summary>
        /// 判断物体是否处于 Skill Effect 层
        /// </summary>
        /// <param name="targetObject"></param>
        /// <returns></returns>
        public static bool IsSkillEffectObject(GameObject targetObject)
        {
            return targetObject != null &&
                   TryGetLayer(out int skillEffectLayer) &&
                   targetObject.layer == skillEffectLayer;
        }

        public static bool TryGetLayer(out int skillEffectLayer)
        {
            skillEffectLayer = ResolveLayer();
            return skillEffectLayer >= 0;
        }

        private static int ResolveLayer()
        {
            if (_cachedLayer != MissingLayerCacheValue)
                return _cachedLayer;

            _cachedLayer = LayerMask.NameToLayer(SkillEffectLayerName);
            return _cachedLayer;
        }

        private static void ConfigureLayerCollision()
        {
            if (!TryGetLayer(out int skillEffectLayer))
                return;

            Physics.IgnoreLayerCollision(skillEffectLayer, skillEffectLayer, true);
        }

        private static void SetLayerRecursively(Transform rootTransform, int layer)
        {
            if (rootTransform == null)
                return;

            rootTransform.gameObject.layer = layer;
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                SetLayerRecursively(rootTransform.GetChild(i), layer);
            }
        }
    }
}
