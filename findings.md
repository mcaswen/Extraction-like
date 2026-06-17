# Loot Item TSV Importer Findings

## Existing Project Facts

- `InventoryItemData` is the static item configuration type in `Assets/Scripts/Gameplay/Backpack/InventoryItemData.cs`.
- Ground pickups use `WorldLootItem` in `Assets/Scripts/Gameplay/Backpack/WorldLootItem.cs`.
- Loot containers use `LootBoxEntity` in `Assets/Scripts/Gameplay/Backpack/LootBoxEntity.cs`.
- `LootBoxEntity.LootTable` already references `InventoryItemData` entries, so the TSV importer does not need to generate box contents.
- Dragging an item out of the inventory uses `InventoryItemData.WorldPrefab` to instantiate a world pickup.
- Enemy death loot uses `EnemyHealthController.DeathLootContainerPrefab`, which is a loot container prefab, not an individual item prefab.
- Existing item data assets now live under `Assets/SO/ItemData`.
- Existing world item prefabs live under `Assets/Prefabs/ItemPrefabIn3D`.
- The project does not currently include a dedicated Excel parsing package in `Packages/manifest.json`.

## Decisions

- Designers will edit an `.xlsx` template and export/save the import source as UTF-8 TSV.
- Unity imports TSV, not `.xlsx`, to avoid adding spreadsheet runtime/editor dependencies to the Unity project.
- `ItemID` is the stable primary key. Filenames may change, but `ItemID` must not.
- The first implementation imports item definitions only. It does not create or edit loot boxes.
- Whitebox world prefabs are acceptable as placeholders.
- Artist-authored world prefab visuals take priority over generated whitebox visuals.
- Icons are artist-owned. The importer creates and assigns a default white icon only when no icon exists.
- Removed TSV rows are reported as orphan assets rather than deleted.

## Field Mapping

| TSV Type | Unity ItemType |
| --- | --- |
| `Bag` | `ItemType.Bag` |
| `Rig` | `ItemType.Rig` |
| `Other` | `ItemType.Junk` |

Rarity maps directly to the existing `ItemRarity` enum.

Magic unlock maps directly to the existing `MagicUnlockType` enum.

## Risks

- Existing comments in some Chinese scripts are mojibake, so new files should use clean ASCII identifiers/comments where possible.
- Creating `.xlsx` files from local tooling needs verification because Unity itself will only consume TSV.
- Unity compile verification may require launching Unity, which is outside the current shell-only workflow.
- If artists rename assets and also clear `ItemID`, the importer cannot safely match them.

## 2026-05-15 Enemy Attack Design Alignment Findings

- Five requested enemies already have dedicated configs/controllers: `ModernStranderConfig`, `TidalAberrationConfig`, `AnchorSentinelConfig`, `AncientStranderConfig`, and `HunterBossConfig`.
- Modern Strander already implements tentacle hitbox, initial damage, pull, corrosion DOT, and corrosive slime puddle spawning after a successful latch. Conflict found: current config asset and script default attack interval are `2s`, but the requested version says `5s`.
- Tidal Aberration already implements tentacle contact damage, player silence, high-pressure water jet damage, and knockback impulse. Conflicts/gaps found: ranged water jet interval is `2.6s`, requested `8s`; water jet knockback does not yet apply the requested 1s 50% move-speed slow debuff.
- Anchor Sentinel already has activation, lock/firing/cooldown beam states, beam tracking visual, range, damage-per-second, and tick interval. Conflict found: beam timing is `1.1s` lock + `0.55s` firing + `1.5s` cooldown, but requested version is every `5s` firing a beam that lasts `3s` and tracks player while active.
- Ancient Strander already implements melee fishbone sweep area damage and ranged fishbone bite hitbox damage. No major missing requested attack feature found.
- Hunter Boss already has melee anchor sweep, vortex field, anchor throw, low-health roar, cover check, and elemental freeze/fire support in the generic status/bullet pipeline. Conflicts/gaps found: vortex triggers by distance rather than every 3 melee attacks; vortex arms too early and immobilizes only `0.4s` instead of arming after `2s` and immobilizing `3s`; low-health roar lacks 20% max-health shield, 3s charge, defensive force field, 100% defense boost, freeze-specific force-field slowdown/blue visual, fire break/bonus damage, and player tremble debuff.

## 2026-05-10 SellPrice / ItemID Follow-up

- Existing planning files show the Excel template, TSV source, and Unity TSV importer are already in place.
- Current loot source files are `Assets/Config/Loot/LootItems_Template.xlsx` and `Assets/Config/Loot/LootItems.tsv`.
- Current TSV columns end at `Notes`; there is no sell price column yet.
- The importer currently requires `ItemID` and updates `InventoryItemData` by stable `ItemID`.
- The visible TSV sample rows already have IDs: `ammo_9mm`, `medical_kit`, and `small_bag`.
- User clarified the loot list to generate IDs for: `书`, `笔记`, `有机纤维`, `军用对讲机`, `uphone手机`, `次级有机纤维`, `u盘`, `情报文件`, `高级有机纤维`, `怀表`, `纯金戒指`, `顶级有机纤维`, `显示卡`, `萨卢佐醇酿`, `巫师帽`.
- User explicitly asked to delete the previously written sample data and restore the original planned three item types only: `Bag`, `Rig`, and `Other`.
- The loot template now uses only the clarified 15 loot rows, all defaulted to `Other`, with generated English snake_case `ItemID` values.
- Removed the autonomous earlier changes to existing real item assets and BoardGame runtime mapping; this task should only update the loot table/import workflow requested by the user.

## 2026-05-16 Enemy/Loot SO Folder Migration Findings

- Enemy config ScriptableObject assets currently live in `Assets/Config/Enemies` and are named `SO_Enemy_*.asset`.
- Loot item data ScriptableObject assets currently live in `Assets/Prefabs/ItemData` and are `InventoryItemData` assets.
- `Assets/SO` already exists and contains BoardGame SO assets under `Assets/SO/BoardGame/Config`, so the migration should use `Assets/SO/Enemies` and `Assets/SO/ItemData` to preserve the previous category names without mixing domains.
- Hardcoded old enemy config paths are concentrated in `Assets/Scripts/Editor/EnemyConfigMigrationTool.cs`.
- Hardcoded old loot item data paths are concentrated in `Assets/Scripts/Editor/LootItemTsvImporterWindow.cs`; world pickup prefab paths under `Assets/Prefabs/ItemPrefabIn3D` are GameObject prefabs, not SO assets, so they should stay in Prefabs unless the user asks for prefab migration too.
- Loot TSV/workbook files under `Assets/Config/Loot` are data source files rather than ScriptableObject assets; they should stay there unless the user specifically wants all loot config source files moved too.

## 2026-05-29 Fixed Backpack UI Redesign Findings

- The user clarified that the current backpack UI should not include any rig/chest-rig equipment slot, rig storage grid, or reserved rig area. Existing rig assets may remain in the project, but this backpack system must not expose the rig as an active feature.
- The backpack is no longer equipped as a container item. Player storage is now a fixed grid of 5 columns by 6 rows.
- The visible equipment area should contain 6 functional equipment slots: head, body, face, headset, totem 1, and totem 2. These slots should accept only matching equipment item types and preserve equipped item views at runtime, but they do not expand inventory storage.
- Existing loot item shapes should remain multi-cell where configured; the fixed backpack stores differently shaped loot items.
- `EquipmentSlotUI` can be reused for the new equipment slots because it already validates `ItemType` and works without a linked internal grid when `LinkedGrid` is null.
- To avoid Unity enum value drift for existing assets, `ItemType.Rig` and `ItemType.Bag` should remain in the enum even though the current backpack UI no longer uses them as equip-expand containers.

## 2026-05-29 Backpack UI Redesign Findings

- The current backpack implementation treats rig and backpack as equipped container slots: `RigSlot` links to `TacticalRigGrid`, and `BackpackSlot` links to `BackpackGrid`.
- `InventoryScreenController` routes quick transfer, direct pickup, world-item storage, and world-container equipment through both rig and backpack linked grids.
- The user's clarified target removes chest rigs from the final backpack system UI and removes equipped backpacks as capacity providers.
- The new player inventory should be a fixed 5-column by 6-row `BackpackGrid`.
- The equipment area should contain six non-container equipment slots: head, body, face, headset, totem 1, and totem 2.
- Existing rig and bag assets may remain in the project, but the delivered UI/interaction path should not expose rig storage or backpack-equipping storage expansion.
- Updating `Assets/Prefabs/Canvas.prefab` is preferred over editing scene files because prefab instances can pick up the shared UI update with lower scene merge-conflict risk.

## 2026-05-30 Backpack UI Scale-Up Findings

- The prefab rebuild tool already exposed larger slot constants, but `CreateInventoryGrid` still used hardcoded `50f` cell metrics. That prevented the generated prefab from actually using the requested larger grid.
- `InventoryScreenController.ApplyFixedLayoutAnchorsIfNeeded` still used the old `330x700` left panel fallback. Existing scene instances could therefore be corrected back to the small layout even after prefab edits.
- The enlarged player backpack now uses UGUI `Image` cells with the rounded sprite and sliced rendering. This is compatible with swapping the cell background sprite later, as long as the replacement art has a usable nine-slice border.

## 2026-05-30 BoardGame Loot Inventory Slot Sync Findings

- The normal backpack prefab already uses `Assets/Art/Sprites/UI/InventoryRoundedCell.png` for `BackpackGrid`, `LootChestGrid`, and runtime draggable item backgrounds.
- The F-key BoardGame loot/search overlay is built at runtime by `BoardGameLootInventoryController`, so it did not inherit `Canvas.prefab` slot art automatically.
- `BoardGamePrototypeInstaller` can dynamically create `BoardGameLootInventoryController`; therefore the slot sprite needs to be passed through the installer rather than relying only on a scene-authored loot inventory controller reference.
- Dragging revealed BoardGame loot into the player backpack already uses `DraggableItemUI` and `InventoryUIController`, so the functional drag/drop path did not need to change.
- `BoardInventoryState` stores only `BoardItemInstance` values. Persisting exact backpack placement requires storing grid coordinates and rotation on the item instance or another inventory placement record.

## 2026-05-30 Loot Drag / Preview / Search Regression Findings

- The standard loot chest grid now uses 72px cells while the fixed player backpack uses 96px cells. `DraggableItemUI.UpdateVisualSize` sizes dragged items from `CurrentGrid`, but `CurrentGrid` still points at the source grid until drop completes. That makes cross-grid dragging show the source-grid-sized item while preview/highlight/placement validation are calculated in the target-grid cell metrics.
- `DraggableItemUI.UpdatePreviewRotationIfNeeded` mutates `_currentPreviewIsRotated` during hover and resizes the dragged item immediately. Near edges this can flip back and forth, making the predicted footprint visibly grow/shrink and desyncing from the cursor.
- `GetPredictedGridIndex` predicts from the pointer center. This is reasonable only if the drag visual is also centered and already sized for the same target grid. With mixed source/target cell sizes it produces large offsets and false red previews.
- The prefab `GlobalDragLayer` is created as a child Canvas without explicitly setting `renderMode`; nested canvases can default to a world-space canvas mode in serialized prefab data. Drag projection through `ScreenPointToWorldPointInRectangle` against that layer can therefore put the dragged item far from the pointer.
- Normal loot-box search progress is automatic on `DraggableItemUI.Update`, while BoardGame loot reveal is manually driven by `BoardGameLootInventoryController`. A frozen first frame can be caused either by the view being inactive, by manual ticking not finding the current reveal item, or by the search overlay not repainting every frame.

## 2026-05-30 Loot Drag / Preview / Search Regression Fix Continued Findings

