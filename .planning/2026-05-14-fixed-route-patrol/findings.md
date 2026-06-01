# Fixed Route Patrol Findings

## Existing System

- `EnemyConfigBase.cs` already contains `EnemyPatrolSettings` with patrol radius and wait time.
- The five regular enemy controllers each implement their own `GetNewPatrolPoint` random-radius logic.
- Current project does not expose a clear runtime enemy spawn point script. Existing enemy placement is mostly editor-driven through `RaidMvpScenePopulationBuilder`.
- The user clarified runtime spawning is desired, and this feature should introduce/standardize `EnemySpawnPoint`.

## Design Notes

- Route references must live on scene objects, not ScriptableObject enemy configs, because route waypoints are scene transforms.
- Spawn point entries should bind enemy prefab to route to support several routes under the same spawn point.
- Route follower should be added automatically when the spawn point injects a route, but prefabs may also include it.
- Fixed route fallback must preserve current random patrol behavior.
