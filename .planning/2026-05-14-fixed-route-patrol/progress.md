# Fixed Route Patrol Progress

## 2026-05-14

- Created isolated planning files for the fixed route patrol implementation.
- Confirmed previous enemy-config work already added typed ScriptableObject configs under `Assets/Scripts/Gameplay/Enemy/Config`.
- Confirmed no reusable runtime enemy spawn point script was found in `Assets/Scripts`; implementation will add `EnemySpawnPoint`.
- Added `EnemyPatrolMode` to `EnemyPatrolSettings`, defaulting to `RandomRadius`.
- Added `EnemyPatrolRoute`, `EnemyPatrolRouteFollower`, and `EnemySpawnPoint`.
- Added custom inspectors for patrol routes and spawn points.
- Updated `EnemyBehaviorController`, `RangedEnemyBehaviorController`, `ModernStranderBehaviorController`, `AncientStranderBehaviorController`, and `TidalAberrationBehaviorController` to use fixed routes when their config requests it.
- Verified no old `GetNewPatrolPoint` calls remain in the five target controllers.
- Ran Unity 2022.3.62f2c1 batchmode compile with log `Logs/FixedRoutePatrolCompile.log`; no C# errors found. Existing unrelated warnings remain in backpack and post-effect scripts.
