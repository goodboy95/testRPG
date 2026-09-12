# Dialogue Manager v3.10.4

Pinned upstream tag: https://github.com/nathanhoad/godot_dialogue_manager/tree/v3.10.4

License: MIT (see LICENSE). Vendored on 2026-09-07.

Source archive SHA-256: `3b31f7b23655b98cb6ef943fe531a92a9c7e53c12898cf0c25566ba577c971a7`.

Only upstream addons/dialogue_manager is included. Runtime story flow uses the plugin; the application atomically commits the validated event completion after the final line.

Local compatibility patch: `utilities/dialogue_cache.gd` respects `.gdignore` during directory scanning. Archived builds in `work/` and `outputs/` otherwise register duplicate static IDs. No dialogue syntax or runtime behavior changes. The unused upstream C# example balloon is excluded from the game assembly because implicit usings make its `Timer` ambiguous; the game uses its existing `DialoguePanel`.

`DialogueManager.cs` also uses explicit reflection casts, nullable guards and an unnamed catch for the project's nullable-enabled C# build. These changes preserve v3.10.4 behavior and avoid suppressing application warnings.

`dialogue_manager.gd` shallow-copies the line dictionary before assigning its runtime `resource` owner. Upstream assigns the resource into its own cached `lines` dictionary, creating a reference cycle (all 13 played resources remained live at exit). Copying the working dictionary keeps traversal data intact while allowing completed/cancelled resources to release. This is a local lifecycle fix, not a plugin version upgrade.
