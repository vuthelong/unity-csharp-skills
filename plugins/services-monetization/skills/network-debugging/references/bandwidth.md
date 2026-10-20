# Bandwidth: measure and budget

Read when measuring bytes with the Network Profiler or Runtime Network Stats Monitor, setting a bandwidth budget, or reducing NetworkVariable, RPC and NetworkTransform traffic.

## Measure

### Network Profiler modules

Installing `com.unity.multiplayer.tools` adds two modules to the Unity Profiler (Profiler Modules dropdown):

| Module | Counters |
|---|---|
| **NGO Messages** | Total Bytes Sent/Received, Named and Unnamed Message Bytes, Scene Event Bytes |
| **NGO Objects** | RPC Bytes, Network Variable Bytes, Object Spawned/Destroyed Bytes, Ownership Change Bytes (each sent and received) |

- Select a frame to list individual messages and objects. Filter the list with `t:<type>` (for example `t:RPC`), `dir:in`, `dir:out`, `b>N`, `b<=N` and so on; combine rules.
- Double-click a `NetworkObject` row to select it in the Hierarchy.
- Messages to the local client (host to itself) are greyed out: they do not cross the network.
- Profile the **host in a development build** with real clients connected: the host's sent bytes are your upload bottleneck.

### Runtime Network Stats Monitor (RNSM)

- Component **Netcode > Runtime Network Stats Monitor** (`Unity.Multiplayer.Tools.NetStatsMonitor.RuntimeNetStatsMonitor`). An on-screen UI Toolkit panel driven by a `NetStatsMonitorConfiguration` asset (Create > Multiplayer > NetStatsMonitorConfiguration); start from the default configuration in the package.
- Included in the Editor and development builds, excluded from release builds by default. Project Settings > Multiplayer Tools > Net Stats Monitor toggles this, or the defines `UNITY_MP_TOOLS_NET_STATS_MONITOR_DISABLED_IN_DEVELOP`, `UNITY_MP_TOOLS_NET_STATS_MONITOR_ENABLED_IN_RELEASE`, `UNITY_MP_TOOLS_NET_STATS_MONITOR_IMPLEMENTATION_ENABLED`.
- Built-in metrics include total bytes, RPC, NetworkVariable delta, spawn, ownership and scene event traffic, packets, RTT to server, packet loss and connection counts. Add your own values with `AddCustomValue(MetricId, float)`.
- The package's custom-stat example calls `FindObjectOfType`; in Unity 6 use `FindAnyObjectByType` or a serialized reference.

### Without the tools package

`scripts/NetStatsHud.cs` shows role, transport, tick rate, server tick, local minus server time, RTT per client (from `NetworkTransport.GetCurrentRtt`) and connection events. It does not count bytes.

## Budget

There is no official number. A workable starting budget for 2-4 player co-op:

1. Compute the host's upload: per-client bytes per second x number of remote clients. The host also pays for its own simulation; see `cpu-performance-profiling`.
2. Keep the steady-state host upload well below what home connections sustain; aim for tens of KB/s, not hundreds, and treat spikes (scene load, mass spawn) separately.
3. Write the budget down per system (players, enemies, projectiles, world state) and check it with the NGO Objects module after each feature.

Rough sizes to reason with (payload only, before NGO and transport headers):

| Data | Bytes |
|---|---|
| `bool`, `byte` | 1 |
| `int`, `float` | 4 |
| `Vector3` | 12 |
| `Quaternion` | 16 |
| `FixedString32Bytes` | length prefix + only the used UTF-8 bytes |
| `NetworkObjectReference` | 8 (the `ulong` NetworkObjectId) |

A `Vector3` `NetworkVariable` that changes every tick at the default 30 Hz costs about 360 B/s per client before headers. Fifty of them is about 18 KB/s per client.

## Reduce

### NetworkVariable

- Changes are collected and sent on network ticks (`NetworkConfig.TickRate`, default 30), not every frame. Several writes in one tick send one value.
- Throttle noisy values with `NetworkVariableUpdateTraits` (NGO 2.0+): `MinSecondsBetweenUpdates` caps the send rate, `MaxSecondsBetweenUpdates` forces a send of a dirty value after that time.
- Skip insignificant changes with the `CheckExceedsDirtinessThreshold` callback on `NetworkVariable<T>`.

```csharp
using Unity.Netcode;
using UnityEngine;

public sealed class Stamina : NetworkBehaviour
{
    private readonly NetworkVariable<float> _stamina = new(100f);

    private void Awake()
    {
        _stamina.SetUpdateTraits(new NetworkVariableUpdateTraits
        {
            MinSecondsBetweenUpdates = 0.2f,
            MaxSecondsBetweenUpdates = 1f,
        });
        _stamina.CheckExceedsDirtinessThreshold = ExceedsThreshold;
    }

    private static bool ExceedsThreshold(in float previous, in float current) => Mathf.Abs(current - previous) >= 1f;
}
```

- Prefer small types: `byte`/`ushort` enums and counters, `FixedString` instead of `string`, ids instead of structs of data.
- Split big structs: a struct `NetworkVariable` resends the whole struct when any field changes.
- `NetworkList<T>` sends element deltas; replacing a whole list or a managed collection value resends everything.
- Managed collections inside a `NetworkVariable` need `CheckDirtyState()` after in-place edits, otherwise nothing is sent.

### NetworkTransform

| Setting | Effect |
|---|---|
| Sync Position/Rotation/Scale per axis | Turn off axes you do not need (scale is often constant; Y rotation only for upright characters) |
| `PositionThreshold`, `RotAngleThreshold`, `ScaleThreshold` | Minimum change before sending (defaults 0.001, 0.01, 0.01). Raise for props and slow objects |
| `UseHalfFloatPrecision` | Half-precision position deltas |
| `UseQuaternionSynchronization` + `UseQuaternionCompression` | Full quaternion sync, compressed |
| `InLocalSpace` | Sync local instead of world space (parented objects) |
| `UseUnreliableDeltas` | Send deltas unreliably; check the NGO manual for its trade-offs before enabling |
| `Interpolate` | Smoothing on non-authority peers; turning it off removes visual delay but shows jitter |

Static or rarely moving objects should not have a `NetworkTransform` at all; send discrete state (door open, lever angle step) through a `NetworkVariable`.

### RPCs

- Do not send RPCs every frame. Batch input into the tick (owner-authoritative movement needs no RPC at all) and send events, not polling.
- One-off cosmetic events (sound, VFX) to `SendTo.ClientsAndHost` are cheap; continuous state belongs in `NetworkVariable`s.
- Avoid `string` and arrays in RPC parameters on hot paths; use ids, enums and `FixedString`.

### Tick rate

`NetworkConfig.TickRate` (default 30) sets how often state is sent. Lowering it saves bandwidth and adds delay; raising it does the opposite. It is part of the connection config hash, so host and clients must match. Change it only after the per-system fixes above.

### Spawns

Spawn messages carry the full initial state of every `NetworkVariable` on the object. Mass spawns (loot explosions, waves) show as spikes in Object Spawned Bytes: pool, spawn over several frames, or replicate one manager object instead of many small `NetworkObject`s.
