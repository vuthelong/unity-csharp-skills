# Testing NGO co-op

Read when testing with Multiplayer Play Mode, simulating latency, or debugging desync and missing objects.

## Multiplayer Play Mode (MPPM)

- Package `com.unity.multiplayer.playmode`. 1.6.x declares Unity 6000.0; 2.0.x declares 6000.3 or newer. Install the line your Editor accepts.
- Up to four Editor players (the main Editor plus three virtual players) sharing the project's assets, plus up to four local build instances.
- In 1.6.x, configure players through Play Mode Scenarios (**Window > Multiplayer > Play Mode Scenarios**, or the Play Mode dropdown); entering Play Mode starts every player in the scenario. Menu paths move between package versions; check the manual of your installed version.
- Create tags under **Edit > Project Settings > Multiplayer > PlayMode > Player Tags**, assign them per player (for example `Host`, `Client`) and read the tags at runtime with `Unity.Multiplayer.Playmode.CurrentPlayer.ReadOnlyTags()`; `CurrentPlayer.IsMainEditor` tells you if you are the main Editor.
- Use `UnityTransport` on 127.0.0.1 for MPPM sessions. Platform services that bind to one user account (Steam, a single UGS sign-in profile) see the same identity in every player; switch them off or use per-player profiles (see `setup-multiplayer-services`).

```csharp
using System.Linq;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using UnityEngine;

public class PlaymodeAutoStart : MonoBehaviour
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

## Network conditions

- Install `com.unity.multiplayer.tools` and add the Network Simulator component next to the `NetworkManager`; pick a preset (home broadband, mobile) or custom delay, jitter and loss. `UnityTransport.DebugSimulator` is obsolete in NGO 2.x and does nothing.
- Test co-op at about 150 ms round trip and 2-5 % loss at least once per feature. Owner-authoritative movement should feel instant locally and smooth remotely; server-authoritative interactions should show the delay but never desync.
- Multiplayer Tools also provides the Runtime Network Stats Monitor and network profiler modules for bandwidth per message and per `NetworkVariable`.

## Test checklist per feature

| Case | Check |
|---|---|
| Host acts | Clients see the result |
| Client acts | Host and other clients see the result; server validated it |
| Late join after the action | Joiner sees the current state without replaying events |
| Client leaves mid-action | No orphaned objects, no exceptions on the host |
| Host leaves | Clients return to menu with a reason |
| Ownership change (pick up, vehicle) | New owner drives the object, old owner stops writing |
| Scene change | All clients arrive, nobody stays on the old scene, objects with `destroyWithScene` are gone |
| Latency 150 ms + loss | No duplicated pickups, no doors stuck half-open |

## Debugging

| Symptom | Look at |
|---|---|
| Object missing on clients | Prefab in the Network Prefabs List? Spawned on the server? `NetworkObject` on the root? Observers (`SpawnWithObservers`, `CheckObjectVisibility`)? |
| Value differs between peers | Who writes it? Only one writer per `NetworkVariable`; managed contents mutated without `CheckDirtyState()` |
| RPC never arrives | Name ends in `Rpc`? Object spawned on both sides? `InvokePermission` blocking the caller? Target excludes the host (`NotServer`)? |
| Client connects then drops immediately | Config hash mismatch or approval rejection; log `DisconnectReason` on the client |
| Works in MPPM, fails between machines | Firewall or port for `UnityTransport` (the host must listen on `0.0.0.0` with `SetConnectionData(address, port, "0.0.0.0")`), or the production transport behaves differently; test the real transport early |

Set `NetworkManager` Log Level to Developer while debugging; reset to Normal for builds.

## Automated tests

NGO's own integration test framework (`NetcodeIntegrationTest`) lives in the package's test assemblies and is not a stable public API; check that it is available in your installed version before building on it. For game code, keep rules (damage, crafting, validation) in plain C# classes covered by Edit Mode tests, and keep `NetworkBehaviour`s thin.
