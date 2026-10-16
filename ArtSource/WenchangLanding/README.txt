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
