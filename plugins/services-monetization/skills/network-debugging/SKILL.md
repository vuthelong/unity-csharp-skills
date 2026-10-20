---
name: network-debugging
description: Diagnoses and reproduces online co-op problems in Netcode for GameObjects 2.x on Unity 6 - Network Simulator presets, lag spikes and disconnects (com.unity.multiplayer.tools), Network Profiler modules and the Runtime Network Stats Monitor, bandwidth budgets for NetworkVariables, RPCs and NetworkTransform, desync and missing-object triage (authority mismatch, state lost for late joiners, in-scene object and NetworkPrefab hash errors, NetworkConfig mismatch), NetworkManager LogLevel, Multiplayer Play Mode repros under latency, and Steam P2P checks (SDR relay vs direct, connection end reasons, Steam fake lag, two-account tests). Use for "works on LAN but breaks online", rubber-banding, values that differ between players, objects missing on clients, clients dropping, bandwidth spikes, or adding a net stats HUD. Not for NGO API basics (netcode-for-gameobjects) or Steam lobby, invite and transport setup (steam-lobbies-p2p).
license: MIT
metadata:
  category: services-monetization
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/manual/network-simulator.html, https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/manual/runtime-stats-monitor.html, https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/manual/profiler.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/logging.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/troubleshooting/troubleshooting.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/learn/ticks-and-update-rates.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/networktime-ticks.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/networkvariable.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/helper/networktransform.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/tutorials/testing/techniques_and_tricks_for_debugging_multiplayer_games.html, https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/tree/develop-2.0.0/com.unity.netcode.gameobjects, https://docs.unity3d.com/Packages/com.unity.transport@2.7/manual/index.html, https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@1.6/manual/index.html, https://partner.steamgames.com/doc/api/steamnetworkingtypes, https://partner.steamgames.com/doc/api/ISteamNetworkingSockets, https://partner.steamgames.com/doc/api/ISteamNetworkingUtils, https://github.com/rlabrecque/Steamworks.NET/tree/master/com.rlabrecque.steamworks.net"
  unity: "6000.0+"
---

# Network Debugging (NGO co-op)

Package facts below are from NGO 2.13.3, Multiplayer Tools 2.2.12 and Unity Transport 2.7.4 sources. Check `Packages/manifest.json` and re-check names if your versions differ. NGO 3.x (Unity 6000.7+) moves `NetworkTime` to `Unity.Netcode.GameObjects.Timing` and turns some obsolete APIs into errors; see `netcode-for-gameobjects` before reusing this skill's code there.

## Workflow

1. **Collect evidence from every peer.** Set `NetworkManager` **Log Level** to Developer (`networkManager.LogLevel = LogLevel.Developer` before `StartHost`/`StartClient`) on host and clients. Many NGO warnings, including `NetworkConfig` mismatch, only print at Normal or Developer. Collect the host's and each client's `Player.log`, not only the Editor Console.
2. **Classify the symptom** with the triage table below: never connects, connects then drops, objects missing, values differ (desync), jitter or rubber-banding, bandwidth spikes.
3. **Reproduce locally under bad conditions.** Multiplayer Play Mode (main Editor plus virtual players) on `UnityTransport` at 127.0.0.1, plus the Network Simulator. Start with **Home Broadband** (32 ms delay, 12 ms jitter, 2 % loss), then a custom 150 ms / 30 ms / 3 % preset. Details: [references/simulate-conditions.md](references/simulate-conditions.md).
4. **Measure.** Network Profiler modules for bytes per message type and per `NetworkObject`; the Runtime Network Stats Monitor or [scripts/NetStatsHud.cs](scripts/NetStatsHud.cs) for RTT, ticks and connection events on screen.
5. **Fix, then rerun the feature checklist** in `netcode-for-gameobjects` (`references/testing.md`) under the same simulated conditions.
6. **Confirm on the real transport.** Two Steam accounts on two machines, same build. The Network Simulator does nothing on a Steam transport. Read [references/steam-p2p-checks.md](references/steam-p2p-checks.md).

