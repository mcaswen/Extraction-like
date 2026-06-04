# Enemy Config Research Findings

## File Inventory

- Main realtime enemy code lives under `Assets/Scripts/Gameplay/Enemy`.
- Enemy prefabs live under `Assets/Prefabs/EnemyPrefab`.
- BoardGame also contains AI enemy/Boss stat SOs, but that appears to be a separate board-game combat layer rather than the realtime raid enemy controllers.

## Current Config Shape

- Realtime enemy data is currently serialized directly on `MonoBehaviour` components through public fields.
- No dedicated realtime enemy `ScriptableObject` config type was found in the first pass.
- Base melee controller `EnemyBehaviorController` owns patrol radius/wait, detection/lose range, attack range/damage/interval.
- `RangedEnemyBehaviorController` owns patrol, detection, attack range/interval, plus `FirePoint` and `EnemyBulletPrefab`.
- `EnemyHealthController` owns `MaxHealth`, health UI reference, death loot toggle/prefab/spawn point/offset.
- `EnemyBulletController` owns projectile speed, damage, lifetime.
- Player bullet/status effect code is coupled to enemies through `EnemyHealthController`, `EnemyStatusEffectController`, and sentinel rune weakpoints.

## Special Enemy Controllers

- `ModernStranderBehaviorController` adds tentacle latch, pull, corrosion DOT, tentacle hitbox, and corrosive puddle data.
- `TidalAberrationBehaviorController` adds melee electric latch/silence and ranged water jet knockback data.
- `AncientStranderBehaviorController` adds melee sweep and ranged bite hitbox data.
- `AnchorSentinelBehaviorController` is a different puzzle/turret style controller with rune weakpoints, lock/fire beam timings, beam DPS/tick, and death loot.
- `HunterBossBehaviorController` is not NavMeshAgent-based. It uses hand movement, melee sweep, vortex field, anchor projectile, rage roar, cover checks, and cooldowns.
- These special behaviours are hard-coded per enemy type. The shared shape is patrol/detection/attack timing, but the actual skill execution is not a common reusable skill system today.

## Important Coupling

- Runtime scene/prefab references such as player transform, fire points, origins, line renderers, rune arrays, death loot spawn points, and cover masks are mixed into the same inspector surface as numeric balance fields.
- Some components create helper objects/components at runtime if missing, such as line renderers, hitboxes, status effects, vortex fields, and projectiles.
- Some controllers mutate their own serialized-ish values at startup for guardrails, such as minimum ranged distance and NavMeshAgent stopping distance.

## Spawning / Placement

- `RaidMvpPopulationProfile` is already a ScriptableObject used by editor tooling to populate MVP raid scenes.
- It controls scene bootstrap, density rules, region prefab pools, prefab weights, spacing, random yaw, position/rotation offsets, and default enemy death loot override.
- It references enemy prefabs directly. It does not own per-enemy health, movement, patrol, detection, or skill balance.
- `RaidMvpScenePopulationBuilder` assigns a default death loot prefab to spawned enemies if their `EnemyHealthController.DeathLootContainerPrefab` is missing.

## Existing SO Patterns

- BoardGame uses SOs with private serialized fields plus read-only properties and context-menu preset fill methods.
- Inventory item data is a designer-facing SO already used in gameplay.
- Realtime raid enemy configuration has no equivalent SO yet.
