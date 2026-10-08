# Runtime mixer control

Project code, not `eval` input. Unity 6000.x.

## Exposing a parameter (user step)

In the Audio Mixer window select the group, right-click **Volume** (or any effect field) in the Inspector, choose **Expose ... to script**, then rename it in the **Exposed Parameters** dropdown (top right of the window), for example `MusicVolume`. Names are case-sensitive. `Edit > Project Settings > Audio` is unrelated.

Verify from an Editor `eval` before writing gameplay code:

```csharp
var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Audio/TheMixer.mixer");
var ok = mixer.GetFloat("MusicVolume", out var db);
return ok ? $"MusicVolume exposed, {db} dB" : "MusicVolume is not exposed";
```

## Volume slider with dB conversion

Perceived loudness is logarithmic. Map linear 0..1 to decibels with `20 * log10(v)` and clamp to -80 dB (the mixer floor).

```csharp
using UnityEngine;
using UnityEngine.Audio;

public sealed class MixerVolume : MonoBehaviour
{
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private string _parameter = "MusicVolume";
    [SerializeField, Range(0f, 1f)] private float _default = 0.8f;

    private const float MinDb = -80f;

    private void Start()
    {
        Apply(PlayerPrefs.GetFloat(_parameter, _default));
    }

    public void Apply(float linear)
    {
        float db = linear <= 0.0001f ? MinDb : Mathf.Max(MinDb, 20f * Mathf.Log10(linear));
        if (!_mixer.SetFloat(_parameter, db))
        {
            Debug.LogError($"Exposed parameter '{_parameter}' not found on {_mixer.name}", this);
            return;
        }
        PlayerPrefs.SetFloat(_parameter, linear);
    }

    public float Read()
    {
        return _mixer.GetFloat(_parameter, out float db) ? Mathf.Pow(10f, db / 20f) : 0f;
    }
}
```

Hook `Apply` to `Slider.onValueChanged` (uGUI) or a `Slider` `RegisterValueChangedCallback` (UI Toolkit). Write `PlayerPrefs.Save()` on menu close, not per slider tick.

## Rules

- Apply saved values in `Start` or later. `SetFloat` during `Awake` can be overwritten when the mixer initializes.
- `SetFloat` returns `false` for unexposed or misspelled names; log it.
- After `SetFloat`, the parameter is script-owned: snapshot transitions no longer move it. Call `ClearFloat(name)` to hand control back to snapshots.
- Do not drive pitch on the Master group to fake slow motion if music should stay at pitch; route music to its own group.
- Changing a mixer parameter at runtime in the Editor does not modify the asset unless the mixer window is in **Edit in Play Mode**.

## Snapshots

Create snapshots in the Audio Mixer window (user step). At runtime:

```csharp
using UnityEngine;
using UnityEngine.Audio;

public sealed class MixStates : MonoBehaviour
{
    [SerializeField] private AudioMixerSnapshot _explore;
    [SerializeField] private AudioMixerSnapshot _combat;
    [SerializeField] private AudioMixerSnapshot _paused;

    public void Explore() => _explore.TransitionTo(1.5f);
    public void Combat() => _combat.TransitionTo(0.3f);
    public void Pause() => _paused.TransitionTo(0.1f);
}
```

Blend several snapshots with `AudioMixer.TransitionToSnapshots(snapshots, weights, time)`. Use snapshots to bypass or attenuate expensive effects in states where they are inaudible; that is cheaper than toggling effects from script.

Snapshot transitions follow `AudioMixer.updateMode`. With the default `Normal` they use scaled time and stall while `Time.timeScale == 0`; set the mixer's Update Mode to `UnscaledTime` (Inspector or `AudioMixerUpdateMode.UnscaledTime`) when a pause menu transitions the mix. For a pause menu, also set `AudioListener.pause = true` and mark UI sources with `ignoreListenerPause = true`.

## Ducking

Prefer the mixer's **Duck Volume** effect on the music group, fed by a **Send** from the Voice group (user creates both in the window). It reacts on the audio thread without script polling. Script-side ducking via `SetFloat` every frame is a fallback.

## AudioListener

Exactly one enabled `AudioListener` at runtime. When cameras are swapped or scenes loaded additively, disable the listener on the outgoing camera before enabling the new one, or keep a single listener on a persistent object that follows the active camera.
