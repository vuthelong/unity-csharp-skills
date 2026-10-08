# FMOD for Unity API reference

Namespaces: `FMODUnity` (integration helpers and components), `FMOD.Studio` (Studio API), `FMOD` (Core API, `RESULT`, `GUID`, `ATTRIBUTES_3D`). Signatures were checked against the 2.03 Unity and Engine docs. Almost every Studio/Core call returns `FMOD.RESULT` instead of throwing, so check results on calls that can fail (lookups, loads).

## RuntimeManager (static, `FMODUnity`)

| Member | Notes |
|---|---|
| `StudioSystem` / `CoreSystem` | The `FMOD.Studio.System` and `FMOD.System` the integration created. Never release them. |
| `IsInitialized`, `IsMuted` | State flags. |
| `HaveAllBanksLoaded`, `HaveMasterBanksLoaded` | True once every bank load issued through the RuntimeManager has finished. Used as properties in FMOD's own examples (the reference page lists a method form). Poll them on platforms where loads are async (WebGL, Android OBB). |
| `CreateInstance(EventReference / string path / Guid)` | Returns a stopped `EventInstance`. You own it and must `release()` it. A 3D event starts far away until you set 3D attributes or attach it. |
| `PlayOneShot(EventReference / path / Guid, Vector3 position = default)` | Fire and forget at a position. No parameters, no handle. |
| `PlayOneShotAttached(EventReference / path / Guid, GameObject)` | Fire and forget that follows the GameObject. |
| `AttachInstanceToGameObject(instance, GameObject or Transform, bool allowNonRigidbodyVelocity = false)` | Also overloads taking a `Rigidbody` or `Rigidbody2D`. Updates 3D attributes every frame. Call `start()` right after, and the instance auto-detaches when it stops. |
| `DetachInstanceFromGameObject(instance)` | Stop tracking. |
| `GetEventDescription(EventReference / path / Guid)` | For parameter IDs, `isSnapshot`, `loadSampleData`, `getLength`, and so on. |
| `GetBus("bus:/...")`, `GetVCA("vca:/...")` | Path lookups. They need the strings bank. Cache the result after banks load. If you need a `RESULT` instead of the helper's failure behavior, use `StudioSystem.getBus` / `getVCA`. |
| `PathToGUID(path)`, `PathToEventReference(path)` | Runtime path to GUID. Needs the strings bank. |
| `PauseAllEvents(bool)`, `MuteAllEvents(bool)` | Global pause and mute. |
| `LoadBank(string bankName, bool loadSamples = false)` | Streaming Assets import. Name without `.bank`. Reference counted. |
| `LoadBank(AssetReference, bool loadSamples = false, Action completionCallback = null)` | Addressables (Asset Bundle import). Async. The integration releases the AssetReference itself. |
| `LoadBank(TextAsset, bool loadSamples = false)` | AssetBundles (Asset Bundle import). |
| `UnloadBank(string / AssetReference / TextAsset)` | Decrements the ref count, and unloads at zero. Unloading invalidates every instance from that bank. |
| `HasBankLoaded(string bankName)` | Per-bank check. |
| `AnySampleDataLoading()` / `WaitForAllSampleLoading()` | Poll or block on sample loading. Blocking does not work on WebGL. `AnyBankLoading` and `WaitForAllLoads` are the deprecated names. |
| `SetListenerLocation([int index,] GameObject, [Rigidbody/Rigidbody2D,] GameObject attenuationObject = null)` | Manual listener placement, for when you do not use `StudioListener`. |

`RuntimeUtils.To3DAttributes(Vector3 / Transform / GameObject [, Rigidbody / Rigidbody2D])` builds `FMOD.ATTRIBUTES_3D`. `RuntimeUtils.ToFMODVector(Vector3)` converts vectors.

Do not call `RuntimeManager` from Editor-only code (custom inspectors, menu items outside Play Mode). It logs "accessed outside of runtime". Create and release your own `FMOD.Studio.System` there instead.

## EventInstance (`FMOD.Studio`)

