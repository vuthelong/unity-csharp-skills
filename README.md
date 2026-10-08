# Unity C# Skills

Curated, categorized [Agent Skills](https://docs.claude.com/en/docs/claude-code/skills) for **Unity 6 (6000.x)** and C# development, packaged as a Claude Code plugin marketplace. 71 skills in 10 plugins.

Skills are merged, deduplicated and updated from Unity-Technologies/skills, Unity-Open-MCP, a5c-ai/babysitter and original work. Third-party library skills are grounded in each library's source code; where an upstream README disagrees with its source, the skill follows the source.

## Install

Claude Code:

```
/plugin marketplace add vuthelong/unity-csharp-skills
/plugin install unity-csharp-core@unity-csharp-skills
```

Install any plugin below the same way. To use a single skill with another agent, copy its folder (`plugins/<category>/skills/<skill>/`) into that agent's skills directory, e.g. `~/.claude/skills/` or `.claude/skills/`.

## Plugins and skills

### C# Core — `unity-csharp-core`

C# and Unity coding standards, code review, editor safety.

| Skill | License | Summary |
|---|---|---|
| [`csharp-unity`](plugins/csharp-core/skills/csharp-unity/SKILL.md) | MIT | Writes and refactors Unity 6 C# code to a consistent house style (naming, `this.` on fields, `var`, `#region` layout, early return, no LINQ) with GC-free, lifecycle-correct… |
| [`review-unity-csharp`](plugins/csharp-core/skills/review-unity-csharp/SKILL.md) | MIT | Reviews existing Unity C# code for correctness bugs, lifecycle mistakes, memory and native-resource leaks, Unity 6 obsolete APIs, and violations of the csharp-unity house… |
| [`unity-editor-safety`](plugins/csharp-core/skills/unity-editor-safety/SKILL.md) | MIT | Safety rules and recovery steps for an agent changing a Unity 6 project from outside or inside the Editor - verifying C# edits actually compiled, reading CSxxxx errors from… |

### Editor Tooling — `unity-editor-tooling`

Unity CLI, packages, project bootstrap, search, auditing, builds.

| Skill | License | Summary |
|---|---|---|
| [`asset-transformer-toolkit`](plugins/editor-tooling/skills/asset-transformer-toolkit/SKILL.md) | Unity Companion | Imports CAD, 3D model and point-cloud files with Unity Asset Transformer Toolkit (formerly the Pixyz Plugin, `com.unity.industry.toolkit` 4.0+) and creates, edits, validates… |
| [`build-gtk`](plugins/editor-tooling/skills/build-gtk/SKILL.md) | Unity Companion | Builds node-based Editor tools with Unity Graph Toolkit (GTK, the built-in `Unity.GraphToolkit.Editor` module) — `Graph`/`Node` subclasses, port and option builders,… |
| [`generate-editor-search-query`](plugins/editor-tooling/skills/generate-editor-search-query/SKILL.md) | Unity Companion | Translates natural-language "find/search/locate/list/filter/which assets use/what references X" requests into Unity Search (Quick Search) query syntax — `t:`, `dir:`, `l:`,… |
| [`new-unity-project`](plugins/editor-tooling/skills/new-unity-project/SKILL.md) | Unity Companion | Runs a guided, step-by-step flow from a game idea to a running, version-controlled Unity 6 project — gathers concept, target platforms and monetization; starts the Editor +… |
| [`project-auditor-fixes`](plugins/editor-tooling/skills/project-auditor-fixes/SKILL.md) | Unity Companion | Runs Unity Project Auditor static analysis through the `unity` CLI (`unity command audit` / `audit_status`), reads the issue CSV (Category, Severity, Areas, Description,… |
| [`unity-cli`](plugins/editor-tooling/skills/unity-cli/SKILL.md) | Unity Companion | Drives the official `unity` command-line tool — controls a running Unity Editor live (create/modify GameObjects, edit scenes and assets, inspect the hierarchy, run C# via… |
| [`unity-package-management`](plugins/editor-tooling/skills/unity-package-management/SKILL.md) | Unity Companion | Adds, removes, upgrades, pins and discovers Unity Package Manager (UPM) packages programmatically with `UnityEditor.PackageManager.Client` (`AddAndRemove`, `Search`,… |

### Rendering & VFX — `unity-rendering-vfx`

URP, Render Graph, post-processing, Shader Graph, VFX Graph, lighting.

| Skill | License | Summary |
|---|---|---|
| [`migrate-birp-to-urp`](plugins/rendering-vfx/skills/migrate-birp-to-urp/SKILL.md) | Unity Companion | Plans, executes, and troubleshoots moving a Unity project from the Built-in Render Pipeline (BiRP) to URP on Unity 6 in safe, verified phases - URP asset and renderer setup,… |
| [`optimize-3d-rendering`](plugins/rendering-vfx/skills/optimize-3d-rendering/SKILL.md) | MIT | Diagnoses and reduces 3D rendering cost in Unity 6 URP (URP 17+) - CPU vs GPU triage with the Frame Debugger, Rendering Debugger, Profiler GPU module, Stats and Render Graph… |
| [`shader-graph-create-custom-node`](plugins/rendering-vfx/skills/shader-graph-create-custom-node/SKILL.md) | Unity Companion | Turns HLSL into reusable Shader Graph nodes for URP on Unity 6 - Custom Function nodes (File or String mode, _float/_half suffixes, UnityTexture2D/UnitySamplerState,… |
| [`unity-lighting`](plugins/rendering-vfx/skills/unity-lighting/SKILL.md) | MIT | Sets up, scripts, bakes, and debugs lighting in Unity 6 URP scenes. |
| [`unity-particle-system`](plugins/rendering-vfx/skills/unity-particle-system/SKILL.md) | MIT | Creates, tunes, scripts, and optimizes Unity's built-in CPU Particle System (Shuriken) on Unity 6 with URP particle shaders. |
| [`unity-texture-import`](plugins/rendering-vfx/skills/unity-texture-import/SKILL.md) | MIT | Configures and audits Unity 6 texture import settings through TextureImporter, platform overrides, Presets, and AssetPostprocessor rules. |
| [`unity-vfx-graph`](plugins/rendering-vfx/skills/unity-vfx-graph/SKILL.md) | MIT | Builds, drives, and debugs GPU particle effects with Visual Effect Graph (com.unity.visualeffectgraph 17.x) on Unity 6 URP. |
| [`urp-postprocessing`](plugins/rendering-vfx/skills/urp-postprocessing/SKILL.md) | Unity Companion | Sets up, configures, scripts, and debugs URP post-processing with the Volume framework on Unity 6 (URP 17). |
| [`validate-urp-render-graph-renderer-feature`](plugins/rendering-vfx/skills/validate-urp-render-graph-renderer-feature/SKILL.md) | Unity Companion | Reviews and corrects Unity 6 URP ScriptableRendererFeature / ScriptableRenderPass code that uses the Render Graph API (RecordRenderGraph, ContextContainer,… |

### UI — `unity-ui`

UI Toolkit, uGUI, IMGUI, TextMeshPro, localization, screen navigation.

| Skill | License | Summary |
|---|---|---|
| [`localization`](plugins/ui/skills/localization/SKILL.md) | Unity Companion | Sets up and drives Unity Localization (`com.unity.localization`) — LocalizationSettings and Locales, String Table and Asset Table collections, Smart Strings,… |
| [`optimize-text-mesh-pro`](plugins/ui/skills/optimize-text-mesh-pro/SKILL.md) | Unity Companion | Optimizes TextMeshPro memory, quality, CPU cost, and build size in Unity 6 — static main font plus dynamic fallback chains, atlas size and multi-atlas, Clear Dynamic Data On… |
| [`screen-navigator`](plugins/ui/skills/screen-navigator/SKILL.md) | MIT | Sets up and uses Haruma-K/UnityScreenNavigator (`com.harumak.unityscreennavigator`, uGUI only) for screen routing — PageContainer push/pop with back history, ModalContainer… |
| [`ui`](plugins/ui/skills/ui/SKILL.md) | Unity Companion | Routes Unity UI requests to the right framework skill — `ui-uitk` (UI Toolkit, UXML/USS), `ui-ugui` (Canvas, RectTransform), or `ui-imgui` (OnGUI) — and to the specialist… |
| [`ui-imgui`](plugins/ui/skills/ui-imgui/SKILL.md) | Unity Companion | Maintains and writes Unity IMGUI (immediate mode) code — EditorWindow `OnGUI`, custom inspectors with `OnInspectorGUI`, IMGUI PropertyDrawers, ScriptableWizards, `Handles`… |
| [`ui-ugui`](plugins/ui/skills/ui-ugui/SKILL.md) | Unity Companion | Understands, edits, and generates Unity uGUI (Canvas-based) UI — Canvas/CanvasScaler/GraphicRaycaster setup, RectTransform anchors and pivots, Horizontal/Vertical/Grid Layout… |
| [`ui-uitk`](plugins/ui/skills/ui-uitk/SKILL.md) | Unity Companion | Understands, edits, and generates Unity 6 UI Toolkit UI — UXML layouts, USS stylesheets (flexbox, variables, transitions, 9-slice), UIDocument and PanelSettings setup, runtime… |

### 2D — `unity-2d`

Pixel perfect, sprites, sprite atlases, tilemaps and rule tiles.

| Skill | License | Summary |
|---|---|---|
| [`2d-pixel-perfect`](plugins/2d/skills/2d-pixel-perfect/SKILL.md) | Unity Companion | Sets up, diagnoses, and fixes pixel-perfect 2D rendering with the Pixel Perfect Camera in URP (UnityEngine.Rendering.Universal.PixelPerfectCamera) or Built-in… |
| [`manage-sprite-atlas`](plugins/2d/skills/manage-sprite-atlas/SKILL.md) | Unity Companion | Creates, configures, and loads Sprite Atlas V2 assets (.spriteatlasv2) from editor C# using SpriteAtlasAsset, SpriteAtlasImporter, and SpriteAtlasUtility, by default through an… |
| [`sprite-editor`](plugins/2d/skills/sprite-editor/SKILL.md) | Unity Companion | Edits sprite metadata (names, rects, pivots, 9-slice borders, outlines, physics shapes) and slices sprite sheets (automatic, grid, isometric) by generating editor C# against… |
| [`tilemap-authoring`](plugins/2d/skills/tilemap-authoring/SKILL.md) | Unity Companion | Builds 2D tile levels with Unity Tilemap (com.unity.2d.tilemap) and Rule Tiles (com.unity.2d.tilemap.extras) — creates Grid/Tilemap/TilemapRenderer hierarchies, Tile and… |

### Gameplay Systems — `unity-gameplay-systems`

Physics, navigation, animation, cameras, input, terrain, splines.

| Skill | License | Summary |
|---|---|---|
| [`animation-authoring`](plugins/gameplay-systems/skills/animation-authoring/SKILL.md) | MIT | Authors and drives Unity Mecanim animation in Unity 6 - AnimationClip curves and events (AnimationUtility, EditorCurveBinding), AnimatorController state machines, parameters,… |
| [`character-controller-3d`](plugins/gameplay-systems/skills/character-controller-3d/SKILL.md) | MIT | Builds and debugs 3D player character movement in Unity 6 - choosing between CharacterController, kinematic Rigidbody and dynamic Rigidbody; CharacterController.Move vs… |
| [`cinemachine-cameras`](plugins/gameplay-systems/skills/cinemachine-cameras/SKILL.md) | MIT | Builds and scripts gameplay and cutscene cameras with Cinemachine 3.x (com.unity.cinemachine, namespace Unity.Cinemachine) in Unity 6 - CinemachineBrain, CinemachineCamera… |
| [`initialize-ai-navigation`](plugins/gameplay-systems/skills/initialize-ai-navigation/SKILL.md) | Unity Companion | Sets up, scripts and troubleshoots Unity AI Navigation (com.unity.ai.navigation 2.x) in Unity 6 - NavMeshSurface baking (editor and runtime BuildNavMesh / UpdateNavMesh),… |
| [`input-system-actions`](plugins/gameplay-systems/skills/input-system-actions/SKILL.md) | MIT | Sets up, scripts and tests the Unity Input System 1.x (com.unity.inputsystem) in Unity 6 - Active Input Handling, project-wide actions (InputSystem.actions), .inputactions… |
| [`level-geometry-authoring`](plugins/gameplay-systems/skills/level-geometry-authoring/SKILL.md) | MIT | Generates and edits level geometry by script in Unity 6 - Terrain (TerrainData heightmaps, SetHeights / GetHeights, TerrainLayer splat painting with SetAlphamaps, tree and… |
| [`physics-3d-collision`](plugins/gameplay-systems/skills/physics-3d-collision/SKILL.md) | Unity Companion | Diagnoses and fixes 3D PhysX collision, trigger and query problems in Unity 6 MonoBehaviour projects. |
| [`splines-paths`](plugins/gameplay-systems/skills/splines-paths/SKILL.md) | MIT | Creates, edits and samples Unity Splines 2.x (com.unity.splines, namespace UnityEngine.Splines) in Unity 6 - SplineContainer, Spline, BezierKnot and TangentMode, world vs local… |
| [`timeline-sequencing`](plugins/gameplay-systems/skills/timeline-sequencing/SKILL.md) | MIT | Builds, binds and controls Unity Timeline (com.unity.timeline) sequences in Unity 6 - TimelineAsset (.playable) creation by script, Animation / Activation / Audio / Signal /… |

### Audio & Performance — `unity-audio-performance`

Audio import and mixers, web build optimization, memory profiling.

| Skill | License | Summary |
|---|---|---|
| [`audio-playback-system`](plugins/audio-performance/skills/audio-playback-system/SKILL.md) | MIT | Builds a runtime audio playback layer for Unity 6 built-in audio - an AudioService (VContainer singleton or MonoBehaviour fallback) with pooled AudioSources, AudioCue… |
| [`audio-setup-mixers`](plugins/audio-performance/skills/audio-setup-mixers/SKILL.md) | Unity Companion | Inventories a project's AudioMixers and routes scene AudioSources into the right AudioMixerGroup (Music, SFX, Foley, Voice, UI, Ambience) by classifying what each source plays,… |
| [`fmod-unity`](plugins/audio-performance/skills/fmod-unity/SKILL.md) | MIT | Integrates FMOD Studio into Unity 6 with the FMOD for Unity plugin (2.03/2.04) - install and Studio version matching, Setup Wizard, FMOD Settings (Source Type, Import Type,… |
| [`memory-snapshot-profiling`](plugins/audio-performance/skills/memory-snapshot-profiling/SKILL.md) | MIT | Captures, compares, and interprets Unity Memory Profiler snapshots (.snap) to find leaks, duplicate or oversized assets, and runtime memory growth in Unity 6. |
| [`optimize-audio`](plugins/audio-performance/skills/optimize-audio/SKILL.md) | Unity Companion | Audits and optimizes Unity 6 audio memory, DSP CPU cost, and playback quality through AudioImporter settings, per-platform overrides, and AudioMixer topology. |
| [`optimize-web`](plugins/audio-performance/skills/optimize-web/SKILL.md) | Unity Companion | Audits and optimizes Unity 6 WebGL and WebGPU builds for download size, startup time, browser memory, and runtime smoothness. |

### Services & Monetization — `unity-services-monetization`

Unity Gaming Services, multiplayer, Vivox, IAP, LevelPlay ads.

| Skill | License | Summary |
|---|---|---|
| [`build-live-game`](plugins/services-monetization/skills/build-live-game/SKILL.md) | Unity Companion | Builds and operates live-service games on Unity Gaming Services - Authentication (anonymous, platform, Unity Player Accounts, username/password), Cloud Save access classes,… |
| [`implement-in-app-purchases`](plugins/services-monetization/skills/implement-in-app-purchases/SKILL.md) | Unity Companion | Implements, configures, debugs and migrates Unity In-App Purchasing 5.x (com.unity.purchasing, UnityIAPServices.StoreController) - store connection, product catalog and… |
| [`levelplay-unity-integration`](plugins/services-monetization/skills/levelplay-unity-integration/SKILL.md) | Unity Companion | Guides a step-by-step LevelPlay ad mediation integration through the Ads Mediation package (com.unity.services.levelplay, SDK 9.x) - install and verify, Android Gradle / iOS… |
| [`setup-multiplayer-services`](plugins/services-monetization/skills/setup-multiplayer-services/SKILL.md) | Unity Companion | Designs and implements session-based online multiplayer with the Unity Multiplayer Services SDK (com.unity.services.multiplayer) - topology choice, rooms, parties and lobbies,… |
| [`setup-vivox-voice-chat`](plugins/services-monetization/skills/setup-vivox-voice-chat/SKILL.md) | Unity Companion | Adds in-game voice and text chat with Unity Vivox v16+ (com.unity.services.vivox, VivoxService.Instance) - init and login with Unity Authentication, group (party, team, lobby,… |

### Third-party Libraries — `unity-libraries`

Third-party libraries: UniTask, R3, ObservableCollections, MessagePipe, VContainer, ZString, ZLogger, MemoryPack, MasterMemory, NativeMemoryArray, MessagePack, MagicOnion, UIEffect, Animation Sequencer, ZBase CSV Reader, PubSub, Pooling.

| Skill | License | Summary |
|---|---|---|
| [`animation-sequencer`](plugins/libraries/skills/animation-sequencer/SKILL.md) | MIT | Sets up and uses brunomikoski/Animation-Sequencer (com.brunomikoski.animationsequencer), a DOTween-based inspector tool for authoring and previewing UI/object animation… |
| [`magiconion`](plugins/libraries/skills/magiconion/SKILL.md) | MIT | Builds client-server networking for Unity with Cysharp MagicOnion (gRPC + MessagePack RPC over HTTP/2): a shared C# interface project, an ASP.NET Core server and a Unity client. |
| [`mastermemory`](plugins/libraries/skills/mastermemory/SKILL.md) | MIT | Builds read-only, source-generated master-data databases in Unity 6 with Cysharp MasterMemory v3 (NuGet MasterMemory, on MessagePack-CSharp). |
| [`memorypack`](plugins/libraries/skills/memorypack/SKILL.md) | MIT | Serializes C# objects to a compact binary format in Unity 6 with Cysharp MemoryPack (source-generated, reflection-free, IL2CPP-safe). |
| [`messagepack-csharp`](plugins/libraries/skills/messagepack-csharp/SKILL.md) | MIT | Writes and reviews binary serialization in Unity and .NET with MessagePack-CSharp v3 (NuGet MessagePack, UPM com.github.messagepack-csharp). |
| [`messagepipe`](plugins/libraries/skills/messagepipe/SKILL.md) | MIT | Sets up and reviews Cysharp MessagePipe (com.cysharp.messagepipe) for DI-first pub/sub in Unity. |
| [`native-memory-array`](plugins/libraries/skills/native-memory-array/SKILL.md) | MIT | Uses Cysharp NativeMemoryArray (Cysharp.Collections.NativeMemoryArray<T>) in Unity 6 for native-memory buffers that bypass the managed heap and the 2 GB array limit, exposed as… |
| [`observable-collections`](plugins/libraries/skills/observable-collections/SKILL.md) | MIT | Builds data-driven Unity UI on Cysharp ObservableCollections - generic, low-allocation observable collections (ObservableList, ObservableDictionary, ObservableHashSet,… |
| [`r3`](plugins/libraries/skills/r3/SKILL.md) | MIT | Writes and reviews reactive code in Unity with Cysharp R3 (NuGet R3 + UPM com.cysharp.r3), the successor to UniRx. |
| [`ui-effect`](plugins/libraries/skills/ui-effect/SKILL.md) | MIT | Applies and configures mob-sakai UIEffect v5 (com.coffee.ui-effect, namespace Coffee.UIEffects) on uGUI Image, RawImage, Text and TextMeshProUGUI - grayscale, sepia, blur,… |
| [`unitask`](plugins/libraries/skills/unitask/SKILL.md) | MIT | Writes and reviews allocation-free async/await code in Unity with Cysharp UniTask (com.cysharp.unitask, namespace Cysharp.Threading.Tasks). |
| [`vcontainer`](plugins/libraries/skills/vcontainer/SKILL.md) | MIT | Sets up and reviews hadashiA VContainer (jp.hadashikick.vcontainer), the fast DI container for Unity. |
| [`zbase-csv-reader`](plugins/libraries/skills/zbase-csv-reader/SKILL.md) | MIT | Sets up and uses ZBase.Csv-Reader (com.zbase.csv-reader, Zitga-Tech) to bake CSV files into strongly typed ScriptableObjects at edit time, with optional Google Sheets download. |
| [`zbase-pooling`](plugins/libraries/skills/zbase-pooling/SKILL.md) | MIT | Sets up and reviews Zitga-Tech ZBase.Foundation.Pooling (com.zbase.foundation.pooling) for pooling C# objects, collections, GameObjects and Components in Unity, and chooses… |
| [`zbase-pubsub`](plugins/libraries/skills/zbase-pubsub/SKILL.md) | MIT | Sets up and reviews Zitga-Tech ZBase.Foundation.PubSub (com.zbase.foundation.pubsub, namespace ZBase.Foundation.PubSub), a DI-free, UniTask-based messenger for Unity. |
| [`zlogger`](plugins/libraries/skills/zlogger/SKILL.md) | MIT | Sets up Cysharp ZLogger v2 (Microsoft.Extensions.Logging provider with zero-allocation UTF-8 interpolated logging) in Unity 6. |
| [`zstring`](plugins/libraries/skills/zstring/SKILL.md) | MIT | Builds strings without GC garbage in Unity using Cysharp ZString (com.cysharp.zstring, namespace Cysharp.Text). |

### Asset Pipeline — `unity-asset-pipeline`

Blender to Unity import, Spine runtime, Addressables, CSV to ScriptableObject game data pipeline.

| Skill | License | Summary |
|---|---|---|
| [`addressables-asset-loading`](plugins/asset-pipeline/skills/addressables-asset-loading/SKILL.md) | MIT | Sets up, loads, releases and ships content with Unity Addressables (com.unity.addressables 2.x-4.x) in Unity 6. |
| [`blender-to-unity-pipeline`](plugins/asset-pipeline/skills/blender-to-unity-pipeline/SKILL.md) | MIT | Sets up and audits the Blender to Unity 6 URP model pipeline. |
| [`game-data-pipeline`](plugins/asset-pipeline/skills/game-data-pipeline/SKILL.md) | MIT | Builds and maintains an end-to-end game data pipeline in Unity 6 - CSV or Google Sheets baked by ZBase.Csv-Reader into ScriptableObject tables, auto-marked Addressable in a… |
| [`spine-unity`](plugins/asset-pipeline/skills/spine-unity/SKILL.md) | MIT | Integrates Esoteric Software Spine 2D skeletal animation into Unity 6 with the spine-unity runtime and Spine URP Shaders. |

## Accuracy notes

- Targets Unity 6000.0 to 6000.6. APIs that changed across minors are flagged in the skills, e.g. `Object.GetInstanceID()` is a CS0619 error on 6000.5+.
- Snippets have not all been compiled against every 6000.x minor. Where a skill could not verify an API it says so in place; check those against your installed package version.
- `zbase-pubsub` and `zbase-pooling` upstream packages call `GetInstanceID()` and may not compile on 6000.5+ until patched.
- `zlogger` v2 needs `-langVersion:10` via `csc.rsp` in every assembly that logs; Unity 6 compiles C# 9 by default.

## License

Mixed. See [NOTICE.md](NOTICE.md). Each skill declares its license in its `SKILL.md` frontmatter. Skills derived from Unity-Technologies/skills are under the Unity Companion License and are for Unity-dependent projects only; everything else is MIT.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
