# Terrain3D vendor record

Pinned 1.0.2-stable, unmodified upstream addon from the official release package. License: [MIT](LICENSE.txt).

Only the terrain sample loads a Terrain3D node. Godot remains 4.6.3 .NET with Compatibility. The upstream editor uses GDScript; gameplay integration is C# via GDExtension calls. The demo is retained only under ignored work/ and is not installed into the game.

Release hash and Windows binary hashes: [sample provenance](../../assets/generated/village/terrain-sample-v1/provenance.json).

Runtime sample sets free_editor_textures=false to avoid releasing/reloading pathless resources during authoring or cached scene transitions. The upstream native binary emits one instance_reset_physics_interpolation deprecation warning on Godot 4.6.3; no binary patch or engine upgrade is applied.
