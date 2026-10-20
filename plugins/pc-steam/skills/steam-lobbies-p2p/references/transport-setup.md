# Steam transport for NGO

Read when installing or configuring the SteamNetworkingSockets or Facepunch transport, checking NGO 2.x compatibility, or planning a custom Steam transport.

## Options

| Transport | Package (in `Unity-Technologies/multiplayer-community-contributions`) | Steam wrapper | Use when |
|---|---|---|---|
| SteamNetworkingSockets | `Transports/com.community.netcode.transport.steamnetworkingsockets` | Steamworks.NET (you install it) | Default for this skill: shares the Steamworks.NET init, lobbies and achievements code |
| Facepunch | `Transports/com.community.netcode.transport.facepunch` | Facepunch.Steamworks 2.3.2 DLLs bundled in the package | The project already uses Facepunch.Steamworks instead of Steamworks.NET |
| Custom `NetworkTransport` | your code | Steamworks.NET | The community package fails to compile or misbehaves on your NGO version (see the fallback section) |

Pick one Steam wrapper per project. Facepunch.Steamworks and Steamworks.NET each ship their own `steam_api` native libraries and each expects to own `SteamAPI` initialization; installing both invites duplicate-plugin and double-init problems.

`UnityTransport` stays in the project for local testing (see `testing.md`). Swap the `NetworkManager` transport by build configuration or by a menu toggle, not by editing the prefab before each build.

## Install the SteamNetworkingSockets transport

1. Steamworks.NET, pinned to the release used across this plugin (install details and the reason for the pin are in `steamworks-integration`):
   `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1`
   The transport README shows the unpinned URL; do not use it, `master` tracks a newer SDK.
2. The transport, same dialog:
   `https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.steamnetworkingsockets`
   The repo has no per-transport release tags; pin a commit hash with `#<commit>` so an upstream change cannot break a release build.
3. On the `NetworkManager`, set Network Transport to none, then pick the transport in the Select Transport dropdown (or add the component and assign it).

What the package contains (verified against the `main` branch, last code change 2026-09-08):

