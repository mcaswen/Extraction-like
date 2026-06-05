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
