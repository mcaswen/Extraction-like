# Loot Item TSV Importer Plan

## Goal

Build a Unity Editor workflow that lets designers maintain loot item data in an Excel-friendly table, export it as TSV, and import it into the project as:

- `InventoryItemData` assets under `Assets/SO/ItemData`
- matching simple whitebox `World_*` pickup prefabs under `Assets/Prefabs/ItemPrefabIn3D`

The importer must not generate or manage loot box contents. Loot box configuration remains a manual design task that references the generated item data assets.

## Scope

In scope:

- Create an editable Excel template with validation lists, rarity colors, and field guidance.
- Provide a TSV source file shape that Unity can import safely.
- Add a Unity Editor menu/window for validating and importing TSV data.
- Create or update `InventoryItemData` by stable `ItemID`.
- Create missing world pickup prefabs as whitebox placeholders.
- Preserve artist-authored prefab visuals, materials, children, scale, and manually configured non-default icons.
- Auto-repair required runtime references/components when safe.
- Report orphan item assets whose `ItemID` no longer appears in the TSV.

Out of scope:

- Generating or editing `LootBoxEntity` loot tables.
- Generating production art models.
- Runtime `.xlsx` parsing.
- Automatic deletion of removed items.

## Phases

| Phase | Status | Output | Validation |
| --- | --- | --- | --- |
| 1. Planning files | Done | `task_plan.md`, `findings.md`, `progress.md` | Files exist and capture agreed scope |
| 2. Excel/TSV template | Done | `Assets/Config/Loot/LootItems_Template.xlsx`, `Assets/Config/Loot/LootItems.tsv` | Template opens as an editable workbook; TSV has valid sample rows |
| 3. Unity editor window | Done | `Assets/Scripts/Editor/LootItemTsvImporterWindow.cs` | Menu appears at `Tools/Backpack/Import Loot Items From TSV` |
| 4. TSV parser and validation | Done | Full-table validation before writes | Invalid data stops import before asset mutation |
| 5. Item data import | Done | Create/update `InventoryItemData` assets by `ItemID` | Existing assets are matched by ID, not filename |
| 6. World prefab generation/repair | Done | Missing `World_*` prefabs created; existing prefabs repaired conservatively | Artist visual edits are preserved |
| 7. Static verification | Done | Code grep, workbook inspection, Unity batchmode compile | No C# compiler errors found |
| 8. Loot sell price and ItemID update | Done | Added `SellPrice` to loot table data flow and ensured existing loot rows/assets have stable `ItemID` values | Workbook/TSV/importer validated against the new 18-column shape |
| 9. Enemy intelligent patrol vision | Done | Independent patrol vision pivot, moving/wait scanning, suspicion stimuli, investigation/search behavior | Unity batchmode compile passes with no C# compiler errors in `Logs/EnemyAwarenessCompile3.log` |
| 10. Enemy attack design alignment | Done | Check and align Modern Strander, Tidal Aberration, Anchor Sentinel, Ancient Strander, and Hunter Boss attack features with the user's design notes | `dotnet build` and Unity batchmode compile pass with no C# errors in `Logs/EnemyAttackDesignCompile.log`; designer-tunable fields listed in final response |
| 11. Enemy/loot SO folder migration | Done | Move enemy config SO assets and loot item data SO assets under `Assets/SO`, preserving previous category names and updating hardcoded editor/tool paths | `rg` old-path scan is clean; `dotnet build` passes; Unity batchmode exits 0 with `LogAssemblyErrors (0ms)` in `Logs/SOFolderMigrationCompile.log` |
| 20. Equipment slot implementation restart | Done | Fixed 5x6 backpack, six typed equipment slots, equipment data/assets, TSV/XLSX support, and prefab bindings | Static scans, workbook verify, dotnet builds, Unity compile/prefab verification, and user Play Mode acceptance |
| 21. Enemy direct-hit combat response | Done | Player direct damage carries attacker context; hit patrol enemy locks player and enters chase without alerting nearby enemies | Static scans, `dotnet build Assembly-CSharp.csproj`, and `dotnet build Assembly-CSharp-Editor.csproj` pass |
| 22. Enemy direct-hit long-range chase fix | Done | Directly hit patrol enemies get a short forced chase window and sampled NavMesh chase destination before normal lose-range leash resumes | Static scans, `dotnet build Assembly-CSharp.csproj`, and `dotnet build Assembly-CSharp-Editor.csproj` pass |
| 23. Enemy direct attacker retaliation | Done | Direct Agent/player damage carries an attacker; patrol enemies chase and attack the direct attacker instead of stopping in patrol awareness | Static scans, `dotnet build Assembly-CSharp.csproj`, and `dotnet build Assembly-CSharp-Editor.csproj` pass |
| 24. Enemy script Chinese comments | Done | Added Chinese XML comments for public enemy APIs and concise normal comments for complex private enemy logic, following `Assets/Scripts/Core` style | Public/protected method comment scan, `git diff --check`, and `dotnet build Assembly-CSharp.csproj` pass |

