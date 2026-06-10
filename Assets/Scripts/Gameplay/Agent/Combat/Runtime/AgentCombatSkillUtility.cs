using System.Collections.Generic;
using Gameplay.SkillEffect;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    public static class AgentCombatSkillUtility
    {
        private static readonly Collider[] HitBuffer = new Collider[96];

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
            statusEffect.ApplyTo(enemyHealth);
        }

        public static Vector3 ResolveTargetPosition(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            if (target.HasPosition)
                return target.Position;

            return context.Position + context.Forward * Mathf.Max(1f, context.StyleConfig != null
                ? context.StyleConfig.NormalAttackRange
                : 1f);
        }

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
