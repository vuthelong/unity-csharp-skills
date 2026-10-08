# Voice Channels

## Channel Types

| Type | Join method | Use for |
|---|---|---|
| Non-positional (group) | `JoinGroupChannelAsync` | Party, team, lobby, guild — all participants hear each other equally |
| Echo | `JoinEchoChannelAsync` | Test-only — your own audio is echoed back |
| Positional (3D) | `JoinPositionalChannelAsync` | Proximity / spatial audio driven by transform position |

## Join Signatures

```csharp
Task JoinGroupChannelAsync(
    string channelName,
    ChatCapability chatCapability,
    ChannelOptions channelOptions = null);

Task JoinEchoChannelAsync(
    string channelName,
    ChatCapability chatCapability,
    ChannelOptions channelOptions = null);

Task JoinPositionalChannelAsync(
    string channelName,
    ChatCapability chatCapability,
    Channel3DProperties positionalChannelProperties,
    ChannelOptions channelOptions = null);
```

Bind `ChannelJoined(string channelName)` **before** calling a join method. The event can fire before the awaited call returns and re-fires when Vivox auto-reconnects, so drive "in channel" UI from the event rather than from the line after `await`.

## ChatCapability

- `ChatCapability.TextOnly` — text-only channel (no audio at all)
- `ChatCapability.AudioOnly` — voice-only, no text
- `ChatCapability.TextAndAudio` — both

## ChannelOptions

Optional. Common use: set this channel as the active transmit target on join success. Leave `null` for default behavior (join without changing transmission mode).

## Positional Channels — Channel3DProperties

`Channel3DProperties` controls how distance and direction affect voice attenuation. Key fields:

- `AudibleDistance` — beyond this, participant is inaudible.
- `ConversationalDistance` — below this, participant is at full volume.
- `AudioFadeIntensityByDistance` — falloff steepness between conversational and audible distance.
- `AudioFadeModel` — `InverseByDistance`, `LinearByDistance`, `ExponentialByDistance`.

Example call-site:

```csharp
var props = new Channel3DProperties(50, 5, 1.0f, AudioFadeModel.InverseByDistance);

await VivoxService.Instance.JoinPositionalChannelAsync(
    "world-proximity", ChatCapability.AudioOnly, props);
```

Pass the constructor arguments positionally (audible distance, conversational distance, fade intensity, fade model); parameter names have differed between package versions, so named arguments can fail to compile. Keep `conversationalDistance` < `audibleDistance`.

Drive per-frame position updates by calling `VivoxService.Instance.Set3DPosition(GameObject speakerObject, string channelName)` from a listener/speaker script (typically on the player camera and on remote player representations).

For >200 participants in a positional channel, enable the enterprise-tier Large 3D channels setting; see the documentation map's positional channels page.

## Leaving

```csharp
await VivoxService.Instance.LeaveChannelAsync(channelName);
await VivoxService.Instance.LeaveAllChannelsAsync();
```

Both fire `ChannelLeft(string channelName)` for each channel exited.

## Mic Permission

Joining an `AudioOnly` or `TextAndAudio` channel requires microphone access.

- **Android:** request `RECORD_AUDIO` at runtime with `Permission.RequestUserPermission(Permission.Microphone)` before the first audio-capable join. Merge `<uses-permission android:name="android.permission.RECORD_AUDIO"/>` if not present.
- **iOS:** add `NSMicrophoneUsageDescription` to the Info.plist (Project Settings → Player → iOS → Microphone Usage Description).
- **Desktop / WebGL:** the browser or OS prompts on first capture attempt; no code change required, but WebGL has additional limitations — see [troubleshooting.md](troubleshooting.md).

Android request with result callbacks (`UnityEngine.Android`), joining only once granted:

```csharp
#if UNITY_ANDROID
if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
{
    var callbacks = new PermissionCallbacks();
    callbacks.PermissionGranted += _ => JoinVoice();
    callbacks.PermissionDenied += _ => ShowTextOnlyFallback();
    Permission.RequestUserPermission(Permission.Microphone, callbacks);
    return;
}
#endif
JoinVoice();
```

On iOS, `Application.RequestUserAuthorization(UserAuthorization.Microphone)` returns an `AsyncOperation`; check `Application.HasUserAuthorization(UserAuthorization.Microphone)` after it completes.

## Muting

- **Local mic mute (self):** `VivoxService.Instance.MuteInputDevice()` / `UnmuteInputDevice()` stops your audio from being sent anywhere. Read state via `IsInputDeviceMuted`.
- **Deafen (self):** `MuteOutputDevice()` / `UnmuteOutputDevice()`.
- **Mute another player locally (only you stop hearing them):** `participant.MutePlayerLocally()` / `participant.UnmutePlayerLocally()` on the `VivoxParticipant` from `ParticipantAddedToChannel`.
- **Server-side kick / mute-all:** requires a privileged Vivox Access Token minted server-side.

## Push-to-Talk and Transmission

- **Push-to-talk:** keep the input device muted and call `UnmuteInputDevice()` while the bound Input System action is pressed, `MuteInputDevice()` on release. Read the action with `InputAction.WasPressedThisFrame()` / `WasReleasedThisFrame()`; do not use legacy `Input.GetKey` in new Unity 6 projects.
- **Which channel hears you:** `VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, channelName)` transmits to one channel only (e.g. team channel while also in a proximity channel); `TransmissionMode.All` transmits to every joined channel; `TransmissionMode.None` stops transmitting.

## Volume and Voice Activity Detection (Settings UI)

Verify these signatures against the installed package before use; they are stable across 16.x but not covered by the legacy v15 docs.

| Control | API | Notes |
|---|---|---|
| Mic volume slider | `VivoxService.Instance.SetInputDeviceVolume(int)` | Range -50..50, 0 = unity gain |
| Speaker volume slider | `VivoxService.Instance.SetOutputDeviceVolume(int)` | Range -50..50 |
| Per-player volume | `participant.SetLocalVolume(int)` | Local only, range -50..50 |
| Device pickers | `AvailableInputDevices` / `AvailableOutputDevices`, `SetActiveInputDeviceAsync`, `SetActiveOutputDeviceAsync` | Refresh lists on `AvailableInputDevicesChanged` |
| VAD tuning | `SetVoiceActivityDetectionPropertiesAsync(hangover, noiseFloor, sensitivity)` | Raise noise floor / lower sensitivity to cut keyboard and fan noise |
| Automatic VAD | `EnableAutoVoiceActivityDetectionAsync()` / `DisableAutoVoiceActivityDetectionAsync()` | Auto mode adapts to ambient noise; disable before manual tuning |
| Speaking indicator | `participant.SpeechDetected` + `ParticipantSpeechDetected` | Use `AudioEnergy` + `ParticipantAudioEnergyChanged` for a level meter |

Persist slider values (for example in `PlayerPrefs`) and re-apply them after each `LoginAsync`.
