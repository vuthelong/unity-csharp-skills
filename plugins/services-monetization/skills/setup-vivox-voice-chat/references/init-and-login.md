# Initialization and Login

## Package and Namespaces

Install `com.unity.services.vivox` via Package Manager. Add `using Unity.Services.Vivox;` to any script that touches the SDK. For UGS-backed auth also add `using Unity.Services.Core;` and `using Unity.Services.Authentication;`.

## Full Initialization Snippet

Grounded on the Vivox docs — do not deviate from this order.

```csharp
using System;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;

public class VivoxBootstrap : MonoBehaviour
{
    static bool s_Initialized;

    async void Start()
    {
        try
        {
            await InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    async Awaitable InitializeAsync()
    {
        if (s_Initialized)
            return;

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        await VivoxService.Instance.InitializeAsync();
        s_Initialized = true;

        VivoxService.Instance.LoggedIn  += OnLoggedIn;
        VivoxService.Instance.LoggedOut += OnLoggedOut;

        await VivoxService.Instance.LoginAsync(new LoginOptions
        {
            DisplayName = AuthenticationService.Instance.PlayerName ?? "Player",
            EnableTTS   = false
        });
    }

    void OnLoggedIn()  { }
    void OnLoggedOut() { }

    void OnDestroy()
    {
        if (VivoxService.Instance == null) return;
        VivoxService.Instance.LoggedIn  -= OnLoggedIn;
        VivoxService.Instance.LoggedOut -= OnLoggedOut;
    }
}
```

## VivoxConfigurationOptions

`InitializeAsync` takes an optional `VivoxConfigurationOptions`. Common fields: log level, audio ducking behavior, server region. Leave defaults for most projects; only override when platform-specific tuning is documented in the Vivox docs (e.g. mobile ducking).

## LoginOptions

| Field | Notes |
|---|---|
| `DisplayName` | Shown to other participants via `VivoxParticipant.DisplayName`. Session-only, not persisted. Max 127 bytes. Sanitize / uniqueness-check server-side; the SDK does not validate. |
| `EnableTTS` | Enables text-to-speech injection into channels. Off by default. |
| Blocked list | Preload users blocked by this player. |

The identity Vivox binds this login to is the current `AuthenticationService.Instance.PlayerId` — that's how other clients address you for directed messages. If you skip UAS, Vivox falls back to a per-session GUID and cross-session identity is lost.

## Sign Out

```csharp
await VivoxService.Instance.LogoutAsync();
```

`LogoutAsync` fires `LoggedOut`. Call it before shutting the app down cleanly; the SDK also handles ungraceful teardown but explicit logout gives you a clean disconnect on the server side.

## Access Tokens (VAT) — When You Need Them

The default UGS-backed path automatically mints access tokens signed by your UGS project. You don't touch tokens in code.

You need to switch to server-side VAT minting when:

- You're not using UGS Authentication (custom identity system).
- You need privileged tokens: kick a user from a channel, mute-all, transcription enable, join-muted.
- You want channel-scoped ACLs (only players holding a valid join token for `raid-42` can enter).

Do **not** embed the Vivox app secret / HMAC signing key in client code. See the "Access Token Developer Guide" and the C++, C#, Python, and JavaScript minting examples in the Unity Vivox documentation map for server implementations.

## Re-init Guard

Calling `VivoxService.Instance.InitializeAsync()` twice throws `5041 VxErrorAlreadyInitialized`. Guard with a static flag as in the bootstrap above, or keep the bootstrap on a `DontDestroyOnLoad` GameObject so `Start` runs once. If the project disables domain reload (Enter Play Mode Options), reset the flag in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method.

If `VivoxService.Instance.IsLoggedIn` is already `true`, skip `LoginAsync` as well.