- Component `Netcode.Transports.SteamNetworkingSocketsTransport : NetworkTransport`.
- Public fields: `ulong ConnectToSteamID` (the host's SteamID64, required on clients) and `SteamNetworkingConfigValue_t[] options` (passed to `CreateListenSocketP2P` / `ConnectP2P`).
- The whole file is wrapped in `#if !DISABLESTEAMWORKS && STEAMWORKSNET`; `STEAMWORKSNET` comes from an asmdef version define on `com.rlabrecque.steamworks.net`, so the component does not exist until Steamworks.NET is installed through UPM under that package name.
- asmdef platforms: Editor, Windows 32/64, Linux 64, macOS.
- It does not initialize Steam. `IsSupported` calls `InteropHelp.TestIfAvailableClient()`; `SteamBootstrap` (see `steamworks-integration`) must have initialized `SteamAPI` before `StartHost` / `StartClient`.

## How it connects

- Host: `StartServer` calls `SteamNetworkingSockets.CreateListenSocketP2P(0, ...)` (virtual port 0) and accepts every incoming connection in `PollEvent`. Approval is left to NGO connection approval.
- Client: `StartClient` calls `SteamNetworkingUtils.InitRelayNetworkAccess()` and `SteamNetworkingSockets.ConnectP2P` to `ConnectToSteamID` on virtual port 0. Traffic is routed through Valve's relay network, so no port forwarding or NAT punch is needed.
- Transport client ids are SteamID64 values; `ServerClientId` is 0. NGO maps them to its own client ids, so do not assume `OwnerClientId` equals a SteamID. Send the SteamID in `NetworkConfig.ConnectionData` if the host needs it during approval.
- Delivery mapping: `Reliable` and `ReliableFragmentedSequenced` → `k_nSteamNetworkingSend_Reliable`; `ReliableSequenced` → `ReliableNoNagle`; `UnreliableSequenced` → `UnreliableNoNagle`; otherwise unreliable.

Set the target right before `StartClient`:

```csharp
var transport = (SteamNetworkingSocketsTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
transport.ConnectToSteamID = hostSteamId.m_SteamID;
NetworkManager.Singleton.StartClient();
```

`SteamLobbyService` raises `BeforeStartClient(CSteamID host)` for exactly this assignment.

## NGO 2.x compatibility status

Facts from the source:

- `package.json` says version `1.0.1`, `"unity": "2020.3"`, while its CHANGELOG lists 2.0.1 (2021). The repo README compatibility table has columns for NGO 1.0.0, 0.1.0 and v12 only; there is no NGO 2.x column.
- The repo README states it does not guarantee support for future NGO versions and that Unity does not fix bugs in community content.
- The transport overrides every abstract member of `NetworkTransport` in NGO 2.13.3 (`ServerClientId`, `Send`, `PollEvent`, `StartClient`, `StartServer`, `DisconnectRemoteClient`, `DisconnectLocalClient`, `GetCurrentRtt`, `Shutdown`, `Initialize(NetworkManager)`) with matching signatures, and its asmdef GUID references resolve to `Unity.Netcode.Runtime` and Steamworks.NET. It should therefore compile against NGO 2.x.
- The 2026-09-08 commit added an asmdef reference named `Unity.Networking.Transport.NetcodeInterop`. No assembly with that name exists in NGO 2.13.3 or `com.unity.transport` 2.7.4; it appears to target the newer NGO 3.x stack (Unity 6000.7+).

Not confirmed (check in your project):

- Whether the unresolved `Unity.Networking.Transport.NetcodeInterop` reference produces an error or only a warning in your Editor version. If it blocks compilation, embed the package (copy it into `Packages/`) and remove that one reference.
- Whether it behaves correctly at runtime with NGO 2.x. Unity does not run it in CI; test host, join, late join, client quit and host quit between two Steam accounts before relying on it.

Limitations visible in the source:

- `GetCurrentRtt` always returns 0, so NGO RTT readouts and anything derived from them are meaningless on this transport.
- Each received message allocates a new `byte[]` and the receive loop reads one message per connection per poll (NGO keeps polling until `NetworkEvent.Nothing`, so throughput is fine at co-op scale).
- `Shutdown` closes connections from a coroutine on `NetworkManager.Singleton` 0.1 s later; do not destroy the `NetworkManager` in the same frame you shut down.

## Facepunch transport

- Component `Netcode.Transports.Facepunch.FacepunchTransport`; fields `steamAppId` (serialized, default 480) and `targetSteamId` (public `ulong`, the host to join).
- `Initialize` calls `SteamClient.Init(steamAppId, false)` itself if Steam is not valid yet, and `Shutdown` calls `SteamClient.Shutdown()`.
- Uses `SteamNetworkingSockets.CreateRelaySocket` / `ConnectRelay` from Facepunch.Steamworks.
- Its CHANGELOG (2.0.0) says it targets NGO 1.0.0; NGO 2.x support is not stated. Same verification steps as above.
- `SteamLobbyService` in this skill is written for Steamworks.NET; with Facepunch, port the same flow to Facepunch.Steamworks' own lobby API and event model (not covered here).

## Fallback: a minimal custom transport

If the community package cannot be made to work, write a `NetworkTransport` over Steamworks.NET yourself. Keep it small and test it hard; do not paste a large untested implementation.

Members to override (NGO 2.13.3): `ulong ServerClientId { get; }`, `bool IsSupported` (virtual), `void Initialize(NetworkManager networkManager = null)`, `bool StartServer()`, `bool StartClient()`, `void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery)`, `NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)`, `void DisconnectRemoteClient(ulong clientId)`, `void DisconnectLocalClient()`, `ulong GetCurrentRtt(ulong clientId)`, `void Shutdown()`.

Connection-oriented design with `ISteamNetworkingSockets`:

1. Host: `CreateListenSocketP2P(0, 0, null)`, plus `CreatePollGroup()`.
2. Client: `SteamNetworkingUtils.InitRelayNetworkAccess()`, then `ConnectP2P(ref identity, 0, 0, null)` with `identity.SetSteamID(host)`.
3. Keep a `Callback<SteamNetConnectionStatusChangedCallback_t>` in a field; queue its events. On `k_ESteamNetworkingConnectionState_Connecting` (host side) call `AcceptConnection` and `SetConnectionPollGroup`; on `Connected` return `NetworkEvent.Connect`; on `ClosedByPeer` or `ProblemDetectedLocally` call `CloseConnection` and return `NetworkEvent.Disconnect`.
4. `Send`: copy the payload to a pinned or pooled buffer and call `SendMessageToConnection` with reliable or unreliable flags from the `NetworkDelivery`.
5. `PollEvent`: return queued connection events first, then one message from `ReceiveMessagesOnPollGroup` (host) or `ReceiveMessagesOnConnection` (client), copy `m_pData` into a reusable buffer and `SteamNetworkingMessage_t.Release` the pointer; return `NetworkEvent.Nothing` when empty.
6. `GetCurrentRtt`: `GetConnectionRealTimeStatus` and read `m_nPing`.
7. `Shutdown`: close every connection, the poll group and the listen socket, and dispose the callback.

Connectionless alternative: `ISteamNetworkingMessages` (`SendMessageToUser`, `ReceiveMessagesOnChannel`, `AcceptSessionWithUser` on `SteamNetworkingMessagesSessionRequest_t`). Simpler, but you must invent connect and disconnect events for NGO yourself; prefer the sockets design.

Verify field and enum names against the Steamworks.NET version you install; the transport source linked in the frontmatter is a working reference for every call above except poll groups and RTT.
