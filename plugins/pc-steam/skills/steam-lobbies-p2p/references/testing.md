# Testing Steam co-op

Read when testing co-op locally without Steam, testing real Steam invites, or debugging a client that never connects.

Test in two layers. Gameplay netcode runs on `UnityTransport` inside one machine; Steam lobbies, invites and the Steam transport need real Steam accounts.

## Layer 1: local, no Steam

Use Multiplayer Play Mode (`com.unity.multiplayer.playmode`): the main Editor plus up to three virtual players on one machine, sharing the project's assets. Version line by Editor: 1.6.x declares Unity 6000.0, 2.0.x declares 6000.3 or newer; let Package Manager pick the compatible one.

1. Give the `NetworkManager` a `UnityTransport` for this mode (address 127.0.0.1, any free port).
2. Skip the Steam path in Play Mode. Every virtual player runs under the same Steam account, so lobbies and P2P to "yourself" cannot be tested this way.
3. Start host or client per player with MPPM player tags:

```csharp
using System.Linq;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using UnityEngine;

public class LocalSessionStarter : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    private void Start()
    {
        if (!Application.isEditor) return;
        var tags = CurrentPlayer.ReadOnlyTags();
        if (tags.Contains("Host")) this.networkManager.StartHost();
        else if (tags.Contains("Client")) this.networkManager.StartClient();
    }
}
```

The `Unity.Multiplayer.Playmode` runtime assembly is auto-referenced, so `Assembly-CSharp` sees it; scripts inside your own asmdef need an explicit reference to it.

4. Add latency and loss with the Network Simulator from `com.unity.multiplayer.tools`. `UnityTransport.DebugSimulator` is obsolete in NGO 2.x and has no effect.

What layer 1 proves: authority, RPCs, NetworkVariables, spawning, scene loads, late join, client and host disconnect handling. See `netcode-for-gameobjects` → `references/testing.md`.

## Layer 2: real Steam

Requirements:

- Two Steam accounts that are friends, on two machines (one Steam client runs one logged-in account per machine).
- Both accounts own the app, or both use AppID 480 for early tests. With 480, filter any lobby search on a game-specific key; every developer shares that AppID.
- Same build on both machines (same `Application.version`).
- To test "accept invite while the game is not running", the game must be installed through Steam on the invitee's machine (upload to a private branch, see `steam-build-release`). A loose build with `steam_appid.txt` can use the API but Steam will not launch it from an invite.

Test matrix, run before every external playtest:

| Case | Expected |
|---|---|
| Host creates lobby, opens invite overlay | Overlay friend list appears (build launched through Steam) |
| Invitee accepts while in main menu | `GameLobbyJoinRequested_t` → join → `StartClient` → player spawns |
| Invitee accepts while game is closed | Game launches with `+connect_lobby <id>` and joins without extra clicks |
| Join from friends list "Join Game" | Lobby join or rich presence `connect` path works |
| Different `Application.version` | Client refuses before `StartClient` with a clear message |
| Lobby full | `k_EChatRoomEnterResponseFull` shown as "session full" |
| Client quits from menu | Host sees disconnect, player object despawns, lobby member count drops |
| Host quits from menu | Clients return to menu with a message and leave the lobby |
| Host process killed (Task Manager) | Clients time out, return to menu; no stuck loading screen |
| Client network cable pulled for 30 s | Client times out cleanly; host keeps running |
| Rejoin after leaving | Works without restarting the game |

## Client never connects

1. Both peers: `SteamAPI` initialized and `RunCallbacks` running (lobby callbacks arrive at all?).
2. Client: transport target equals the host's SteamID64, read from lobby data, not the client's own id.
3. Host: `StartHost` returned true before the client started.
4. Set `NetworkManager` Log Level to Developer; the SteamNetworkingSockets transport logs connection state changes only at that level.
5. `SteamNetworkingUtils.GetRelayNetworkStatus(out SteamRelayNetworkStatus_t details)` on both sides: relay access must be available before P2P works.
6. Connection approval: the NGO config hash (protocol version, tick rate, prefab list when `ForceSamePrefabs` is on, scene management flag) must match, otherwise the host rejects the client during the handshake.
7. If relay status never becomes available on one machine, retest that machine on a different network; restrictive networks may block Valve's relay.