- Drag prediction now captures the pointer's normalized offset inside the source item and maps that offset onto the target grid's cell metrics. This makes the dragged item visual, predicted highlighter, and final placement validation use the same target-grid footprint.
- The old preview auto-rotation path was removed from drag hover because it mutated `_currentPreviewIsRotated` while hovering near grid edges, which could make the preview footprint grow/shrink and desync from the pointer.
- `ScreenPointToWorldPointInRectangle` is no longer used for the backpack drag visual. Dragged items are positioned by converting the screen point into the normalized drag-layer RectTransform local coordinate space, avoiding large world-space offsets from stale or nested canvas state.
- Same-grid swap now preserves the dragged item's pre-drag original placement before temporarily placing it, so failed swap rollback cannot poison `_originalGridIndex` or `_originalIsRotated` for later previews/restores.
- BoardGame manual reveal now refreshes `DraggableItemUI` search visuals every frame and writes partial reveal progress back to the active node item state, so animation-only search effects do not freeze on the first frame and HUD/node progress can reflect partial search.

## 2026-05-30 Loot Box UI Visibility/Layout Findings

- The screenshot path is the ordinary scene loot-box UI (`InventoryScreenController` + `LootBoxEntity`), not the BoardGame F-key overlay. The right panel is `LootChestPanel`/`LootChestGrid` under `Assets/Prefabs/Canvas.prefab`.
- The loot box prefabs (`LootBox_1` through `LootBox_4`) do have `LootTable` entries, so an empty-looking grid is not explained by missing prefab loot configuration alone.
- `InventoryItemFactory` instantiates the hidden `RuntimeDraggableItem` prefab from `Canvas.prefab`; Unity preserves inactive state on clones, so spawned loot item UI objects could be present but invisible unless explicitly reactivated.
- The right loot panel used a fixed 560x650 size while some containers are 7x7 or 9x9. With 72px cells and 6px spacing, larger containers can exceed that panel and appear misaligned/cropped.
- Replacing the slot art is done through the sprite referenced by `InventoryUIController.CellBackgroundSprite` and `InventoryItemFactory.ItemBackgroundSprite`; currently the shared art is `Assets/Art/Sprites/UI/InventoryRoundedCell.png`.
- For a replacement PNG, import it as `Sprite (2D and UI)`, use single-sprite mode, enable alpha transparency, and set a sensible Sprite Border if it should be sliced without distorted corners.

## 2026-05-30 Backpack Regression Re-audit Findings

- User explicitly asked to stop coding and first challenge/diagnose the backpack regressions before proposing an implementation.
- Current working tree has large unstaged changes in backpack UI/drag/search files and `Canvas.prefab`; do not revert them blindly.
- Git history shows the backpack system existed before the `Backpack` rename under `Assets/Scripts/Gameplay/Bag`. Important historical baseline commits:
  - `9fa9cff` (`lyl提交最终版背包系统`) introduced the fuller bag system with equipment slots, replacement, grid model, stacking, and world loot.
  - `9f6296a` (`修复高亮预测落点错误并整理BoardGame背包交互`) kept same-grid swap, stack merge, quick transfer, replacement logic, and the center-based highlighter prediction fix.
  - `7b86091` renamed `Bag` to `Backpack`; current `HEAD` still contains the same key drag methods split into `DraggableItemUI.Drag.cs`.
- Historical successful drag flow in `9f6296a`:
  - `OnBeginDrag` removes the item from its source grid but leaves `CurrentGrid` pointing to the source grid.
  - `OnEndDrag` resolves target slot/grid, then tries empty placement, stack merge, then same-grid swap.
  - Same-grid swap works because `TrySwapWithinSameGrid` compares `targetGrid` against `CurrentGrid`, which still points to the source grid during the drag.
  - Stack merge only triggers when the target footprint overlaps exactly one same-`InventoryItemData` stackable item.
  - Highlighter prediction is center-based via `GetPredictedGridIndex`, and automatic hover rotation mutates preview rotation/size near boundaries.
- Current working copy modified the drag flow:
  - It stores `_dragSourceGrid` on begin drag and uses that for same-grid swap, which is the right idea because later visual changes may decouple `CurrentGrid`.
  - It caches preview placement before `RestoreDragVisualState`, which is also necessary because restore clears preview fields.
  - It resizes dragged visuals to the hovered target grid metrics during hover and uses pointer-offset-based prediction. This is intended to fix mixed 72px loot grid vs 96px backpack grid dragging.
  - However, the current code still keeps `UpdatePreviewRotationIfNeeded` active during hover; it can mutate `_currentPreviewIsRotated`, invalidate `_dragVisualMetricsGrid`, resize the dragged item, and shift the computed footprint while hovering.
  - `RestoreDragVisualState` clears `_dragSourceGrid`; because `dragSourceGrid` is captured before restore, same-grid swap can still be called, but this path needs runtime verification with grid occupancy before and after.
- Current code contradicts the earlier progress note saying hover auto-rotation was removed. It has not been removed; `DraggableItemUI.Drag.cs` still calls `UpdatePreviewRotationIfNeeded` from `ResolvePreviewPlacement`.
- The older replacement logic the user referenced exists in historical `GameUIController` / current `InventoryScreenController` lineage:
  - `TryReplaceEquippedContainerFromDrag`
  - `TrySwapBackpack`
  - `TryBuildReplacementBagLayout`
  - `CanWorldItemEnterGrid`
  - `CanStaticItemEnterGrid`
  - `RefreshCharacterContainerState`
  Those were part of the old bag/rig container-equipment system. Some were intentionally removed or bypassed by the fixed 5x6 backpack redesign, but the user may now mean item-to-item/grid replacement rather than bag-equipment replacement. Clarification required before reintroducing removed container-equipment behavior.
- Search animation has two paths:
  - Ordinary loot boxes rely on `DraggableItemUI.Update` with `AutoTickSearchProgress = true`.
  - BoardGame F-key loot overlay sets `AutoTickSearchProgress = false` and manually drives `TickReveal`.
- Current BoardGame search path improved over `HEAD` by calling `RefreshSearchVisuals()` every frame and syncing partial progress to `BoardNodeRuntimeState`, but visual freezing can still happen if the current reveal item is not returned by `GetCurrentRevealItem`, if spawned item views inherit inactive/hidden icon state, or if `TickSearchRevealAnimation` is advanced twice in one frame during manual refresh.

## 2026-05-30 Equipment Slot System Findings

- The user approved a real equipment-slot system while preserving the current backpack UI direction: six equipment slots, a fixed 5x6 backpack grid, hidden old container slots, and the latest slot frame art at `Assets/Art/Sprites/UI design/Bag/Jpg_SlotFrame.PNG`.
- Directly renaming existing `ItemType` values is risky because Unity serializes enum values by integer and current code branches on `ItemType.Bag` and `ItemType.Rig` for container behavior, world secondary interactions, and placement restrictions.
- Safer implementation: append `ItemType.Equipment` and add a separate `EquipmentSlotKind` marker on `InventoryItemData` for head/body/face/headphone/totem slot validation.
- `PocketGrid` should be hidden and removed from active player capacity routing so actual player storage is only `BackpackGrid` and hidden UI cannot influence preview/drop coordinates.
- `Bag_Small.asset` should become the locked default 5x6 backpack. The default equipped instance is locked, but the general old backpack world/drop infrastructure remains in code.
- The equipment data being added now is intentionally placeholder-only: no combat stats, default icon, whitebox world prefab, green/blue/gold mapped to `Uncommon`/`Rare`/`Legendary`, and placeholder sell prices 100/300/800.
- A hidden default bag slot should not participate in equipment-slot screen hit testing. The visible drop targets are only `HeadSlot`, `BodySlot`, `FaceSlot`, `HeadphoneSlot`, `TotemSlotA`, and `TotemSlotB`.
- Equipment replacement should not use the old container replacement behavior of dropping the replaced item into the world. For typed equipment slots, the replaced equipment must return to the fixed `BackpackGrid`; if there is no room, the replacement must fail and restore the old equipped item.
- Stale prefab instances can still contain old `BackpackSlot` / `RigSlot` / `PocketGrid` nodes until `Canvas.prefab` is rebuilt. Runtime code must therefore reparent the active `BackpackGrid` under `LeftPanel` and hide legacy nodes defensively.
- Unity batchmode cannot rebuild `Assets/Prefabs/Canvas.prefab` while the same project is open in another Unity editor instance. The current open editor process is recorded by `Library/EditorInstance.json` as process `47324`, so prefab rebuilding must either run from that open editor through the queued flag or after closing the editor.
- After the user closed the editor, asset generation could run directly in the main project, but this Windows session retained no-window Unity processes that report only 32 KB memory and cannot be stopped by the current shell. A temporary project copy can still run the same prefab rebuild method and produce a valid Unity-serialized `Canvas.prefab`; copying back only that prefab is the least invasive workaround when the main project path remains blocked by stale Unity processes.
- The completed prefab now has the intended final backpack route: six visible typed equipment slots, one hidden locked default `Bag_Small` slot, one fixed 5x6 `BackpackGrid`, one dynamic right-side `LootChestGrid`, and no named old visible `RigSlot`, `BackpackSlot`, `TacticalRigPanel`, `PocketGrid`, or `RigInternalGrid` route.

## 2026-05-31 Current Implementation Contradiction Audit

Active implementation plan used for this audit:

- The normal backpack UI has six visible typed equipment slots only: Head, Body, Face, Headphone, Totem A, and Totem B.
- Player storage is a fixed 5-column by 6-row `BackpackGrid`.
- Old Bag/Rig container equipment may remain as historical assets/code infrastructure, but it must not be a visible/effective player capacity route in the current backpack UI.
- A hidden locked default `Bag_Small` may exist only to preserve runtime compatibility; it must not be draggable, replaceable, or a source of extra storage.
- Right-side loot containers may be dynamic.
- The loot TSV/template is the source of truth for imported loot item fields.
- New placeholder equipment assets/prefabs must not be added to loot-box drop tables.

Contradictions found:

1. BoardGame loot/backpack bridge still uses a 6x4 player inventory instead of the fixed 5x6 backpack plan.
   - `Assets/Scripts/BoardGame/Runtime/BoardGameBagLayoutSettings.cs` defaults `_playerInventoryColumns = 6` and `_playerInventoryRows = 4`.
   - `Assets/Scenes/Scene_zl_BoardGame.unity`, `Assets/Scenes/Scene_sdw_BoardGame.unity`, and `Assets/Scenes/Scene_ZHY_BoardGame 2.0.unity` serialize the same 6x4 values.
   - This contradicts the fixed 5x6 player storage rule for the BoardGame F-key loot overlay path.

2. One BoardGame scene has the bag/loot inventory bridge disabled.
   - `Assets/Scenes/Scene_ZHY_BoardGame 2.0.unity` serializes `_enableBagSystem: 0`.
   - The current plan includes the BoardGame F-key loot/search overlay using the backpack-style slot flow. This scene will still use the bagless/auto-resolve path instead of the current overlay implementation.

3. BoardGame runtime can expand the player inventory grid when item count exceeds configured capacity.
   - `Assets/Scripts/BoardGame/Presentation/BoardGameLootInventoryController.cs` increases `columns` beyond the configured value when `playerItems.Count > availableSlots`.
   - That behavior intentionally avoids dropping data, but it contradicts "fixed player backpack capacity" if the BoardGame bridge is expected to enforce the same fixed 5x6 storage rule.

