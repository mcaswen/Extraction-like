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
| 25. Backpack UI asset integration review | Done | Inspected current UI sprite assets, backpack prefab hooks, runtime scripts, draggable item prefab, and scene binding differences | Findings recorded; implementation/configuration plan prepared for the user |
| 26. Enemy chase and boss indicator icons | Done | Added IMG_0583 chase indicator support and IMG_0606 persistent Boss indicator support | Runtime/editor dotnet builds pass; Unity binder logged 6 chase prefabs, 1 boss prefab, and 2 skipped prefabs; static prefab scan confirms the bindings |
| 27. Player HUD icon refresh | Done | Added IMG_0582 avatar, IMG_0584 health icon, and IMG_0586/IMG_0587 status bar support to the runtime player HUD | Runtime/editor dotnet builds pass; Unity SpriteSet builder generated `Assets/Resources/HUD/PlayerStatusHudSpriteSet.asset` with the four sprite references |
| 28. Enemy ClipUpdate art refresh | Done | Replace enemy model/animation references with the new `ClipUpdate` Zombie/Robot resources and assess script-generated VFX bindings | Static scans, Unity prefab/controller update, and runtime/editor build verification |
| 29. Tidal Aberration Water Jet test setup | Done | Diagnosed likely Water Jet knockback causes without changing gameplay logic; temporary `Scene_lyl_test` test area was later reverted at user request | Current scene contains no `EnemyFunctionTestArea`, test marker, or test Tidal Aberration instance |
| 30. Enemy function test area expansion | Done | Temporarily enlarged the enemy test area and configured the player; this scene setup was later reverted at user request | `Scene_lyl_test.unity` restored to the version without the dedicated test field |
| 31. Tidal Aberration Water Jet existence check | Done | Confirm whether Tidal Aberration has a Water Jet knockback skill and why it is not visible in scene testing | Skill exists in code/config/prefab; current scene/player setup can prevent visible knockback |
| 32. Tidal Aberration Water Jet visible test tuning | Done | Made Water Jet easier to trigger, easier to see, and more reliable at moving the player during tests | `dotnet build`, `git diff --check`, and Unity batch refresh compile passed with no C# errors |
| 33. Tidal Aberration runtime no-knockback diagnosis | Done | Check why Player is not knocked back after pressing Play in the current scene | Current Player placement is outside both Tidal detection and Water Jet attack range |
| 34. Tidal Aberration scene player placement | Done | Move the current `Scene_lyl_test` Player into Water Jet range of the existing Tidal Aberration | Player placed about 8m from Tidal Aberration without recreating the dedicated test area |
| 35. Tidal Aberration opening Water Jet bugfix | Done | Make Water Jet fire immediately when the scene starts with Player already in ranged window and make its hit scan choose the nearest valid current Player receiver | `dotnet build` passes; Unity batch refresh reports `LogAssemblyErrors (0ms)` |
| 36. Tidal Aberration no visible knockback audit | Done | Audit why Water Jet damage occurs but visible knockback still does not happen, and document the current implementation logic | Editor log proves Water Jet hits for 14 damage; current Player/Agent prefab and scene instance do not include `PlayerMovementController`, so impulse is skipped |
| 37. Agent external movement receiver migration | Done | Move external knockback/slow receiving capability from old manual player movement assumptions into the automatic Agent pawn path | `dotnet build` passes; Unity batch refresh reports `Tundra build success` and `LogAssemblyErrors (0ms)` |
| 38. Modern Strander no-damage audit | Done | Investigated why Modern Strander appears not to damage Player and documented the current implementation flow | Current `Scene_lyl_test` ModernStrander prefab instance is overridden inactive; no ModernStrander skill-damage logs were present; the code path would damage `PlayerHealthController` when the enemy is active and latched |
| 39. Modern Strander active-scene re-audit | Done | Re-checked the current active ModernStrander scene state, damage flow, player setup, and recent logs | ModernStrander is now active, but the player starts about 8.36m away and behind its 78-degree vision cone while the player can shoot from 10m, so the enemy likely dies before reaching its 3.2m tentacle-latch range |
| 40. Modern Strander direct-hit counter fix | Done | Added direct-player-hit counter tentacle range and migrated latch pull to the shared external movement receiver | Runtime/editor dotnet builds pass with 0 warnings/errors; Unity batch compile log reports `Tundra build success` |
| 41. Modern Strander pull tuning | Done | Reduced ModernStrander latch pull strength from launch-like movement to a light visible tug | Runtime dotnet build passes with 0 warnings/errors |
| 42. Modern Strander body push fix | Done | Prevent ModernStrander's walking body collision from pushing the Player during enemy attack tests | Body collision now ignores the Player body collider and chase stops at a stand-off position; runtime dotnet build passes with 0 warnings/errors |
| 43. Ancient Strander attack cooldown fix | Done | Fix AncientStrander melee attacks ignoring cooldown after repeated direct-damage state resets | Melee cooldown now uses absolute next-ready time and runtime dotnet build passes with 0 warnings/errors |
| 44. Anchor Sentinel health and damage fix | Done | Make Anchor Sentinel health tuning affect runtime survivability and make its beam reliably damage the current Player/Agent target | Rune hits now damage the sentinel body, rune completion no longer bypasses HP while alive, range detection activates the beam, and runtime dotnet build passes with 0 warnings/errors |
| 45. Anchor Sentinel standard enemy conversion | Done | Remove rune puzzle logic and prefab rune cubes so Anchor Sentinel is damaged directly and attacks from player detection | Rune scripts/config/prefab data removed; direct body damage and detection-driven beam attack validated by runtime/editor builds |
| 46. Player defense damage mitigation fix | Done | Make player defense from Agent pawn settings reduce incoming enemy damage | Agent defense now feeds player damage mitigation; runtime/editor builds pass with 0 warnings/errors |
| 47. Enemy VFX integration audit | Done | Identify artist-provided enemy VFX assets/scripts that still need enemy-system hookup | Compared VFX assets, formal enemy prefabs, controller call sites, and GUID references; integration list prepared |
| 48. Enemy VFX integration | Done | Hook Modern Strander, Tidal Aberration, Ancient Strander, and Anchor Sentinel to artist VFX without duplicating gameplay damage | Added visual-only VFX trigger APIs, called them from enemy controllers, and verified Unity script compilation |
| 49. Modern Strander VFX correction | Done | Fix Modern Strander tentacle VFX visibility/regression after initial enemy VFX integration | `dotnet build`, `git diff --check`, and Unity batchmode compile passed |
| 50. Modern Strander attack loop fix | Done | Stop repeated raise/lower attack animation loops while Modern Strander is in melee range | `dotnet build`, `git diff --check`, and Unity batchmode compile passed |
| 51. Modern Strander combat-distance correction | Done | Stop Chase/Attack state flapping caused by root-position distance mismatch and restore tentacle VFX/damage trigger | `dotnet build`, `git diff --check`, and Unity batchmode compile passed |
| 52. Enemy scale normalization and Ancient Strander alignment | Done | Normalize formal enemy model scale against the current Agent player size and realign Ancient Strander animation/VFX | Unity prefab YAML check, scale report, and `dotnet build` pass |
| 53. Storage warehouse UI | Done | Independent `StorageCanvas.prefab`, storage test scene, paged 6x10 warehouse UI, current-page storage sort, and persistent player storage data flow | Runtime/editor `dotnet build` pass; prefab has unique GUID, no nested prefab instances, no missing scripts, refreshed backpack grid, in-bounds single-column page selector, and test scene references only `StorageCanvas.prefab` |
| 54. Totem shop UI | Done | Independent `ShopCanvas.prefab`, `ShopCanvasTest.unity`, persistent per-agent gold, 30-minute real-time totem stock refresh, warehouse sell flow, and totem purchase flow | Runtime/editor `dotnet build` pass; prefab has independent GUID, left storage grid, right 4x4 totem shop grid, gold display, refresh countdown, no missing-script entries, and test scene references only `ShopCanvas.prefab` |
| 55. Extraction storage settlement | Done | Successful extracted agents append backpack loot and equipped gear to the shared warehouse; failed agents discard extractable inventory | Runtime/editor `dotnet build` pass; static scans confirm settlement, discard, and storage append call sites |
| 56. Out-of-raid totem expansion | Done | Six named totems x three qualities as active equipment totems, old generic totems retired from active lists, TSV/XLSX/importer/shop/database/runtime modifier support updated | Workbook inspection, TSV/database/shop/prefab static checks, `git diff --check`, and runtime/editor `dotnet build` pass; Unity batchmode unavailable on this machine |
| 57. Element selection menu art hookup | Done | Rebuilt `Scene_ElementSelectionMenu` Canvas with separated background, panel, repeated button layers, element icons, Title font labels, and IMG_0826/Text start button | Static scene GUID checks pass, editor `dotnet build` passes, and a local preview image was generated; Unity batchmode scene rebuild was blocked by an already-open project instance |
| 58. Shop scene spacing, back button, and storage-test background | Done | Moved shop/storage panels toward center, added IMG_0494 return button to `Scene_PreparationInterface`, and synced `StorageCanvasTest` background with preparation interface | Runtime/editor `dotnet build` pass; targeted `git diff --check` pass; YAML duplicate fileID scan pass |

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
| `CarryWeight` | No | Optional non-negative carry weight. Defaults to `1` when absent. |
| `ItemBackgroundSpritePath` | No | Optional Sprite path used as the item UI background. |
| `IncludeInRuntimeDatabase` | No | `Yes`/`No`; `No` keeps an asset on disk but removes it from runtime lookup output. |
| `IncludeInTotemShop` | No | `Yes`/`No`; `No` prevents a totem from being generated into the shop pool. |
| `TotemQuality` | No | `None`, `Green`, `Blue`, or `Gold`. |
| `TotemModifiers` | No | Semicolon-separated percent modifiers, for example `MaxHealth=0.1;MoveSpeed=-0.05`. |
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

## Phase 6 - Backpack system DOCX deliverable
- [x] Draft the detailed Chinese Word document covering the backpack system.
- [x] Export the document to `backpack_system_detailed_zh.docx`.
- [ ] Render and visually inspect the DOCX.
- [ ] Deliver the final document path to the user.

