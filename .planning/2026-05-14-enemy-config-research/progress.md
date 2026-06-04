# Enemy Config Research Progress

## 2026-05-14

- Created isolated planning files for enemy configuration research. No gameplay code has been changed.
- Located realtime enemy scripts under `Assets/Scripts/Gameplay/Enemy` and enemy prefabs under `Assets/Prefabs/EnemyPrefab`.
- Read base melee/ranged behaviour, health/death loot, enemy projectile, special enemy controllers, boss projectile/vortex, rune weakpoints, and raid population profile.
- Confirmed realtime enemy balance is currently serialized directly on prefabs/components rather than centralized in SO assets.
- Confirmed `RaidMvpPopulationProfile` already handles enemy prefab pools and spawn density, but not per-enemy balance data.
- User clarified implementation intent: only realtime enemy controllers, SO runtime source of truth, existing fields only, current random patrol only, death loot inside enemy config, and no Excel/TSV importer.
- User approved typed config SOs, keeping prefab references on components, fail-fast missing config handling, clean inspector with config plus references only, and creating initial enemy config assets under a new folder.
- Implemented typed enemy config SO classes under `Assets/Scripts/Gameplay/Enemy/Config`.
- Refactored the seven confirmed realtime enemy controllers to read tunable values from typed config assets at runtime.
- Refactored `EnemyHealthController` so health and death loot are supplied by `EnemyHealthConfigBase`.
- Moved ranged bullet speed/damage/lifetime and Hunter Boss anchor projectile lifetime into enemy configs.
- Created `Assets/Scripts/Editor/EnemyConfigMigrationTool.cs` to generate/update initial config assets and bind prefabs.
- Generated seven initial config assets under `Assets/Config/Enemies` and wired matching enemy prefabs.
- Hid legacy death loot override fields on `RaidMvpPopulationProfile` and removed runtime population builder's death-loot override step because death loot now belongs to enemy configs.
- Ran Unity 2022.3.62f2c1 batchmode compile with log `Logs/EnemyConfigCompile.log`; no C# errors or exceptions found. Existing unrelated warnings remain.
