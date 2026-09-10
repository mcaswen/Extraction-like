using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace AgentReproduction.World
{
    public static class TestNavMeshBuilder
    {
        public static void Build(TestWorldBuilder world, params Bounds[] floors)
        {
            var sources = new List<NavMeshBuildSource>();
            Bounds bounds = floors[0];
            foreach (Bounds floor in floors)
            {
                world.Cube("Test ground",floor.center,floor.size);
                bounds.Encapsulate(floor);
                sources.Add(new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box, size=floor.size, transform=Matrix4x4.TRS(floor.center,Quaternion.identity,Vector3.one), area=0 });
            }
            bounds.Expand(new Vector3(4,10,4));
            NavMeshData data = world.Own(NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),sources,bounds,Vector3.zero,Quaternion.identity));
            Assert.That(data,Is.Not.Null,"NavMesh fixture must build.");
            NavMesh.AddNavMeshData(data);
            Physics.SyncTransforms();
        }
        public static void Flat(TestWorldBuilder world, float size=100) => Build(world,new Bounds(new Vector3(0,-0.1f,0),new Vector3(size,0.2f,size)));
    }
}
