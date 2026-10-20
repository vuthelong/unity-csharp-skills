---
name: build-live-game
description: Builds live-service backends on Unity Gaming Services - sign-in, Cloud Save, Cloud Code C# modules for server-authoritative writes, Remote Config and Game Overrides, Economy, Leaderboards, Analytics, Access Control and deployment - with blueprints for achievements, battle passes and player accounts. Use for live-ops, player progression, season passes, rankings, feature flags or A/B tests, or when code touches UnityServices.InitializeAsync or .rc/.ac/.ccmr files. Not for sessions or IAP.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: services-monetization
  sources: "Unity-Technologies/skills/skills/build-live-game"
  unity: "6000.0+"
---

# Build a Live Game With Unity Gaming Services

## UGS Packages

| Package | Tested version | Purpose |
|---|---|---|
| `com.unity.services.core` | 1.16.0 | Initialization, dependency graph |
| `com.unity.services.authentication` | 3.6.1 | Player sign-in and identity |
| `com.unity.services.cloudcode` | 2.10.3 | Server-authoritative C# modules |
| `com.unity.services.cloudsave` | 3.4.0 | Per-player and shared key-value storage |
| `com.unity.remote-config` | 4.2.5 | Server-side game configuration (note: no `services.` in the id) |
| `com.unity.services.economy` | - | Virtual currencies, inventory, virtual purchases |
| `com.unity.services.leaderboards` | - | Scores, rankings, tiers |
| `com.unity.services.analytics` | - | Custom/standard events, consent |
| `com.unity.services.deployment` | 1.7.2 | Deploy cloud resources from Editor |
| `com.unity.services.tooling` | 1.4.1 | Access Control and Game Overrides |
| `com.unity.services.apis` | 1.1.1 | Generated REST clients for all UGS services |

Versions are the ones these references were written against, not hard minimums; verify current versions in Package Manager and keep newer ones the project already has. Install with Package Manager or headlessly (see `unity-package-management`). Link the project to a UGS project and environment in **Project Settings > Services** before any call succeeds.

