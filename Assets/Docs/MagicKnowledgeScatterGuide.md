# Magic Knowledge Scatter Guide

This guide describes how to generate and scatter magic books/relics in `Scene_lyl_IslandWhitebox`.

## One-click workflow

1. Open Unity editor.
2. Use menu: `Tools/Whitebox/Magic Knowledge/Generate Default Prefabs + Scatter`.

The tool will:

- Create default pickup prefabs in `Assets/Prefabs/Raid/MagicKnowledge`.
- Create materials in `Assets/Art/Materials/Raid/MagicKnowledge`.
- Create/load scatter profile: `Assets/Settings/MagicKnowledge/SO_RaidMagicKnowledgeScatterProfile.asset`.
- Open `Assets/Scenes/Scene_lyl_IslandWhitebox.unity`.
- Scatter pickups by `RaidRegionMarker` rules.
- Save the scene.

## Menu commands

- `Generate Default Prefabs`
- `Scatter To Scene_lyl_IslandWhitebox`
- `Generate Default Prefabs + Scatter`
- `Clear Generated Pickups In Scene_lyl_IslandWhitebox`
- `Select Scatter Profile Asset`

## Main config asset

Edit:

- `Assets/Settings/MagicKnowledge/SO_RaidMagicKnowledgeScatterProfile.asset`

Important fields:

- `TargetScenePath`: target scene path.
- `GeneratedRootName`: parent object name for generated pickups.
- `ClearPreviousGeneratedRoot`: whether to clear previous generated set before scatter.
- `UseFixedSeed` + `FixedSeed`: deterministic/random scatter control.
- `PlacementAttemptsPerItem`: retry count for each pickup.
- `GroundMask`, `GroundProbeHeight`, `SpawnHeightOffset`: grounding behavior.
- `RegionEdgePadding`, `GlobalMinSpacing`: spacing/edge constraints.
- `SpawnEntries`: each pickup spawn rule.

## Spawn entry fields

Each `SpawnEntry` controls one pickup type:

- `EntryId`: stable key for the default entry.
- `Label`: instance naming label.
- `Prefab`: pickup prefab reference.
- `Count`: number of instances to place.
- `MinSpacing`: minimum spacing for this entry.
- `AllowedRegionPurposes`: allowed `RaidRegionMarker` purposes.

## Supported region masks

- `Spawn`
- `Resource`
- `DenseResource`
- `Boss`
- `Extraction`
- `Transit`

## Pickup behavior setup

Each generated prefab includes:

- `MagicKnowledgePickup`
- Trigger collider for interaction
- Colored whitebox visual mesh

To change unlock behavior, edit the prefab's `MagicKnowledgePickup` fields:

- `PickupName`
- `UnlockType`
- `RunePatternPoints`
- `ConsumeOnUnlock`
- Highlight settings