## TSV Columns

| Column | Required | Notes |
| --- | --- | --- |
| `ItemID` | Yes | Stable unique key. Use lowercase English letters, numbers, and underscores. |
| `ItemName` | Yes | Display name. Chinese is allowed. |
| `Type` | Yes | `Bag`, `Rig`, `Equipment`, or `Other`. |
| `Rarity` | Yes | `Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`. |
| `Width` | Yes | Inventory width, 1-10. |
| `Height` | Yes | Inventory height, 1-10. |
| `IsStackable` | Yes | `Yes`/`No` or `鏄痐/`鍚. |
| `MaxStack` | Yes | Must be 1 when not stackable; at least 1 when stackable. |
| `ContainerColumns` | Conditional | Required for `Bag` and `Rig`; blank for `Other`. |
| `ContainerRows` | Conditional | Required for `Bag` and `Rig`; blank for `Other`. |
| `BlockedCells` | No | Container-only cells formatted as `x,y;x,y`. |
| `RequiresSearchInLootContainer` | Yes | `Yes`/`No` or `鏄痐/`鍚. |
| `SearchDurationOverride` | Yes | `-1` means use rarity/size default. Otherwise non-negative seconds. |
| `MagicUnlock` | Yes | Existing `MagicUnlockType` enum value. |
| `RunePatternPoints` | Yes | At least 1. |
| `Enabled` | Yes | Disabled rows are skipped. |
| `SellPrice` | Yes | Non-negative integer sale value for future sell-item systems. |
| `Notes` | No | Designer-only notes, not imported. |

## Import Rules

- Validate the whole TSV before mutating any Unity asset.
- Stop the import on duplicate IDs, invalid enum values, invalid dimensions, invalid stack rules, or invalid container rules.
- Match existing `InventoryItemData` assets by `ItemID`.
- Update data fields from TSV because TSV is the source of truth.
- Preserve existing non-default icons.
- Preserve existing artist-authored world prefabs.
- Create a world prefab only when the item has none or the referenced prefab path is missing.
- Existing world prefabs may be repaired by adding `WorldLootItem`, a collider, and the correct `WorldLootItem.ItemData` reference.
- Do not delete assets that are absent from the TSV; report them as orphaned.

## Artist Protection

Existing `World_*` prefabs must not have their mesh, material, children, root transform scale, or collider shape overwritten unless the required runtime component is missing. The importer may add missing minimum runtime pieces but should avoid visual rebuilds for existing prefabs.

## Enemy Intelligent Patrol Vision Scope

In scope:

- Add an independent `VisionPivot` transform for enemies that implement `IEnemyVisionSource`.
- Drive patrol vision with path-aware forward bias plus timed random scanning.
- Let patrol wait points scan more deliberately instead of holding one linear direction.
- Add a lightweight suspicious stimulus event system for sounds, impacts, and other interest points.
- Add suspicious, investigate, and search behavior to the main patrol enemies while preserving existing chase and attack behavior.
- Keep the implementation reusable across existing enemy controllers.

Out of scope for this phase:

- Full behavior tree rewrite.
- Production animation blending for head/torso bones.
- Designer-authored audio propagation through portals or rooms.
- Large prefab reauthoring beyond script-side runtime component setup.

