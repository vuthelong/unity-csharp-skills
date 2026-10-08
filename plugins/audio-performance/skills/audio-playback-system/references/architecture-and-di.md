# Architecture and wiring

Project code for Unity 6000.x. The scripts in `../scripts/` compile against UnityEngine only; DI, pooling-library and Addressables glue lives here so the core has no package dependency.

## Ownership

```
AudioService (plain C#, IDisposable)          one per app, DontDestroyOnLoad root
├── ObjectPool<Voice>                          N GameObjects, one AudioSource each, maxSize = voice budget
├── one-shot AudioSource per AudioMixerGroup   2D UI clicks, PlayOneShot
├── MusicPlayer                                2 decks x (intro + body) AudioSource
└── Tick(unscaledDeltaTime)                    follow targets, fades, finish detection, music
AudioSettingsStore (plain C#)                  PlayerPrefs <-> exposed mixer parameters
AudioOcclusion (MonoBehaviour)                 on long-lived scene emitters only
AudioCue (ScriptableObject)                    authored data, one asset per sound event
```

Gameplay code depends on `AudioService` and `AudioCue` assets, never on raw `AudioClip` or `AudioSource`.

## VContainer registration

Put this in the root (project-wide) scope so the service survives scene loads. See `vcontainer` for root scopes in `VContainerSettings`.

```csharp
using UnityEngine;
using UnityEngine.Audio;
using VContainer;
using VContainer.Unity;

public sealed class AudioLifetimeScope : LifetimeScope
{
    #region Fields
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField, Min(1)] private int maxVoices = 24;
    #endregion

    #region Private Methods
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register(_ => new AudioService(null, this.musicGroup, this.maxVoices), Lifetime.Singleton);
        builder.Register(_ => new AudioSettingsStore(this.mixer), Lifetime.Singleton);
        builder.RegisterEntryPoint<AudioDriver>();
    }
    #endregion
}

public sealed class AudioDriver : IStartable, ILateTickable
{
    #region Fields
    private readonly AudioService _service;
    private readonly AudioSettingsStore _settings;
    #endregion

    #region Public Methods
    public AudioDriver(AudioService service, AudioSettingsStore settings)
    {
        this._service = service;
        this._settings = settings;
    }

    public void Start()
    {
        this._settings.Load();
        this._settings.ApplyAll();
    }

    public void LateTick() => this._service.Tick(Time.unscaledDeltaTime);
    #endregion
}
```

- `AudioService(null, ...)` creates its own `DontDestroyOnLoad` root and destroys it in `Dispose`; the container calls `Dispose` when the scope is disposed.
- Tick in `LateTick` so follow targets have already moved this frame.
- `IStartable.Start` runs after `Awake`, which satisfies the "apply mixer values after Awake" rule from `audio-setup-mixers`.
- Inject `AudioService` into consumers by constructor (plain C#) or `[Inject]` method (MonoBehaviours).

## MonoBehaviour singleton fallback

For projects without DI. Place one in the first scene.

```csharp
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-500)]
public sealed class AudioServiceHost : MonoBehaviour
{
    #region Fields
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField, Min(1)] private int maxVoices = 24;
    [SerializeField] private bool pauseOnFocusLoss = true;
    #endregion

    #region Properties
    public static AudioService Service { get; private set; }
    public static AudioSettingsStore Settings { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Service != null)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        Service = new AudioService(transform, this.musicGroup, this.maxVoices);
        Settings = new AudioSettingsStore(this.mixer);
        Settings.Load();
    }

    private void Start()
    {
        if (Settings != null) Settings.ApplyAll();
    }

    private void LateUpdate()
    {
        if (Service != null) Service.Tick(Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        if (Service == null || transform != Service.Root) return;

        Service.Dispose();
        Service = null;
        Settings = null;
    }
    #endregion

    #region Private Methods
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Service = null;
        Settings = null;
    }
    #endregion

    #region Unity Callbacks
    private void OnApplicationFocus(bool hasFocus)
    {
        if (this.pauseOnFocusLoss && Service != null) Service.SetPaused(!hasFocus);
    }

    private void OnApplicationPause(bool paused)
    {
        if (Service != null) Service.SetPaused(paused);
    }
    #endregion
}
```

`ResetStatics` matters with domain reload disabled (see `csharp-unity`). If gameplay also pauses audio (pause menu), keep two flags (`focusPaused`, `menuPaused`) and call `SetPaused(focusPaused || menuPaused)`; `AudioListener.pause` is one global switch.

## Swapping the pool for zbase-pooling

`AudioService` uses `UnityEngine.Pool.ObjectPool<T>` (built in, sync, `maxSize` cap). With `zbase-pooling` installed, a `ComponentPool<AudioSource>` from a prefab works too, but note:

- ZBase rents for Unity objects are async (`UniTask<T>`). A sound must start this frame, so prepool (`PrepoolAmount` / `UnityPrepooler`) and never await inside `Play`; or keep `ObjectPool<T>` for voices and use ZBase for everything else.
- ZBase pools have no max size. Keep the voice budget check (`TryMakeRoomGlobal`) in the service, not in the pool.
- Returning twice corrupts both libraries. `AudioService` releases only from `_active` via swap-remove, which makes double-return impossible.

## Usage

```csharp
public sealed class Weapon : MonoBehaviour
{
    #region Fields
    [SerializeField] private AudioCue fireCue;
    [SerializeField] private AudioCue reloadLoopCue;

    private AudioHandle _reloadHandle;
    #endregion

    #region Public Methods
    public void Fire() => AudioServiceHost.Service.Play(this.fireCue, transform.position);

    public void BeginReload() => this._reloadHandle = AudioServiceHost.Service.Play(this.reloadLoopCue, transform);

    public void EndReload() => AudioServiceHost.Service.Stop(this._reloadHandle, 0.1f);
    #endregion
}
```

A stale `AudioHandle` is harmless: ids are never reused, so `Stop` on a finished voice is a no-op.
