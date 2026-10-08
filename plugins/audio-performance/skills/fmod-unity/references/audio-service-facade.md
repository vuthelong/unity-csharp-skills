# FMOD backend for the audio service facade

`audio-playback-system` ships a concrete `AudioService` (built-in Unity audio) with no interface. This file defines `IAudioService<TCue>` with the same surface, so gameplay code calls the same methods whichever backend is installed:

- The built-in backend implements it as-is: add `: IAudioService<AudioCue>` to `public sealed class AudioService` in your project copy. Every member already matches.
- The FMOD backend implements `IAudioService<EventReference>`. The FMOD event reference is the cue, because randomization, voice limits, cooldown and 3D settings are authored on the event in FMOD Studio, so no extra cue asset is needed.

On a backend switch, the call sites (`Play`, `Stop`, `StopCue`, handles) stay identical. The serialized cue fields change type (`AudioCue` to `EventReference`), as does the generic argument the consumer injects.

## Shared contract

Put this next to `AudioHandle`. `AudioHandle` is the `readonly struct` with an `int Id` from `audio-playback-system/scripts/AudioService.cs`. In an FMOD-only project, copy just that struct into the same assembly.

```csharp
using System;
using UnityEngine;

public interface IAudioService<in TCue> : IDisposable
{
    AudioHandle Play(TCue cue, Vector3 position);
    AudioHandle Play(TCue cue, Transform follow);
    void PlayOneShot(TCue cue);
    bool IsPlaying(AudioHandle handle);
    void Stop(AudioHandle handle, float fadeOut = 0f);
    void StopCue(TCue cue, float fadeOut = 0f);
    void StopAll(float fadeOut = 0f);
    void SetPaused(bool paused);
    void Tick(float unscaledDeltaTime);
}
```

Semantics both backends honor:

| Member | Built-in `AudioService` | `FmodAudioService` |
|---|---|---|
| `Play(cue, position)` | Pooled AudioSource at a point | `CreateInstance`, `set3DAttributes`, `start`, `release` |
| `Play(cue, follow)` | Copies the transform each `Tick` | Same: `set3DAttributes(To3DAttributes(follow))` each `Tick`. If the follow target is destroyed, the event stays where it was |
| `PlayOneShot(cue)` | 2D, no handle | `RuntimeManager.PlayOneShot(cue)`, no handle |
| `Stop(handle, fadeOut)` | Linear fade over `fadeOut` seconds | `fadeOut > 0` means `STOP_MODE.ALLOWFADEOUT` (the AHDSR release authored in Studio sets the length). `0` means `IMMEDIATE` |
| `StopCue(cue, fadeOut)` | All voices of that cue | All tracked instances with that event GUID |
| `SetPaused(bool)` | `AudioListener.pause` | `RuntimeManager.PauseAllEvents` |
| `Tick(dt)` | Fades, follow, voice release | Follow and pruning of finished instances. `dt` unused |
| Stale handle | No-op | No-op (ids are never reused) |

## FmodAudioService

