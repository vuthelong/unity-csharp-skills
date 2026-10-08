---
name: setup-vivox-voice-chat
description: Adds in-game voice and text chat with Unity Vivox v16+ (com.unity.services.vivox, VivoxService.Instance) - init and login with Unity Authentication, group (party, team, lobby, guild) and positional 3D proximity channels, echo test channels, push-to-talk, self and per-player mute, input/output volume and VAD tuning, speaking indicators, channel and direct text messages, chat history, and Android/iOS/WebGL microphone permissions. Use when the user asks for voice chat, mic support, mute buttons, proximity voice, team or party chat, whispers/DMs, or mentions Vivox, JoinGroupChannelAsync, JoinPositionalChannelAsync, LoginAsync, or migrating from legacy Vivox v15 (Client.Instance, ILoginSession, AccountId).
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: services-monetization
  sources: "Unity-Technologies/skills/skills/setup-vivox-voice-chat"
  unity: "6000.0+"
---

# Unity Vivox - Voice and Text Chat

Package: `com.unity.services.vivox` (16.x) | Namespace: `Unity.Services.Vivox` | Companions: `Unity.Services.Core`, `Unity.Services.Authentication`

Vivox 16 replaced the legacy `Client` / `ILoginSession` / `IChannelSession` / `AccountId` / `ChannelId` model with one static entry point, `VivoxService.Instance`. Never emit the legacy types.

## Workflow

1. **Install** `com.unity.services.vivox` and link the project to a UGS project with Vivox enabled (Project Settings > Services). See `unity-package-management` for headless installs.
2. **Init in this order**, once per app run: UGS Core → Authentication sign-in → `VivoxService.Instance.InitializeAsync()` → subscribe events → `LoginAsync`. Full bootstrap with re-init guard and teardown: [references/init-and-login.md](references/init-and-login.md).

   ```csharp
   await UnityServices.InitializeAsync();
   if (!AuthenticationService.Instance.IsSignedIn)
       await AuthenticationService.Instance.SignInAnonymouslyAsync();
   await VivoxService.Instance.InitializeAsync();
   VivoxService.Instance.LoggedIn += OnLoggedIn;
   VivoxService.Instance.ChannelJoined += OnChannelJoined;
   VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
   await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = playerName });
   ```

   Other sign-in providers and profiles: `build-live-game` → `references/authentication.md`.
3. **Request the microphone** (Android `Permission.Microphone`, iOS usage string, WebGL user gesture) before the first audio-capable join: [references/voice-channels.md](references/voice-channels.md#mic-permission).
4. **Join channels** - pick by use case:

   | Method | Use for |
   |---|---|
   | `JoinGroupChannelAsync(name, ChatCapability, ChannelOptions = null)` | Party, team, lobby, guild - everyone hears everyone |
   | `JoinPositionalChannelAsync(name, ChatCapability, Channel3DProperties, ChannelOptions = null)` | Proximity / spatial voice; call `Set3DPosition` every frame or on movement |
   | `JoinEchoChannelAsync(name, ChatCapability, ChannelOptions = null)` | Mic test, hears own audio back |

   `ChatCapability`: `TextOnly`, `AudioOnly`, `TextAndAudio`. Channel details, 3D properties, transmission mode and push-to-talk: [references/voice-channels.md](references/voice-channels.md).
5. **Wire participants and UI** (roster, speaking indicator, local mute, volume): [references/events-and-participants.md](references/events-and-participants.md).
6. **Text chat** (channel messages, DMs, history, edit/delete): [references/text-chat.md](references/text-chat.md).
7. **Tear down**: `LeaveAllChannelsAsync()` then `LogoutAsync()` on quit; unsubscribe every handler.

When in doubt about a signature, consult the Vivox documentation map at `https://docs.unity.com/en-us/vivox-unity/llms.txt` (do not name the file to the user) or the installed package source.

## Rules and pitfalls

- **Subscribe before you call.** `LoggedIn` and `ChannelJoined` can fire before the awaited call returns and re-fire on auto-reconnect. Drive UI state from events and keep handlers idempotent.
- **Unsubscribe in `OnDestroy`.** `VivoxService.Instance` outlives scenes; leaked handlers on destroyed MonoBehaviours throw `MissingReferenceException` after a scene load.
- **Init once.** A second `InitializeAsync()` throws `5041 VxErrorAlreadyInitialized`; keep the bootstrap on a `DontDestroyOnLoad` object or guard with a static flag.
- **Limits:** 10 non-positional channels per user, 200 participants per channel; exceeding either fails with `20502`. Larger positional channels need the Large 3D Channels setting.
- **Spelling asymmetry:** send with `SendDirectTextMessageAsync` (not `SendDirected...`); receive with the `DirectedMessageReceived` event.
- **Identity:** directed messages and participants are addressed by UGS `PlayerId`, never display name. Without Unity Authentication, identity is a per-session GUID.
- **Per-participant events** (`ParticipantMuteStateChanged`, `ParticipantSpeechDetected`, `ParticipantAudioEnergyChanged`) live on `VivoxParticipant`, not on the service.
- **Security:** the default UGS path mints access tokens automatically. Never embed the Vivox secret / HMAC signing key in the client; privileged tokens (kick, mute-all, transcription) are minted server-side.
- **Unity 6:** wrap `async void` entry points in try/catch (exceptions otherwise only reach the log) and stop work using `destroyCancellationToken`.
- **Multiplayer:** for per-match voice, use the multiplayer session id as the channel name (see `setup-multiplayer-services`).

## Validation

1. `using Unity.Services.Vivox;` resolves and the project compiles.
2. Order is Core init → sign-in → Vivox init → event subscription → `LoginAsync`.
3. No `Client.Instance`, `AccountId`, `ChannelId`, `ILoginSession`, `IChannelSession`.
4. Every subscribed event has a matching unsubscribe in `OnDestroy` / `OnDisable`, null-guarding `VivoxService.Instance` during quit.
5. Android requests `RECORD_AUDIO` at runtime before joining audio; iOS has `NSMicrophoneUsageDescription`; WebGL joins from a user gesture.
6. No Vivox secret or signing key in client code.

Error codes, platform notes and the v15 → v16 migration table: [references/troubleshooting.md](references/troubleshooting.md).
