# Simulating network conditions

Read when setting up the Network Simulator, choosing presets, scripting lag spikes or disconnects, enabling it in builds, or simulating conditions in Multiplayer Play Mode.

## Which simulator

| Transport | Simulator | Notes |
|---|---|---|
| `UnityTransport` (NGO 2.x) | **Network Simulator** component from `com.unity.multiplayer.tools` | Drives Unity Transport's `SimulatorPipelineStage`. The default for day-to-day testing |
| `UnityTransport`, old API | `UnityTransport.DebugSimulator`, `SetDebugSimulatorParameters(delay, jitter, dropRate)` | Marked `[Obsolete]` in NGO 2.x with the message that they have no effect; do not use |
| SteamNetworkingSockets transport | Steam's global fake-lag and fake-loss config | See [steam-p2p-checks.md](steam-p2p-checks.md). The Network Simulator only attaches to `UnityTransport` |
| Any transport | OS-level tools (clumsy on Windows, `tc netem` on Linux) | Outside Unity; affects every process on the machine. Not covered here |

## Network Simulator setup

1. Install `com.unity.multiplayer.tools` (2.2.x declares Unity 6000.0). It needs `com.unity.transport` 2.0+ and NGO 1.1+.
2. Add **Netcode > Network Simulator** (`Unity.Multiplayer.Tools.NetworkSimulator.Runtime.NetworkSimulator`) to a GameObject that persists across scenes, for example the `NetworkManager` object.
3. Pick a **Connection Preset**, or **Custom** with a preset asset (Create > Multiplayer > NetworkSimulatorPresetAsset), which you can edit live in Play mode.
4. In Play mode the component's Inspector can set Connection to Disconnected and trigger a lag spike of a chosen duration.

Built-in presets (`NetworkSimulatorPresets`):

| Preset | Delay ms | Jitter ms | Loss % |
|---|---|---|---|
| `HomeBroadband` | 32 | 12 | 2 |
| `HomeOptical` (Home Fiber) | 10 | 1 | 0 |
| `HomeCable` | 25 | 5 | 0 |
| `HomeDSL` | 30 | 10 | 0 |
| `HomeSatellite` | 100 | 10 | 0 |
| `HomeCongestedNetwork` | 50 | 50 | 1 |
| `Mobile4G` | 100 | 20 | 4 |
| `Mobile3G` | 360 | 30 | 7 |

The Mobile 2G to 5G presets also exist; they matter little for a PC co-op game. For co-op on Steam add a custom "bad Wi-Fi across a continent" preset around 150 ms delay, 30 ms jitter, 3 % loss.

The delay is applied by the simulator stage on each peer that has the component active. Read the resulting RTT from the HUD or Stats Monitor instead of assuming it equals the preset delay.

## Runtime API

`NetworkSimulator` members (public in 2.2.12):

| Member | Use |
|---|---|
| `ChangeConnectionPreset(INetworkSimulatorPreset)` | Switch conditions mid-session; `NetworkSimulatorPreset.Create(name, description, packetDelayMs, packetJitterMs, packetLossInterval, packetLossPercent)` builds one in code |
| `CurrentPreset` / `ConnectionPreset` | Read or set the active preset |
| `TriggerLagSpike(TimeSpan)`, `TriggerLagSpikeAsync(TimeSpan)` | Simulate a lag spike of that duration. Both do nothing while the component is disabled |
| `Disconnect()`, `Reconnect()` | Simulate losing and regaining the network |
| `IsAvailable`, `IsConnected` | Whether a supported transport is attached, and the simulated connection state |
| `Scenario`, `AutoRunScenario` | Run a `NetworkScenario` (built-in **Connections Cycle** and **Random Connections Swap**) that changes presets over time |

The package's samples include `LagSpikeScenarioWithAsyncTask`, `DisconnectScenarioWithMonoBehaviour`, `UnstableConnection` and `ConnectionParametersWithCurves` as starting points for custom scenarios.

## Builds

NGO's `UnityTransport` compiles the simulator hook when:

- running in the Editor, or
- `DEBUG` is defined (development builds) and `UNITY_MP_TOOLS_NETSIM_DISABLED_IN_DEVELOP` is not, or
- a release build defines `UNITY_MP_TOOLS_NETSIM_ENABLED_IN_RELEASE`.

So a development build on a second PC or the Steam Deck can run the simulator over a real LAN. Never ship a release build with `UNITY_MP_TOOLS_NETSIM_ENABLED_IN_RELEASE`.

## Multiplayer Play Mode recipe

1. Play Mode Scenario with the main Editor as host and one to three virtual players as clients (tags `Host` / `Client`, see `netcode-for-gameobjects` testing).
2. `UnityTransport` on 127.0.0.1 with Network Simulator on the `NetworkManager` object.
3. Change the preset in the virtual player's Inspector or through a debug key bound to `ChangeConnectionPreset`, so only that player suffers.
4. Run the feature checklist; then add a 2-3 s lag spike in the middle of an interaction (pickup, door, trade) and a `Disconnect()` / `Reconnect()` during a scene load.
5. Use `NetStatsHud` on every player to see RTT and connection events side by side.

What to look for under latency:

- Owner-authoritative movement feels instant for its owner and smooth for others. If the owner snaps back, the `NetworkTransform` is still server authority.
- Server-authoritative actions show the round-trip delay, but never apply twice or desync. Duplicate pickups under loss mean the client predicted something the server also did.
- Lag spikes: nothing should be lost (reliable RPCs and NetworkVariables resend), but timers driven by local `Time.time` drift; use `NetworkManager.ServerTime`.
- Disconnect longer than the transport timeout: the client should return to the menu with a reason, the host should clean up that player's objects.

## Timeouts

`UnityTransport` exposes `HeartbeatTimeoutMS`, `ConnectTimeoutMS`, `MaxConnectAttempts` and `DisconnectTimeoutMS`. A lag spike longer than `DisconnectTimeoutMS` disconnects the client. Test spikes just below and just above it so both paths are known. The Steam transport uses Steam's own timeouts (`k_ESteamNetworkingConfig_TimeoutInitial`, `k_ESteamNetworkingConfig_TimeoutConnected`).
