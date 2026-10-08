# Late Binding via Addressables

Atlases are excluded from the player (`includeInBuild = false`), registered as Addressables, and loaded on demand when a sprite asks for them.

## When

DLC, seasonal content, per-level art (many levels), skins/cosmetics, localized sprites: anything not needed at startup.

## Requirements (generate all of them)

1. **Package**: `com.unity.addressables` installed and initialized (Window > Asset Management > Addressables > Groups > Create Addressables Settings). Install with `unity-package-management`.
2. **Define `UNITY_ADDRESSABLES`** so the shipped scripts compile their Addressables paths. Nothing defines it automatically. Either:
   - asmdef Version Defines (preferred), in both the editor and runtime asmdefs:
     ```json
     "versionDefines": [
       { "name": "com.unity.addressables", "expression": "1.0.0", "define": "UNITY_ADDRESSABLES" }
     ]
     ```
     Editor asmdef references `Unity.Addressables`, `Unity.Addressables.Editor`, `Unity.ResourceManager`; runtime asmdef references `Unity.Addressables`, `Unity.ResourceManager`.
   - or Project Settings > Player > Scripting Define Symbols (affects every assembly, only for the targets you set).
3. **Prebuild generator** ([../scripts/SpriteAtlasPrebuildGenerator.cs](../scripts/SpriteAtlasPrebuildGenerator.cs)) with `k_UseAddressables = true`: generates atlases with `includeInBuild = false`, creates/updates an Addressables entry per atlas with the atlas file name as address, and sets `BuildAddressablesWithPlayerBuild = BuildWithPlayer` so content is built as part of the player build.
4. **Runtime loader** ([../scripts/SpriteAtlasLateBinding.cs](../scripts/SpriteAtlasLateBinding.cs)): registers `SpriteAtlasManager.atlasRequested` before the first scene loads, loads `Addressables.LoadAssetAsync<SpriteAtlas>(tag)`, de-duplicates in-flight requests, exposes `Preload`, `Release`, `ReleaseAll`. No scene object needed.

## Addresses

`atlasRequested` passes the atlas **tag**, which is the atlas asset name. The generator uses the file name without extension as address, so `UI.spriteatlasv2` -> address `UI` -> tag `UI`. If you rename addresses, change `OnAtlasRequested` to map tags to addresses.

## Building content

- Default: `BuildAddressablesWithPlayerBuild = BuildWithPlayer` (Addressables Settings > Build > Build Addressables on Player Build). Addressables' own preprocessor builds content after the atlas generator (callbackOrder -100) has run.
- CI with remote content: run `SpriteAtlasPrebuildGenerator.GenerateAll()`, then `AddressableAssetSettings.BuildPlayerContent()`, then `BuildPipeline.BuildPlayer`. Do not build Addressables in `IPostprocessBuildWithReport`; local content must exist before the player is assembled.

## Notes

- Sprites that are themselves Addressable and reference an Addressable atlas are resolved by Addressables automatically; late binding matters for sprites referenced from scenes and prefabs built into the player.
- Until the callback delivers the atlas, affected sprites render invisible. Preload atlases for the next screen during loading screens with `SpriteAtlasLateBinding.Preload(address)`.
- Release handles when leaving content (`Release(address)`); releasing an atlas that sprites still use makes them disappear.
- Keep each sprite in exactly one atlas.