| Method | Notes |
|---|---|
| `start()` | Restarts if the instance is already playing. |
| `stop(STOP_MODE mode)` | `STOP_MODE.ALLOWFADEOUT` lets AHDSR releases and effect tails play out. `STOP_MODE.IMMEDIATE` cuts. |
| `release()` | Marks the instance for destruction once it is stopped. The handle stays usable (stop, set parameters) until then. After that calls return `ERR_INVALID_HANDLE`. |
| `isValid()` | False once destroyed or never created. Use it before touching a cached instance. |
| `getPlaybackState(out PLAYBACK_STATE)` | `PLAYING`, `SUSTAINING`, `STOPPED`, `STARTING`, `STOPPING`. |
| `set3DAttributes(ATTRIBUTES_3D)` | Position, velocity, forward and up. |
| `setParameterByName(string name, float value, bool ignoreseekspeed = false)` | Hashes the name per call. Fine for rare changes. |
| `setParameterByID(PARAMETER_ID id, float value, bool ignoreseekspeed = false)` | Use per frame. Get the ID once from the description. |
| `setParameterByNameWithLabel` / `setParameterByIDWithLabel` | Labeled parameters by label string. |
| `setParametersByIDs(PARAMETER_ID[] ids, float[] values, int count, bool ignoreseekspeed = false)` | Batch. Preallocate the arrays. |
| `getParameterByName/ByID(..., out float value [, out float finalvalue])` | `finalvalue` includes automation and modulation. |
| `setPaused(bool)`, `setVolume(float)`, `setPitch(float)` | Volume and pitch are linear multipliers. |
| `setTimelinePosition(int ms)`, `getTimelinePosition(out int ms)` | Timeline position in milliseconds. |
| `keyOff()` | Moves past a sustain point. |
| `setCallback(EVENT_CALLBACK cb, EVENT_CALLBACK_TYPE mask = ALL)` | See [callbacks.md](callbacks.md). |
| `setUserData(IntPtr)` / `getUserData(out IntPtr)` | Carries a `GCHandle` into the callback. |
| `getDescription(out EventDescription)` | Back to the description. |
| `setProperty(EVENT_PROPERTY, float)` | Per-instance overrides such as `MINIMUM_DISTANCE`, `MAXIMUM_DISTANCE` and `SCHEDULE_DELAY`. |

Parameter ID caching:

```csharp
var description = RuntimeManager.GetEventDescription(this.engineEvent);
description.getParameterDescriptionByName("RPM", out var rpm);
this._rpmId = rpm.id;
```

The ID belongs to the event description, not the instance, so cache it once per event and reuse it across instances.

## Global parameters (`RuntimeManager.StudioSystem`)

| Method | Notes |
|---|---|
| `setParameterByName(string, float, bool ignoreseekspeed = false)` | Global parameters only. Local parameters live on instances. |
| `setParameterByID(PARAMETER_ID, float, bool ignoreseekspeed = false)` | Cache the ID through `getParameterDescriptionByName(name, out PARAMETER_DESCRIPTION)`. |
| `setParameterByNameWithLabel(string, string, bool)` | Labeled globals such as `Weather = "Rain"`. |
| `getBus/getVCA/getEvent/getBank(path, out ...)` | RESULT-returning versions of the RuntimeManager helpers. `getEventByID` / `getBankByID` take a GUID. |
| `setNumListeners(int)`, `setListenerAttributes(int, ATTRIBUTES_3D [, VECTOR attenuationposition])` | `StudioListener` normally does this. |
| `flushCommands()` | Blocks until queued commands and non-blocking bank loads finish. Use it only in loading screens. |

## Buses, VCAs, snapshots

| Call | Notes |
|---|---|
| `Bus.setVolume(float)` | Linear, multiplies the Studio mix level (1 = as authored). |
| `Bus.setPaused(bool)` / `Bus.setMute(bool)` | Pausing a bus overrides its inputs. Unpausing returns them to their own state. |
| `Bus.stopAllEvents(STOP_MODE)` | Stops every instance routed into the bus, including one-shots you have no handle for. |
| `VCA.setVolume(float)` | Linear. Use VCAs for player-facing sliders (Music, SFX, Voice), not buses. |
| Snapshots | `RuntimeManager.CreateInstance("snapshot:/Underwater")` or an `EventReference`. Then `start()` to blend it in, `stop(ALLOWFADEOUT)` to blend out, and `release()`. If the snapshot exposes an intensity parameter in Studio, drive it like any event parameter. `EventDescription.isSnapshot(out bool)` tells snapshots and events apart. |

Slider mapping: VCA volume is linear amplitude, so a perceptual slider needs a curve (`value * value` or dB conversion). See `audio-setup-mixers` for the dB math. It is the same, except FMOD takes the linear result.

## Banks and sample data

