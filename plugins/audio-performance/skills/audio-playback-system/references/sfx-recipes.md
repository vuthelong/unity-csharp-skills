# SFX recipes

## PlayOneShot or a dedicated source

| | `AudioSource.PlayOneShot(clip, volumeScale)` | Dedicated pooled source (`AudioService.Play`) |
|---|---|---|
| Stop one instance | No. `Stop()` kills every one-shot on that source | Yes, through `AudioHandle` |
| Per-instance pitch, position, spatial settings | No. Shared by all one-shots on the source | Yes |
| Looping | No | Yes |
| Voice accounting | Invisible to your limiter; still counts as Unity voices | Counted per cue and globally |
| Cost | One component for many sounds | One GameObject per concurrent voice |
| Use for | UI clicks, 2D stingers, rapid non-spatial ticks | Anything 3D, looping, stoppable, or pitch-randomized |

`AudioService.PlayOneShot` keeps one 2D source per `AudioMixerGroup` so a group change never reroutes sounds already in flight. Those sources set `ignoreListenerPause = true`, so they suit menus; route gameplay SFX through `Play`.

Unity 6 also ships the **Audio Random Container** asset (assign it as an `AudioSource.resource`) for clip randomization, pitch/volume variation and triggers without code. Use it when designers own the variation and no per-cue voice limiting is needed; `AudioCue` remains useful when gameplay code needs cooldowns, voice stealing and handles.

## Voice limiting

Three layers, cheapest first:

1. **Cooldown** per cue (`AudioCue.cooldown`, unscaled time). Debounces ten footsteps on one frame from a physics burst. 0.03–0.08 s for impacts, 0 for UI.
2. **Max instances** per cue. When full, `StealMode.None` drops the new request, `Oldest` frees the instance that started first, `Quietest` frees the lowest `AudioSource.volume`.
3. **Global budget** (`maxVoices`). When full, the service frees the voice with the highest `priority` number (Unity: 0 = most important, 256 = least); ties go to the oldest. A request never steals a voice more important than itself, and voices already fading out are stolen first.

Keep `maxVoices` below **Max Real Voices** in Project Settings > Audio. Above that, Unity virtualizes the quietest voices itself; they keep their slot and position but are inaudible, and you lose control of which survive.

Stealing calls `AudioSource.Stop()`, which can click on loud sustained sounds. For loops, prefer `StealMode.None` plus a lower `maxInstances`, or fade with `Stop(handle, 0.05f)` before replaying.

## Random clip without repeat

`AudioCue.PickClip` rolls an index and, if it equals the last one, shifts by `Random.Range(1, count)` modulo `count`. That is uniform over the other clips and costs one extra roll. For shuffle-bag behaviour (every clip once before any repeats) keep an `int[]` permutation per cue and reshuffle with Fisher–Yates when exhausted.

Last-index state lives in a `[NonSerialized]` field on the ScriptableObject, so it is shared by every user of the cue and resets on domain reload. That is the intent; do not serialize runtime state into the asset (see `csharp-unity` serialization notes).

Typical ranges: pitch 0.92–1.08 for impacts and footsteps, 0.98–1.02 for voices, fixed for musical stingers. Volume 0.85–1.0.

## Moving emitter vs fixed position

- `Play(cue, Vector3)`: fire-and-forget at a point (impacts, explosions).
- `Play(cue, Transform)`: the service copies the target's position every `Tick`. It does not parent the voice, so destroying the target never destroys a pooled source; when the target dies the voice stays where it was and finishes.
- Tick from `LateUpdate` (or VContainer `ILateTickable`) so the copy happens after movement. For Rigidbody-interpolated targets that is the rendered position.
- Doppler uses the source's velocity, which Unity derives from position change each frame; teleporting a pooled voice to a new position produces a one-frame doppler blip if `dopplerLevel > 0`. Set `dopplerLevel = 0` on cues that never need it.

## Returning sources to the pool

`AudioService.Tick` releases a voice when all are true: not fading, not held by `AudioListener.pause` (unless `ignoreListenerPause`), its clip is not still loading, and `isPlaying` is false. Checking `isPlaying` once per frame per voice is cheap and survives pitch changes, pauses and seeks.

Alternative when you cannot tick per voice (for example a burst system with hundreds of entries): compute the end time on the DSP clock.

```csharp
var duration = clip.samples / (double)clip.frequency / Mathf.Abs(source.pitch);
var endDsp = AudioSettings.dspTime + duration;
```

- Use `AudioSettings.dspTime`, not `Time.time`: it is independent of `Time.timeScale` and, per the docs, it freezes while `AudioListener.pause` is true, so the deadline moves with the pause automatically.
- Never use `Time.time` or a scaled `UniTask.Delay`. At `timeScale = 0` the timer stops while audio keeps playing; at `timeScale = 2` sources are released mid-sound.
- Pitch changes after start invalidate the deadline; recompute or fall back to `isPlaying`.
- Looping voices have no end; release them only through `Stop`.

## Slow motion

`Time.timeScale` does not affect audio. If gameplay SFX should slow down, either multiply `source.pitch` by `Time.timeScale` on new voices (clamped above ~0.1) or drive pitch on the SFX group from the mixer (expose it, see `audio-setup-mixers`). Do not pitch the Master group if music must stay in tune.