- [Initialization Pattern](#initialization-pattern)
- [Package Map](#package-map)
- [Architecture — How Packages Combine](#architecture--how-packages-combine)
- [Core Services — Quick Reference](#core-services--quick-reference)
- [Asset Store Building Blocks](references/building-blocks.md)
- [Ready-Made Feature Blueprints](#ready-made-feature-blueprints)
- [Common Architecture Patterns](#common-architecture-patterns)
- [Validation](#validation)
- [Deployment Checklist](#deployment-checklist)
- [Detailed References](#detailed-references)
- [Related skills](#related-skills)

## Initialization Pattern

Every UGS game starts the same way. Initialize Core once, then sign the player in:

```csharp
using Unity.Services.Core;
using Unity.Services.Authentication;

await UnityServices.InitializeAsync();
if (!AuthenticationService.Instance.IsSignedIn)
    await AuthenticationService.Instance.SignInAnonymouslyAsync();
```

After this, service singletons (`CloudSaveService.Instance`, `CloudCodeService.Instance`, ...) are usable.

- Put this in **one** bootstrap (a `DontDestroyOnLoad` object or a scene-zero loader) and have feature managers await it. Several managers each calling `SignInAnonymouslyAsync` concurrently throws "already signing in" errors.
- Pass `new InitializationOptions().SetEnvironmentName("dev")` to target a non-production environment.
- On Unity 6, wrap `async void` entry points (`Start`, UI callbacks) in try/catch, or return `Awaitable` / `Task` from helpers, so `RequestFailedException` and its subclasses are handled instead of only logged. Use `destroyCancellationToken` to stop work after the object is destroyed.
- Sign-in providers, profiles, linking, token expiry and errors: [references/authentication.md](references/authentication.md).

## Package Map

### Foundation

| Package | Purpose | Singleton / Entry Point |
|---|---|---|
| **Core** | Initialization, dependency graph, component registry | `UnityServices.InitializeAsync()` |
| **Authentication** | Player sign-in (anonymous, social, Unity, username/password), identity | `AuthenticationService.Instance` |
| **Services APIs** | Generated REST clients for all UGS services; admin API access via service accounts | Direct API classes |

### Player Data and Configuration

| Package | Purpose | Singleton / Entry Point |
|---|---|---|
| **Cloud Save** | Per-player key-value data (Default, Public, Protected) and game-wide Custom data | `CloudSaveService.Instance.Data.Player` / `.Data.Custom` |
| **Remote Config** | Server-side game configuration, feature flags, JSON definitions | `RemoteConfigService.Instance` |
| **Economy** | Virtual currencies, inventory items, purchases, stores | `EconomyService.Instance` |

### Server Logic and Security

| Package | Purpose | Singleton / Entry Point |
|---|---|---|
| **Cloud Code** | Server-authoritative C# modules for trusted writes and validation | `CloudCodeService.Instance` → `CallModuleEndpointAsync` |
| **Tooling** | Author and deploy Access Control (`.ac`) and Game Overrides (`.ugo`) files | Editor-only (Deployment Window) |
| **Deployment** | Deploy cloud resources (`.rc`, `.ac`, `.ccmr`, `.lb`, etc.) from the Unity Editor | Editor-only (Services > Deployment) |

### Social and Competitive

| Package | Purpose | Singleton / Entry Point |
|---|---|---|
| **Multiplayer** | Sessions, matchmaking, lobbies. Building Blocks: [Multiplayer Session, Matchmaker Session, Server Session](references/building-blocks.md) | `MultiplayerService.Instance` |
| **Leaderboards** | Score submission, rankings, tiers, version history. Building Block: [Leaderboards](references/building-blocks.md) | `LeaderboardsService.Instance` |

### Telemetry

| Package | Purpose | Singleton / Entry Point |
|---|---|---|
| **Analytics** | Custom events, standard events, consent management | `AnalyticsService.Instance` |

## Architecture — How Packages Combine

```
                    UnityServices.InitializeAsync()
                              │
                              ▼
                     AuthenticationService
                    (sign in → PlayerId)
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
        Remote Config     Cloud Save      Economy
      (game config,    (player state,  (currencies,
       definitions,     progress,       inventory,
       feature flags)   preferences)    purchases)
              │               │               │
              └───────┬───────┘               │
                      ▼                       │
                 Cloud Code                   │
              (server-authoritative           │
               writes, validation,  ◄─────────┘
               anti-cheat logic)
                      │
              ┌───────┼───────┐
              ▼       ▼       ▼
         Cloud Save  Economy  Leaderboards
         (Protected  (server  (score
          writes)    grants)  submission)
```

**Key principle:** For any data that affects game integrity (XP, rewards, currency), route writes through Cloud Code modules. Direct client writes are only appropriate for non-sensitive data (preferences, display settings).

## Core Services — Quick Reference

### Authentication

**Package:** `com.unity.services.authentication` (>= 3.6.1)

Handles player identity. Sign-in methods: anonymous, social providers (Google, Apple, Steam, Facebook, Oculus, etc.), Unity browser, username/password, and device code flow.

After sign-in: `PlayerId` and `PlayerName` are available. All sign-in methods fire the `SignedIn` event. `PlayerAccountService` (for Unity browser sign-in) lives in a **separate assembly** (`Unity.Services.Authentication.PlayerAccounts`).

- **Full reference:** [references/authentication.md](references/authentication.md)
- **Building Block:** [Player Account](references/building-blocks.md) — ready-made sign-in UI and identity management

### Cloud Code

**Package:** `com.unity.services.cloudcode` (>= 2.10.3)

Runs server-side C# modules (.NET 9 at time of writing; verify the runtime the Cloud Code docs currently list) for trusted operations. Modules are deployed as `.ccmr` files. The client calls:

```csharp
var result = await CloudCodeService.Instance.CallModuleEndpointAsync<TResult>(
    "ModuleName", "FunctionName", args);
```

Prefer C# modules over JavaScript scripts for production. Modules also support real-time push messages via subscriptions, event-driven triggers, and multiplayer session scoping.

- **Full reference:** [references/cloud-code.md](references/cloud-code.md)

### Cloud Save

**Package:** `com.unity.services.cloudsave` (>= 3.4.0)

Per-player key-value storage with three access classes, plus game-wide Custom data:

| Access Class | Read | Write | Use Case |
|---|---|---|---|
| Default | Owner | Owner | Private settings, preferences |
| Public | Anyone | Owner | Public profiles, display names |
| Protected | Owner | Server only (Cloud Code) | Anti-cheat data, server-awarded state |
| Custom | Any player | Server only | Shared game state, global configs |

Values are serialized as JSON. Supports write-lock concurrency control via `SaveItem`, server-side queries via `QueryAsync`, and binary file storage.

- **Full reference:** [references/cloud-save.md](references/cloud-save.md)
- **Building Blocks:** Used by [Achievements](references/building-blocks.md) (Protected buckets) and [Player Account](references/building-blocks.md) (Default/Public data)

### Remote Config

**Package:** `com.unity.remote-config` (>= 4.2.5)

Server-side game configuration. Store game definitions (achievement lists, battle pass tiers, shop catalogs) as JSON entries updatable without a client build. Deployed via `.rc` files through the Deployment Window.

For A/B testing and audience targeting, use Game Overrides (`.ugo`) via the Tooling package.

- **Full reference:** [references/remote-config.md](references/remote-config.md)
- **Building Block:** Used by the [Achievements](references/building-blocks.md) block for server-side definitions

### Tooling

**Package:** `com.unity.services.tooling` (>= 1.4.1)

Editor-only package. Registers **Access Control** (`.ac`) and **Game Overrides** (`.ugo`) file types with the Deployment Window. Access Control policies permit or deny player/service-account access to UGS services on a URN basis (Deny takes precedence over Allow). Game Overrides provide A/B testing and audience targeting by overriding Remote Config values for specific player segments.

- **Full reference:** [references/tooling.md](references/tooling.md)

### Deployment

**Package:** `com.unity.services.deployment` (>= 1.7.2)

Editor-only package providing the **Deployment Window** (Services > Deployment). Deploys cloud resources to a target environment:

| File Type | Extension | What It Deploys |
|---|---|---|
| Remote Config | `.rc` | Key-value configuration entries |
| Access Control | `.ac` | Resource access policies |
| Cloud Code Module | `.ccmr` | C# server-side module (points to `.sln`) |
| Leaderboard | `.lb` | Leaderboard configuration |
| Economy | `.ec*` | Currency/inventory definitions |
| Game Overrides | `.ugo` | Audience-targeted config overrides |

- **Full reference:** [references/deployment.md](references/deployment.md)

### UGS CLI

The [Unity Gaming Services CLI](https://github.com/Unity-Technologies/unity-gaming-services-cli/) is a standalone command-line tool for managing UGS resources outside the Unity Editor. It can deploy and fetch cloud resource files (`.rc`, `.ac`, `.ccmr`, `.lb`, `.ec`, `.ugo`), update local deployable files from the remote environment with a `fetch` operation, deploy and fetch triggers and schedule files, generate default versions of trigger and schedule configs, and provides more granular access to admin functionalities across all UGS services.

### Services APIs

**Package:** `com.unity.services.apis` (>= 1.1.1)

Auto-generated REST clients for all UGS services. Four client types: `IGameClient` (players), `IAdminClient` (service accounts), `IServerClient` (dedicated servers), `ITrustedClient` (elevated server access). Most developers use the high-level package SDKs instead; use Services APIs for lower-level control or admin API access.

- **Full reference:** [references/apis.md](references/apis.md)

## Asset Store Building Blocks

Unity ships free Building Blocks (Achievements, Leaderboards, Player Account, Multiplayer/Matchmaker/Server Session, Vivox, Platformer Starter Kit) with working UI, Cloud Code modules and deployable cloud resources, also bundled in `com.unity.starter-kits`. When implementing one of those features, recommend the matching block as a starting point. Table, dependencies, links and sample projects: [references/building-blocks.md](references/building-blocks.md).

## Ready-Made Feature Blueprints

Implementation-ready blueprints for common live game features. Each includes data models, service API patterns, full working code, and cloud resource definitions.

| Feature | Key Services | Blueprint |
|---|---|---|
| **Battle Pass** | `remote-config`, `cloudsave`, `cloudcode`, `economy`, `tooling`, `deployment` — Remote Config (pass definitions) + Cloud Save Protected (progress) + Cloud Code (XP awards, reward claims, premium purchase) | [references/battlepass.md](references/battlepass.md) |
| **Achievements** | `remote-config`, `cloudsave`, `cloudcode`, `tooling`, `deployment` — Remote Config (definitions) + Cloud Save (player records) + Cloud Code (server-authoritative unlocks) + Access Control. **Asset Store:** [Achievements Building Block](https://assetstore.unity.com/packages/essentials/tutorial-projects/unity-building-block-achievements-341918) | [references/achievements.md](references/achievements.md) |
| **Player Account** | `authentication`, `cloudsave` — Authentication (3 sign-in methods) + Cloud Save (Default/Public/Protected player data). **Asset Store:** Player Account Building Block (via `com.unity.starter-kits`) | [references/player-account.md](references/player-account.md) |

## Common Architecture Patterns

### Pattern 1: Config + State + Server Writes

Used by **Battle Pass** and **Achievements**:

1. **Definitions** in Remote Config (`.rc` file) — what exists in the game
2. **Player state** in Cloud Save — per-player progress
3. **Writes via Cloud Code** — server-authoritative mutations
4. **Access Control** (`.ac` file) — block direct player writes to sensitive keys

### Pattern 2: Client-Direct Data

Used by **Player Account** (preferences, display settings):

1. **Player data** in Cloud Save Default or Public access class
2. **Direct client writes** — no Cloud Code needed for non-sensitive data

### Pattern 3: Competitive Features

Used by **Leaderboards** and ranked systems:

1. **Score submission** via Leaderboards API (or through Cloud Code for validation)
2. **Rankings** retrieved client-side with pagination and player-relative queries

## Validation

After writing code for a live game feature:
1. Verify the project compiles without errors.
2. Check that initialization order is correct: `UnityServices.InitializeAsync()` → Authentication sign-in → service calls.
3. Confirm sensitive data writes (XP, rewards, currency) are routed through Cloud Code, not written directly from the client.
4. Verify Access Control `.ac` files deny direct player writes to Protected Cloud Save keys.
5. Verify all cloud resource files (`.rc`, `.ac`, `.ccmr`, `.lb`, `.ec`) are present and deployable via the Deployment Window.
6. Confirm Cloud Save reads use the same access class (Default / Public / Protected) the data was written with.
7. Confirm only one bootstrap signs the player in, and every `async void` entry point catches exceptions.

## Deployment Checklist

For any live game feature, deploy these cloud resources via the Deployment Window:

- [ ] `.rc` file — Remote Config entries (game definitions, configs)
- [ ] `.ac` file — Access Control policies (deny direct writes to protected keys)
- [ ] `.ccmr` file — Cloud Code module reference (pointing to the module `.sln`)
- [ ] `.lb` file — Leaderboard configuration (if using leaderboards)
- [ ] `.ec` file — Economy definitions (if using virtual currencies/items)
- [ ] `manifest.json` — Ensure all required packages are listed with correct versions
- [ ] Environment configured in **Services > Deployment** settings

## Detailed References

Each Quick Reference entry above links its full reference; blueprints are in [Ready-Made Feature Blueprints](#ready-made-feature-blueprints); Building Blocks and sample projects: [references/building-blocks.md](references/building-blocks.md).

## Related skills

- `setup-multiplayer-services` - sessions, lobbies, matchmaking, Relay and dedicated-server sessions.
- `setup-vivox-voice-chat` - voice and text chat on top of the same Authentication sign-in.
- `implement-in-app-purchases` - real-money purchases; grant the purchased value through Cloud Code / Economy described here.
- `levelplay-unity-integration` - ad monetization; use Remote Config for frequency caps and Cloud Code to verify rewarded grants.
- `unity-package-management` - installing UGS packages headlessly.