4. The normal inventory session path can still rebuild `BackpackGrid` from arbitrary session player dimensions.
   - `InventoryScreenSessionContext` still exposes `UseCustomPlayerInventory`, `PlayerColumns`, `PlayerRows`, `PlayerBlockedCells`, `PlayerItems`, and `PlayerCellStates`.
   - `InventoryScreenController.PrepareSessionForDisplay` rebuilds `BackpackGrid` from those session dimensions when `UseCustomPlayerInventory` is true.
   - Current `LootBoxEntity` does not set `UseCustomPlayerInventory`, so ordinary loot boxes are safe today. The remaining generic path is still contrary to the fixed 5x6 rule if reused by any future caller.

5. `SceneLylSupportMigrator` still treats old rig/pocket/backpack-slot references as required valid inventory support.
   - It imports/rebinds `PocketGrid`, `TacticalRigGrid`, `RigSlot`, and old `BackpackSlot`.
   - `HasValidInventoryReferences` requires those old references to be non-null.
   - This contradicts the current prefab/UI contract, where `PocketGrid`, `TacticalRigGrid`, and visible `RigSlot`/old `BackpackSlot` should not be part of the active UI route.

6. `InventoryScreenController.GetEquipmentSlotAtScreenPosition` still tests `RigSlot` as a drop target after the six real equipment slots.
   - Runtime hiding normally prevents this in the rebuilt `Canvas.prefab`, where `RigSlot` is null.
   - In a stale scene that still has a visible `RigSlot`, drag/drop could still route into old rig-slot handling. This is a conditional contradiction with the "six visible equipment slots only" rule.

7. Several `InventoryItemData` assets do not match the current TSV source of truth for item shape.
   - TSV has all 15 non-equipment loot rows as 1x1.
   - Current SO assets still have larger shapes for: `organic_fiber` 2x2, `military_radio` 1x2, `uphone_phone` 1x2, `lesser_organic_fiber` 2x2, `intel_file` 1x2, `advanced_organic_fiber` 2x2, `top_organic_fiber` 2x3, `graphics_card` 1x2, `saluzzo_aged_wine` 1x2, and `wizard_hat` 3x3.
   - This is a data synchronization contradiction rather than a code contradiction: the importer would overwrite the SOs if run, but the current checked-in assets are not aligned with the current TSV.

Non-contradictions verified:

- `Assets/Prefabs/Canvas.prefab` currently contains the intended six visible typed equipment slots plus `HiddenDefaultBackpackSlot`, and no named visible old `RigSlot`, `BackpackSlot`, `PocketGrid`, `RigInternalGrid`, or `TacticalRigPanel` nodes.
- `Canvas.prefab` serializes `PocketGrid` and `TacticalRigGrid` as null and points the legacy `BackpackSlot` field at `HiddenDefaultBackpackSlot`, which is locked and hidden offscreen.
- The current TSV has 19 columns, 27 rows, 12 equipment rows, valid `EquipmentKind` values, and no container values on non-container rows.
- All 12 placeholder equipment item assets and matching `World_*` prefabs exist.
- The 12 placeholder equipment item GUIDs are referenced only by their own world prefabs in the scanned prefab/scene/SO assets; they are not present in loot-box drop tables.

## 2026-05-31 Backpack Plan Compliance Cleanup Findings

- BoardGame inventory compliance should use fixed 5x6 projection rather than temporary column expansion. To avoid silently deleting old over-capacity runtime state, overflow BoardGame items are preserved in memory but not shown in the fixed grid.
- `InventoryScreenSessionContext.UseCustomPlayerInventory` remains as a session concept for result exchange, but it no longer controls player backpack dimensions. The active player grid is still the fixed `BackpackGrid`.
- The old world Bag/Rig equipment/replacement interaction remains intentionally disabled because the approved current plan keeps only the hidden locked default backpack and six visible typed equipment slots.
- The loot TSV/template remains the source of truth and now combines the restored original normal loot economics/footprints with the approved equipment-slot placeholder rows.
- The backpack prefab rebuild tool is now menu-driven only; it no longer runs automatically from a Temp flag during editor domain reload.

## 2026-06-07 Backpack UI Asset Integration Planning

- The requested `Sprites/UI` directory does not exist at the workspace root. The matching Unity asset paths are `Assets/Art/Sprites/UI` and `Assets/Art/Sprites/UI design/Bag`.
- `Assets/Art/Sprites/UI design/Bag` contains the most directly named backpack UI art: `Jpg_Background.PNG`, `Jpg_Backpack.PNG`, `Jpg_Bag.PNG`, `Jpg_Equipment.PNG`, `Jpg_SlotFrame.PNG`, `Jpg_Value.PNG`, and five rarity frame sprites.
- `Assets/Art/Sprites/UI/Backpack_background` and `Assets/Art/Sprites/UI/Backpack_Slot` contain earlier/generated cut names such as `IMG_0607.PNG` and `IMG_0590.PNG`; these overlap with the named Bag design art but are less self-documenting.
- Current backpack runtime is script-driven through `InventoryScreenController`, `InventoryUIController`, `InventoryItemFactory`, `EquipmentSlotUI`, and `DraggableItemUI`.
- The shared authored UI prefab is `Assets/Prefabs/Canvas.prefab`. The current backpack contract is six visible typed equipment slots, a fixed 5x6 `BackpackGrid`, and a dynamic right-side `LootChestGrid`.
- UI slot art enters the system in three places: `InventoryUIController.CellBackgroundSprite` for grid cell backgrounds, each equipment slot root `Image.sprite` for equipment frames, and the draggable item prefab/root `Image.sprite` or item data `ItemIcon` for item visuals.
- The named `Jpg_SlotFrame.PNG` asset is already imported as a UI Sprite and is referenced by some equipment slot `Image` components in `Canvas.prefab`.

## 2026-06-01 Enemy Patrol Hit-Reaction Diagnosis

- Player bullet damage currently reaches enemies through `BulletController.ApplyElementalDamage`, then calls `EnemyHealthController.TakeDamage(finalDamage)` with no attacker, instigator, hit point, or incoming direction.
- `EnemyHealthController.TakeDamage` raises `EnemySuspicionStimulusBus.ReportEnemyDamaged(transform.position, null)`. That reports the damaged enemy's own position and a null source, so patrol AI can at best investigate itself instead of knowing who attacked.
- Player magic attacks in `PlayerShootingController.TryCastIceFreeze` and `TryCastIceCone` also call `EnemyHealthController.TakeDamage(...)` without source context.
- The shared patrol enemies (`EnemyBehaviorController`, `RangedEnemyBehaviorController`, `ModernStranderBehaviorController`, `TidalAberrationBehaviorController`, and `AncientStranderBehaviorController`) already have patrol/chase/attack state machines and awareness components. They can chase once `CurrentState` becomes combat state, but there is no unified "I was attacked by this target" entry point.
- `EnemyLookController` already supports combat look intent and body snapping, so the missing piece is not visual-turn capability. The missing piece is a damage/aggro event that tells the owning behavior which target to face and whether to enter chase or attack.
- Existing suspicion events are appropriate for sounds and impact investigation, but direct damage should be stronger than ordinary suspicion and should not depend on hearing radius, vision cone, or line of sight if the desired gameplay is immediate retaliation.

## 2026-06-01 Enemy Direct-Hit Combat Response Findings

- User confirmed the default behavior: player direct damage should make only the hit patrol enemy lock the player and enter chase; nearby enemies should not be alerted; Boss and Anchor Sentinel remain out of scope.
- `EnemyDamageContext`, `EnemyDamageSourceType`, and `IEnemyDirectDamageReceiver` now live in `EnemyHealthController.cs` because the generated Unity csproj did not include newly added source files until Unity regenerates it.
- Player projectile, Ice Freeze, and Ice Cone now pass player attacker context into enemy damage. `EnemyHealthController` only invokes direct combat receivers for direct player damage; source-less damage continues to use the previous suspicious damaged stimulus.
- Player gunshot and player bullet impact stimuli are disabled by default through `ReportGunshotStimulus=false` and `BulletController.ReportImpactStimulus=false` on player-fired bullets, matching the no-nearby-alert requirement.
- The five patrol enemy controllers implement `IEnemyDirectDamageReceiver` and respond by assigning the player target, clearing awareness, turning to face the player, unstopping the NavMeshAgent, setting destination to the player, and entering `Chase`. Existing distance checks then transition into attack naturally.

## 2026-06-01 Enemy Direct-Hit Long-Range Chase Findings

- The tester-reported "hit outside patrol range, enemy stands still" case can occur because direct damage enters `Chase`, then the next chase tick immediately sees `distanceToPlayer > LoseRange`, raises a last-seen stimulus, resets patrol, and returns. This makes the direct hit reaction visually collapse back into idle/patrol.
- The fix should preserve the normal leash after a short response period, so the enemy does not chase across the map forever. A short forced chase window after direct player damage is enough to let the agent acquire a real path and visibly retaliate.
- Directly setting `NavMeshAgent.SetDestination(PlayerTransform.position)` is brittle if the player's exact transform point is off the NavMesh or sitting on a collider edge. Sampling near the player first, then falling back to the recorded damage source position, gives the agent a better first chase target.
- The direct-hit response still does not alert nearby enemies. It is local to the damaged enemy through `IEnemyDirectDamageReceiver`.

## 2026-06-02 Enemy Direct Attacker Retaliation Findings

- The new "start game, character attacks enemy, enemy remains Patrol and stands still" report can occur when the attacker is an Agent pawn rather than the player.
- Before this fix, Agent direct fallback damage called `EnemyHealthController.TakeDamage(float)` with no attacker context, and Agent-fired bullets did not assign `BulletController.SourceTransform`. `BulletController` also only converted player sources into direct damage context.
- Source-less enemy damage entered `EnemyHealthController.NotifyDamageReaction` as a generic damaged stimulus. The hit enemy could consume that as patrol awareness, stop its NavMeshAgent in Suspicious/Search, and keep the main enemy `CurrentState` as Patrol because no combat target was assigned.
- The fix generalizes `EnemyDamageContext` from "direct player damage" to "direct attacker damage" while preserving the player-specific field for compatibility.
- `IEnemyDirectDamageReceiver.NotifyDirectDamage` now receives direct Agent/player damage, and all five patrol enemy controllers assign the direct attacker as their combat target, clear awareness, face the target, unstop navigation, and enter Chase.
- `ICombatDamageReceiver` lets enemy attacks damage both `PlayerHealthController` and `AgentPawnRoot`. Player-only effects such as silence, pull, corrosion tint, and movement knockback still apply only when the target has the matching player component.
- Source-less damage now reports `EnemyDamaged` with the damaged enemy transform as source, preventing the damaged enemy from treating its own no-source damage as an external patrol suspicion event.

## 2026-06-05 Enemy Script Commenting Findings

- `Assets/Scripts/Core` uses Chinese XML documentation comments for public classes, public methods, and important public members. It also uses short `//` comments near private logic blocks where the control flow is non-obvious.
- Enemy runtime scripts are concentrated under `Assets/Scripts/Gameplay/Enemy`, with supporting config classes in `Assets/Scripts/Gameplay/Enemy/Config`.
- The current task should be comment-only: do not change enemy behavior, serialized field names, prefab references, or public API signatures.

## 2026-06-08 Enemy Art/Animation Integration Findings