## Triage

| Symptom | First checks | Read |
|---|---|---|
| Client never connects | Host listening? Transport target set? `NetworkConfig` mismatch warning on host? Steam relay status? | [steam-p2p-checks.md](references/steam-p2p-checks.md), [desync-and-errors.md](references/desync-and-errors.md) |
| Connects, then drops | `DisconnectReason` on client, `OnTransportFailure`, Steam end reason, timeouts vs loss | [steam-p2p-checks.md](references/steam-p2p-checks.md) |
| Object missing on clients | Prefab registered? Spawned on server? In-scene object hash errors? | [desync-and-errors.md](references/desync-and-errors.md) |
| Value differs between peers | Two writers, client-side writes without permission, collection mutated without `CheckDirtyState()` | [desync-and-errors.md](references/desync-and-errors.md) |
| Late joiner sees old state | State delivered only by RPC | [desync-and-errors.md](references/desync-and-errors.md) |
| Rubber-banding, jitter | Authority mode, interpolation, tick rate, RTT variance | [simulate-conditions.md](references/simulate-conditions.md), [bandwidth.md](references/bandwidth.md) |
| Hitch when many things change | Large `NetworkVariable` payloads, spawn bursts, RPC per frame | [bandwidth.md](references/bandwidth.md) |

## Log strings to search for

Quoted from NGO 2.13.3 source; NGO adds its own prefix and context to each line, so search for a substring.

| Text in the log | Meaning |
|---|---|
| `NetworkConfig mismatch. The configuration between the server and client does not match` | Host rejected a client: protocol version, NGO protocol, tick rate, connection approval flag, `ForceSamePrefabs` and prefab list, scene management flag, `EnsureNetworkVariableLengthSafety` or RPC hash size differ between builds |
| `Failed to create object locally. NetworkPrefab could not be found. Is the prefab registered with this NetworkManager` | Client got a spawn for a prefab hash it does not know: prefab list differs or the prefab is missing from the client build |
| `NetworkPrefab hash was not found! In-Scene placed NetworkObject soft synchronization failure!` | In-scene placed object exists on the host but not in the client's copy of the scene |
| `In-Scene NetworkObject GlobalObjectIdHash (<hash>) could not be found!` | Same family: scene contents or scene versions differ |
| `Detected NetworkObject GlobalObjectIdHash value of 0!` | A `NetworkObject` created at runtime (not from a prefab asset) was spawned |
| `Write permissions (<perm>) for this client instance is not allowed!` | A peer without write permission set a `NetworkVariable` |
| `has a duplicate GlobalObjectIdHash source entry value of` | Two prefab list entries share a hash (duplicated or overridden prefab) |

## Bandwidth in one paragraph

The host sends every replicated change to every client, so host upload grows with (clients x replicated state x update rate). `NetworkVariable` changes go out on network ticks (`NetworkConfig.TickRate`, default 30), not every frame; RPCs go out when called. Open the Profiler with the **NGO Messages** and **NGO Objects** modules, sort by bytes, and fix the biggest sender first: smaller types, fewer axes and thresholds on `NetworkTransform`, `NetworkVariableUpdateTraits` throttling, events instead of per-frame RPCs. Numbers and settings: [references/bandwidth.md](references/bandwidth.md).

## Network Simulator from code

```csharp
using System;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using UnityEngine;

public sealed class NetSimShortcuts : MonoBehaviour
{
    [SerializeField] private NetworkSimulator simulator;

    public void UseBadWifi()
    {
        simulator.ChangeConnectionPreset(NetworkSimulatorPreset.Create("Bad Wi-Fi", packetDelayMs: 150, packetJitterMs: 30, packetLossPercent: 3));
    }

    public void UseHomeBroadband() => simulator.ChangeConnectionPreset(NetworkSimulatorPresets.HomeBroadband);

    public void LagSpike() => simulator.TriggerLagSpike(TimeSpan.FromSeconds(2));

    public void DropConnection() => simulator.Disconnect();

    public void RestoreConnection() => simulator.Reconnect();
}
```