```csharp
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public sealed class FmodAudioService : IAudioService<EventReference>
{
    #region Fields
    private readonly List<Voice> _active;
    private int _nextId;
    private bool _paused;
    private bool _disposed;
    #endregion

    #region Properties
    public int ActiveVoiceCount => this._active.Count;
    public bool IsPaused => this._paused;
    #endregion

    #region Public Methods
    public FmodAudioService(int capacity = 64)
    {
        this._active = new List<Voice>(capacity);
    }

    public AudioHandle Play(EventReference cue, Vector3 position) => StartInstance(cue, position, null);

    public AudioHandle Play(EventReference cue, Transform follow)
    {
        if (follow == null) return AudioHandle.None;

        return StartInstance(cue, follow.position, follow);
    }

    public void PlayOneShot(EventReference cue)
    {
        if (this._disposed || cue.Guid.IsNull) return;

        RuntimeManager.PlayOneShot(cue);
    }

    public bool IsPlaying(AudioHandle handle)
    {
        var index = IndexOf(handle);
        return index >= 0 && this._active[index].Instance.isValid();
    }

    public void Stop(AudioHandle handle, float fadeOut = 0f)
    {
        var index = IndexOf(handle);
        if (index < 0) return;

        StopAt(index, fadeOut);
    }

    public void StopCue(EventReference cue, float fadeOut = 0f)
    {
        for (var i = this._active.Count - 1; i >= 0; i--)
        {
            if (this._active[i].Event.Guid.Equals(cue.Guid)) StopAt(i, fadeOut);
        }
    }

    public void StopAll(float fadeOut = 0f)
    {
        for (var i = this._active.Count - 1; i >= 0; i--) StopAt(i, fadeOut);
    }

    public void SetPaused(bool paused)
    {
        if (this._paused == paused) return;

        this._paused = paused;
        RuntimeManager.PauseAllEvents(paused);
    }

    public void Tick(float unscaledDeltaTime)
    {
        for (var i = this._active.Count - 1; i >= 0; i--)
        {
            var voice = this._active[i];
            if (!voice.Instance.isValid())
            {
                RemoveAt(i);
                continue;
            }

            if (!voice.Following) continue;

            if (voice.Follow == null)
            {
                voice.Following = false;
                voice.Follow = null;
                this._active[i] = voice;
                continue;
            }

            voice.Instance.set3DAttributes(RuntimeUtils.To3DAttributes(voice.Follow));
        }
    }

    public void Dispose()
    {
        if (this._disposed) return;

        StopAll();
        this._active.Clear();
        this._disposed = true;
    }
    #endregion

    #region Private Methods
    private AudioHandle StartInstance(EventReference cue, Vector3 position, Transform follow)
    {
        if (this._disposed || cue.Guid.IsNull) return AudioHandle.None;

        EventInstance instance;
        try
        {
            instance = RuntimeManager.CreateInstance(cue);
        }
        catch (EventNotFoundException exception)
        {
            Debug.LogException(exception);
            return AudioHandle.None;
        }

        instance.set3DAttributes(follow != null ? RuntimeUtils.To3DAttributes(follow) : RuntimeUtils.To3DAttributes(position));
        instance.start();
        instance.release();

        this._nextId++;
        if (this._nextId <= 0) this._nextId = 1;

        this._active.Add(new Voice(this._nextId, cue, instance, follow));
        return new AudioHandle(this._nextId);
    }

    private void StopAt(int index, float fadeOut)
    {
        var instance = this._active[index].Instance;
        if (instance.isValid()) instance.stop(fadeOut > 0f ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
        RemoveAt(index);
    }

    private void RemoveAt(int index)
    {
        var last = this._active.Count - 1;
        this._active[index] = this._active[last];
        this._active.RemoveAt(last);
    }

    private int IndexOf(AudioHandle handle)
    {
        if (!handle.IsValid) return -1;

        for (var i = 0; i < this._active.Count; i++)
        {
            if (this._active[i].Id == handle.Id) return i;
        }

        return -1;
    }

    #endregion

    private struct Voice
    {
        #region Fields
        public readonly int Id;
        public readonly EventReference Event;
        public readonly EventInstance Instance;
        public Transform Follow;
        public bool Following;
        #endregion

        #region Public Methods
        public Voice(int id, EventReference cue, EventInstance instance, Transform follow)
        {
            this.Id = id;
            this.Event = cue;
            this.Instance = instance;
            this.Follow = follow;
            this.Following = follow != null;
        }
        #endregion
    }
}
```

Design notes:

- `start()` is followed immediately by `release()`. The instance frees itself when it stops, so nothing leaks even if gameplay forgets a handle. The handle still works for `stop` while the instance plays.
- Liveness is `isValid()`, not `getPlaybackState`. The playback state is updated by the Studio update, so right after `start()` it can still read `STOPPED`, and pruning on it would drop sounds that are just starting. A released instance becomes invalid once it has stopped and been destroyed, and `Tick` prunes it then.
- There is no voice budget in code. Set Max Instances, stealing and Cooldown on the event in Studio, and set Real Channel Count per platform in FMOD Settings.
- Following is done manually so a destroyed target is handled the same way as in the built-in service. `RuntimeManager.AttachInstanceToGameObject` is the alternative when you do not need that.
- `SetPaused` pauses everything, like `AudioListener.pause`. For a pause menu that keeps UI and menu music running, pause a gameplay bus instead: `RuntimeManager.GetBus("bus:/SFX").setPaused(true)`.
- Volume settings: keep a separate settings store that writes VCAs (`RuntimeManager.GetVCA("vca:/Music").setVolume(linear)`). It is the FMOD counterpart of `AudioSettingsStore`.
- Music: the built-in `MusicPlayer` has no counterpart on the interface. Use `MusicDirector` from [adaptive-music.md](adaptive-music.md), because FMOD Studio handles crossfades, intros and loops.

## Loading banks before audio is used

Call into the service only after the banks that hold the referenced events are loaded. With Load Banks = All on desktop and mobile, loading is effectively done before the first scene. On WebGL, with Android OBB, with Asset Bundle import, or with Load Banks = None, load explicitly behind a loading screen:

