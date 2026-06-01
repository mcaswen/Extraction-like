# Fixed Route Patrol Plan

## Goal

Add a designer-configurable patrol mode that lets runtime-spawned enemies either keep the existing random-radius patrol or follow routes assigned by an enemy spawn point.

## Scope

- Extend enemy patrol config with a mode dropdown.
- Add runtime scene components for spawn points, route sets, and route following.
- Integrate fixed-route patrol into the five NavMesh-based regular enemy controllers.
- Add editor tooling and gizmos for route/spawn configuration.
- Preserve existing random patrol as the default and fallback.

## Phases

| Phase | Status | Output |
| --- | --- | --- |
| 1. Restore context and inspect current code | Done | Existing enemy Config SO system and lack of runtime enemy spawner confirmed |
| 2. Add shared runtime patrol/spawn scripts | Done | Patrol mode enum, route, follower, spawn point |
| 3. Patch regular enemy controllers | Done | Five controllers use route follower when config requests fixed routes |
| 4. Add editor tools | Done | Inspector buttons for routes/spawn points |
| 5. Verify | Done | Static search and Unity batchmode compile completed |

## Decisions

- Enemy Config chooses `RandomRadius` or `FixedRoute`; default remains random.
- Scene spawn point entries choose which route each spawned enemy receives.
- The same spawn point can hold multiple routes and multiple enemy entries.
- Invalid route points are skipped.
- Routes with fewer than two valid sampled points are invalid; enemies warn and fall back to random patrol.
- After losing the player, fixed-route enemies return to the nearest valid route point.
- Boss and Anchor Sentinel are out of scope for route patrol.

## Target Enemy Controllers

- `EnemyBehaviorController`
- `RangedEnemyBehaviorController`
- `ModernStranderBehaviorController`
- `AncientStranderBehaviorController`
- `TidalAberrationBehaviorController`