Only works with `UnityTransport`, and only in the Editor and development builds unless you add the define described in [references/simulate-conditions.md](references/simulate-conditions.md). Check `simulator.IsAvailable` before relying on it.

## Rules

- Debug with `UnityTransport` and the Network Simulator first; switch to the Steam transport only to test Steam-specific behaviour. Keep both transports selectable without editing the `NetworkManager` prefab.
- Test every feature at least once at about 150 ms RTT with jitter and 2-5 % loss, and once with a 2-3 second lag spike.
- Give every replicated value exactly one writer. Server-authoritative state is written only on the server; owner-authoritative state only by the owner.
- Anything a late joiner must see lives in `NetworkVariable`s, `NetworkList`s or spawned objects.
- Use `NetworkManager.ServerTime` / `LocalTime` for networked timers, not `Time.time` on each peer.
- Do not ship with Log Level Developer; reset it to Normal for release builds.
- No `async void` in debug tooling except UI button handlers wrapped in try/catch.

## Pitfalls

| Symptom | Cause / fix |
|---|---|
| Network Simulator changes nothing | Transport is not `UnityTransport`, or a release build without the netsim define |
| `UnityTransport.SetDebugSimulatorParameters` / `DebugSimulator` has no effect | Both are obsolete in NGO 2.x and do nothing; use the Network Simulator |
| RTT shows 0 | The Steam community transport's `GetCurrentRtt` always returns 0; read Steam's own connection status instead |
| Host never rejects anyone but clients silently fail | Log Level Error or Nothing hides the `NetworkConfig` mismatch warning; raise it to Developer |
| Works in MPPM, fails between two PCs | Firewall or bind address on `UnityTransport`, or the real transport behaves differently; see `netcode-for-gameobjects` testing and [steam-p2p-checks.md](references/steam-p2p-checks.md) |
| All MPPM players show the same Steam identity | Expected; Steam binds to one account per machine. Test Steam with two machines |
| Bandwidth spikes when a list changes | `NetworkList` or a managed `NetworkVariable` replaced wholesale; mutate elements, or split state |

## References

- [references/simulate-conditions.md](references/simulate-conditions.md): read when setting up the Network Simulator, choosing presets, scripting lag spikes or disconnects, enabling it in builds, or simulating conditions in Multiplayer Play Mode.
- [references/bandwidth.md](references/bandwidth.md): read when measuring bytes with the Network Profiler or Runtime Network Stats Monitor, setting a bandwidth budget, or reducing NetworkVariable, RPC and NetworkTransform traffic.
- [references/desync-and-errors.md](references/desync-and-errors.md): read when state differs between peers, objects are missing or duplicated, a late joiner sees stale state, or a log line mentions hashes, NetworkConfig or write permissions.
- [references/steam-p2p-checks.md](references/steam-p2p-checks.md): read when a Steam P2P connection fails or drops, to tell relay from direct routes, read connection end reasons, add Steam fake lag, or plan two-account tests.
- [scripts/NetStatsHud.cs](scripts/NetStatsHud.cs): read when you need an on-screen NGO readout (role, transport, tick, RTT per client, connection events, disconnect reason) without the Multiplayer Tools package. TextMeshPro output, no per-frame GC, disabled in non-development builds by default.

## Related skills

- `netcode-for-gameobjects`: NGO setup, authority, RPCs, NetworkVariables, spawning, Multiplayer Play Mode basics.
- `steam-lobbies-p2p`: Steam lobbies, invites, the SteamNetworkingSockets transport.
- `steamworks-integration`: SteamAPI init and callbacks.
- `cpu-performance-profiling`: CPU cost of netcode on the host.
- `setup-multiplayer-services`: Relay and sessions when not on Steam.
