# Unity C# Skills

Curated, categorized [Agent Skills](https://docs.claude.com/en/docs/claude-code/skills) for **Unity 6 (6000.x)** and C# development, packaged as a Claude Code plugin marketplace. 88 skills in 11 plugins.

Skills are merged, deduplicated and updated from Unity-Technologies/skills, Unity-Open-MCP, a5c-ai/babysitter, DietrichGebert/ponytail, scenario-labs/skills and original work. Third-party library skills are grounded in each library's source code; where an upstream README disagrees with its source, the skill follows the source.

## Install

Claude Code:

```
/plugin marketplace add vuthelong/unity-csharp-skills
/plugin install unity-csharp-core@unity-csharp-skills
```

Install any plugin below the same way. To use a single skill with another agent, copy its folder (`plugins/<category>/skills/<skill>/`) into that agent's skills directory, e.g. `~/.claude/skills/` or `.claude/skills/`.

## Conflicts with Unity's official plugin

Many skills here are modified versions of [Unity-Technologies/skills](https://github.com/Unity-Technologies/skills) and keep the same names (`unity-cli`, `optimize-audio`, `build-live-game`, …). Install either these plugins or the official `unity` plugin from `claude-plugins-official`, not both, and remove any loose copies of the same skills from `~/.claude/skills/`. Otherwise one request matches two skills with different content.

## Plugins and skills

### C# Core — `unity-csharp-core`

C# and Unity coding standards, code review, codebase audit, editor safety.

| Skill | License | Summary |
|---|---|---|
| [`audit-unity-codebase`](plugins/csharp-core/skills/audit-unity-codebase/SKILL.md) | MIT | Audits a whole Unity 6 project or folder in one pass: bugs, lifecycle and native leaks, what breaks at the target frame budget or object count, risky code without tests, slow… |
| [`csharp-unity`](plugins/csharp-core/skills/csharp-unity/SKILL.md) | MIT | Writes and refactors Unity 6 C# (MonoBehaviours, ScriptableObjects, editor scripts, game systems, UniTask/Awaitable async, serialization, pooling, tests) with GC-free… |
| [`review-unity-csharp`](plugins/csharp-core/skills/review-unity-csharp/SKILL.md) | MIT | Reviews existing Unity C# for correctness bugs, lifecycle mistakes, memory and native-resource leaks and Unity 6 obsolete APIs, reporting file:line findings with severity and… |
| [`unity-editor-safety`](plugins/csharp-core/skills/unity-editor-safety/SKILL.md) | MIT | Safety rules and recovery steps for an agent changing a Unity 6 project: confirming C# edits really compiled via Editor.log, Safe Mode and stale-assembly recovery, domain… |

### Editor Tooling — `unity-editor-tooling`

Unity CLI, packages, project bootstrap, search, auditing, builds.

| Skill | License | Summary |
|---|---|---|
| [`asset-transformer-toolkit`](plugins/editor-tooling/skills/asset-transformer-toolkit/SKILL.md) | Unity Companion | Imports CAD, 3D model and point-cloud files with Unity Asset Transformer Toolkit (formerly the Pixyz Plugin, com.unity.industry.toolkit 4.0+) and creates, edits and runs its… |
| [`build-gtk`](plugins/editor-tooling/skills/build-gtk/SKILL.md) | Unity Companion | Builds node-based Editor tools with Unity Graph Toolkit (Unity.GraphToolkit.Editor): Graph and Node subclasses, ports and options, subgraphs, blackboard variables… |
| [`generate-editor-search-query`](plugins/editor-tooling/skills/generate-editor-search-query/SKILL.md) | Unity Companion | Turns natural-language find/locate/list requests into Unity Search (Quick Search) queries (t:, dir:, l:, ref=, search expressions) and opens the Search window pre-filled in… |
| [`new-unity-project`](plugins/editor-tooling/skills/new-unity-project/SKILL.md) | Unity Companion | Guides a brand-new Unity 6 game from idea to a running, version-controlled project: asks concept, platforms and monetization while the Editor installs in the background… |
| [`project-auditor-fixes`](plugins/editor-tooling/skills/project-auditor-fixes/SKILL.md) | Unity Companion | Runs Unity Project Auditor through the unity CLI (unity command audit / audit_status), reads its issue CSV and fixes the flagged code, asset-import and project-setting issues… |
| [`unity-cli`](plugins/editor-tooling/skills/unity-cli/SKILL.md) | Unity Companion | Drives the official `unity` command-line tool: controls an open Unity Editor live (GameObjects, scenes, assets, C# through `unity command eval` with com.unity.pipeline)… |
| [`unity-package-management`](plugins/editor-tooling/skills/unity-package-management/SKILL.md) | Unity Companion | Adds, removes, upgrades and discovers Unity Package Manager (UPM) packages: through an open Editor's package_add command or the UnityEditor.PackageManager.Client API, or… |

### Rendering & VFX — `unity-rendering-vfx`

URP, Render Graph, post-processing, Shader Graph, HLSL shaders, VFX Graph, lighting, day-night and weather.

| Skill | License | Summary |
|---|---|---|
| [`day-night-weather`](plugins/rendering-vfx/skills/day-night-weather/SKILL.md) | MIT | Builds a dynamic day-night cycle and weather system for Unity 6 URP - a normalized time-of-day clock with day length, time scale and sunrise/sunset/new-day events, sun and… |
| [`migrate-birp-to-urp`](plugins/rendering-vfx/skills/migrate-birp-to-urp/SKILL.md) | Unity Companion | Plans and runs a phased, verified migration of a Unity 6 project from the Built-in Render Pipeline to URP - URP asset setup, Render Pipeline Converter, material and particle… |
| [`optimize-3d-rendering`](plugins/rendering-vfx/skills/optimize-3d-rendering/SKILL.md) | MIT | Diagnoses and reduces the frame cost of 3D URP scenes on Unity 6 - CPU vs GPU triage with the Frame Debugger and Profiler, SRP Batcher and GPU Resident Drawer batching… |
| [`shader-graph-create-custom-node`](plugins/rendering-vfx/skills/shader-graph-create-custom-node/SKILL.md) | Unity Companion | Turns HLSL into reusable Shader Graph nodes for URP on Unity 6 - Custom Function nodes (File or String mode, _float/_half suffixes, SHADERGRAPH_PREVIEW guards), Sub Graphs… |
| [`unity-lighting`](plugins/rendering-vfx/skills/unity-lighting/SKILL.md) | MIT | Sets up, scripts, bakes, and debugs lighting in Unity 6 URP scenes - Light components and modes, shadows, ambient and skybox, fog, reflection and light probes, Adaptive Probe… |
| [`unity-particle-system`](plugins/rendering-vfx/skills/unity-particle-system/SKILL.md) | MIT | Creates, tunes, scripts, and optimizes the built-in CPU Particle System (Shuriken) on Unity 6 with URP particle shaders - the module struct API, bursts, Emit with EmitParams… |
| [`unity-texture-import`](plugins/rendering-vfx/skills/unity-texture-import/SKILL.md) | MIT | Configures and audits Unity 6 texture import settings with TextureImporter, per-platform overrides, Presets, and AssetPostprocessor rules - texture type, sRGB vs linear data… |
| [`unity-vfx-graph`](plugins/rendering-vfx/skills/unity-vfx-graph/SKILL.md) | MIT | Builds, drives, and debugs GPU particle effects with Visual Effect Graph on Unity 6 URP - contexts and blocks, exposed properties and the VisualEffect C# API, events… |
| [`urp-hlsl-shaders`](plugins/rendering-vfx/skills/urp-hlsl-shaders/SKILL.md) | MIT | Writes, fixes and verifies hand-written HLSL/ShaderLab shaders for URP on Unity 6: UniversalForward, ShadowCaster, DepthOnly and DepthNormals passes that agree, SRP Batcher… |
| [`urp-postprocessing`](plugins/rendering-vfx/skills/urp-postprocessing/SKILL.md) | Unity Companion | Sets up, scripts, and debugs URP post-processing with the Volume framework on Unity 6 - global and local Volumes, VolumeProfile overrides such as Bloom, Tonemapping, Color… |
| [`validate-urp-render-graph-renderer-feature`](plugins/rendering-vfx/skills/validate-urp-render-graph-renderer-feature/SKILL.md) | Unity Companion | Writes, reviews, and fixes Unity 6 URP ScriptableRendererFeature and ScriptableRenderPass code built on the Render Graph API (RecordRenderGraph, AddRasterRenderPass… |

### UI — `unity-ui`

UI Toolkit, uGUI, IMGUI, TextMeshPro, localization, screen navigation, PC settings menu, accessibility.

| Skill | License | Summary |
|---|---|---|
| [`accessibility`](plugins/ui/skills/accessibility/SKILL.md) | MIT | Adds player-facing accessibility options to a Unity 6 PC/Steam game - subtitles and sound captions with speaker names, size, background opacity and reading-speed timing… |
| [`localization`](plugins/ui/skills/localization/SKILL.md) | Unity Companion | Sets up and drives Unity Localization (com.unity.localization) - Locales, String and Asset Tables, Smart Strings, LocalizeStringEvent wiring, runtime locale switching… |
| [`optimize-text-mesh-pro`](plugins/ui/skills/optimize-text-mesh-pro/SKILL.md) | Unity Companion | Optimizes TextMeshPro font assets in Unity 6 for memory, quality, CPU and build size - static main font plus dynamic fallback chains, atlas sizing, padding and sampling… |
| [`pc-settings-menu`](plugins/ui/skills/pc-settings-menu/SKILL.md) | MIT | Builds the logic behind a PC game settings menu in Unity 6 - a settings model saved as JSON under Application.persistentDataPath separately from save games, display options… |
| [`screen-navigator`](plugins/ui/skills/screen-navigator/SKILL.md) | MIT | Sets up and uses Haruma-K/UnityScreenNavigator (uGUI only) for screen routing - PageContainer push/pop history, ModalContainer popups with backdrops, SheetContainer tabs… |
| [`ui`](plugins/ui/skills/ui/SKILL.md) | Unity Companion | Routes ambiguous Unity UI requests to the right skill. |
| [`ui-imgui`](plugins/ui/skills/ui-imgui/SKILL.md) | Unity Companion | Maintains and writes Unity IMGUI code - EditorWindow OnGUI, OnInspectorGUI custom editors, IMGUI PropertyDrawers, ScriptableWizards, Handles scene GUI and runtime OnGUI debug… |
| [`ui-ugui`](plugins/ui/skills/ui-ugui/SKILL.md) | Unity Companion | Understands, edits and generates Unity uGUI (Canvas-based) UI - Canvas and CanvasScaler setup, RectTransform anchors, Layout Groups, ScrollRect, buttons and input fields… |
| [`ui-uitk`](plugins/ui/skills/ui-uitk/SKILL.md) | Unity Companion | Understands, edits and generates Unity 6 UI Toolkit UI - UXML layouts, USS styling, UIDocument and PanelSettings, runtime data binding, custom controls, manipulators… |

### 2D — `unity-2d`

Pixel perfect, sprites, sprite atlases, tilemaps and rule tiles.

| Skill | License | Summary |
|---|---|---|
| [`2d-pixel-perfect`](plugins/2d/skills/2d-pixel-perfect/SKILL.md) | Unity Companion | Sets up, diagnoses, and fixes pixel-perfect 2D rendering with the Pixel Perfect Camera in URP or the Built-in pipeline, plus point-filtered, uncompressed sprite import. |
| [`manage-sprite-atlas`](plugins/2d/skills/manage-sprite-atlas/SKILL.md) | Unity Companion | Creates, configures, and loads Sprite Atlas V2 assets from editor C#, by default through a prebuild generator - packables, packing and per-platform texture settings, HD/SD… |
| [`sprite-editor`](plugins/2d/skills/sprite-editor/SKILL.md) | Unity Companion | Edits sprite metadata (rects, names, pivots, 9-slice borders, outlines, physics shapes) and slices sprite sheets (automatic, grid, isometric) through… |
| [`tilemap-authoring`](plugins/2d/skills/tilemap-authoring/SKILL.md) | Unity Companion | Builds 2D tile levels with Unity Tilemap and Rule Tiles from C# - Grid and Tilemap hierarchies, Tile and RuleTile assets, rectangular, isometric, and hex Tile Palettes… |

### Gameplay Systems — `unity-gameplay-systems`

Physics, navigation, animation, cameras, input, terrain and world building, splines, save system, boot flow, interaction, enemy AI, combat.

| Skill | License | Summary |
|---|---|---|
| [`animation-authoring`](plugins/gameplay-systems/skills/animation-authoring/SKILL.md) | MIT | Authors and drives Mecanim animation in Unity 6 - AnimationClip curves and events, AnimatorController state machines, blend trees and layers built from editor scripts… |
| [`character-controller-3d`](plugins/gameplay-systems/skills/character-controller-3d/SKILL.md) | MIT | Builds and debugs 3D player movement in Unity 6 with CharacterController or a kinematic or dynamic Rigidbody - ground probes, gravity, slopes and steps, jump buffering and… |
| [`cinemachine-cameras`](plugins/gameplay-systems/skills/cinemachine-cameras/SKILL.md) | MIT | Builds and scripts gameplay and cutscene cameras with Cinemachine 3.x in Unity 6 - CinemachineBrain, CinemachineCamera priority and blends, follow, orbit, third-person and… |
| [`combat-damage-system`](plugins/gameplay-systems/skills/combat-damage-system/SKILL.md) | MIT | Builds health and damage for Unity 6 3D games - Health with a DamageInfo pipeline (amount, type, source, team, hit point), resistances, invulnerability frames, death… |
| [`enemy-ai-behavior`](plugins/gameplay-systems/skills/enemy-ai-behavior/SKILL.md) | MIT | Builds enemy and NPC combat AI for Unity 6 3D games - choosing a finite state machine, Unity Behavior graphs (com.unity.behavior) or utility AI, perception (vision cone with… |
| [`game-bootstrap-flow`](plugins/gameplay-systems/skills/game-bootstrap-flow/SKILL.md) | MIT | Builds the startup and top-level flow of a Unity 6 game - a persistent services prefab created by RuntimeInitializeOnLoadMethod (or a Boot scene) and kept with… |
| [`initialize-ai-navigation`](plugins/gameplay-systems/skills/initialize-ai-navigation/SKILL.md) | Unity Companion | Sets up, scripts and troubleshoots Unity AI Navigation (com.unity.ai.navigation 2.x) in Unity 6 - NavMeshSurface baking at edit time or runtime, NavMeshAgent, obstacle… |
| [`input-system-actions`](plugins/gameplay-systems/skills/input-system-actions/SKILL.md) | MIT | Sets up, scripts and tests the Unity Input System 1.x in Unity 6 - Active Input Handling, project-wide actions, .inputactions assets, bindings and composites, control… |
| [`interaction-system`](plugins/gameplay-systems/skills/interaction-system/SKILL.md) | MIT | Builds a player interaction system for Unity 6 3D games - an IInteractable interface, target detection by camera SphereCast or OverlapSphereNonAlloc with distance, angle… |
| [`level-geometry-authoring`](plugins/gameplay-systems/skills/level-geometry-authoring/SKILL.md) | MIT | Generates and edits level geometry by script in Unity 6 - Terrain heightmaps, splat painting, tree and detail placement, holes and multi-tile stitching, ProBuilder greybox… |
| [`physics-3d-collision`](plugins/gameplay-systems/skills/physics-3d-collision/SKILL.md) | Unity Companion | Diagnoses and fixes 3D PhysX collision, trigger and query problems in Unity 6 MonoBehaviour projects - Rigidbody body types, Layer Collision Matrix, MeshCollider rules… |
| [`save-system`](plugins/gameplay-systems/skills/save-system/SKILL.md) | MIT | Builds a crash-safe save game system for Unity 6 - plain C# save DTOs serialized with Newtonsoft Json.NET (com.unity.nuget.newtonsoft-json), a version envelope with a tested… |
| [`splines-paths`](plugins/gameplay-systems/skills/splines-paths/SKILL.md) | MIT | Creates, edits and samples Unity Splines 2.x in Unity 6 - SplineContainer and knots, world vs local evaluation, arc-length and nearest-point queries, NativeSpline for jobs… |
| [`timeline-sequencing`](plugins/gameplay-systems/skills/timeline-sequencing/SKILL.md) | MIT | Builds, binds and controls Unity Timeline sequences in Unity 6 - TimelineAsset creation by script, Animation, Activation, Audio, Signal and Control tracks, markers and… |

### Audio & Performance — `unity-audio-performance`

Audio import and mixers, web build optimization, memory and CPU profiling.

| Skill | License | Summary |
|---|---|---|
| [`audio-playback-system`](plugins/audio-performance/skills/audio-playback-system/SKILL.md) | MIT | Builds the runtime playback layer for Unity built-in audio - a pooled AudioService with AudioCue ScriptableObjects, voice limits and stealing, a crossfading MusicPlayer with… |
| [`audio-setup-mixers`](plugins/audio-performance/skills/audio-setup-mixers/SKILL.md) | Unity Companion | Routes scene AudioSources into the right AudioMixerGroup (Music, SFX, Foley, Voice, UI, Ambience) by classifying what each plays, and covers runtime mixer control - exposed… |
| [`cpu-performance-profiling`](plugins/audio-performance/skills/cpu-performance-profiling/SKILL.md) | MIT | Triages CPU-bound frames and hitches in Unity 6 with the Unity Profiler - profiling a development player instead of the Editor (Autoconnect Profiler, targeted Deep Profile)… |
| [`fmod-unity`](plugins/audio-performance/skills/fmod-unity/SKILL.md) | MIT | Integrates FMOD Studio into Unity 6 with the FMOD for Unity plugin - setup and Studio version matching, settings and banks, EventInstance lifecycle, EventReference, 3D… |
| [`memory-snapshot-profiling`](plugins/audio-performance/skills/memory-snapshot-profiling/SKILL.md) | MIT | Captures, compares and interprets Unity Memory Profiler snapshots (.snap) in Unity 6 to find leaks, duplicate or oversized assets, and memory growth, and reads live memory… |
| [`optimize-audio`](plugins/audio-performance/skills/optimize-audio/SKILL.md) | Unity Companion | Audits and fixes the cost of audio assets in Unity 6 - AudioImporter Load Type, compression format, sample rate, Force To Mono and per-platform overrides, plus DSP cost from… |
| [`optimize-web`](plugins/audio-performance/skills/optimize-web/SKILL.md) | Unity Companion | Audits and optimizes Unity 6 WebGL and WebGPU builds for download size, startup time, browser memory, and frame pacing. |

### Services & Monetization — `unity-services-monetization`

Unity Gaming Services, Netcode for GameObjects, network debugging, multiplayer, Vivox, IAP, LevelPlay ads.

| Skill | License | Summary |
|---|---|---|
| [`build-live-game`](plugins/services-monetization/skills/build-live-game/SKILL.md) | Unity Companion | Builds live-service backends on Unity Gaming Services - sign-in, Cloud Save, Cloud Code C# modules for server-authoritative writes, Remote Config and Game Overrides, Economy… |
| [`implement-in-app-purchases`](plugins/services-monetization/skills/implement-in-app-purchases/SKILL.md) | Unity Companion | Implements, debugs and migrates real-money purchases with Unity IAP 5.x (StoreController) - store connection, product catalog, consumables, non-consumables, subscriptions… |
| [`levelplay-unity-integration`](plugins/services-monetization/skills/levelplay-unity-integration/SKILL.md) | Unity Companion | Integrates LevelPlay ad mediation into Unity through the Ads Mediation package (com.unity.services.levelplay, SDK 9.x) - install/upgrade, Android Gradle and iOS CocoaPods… |
| [`netcode-for-gameobjects`](plugins/services-monetization/skills/netcode-for-gameobjects/SKILL.md) | MIT | Builds host-client co-op multiplayer with Netcode for GameObjects 2.x (com.unity.netcode.gameobjects) in Unity 6 - NetworkManager and UnityTransport setup, NetworkObject… |
| [`network-debugging`](plugins/services-monetization/skills/network-debugging/SKILL.md) | MIT | Diagnoses and reproduces online co-op problems in Netcode for GameObjects 2.x on Unity 6 - Network Simulator presets, lag spikes and disconnects… |
| [`setup-multiplayer-services`](plugins/services-monetization/skills/setup-multiplayer-services/SKILL.md) | Unity Companion | Designs and implements session-based online multiplayer with the Unity Multiplayer Services SDK - topology choice, lobbies, parties and rooms, join codes, session browsing… |
| [`setup-vivox-voice-chat`](plugins/services-monetization/skills/setup-vivox-voice-chat/SKILL.md) | Unity Companion | Adds in-game voice and text chat with Unity Vivox 16+ (VivoxService) - init and login with Unity Authentication, party, team or lobby channels, positional proximity voice… |

### Third-party Libraries — `unity-libraries`

Third-party libraries: UniTask, R3, ObservableCollections, MessagePipe, VContainer, ZString, ZLogger, MemoryPack, MasterMemory, NativeMemoryArray, MessagePack, MagicOnion, UIEffect, Animation Sequencer, ZBase CSV Reader, PubSub, Pooling.

| Skill | License | Summary |
|---|---|---|
| [`animation-sequencer`](plugins/libraries/skills/animation-sequencer/SKILL.md) | MIT | Authors and drives DOTween-based UI/object animation sequences with brunomikoski Animation Sequencer (AnimationSequencerController, steps, DOTween actions, custom steps). |
| [`magiconion`](plugins/libraries/skills/magiconion/SKILL.md) | MIT | Builds real-time client-server networking between Unity and an ASP.NET Core server with Cysharp MagicOnion (gRPC + MessagePack over HTTP/2): shared interface project, Unary… |
| [`mastermemory`](plugins/libraries/skills/mastermemory/SKILL.md) | MIT | Builds read-only, indexed master-data databases with Cysharp MasterMemory v3 (source-generated tables over MessagePack): [MemoryTable] schemas, primary/secondary/composite… |
| [`memorypack`](plugins/libraries/skills/memorypack/SKILL.md) | MIT | Serializes C# objects with Cysharp MemoryPack, the fastest C#-only binary format (source-generated, IL2CPP-safe): [MemoryPackable] types, version tolerance, unions, Unity… |
| [`messagepack-csharp`](plugins/libraries/skills/messagepack-csharp/SKILL.md) | MIT | Writes and reviews MessagePack-CSharp v3 binary serialization in Unity and .NET: [MessagePackObject]/[Key] contracts, source-generated resolvers, the single IL2CPP resolver… |
| [`messagepipe`](plugins/libraries/skills/messagepipe/SKILL.md) | MIT | Sets up Cysharp MessagePipe for DI-integrated, typed pub/sub and request/response in Unity (VContainer, Zenject or built-in container): IPublisher/ISubscriber, keyed… |
| [`native-memory-array`](plugins/libraries/skills/native-memory-array/SKILL.md) | MIT | Uses Cysharp NativeMemoryArray<T> for native-memory buffers that bypass the managed heap and the 2 GB array limit, exposed as Span, Memory, IBufferWriter and ReadOnlySequence. |
| [`observable-collections`](plugins/libraries/skills/observable-collections/SKILL.md) | MIT | Builds data-driven UI on Cysharp ObservableCollections: observable list/dictionary/queue/ring-buffer with struct change events, synchronized views that map items to row views… |
| [`r3`](plugins/libraries/skills/r3/SKILL.md) | MIT | Writes and reviews reactive code with Cysharp R3, the UniRx successor: Observable, Subject, ReactiveProperty, operators, frame/time providers, subscription disposal, uGUI… |
| [`ui-effect`](plugins/libraries/skills/ui-effect/SKILL.md) | MIT | Applies mob-sakai UIEffect v5 effects on uGUI Image, RawImage, Text and TextMeshProUGUI (grayscale, blur, dissolve, shiny, shadow/outline, gradation), with presets… |
| [`unitask`](plugins/libraries/skills/unitask/SKILL.md) | MIT | Writes and reviews allocation-free async/await in Unity with Cysharp UniTask: UniTask/UniTaskVoid return types, PlayerLoop timing, cancellation tied to object lifetime… |
| [`vcontainer`](plugins/libraries/skills/vcontainer/SKILL.md) | MIT | Sets up and reviews hadashiA VContainer dependency injection in Unity: LifetimeScope hierarchies, lifetimes, interface and keyed registration, factories, component… |
| [`zbase-csv-reader`](plugins/libraries/skills/zbase-csv-reader/SKILL.md) | MIT | Bakes CSV files into strongly typed ScriptableObjects at edit time with ZBase.Csv-Reader (Zitga-Tech), with optional Google Sheets download: Csv-Reader window, reader config… |
| [`zbase-pooling`](plugins/libraries/skills/zbase-pooling/SKILL.md) | MIT | Sets up Zitga-Tech ZBase.Foundation.Pooling for pooling C# objects, collections, GameObjects and Components, including UniTask async pools, prepooling and Addressables pools… |
| [`zbase-pubsub`](plugins/libraries/skills/zbase-pubsub/SKILL.md) | MIT | Sets up Zitga-Tech ZBase.Foundation.PubSub, a DI-free, UniTask-based messenger: Messenger object, typed publishers/subscribers, global and object-bound scopes, sync and async… |
| [`zlogger`](plugins/libraries/skills/zlogger/SKILL.md) | MIT | Sets up Cysharp ZLogger v2, a zero-allocation Microsoft.Extensions.Logging provider for Unity: NuGet + UPM install, the csc.rsp -langVersion:10 opt-in, Unity console, file… |
| [`zstring`](plugins/libraries/skills/zstring/SKILL.md) | MIT | Builds strings without GC garbage in Unity using Cysharp ZString: Concat, Format, Join, UTF-16/UTF-8 value builders, prepared formats, TextMeshPro SetTextFormat/SetText… |

### Asset Pipeline — `unity-asset-pipeline`

Blender to Unity import, Spine runtime, Addressables, CSV to ScriptableObject game data pipeline.

| Skill | License | Summary |
|---|---|---|
| [`addressables-asset-loading`](plugins/asset-pipeline/skills/addressables-asset-loading/SKILL.md) | MIT | Loads, releases and ships content with Unity Addressables in Unity 6 - groups and packing, addresses vs AssetReference vs labels, async loading and scenes, handle ownership… |
| [`blender-to-unity-pipeline`](plugins/asset-pipeline/skills/blender-to-unity-pipeline/SKILL.md) | MIT | Sets up and audits the Blender to Unity 6 URP model pipeline - FBX/glTF export settings, ModelImporter scale and axis, rigs (Generic vs Humanoid, Avatar reuse), animation… |
| [`game-data-pipeline`](plugins/asset-pipeline/skills/game-data-pipeline/SKILL.md) | MIT | Builds an end-to-end game data pipeline in Unity 6 - CSV or Google Sheets baked into ScriptableObject tables, auto-registered as Addressables, loaded by label into id lookups… |
| [`spine-unity`](plugins/asset-pipeline/skills/spine-unity/SKILL.md) | MIT | Integrates Spine 2D skeletal animation into Unity 6 with spine-unity 4.2 or 4.3 and the Spine URP Shaders - install, export settings, texture workflow, SkeletonAnimation /… |

### PC & Steam — `unity-pc-steam`

Steamworks.NET, Steam lobbies and P2P co-op, SteamPipe builds and uploads, Steam Deck and controllers.

| Skill | License | Summary |
|---|---|---|
| [`steam-build-release`](plugins/pc-steam/skills/steam-build-release/SKILL.md) | MIT | Builds a Unity 6 game for Steam and uploads it with SteamPipe - a batch-mode Editor build script (BuildPipeline.BuildPlayer or a Build Profile, nonzero exit on failure… |
| [`steam-deck-controller`](plugins/pc-steam/skills/steam-deck-controller/SKILL.md) | MIT | Makes a Unity 6 Steam game work well on Steam Deck and with gamepads - Deck detection through Steamworks.NET (IsSteamRunningOnSteamDeck on SDK 1.64, IsRunningOnSteamHardware… |
| [`steam-lobbies-p2p`](plugins/pc-steam/skills/steam-lobbies-p2p/SKILL.md) | MIT | Builds friends-only Steam lobbies, invites and join flows for host-client co-op in Unity 6 with Steamworks.NET, and connects them to Netcode for GameObjects over Steam P2P… |
| [`steamworks-integration`](plugins/pc-steam/skills/steamworks-integration/SKILL.md) | MIT | Integrates Steamworks into a Unity 6 PC game with Steamworks.NET (com.rlabrecque.steamworks.net) - UPM git install, steam_appid.txt, SteamAPI.RestartAppIfNecessary… |

## Third-party SDKs and tools

Skills that target software outside Unity's own packages. Each one is installed separately and carries its own license; these skills don't bundle it.

| SDK / tool | Link | License | Skill |
|---|---|---|---|
| UniTask | [Cysharp/UniTask](https://github.com/Cysharp/UniTask) | MIT | `unitask` |
| R3 | [Cysharp/R3](https://github.com/Cysharp/R3) | MIT | `r3` |
| ObservableCollections | [Cysharp/ObservableCollections](https://github.com/Cysharp/ObservableCollections) | MIT | `observable-collections` |
| MessagePipe | [Cysharp/MessagePipe](https://github.com/Cysharp/MessagePipe) | MIT | `messagepipe` |
| ZString | [Cysharp/ZString](https://github.com/Cysharp/ZString) | MIT | `zstring` |
| ZLogger | [Cysharp/ZLogger](https://github.com/Cysharp/ZLogger) | MIT | `zlogger` |
| MemoryPack | [Cysharp/MemoryPack](https://github.com/Cysharp/MemoryPack) | MIT | `memorypack` |
| MasterMemory | [Cysharp/MasterMemory](https://github.com/Cysharp/MasterMemory) | MIT | `mastermemory` |
| NativeMemoryArray | [Cysharp/NativeMemoryArray](https://github.com/Cysharp/NativeMemoryArray) | MIT | `native-memory-array` |
| MagicOnion | [Cysharp/MagicOnion](https://github.com/Cysharp/MagicOnion) | MIT | `magiconion` |
| MessagePack for C# | [MessagePack-CSharp/MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp) | MIT | `messagepack-csharp`, `mastermemory`, `magiconion` |
| VContainer | [hadashiA/VContainer](https://github.com/hadashiA/VContainer) | MIT | `vcontainer` |
| UIEffect | [mob-sakai/UIEffect](https://github.com/mob-sakai/UIEffect) | MIT | `ui-effect` |
| Animation Sequencer | [brunomikoski/Animation-Sequencer](https://github.com/brunomikoski/Animation-Sequencer) | MIT | `animation-sequencer` |
| DOTween | [dotween.demigiant.com](https://dotween.demigiant.com) | Free / DOTween Pro (commercial) | `animation-sequencer` (dependency) |
| ZBase CSV Reader | [Zitga-Tech/ZBase.Csv-Reader](https://github.com/Zitga-Tech/ZBase.Csv-Reader) | MIT | `zbase-csv-reader`, `game-data-pipeline` |
| ZBase PubSub | [Zitga-Tech/ZBase.Foundation.PubSub](https://github.com/Zitga-Tech/ZBase.Foundation.PubSub) | MIT | `zbase-pubsub` |
| ZBase Pooling | [Zitga-Tech/ZBase.Foundation.Pooling](https://github.com/Zitga-Tech/ZBase.Foundation.Pooling) | MIT | `zbase-pooling` |
| Odin Inspector | [odininspector.com](https://odininspector.com) | Commercial | `zbase-csv-reader`, `game-data-pipeline` (dependency) |
| NuGetForUnity | [GlitchEnzo/NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) | MIT | NuGet-based installs in `r3` and other library skills |
| UnityScreenNavigator | [Haruma-K/UnityScreenNavigator](https://github.com/Haruma-K/UnityScreenNavigator) | MIT | `screen-navigator` |
| Spine Runtimes (spine-unity) | [EsotericSoftware/spine-runtimes](https://github.com/EsotericSoftware/spine-runtimes) | Spine Runtimes License (needs a Spine license) | `spine-unity` |
| FMOD for Unity | [fmod.com/unity](https://www.fmod.com/unity), [download](https://www.fmod.com/download), [licensing](https://www.fmod.com/licensing) | FMOD EULA (per-title license) | `fmod-unity` |
| LevelPlay (ironSource) | `com.unity.services.levelplay` (Unity Registry), [dashboard](https://platform.ironsrc.com/) | Unity / ironSource terms | `levelplay-unity-integration` |
| Blender | [blender.org](https://www.blender.org) | GPL-3.0-or-later (exported files are yours) | `blender-to-unity-pipeline` |
| Steamworks.NET | [rlabrecque/Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) | MIT | `steamworks-integration`, `steam-lobbies-p2p`, `steam-deck-controller` |
| Steamworks SDK, SteamCMD | [partner.steamgames.com](https://partner.steamgames.com/doc/sdk) | Steamworks SDK Access Agreement | `steamworks-integration`, `steam-build-release` |
| Netcode SteamNetworkingSockets transport | [Unity-Technologies/multiplayer-community-contributions](https://github.com/Unity-Technologies/multiplayer-community-contributions) | MIT | `steam-lobbies-p2p` |

## Accuracy notes

- Targets Unity 6000.0 to 6000.6. APIs that changed across minors are flagged in the skills, e.g. newer Unity 6 releases move to `EntityId` and deprecate `Object.GetInstanceID()`; check your version's docs.
- Snippets have not all been compiled against every 6000.x minor. Where a skill could not verify an API it says so in place; check those against your installed package version.
- `zbase-pubsub` and `zbase-pooling` upstream packages call `GetInstanceID()` and may break on newer Unity 6 releases that remove it, until patched.
- `zlogger` v2 needs `-langVersion:10` via `csc.rsp` in every assembly that logs; Unity 6 compiles C# 9 by default.

## License

Mixed. See [NOTICE.md](NOTICE.md). Each skill declares its license in its `SKILL.md` frontmatter. Skills derived from Unity-Technologies/skills are under the Unity Companion License and are for Unity-dependent projects only; everything else is MIT.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