- Enemy model candidates are under `Assets/Art/Models/Characters`: `AI.fbx`, `Guardian.fbx`, `Mud.fbx`, `Skeleton.fbx`, `Tracer.fbx`, and `Zombie.fbx`, plus three basecolor JPEG textures and one reference JPG.
- Enemy pawn prefabs are under `Assets/Prefabs/Enemy/Pawn`, and their actual names use `Pfb_Enemy_*`, not `Pre_*`: `Enemy.prefab`, `Pfb_Enemy_RangedEnemy.prefab`, `Pfb_Enemy_Common_ModernStrander.prefab`, `Pfb_Enemy_Common_TidalAberration.prefab`, `Pfb_Enemy_Common_AncientStrander.prefab`, `Pfb_Enemy_Common_AnchorSentinel.prefab`, and `Pfb_Enemy_HunterBoss.prefab`.
- The current pawn prefabs are still mostly whitebox visuals: root objects have built-in primitive `MeshFilter`/`MeshRenderer` meshes, with runtime behavior scripts, `EnemyHealthController`, colliders, NavMeshAgent where applicable, health bar canvas, and attack/vision helper transforms as children.
- No enemy pawn prefab currently serializes an `Animator` component or any reference to the new enemy animator override controllers.
- Runtime enemy behavior scripts currently do not drive Animator parameters such as `Speed` or `Attack`; the animation controller assets exist, but prefab binding plus runtime parameter driving are still needed for visible animation playback.
- Existing enemy animation setup uses `Assets/Art/Animation Controllers/Enemy/AC_Enemy_Base.controller` as the shared state machine with `Speed` and `Attack`, and five override controllers: `AOC_Enemy_BasicMelee`, `AOC_Enemy_Ranged`, `AOC_Enemy_ModernStrander`, `AOC_Enemy_TidalAberration`, and `AOC_Enemy_AncientStrander`.
- Confirmed clip mapping: BasicMelee override uses Zombie idle/walk/attack clips; Ranged override uses Skeleton idle/walk/attack clips; ModernStrander, TidalAberration, and AncientStrander currently still use generic `Idle`, `Run`, and `Attack` clips.
- Model-to-prefab matching is certain only for `Zombie.fbx` to `Enemy.prefab`/basic melee and `Skeleton.fbx` to `Pfb_Enemy_RangedEnemy.prefab`; `AI.fbx`, `Guardian.fbx`, `Mud.fbx`, and `Tracer.fbx` need art/design confirmation against ModernStrander, TidalAberration, AncientStrander, HunterBoss, and possibly AnchorSentinel.
- Important prefab helper transforms to preserve: Ranged `FirePoint`; ModernStrander `TentacleOrigin`; TidalAberration `MeleeOrigin` and `RangedOrigin`; AncientStrander `MeleeOrigin` and `BiteOrigin`; AnchorSentinel `EyeOrigin` and rune cube children; HunterBoss `MeleeOrigin`, `ProjectileOrigin`, and `EyeOrigin`.

## 2026-06-08 Backpack UI Asset Integration Review

- The user-facing "Sprites/UI" assets are present under Unity paths `Assets/Art/Sprites/UI` and `Assets/Art/Sprites/UI design/Bag`.
- The clearest backpack-specific set is `Assets/Art/Sprites/UI design/Bag`: `Jpg_Background.PNG` (677x1033 panel), `Jpg_Backpack.PNG` (745x652 precomposed 6x5 grid), `Jpg_Bag.PNG` (617x87 strip), `Jpg_Equipment.PNG` (761x158 precomposed six-slot row), `Jpg_SlotFrame.PNG` (180x180 slot frame), `Jpg_Value.PNG` (1664x227 long value strip), and five 64x64 rarity frame/icon sprites.
- All inspected Bag sprites are already imported as `Sprite (2D and UI)` with alpha transparency, but their `spriteBorder` values are `{0,0,0,0}`. Any sprite used as a sliced scalable panel/frame should be given a proper 9-slice Border in Sprite Editor before being assigned to `Image.Type.Sliced`.
- `Jpg_Backpack.PNG` visually represents a 6-column by 5-row grid. The current approved player backpack system is fixed 5 columns by 6 rows, so this sprite should not be used directly as the authoritative `BackpackGrid` background unless art exports a matching 5x6 version or the design intentionally changes.
- `Jpg_SlotFrame.PNG` is the safest existing grid/slot sprite because `InventoryUIController.RebuildBackgroundCells` generates individual cell `Image`s and sets them to `Image.Type.Sliced`.
- Current runtime entry points are `InventoryScreenController`, `InventoryUIController`, `EquipmentSlotUI`, `InventoryItemFactory`, and `DraggableItemUI`.
- `Canvas.prefab` does not contain `InventoryScreenController` or `InventoryItemFactory`; those are serialized on scene `GameManager` objects. The prefab contains the visual UI objects, grid controllers, and equipment slot components.
- `Canvas.prefab` already references `Jpg_SlotFrame.PNG` for the six visible equipment slot root `Image`s and for `BackpackGrid.CellBackgroundSprite` / `LootChestPanel.CellBackgroundSprite`.
- `BackpackGrid` in `Canvas.prefab` is configured as 5x6 with `CellSize=78` and `Spacing=6`. `LootChestPanel` is a dynamic external grid currently serialized as 5x5 with `CellSize=50` and `Spacing=2`, but runtime loot containers rebuild its dimensions from the session.
- The draggable item prefab is `Assets/Prefabs/Prefab.prefab`. It is very minimal: root `Image` + `DraggableItemUI`, no preauthored `ItemIcon` child and no bound `AmountText`. `DraggableItemUI` creates `ItemIcon` at runtime and uses solid rarity colors for the root item background.
- The five `S_ItemIcon_Rarity_*.png` sprites are not used by runtime `DraggableItemUI` as rarity frames. Some test/container item data assets currently use them as `ItemIcon`, but the draggable item background still comes from `ResolveRarityBackgroundColor`.
- Scene binding differs by scene. `Scene_lyl_IslandWhitebox.unity` has the six equipment slots, `DefaultBackpackItem`, `BackpackGrid`, `LootChestGrid`, `DraggableItemPrefab`, and `GlobalDragLayer` bound. `Scene_lyl.unity` and `Scene_ZL/Scenel_Zl_IslandWhitebox.unity` have grid/factory references but their serialized `InventoryScreenController` snippets do not include the newer six equipment slot/default backpack fields. Several MVP/test scenes have `DraggableItemPrefab` and `GlobalDragLayer` set to null.
- There is no current `BackpackUiPrefabRedesignTool` or equivalent editor rebuild script in `Assets/Scripts/Editor`; UI art integration should therefore be done through prefab/scene Inspector configuration or a new dedicated editor tool if repeatability is required.

## 2026-06-10 Enemy Chase Indicator Findings

- The requested chase icon asset is `Assets/Art/Sprites/UI/Attack_range_Enemy_lock-on_indicator_Boss_symbol/IMG_0583.PNG`, imported as a UI Sprite with GUID `23d7599f50b28934ca3444e8d7f1dac1`.
- The requested persistent Boss icon asset is `Assets/Art/Sprites/UI/Attack_range_Enemy_lock-on_indicator_Boss_symbol/IMG_0606.PNG`, imported as a UI Sprite with GUID `7f350447a8954624084a07f13242a32f`.
- Chase-capable enemy pawn prefabs are `Enemy.prefab`, `Pfb_Enemy_RangedEnemy.prefab`, `Pfb_Enemy_Common_ModernStrander.prefab`, `Pfb_Enemy_Common_TidalAberration.prefab`, `Pfb_Enemy_Common_AncientStrander.prefab`, and `Pfb_Enemy_HunterBoss.prefab`.
- `Pfb_Enemy_Common_AnchorSentinel.prefab` and `HunterBossAnchorProjectile .prefab` were skipped by the binder because they do not expose one of the supported Chase state components.
- `EnemyChaseIndicatorController` creates a world-space UI Image at runtime, uses collider/renderer bounds to keep the icon above the enemy, faces `Camera.main`, and supports `ChaseOnly` or `Always` visibility modes.
- `Pfb_Enemy_HunterBoss.prefab` now has two indicator components: `ChaseOnly` with `IMG_0583` and `Always` with `IMG_0606` using a separate `BossIndicatorCanvas`, so the Boss marker remains visible and the chase prompt can still appear separately.
- Saving the basic melee and ranged prefabs through Unity also serialized their missing `[RequireComponent]` awareness dependencies (`EnemyLookController`, `EnemySuspicionSensor`, and `EnemyPatrolAwarenessController`), matching the components that were previously installed at runtime.

## 2026-06-10 Player HUD Icon Findings

- The requested HUD art lives under `Assets/Art/Sprites/UI/Main_character, progress_bar, status_bar`: `IMG_0582.PNG`, `IMG_0584.PNG`, `IMG_0586.PNG`, and `IMG_0587.PNG`.
- Sprite GUIDs are `IMG_0582` = `09b6358d73c12234999e0fc89fe1ce29`, `IMG_0584` = `acfe167cc8d0839469caf455d79f02b9`, `IMG_0586` = `e52b4701264234d49bcfaa58079a4e1d`, and `IMG_0587` = `8dbd55724764ce04ea03c5d5de593bd2`.
- `PlayerStatusHudController` is not authored in a scene or prefab; it is created dynamically by `RaidFlowController.Start` and `PlayerHealthController.Start`. Because of that, serialized Inspector sprite references alone would be brittle for the current HUD.
- `Assets/Resources/HUD/PlayerStatusHudSpriteSet.asset` is the runtime bridge for these sprites. It lets the dynamic HUD load the configured art without modifying the already-dirty shared `Canvas.prefab`.
- `IMG_0582.PNG` is 1454x1454 but its visible portrait occupies only about 371x398 pixels near the center. The SpriteSet stores a larger square crop rect `(489, 497, 478, 478)` so the runtime HUD portrait is readable at small size without editing the source PNG.
- `IMG_0587.PNG` is 745x96 with no sprite border, so the HUD uses it as a simple progress-bar frame while the actual fill remains a runtime `Image.Type.Filled` rectangle inside the frame padding.

## 2026-06-10 Enemy Animator Avatar Configuration Findings

- Newly visible standalone Avatar assets are under `Assets/Art/Animations/Avator`: `Zombie Idle (1)Avatar.asset` and `Ske IdleAvatar.asset`.
- Character model import settings differ by enemy: `Zombie.fbx` and `Skeleton.fbx` are Humanoid (`animationType: 3`, `avatarSetup: 1`), while `Guardian.fbx`, `Mud.fbx`, and `Tracer.fbx` are Generic (`animationType: 2`, `avatarSetup: 0`).
- The five art-integrated enemy prefabs contain model instances under a direct `Visual` child, and now have a single Animator on the model instance root with `Apply Root Motion` disabled.
- `ModernStrander` uses `AOC_Enemy_ModernStrander` plus the uploaded `Zombie Idle (1)Avatar.asset`.
- `AncientStrander` uses `AOC_Enemy_AncientStrander` plus the uploaded `Ske IdleAvatar.asset`; the duplicate root/visual Animator setup has been normalized to one model Animator.
- `TidalAberration`, `AnchorSentinel`, and `HunterBoss` have their Animator controllers assigned, but their Avatar references remain empty because `Mud.fbx`, `Guardian.fbx`, and `Tracer.fbx` are Generic and no matching standalone Avatar assets were present.
- `AOC_Enemy_AnchorSentinel.overrideController` was created using the shared base enemy controller. `AOC_Enemy_HunterBoss.overrideController` and the new Anchor override currently map to the base Idle/Run/Attack clips as identity overrides until dedicated clips are supplied.
- A Humanoid Avatar generation attempt for `Mud.fbx`, `Guardian.fbx`, and `Tracer.fbx` failed because Unity could not find a valid required Hips bone, so the importer settings should remain Generic unless art re-exports those rigs with valid Humanoid mapping.
- No gameplay enemy script currently drives Animator parameters such as `Speed` and `Attack`, so prefab Animator configuration alone will not make AI movement/attack states animate beyond default controller playback.

- Enemy bugfix root causes found: Tidal Aberration Water Jet and Ancient Strander ranged bite could bypass cooldown by re-entering their ranged states because those transitions reset attack timers to the configured interval. Several enemy hit paths were too dependent on trigger callbacks or parent-only combat receiver lookup, so player collider/root layouts could make attacks appear to hit without resolving PlayerHealthController. Anchor Sentinel attack config was applied, but its EnemyHealthController prefab/config path was not synchronized, so health tuning could be ignored by systems reading the health component directly.

