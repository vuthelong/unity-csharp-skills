# Loading audio

Import settings (Load Type, compression, sample rate, Load In Background, Preload Audio Data) are covered by `optimize-audio`. This file covers runtime loading.

## What keeps a clip in memory

| Reference | Effect |
|---|---|
| Direct `AudioClip` field on a loaded scene object or ScriptableObject | Clip object loaded with the referencer. Sample data loaded too if **Preload Audio Data** is on |
| Preload Audio Data off | Clip object loaded, sample data not. First `Play` loads it synchronously (hitch) unless you call `LoadAudioData` first |
| `AssetReferenceT<AudioClip>` (Addressables) | Nothing loaded until `LoadAssetAsync`; unloaded when the last handle is released |
| `AudioCue` loaded through Addressables | The cue's bundle depends on its clips' bundles; loading the cue loads the clips |

## LoadAudioData / UnloadAudioData

For clips with Preload Audio Data off (large ambience, VO lines, level-specific music) referenced directly:

```csharp
public static async UniTask EnsureLoadedAsync(AudioClip clip, CancellationToken ct)
{
    if (clip.loadState == AudioDataLoadState.Loaded) return;

    clip.LoadAudioData();
    await UniTask.WaitWhile(() => clip.loadState == AudioDataLoadState.Loading, cancellationToken: ct);

    if (clip.loadState == AudioDataLoadState.Failed) Debug.LogError($"Audio data failed to load: {clip.name}", clip);
}
```

- `LoadAudioData` is synchronous unless the clip has **Load In Background** on. With it on, the call returns immediately and `loadState` moves through `Loading` to `Loaded`; an `AudioSource` playing it waits until the data is ready (the "first-play silence" in `optimize-audio` troubleshooting).
- It does nothing for clips with Preload Audio Data on; those are already loaded.
- `UnloadAudioData` frees the sample data but keeps the `AudioClip` object. Call it when leaving the area, never while a source is playing the clip.
- Streaming clips decode from disk while playing and keep only a small buffer in memory. Schedule streaming music further ahead instead of relying on preloading.

## Addressables

See `addressables-asset-loading` for groups, labels, handle reference counting and release rules. Audio-specific patterns:

### Music track by AssetReferenceT<AudioClip>

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class MusicLoader
{
    #region Fields
    private readonly MusicPlayer _music;

    private AsyncOperationHandle<AudioClip> _current;
    private AsyncOperationHandle<AudioClip> _previous;
    #endregion

    #region Public Methods
    public MusicLoader(MusicPlayer music) => this._music = music;

    public async UniTask PlayAsync(AssetReferenceT<AudioClip> track, float fade, CancellationToken ct)
    {
        var handle = Addressables.LoadAssetAsync<AudioClip>(track);
        AudioClip clip;
        try
        {
            clip = await handle.ToUniTask(cancellationToken: ct);
        }
        catch
        {
            Addressables.Release(handle);
            throw;
        }

        ReleasePrevious();
        this._previous = this._current;
        this._current = handle;
        this._music.Play(clip, fade);

        await UniTask.Delay(System.TimeSpan.FromSeconds(fade), DelayType.UnscaledDeltaTime, cancellationToken: ct);
        ReleasePrevious();
    }

    public void ReleaseAll()
    {
        ReleasePrevious();
        if (this._current.IsValid()) Addressables.Release(this._current);
        this._current = default;
    }
    #endregion

    #region Private Methods
    private void ReleasePrevious()
    {
        if (this._previous.IsValid()) Addressables.Release(this._previous);
        this._previous = default;
    }
    #endregion
}
```

- Keep the outgoing handle until its crossfade finishes. Releasing it early unloads the clip under a playing source (silence or an error).
- Load through `Addressables.LoadAssetAsync(reference)` and keep the handle, rather than `reference.LoadAssetAsync()`; an `AssetReference` allows only one active load per instance.
- Awaiting with `ToUniTask` needs `UNITASK_ADDRESSABLE_SUPPORT` (automatic when both packages are installed). See `unitask`.

### Per-scene SFX bank

Label every `AudioCue` used by a level (for example `audio-level-forest`) and load the set on scene entry. Same `using` directives as above plus `System.Collections.Generic`.

```csharp
public sealed class AudioBank
{
    #region Fields
    private AsyncOperationHandle<IList<AudioCue>> _handle;
    #endregion

    #region Public Methods
    public async UniTask LoadAsync(string label, CancellationToken ct)
    {
        this._handle = Addressables.LoadAssetsAsync<AudioCue>(label, null);
        await this._handle.ToUniTask(cancellationToken: ct);
    }

    public void Release(AudioService service)
    {
        if (!this._handle.IsValid()) return;

        var cues = this._handle.Result;
        for (var i = 0; i < cues.Count; i++)
        {
            service.StopCue(cues[i]);
        }

        Addressables.Release(this._handle);
        this._handle = default;
    }
    #endregion
}
```

- Load the bank behind the loading screen, before the scene's first `Start`, so no cue loads on first use.
- Stop every voice using a cue before releasing the bank. Pooled sources still referencing an unloaded clip play silence, and a cue unloaded mid-`Tick` leaves stale voices.
- Cues shared by every level (UI, player) go in a global bank loaded once.
- Do not reference the same clips both directly from a scene and from Addressables; the clip is duplicated in the build (Analyze rule in `addressables-asset-loading`).

## Preloading checklist per scene

1. Global bank (UI, player, music stingers) loaded at boot, never released.
2. Level bank loaded with the scene, released on unload after `StopCue`.
3. Long ambience or VO with Preload Audio Data off: `EnsureLoadedAsync` during the loading screen, `UnloadAudioData` on exit.
4. Music via `AssetReferenceT<AudioClip>`, released after the crossfade that replaces it.
