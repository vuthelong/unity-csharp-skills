# Platform notes

## Web (WebGL / WebGPU)

From the Unity Web audio manual page:

- Browsers block audio until the user clicks, taps or presses a key. Music started on load stays silent until then. Put a "click to start" or title screen before the first sound and start music from that input handler.
- Do not rely on `dspTime`-scheduled transitions created before the first interaction; reschedule after it. `PlayScheduled`, `SetScheduledStartTime` and `SetScheduledEndTime` are listed as supported; `dspTime` behaviour before unlock is not documented.
- Only positive pitch values work. `AudioCue.OnValidate` already clamps the minimum pitch above 0.
- AudioMixer: groups and exposed volume work; other parameters and all effects (Duck Volume, reverb, low-pass on groups) do not. Use `MusicPlayer.Duck` instead of a sidechain, and expect `AudioOcclusion`'s low-pass to be a volume-only effect at best.
- Clips are AAC. Loop points can glitch (see [music-recipes.md](music-recipes.md)). On iOS Safari, Decompress On Load clips may be silent in Silent Mode; prefer Compressed In Memory there.
- The browser can resample: read `clip.frequency` at runtime rather than assuming 44100 Hz. `MusicPlayer` already does.

Build-size and codec choices: `optimize-web`.

## Mobile interruptions

Phone calls, alarms, Siri/Assistant, other apps taking audio focus, and backgrounding usually reach Unity as `OnApplicationPause` or `OnApplicationFocus`; which one depends on the OS and the interruption, so handle both:

- Handle `OnApplicationPause(true)` by setting `AudioListener.pause = true` (the host in [architecture-and-di.md](architecture-and-di.md) does) and save volume settings with `PlayerPrefs.Save()`.
- On `OnApplicationPause(false)`, unpause and then verify state: if music should be playing and neither deck's `isPlaying` is true, restart the current track. Do not assume the platform resumed every source.
- iOS Player Settings that affect the audio session: **Mute Other Audio Sources** (off lets the user's own music keep playing; pair it with a "game music" toggle), **Prepare iOS for Recording** and **Force iOS Speakers when Recording** (only for microphone apps; they change routing and latency).
- Headphones unplugged / Bluetooth connected raise `AudioSettings.OnAudioConfigurationChanged` (below).

## Device changes: OnAudioConfigurationChanged

```csharp
private void OnEnable() => AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;

private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;

private void HandleAudioConfigurationChanged(bool deviceWasChanged)
{
    if (!deviceWasChanged) return;

    RestartMusicIfStopped();
}
```

- `deviceWasChanged` is true when the output device changed at runtime (OS control panel, default device switched, headset connected). It is false when the event was raised by your own `AudioSettings.Reset`.
- The official example restarts its `AudioSource` after handling the event. Treat every source as possibly stopped: restart music, let short SFX finish or drop.
- Scheduled `dspTime` values from before the change may be invalid. Reschedule intro→loop transitions from the current state.

## DSP buffer size and latency

Project Settings > Audio > **DSP Buffer Size**: Default, Best Latency, Good Latency, Best Performance. Smaller buffers lower input-to-sound latency but run the mixer more often, raising CPU cost and the risk of crackles on weak devices.

Read and change it at runtime:

```csharp
var config = AudioSettings.GetConfiguration();
config.dspBufferSize = 512;
if (!AudioSettings.Reset(config)) Debug.LogError("Audio reset failed");
```

- `AudioSettings.Reset` stops all playing audio and raises `OnAudioConfigurationChanged(false)`. Do it at boot or in a settings menu, never mid-gameplay.
- Approximate output latency: `bufferLength * numBuffers / outputSampleRate` from `AudioSettings.GetDSPBufferSize(out var bufferLength, out var numBuffers)`.
- Rhythm games and instruments: Best Latency, and measure crackling on the lowest target device. Everything else: Default or Best Performance. Android output latency varies widely by device regardless of this setting.
- `optimize-audio` flags buffers under 256 samples as a CPU risk.

## Voice limits

Project Settings > Audio > **Max Real Voices** (audible voices mixed each frame; Unity keeps the loudest) and **Max Virtual Voices** (voices tracked in total; keep it above the most the game can request or Unity warns). Read at runtime with `AudioSettings.GetConfiguration().numRealVoices` / `numVirtualVoices`. Size `AudioService.maxVoices` at or below the real voice count so your priority rules, not Unity's loudness culling, decide what is heard.