## 2026-06-10 Enemy Animator Runtime Driver Findings

- `AC_Enemy_Base.controller` exposes `Speed` as a float parameter and `Attack` as a trigger parameter, so runtime code can safely drive movement blend and attack transitions without changing the animator state machine.
- The patrol enemy controllers that use `NavMeshAgent` can derive animation speed from the current agent velocity: basic melee, ranged, Modern Strander, Tidal Aberration, and Ancient Strander.
- Hunter Boss moves through manual transform interpolation rather than a `NavMeshAgent`, so its animator speed needs to come from the existing boss chase state instead of agent velocity.
- Anchor Sentinel is stationary in the current combat design, so its animator speed should stay at zero and only its beam firing entry should trigger the attack animation.
- Attack animation triggers were added only at existing attack execution points. No attack timing, damage values, cooldowns, target selection, or state transitions needed to change for the requested animator hookup.

## 2026-06-11 Enemy Humanoid Animation Stabilization Findings

- The Modern Strander runtime `NullReferenceException` came from checking `animator.avatar.isValid` after the no-Avatar branch had already accepted a Generic rig with visible bones. The driver now only checks `isValid` when an Avatar object actually exists.
- Modern Strander and Ancient Strander currently use rigged Generic model instances from `Zombie Idle (1).fbx` and `Ske Idle.fbx`, with Animator Avatar intentionally empty so the extracted Generic `.anim` transform curves can drive the same bone hierarchy.
- The active Zombie/Skeleton Idle/Walk/Attack `.anim` clips all contain `mixamorig:Hips` position curves. These curves can pull the animated mesh back toward the clip origin during loop boundaries or state transitions unless X/Z motion is stabilized.
- The runtime driver now stabilizes the animated root bone X/Z in `LateUpdate`, after Animator sampling, for all enemy controllers that use `EnemyAnimatorDriver`.
- The six active humanoid clips now keep original X/Z position in their clip settings. This complements the runtime root-bone stabilization and reduces loop/transition snapping.
- Initial visual ground alignment now runs after Animator priming and first-frame sampling. This avoids aligning against the bind pose and then having the first animation frame move the mesh away from the ground.
- Ground alignment now prefers foot/toe bones when a humanoid-style foot hierarchy is available and the renderer bottom is suspiciously lower. This prevents cloak, stretched mesh, or trailing geometry from becoming the grounding reference while the actual feet float.
- The current Modern Strander prefab has only root-level FBX instance transform overrides for scale, position, rotation, controller, culling, and root motion. It no longer has prefab overrides on specific left-leg or foot bones; if the left foot still pulls after this fix, the remaining cause is inside the source FBX mesh skinning/bone weights or the authored animation pose.

## 2026-06-11 Enemy ClipUpdate Art Refresh Findings

- `Assets/Art/Animations/ClipUpdate` contains two complete replacement sets: Zombie (`Zombie Idle (2).fbx`, `ZombieIdleNew`, `ZombieWalkNew`, `ZombieAttackNew`) and Robot (`Robot.fbx`, `RobotIdle`, `RobotWalk`, `RobotAttack`).
- BasicMelee and ModernStrander both map cleanly to the new Zombie resources; AnchorSentinel maps cleanly to the new Robot resources.
- The new ClipUpdate model Animator Avatar references are intentionally empty on the prefabs so the current Generic transform-curve workflow stays aligned with `EnemyAnimatorDriver` root stabilization and avoids Humanoid/Generic binding warnings.
- The new VFX scripts (`RobotAnchorBeamVfx`, `SkeFishboneAttackVfx`, and `ZombieTentacleCorrosionVfx`) are not passive visual-only components. They contain runtime-created effects plus autonomous target selection and damage calls, so they should be integrated through explicit attack hooks or visual-only wrappers instead of being attached directly to existing enemy prefabs.

## 2026-06-12 Tidal Aberration Water Jet Test Setup Findings

- `SO_Enemy_TidalAberration.asset` and the pawn prefab both serialize `_waterJetKnockbackStrength` / `WaterJetKnockbackStrength` as `5.2`, so the effect is not disabled by data.
- `TidalAberrationBehaviorController.PerformRangedAttack()` only applies knockback through the cached `_playerMovementController`; damage uses `CombatDamageUtility.TryGetDamageReceiver(hit.collider, ...)`, but knockback does not resolve movement from the actual hit receiver/root at hit time.
- A likely failure mode is "damage receiver found, movement controller missing or stale": if the ray hits a child collider or a combat target whose `DamageRootTransform` differs from the originally assigned transform, damage can land while `_playerMovementController` stays null, so no impulse or slow is applied.
- A second likely failure mode is that the unmasked `Physics.Raycast` can hit Tidal Aberration's own collider or other level colliders before the player because it does not ignore the enemy root. In that case both damage and knockback would be skipped.
- The current player prefab `Assets/Prefabs/PlayerPrefab/Agent.prefab` includes an enabled `NavMeshAgent` on the same root as `PlayerMovementController`. `PlayerMovementController.FixedUpdate()` skips normal rigidbody movement when the NavMeshAgent is considered controlling movement, so `ApplyExternalImpulse()` can add external velocity that is never applied to `MovePosition` before being decayed.
- The temporary dedicated scene setup was later reverted at user request. Current `Scene_lyl_test` contains no `EnemyFunctionTestArea`, test marker, or test Tidal Aberration instance.

## 2026-06-12 Enemy Function Test Area Expansion Findings

- The expanded 60x60 `EnemyFunctionTestArea` and test-lane player configuration were reverted at user request.
- Current `Scene_lyl_test` has been restored to the version without the dedicated enemy test field, so any future Water Jet test setup should be recreated intentionally instead of assuming those scene objects still exist.

## 2026-06-12 Tidal Aberration Water Jet Existence Check Findings

- Water Jet exists. The implementation lives in `TidalAberrationBehaviorController.PerformRangedAttack()`, and the knockback call is `_playerMovementController.ApplyExternalImpulse(direction, WaterJetKnockbackStrength)`.
- The Tidal Aberration config and prefab both keep knockback enabled: `WaterJetKnockbackStrength` is `5.2`, the post-hit slow multiplier is `0.5`, and slow duration is `1s`.
- Water Jet has a narrow usable range. Runtime config uses `MinimumRangedDistance = 5` and `RangedAttackRange = 9`; outside that band the enemy chases, and below melee range it switches to Electric Tentacle instead.
- The visual is intentionally very short: `WaterJetDuration = 0.18`, so even when it fires it can be easy to miss unless looking directly at the enemy or logging the skill damage.
- Current `Scene_lyl_test` places the normal scene `TidalAberration` around world `(7.9, 1.0, -36.78)` and `Player` at `(12.8, 1.0, -46.9)`, roughly `11.2m` apart. That starts outside the 9m Water Jet window.
- Player knockback visibility can be masked by the player prefab's enabled same-root `NavMeshAgent`. The current `PlayerMovementController` skips its rigidbody movement branch whenever that agent is considered controlling movement, which can make Water Jet's external impulse invisible even though the skill call exists.

## 2026-06-12 Tidal Aberration Water Jet Visible Test Tuning Findings

- The current scene's starting `Player` to `TidalAberration` distance of about `11.2m` now falls inside the Water Jet test window because `RangedAttackRange` was increased to `13m`.
- Water Jet is now much more visible: the renderer lasts `0.65s` instead of `0.18s`, uses thicker widths, and pulses between bright cyan and white.
- Water Jet hit detection is more forgiving and more reliable for testing because it uses a `0.65` radius sphere cast, skips the caster's own colliders, and then applies damage/knockback to the closest valid combat receiver.
- Knockback no longer depends solely on the cached `_playerMovementController`; the hit damage root or collider is used to resolve the movement controller at the moment of impact.
- Player external impulses now briefly take priority over same-root `NavMeshAgent` movement and clear the agent path. This affects all external impulse users, not just Water Jet, and makes knockback-style effects visibly move the player during tests.

## 2026-06-12 Tidal Aberration Runtime No-Knockback Diagnosis Findings

- The earlier `11.2m` test-friendly distance no longer matches the current saved scene. Current `Scene_lyl_test` places `Player` around `(37.72, 1.00, -39.07)`.
- The active scene Tidal Aberration is under `Zone_A/ActiveEnemyCluster_A`; combining parent and local transforms puts it around `(7.90, 1.00, -36.78)`.
- That makes the current start distance roughly `29.9m`, which is outside both Tidal's `16m` detection range and its `13m` Water Jet range.
- In code, Water Jet can only occur after Patrol sees the player, enters Chase, and then enters `RangedAttack` while distance is between `MinimumRangedDistance` and `RangedAttackRange`. Current placement fails before that state transition.

## 2026-06-12 Tidal Aberration Scene Player Placement Findings

- Current `Scene_lyl_test` Player is now placed at `(7.90, 1.00, -44.78)`, about `8m` from the existing Tidal Aberration.
- This placement should start inside the Water Jet range window while remaining outside the effective melee range, so the enemy can enter `RangedAttack` instead of immediately switching to Electric Tentacle.

## 2026-06-12 Tidal Aberration Opening Water Jet Bugfix Findings

- Starting inside the Water Jet distance window still depended on the patrol/chase/ranged state handoff, so the enemy could fail to visibly cast before Player damage ended the test.
- Water Jet's sweep previously picked the nearest non-own collider first and only then checked for a combat receiver. If that collider was ground or scene geometry, the skill logged `0` damage and never called player knockback.
- The controller now queues an opening Water Jet when Player starts in the ranged window and fires it on the first Update, before normal patrol awareness timing can delay the test.
- The sweep now searches for the nearest valid current Player combat receiver instead of the nearest arbitrary collider, and falls back to the current Player combat receiver if no sweep hit resolves cleanly.

## 2026-06-12 Tidal Aberration No Visible Knockback Audit Findings

- Current Unity `Editor.log` proves Water Jet is firing from the opening path and dealing damage: `[EnemySkillDamage] TidalAberration finished Water Jet, total damage: 14`.
- The active Player scene object is a prefab instance of `Assets/Prefabs/PlayerPrefab/Agent.prefab` at `(7.9, 1, -44.78)` and has no scene-added components.
- `Agent.prefab` contains `PlayerHealthController`, so `CombatDamageUtility.TryGetPlayerHealthReceiver()` resolves a valid damage receiver and Water Jet damage succeeds.
- `Agent.prefab` does not contain `PlayerMovementController`, and the scene instance also has no added `PlayerMovementController` component. A GUID scan found no `52125890361f2e0428d8b29a658611ce` reference in the prefab or scene.
- `TidalAberrationBehaviorController.PerformRangedAttack()` applies knockback only if `ResolveMovementController()` returns a non-null `PlayerMovementController`. On the current Player/Agent setup that resolution returns null, so `ApplyExternalImpulse()` is not called.
- Therefore the present issue is not Water Jet range, cooldown, visual duration, damage, or hit detection. It is an integration mismatch: the damage target is the Agent/Player health receiver, but the knockback implementation is coupled to a movement component that this Player prefab does not currently have.

## 2026-06-12 Agent External Movement Receiver Migration Findings

