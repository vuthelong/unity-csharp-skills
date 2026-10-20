# LevelPlay Ad Units: Shared API and Lifecycle

Shared surface of the three ad unit classes in `Unity.Services.LevelPlay` (SDK 9.x Ad Unit API):
`LevelPlayRewardedAd`, `LevelPlayInterstitialAd`, `LevelPlayBannerAd`. Format-specific API, events and
reference implementations live in [rewarded-api.md](rewarded-api.md),
[interstitial-api.md](interstitial-api.md) and [banner-api.md](banner-api.md). Verify signatures against
the installed package (`Packages/packages-lock.json` → `com.unity.services.levelplay`) when in doubt.

## Contents
- [Lifecycle](#lifecycle)
- [Construction and Config Builder](#construction-and-config-builder)
- [Shared Methods](#shared-methods)
- [Events](#events)
- [Threading](#threading)
- [OnAdInfoChanged](#onadinfochanged)
- [Data Types](#data-types)
- [Retry and Backoff](#retry-and-backoff)
- [Error 1037: Load During Load](#error-1037-load-during-load)
- [Placements and Capping](#placements-and-capping)
- [Cleanup: Unsubscribe and DestroyAd](#cleanup-unsubscribe-and-destroyad)
- [Common Issues](#common-issues)
- [Shared Testing Checklist](#shared-testing-checklist)

## Lifecycle

1. **Create** the ad object only after `LevelPlay.Init()` succeeds (in or after `OnInitSuccess`). Creating it earlier is the most common "ad not loading" cause.
2. **Subscribe** to events right after construction (including `OnAdImpressionDataReady` on SDK 9.5.0+ when ILRD is wanted, see [ilrd-api.md](ilrd-api.md)).
3. **Load** with `LoadAd()`. The SDK never loads automatically (unlike legacy `IronSource.Agent` rewarded video).
4. **Check readiness**: rewarded and interstitial use `IsAdReady()`; banners have no `IsAdReady()` and are ready once `OnAdLoaded` fires.
5. **Show** with `ShowAd()` / `ShowAd(placementName: ...)`.
6. **Reload** after close or display failure (full-screen formats; one load per object for banners).
7. **Clean up** in `OnDestroy()`: unsubscribe every handler, then `DestroyAd()` where applicable.

Managers must be `MonoBehaviour`s on a persistent GameObject (`DontDestroyOnLoad`, same object as the initializer is fine).

## Construction and Config Builder

Every format has two constructors:

| Constructor | Notes |
|---|---|
| `new LevelPlayXxxAd(string adUnitId)` | Default config. `adUnitId` from the LevelPlay dashboard. |
| `new LevelPlayXxxAd(string adUnitId, Config config)` | `config` built with `LevelPlayXxxAd.Config.Builder()` |

Builder (`LevelPlayXxxAd.Config.Builder`), shared methods:

| Method | Notes |
|---|---|
| `new LevelPlayXxxAd.Config.Builder()` | Returns `LevelPlayXxxAd.Config.Builder` |
| `SetBidFloor(double)` | Minimum bid price in USD, e.g. `SetBidFloor(1.0)` = $1.00 CPM. Raises eCPM, lowers fill. |
| `Build()` | Returns `LevelPlayXxxAd.Config` |

Builder calls can be chained:

```csharp
var config = new LevelPlayRewardedAd.Config.Builder()
    .SetBidFloor(0.80)
    .Build();
rewardedAd = new LevelPlayRewardedAd(adUnitId, config);
```

Banner adds `SetSize`, `SetPosition`, `SetDisplayOnLoad`, `SetRespectSafeArea`, `SetPlacementName` (see [banner-api.md](banner-api.md)).

## Shared Methods

| Method | Rewarded | Interstitial | Banner | Notes |
|---|---|---|---|---|
| `void LoadAd()` | yes | yes | yes | Call after init, after close, after load/display failure (full-screen). Banner: once per object lifetime, except failure retry. |
| `void ShowAd()` | yes | yes | yes | Full-screen: always guard with `IsAdReady()`. Banner: must be loaded first. |
| `void ShowAd(placementName: string)` | yes | yes | no (use `SetPlacementName` in config) | Placement configured in the dashboard, used for reporting and capping. |
| `bool IsAdReady()` | yes | yes | **no** | True when loaded and showable. |
| `static bool IsPlacementCapped(string placementName)` | `LevelPlayRewardedAd.IsPlacementCapped` | `LevelPlayInterstitialAd.IsPlacementCapped` | no | True when the placement hit its cap. |
| `void DestroyAd()` | see note | yes | yes | Frees native resources. Object is unusable afterwards; create a new one to show again. |

Note: the code-gen rules in [best-practices.md](best-practices.md) require `DestroyAd()` in `OnDestroy()` for banner and interstitial managers; the rewarded reference implementation only unsubscribes. Check the installed package for `LevelPlayRewardedAd.DestroyAd()` before relying on it.

## Events

All events are members of the ad object.

| Event | Signature | Rewarded | Interstitial | Banner |
|---|---|---|---|---|
| `OnAdLoaded` | `event Action<LevelPlayAdInfo>` | yes | yes | yes |
| `OnAdLoadFailed` | `event Action<LevelPlayAdError>` | yes | yes | yes |
| `OnAdDisplayed` | `event Action<LevelPlayAdInfo>` | yes | yes | yes |
| `OnAdDisplayFailed` | `event Action<LevelPlayAdInfo, LevelPlayAdError>` | yes | yes | yes |
| `OnAdClicked` | `event Action<LevelPlayAdInfo>` | yes | yes | yes |
| `OnAdClosed` | `event Action<LevelPlayAdInfo>` | yes | yes | no |
| `OnAdInfoChanged` | `event Action<LevelPlayAdInfo>` | yes | yes | no |
| `OnAdRewarded` | `event Action<LevelPlayAdInfo, LevelPlayReward>` | yes | no | no |
| `OnAdExpanded` / `OnAdCollapsed` / `OnAdLeftApplication` | `event Action<LevelPlayAdInfo>` | no | no | yes |
| `OnAdImpressionDataReady` | per-instance ILRD, SDK 9.5.0+ | yes | yes | yes |

`OnAdImpressionDataReady` replaces the global `LevelPlay.OnImpressionDataReady`, which is deprecated on 9.5.0+ (compiler warning). On 9.4.x and earlier only the global event exists. Details: [ilrd-api.md](ilrd-api.md).

## Threading

All ad unit callbacks run on the Unity main thread, so Unity APIs (UI, GameObjects) are safe inside them. The global `LevelPlay.OnImpressionDataReady` runs on a background thread.

## OnAdInfoChanged

Rewarded and interstitial only. Fires when the loaded ad's info changes, e.g. a new auction winner becomes available:

- after a new ad loads with different properties than the previous one
- when the waterfall selects a different ad network
- when auction results change

The SDK shows the current winner on `ShowAd()` automatically; the event only reports the change. The updated `LevelPlayAdInfo` carries the latest revenue estimate and network, so always log the most recent `adInfo`. With ILRD wired, `LevelPlayImpressionData` already holds the final revenue, so `OnAdInfoChanged` is mostly a debugging/logging hook.

```csharp
interstitialAd.OnAdInfoChanged += adInfo =>
    Debug.Log($"Ad info changed - network: {adInfo.AdNetwork}, revenue: ${adInfo.Revenue}");
```

## Data Types

### LevelPlayAdInfo

| Property | Type | Notes |
|---|---|---|
| `AdId` | string | Unique id of this ad instance |
| `AdUnitId` | string | |
| `AdUnitName` | string | |
| `AdSize` | LevelPlayAdSize | May be null; banner-relevant |
| `AdFormat` | string | e.g. `"REWARDED"`, `"INTERSTITIAL"`, `"BANNER"` |
| `PlacementName` | string | Placement where the ad was shown |
| `AuctionId` | string | |
| `CreativeId` | string | |
| `Country` | string | ISO 3166-1 |
| `Ab` | string | A/B test segment |
| `SegmentName` | string | |
| `AdNetwork` | string | Network that served the ad |
| `InstanceName` | string | |
| `InstanceId` | string | |
| `Revenue` | double? | Estimated USD; nullable, check for null |
| `Precision` | string | Revenue precision |
| `EncryptedCPM` | string | |

### LevelPlayAdError

| Property | Type |
|---|---|
| `ErrorCode` | int |
| `ErrorMessage` | string |
| `AdUnitId` | string |
| `AdId` | string |

Error code table (524, 526, 1037, ...): [initialization-api.md](initialization-api.md).

## Retry and Backoff

Retry on `OnAdLoadFailed`, never in `Update()`. Reference samples use a fixed delay (30 s rewarded/interstitial, 60 s banner); exponential backoff is the recommended upgrade:

```csharp
private const float BaseRetryDelay = 5f;
private const float MaxRetryDelay = 120f;
private int retryAttempt;

private void OnAdLoadFailed(LevelPlayAdError error)
{
    Debug.LogWarning($"Load failed ({error.ErrorCode}): {error.ErrorMessage}");
    float delay = Mathf.Min(MaxRetryDelay, BaseRetryDelay * Mathf.Pow(2f, retryAttempt));
    retryAttempt++;
    Invoke(nameof(LoadAd), delay);
}

private void OnAdLoaded(LevelPlayAdInfo adInfo)
{
    retryAttempt = 0;
}
```

On `OnAdDisplayFailed`, full-screen formats load a fresh ad. Load failures are expected (no fill, offline): log with `Debug.LogWarning`, degrade gracefully, never block game flow.

## Error 1037: Load During Load

Full-screen formats. Calling `LoadAd()` while a load is in flight fails with error 1037 ("Cannot load an interstitial while another interstitial is loading"; the rewarded doc phrases it "Cannot load while another load is in progress"). Typical cause:

```csharp
void Update()
{
    if (!rewardedAd.IsAdReady())
    {
        rewardedAd.LoadAd();
    }
}
```

Guard with a loading flag:

```csharp
private bool isLoadingAd;

void LoadAdGuarded()
{
    if (isLoadingAd) return;
    isLoadingAd = true;
    ad.LoadAd();
}

void OnAdLoaded(LevelPlayAdInfo adInfo) => isLoadingAd = false;
void OnAdLoadFailed(LevelPlayAdError error) => isLoadingAd = false;
```

Banners do not raise 1037 but still take exactly one `LoadAd()` per object (see [banner-api.md](banner-api.md)).

## Placements and Capping

- Create placements in the LevelPlay dashboard; pass them with `ShowAd(placementName: "name")` (banner: `SetPlacementName`).
- Before showing a placement, check the static `IsPlacementCapped(placementName)`. Showing a capped placement fails with 524 (placement pacing/capping limit) or 526 (ad unit daily cap per session).
- One ad unit can serve several placements; track which placement is showing if rewards or analytics differ per placement.

## Cleanup: Unsubscribe and DestroyAd

```csharp
void OnDestroy()
{
    if (ad == null) return;
    ad.OnAdLoaded -= OnAdLoaded;
    ad.OnAdLoadFailed -= OnAdLoadFailed;
    ad.OnAdDisplayed -= OnAdDisplayed;
    ad.OnAdDisplayFailed -= OnAdDisplayFailed;
    ad.OnAdClicked -= OnAdClicked;
    ad.DestroyAd();
    ad = null;
}
```

Unsubscribe every handler you subscribed (format-specific events and `OnAdImpressionDataReady` too). Missing unsubscription leaks managers and causes duplicate callbacks (e.g. double reward grants).

## Common Issues

**Ad not loading**
- Ad object created before `OnInitSuccess`
- No connectivity, or no inventory (in the test region)
- Wrong ad unit ID: verify in the dashboard
- Test on a real device with a good connection; in the Editor the build target must be Android/iOS for mock ads

**Ad loaded but not showing**
- `ShowAd()` called without `IsAdReady()` (full-screen)
- Ad expired: loaded too long ago; load close to when it will be shown (within a few minutes)

**Callbacks not firing**: subscriptions made on a different/destroyed object, or the manager is not on a persistent GameObject. Log every callback while debugging. Mock ads in the Editor do not fire failure, click or impression callbacks ([testing-and-validation.md](testing-and-validation.md)).

## Shared Testing Checklist

- [ ] Ad loads after SDK initialization
- [ ] `IsAdReady()` true when loaded (full-screen formats)
- [ ] Ad displays on `ShowAd()`
- [ ] Ad reloads after being shown (full-screen formats)
- [ ] Retry logic works when load fails (test in airplane mode; game must not block or crash)
- [ ] Multiple placements track correctly and placement capping works (if used)
- [ ] Events unsubscribed, no leaks or duplicate callbacks
- [ ] Tested on multiple devices and network conditions
