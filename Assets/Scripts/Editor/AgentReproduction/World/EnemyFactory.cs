using UnityEngine;

namespace AgentReproduction.World
{
    public static class EnemyFactory
    {
        public static EnemyHealthController Passive(TestWorldBuilder world, Vector3 position)
        {
            GameObject root=world.Root("Passive damage source",false);
            root.transform.position=position;
            CapsuleCollider collider=root.AddComponent<CapsuleCollider>();
            collider.center=Vector3.up; collider.height=2; collider.radius=0.4f;
            EnemyHealthController health=root.AddComponent<EnemyHealthController>();
            health.MaxHealth=10000;
            root.SetActive(true);
            return health;
        }
    }
}