- The correct fix is to migrate the external movement receiving contract, not to reattach the old manual input movement controller to the automatic Agent player.
- `IExternalMovementReceiver` is now the shared contract for enemy/player external movement effects. This preserves old `PlayerMovementController` compatibility while allowing `AgentPawnRoot` to receive knockback and slow directly.
- `AgentPawnRoot.ApplyExternalImpulse()` stops and clears the NavMeshAgent path before pushing the Agent through `NavMeshAgent.Move()`, so automatic movement does not consume the knockback in the same frame.
- `AgentPawnRoot.ApplyMoveSpeedDebuff()` updates a local debuff multiplier, and `SyncBodyFactsToBlackboard()` writes the effective speed back to `AgentBlackboardKeys.MoveSpeed`, so normal Agent behavior observes the Water Jet slow.
- Water Jet now resolves `IExternalMovementReceiver` from the same damage root/collider path used for the hit, so the current `Agent.prefab` can be damaged and knocked back through the same target object.

## 2026-06-12 Modern Strander No-Damage Audit Findings

- Current `Scene_lyl_test` has one `ModernStrander` prefab instance under `ActiveEnemyCluster_A`; its root GameObject override sets `m_IsActive` to `0`, so `ModernStranderBehaviorController.Start()` and `Update()` never run for that instance.
- The same `ActiveEnemyCluster_A` lists that disabled scene enemy as an initial enemy member, but `EnemyHealthController.IsAlive` requires `gameObject.activeInHierarchy`, so target discovery treats the disabled enemy as not alive.
- `EnemyResourceCluster_A` has `_enemyPrefabs: []`, so that source cluster is not configured to spawn a replacement ModernStrander at runtime.
- Recent Unity Editor logs contain no `ModernStrander finished Corrosive Tentacle` or `Corrosive Tentacle` entries, while Tidal Aberration skill-damage logs are present. That matches the inactive-scene-instance finding.
- When active, ModernStrander's intended damage flow is config apply -> player receiver resolution -> Patrol/Chase/Attack state machine -> `BeginTentacleStrike()` -> `TryLatchCurrentCombatTarget()` or `ModernStranderTentacleHitbox` overlap -> `NotifyTentacleHit()` -> `CombatDamageUtility.ApplyDamageTo()` plus `PlayerHealthController.ApplyCorrosion()`.
- Current `Assets/Prefabs/PlayerPrefab/Agent.prefab` has root tag `Player`, root `CapsuleCollider`, `Rigidbody`, `PlayerHealthController`, and `AgentPawnRoot`, so ModernStrander's damage receiver lookup should resolve `PlayerHealthController` for the current player. Missing old `PlayerMovementController` only skips the latch pull effect, not the damage.
- Secondary fragility: `CorrosiveSlimePuddle.OnTriggerStay()` checks `other.CompareTag("Player")` before looking up `PlayerHealthController`. If future Player colliders are moved to untagged child objects, puddle damage can be skipped even when the root has `PlayerHealthController`. The current Agent root collider is tagged `Player`, so this is not the main current cause.

## 2026-06-12 Modern Strander Active Scene Re-Audit Findings

- Re-checking the current `Scene_lyl_test` state shows the previous inactive finding is no longer current: the scene `ModernStrander` prefab instance now has `m_IsActive: 1`.
- Current placement puts `Zone_A` at approximately `(8.06, -0.32, -36.6)`, ModernStrander locally at `(-3.47, 1.32, -0.5)`, and the player at `(7.9, 1, -44.78)`. That places ModernStrander around `(4.59, 1.0, -37.1)`, about `8.36m` from the player.
- ModernStrander's config uses `DetectionRange: 12`, `ViewAngle: 78`, `AttackRange: 3.2`, `AttackInterval: 5`, and `InitialContactDamage: 6`. The player is inside detection range but outside attack range.
- With the current transform data, the player is roughly behind ModernStrander's default forward direction, around `156.7` degrees away from +Z. `EnemyVisionUtility` uses half of `ViewAngle`, so a 78-degree cone only allows roughly 39 degrees to either side. Initial patrol sight should therefore fail unless scanning or direct-damage retaliation rotates it toward the player.
- The player Agent config has `AttackRange: 10`, `AttackDamage: 15`, and `AttackInterval: 0.65`. In the current test layout, the player can attack ModernStrander immediately from outside ModernStrander's 3.2m tentacle range.
- If ModernStrander is damaged, the code should force a chase through `NotifyDirectDamage(...)`, but the latest logs still contain no ModernStrander / Corrosive Tentacle damage entries, so there is no evidence that it has reached the latch/damage resolution path in recent play sessions.
- Updated likely cause: the skill/damage implementation exists, but the current scene setup gives the player a range advantage while ModernStrander starts outside melee range and outside its initial vision cone. It can be killed before the tentacle latch occurs, which appears to the tester as "no damage".

## 2026-06-12 Modern Strander Direct-Hit Counter Fix Findings

- Added `DirectDamageCounterAttackRange` to `ModernStranderConfig` and `SO_Enemy_ModernStrander.asset`, defaulting to `10.5m`. This intentionally covers the current Agent player's `10m` auto-attack range while preserving the normal `3.2m` melee tentacle range.
- `ModernStranderBehaviorController.NotifyDirectDamage(...)` now checks that the enemy is still alive after the damage reaction, locks the direct player attacker, faces the target, and attempts an immediate counter tentacle if the player is within counter range and line of sight.
- The old direct-damage reaction always called `StopTentacleAttack()`. That could let the player's rapid auto-fire repeatedly cancel an in-progress tentacle strike/latch. The direct-damage reaction now keeps active tentacles in `Attack` instead of interrupting them.
- Counter tentacles use the wider counter range only for that strike's latch/hold window. Normal patrol/chase attacks still require the configured `AttackRange` before they begin.
- ModernStrander latch pull now resolves `IExternalMovementReceiver`, not only `PlayerMovementController`. The current Agent player can therefore receive the pull through `AgentPawnRoot`.
- `IExternalMovementReceiver` now includes `ApplyExternalPull(...)`; `PlayerMovementController` already had the matching method, and `AgentPawnRoot` now implements it by feeding the existing external movement override path.
- Validation passed: `dotnet build Assembly-CSharp.csproj --no-restore /nologo /verbosity:minimal`, `dotnet build Assembly-CSharp-Editor.csproj --no-restore /nologo /verbosity:minimal`, and Unity batch compile in `Logs/ModernStranderCounterFixCompile.log` all completed without C# errors.

## 2026-06-12 Modern Strander Body Push Findings

- ModernStrander's Animator has root motion disabled, so the observed push is not authored walk animation root motion.
- Both ModernStrander and Player use enabled, non-trigger root `CapsuleCollider`s on Default layer with `NavMeshAgent` radius `0.5`.
- ModernStrander's prefab `NavMeshAgent` has `m_StoppingDistance: 0`, so chase destinations set directly to the Player position can drive the enemy body into the Player collider before/around attack transitions.
- The intended gameplay effect should come from the tentacle hitbox/pull, not from the walking body collider. The fix should keep the tentacle trigger active while preventing root body collision from physically pushing the player.
- Implemented fix: ModernStrander now ignores collision between its root body collider and the Player body collider after a combat target is assigned, while keeping child tentacle trigger logic intact.
- Implemented fix: ModernStrander chase destinations now resolve to a stand-off position near attack range instead of the Player's exact position, so the `NavMeshAgent` no longer tries to walk into the Player center.
- Validation passed: `dotnet build Assembly-CSharp.csproj --no-restore /nologo /verbosity:minimal` completed with 0 warnings and 0 errors.

## 2026-06-12 Ancient Strander Cooldown Findings

- `SO_Enemy_AncientStrander.asset` has non-zero cooldown values: `_meleeAttackInterval: 1.8` and `_rangedAttackInterval: 2.4`.
- `AncientStranderBehaviorController.ChaseBehavior()` enters `MeleeAttack` by setting `_meleeAttackTimer = MeleeAttackInterval`, which intentionally makes the first contact attack immediate.
- `AncientStranderBehaviorController.NotifyDirectDamage(...)` always changes the enemy back to `Chase` when hit by direct player damage.
- With the current automatic Player attacking frequently, each direct hit can force `MeleeAttack -> Chase -> MeleeAttack`, and the state re-entry refills `_meleeAttackTimer`, bypassing the intended `1.8s` melee cooldown.
- The ranged bite path already uses absolute `_nextRangedAttackTime`, so its cooldown is less vulnerable to this specific state re-entry reset.
- Implemented fix: melee fishbone sweep now uses `_nextMeleeAttackTime`, written when `PerformMeleeAttack()` actually fires, so state re-entry cannot refresh the cooldown.
- The first valid melee contact can still attack immediately because `_nextMeleeAttackTime` starts at the default `0`, preserving the initial-contact behavior.
- Validation passed: `dotnet build Assembly-CSharp.csproj --no-restore /nologo /verbosity:minimal` completed with 0 warnings and 0 errors.

## 2026-06-12 Anchor Sentinel Health And Damage Findings

- `Pfb_Enemy_Common_AnchorSentinel.prefab` and `EnemyHealthController` both reference `SO_Enemy_AnchorSentinel.asset`, and the current `Scene_lyl_test` Anchor Sentinel instance is active with no scene override on health or beam damage.
- The current config asset has `_maxHealth: 100`, `_beamDamagePerSecond: 22`, `_beamTickInterval: 0.12`, and non-zero lock/firing/cooldown values, so the data itself was not zeroed.
- Player bullets previously handled `AnchorSentinelRuneWeakpoint` before normal `EnemyHealthController` damage and destroyed the bullet immediately. Hitting sentinel runes therefore advanced the puzzle but did not reduce sentinel HP, making max-health tuning appear ineffective during rune-focused tests.
- Solving all runes previously called `DisableSentinel()` directly, bypassing `EnemyHealthController.Die()` and ignoring the configured HP amount.
- The sentinel's dormant detection previously activated only when `WrongRuneImmediatelyActivates` was false. The current config sets it true, so in a normal proximity test the sentinel could stay dormant forever unless the player hit a wrong rune; that explains the missing `Energy Beam` damage logs.
- Implemented fix: bullet hits on sentinel runes now also apply normal elemental/player damage to the owning `EnemyHealthController` before notifying rune puzzle logic.
- Implemented fix: completing the rune sequence no longer disables a still-alive sentinel. If HP remains, the puzzle resets and the sentinel activates, so `Max Health` now determines whether rune hits are enough to destroy it.
- Implemented fix: dormant sentinel now activates when the player is within detection range, while wrong-rune activation still works.
- Validation passed: `dotnet build Assembly-CSharp.csproj --no-restore /nologo /verbosity:minimal` completed with 0 warnings and 0 errors.

## 2026-06-12 Anchor Sentinel Standard Enemy Conversion Findings

- Anchor Sentinel no longer uses the rune puzzle as a gameplay gate. Its behavior controller has no rune sequence, wrong-rune activation, rune reset, or puzzle-completion disable path.
- The three cube rune child objects were removed from `Pfb_Enemy_Common_AnchorSentinel.prefab`; the remaining prefab root keeps its solid `CapsuleCollider`, `AnchorSentinelBehaviorController`, `EnemyHealthController`, health bar canvas, eye origin, and visual child.
- Player bullets now use the normal enemy hit path only: resolve `EnemyHealthController` from the hit collider parent chain, apply elemental/player damage to that health controller, then destroy the bullet.
- Anchor Sentinel survivability is now controlled by `EnemyHealthController` plus `SO_Enemy_AnchorSentinel.asset` `_maxHealth`; changing max health affects direct body-damage tests.
- Anchor Sentinel attacks through detection only. When the player is inside `DetectionRange`, the sentinel enters its lock/fire/cooldown beam cycle without requiring any rune hit.
- Anchor Sentinel beam damage still uses `CombatDamageUtility.ApplyDamageTo()` against the current player damage receiver, so the current Agent player path is supported.
- Obsolete `AnchorSentinelRuneWeakpoint.cs` and its `.meta` were deleted, and the generated C# project no longer compiles that script.
- Validation passed: runtime and editor `dotnet build` completed with 0 warnings and 0 errors, and targeted scans found no remaining Anchor Sentinel rune puzzle references in scripts, config asset, prefab, or runtime project file.