| Call | Notes |
|---|---|
| `Bank.loadSampleData()` / `unloadSampleData()` | Reference counted, async. Pair them per load. |
| `Bank.getLoadingState` / `getSampleLoadingState(out LOADING_STATE)` | Poll during loading screens. |
| `EventDescription.loadSampleData()` | Preloads one event (and the events it references). Use it for the first gunshot or footstep. |
| `Bank.unload()` | Destroys all objects from that bank. Prefer `RuntimeManager.UnloadBank` so ref counts stay correct. |

Sample data rules:

- By default a bank load brings only metadata. Samples load when an instance is created and unload when the last one is gone. The first play of a non-streaming sound can be late.
- Preload samples for anything latency-sensitive: the `loadSamples` argument, `StudioBankLoader.PreloadSamples`, emitter Preload, or `EventDescription.loadSampleData()`.
- Streaming assets (long music or VO set to stream in Studio) do not need sample preloading. They read from disk.
- Split banks: load the assets bank first, then the metadata bank with `loadSamples`.

## Components

| Component | Key fields and methods |
|---|---|
| `StudioListener` | Put it on the camera, or on the player with camera rotation. `AttenuationObject` (2.04: public property) splits attenuation from panning for third-person and top-down games. `NonRigidbodyVelocity` enables Doppler without a Rigidbody. `ListenerNumber`, static `ListenerCount`, static `DistanceToNearestListener(Vector3)`. Up to 8 listeners. One is created if none exists. |
| `StudioEventEmitter` | `EventReference`, `EventPlayTrigger` / `EventStopTrigger` (`EmitterGameEvent`: `ObjectStart`, `ObjectEnable`, `TriggerEnter`, `CollisionEnter`, `UIMouseDown`, ...; renamed from `PlayEvent`/`StopEvent`), `Play()`, `Stop()` (honors `AllowFadeout`), `IsPlaying()`, `IsActive`, `SetParameter(string or PARAMETER_ID, float, bool ignoreseekspeed = false)` (only while playing), `Params` (initial values), `Preload`, `TriggerOnce`, `NonRigidbodyVelocity`, `OverrideAttenuation` + `OverrideMinDistance` / `OverrideMaxDistance`, `EventInstance`, `EventDescription`, `CollisionTag`. |
| `StudioParameterTrigger` | `Emitters` (`EmitterRef[]`: `Target` emitter plus `Params`), `TriggerEvent`, `TriggerParameters()`. Sets local parameters on emitters when a trigger condition fires. |
| `StudioGlobalParameterTrigger` | `Parameter` (`[ParamRef]` string), `Value`, `TriggerEvent`, `TriggerParameters()`. |
| `StudioBankLoader` | `Banks` (`[BankRef] List<string>`, loaded in order), `LoadEvent` / `UnloadEvent` (`LoaderGameEvent`), `PreloadSamples`, `Load()`, `Unload()`. Needs Streaming Assets import. |

Emitter notes:

- One-shot events: every `Play()` spawns a new instance, and `Stop()` stops only the latest. For looping events, `Play()` restarts.
- Override Attenuation does not affect a spatializer on the event that sets its own distances.
- Use `ObjectEnable`/`ObjectDisable` triggers for pooled objects. `ObjectStart` fires once per object lifetime.
- Inspector pickers: `EventReference` fields, `[BankRef] string`, `[ParamRef] string`.

## EventReference and [EventRef] migration

- `FMODUnity.EventReference` is a serializable struct. At runtime it holds only `Guid` (`FMOD.GUID`). `Path`, `IsNull` and `EventReference.Find(path)` are Editor-only (`#if UNITY_EDITOR`). In runtime code test `reference.Guid.IsNull`. Using `Path` or `IsNull` there compiles in the Editor and fails in the player build.
- `EventReference.Find("event:/...")` in `Reset()` gives a component a default event (Editor-only).
- 2.01 and earlier used `[FMODUnity.EventRef] string`. To migrate:
  1. Add an `EventReference` field next to each `[EventRef]` string.
  2. Mark the old field `[FMODUnity.EventRef(MigrateTo = "newFieldName")]`.
  3. Run FMOD > Update Event References: Scan, then execute the tasks. Manual tasks (prefixed "Manual task:") need the code edits above.
  4. Remove the old string fields once the scan shows no remaining references.
- The same tool fixes GUID/path mismatches after events are renamed in Studio. Event Linkage decides which side wins.
