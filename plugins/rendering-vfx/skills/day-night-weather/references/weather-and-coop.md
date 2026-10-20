# Weather, surface effects, saving and co-op sync

Read when adding weather types, rain/snow effects, wetness shaders, saving, or syncing time and weather in NGO co-op.

## Weather state machine

`WeatherController` holds one `WeatherSnapshot`:

| Field | Meaning |
|---|---|
| `from`, `to` | Settled type before the transition, target type |
| `transitionStart` | Clock time the blend began (same clock as `TimeOfDay`) |
| `transitionSeconds` | Blend length of the target preset |
| `nextChangeAt` | Clock time the authority picks the next type |

Everything visible is computed from the snapshot and the clock: `t = SmoothStep(clamp01((now - transitionStart) / transitionSeconds))`, `Current = Lerp(from.sample, to.sample, t)`. Wetness and snow cover use the same formula with `transitionSeconds * surfaceLagFactor`, so ground dries slower than the rain stops. Because nothing accumulates per frame, a late joiner computes the same values as everyone else.

Design the transition table so storms only follow rain and snow only follows overcast, for example:

| From | Next (weight) |
|---|---|
| Clear | Clear 3, Overcast 2 |
| Overcast | Clear 2, Rain 2, Snow 1 |
| Rain | Overcast 2, Storm 1, Rain 1 |
| Storm | Rain 1 |
| Snow | Overcast 1, Snow 1 |

Keep `minDurationSeconds` well above `transitionSeconds`. A forced `TransitionTo` in the middle of a blend starts from the snapshot's `from` type (or `to` if the blend finished), which can pop; designers forcing weather for a scripted moment should wait for `TransitionProgress >= 1`.

Seasons or biomes: swap the `presets` array or weights per biome on the authority only; clients never need them because they receive snapshots.

## Rain and snow effects

Authoring the effect belongs to `unity-vfx-graph` (GPU, large particle counts, depth collision) or `unity-particle-system` (CPU, simpler). Either way:

- Attach the emitter to the local camera or player, never to world origin; precipitation only has to exist around the viewer. Each client drives its own emitter from the synced `WeatherController.Current`, so nothing about particles is networked.
- Rain inside buildings: use a roof/occlusion check (raycast up from the camera, or interior trigger volumes) to scale the rate to 0, or VFX Graph depth-buffer collision for edges.
- `WeatherController` has optional Particle System slots: it sets `emission.rateOverTimeMultiplier` and plays/stops the system.