## 2026-06-12 Player Defense Damage Mitigation Findings

- `SO_Agent_PawnConfig.asset` currently has `_defense: 100`, and `AgentPawnRoot` already exposes that config to decision/runtime facts, but it was not used by the actual incoming damage calculation.
- Enemy damage resolution prefers `PlayerHealthController` via `CombatDamageUtility.TryGetPlayerHealthReceiver(...)`, so most enemy attacks were subtracting from `PlayerHealthController.CurrentHealth`, not from `AgentPawnRoot._currentHealth`.
- `PlayerHealthController.TakeDamage(...)` only used the rune-pattern `_damageTakenMultiplier`; it did not read `AgentPawnRoot.Defense`, which explains why changing the player setting defense appeared to do nothing.
- `AgentPawnRoot.TakeCombatDamage(...)` also previously rounded and applied raw damage directly, so if future damage receiver resolution hit the Agent receiver first, defense would still be bypassed.
- Implemented fix: added a shared `CombatDamageUtility` mitigation formula `100 / (100 + Defense)` with the existing minimum-damage multiplier as a floor. Defense 0 means full damage; defense 100 means about half damage; high defense keeps scaling without making normal enemies deal 0 damage.
- Implemented fix: `PlayerHealthController` now caches/looks up same-object `AgentPawnRoot` and combines Agent defense mitigation with the existing rune-pattern defense multiplier before subtracting health.
- Implemented fix: `AgentPawnRoot` now exposes `Defense` and applies the same mitigation in both `TakeCombatDamage(...)` and `ApplyDamage(...)`.
- Validation passed: runtime/editor `dotnet build` completed with 0 warnings and 0 errors. Formula spot-check: with minimum multiplier `0.2`, raw 20 damage becomes 20 at defense 0, 10 at defense 100, and 4 at defense 400.

## 2026-06-14 Enemy VFX Integration Audit Findings

- Enemy-directed VFX scripts under `Assets/Art/VFX` are `MudTidalAberrationVfx`, `RobotAnchorBeamVfx`, `SkeFishboneAttackVfx`, `TracerAnchorVortexVfx`, and `ZombieTentacleCorrosionVfx`.
- `PlayerElementalSkillVfx` and `SimpleMagicRangedAttack` are mounted on `Assets/Prefabs/Character/Ai Idle.prefab` and are player/agent-style input preview scripts, not current enemy-system integrations.
- `ZombieTentacleCorrosionVfx` is mounted on `Assets/Prefabs/Character/Zombie Idle (2).prefab`, while the formal Modern Strander prefab `Assets/Prefabs/Enemy/Pawn/Common/Pfb_Enemy_Common_ModernStrander.prefab` uses the same `Zombie Idle (2).fbx` source but does not reference the VFX script.
- `SkeFishboneAttackVfx` is mounted on `Assets/Prefabs/Character/Ske Idle.prefab`, while the formal Ancient Strander prefab uses `Assets/Art/Animations/Ske Idle.fbx` but does not reference the VFX script.
- `RobotAnchorBeamVfx` is mounted on `Assets/Prefabs/Character/Robot.prefab`, while the formal Anchor Sentinel prefab uses the Robot/AI model source but does not reference the VFX script.
- `MudTidalAberrationVfx` is not mounted on the formal Tidal Aberration prefab; it appears only in obsolete scene content, while `Pfb_Enemy_Common_TidalAberration.prefab` still uses runtime `LineRenderer` placeholders for electric tentacle and water jet.
- `TracerAnchorVortexVfx` is already mounted on `Assets/Prefabs/Enemy/Pawn/Boss/Pfb_Enemy_HunterBoss.prefab`; `HunterBossBehaviorController` resolves it with `GetComponentInChildren<TracerAnchorVortexVfx>(true)` and calls `PlayMeleeSweep`, `PlayVortexAnchorCombo`, and `PlayAnchorThrow`.
- The unintegrated VFX scripts often include their own automatic target search, input preview, and damage/effect application. Proper enemy integration should disable or bypass duplicate auto gameplay and drive visuals from the existing enemy controller state transitions.

## 2026-06-14 Enemy VFX Integration Findings

- The integration keeps gameplay authority in the existing enemy controllers. VFX scripts now expose enemy-driven visual APIs that disable their auto target search, keyboard preview, and duplicated damage/effect application when controlled by enemies.
- Modern Strander now auto-adds or finds `ZombieTentacleCorrosionVfx`, plays the tentacle visual when `BeginTentacleStrike()` starts, and plays the slime pool visual when a latched attack ends. The existing `CorrosiveSlimePuddle` still owns real corrosion gameplay, but its simple cylinder visual is suppressed when the new VFX is present.
- Tidal Aberration now auto-adds or finds `MudTidalAberrationVfx`, playing mud tentacle contact on melee and high-pressure water jet on ranged attack. Existing controller damage, silence, knockback, and slow remain the source of truth.
- Ancient Strander now auto-adds or finds `SkeFishboneAttackVfx`, playing fishbone sweep for melee and fishbone bite for ranged strike. Existing overlap/hitbox damage remains the source of truth.
- Anchor Sentinel now auto-adds or finds `RobotAnchorBeamVfx`, playing the robot beam sequence when the sentinel enters each beam cycle. Existing beam damage ticks remain the source of truth.
- Hunter Boss was left unchanged because `TracerAnchorVortexVfx` was already integrated.

## 2026-06-14 Modern Strander VFX Correction Findings

- User reported that the Modern Strander VFX hookup made the attack animation look twitchy and no effect was visible.
- The initial integration disabled Modern Strander's old `LineRenderer` whenever a `ZombieTentacleCorrosionVfx` component existed. Because the formal enemy prefab does not serialize that VFX component, the runtime-added component could leave the attack with no visible fallback if the generated VFX did not render clearly.
- The artist sample prefab `Assets/Prefabs/Character/Zombie Idle (2).prefab` only serializes the `ZombieTentacleCorrosionVfx` script settings; it does not provide a separate reusable particle prefab. Runtime component hookup is therefore valid, but it must be driven from the formal enemy's `TentacleOrigin` and keep a fallback visible path.
- The current `Scene_lyl_test.unity` serialized ModernStrander prefab instance is overridden inactive (`m_IsActive: 0`), so scene-level no-VFX tests can also be affected by that state. The user may be testing a different scene or an unsaved active editor state because they observed attack animation playback.

## 2026-06-14 Modern Strander Attack Loop Findings

- User reported that Modern Strander repeatedly plays a raise/lower hand attack animation while trying to attack at melee range.
- The likely loop path is direct player damage during counterattack cooldown: `NotifyDirectDamage(...)` failed to counterattack, then forced `CurrentState = Chase`; the next `ChaseBehavior(...)` saw the player inside `AttackRange`, re-entered `Attack`, and reset the old `_attackTimer` to `AttackInterval`, causing an immediate new `BeginTentacleStrike()` and Animator `Attack` trigger.
- This bypassed the intended 5-second tentacle cooldown whenever the player's frequent automatic attacks kept notifying direct damage.
- The fix should make tentacle readiness an absolute next-ready time, keep close-range direct-damage reactions in `Attack` without retriggering, and add a small hold-range buffer so tiny distance oscillations do not bounce the enemy between `Chase` and `Attack`.

## 2026-06-14 Modern Strander Combat Distance Findings

- User clarified that normal walking already has the hands raised, so the visible raise/lower loop likely comes from state switching rather than the attack clip itself.
- Remaining risky path: `Update`, close-range direct-damage handling, direct counterattack range checks, and immediate latch checks still used root-to-root `Vector3.Distance(transform.position, PlayerTransform.position)`.
- For the current Agent/player setup, the visible mesh, root transform, NavMeshAgent, and body capsule can disagree enough that a visually close target may be treated as outside attack hold range, pushing ModernStrander back to `Chase`.
- The correction uses planar closest-point distance between ModernStrander's body collider and the cached player body collider for Chase/Attack switching, direct counterattack gating, and latch checks, so state changes and hit resolution share the same physical distance.

## 2026-06-14 Enemy Scale Normalization Findings

- Measured the current Agent player and formal enemy prefabs. The player root is `3,3,3`, and its renderer height is about `10.22`.
- Ancient Strander was effectively about twice the player height before the change. I normalized `Pfb_Enemy_Common_AncientStrander.prefab` root scale from `3` to `1.5`, which brings its world size back in line with the player baseline.
- Modern Strander was also oversized at the root. I normalized `Pfb_Enemy_Common_ModernStrander.prefab` root scale from `6` to `3` so its body/VFX placement matches the same player-sized baseline.
- Ancient Strander already drives its fishbone attack through `MeleeOrigin`, `BiteOrigin`, and `SkeFishboneAttackVfx` on the prefab hierarchy, so the root-scale correction realigns animation and VFX without changing controller code. This is an inference from the current wiring.
- Tidal Aberration, Anchor Sentinel, and Hunter Boss were left unchanged because their current sizes are either intentional boss proportions or already closer to the player baseline.

## 2026-06-15 Code Ownership Summary Findings

- Current Git identity is `Lin <392960892@qq.com>`.
- Commit count on `origin/dev` by this author: `30` total, `24` excluding merge commits.
- Repo-wide history by this author across all refs: `33` total, `26` excluding merge commits.
- Directly related code for the requested scope totals `145` tracked source/tool files and `41,645` physical lines, excluding `.meta` files, `Assets/Scripts/Obsolete/BoardGame/**`, planning notes, and unrelated ThirdParty assets.
- Main active code folders for the scope are `Assets/Scripts/Gameplay/Enemy`, `Assets/Scripts/Gameplay/Backpack`, `Assets/Scripts/Gameplay/Raid`, `Assets/Scripts/Gameplay/Agent/Animation`, `Assets/Scripts/Gameplay/Agent/AI`, `Assets/Scripts/Core/BehaviorTree`, `Assets/Scripts/Core/StateMachine`, `Assets/Scripts/Gameplay/Visual Effect`, `Assets/Art/VFX`, `Assets/Scripts/Gameplay/SkillEffect`, `Assets/Scripts/Editor`, and `tools`.
- No dedicated AnimatorController generator was found; the animation/state-machine side is implemented through runtime Animator bridges plus the Agent state-machine / behavior-tree architecture.

## 2026-06-17 Storage Warehouse UI Findings

