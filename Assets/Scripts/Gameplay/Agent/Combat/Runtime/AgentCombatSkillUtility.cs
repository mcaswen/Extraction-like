using System.Collections.Generic;
using Gameplay.SkillEffect;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 战斗技能运行时工具
    /// 提供目标收集、范围判断、伤害结算和临时视觉生成
    /// </summary>
    public static class AgentCombatSkillUtility
    {
        private static readonly Collider[] HitBuffer = new Collider[96];

        /// <summary>
        /// 收集球形范围内仍存活的敌人
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="layerMask"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        public static int CollectEnemiesInSphere(
            Vector3 center,
            float radius,
            LayerMask layerMask,
            HashSet<global::EnemyHealthController> results)
        {
            if (results == null)
                return 0;

            results.Clear();
            float safeRadius = Mathf.Max(0f, radius);
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                safeRadius,
                HitBuffer,
                layerMask,
                QueryTriggerInteraction.Ignore);

            // 命中 Collider 后向父级查找敌人生命组件，避免表现子物体漏判
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = HitBuffer[i];
                if (hit == null)
                    continue;

                global::EnemyHealthController enemyHealth = hit.GetComponentInParent<global::EnemyHealthController>();
                if (enemyHealth != null && enemyHealth.IsAlive)
                    results.Add(enemyHealth);
            }

            return results.Count;
        }

        /// <summary>
        /// 判断目标点是否位于水平扇形范围内
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="forward"></param>
        /// <param name="point"></param>
        /// <param name="radius"></param>
        /// <param name="angleDegrees"></param>
        /// <returns></returns>
        public static bool IsInsidePlanarCone(
            Vector3 origin,
            Vector3 forward,
            Vector3 point,
            float radius,
            float angleDegrees)
        {
            Vector3 direction = point - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return true;

            if (direction.sqrMagnitude > radius * radius)
                return false;

            return Vector3.Angle(forward, direction.normalized) <= angleDegrees * 0.5f;
        }

        /// <summary>
        /// 对敌人造成技能伤害并附加状态效果
        /// </summary>
        /// <param name="enemyHealth"></param>
        /// <param name="damage"></param>
        /// <param name="context"></param>
        /// <param name="hitPosition"></param>
        /// <param name="sourceType"></param>
        /// <param name="statusEffect"></param>
        public static void ApplyDamageAndStatus(
            global::EnemyHealthController enemyHealth,
            float damage,
            AgentCombatSkillContext context,
            Vector3 hitPosition,
            global::EnemyDamageSourceType sourceType,
            AgentCombatStatusEffectDefinition statusEffect)
        {
            if (enemyHealth == null || damage <= 0f)
                return;

            Vector3 sourcePosition = context.Position;
            global::EnemyDamageContext damageContext = global::EnemyDamageContext.FromAttacker(
                context.CasterTransform,
                hitPosition,
                sourcePosition,
                hitPosition - sourcePosition,
                sourceType);

            enemyHealth.TakeDamage(damage, damageContext);
            statusEffect.ApplyTo(enemyHealth, context.SkillModifiers.SlowDurationBonusSeconds);
        }

        /// <summary>
        /// 解析技能目标位置，缺少显式目标时落在施法者前方
        /// </summary>
        /// <param name="context"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static Vector3 ResolveTargetPosition(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            if (target.HasPosition)
                return target.Position;

            return context.Position + context.Forward * Mathf.Max(1f, context.StyleConfig != null
                ? context.StyleConfig.NormalAttackRange
                : 1f);
        }

        /// <summary>
        /// 生成一个圆形范围指示器
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        /// <param name="durationSeconds"></param>
        public static void SpawnCircleIndicator(
            Vector3 center,
            float radius,
            Color color,
            float durationSeconds)
        {
            GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicatorObject.name = "AgentCombatCircleIndicator";
            indicatorObject.transform.position = center + Vector3.up * 0.03f;
            indicatorObject.transform.localScale = new Vector3(
                Mathf.Max(0.1f, radius * 2f),
                0.02f,
                Mathf.Max(0.1f, radius * 2f));
            SkillEffectLayerUtility.ApplyToRoot(indicatorObject);

            Collider collider = indicatorObject.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            ApplyColor(indicatorObject, color);
            Object.Destroy(indicatorObject, Mathf.Max(0.05f, durationSeconds));
        }

        /// <summary>
        /// 生成技能视觉预制体并按需自动销毁
        /// </summary>
        /// <param name="visualPrefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="durationSeconds"></param>
        /// <returns></returns>
        public static GameObject SpawnVisualPrefab(
            GameObject visualPrefab,
            Vector3 position,
            Quaternion rotation,
            float durationSeconds)
        {
            if (visualPrefab == null)
                return null;

            GameObject visualObject = Object.Instantiate(visualPrefab, position, rotation);
            if (durationSeconds > 0f)
                Object.Destroy(visualObject, durationSeconds);

            return visualObject;
        }

        /// <summary>
        /// 生成一个扇形范围指示器
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="forward"></param>
        /// <param name="radius"></param>
        /// <param name="angleDegrees"></param>
        /// <param name="color"></param>
        /// <param name="durationSeconds"></param>
        public static void SpawnConeIndicator(
            Vector3 origin,
            Vector3 forward,
            float radius,
            float angleDegrees,
            Color color,
            float durationSeconds)
        {
            GameObject indicatorObject = new GameObject("AgentCombatConeIndicator");
            indicatorObject.transform.position = origin + Vector3.up * 0.03f;
            indicatorObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            SkillEffectLayerUtility.ApplyToRoot(indicatorObject);

            MeshFilter meshFilter = indicatorObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = indicatorObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = BuildConeMesh(radius, angleDegrees, 24);
            meshRenderer.sharedMaterial = CreateRuntimeMaterial(color);
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            Object.Destroy(meshFilter.sharedMesh, Mathf.Max(0.05f, durationSeconds) + 0.1f);
            Object.Destroy(indicatorObject, Mathf.Max(0.05f, durationSeconds));
        }

        private static Mesh BuildConeMesh(float radius, float angleDegrees, int segments)
        {
            float safeRadius = Mathf.Max(0.1f, radius);
            int safeSegments = Mathf.Clamp(segments, 4, 64);
            Vector3[] vertices = new Vector3[safeSegments + 2];
            int[] triangles = new int[safeSegments * 3];

            // 顶点 0 是扇形圆心，后续顶点沿角度弧线展开
            vertices[0] = Vector3.zero;
            float halfAngle = Mathf.Clamp(angleDegrees, 1f, 360f) * 0.5f;
            for (int i = 0; i <= safeSegments; i++)
            {
                float t = i / (float)safeSegments;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Sin(angle) * safeRadius,
                    0f,
                    Mathf.Cos(angle) * safeRadius);
            }

            // 每个分段生成一个以圆心为顶点的三角形
            for (int i = 0; i < safeSegments; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }

            Mesh mesh = new Mesh();
            mesh.name = "AgentCombatConeIndicatorMesh";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>
        /// 将颜色应用到目标对象及其子级 Renderer
        /// </summary>
        /// <param name="targetObject"></param>
        /// <param name="color"></param>
        public static void ApplyColor(GameObject targetObject, Color color)
        {
            if (targetObject == null)
                return;

            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rendererComponent = renderers[i];
                if (rendererComponent != null)
                    rendererComponent.material = CreateRuntimeMaterial(color);
            }
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.color = color;
            return material;
        }
    }
}
