#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Linq;
using Gameplay.Agent.Core;
using UnityEngine;
using UnityEngine.AI;

namespace AnomalySearch.Automation.SceneRaid
{
    /// <summary>仅在失败事件执行的只读邻域查询，不改变路径或避障参数。</summary>
    public static class SceneRaidNavigationEvidence
    {
        [Serializable] public sealed class Body
        {
            public string identity, type, layer;
            public Vector3 center, size, closestToPawn, closestToDestination;
            public bool trigger, closestUsesBounds;
        }
        [Serializable] public sealed class NavigationAgent
        {
            public string identity, avoidance;
            public Vector3 position, nextPosition, velocity, destination;
            public float radius, height, baseOffset;
            public int priority, agentTypeId, areaMask;
            public bool ready, hasPath, stopped;
        }
        [Serializable] public sealed class Obstacle
        {
            public string identity, shape;
            public Vector3 position, scale, center, size;
            public float radius, height;
            public bool carving, stationaryOnly;
        }
        [Serializable] public sealed class Record
        {
            public Vector3 pawnPosition, destination;
            public float queryRadius = 10;
            public bool colliderBufferFull, pathCalculated;
            public string pathStatus;
            public Vector3[] pathCorners;
            public Body[] bodies;
            public NavigationAgent[] agents;
            public Obstacle[] obstacles;
        }
        public static Record Capture(AgentPawnRoot pawn, Vector3 destination, SceneRaidIdentityMap identity)
        {
            var record = new Record { pawnPosition = pawn.Position, destination = destination };
            var nearby = new Collider[256];
            int count = Physics.OverlapSphereNonAlloc(pawn.Position, record.queryRadius, nearby, ~0, QueryTriggerInteraction.Collide);
            record.colliderBufferFull = count == nearby.Length;
            record.bodies = nearby.Take(count).Where(x => x != null).Select(x => new Body
            {
                identity = identity.Get(x), type = x.GetType().Name, layer = LayerMask.LayerToName(x.gameObject.layer),
                center = x.bounds.center, size = x.bounds.size, trigger = x.isTrigger,
                closestUsesBounds = !SupportsClosestPoint(x),
                closestToPawn = ClosestPoint(x, pawn.Position), closestToDestination = ClosestPoint(x, destination)
            }).ToArray();
            bool Near(Vector3 p) => new Vector2(p.x - pawn.Position.x, p.z - pawn.Position.z).sqrMagnitude <= 100;
            record.agents = UnityEngine.Object.FindObjectsOfType<NavMeshAgent>().Where(x => Near(x.transform.position)).Select(x =>
            {
                bool ready = x.isActiveAndEnabled && x.isOnNavMesh;
                return new NavigationAgent { identity = identity.Get(x), position = x.transform.position,
                    radius = x.radius, height = x.height, baseOffset = x.baseOffset, priority = x.avoidancePriority,
                    agentTypeId = x.agentTypeID, areaMask = x.areaMask,
                    avoidance = x.obstacleAvoidanceType.ToString(), ready = ready, hasPath = ready && x.hasPath,
                    nextPosition = ready ? x.nextPosition : Vector3.zero, velocity = ready ? x.velocity : Vector3.zero,
                    stopped = ready && x.isStopped, destination = ready && x.hasPath ? x.destination : Vector3.zero };
            }).ToArray();
            record.obstacles = UnityEngine.Object.FindObjectsOfType<NavMeshObstacle>().Where(x => Near(x.transform.position)).Select(x =>
                new Obstacle { identity = identity.Get(x), shape = x.shape.ToString(), position = x.transform.position,
                    scale = x.transform.lossyScale, center = x.center, size = x.size, radius = x.radius, height = x.height,
                    carving = x.carving, stationaryOnly = x.carveOnlyStationary }).ToArray();
            var nav = pawn.NavMeshAgent;
            if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh)
            {
                var path = new NavMeshPath();
                record.pathCalculated = nav.CalculatePath(destination, path);
                record.pathStatus = path.status.ToString(); record.pathCorners = path.corners;
            }
            return record;
        }
        private static bool SupportsClosestPoint(Collider collider) => collider is BoxCollider ||
            collider is SphereCollider || collider is CapsuleCollider || collider is MeshCollider mesh && mesh.convex;
        private static Vector3 ClosestPoint(Collider collider, Vector3 position) => SupportsClosestPoint(collider)
            ? collider.ClosestPoint(position) : collider.bounds.ClosestPoint(position);
    }
}
#endif
