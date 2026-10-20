---
name: steam-lobbies-p2p
description: Builds friends-only Steam lobbies, invites and join flows for host-client co-op in Unity 6 with Steamworks.NET, and connects them to Netcode for GameObjects over Steam P2P (SteamNetworkingSockets transport, Steam Datagram Relay). Covers SteamMatchmaking.CreateLobby, LobbyCreated_t, LobbyEnter_t, SetLobbyData, ActivateGameOverlayInviteDialog, GameLobbyJoinRequested_t, GameRichPresenceJoinRequested_t, +connect_lobby launch arguments, build-version checks and host-quit handling. Use for "invite a Steam friend", "join through the Steam overlay", "host a co-op game on Steam", or a client that cannot reach the host by SteamID. Not for NGO RPCs, NetworkVariables or spawning (netcode-for-gameobjects), Unity Gaming Services sessions, Relay or join codes (setup-multiplayer-services), SteamAPI init, achievements or Steam Cloud (steamworks-integration), Steam builds and uploads (steam-build-release), saving the host's world (save-system) or voice chat (setup-vivox-voice-chat).
license: MIT
metadata:
  category: pc-steam
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://partner.steamgames.com/doc/features/multiplayer/matchmaking, https://partner.steamgames.com/doc/api/ISteamMatchmaking, https://partner.steamgames.com/doc/api/ISteamFriends, https://partner.steamgames.com/doc/api/ISteamApps, https://github.com/rlabrecque/Steamworks.NET/tree/master/com.rlabrecque.steamworks.net, https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/main/Transports/com.community.netcode.transport.steamnetworkingsockets, https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/main/Transports/com.community.netcode.transport.facepunch, https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/tree/develop-2.0.0/com.unity.netcode.gameobjects"
  unity: "6000.0+"
---

# Steam Lobbies and P2P Co-op

Topology: one player hosts (listen server), 2-4 friends join, no dedicated server, no host migration. The Steam lobby is only the meeting room; gameplay traffic goes through Netcode for GameObjects (NGO) over a Steam transport addressed by SteamID.

## Prerequisites

- Steamworks.NET pinned to `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1` and `SteamBootstrap` from `steamworks-integration` in the boot scene. The bootstrap owns `SteamAPI` init, `RunCallbacks()` every frame and `Shutdown`; this skill never initializes Steam itself and checks `SteamBootstrap.IsInitialized` instead. Without `RunCallbacks` no callback or call result below ever fires.
- NGO 2.x with a `NetworkManager` in the first scene (see `netcode-for-gameobjects`).
- A Steam transport on the `NetworkManager`: [references/transport-setup.md](references/transport-setup.md).
- Same AppID on every machine (480 Spacewar while prototyping, the real AppID before any external test).

## Workflow

1. **Host**: `SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers)` and register the returned `SteamAPICall_t` with a `CallResult<LobbyCreated_t>`. On `k_EResultOK` the host is already inside the lobby. Then:
   - `SetLobbyData(lobby, "host_steam_id", SteamUser.GetSteamID())` and `SetLobbyData(lobby, "build_version", Application.version)`. Only the owner can write lobby data.
   - `SteamFriends.SetRichPresence("connect", "+connect_lobby <lobbyId>")` so friends can join from the friends list.
   - `NetworkManager.StartHost()`. If it fails, `LeaveLobby` and report.
2. **Invite**: `SteamFriends.ActivateGameOverlayInviteDialog(lobbyId)`. Needs the Steam overlay; it often does not appear in the Editor, so test invites from a build launched through Steam.
3. **Join requests**, all ending in `SteamMatchmaking.JoinLobby(lobbyId)`:
   - Game running, invite or "Join Game" on a lobby: `Callback<GameLobbyJoinRequested_t>` gives `m_steamIDLobby`.
   - Game running, join through rich presence: `Callback<GameRichPresenceJoinRequested_t>` gives the `connect` string in `m_rgchConnect`; parse `+connect_lobby <id>`.
   - Game not running: Steam launches it with `+connect_lobby <id>` on the command line. Read `Environment.GetCommandLineArgs()` at startup, and also `SteamApps.GetLaunchCommandLine` if the app has "Use launch command line" enabled in Steamworks.