```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FMODUnity;

public sealed class FmodBankLoader
{
    #region Fields
    private readonly List<string> _loaded = new List<string>();
    #endregion

    #region Public Methods
    public async UniTask LoadAsync(IReadOnlyList<string> bankNames, bool loadSamples, CancellationToken cancellationToken)
    {
        for (var i = 0; i < bankNames.Count; i++)
        {
            RuntimeManager.LoadBank(bankNames[i], loadSamples);
            this._loaded.Add(bankNames[i]);
        }

        await UniTask.WaitUntil(() => RuntimeManager.HaveAllBanksLoaded, cancellationToken: cancellationToken);
        await UniTask.WaitWhile(() => RuntimeManager.AnySampleDataLoading(), cancellationToken: cancellationToken);
    }

    public void UnloadAll()
    {
        for (var i = this._loaded.Count - 1; i >= 0; i--) RuntimeManager.UnloadBank(this._loaded[i]);
        this._loaded.Clear();
    }
    #endregion
}
```

- Order: `"Master"`, `"Master.strings"`, then content banks. Use the bank names from Studio, without `.bank`.
- Addressables (Import Type = Asset Bundle, Addressables package installed): mark the bank stub `TextAsset`s Addressable and use `RuntimeManager.LoadBank(AssetReference, loadSamples)` / `UnloadBank(AssetReference)` with the same waits. The integration releases the `AssetReference` itself, so do not call `ReleaseAsset`. Group, label and catalog rules are in `addressables-asset-loading`.
- AssetBundles: `bundle.LoadAsset<TextAsset>("SFX")`, then `RuntimeManager.LoadBank(textAsset)`. Unloading the bundle does not unload the bank, so call `UnloadBank(textAsset)` as well. Build with `EventManager.CopyToStreamingAssets(target)` before `BuildPipeline.BuildAssetBundles` and `EventManager.UpdateBankStubAssets(target)` after.
- Stop every instance from a bank (`StopCue`, `StopAll`, or `Bus.stopAllEvents`) before unloading it.

## VContainer wiring

```csharp
using FMODUnity;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class FmodAudioLifetimeScope : LifetimeScope
{
    #region Fields
    [SerializeField, Min(1)] private int trackedVoices = 64;
    #endregion

    #region Private Methods
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IAudioService<EventReference>>(_ => new FmodAudioService(this.trackedVoices), Lifetime.Singleton);
        builder.Register<FmodBankLoader>(Lifetime.Singleton);
        builder.RegisterEntryPoint<FmodAudioDriver>();
    }
    #endregion
}

public sealed class FmodAudioDriver : ILateTickable
{
    #region Fields
    private readonly IAudioService<EventReference> _audio;
    #endregion

    #region Public Methods
    public FmodAudioDriver(IAudioService<EventReference> audio) => this._audio = audio;

    public void LateTick() => this._audio.Tick(Time.unscaledDeltaTime);
    #endregion
}
```

- Register it in the root scope so it outlives scenes (see `vcontainer`). The container disposes the service, which stops tracked instances. `RuntimeManager` keeps running on its own `DontDestroyOnLoad` object.
- Built-in backend: `builder.Register<IAudioService<AudioCue>>(_ => new AudioService(null, this.musicGroup, this.maxVoices), Lifetime.Singleton);` after adding the interface to `AudioService`.
- Without DI, use the `AudioServiceHost` pattern from `audio-playback-system` with `IAudioService<EventReference>` as the static property type, and reset it in `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.

## Gameplay usage

```csharp
using FMODUnity;
using UnityEngine;
using VContainer;

public sealed class Weapon : MonoBehaviour
{
    #region Fields
    [SerializeField] private EventReference fireCue;
    [SerializeField] private EventReference reloadLoopCue;

    private IAudioService<EventReference> _audio;
    private AudioHandle _reloadHandle;
    #endregion

    #region Public Methods
    [Inject]
    public void Construct(IAudioService<EventReference> audio) => this._audio = audio;

    public void Fire() => this._audio.Play(this.fireCue, transform.position);

    public void BeginReload() => this._reloadHandle = this._audio.Play(this.reloadLoopCue, transform);

    public void EndReload() => this._audio.Stop(this._reloadHandle, 0.1f);
    #endregion
}
```

Parameters (surface type, RPM) are FMOD-specific and are not on the facade. Keep them in FMOD-aware components that own their `EventInstance`, or extend the FMOD service with a `SetParameter(AudioHandle, PARAMETER_ID, float)` method that the built-in backend does not need.