- The current backpack UI prefab is `Assets/Prefabs/Canvas.prefab`.
- The existing backpack/loot screen already uses `InventoryScreenSessionContext` to represent a left player inventory plus a right external container.
- `LootBoxEntity.CreateInventorySessionContext()` is the existing right-panel container entry point, and `InventoryScreenController.OpenInventorySession(...)` loads that context into `LootChestGrid`.
- Quick transfer currently routes between `LootChestGrid` and `BackpackGrid`, so reusing `LootChestGrid` for the warehouse preserves current drag, stack, swap, and double-click behavior.
- No general persistent save service was found for backpack containers. Existing loot boxes keep runtime snapshots on scene objects, and character inventory snapshots are held in memory by `InventoryScreenController`.
- `InventoryItemData` has a stable `ItemID`, but the runtime project does not yet expose a reusable `ItemID -> InventoryItemData` resolver. Warehouse persistence should add one before writing JSON.
- Implemented storage session keeps the left backpack route unchanged and treats the right warehouse as `InventoryExternalContainerKind.Storage`, so future storage-only restrictions can branch on the session kind without changing ordinary loot boxes.
- `StorageCanvas.prefab` is an asset-level copy with its own `.meta` GUID and no nested prefab instance records. Editing this prefab will not mutate `Canvas.prefab`.
- `StorageCanvasTest.unity` intentionally instantiates `StorageCanvas.prefab`, providing the requested independent scene test surface.
- Storage persistence writes `player_storage.json` under `Application.persistentDataPath`, keyed by agent id with `default_player` fallback. It uses `InventoryItemDatabase` in `Resources/Inventory` to restore item references by `ItemID`.
- Storage items are sanitized on save/load so `RequiresSearch=false`, `IsSearched=true`, and search timers are zeroed. This removes the loot search flow from the warehouse while preserving the shared item UI and drag behavior.
- The storage grows by maintaining at least one trailing blank page after the configured minimum 10 pages. When the last page receives items, the service appends a new empty page.
- The warehouse sort button sorts only the currently loaded page because each page is loaded into `StorageGrid` independently before `InventoryUIController.AutoSort()` runs.
- Unity edit-mode rebuild warnings from grid-cell destruction were fixed by switching to `DestroyImmediate` when not playing. Runtime grid rebuilds still use `Destroy`.
- Unity hot-reload serialization warnings from recursive storage save records were fixed by marking the service's runtime JSON cache fields as `[NonSerialized]`.
- Follow-up play-mode report showed three UI issues: the left backpack grid could be blank after opening the storage scene, the warehouse page shape needed to be 6 columns by 10 rows, and page buttons were receiving clicks while not rendering visibly.
- The backpack grid issue was consistent with an external-session opening order that did not force the linked backpack grid/background cells to refresh after the storage session was projected into the UI.
- The page selector issue was consistent with copied scroll viewport masking/graphics from the original canvas hiding generated children. Runtime repair now disables the old `Mask`, adds `RectMask2D`, restores the scroll background alpha, and normalizes generated button visuals.
- Warehouse save records do not need recursive container contents because container-with-contents items are out of scope for this storage flow. Flattening records also avoids recursive `JsonUtility` depth warnings.
- The second follow-up showed the backpack grid could still be logically present but visually blank. The safer fix is to refresh the 5x6 background cells unconditionally when showing the warehouse instead of relying on `NeedsBackgroundCellRefresh()`.
- The selector overflow came from anchoring the storage panel near the screen edge and then anchoring an 86px-wide selector to the panel's right side. A single-column 42px selector plus a larger right inset keeps the page buttons visible without changing the warehouse grid size.

## 2026-06-17 Totem Shop UI Findings

- The shop can reuse `PlayerStorageService` for warehouse persistence without changing ordinary storage behavior.
- No existing gold/currency service was found in active gameplay scripts, so the shop needs a new per-agent economy save service.
- `InventoryItemData.SellPrice` already exists and is used by run-revenue calculations. Values `<= 0` should be treated as missing for shop fallback pricing.
- Totem items are identified by `InventoryItemData.EquipmentKind == EquipmentSlotKind.Totem`; current table totems are green, blue, and gold equipment entries.
- Current totem dimensions are `1x2`. The shop's 4x4 right grid should therefore follow normal placement and show at most eight items, preserving the user-confirmed real-grid rule.
- `DraggableItemUI.OnPointerClick` only handles `Ctrl+LeftClick` quick transfer by default, so shop-specific normal-left-click behavior can be layered through separate click-catcher components instead of altering the existing item script.
- `ShopCanvas.prefab` is an asset-level copy with its own prefab GUID and no nested prefab instance records. `ShopCanvasTest.unity` references the new shop prefab GUID and does not reference the original `Canvas.prefab` or `StorageCanvas.prefab` GUIDs.
- The shop uses a small opt-in grid interaction policy and an existing placement-policy hook so the right shop grid cannot be used as a drag source or drop target, while ordinary inventory/storage grids remain unaffected unless they explicitly add the policy components.

## 2026-06-17 Extraction Storage Settlement Findings

- The settled inventory definition is backpack contents plus the six equipment slots: head, body, face, headphone, totem A, and totem B.
- Default backpack, backpack item body, rig/chest-rig body, and rig contents are excluded from settlement.
- Extracted agents settle immediately when their extraction timer completes, before the pawn is destroyed. Later mission failure does not roll back already settled agents.
- Failed or dead agents discard the same extractable inventory without writing it to storage.
- Settlement writes to the shared `default_player` warehouse and can create a hidden runtime `PlayerStorageService` when no storage UI service exists in the raid scene.

## 2026-06-17 Out-of-Raid Totem Expansion Planning Findings

- Designer workbook `C:/Users/Lenovo/Downloads/局外图腾系统.xlsx` contains one sheet with six base totems: 生命, 狙击, 冰霜, 地鸣, 进击, 轻盈. Columns are `图腾`, `具体作用`, `1级图腾数值`, `2级图腾数值`, and `3级图腾数值`.
- User intent: level 1/2/3 correspond to the currently existing green/blue/gold totem quality baselines, producing 6 base totems x 3 qualities = 18 totem items.
- Existing totems are `equip_totem_green`, `equip_totem_blue`, and `equip_totem_gold` in `Assets/SO/ItemData/Table`, with `Type=Equipment`, `EquipmentKind=Totem`, dimensions `1x2`, non-stackable, carry weight `1`, search enabled, `MagicUnlock=None`, `RunePatternPoints=1`, and sell prices `100/300/800`.
- Existing color baselines map to rarities `Uncommon`, `Rare`, and `Legendary`, not Common/Rare/Epic. Item icon references are generic rarity sprites: Uncommon, Rare, Legendary.
- `InventoryItemData` currently has no structured totem effect fields. It only stores generic item/equipment data, economy, carry weight, search data, and magic unlock data.
- `LootItemTsvImporterWindow` can import generic item fields and an optional `CarryWeight` column, but currently cannot import totem effect kind/value/secondary penalties.
- `WorldPrefab` should not be blindly copied from the three baseline totems for new IDs because existing world pickup prefabs serialize `WorldLootItem.ItemData` to the specific old item asset.
- `InventoryItemInfoPanelController` currently does not display totem effect text, so generated effect data would need UI display support if players should understand each totem.
- Totem shop generation repopulates `TotemShopPool` from every `InventoryItemData` with `EquipmentKind=Totem` and defaults buy price to `SellPrice * 2`; old generic totems must be deliberately retired or they will remain shop candidates.
- Equipped totem snapshots are currently captured in `InventoryScreenController` for UI persistence, extraction settlement, and discard logic, but no active bridge was found that applies equipped item modifiers into `AgentPawnRoot` or `AgentCombatController`.
- User confirmed the old generic green/blue/gold totem assets should remain on disk for reference safety but be removed from active TSV/database/shop output.
- User confirmed all three quality variants of a base totem should share the same player-facing name.
- User confirmed composite effects: 生命 level 3 = max health +30% and move speed -5%; 进击 level 3 = staff/normal attack damage +40% and range -10%; same-effect equipped totems may stack.
- User confirmed gold data quality should still use `Legendary`. Current item UI background tint maps `Legendary` to red, while the Bag sprite set has green/blue/gold visual assets as `S_ItemIcon_Rarity_Common`, `S_ItemIcon_Rarity_Uncommon`, and `S_ItemIcon_Rarity_Epic`; implementation should separate gameplay rarity from totem quality visuals if gold totems must look gold.
- Implementation uses separate fields for gameplay/data rarity and visual totem quality. The green/blue/gold variants keep the old rarity/economy baselines `Uncommon/100`, `Rare/300`, and `Legendary/800`, while their item backgrounds use `S_ItemIcon_Rarity_Common`, `S_ItemIcon_Rarity_Uncommon`, and `S_ItemIcon_Rarity_Epic`.
- The old generic totem assets remain in `Assets/SO/ItemData/Table`, but `IncludeInRuntimeDatabase=0` and `IncludeInTotemShop=0` prevent them from entering generated runtime database or shop output.
- Runtime totem effects are additive across the two equipped totem slots. `InventoryScreenController` builds a `TotemModifierSet` from live equipment slots or saved agent snapshots; `AgentPawnRoot` refreshes it periodically and passes it into `AgentCombatController`.
- Current runtime effect coverage includes max health, move speed, attack range, target discovery range, normal/staff attack damage, ice skill damage, and earth skill damage. Effects not represented by these modifier types would need additional enum/runtime handling before designers add them to TSV.

## 2026-06-17 Element Selection Menu Art Findings

- User request: update `Scene_ElementSelectionMenu` with separated UI art from `Png_Item_attribute selection`, using `Png_Item_ background` for the scene background, `Png_Item_ button` for attribute buttons, `Png_Item_ fire` and similarly named sprites for element icons, `Png_Item_ panel` for the right rounded panel, `Font/Title` for right-panel text, and `supplement/IMG_0826` plus `Font/Text` for the center start button.
- The existing scene was a minimal Canvas that displayed only the old composite `IMG_0828` background through sprite GUID `e61f6a20073056f429751081f2c2dd6c`.
- The replacement asset GUIDs are: background `0d1f662e7823d43c7b13401e05a6b7eb`, button `d772d1f6dc373483598e05c312e11f1e`, panel `aa082b7802c524bd3bb051fd2db89eb4`, fire `3118f45a9e30d4e65b52006c1b5d2abc`, ice `f628dd4105cd94e81a54a9672e1b74f8`, earth `b89f63428688e4c71991b2c3a915d113`, water `5b8fc751a3b54450190d3da3e45267a6`, metal `7d85d537512e64668ba4f5c337c78a43`, start background `ec8fe7059c114ce4ca89b7d3a7460720`, Title font `59cd6b90a673496449300d960d81089c`, and Text font `522a197589a7c2d438a889334d081de7`.
- The `Png_Item_ button` sprite is a 1920x1080 transparent layer whose visible button is located at the original Fire-row position. Reusing it for the other four rows requires full-canvas duplicated Images with vertical offsets.
- Unity batchmode could not rebuild the scene because another Unity instance already had this project open, so the scene YAML was updated directly while keeping the same hierarchy and references generated by the editor builder.

## 2026-06-17 Shop Return / Background Findings

- The independent shop UI is still driven by `Assets/Prefabs/ShopCanvas.prefab`, `Assets/Scenes/ShopCanvasTest.unity`, `Assets/Scripts/Gameplay/Backpack/ShopScreenController.cs`, and `Assets/Scripts/Editor/ShopCanvasPrefabBuilder.cs`.
- The requested return button art exists at `Assets/Art/Sprites/UI/Button/IMG_0494.PNG` with sprite GUID `2e9b38a3b2076e44aadbb83e41faa772`.
- Existing scene navigation uses simple `SceneManager.LoadScene(...)` calls in menu/preparation controllers; a small reusable button component can keep the shop button independent from the existing shop/storage controllers.
- `ShopCanvasTest.unity` currently uses the same dark camera clear color as the earlier test UI scenes, so the preparation interface background must be inspected before syncing `StorageCanvasTest`.
- `Scene_PreparationInterface.unity` uses camera clear color `{0.54, 0.72, 0.82, 1}` and a full-canvas background sprite at `Assets/Art/Sprites/Png_Item_preparation interface/Png_Item_background.PNG` with GUID `9eeecfe495e0d4ced9a0ba4f67d8d57d`.
- `StorageCanvasTest.unity` now has a low sorting-order `PreparationBackgroundCanvas` referencing the same background sprite, plus the same camera clear color. This keeps the storage UI prefab independent while matching the preparation interface background in the test scene.
- `Scene_PreparationInterface.unity`, `Scene_ElementSelectionMenu.unity`, and `ShopCanvasTest.unity` are already present in `ProjectSettings/EditorBuildSettings.asset`, so the shop return button can use `SceneManager.LoadScene("Scene_PreparationInterface")`.
