# Music recipes

`MusicPlayer` owns two decks. Each deck has an intro source and a body source so an intro→loop track can crossfade with the previous track.

## Equal-power crossfade

A linear crossfade (`a = 1 - t`, `b = t`) dips about 3 dB at the midpoint because uncorrelated signals add in power, not amplitude. Equal power keeps `a² + b² = 1`:

```csharp
var incoming = Mathf.Sin(t * Mathf.PI * 0.5f);
var outgoing = outgoingStartGain * Mathf.Cos(t * Mathf.PI * 0.5f);
```

- Drive `t` with `Time.unscaledDeltaTime` so a pause menu at `timeScale = 0` can still change music.
- Use linear fades only for two takes of the same material (correlated signals), where equal power bulges by 3 dB.
- `MusicPlayer` writes `AudioSource.volume`, not the mixer. The Music mixer group carries the user's volume setting; fades and ducking multiply under it, so they never fight `AudioSettingsStore`.
- A new `Play` during a fade restarts the quieter deck; its fading tail is cut. For very rapid track changes add a third deck or fade the tail out first.

## Sample-accurate loop and intro→loop

`AudioSource.loop = true` is sample-accurate when the clip itself loops cleanly:

- Author WAV/AIFF masters. MP3 adds encoder padding, so an MP3 master never loops seamlessly. Vorbis-imported clips loop correctly from a WAV master.
- On Web, clips are AAC; the encoder can alter the first 1024 samples. The Unity Web audio page recommends a WAV with at least 1024 silent samples at the start and an adjusted `smpl` chunk.

Intro then loop uses the DSP clock:

```csharp
var start = AudioSettings.dspTime + 0.1;
intro.PlayScheduled(start);
body.loop = true;
body.PlayScheduled(start + introClip.samples / (double)introClip.frequency);
```

- Compute durations from `samples / (double)frequency`. `AudioClip.length` is a `float`; at long durations the rounding error is audible as a click or gap.
- Schedule ahead. The `PlayScheduled` docs recommend ~100–200 ms; a time already in the past starts as soon as possible and skips the missed audio.
- Use two sources. Re-assigning `clip` on a source that is already scheduled cancels or glitches the transition.
- `SetScheduledEndTime(dspTime)` stops a source on an exact sample, for example to end an intro early at a bar line, or to cut a non-looping body for a stinger. `SetScheduledStartTime` moves a scheduled start.
- `Streaming` clips need time to open the stream; preload with `LoadAudioData` and schedule further ahead (0.3–0.5 s), see `optimize-audio` for Load Type choice.

## Pausing music

- `AudioListener.pause = true` freezes `AudioSettings.dspTime` and every scheduled play request; scheduled intro→loop transitions stay in sync after unpausing. This is the right tool for focus loss and full-game pause.
- `MusicPlayer.Pause()` is music-only (for example, menu music on another source with `ignoreListenerPause`). It stops a pending scheduled body and reschedules it from the intro's `timeSamples` on `Resume()`.
- Music sources with `ignoreListenerPause = true` keep playing during listener pause, but `dspTime` is frozen; do not schedule new music while paused in that configuration.

## Playlists

`PlayPlaylist(tracks, shuffle, fade)` plays tracks non-looping and starts the next one `fade` seconds before the current end (`EndDsp`, computed on the DSP clock). Shuffle avoids an immediate repeat. For long playlists load tracks on demand through Addressables (see [loading.md](loading.md)) instead of holding an `AudioClip[]` that keeps every track's data referenced.

## Ducking under dialogue

Preferred, no script per frame:

1. **Sidechain.** Add Duck Volume on the Music group and a Send from the Voice group to it (user creates both in the Audio Mixer window). Reacts on the audio thread to actual dialogue level.
2. **Snapshot.** A `Dialogue` snapshot with Music at -10 dB; `dialogueSnapshot.TransitionTo(0.25f)` on line start, back to `Default` on line end. Set the mixer's Update Mode to Unscaled Time if dialogue can run while paused.

Both are covered in `audio-setup-mixers` (runtime mixer control). Neither works on Web, where mixer effects are unsupported and only group volume works; use the script fallback there:

```csharp
audioService.Music.Duck(0.35f, 0.25f);
audioService.Music.Duck(1f, 0.6f);
```

## Focus loss

Desktop: `OnApplicationFocus(false)` → `AudioListener.pause = true` (see the host in [architecture-and-di.md](architecture-and-di.md)). Respect Project Settings > Player > Resolution > **Run In Background**: when it is on, players often expect music to continue; make pause-on-focus-loss a user option. Mobile: see [platform-notes.md](platform-notes.md).
