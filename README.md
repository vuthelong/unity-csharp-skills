# Unity C# Skills

[Agent Skills](https://docs.claude.com/en/docs/claude-code/skills) for **Unity 6 (6000.x)** and C#, packaged as a Claude Code plugin marketplace: 88 skills in 11 plugins.

## Install

```
/plugin marketplace add vuthelong/unity-csharp-skills
/plugin install unity-csharp-core@unity-csharp-skills
```

Install other plugins the same way (`/plugin install <plugin>@unity-csharp-skills`). For another agent, copy a skill folder (`plugins/<category>/skills/<skill>/`) into its skills directory, e.g. `~/.claude/skills/`.

> **Do not install alongside Unity's official `unity` plugin.** Many skills here are modified versions of [Unity-Technologies/skills](https://github.com/Unity-Technologies/skills) with the same names (`unity-cli`, `optimize-audio`, …). Two copies make one request match two different skills. Also remove loose copies from `~/.claude/skills/`.

## Plugins

Start with `unity-csharp-core`; add the others for the areas your project uses. \* = Unity Companion License.

| Plugin | Covers | Skills |
|---|---|---|
| `unity-csharp-core` | C# and Unity coding standards, code review, codebase audit, editor safety | [`audit-unity-codebase`](plugins/csharp-core/skills/audit-unity-codebase/SKILL.md), [`csharp-unity`](plugins/csharp-core/skills/csharp-unity/SKILL.md), [`review-unity-csharp`](plugins/csharp-core/skills/review-unity-csharp/SKILL.md), [`unity-editor-safety`](plugins/csharp-core/skills/unity-editor-safety/SKILL.md) |
| `unity-editor-tooling` | Unity CLI, packages, project bootstrap, search, auditing, builds | [`asset-transformer-toolkit`](plugins/editor-tooling/skills/asset-transformer-toolkit/SKILL.md)\*, [`build-gtk`](plugins/editor-tooling/skills/build-gtk/SKILL.md)\*, [`generate-editor-search-query`](plugins/editor-tooling/skills/generate-editor-search-query/SKILL.md)\*, [`new-unity-project`](plugins/editor-tooling/skills/new-unity-project/SKILL.md)\*, [`project-auditor-fixes`](plugins/editor-tooling/skills/project-auditor-fixes/SKILL.md)\*, [`unity-cli`](plugins/editor-tooling/skills/unity-cli/SKILL.md)\*, [`unity-package-management`](plugins/editor-tooling/skills/unity-package-management/SKILL.md)\* |
| `unity-rendering-vfx` | URP, Render Graph, post-processing, Shader Graph, HLSL shaders, VFX Graph, lighting, day-night and weather | [`day-night-weather`](plugins/rendering-vfx/skills/day-night-weather/SKILL.md), [`migrate-birp-to-urp`](plugins/rendering-vfx/skills/migrate-birp-to-urp/SKILL.md)\*, [`optimize-3d-rendering`](plugins/rendering-vfx/skills/optimize-3d-rendering/SKILL.md), [`shader-graph-create-custom-node`](plugins/rendering-vfx/skills/shader-graph-create-custom-node/SKILL.md)\*, [`unity-lighting`](plugins/rendering-vfx/skills/unity-lighting/SKILL.md), [`unity-particle-system`](plugins/rendering-vfx/skills/unity-particle-system/SKILL.md), [`unity-texture-import`](plugins/rendering-vfx/skills/unity-texture-import/SKILL.md), [`unity-vfx-graph`](plugins/rendering-vfx/skills/unity-vfx-graph/SKILL.md), [`urp-hlsl-shaders`](plugins/rendering-vfx/skills/urp-hlsl-shaders/SKILL.md), [`urp-postprocessing`](plugins/rendering-vfx/skills/urp-postprocessing/SKILL.md)\*, [`validate-urp-render-graph-renderer-feature`](plugins/rendering-vfx/skills/validate-urp-render-graph-renderer-feature/SKILL.md)\* |
| `unity-ui` | UI Toolkit, uGUI, IMGUI, TextMeshPro, localization, screen navigation, PC settings menu, accessibility | [`accessibility`](plugins/ui/skills/accessibility/SKILL.md), [`localization`](plugins/ui/skills/localization/SKILL.md)\*, [`optimize-text-mesh-pro`](plugins/ui/skills/optimize-text-mesh-pro/SKILL.md)\*, [`pc-settings-menu`](plugins/ui/skills/pc-settings-menu/SKILL.md), [`screen-navigator`](plugins/ui/skills/screen-navigator/SKILL.md), [`ui-imgui`](plugins/ui/skills/ui-imgui/SKILL.md)\*, [`ui-ugui`](plugins/ui/skills/ui-ugui/SKILL.md)\*, [`ui-uitk`](plugins/ui/skills/ui-uitk/SKILL.md)\*, [`ui`](plugins/ui/skills/ui/SKILL.md)\* |
| `unity-2d` | Pixel perfect, sprites, sprite atlases, tilemaps and rule tiles | [`2d-pixel-perfect`](plugins/2d/skills/2d-pixel-perfect/SKILL.md)\*, [`manage-sprite-atlas`](plugins/2d/skills/manage-sprite-atlas/SKILL.md)\*, [`sprite-editor`](plugins/2d/skills/sprite-editor/SKILL.md)\*, [`tilemap-authoring`](plugins/2d/skills/tilemap-authoring/SKILL.md)\* |
| `unity-gameplay-systems` | Physics, navigation, animation, cameras, input, terrain and world building, splines, save system, boot flow, interaction, enemy AI, combat | [`animation-authoring`](plugins/gameplay-systems/skills/animation-authoring/SKILL.md), [`character-controller-3d`](plugins/gameplay-systems/skills/character-controller-3d/SKILL.md), [`cinemachine-cameras`](plugins/gameplay-systems/skills/cinemachine-cameras/SKILL.md), [`combat-damage-system`](plugins/gameplay-systems/skills/combat-damage-system/SKILL.md), [`enemy-ai-behavior`](plugins/gameplay-systems/skills/enemy-ai-behavior/SKILL.md), [`game-bootstrap-flow`](plugins/gameplay-systems/skills/game-bootstrap-flow/SKILL.md), [`initialize-ai-navigation`](plugins/gameplay-systems/skills/initialize-ai-navigation/SKILL.md)\*, [`input-system-actions`](plugins/gameplay-systems/skills/input-system-actions/SKILL.md), [`interaction-system`](plugins/gameplay-systems/skills/interaction-system/SKILL.md), [`level-geometry-authoring`](plugins/gameplay-systems/skills/level-geometry-authoring/SKILL.md), [`physics-3d-collision`](plugins/gameplay-systems/skills/physics-3d-collision/SKILL.md)\*, [`save-system`](plugins/gameplay-systems/skills/save-system/SKILL.md), [`splines-paths`](plugins/gameplay-systems/skills/splines-paths/SKILL.md), [`timeline-sequencing`](plugins/gameplay-systems/skills/timeline-sequencing/SKILL.md) |
| `unity-audio-performance` | Audio import and mixers, web build optimization, memory and CPU profiling | [`audio-playback-system`](plugins/audio-performance/skills/audio-playback-system/SKILL.md), [`audio-setup-mixers`](plugins/audio-performance/skills/audio-setup-mixers/SKILL.md)\*, [`cpu-performance-profiling`](plugins/audio-performance/skills/cpu-performance-profiling/SKILL.md), [`fmod-unity`](plugins/audio-performance/skills/fmod-unity/SKILL.md), [`memory-snapshot-profiling`](plugins/audio-performance/skills/memory-snapshot-profiling/SKILL.md), [`optimize-audio`](plugins/audio-performance/skills/optimize-audio/SKILL.md)\*, [`optimize-web`](plugins/audio-performance/skills/optimize-web/SKILL.md)\* |
| `unity-services-monetization` | Unity Gaming Services, Netcode for GameObjects, network debugging, multiplayer, Vivox, IAP, LevelPlay ads | [`build-live-game`](plugins/services-monetization/skills/build-live-game/SKILL.md)\*, [`implement-in-app-purchases`](plugins/services-monetization/skills/implement-in-app-purchases/SKILL.md)\*, [`levelplay-unity-integration`](plugins/services-monetization/skills/levelplay-unity-integration/SKILL.md)\*, [`netcode-for-gameobjects`](plugins/services-monetization/skills/netcode-for-gameobjects/SKILL.md), [`network-debugging`](plugins/services-monetization/skills/network-debugging/SKILL.md), [`setup-multiplayer-services`](plugins/services-monetization/skills/setup-multiplayer-services/SKILL.md)\*, [`setup-vivox-voice-chat`](plugins/services-monetization/skills/setup-vivox-voice-chat/SKILL.md)\* |
| `unity-libraries` | UniTask, R3, ObservableCollections, MessagePipe, VContainer, ZString, ZLogger, MemoryPack, MasterMemory, NativeMemoryArray, MessagePack, MagicOnion, UIEffect, Animation Sequencer, ZBase CSV Reader, PubSub, Pooling | [`animation-sequencer`](plugins/libraries/skills/animation-sequencer/SKILL.md), [`magiconion`](plugins/libraries/skills/magiconion/SKILL.md), [`mastermemory`](plugins/libraries/skills/mastermemory/SKILL.md), [`memorypack`](plugins/libraries/skills/memorypack/SKILL.md), [`messagepack-csharp`](plugins/libraries/skills/messagepack-csharp/SKILL.md), [`messagepipe`](plugins/libraries/skills/messagepipe/SKILL.md), [`native-memory-array`](plugins/libraries/skills/native-memory-array/SKILL.md), [`observable-collections`](plugins/libraries/skills/observable-collections/SKILL.md), [`r3`](plugins/libraries/skills/r3/SKILL.md), [`ui-effect`](plugins/libraries/skills/ui-effect/SKILL.md), [`unitask`](plugins/libraries/skills/unitask/SKILL.md), [`vcontainer`](plugins/libraries/skills/vcontainer/SKILL.md), [`zbase-csv-reader`](plugins/libraries/skills/zbase-csv-reader/SKILL.md), [`zbase-pooling`](plugins/libraries/skills/zbase-pooling/SKILL.md), [`zbase-pubsub`](plugins/libraries/skills/zbase-pubsub/SKILL.md), [`zlogger`](plugins/libraries/skills/zlogger/SKILL.md), [`zstring`](plugins/libraries/skills/zstring/SKILL.md) |
| `unity-asset-pipeline` | Blender to Unity import, Spine runtime, Addressables, CSV to ScriptableObject game data pipeline | [`addressables-asset-loading`](plugins/asset-pipeline/skills/addressables-asset-loading/SKILL.md), [`blender-to-unity-pipeline`](plugins/asset-pipeline/skills/blender-to-unity-pipeline/SKILL.md), [`game-data-pipeline`](plugins/asset-pipeline/skills/game-data-pipeline/SKILL.md), [`spine-unity`](plugins/asset-pipeline/skills/spine-unity/SKILL.md) |
| `unity-pc-steam` | Steamworks.NET, Steam lobbies and P2P co-op, SteamPipe builds and uploads, Steam Deck and controllers | [`steam-build-release`](plugins/pc-steam/skills/steam-build-release/SKILL.md), [`steam-deck-controller`](plugins/pc-steam/skills/steam-deck-controller/SKILL.md), [`steam-lobbies-p2p`](plugins/pc-steam/skills/steam-lobbies-p2p/SKILL.md), [`steamworks-integration`](plugins/pc-steam/skills/steamworks-integration/SKILL.md) |

## Third-party SDKs

These skills target software installed separately under its own license; nothing is bundled.

| SDK / tool | License | Used by |
|---|---|---|
| [UniTask](https://github.com/Cysharp/UniTask) | MIT | `unitask` |
| [R3](https://github.com/Cysharp/R3) | MIT | `r3` |
| [ObservableCollections](https://github.com/Cysharp/ObservableCollections) | MIT | `observable-collections` |
| [MessagePipe](https://github.com/Cysharp/MessagePipe) | MIT | `messagepipe` |
| [ZString](https://github.com/Cysharp/ZString) | MIT | `zstring` |
| [ZLogger](https://github.com/Cysharp/ZLogger) | MIT | `zlogger` |
| [MemoryPack](https://github.com/Cysharp/MemoryPack) | MIT | `memorypack` |
| [MasterMemory](https://github.com/Cysharp/MasterMemory) | MIT | `mastermemory` |
| [NativeMemoryArray](https://github.com/Cysharp/NativeMemoryArray) | MIT | `native-memory-array` |
| [MagicOnion](https://github.com/Cysharp/MagicOnion) | MIT | `magiconion` |
| [MessagePack for C#](https://github.com/MessagePack-CSharp/MessagePack-CSharp) | MIT | `messagepack-csharp`, `mastermemory`, `magiconion` |
| [VContainer](https://github.com/hadashiA/VContainer) | MIT | `vcontainer` |
| [UIEffect](https://github.com/mob-sakai/UIEffect) | MIT | `ui-effect` |
| [Animation Sequencer](https://github.com/brunomikoski/Animation-Sequencer) | MIT | `animation-sequencer` |
| [DOTween](https://dotween.demigiant.com) | Free / DOTween Pro (commercial) | `animation-sequencer` (dependency) |
| [ZBase CSV Reader](https://github.com/Zitga-Tech/ZBase.Csv-Reader) | MIT | `zbase-csv-reader`, `game-data-pipeline` |
| [ZBase PubSub](https://github.com/Zitga-Tech/ZBase.Foundation.PubSub) | MIT | `zbase-pubsub` |
| [ZBase Pooling](https://github.com/Zitga-Tech/ZBase.Foundation.Pooling) | MIT | `zbase-pooling` |
| [Odin Inspector](https://odininspector.com) | Commercial | `zbase-csv-reader`, `game-data-pipeline` (dependency) |
| [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) | MIT | NuGet-based installs in `r3` and other library skills |
| [UnityScreenNavigator](https://github.com/Haruma-K/UnityScreenNavigator) | MIT | `screen-navigator` |
| [Spine Runtimes (spine-unity)](https://github.com/EsotericSoftware/spine-runtimes) | Spine Runtimes License (needs a Spine license) | `spine-unity` |
| [FMOD for Unity](https://www.fmod.com/unity) | FMOD EULA (per-title license) | `fmod-unity` |
| LevelPlay (`com.unity.services.levelplay`, [dashboard](https://platform.ironsrc.com/)) | Unity / ironSource terms | `levelplay-unity-integration` |
| [Blender](https://www.blender.org) | GPL-3.0-or-later (exported files are yours) | `blender-to-unity-pipeline` |
| [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) | MIT | `steamworks-integration`, `steam-lobbies-p2p`, `steam-deck-controller` |
| [Steamworks SDK, SteamCMD](https://partner.steamgames.com/doc/sdk) | Steamworks SDK Access Agreement | `steamworks-integration`, `steam-build-release` |
| [Netcode SteamNetworkingSockets transport](https://github.com/Unity-Technologies/multiplayer-community-contributions) | MIT | `steam-lobbies-p2p` |

## Notes

- Targets Unity 6000.0–6000.6. APIs that changed between minors are flagged in place; unverified snippets say so. Check them against your installed package version.
- `Object.GetInstanceID()` is error CS0619 on 6000.5+. Upstream `zbase-pubsub` and `zbase-pooling` call it; embed and patch them (see those skills).
- Unity 6 compiles C# 9. `zlogger` v2 needs `-langVersion:10` in a `csc.rsp` for every assembly that logs.

## License

Mixed, declared per skill in `SKILL.md` frontmatter. Skills derived from Unity-Technologies/skills (marked \*) use the Unity Companion License and are for Unity-dependent projects only; the rest are MIT. See [NOTICE.md](NOTICE.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
