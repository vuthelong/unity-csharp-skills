# Adaptive music recipe: intensity-driven transitions with beat sync

The design lives in FMOD Studio and the code only reports game state. Unity sends one number (intensity) and listens for beats and markers. Studio decides when and how the music moves.

## Studio side (hand to the sound designer)

1. One music event, `event:/Music/Combat`, with a local parameter `Intensity` (continuous 0 to 3, or discrete/labeled: Explore, Tension, Combat, Boss).
2. Timeline with destination markers per section (Explore, Tension, Combat, Boss), with loop regions on each section.
3. Transition regions or markers whose conditions read `Intensity`. Quantize them to the bar (or beat) so changes land musically. Use transition timelines for fills and stingers.
4. A tempo marker at the start (and at any tempo change), or `TIMELINE_BEAT` never fires.
5. Named markers where gameplay should react, for example `Drop` at the climax or `Phrase` at each 8-bar boundary.
6. A Seek Speed on `Intensity` to smooth layer volumes, if layers crossfade on the parameter. Pass `ignoreseekspeed: true` from code to jump instantly (scene loads).
7. Ducking, low-pass on pause and the like as snapshots (`snapshot:/PauseMenu`), not code.

Global parameter alternative: when several events (music, ambience, UI tension) follow the same value, make `Intensity` a global parameter and set it with `RuntimeManager.StudioSystem.setParameterByID`.

## Unity side

```csharp
using System;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public sealed class MusicDirector : MonoBehaviour
{
    #region Fields
    [SerializeField] private EventReference music;
    [SerializeField] private string intensityParameter = "Intensity";
    [SerializeField, Min(0f)] private float startIntensity;
    [SerializeField, Min(0f)] private float changeThreshold = 0.05f;

    private FmodTimelineListener _timeline;
    private PARAMETER_ID _intensityId;
    private bool _hasIntensity;
    private float _intensity;

    public event Action<TimelineBeat> Beat;
    public event Action<string> Marker;
    #endregion

    #region Properties
    public bool IsPlaying => this._timeline != null;
    public float Intensity => this._intensity;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        if (this._timeline != null) this._timeline.Pump();
    }

    private void OnDestroy() => Stop();
    #endregion

    #region Public Methods
    public void Play()
    {
        if (this._timeline != null || this.music.Guid.IsNull) return;

        var instance = RuntimeManager.CreateInstance(this.music);
        this._hasIntensity = TryGetParameterId(instance, this.intensityParameter, out this._intensityId);
        if (!this._hasIntensity) Debug.LogWarning($"Music event has no parameter '{this.intensityParameter}'.", this);

        this._timeline = new FmodTimelineListener(instance);
        this._timeline.BeatReached += HandleBeat;
        this._timeline.MarkerReached += HandleMarker;

        this._intensity = this.startIntensity;
        if (this._hasIntensity) instance.setParameterByID(this._intensityId, this._intensity, true);
        instance.start();
    }

    public void SetIntensity(float value, bool immediate = false)
    {
        if (!immediate && Mathf.Abs(value - this._intensity) < this.changeThreshold) return;

        this._intensity = value;
        if (this._timeline == null || !this._hasIntensity) return;

        this._timeline.Instance.setParameterByID(this._intensityId, value, immediate);
    }

    public void Stop()
    {
        if (this._timeline == null) return;

        this._timeline.BeatReached -= HandleBeat;
        this._timeline.MarkerReached -= HandleMarker;
        this._timeline.Dispose();
        this._timeline = null;
    }
    #endregion

    #region Private Methods
    private static bool TryGetParameterId(EventInstance instance, string parameterName, out PARAMETER_ID id)
    {
        id = default;
        if (instance.getDescription(out var description) != FMOD.RESULT.OK) return false;
        if (description.getParameterDescriptionByName(parameterName, out var parameter) != FMOD.RESULT.OK) return false;

        id = parameter.id;
        return true;
    }

    private void HandleBeat(TimelineBeat beat) => this.Beat?.Invoke(beat);

    private void HandleMarker(string marker) => this.Marker?.Invoke(marker);
    #endregion
}
```

Call `Play()` only after the music bank has loaded (boot flow or loading screen, see [audio-service-facade.md](audio-service-facade.md)). `FmodTimelineListener` and `TimelineBeat` are in [callbacks.md](callbacks.md).

## Driving intensity from gameplay

Compute intensity from game state, smooth it, and add hysteresis so music does not flap at a threshold:

```csharp
public sealed class ThreatIntensity : MonoBehaviour
{
    #region Fields
    [SerializeField] private MusicDirector director;
    [SerializeField, Min(0.01f)] private float riseRate = 2f;
    [SerializeField, Min(0.01f)] private float fallRate = 0.25f;

    private float _target;
    private float _current;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        var rate = this._target > this._current ? this.riseRate : this.fallRate;
        this._current = Mathf.MoveTowards(this._current, this._target, rate * Time.unscaledDeltaTime);
        this.director.SetIntensity(this._current);
    }
    #endregion

    #region Public Methods
    public void ReportThreat(float threatLevel) => this._target = Mathf.Clamp(threatLevel, 0f, 3f);
    #endregion
}
```

- Rise fast and fall slowly. Players notice music dropping out of combat early far more than a late build-up.
- `SetIntensity` drops changes smaller than `changeThreshold`. Studio quantizes anyway, so there is no need to send every frame.
- Use unscaled time so slow motion does not stall the music logic. Pause the music itself with a snapshot or bus pause, not `Time.timeScale`.

## Beat-synced gameplay

```csharp
private void OnEnable() => this.director.Beat += HandleBeat;

private void OnDisable() => this.director.Beat -= HandleBeat;

private void HandleBeat(TimelineBeat beat)
{
    if (beat.Beat == 1) this.spawner.SpawnWave();
    this.pulse.Trigger(60f / beat.Tempo);
}
```

- `beat.Beat == 1` is the downbeat. Use `beat.Bar % 4 == 1` for phrase starts.
- Markers carry designer intent (`Drop`, `Phrase`, `Outro`). Prefer markers over counting bars in code, so musical edits do not break gameplay.
- Seconds per beat = `60 / Tempo`. Use it to time animations to the next beat, not to schedule audio.
- Beats arrive on the main thread one frame or less after FMOD crossed them, and the audible output trails by the mixer buffer. Rhythm-game precision needs `getTimelinePosition` plus latency calibration, which is out of scope here.
- For stingers on gameplay events, set a parameter (for example `Stinger = 1`) that a quantized transition in the music event reacts to. Do not time a `PlayOneShot` in C#.