VFX Graph binder (`VisualEffect.SetFloat(int, float)` with ids for exposed properties named `SpawnRate` and `Wind` in the graph's Blackboard):

```csharp
using UnityEngine;
using UnityEngine.VFX;

public sealed class RainVfxBinder : MonoBehaviour
{
    private static readonly int SpawnRateId = Shader.PropertyToID("SpawnRate");
    private static readonly int WindId = Shader.PropertyToID("Wind");

    [SerializeField] private WeatherController weather;
    [SerializeField] private VisualEffect rain;
    [SerializeField, Min(0f)] private float maxSpawnRate = 20000f;
    [SerializeField] private Transform followTarget;

    private float _lastRate = -1f;

    private void LateUpdate()
    {
        var sample = this.weather.Current;
        var rate = sample.rain * this.maxSpawnRate;
        if (Mathf.Abs(rate - this._lastRate) > 1f)
        {
            this._lastRate = rate;
            this.rain.SetFloat(SpawnRateId, rate);
            this.rain.SetFloat(WindId, sample.wind);
        }

        if (this.followTarget != null)
        {
            this.rain.transform.position = this.followTarget.position;
        }
    }
}
```

Lightning: on `Storm`, trigger flashes locally from a deterministic schedule (for example seeded by `Mathf.FloorToInt((float)(clockNow / 7.0))`) so all peers flash together without RPCs, or accept unsynchronized flashes; they are cosmetic. Pair them with thunder through `audio-playback-system`.

## Wetness and snow as global shader properties

`WeatherController` publishes three globals when they change by more than 0.001:

| Global | Range | Typical shader use |
|---|---|---|
| `_GlobalWetness` | 0-1 | Darken albedo, raise smoothness, puddle mask by height or texture |
| `_GlobalSnowCover` | 0-1 | Blend a snow layer on up-facing normals (`saturate(dot(normalWS, up))`) |
| `_GlobalWind` | 0-1 | Foliage sway amplitude, rain slant |

Shader rules:

- `Shader.SetGlobalFloat` values are used only when the material does not define the property. In Shader Graph set the Blackboard property's **Scope** to **Global** (the Shader Graph 17 docs describe Global as editable "through a C# script only, for all materials that use it"). In hand-written HLSL declare the uniform outside the `UnityPerMaterial` CBUFFER.
- Keep the reference names identical to the strings in the `Shader.PropertyToID` calls.
- Interiors: multiply by a per-material or vertex-color "exposed to sky" mask so floors under roofs stay dry.
- `OnDisable` resets the globals to 0 so the Editor scene does not stay wet after Play Mode.

## Saving (host only)

```csharp
[Serializable]
public sealed class EnvironmentSave
{
    public double totalDays;
    public float timeScale = 1f;
    public WeatherSaveData weather;
}

public static class EnvironmentSaveBridge
{
    public static EnvironmentSave Capture(TimeOfDay time, WeatherController weather) => new()
    {
        totalDays = time.TotalDays,
        timeScale = time.TimeScale,
        weather = weather.CaptureSave()
    };

    public static void Restore(EnvironmentSave save, TimeOfDay time, WeatherController weather)
    {
        time.SetTimeScale(save.timeScale);
        time.SetTotalDays(save.totalDays);
        weather.RestoreSave(save.weather);
    }
}
```

Put `EnvironmentSave` inside the world DTO from `save-system` (it is plain data; `TimeAnchor` clock values never go to disk). `WeatherSaveData` stores the current type and seconds until the next change, both session-independent. Restore before starting the host so the first `NetworkVariable` values are already correct.

## Co-op sync with Netcode for GameObjects 2.x

Goal: host owns time and weather; clients compute everything locally from a few values that change rarely. No per-frame RPC, no per-tick `NetworkVariable` writes.

How `NetworkEnvironmentSync` works:

1. **Shared clock.** On spawn every peer calls `TimeOfDay.SetClockSource(() => NetworkManager.ServerTime.Time)`. NGO docs: `NetworkManager.ServerTime` is "The NetworkTime on the server"; `NetworkTime.Time` is a `double`. On clients `ServerTime` is behind the real server time and `LocalTime` is ahead of it, so `ServerTime` is the right clock for host-authoritative state that every client should render consistently.
2. **Anchors, not values.** The host writes `NetworkVariable<NetTimeAnchor>` only when `TimeOfDay.AnchorChanged` fires (sleep, time-scale change, load) and `NetworkVariable<NetWeatherSnapshot>` only when the weather changes. Each is a small `INetworkSerializable` struct (`NetworkSerialize<T>(BufferSerializer<T>) where T : IReaderWriter`).
3. **Clients apply.** In `OnNetworkSpawn` clients read the current `Value` (late join gets the latest state) and subscribe to `OnValueChanged(previous, current)`; they set `WeatherController.IsAuthority = false` so they never roll their own weather.
4. **Host re-base.** When the host switches from `Time.timeAsDouble` to server time, `SetClockSource` re-anchors so the sky does not jump, and the weather snapshot is re-based through `CaptureSave`/`RestoreSave` because its clock times belonged to the old clock.
5. **Despawn.** Unsubscribes, returns to the local clock and gives weather authority back for single-player continuity.

Rules:

- Place the sync object in the scene with a `NetworkObject` (in-scene placed) or spawn it from the host before clients need it; it must exist on every peer.
- Only the host calls `SetTimeOfDay`, `SetTotalDays`, `SetTimeScale` and `TransitionTo`. Route player requests (a bed, a weather totem) through a server RPC (`netcode-for-gameobjects`).
- Do not pause the world in co-op, and do not set `Time.timeScale` on the host to slow time; use `SetTimeScale` instead. How `ServerTime` reacts to `Time.timeScale` is not covered by the NGO pages used here, so test it before relying on either behavior.
- Sleep with several players: decide on the host (all players in bed, or majority), then `SetTimeOfDay`. The anchor change replicates once.
- Precision: `TimeAnchor` and `ServerTime.Time` are doubles. The NGO time docs mention that casting to float is only safe for short sessions.
- Visual drift between peers is bounded by the `ServerTime` difference (a fraction of a second), invisible for a sky that takes minutes per hour.
- NGO 3.x (Unity 6000.7+) moves `NetworkTime` to another namespace according to the `netcode-for-gameobjects` skill. The script only touches `NetworkManager.ServerTime.Time`, so it should be unaffected, but recheck after upgrading.
- The sync script was compile-checked against hand-written stubs of the NGO API surface it uses, not the real package. Compile it in your project before relying on it.