4. **Enter**: in the `CallResult<LobbyEnter_t>` check `(EChatRoomEnterResponse)m_EChatRoomEnterResponse == k_EChatRoomEnterResponseSuccess` (a full lobby returns `k_EChatRoomEnterResponseFull`). Lobby data is readable immediately.
5. **Validate before connecting**: compare `build_version` with `Application.version`; on mismatch `LeaveLobby` and tell the player. Read the host from `host_steam_id` (fall back to `GetLobbyOwner`, which changes if the original owner leaves).
6. **Connect**: set the transport target to the host SteamID, then `NetworkManager.StartClient()`. NGO connection approval on the host is the second gate (player cap, version, ban list) - see `netcode-for-gameobjects`.
7. **Host quits**: clients get `NetworkManager.OnClientStopped`; read `NetworkManager.DisconnectReason`, `LeaveLobby`, clear rich presence, return to the menu with a message. Steam hands lobby ownership to another member automatically, but the NGO session is gone; do not treat the new lobby owner as a new host.
8. **Leave**: on every exit path call `NetworkManager.Shutdown()`, `SteamMatchmaking.LeaveLobby(lobbyId)` and `SteamFriends.SetRichPresence("connect", "")`.

Reference implementation: [scripts/SteamLobbyService.cs](scripts/SteamLobbyService.cs) - read when wiring a menu to host, invite, join, launch-argument join, version check and host-quit handling. It requires `SteamBootstrap` (from `steamworks-integration/scripts`) in the project and talks to the transport only through the `BeforeStartClient` event, so it compiles without any transport package:

```csharp
lobbyService.BeforeStartClient += hostId => steamTransport.ConnectToSteamID = hostId.m_SteamID;
```

## Rules

- Keep every `Callback<T>` and `CallResult<T>` in a field for as long as it must fire; a local is collected and the callback silently stops. Dispose them in `OnDisable`.
- `CreateLobby` and `JoinLobby` are asynchronous. Never read lobby data or start NGO on the line after the call.
- Guard Steam calls with `SteamBootstrap.IsInitialized` so the game still runs without Steam (offline dev builds, Multiplayer Play Mode clones).
- Lobby data is public to anyone who can see the lobby. Never store tokens or IPs there.
- A user can be in only one regular lobby. Leave the current one before hosting or joining another.
- `k_ELobbyTypePrivate` lobbies are invite-only; `FriendsOnly` lets friends join from the friends list; `Public` shows in `RequestLobbyList` (use it only with a matchmaking UI and filters).
- Wrap Steamworks code in `#if !DISABLESTEAMWORKS` and define `DISABLESTEAMWORKS` for platforms Steamworks.NET does not support, as the reference script does.
- `async void` only on UI button handlers, with try/catch. Lobby calls are callback-based, not `Task`-based.

## References

- [references/lobby-api.md](references/lobby-api.md): read when choosing a lobby type, reading or writing lobby or member data, sending lobby chat, listing lobbies, or mapping a callback to its struct fields.
- [references/transport-setup.md](references/transport-setup.md): read when installing or configuring the SteamNetworkingSockets or Facepunch transport, checking NGO 2.x compatibility, or planning a custom Steam transport.
- [references/testing.md](references/testing.md): read when testing co-op locally without Steam, testing real Steam invites, or debugging a client that never connects.

## Pitfalls

| Symptom | Fix |
|---|---|
| No callback ever fires | `SteamAPI.RunCallbacks()` not called each frame, or the `Callback<T>` / `CallResult<T>` was a local and got collected |
| Invite accepted but nothing happens in game | `GameLobbyJoinRequested_t` not registered, or registered after the menu scene loaded; register it in a persistent object |
| Game launched from an invite stays in the main menu | `+connect_lobby` not parsed at startup |
| Client joins the lobby but `StartClient` never connects | Transport target not set to the host SteamID, host has not called `StartHost`, or both peers are the same Steam account |
| "Version mismatch" on every join | `Application.version` differs between builds; bump it in Player Settings for every release and compare the same field on both sides |
| Lobby owner changes after the host leaves | Expected Steam behaviour; end the session, do not reconnect to the new owner |
| Host overlay shows no invite dialog | Overlay disabled, game launched outside Steam, or running in the Editor |
| Lobby rejoined after quitting | `LeaveLobby` and rich presence clearing missing on the quit path |

## Related skills

- `steamworks-integration`: SteamAPI init, `RunCallbacks`, rich presence display strings, overlay pause.
- `netcode-for-gameobjects`: NetworkManager, connection approval, authority, RPCs, NetworkVariables, scenes.
- `setup-multiplayer-services`: Unity Gaming Services sessions, Relay and join codes when the game is not Steam-only.
- `save-system`: host-owned world saves for co-op.
- `setup-vivox-voice-chat`: in-game voice chat.
- `steam-build-release`: builds for testing invites through a Steam branch.
