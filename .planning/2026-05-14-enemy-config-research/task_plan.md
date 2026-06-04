# Enemy Config Research Plan

## Goal

Read the current enemy system, especially existing enemy configuration code, then challenge and clarify requirements before proposing a designer-friendly ScriptableObject configuration implementation plan.

## Scope

In scope:
- Enemy runtime behaviour, attributes, skills, patrol, perception/AI, spawning or prefab binding if connected.
- Existing config/data classes and serialized fields.
- Designer workflow and validation needs for ScriptableObject-based enemy data.
- Questions that must be answered before implementation.

Out of scope for this phase:
- Editing gameplay code.
- Creating new ScriptableObject assets or importers.
- Changing prefabs or scenes.

## Phases

| Phase | Status | Output |
| --- | --- | --- |
| 1. Locate enemy-related files | Done | File inventory and likely ownership boundaries |
| 2. Read current config/runtime code | Done | Field/system map |
| 3. Identify coupling and missing config seams | Done | Risks and constraints |
| 4. Challenge requirements | Done | Clarifying questions |
| 5. Draft implementation plan | Done | Detailed plan after user answers |

## Open Questions

- Confirmed: scope is only realtime enemy controllers `EnemyBehaviorController`, `RangedEnemyBehaviorController`, `ModernStrander`, `TidalAberration`, `AncientStrander`, `AnchorSentinel`, and `HunterBoss`.
- Confirmed: SO should be the runtime source of truth. Controllers read from SO at runtime.
- Confirmed: designers only tune existing fields/skills. No free skill composition in this phase.
- Confirmed: patrol keeps current random-radius settings for now. Route patrol is future work.
- Confirmed: death loot belongs to enemy config.
- Confirmed: designer UX only needs clear SO Inspector grouping, tooltips, and validation. No table importer for now.

## Remaining Clarifications

- Decide config asset shape: one `EnemyConfig` with all optional blocks, or separate typed SO classes per enemy archetype.
- Decide migration flow: create initial config assets from current prefab values, then wire prefabs to configs.
- Decide fallback policy when a config reference is missing.
- Decide whether prefab inspector should retain old public fields as read-only/migrated, or hide values behind config references after refactor.
- Decide whether runtime-spawned helper prefab references such as bullets, puddles, vortex field, line renderers, origins, rune arrays, and cover masks are all considered part of enemy config or remain prefab/scene references.

## Final Decisions

- Use `EnemyConfigBase` plus one typed config SO per supported archetype so designers only see relevant fields.
- Keep scene/child-object references on prefabs: player transform, origins, line renderers, rune arrays, health UI, and death loot spawn point.
- Put tunable values and shared resource prefabs in SOs: health, patrol, detection, attacks, special skill values, projectile/puddle/vortex prefabs, cover mask, and death loot prefab/toggle/offset.
- Missing config should log an error and disable the affected behaviour instead of silently using old defaults.
- Final component inspector should show a `Config` reference plus necessary prefab/scene references, not duplicate authoritative tuning fields.
- Create initial assets under a new enemy config folder with no specific naming preference from the user.
