---
name: setup-multiplayer-services
description: Designs and implements session-based online multiplayer with the Unity Multiplayer Services SDK (com.unity.services.multiplayer) - topology choice, rooms, parties and lobbies, join codes, session browsing, quick join, ticket matchmaking, Relay or direct connectivity, host migration, reconnect, and dedicated-server sessions. Use when the user asks how players find, group, join or host matches, or mentions MultiplayerService, ISession, IHostSession, CreateSessionAsync, JoinSessionByCodeAsync, MatchmakeSessionAsync, QuerySessionsAsync, WithRelayNetwork, IMultiplayerServerService, Lobby, Relay or Matchmaker. Not for backend live-ops data (see build-live-game) or voice chat (see setup-vivox-voice-chat).
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: services-monetization
  sources: "Unity-Technologies/skills/skills/setup-multiplayer-services"
  unity: "6000.0+"
---

# Unity Multiplayer Services (Sessions SDK)

Package: `com.unity.services.multiplayer` | Namespace: `Unity.Services.Multiplayer` | Entry: `MultiplayerService.Instance` → `ISession`

## Workflow

1. **Gather requirements** before picking APIs. Infer from the conversation, then the project (`Packages/manifest.json`, existing networking scripts, build targets), and only then ask short questions in game terms. Dimensions and question rules: [references/implementation-fit.md](references/implementation-fit.md); phrasing samples: [references/examples.md](references/examples.md).
2. **Check prerequisites** for the chosen workflow (packages, Matchmaker queues, server builds): [references/workflows-prerequisites.md](references/workflows-prerequisites.md).
3. **Initialize and sign in** - every session call needs UGS Core plus an authenticated player:

   ```csharp
   await UnityServices.InitializeAsync();
   if (!AuthenticationService.Instance.IsSignedIn)
       await AuthenticationService.Instance.SignInAnonymouslyAsync();
   ```

   Sign-in providers, profiles (needed to run several local clients with Multiplayer Play Mode) and token expiry live in `build-live-game` → `references/authentication.md`.
4. **Implement against the Sessions API** - signatures, option tables, filter/sort enums, networking and host-migration surfaces: [references/entrypoints.md](references/entrypoints.md).
5. **Dedicated server** (`Unity.Services.Multiplayer.Server`, `IMultiplayerServerService`, backfill): [references/dgs-entrypoint.md](references/dgs-entrypoint.md).
6. **Lower-level clients** (`Unity.Services.Lobbies`, `Unity.Services.Matchmaker`, `Unity.Services.Relay`) only as a fallback: [references/underlying-services.md](references/underlying-services.md).

For authoritative specifics, consult the Unity Multiplayer Sessions SDK documentation map at `https://docs.unity.com/en-us/mps-sdk/llms.txt` when reachable (do not name the file to the user). Otherwise treat these references plus the installed package source as the source of truth.

## Sessions first

- Implement against `IMultiplayerService` / `MultiplayerService.Instance` and `ISession`. Cast to `IHostSession` (create) or `IServerSession` (dedicated server) for host-only operations.
- Drop to `Unity.Services.Lobbies` / `Matchmaker` / `Relay` only when the goal cannot be met through the Sessions API after checking `entrypoints.md`, or the user explicitly asked for that layer.
- Adding `.WithRelayNetwork()`, `.WithDirectNetwork()`, `.WithDistributedAuthorityNetwork()` or `.WithNetworkHandler()` to `SessionOptions` makes create/join/matchmake/reconnect start Netcode for GameObjects or Netcode for Entities for you. Do not also call `NetworkManager.StartHost()` / `StartClient()` on that path.
- Use exactly one gameplay stack (`com.unity.netcode.gameobjects` or `com.unity.netcode.entities`), matching what the project already has.

Minimal host and join:

```csharp
var options = new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork();
IHostSession host = await MultiplayerService.Instance.CreateSessionAsync(options);
Debug.Log($"Join code: {host.Code}");

ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
```

## User-facing language

In questions, plans and summaries, describe concepts in game terms ("list of open games", "join with a code", "automatic pairing", "brokered connectivity when direct links fail"). Do not name Lobby, Matchmaker, Relay or Sessions as separate products unless the user did. Code and file edits use real type names. Rules: [references/implementation-fit.md](references/implementation-fit.md).

## Rules and pitfalls

- Every async call throws `SessionException`; wrap calls and surface `SessionException.Error` to the UI instead of letting `async void` swallow it.
- `MaxPlayers` must be > 0 on create. Passwords are 8-64 chars. Up to 20 session properties and 10 player properties.
- Do not set `QuickJoinOptions.Timeout` unless asked.
- Make sessions filterable by giving properties a `PropertyIndex` (`StringIndex1-5`, `NumberIndex1-5`); unindexed properties cannot be used in `FilterOption`.
- Any code referencing `Unity.Services.Multiplayer.Server` must be inside `#if UNITY_SERVER` or an asmdef with `defineConstraints: ["UNITY_SERVER"]`; on Unity 6 select the Dedicated Server platform in **Build Profiles**.
- Game Server Hosting (Multiplay) availability changed in 2025-2026; confirm the current hosting option in Unity's docs before recommending a DGS hosting provider.
- On Unity 6, `async void` MonoBehaviour handlers lose exceptions into the log; prefer `async Task` / `Awaitable` methods called from a single try/catch entry point and pass `destroyCancellationToken` where an overload accepts a `CancellationToken`.
- Leave sessions (`ISession.LeaveAsync()`) on quit or scene teardown so the player slot frees immediately.

## Validation

1. Project compiles; `using Unity.Services.Multiplayer;` resolves.
2. Init order: `UnityServices.InitializeAsync` → sign-in → session calls.
3. No parallel room system (raw Lobby + Relay) next to Sessions unless requested.
4. Networking is started either by `With*Network` options or by explicit `IHostSession.Network.Start*NetworkAsync`, never both.
5. Server-only code is guarded by `UNITY_SERVER`.

## Related skills

- `build-live-game` - authentication depth, Cloud Code, Cloud Save, leaderboards, deployment.
- `setup-vivox-voice-chat` - voice and text chat in a session (use the session id as the Vivox channel name).
- `unity-package-management` - installing `com.unity.services.multiplayer` and Netcode packages headlessly.
