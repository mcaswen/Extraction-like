Wenchang-01 emergency landing game model

Wenchang01_EmergencyLanding.blend is the editable Blender 4.2 game derivative.
Only WC01_LANDING_LOD0 is visible by default; LOD1 and LOD2 are hidden collections.
The file includes studio camera/lighting, Chinese geometry markings, flag geometry,
the LandingPaint vertex-color attribute and named foot/smoke attachment empties.
It is intentionally outside Assets to avoid Unity requiring Blender on teammates' machines.

Unity uses the three explicit FBX exports in Assets/Art/Environment/WenchangLanding/Models.
They share the same pivot. Blender +Z becomes Unity +Y; the camera-facing side is
chosen by the scene builder. Do not clear import rotations or rescale an individual LOD.
The Unity particle effects and terrain-conforming contact berms belong to the site prefab,
not this model-only Blender file.

Model reduction recipe: tools/art/build_wenchang_landing.py
It takes --source <intact master.blend> --out <separate directory>.
Never point --out at the intact source directory.
The original intact master remains unchanged and is not replaced by this game derivative.
Source master SHA256: ee7a98b5609b1d747bdeb42510375d8978d6e00032dbf9f8bbe4618a49e03c61
Export budgets and material settings are recorded in model-validation.json.

This is the current editable production source, not a discarded alternate or backup.

Game-camera polish (2026-10-17)
- Reinforced fork-type landing legs, hydraulic dampers, trunnions and layered footpads.
- Foot anchor coordinates and bottom contact plane are unchanged; all LODs are
  constrained to the LOD0 occupied envelope to prevent decimator overshoot.
- Broader service spines, shoulder equipment covers and lower graphite heat-shield petals.
- One attached folded service cover exposes conduits, a valve and a small amber lens.
- Ivory coating, matte graphite, subdued orange and cool metal use distinct roughness.
- LandingPaint alpha is now an emission MASK (zero except the small amber lens),
  not opacity. All vehicle materials remain opaque.
- Unity's local DepthNormals pass softens only this vehicle's small panel edges;
  its forward lighting normals, actual depth and global outline settings are retained.
LOD0 / LOD1 / LOD2: 143770 / 74774 / 46485 triangles; 10 renderers per LOD.
No Painter, external texture pack, runtime model generator or new dependency is required.

Verification
- 98/99 selected regression checks passed. The one existing failure is
  BaseAndSurveyRouteSafetyTests.FootprintsAvoidRoutesInteractionSpaceEnemySpawnsTreesAndExistingEquipment
  (Guide_03_Dragonbone_Approach). It reproduces with the pre-polish assets;
  this change does not move that route sign or alter the placement-check rules.
- Both Agents' 1512 navigation probes and exact NavMesh geometry/path-corner hashes
  match the pre-polish baseline (including the same 1308 complete baseline paths).
- Formal scene bytes, terrain bytes, original non-vehicle objects, vehicle transform,
  height and all four foot contact points are unchanged. No occupied-envelope expansion.
- Isolated formal-scene Play-mode validation verifies both Agents on the NavMesh,
  live smoke, day/night rendering and all four compiled vehicle shader passes.
- No full combat replay, standalone Windows build or frame-time benchmark is claimed.
