# Rewarded Ads API Reference

Rewarded ads are opt-in video ads watched in exchange for an in-game reward. Highest-earning format; user-initiated, so good UX when done right. Best for extra lives, hints, currency, power-ups, skipping waits.

Shared lifecycle, constructors, `Config.Builder` (`SetBidFloor`), shared events, `LevelPlayAdInfo` / `LevelPlayAdError`, threading, retry/backoff, error 1037, placement capping and cleanup are in [ad-format-common.md](ad-format-common.md). This file covers what is specific to `LevelPlayRewardedAd`.

## Contents
- [Reference Implementation](#reference-implementation)
- [Multiple Placements](#multiple-placements)
- [Rewarded-Specific API](#rewarded-specific-api)
- [Loading Strategy](#loading-strategy)
- [Reward Granting](#reward-granting)
- [UI Integration](#ui-integration)
- [Common Issues](#common-issues)
- [Testing Checklist](#testing-checklist)

## Reference Implementation

`RewardedAdManager` below is the manager the patterns in [best-practices.md](best-practices.md) build on (public `LoadAd()`, `ShowAd()`, `IsAdReady()`, `OnRewardGranted`).

```csharp
using UnityEngine;
using Unity.Services.LevelPlay;

public class RewardedAdManager : MonoBehaviour
{
    private LevelPlayRewardedAd rewardedAd;
    private string adUnitId = "YOUR_REWARDED_AD_UNIT_ID";

    // Optional: assign a handler to be notified when the user earns the reward.
    // Callers (e.g. a HintSystem) set this before calling ShowAd().
    public System.Action OnRewardGranted;

    void Start()
    {
        // Create the rewarded ad object and register event listeners.
        // Do not call LoadAd() here — load is triggered explicitly by a publisher-controlled
        // action (e.g. a button press, scene entry, or a point in gameplay where the ad
        // should be available). See LoadAd() below.
        rewardedAd = new LevelPlayRewardedAd(adUnitId);

        // ILRD (SDK 9.5.0+): if impression-revenue tracking is enabled, also subscribe here —
        //   rewardedAd.OnAdImpressionDataReady += OnImpressionDataReady;   // see references/ilrd-api.md

        // Register event listeners
        rewardedAd.OnAdLoaded += OnAdLoaded;
        rewardedAd.OnAdLoadFailed += OnAdLoadFailed;
        rewardedAd.OnAdDisplayed += OnAdDisplayed;
        rewardedAd.OnAdDisplayFailed += OnAdDisplayFailed;
        rewardedAd.OnAdRewarded += OnAdRewarded;
        rewardedAd.OnAdClosed += OnAdClosed;
        rewardedAd.OnAdClicked += OnAdClicked;
        rewardedAd.OnAdInfoChanged += OnAdInfoChanged;   // Fires when the loaded ad updates after a new auction result
    }

    void OnDestroy()
    {
        // Unregister event listeners
        if (rewardedAd != null)
        {
            rewardedAd.OnAdLoaded -= OnAdLoaded;
            rewardedAd.OnAdLoadFailed -= OnAdLoadFailed;
            rewardedAd.OnAdDisplayed -= OnAdDisplayed;
            rewardedAd.OnAdDisplayFailed -= OnAdDisplayFailed;
            rewardedAd.OnAdRewarded -= OnAdRewarded;
            rewardedAd.OnAdClosed -= OnAdClosed;
            rewardedAd.OnAdClicked -= OnAdClicked;
            rewardedAd.OnAdInfoChanged -= OnAdInfoChanged;
            // ILRD (9.5.0+): rewardedAd.OnAdImpressionDataReady -= OnImpressionDataReady;
        }
    }

    // Call this from the trigger point in your game where you want the ad to be available —
    // for example, from a UI button handler, on entering a scene, or at a natural moment
    // in gameplay. Unlike the legacy IronSource rewarded video, the SDK does not load
    // automatically; LoadAd() must always be called explicitly.
    public void LoadAd()
    {
        Debug.Log("Loading rewarded ad...");
        rewardedAd.LoadAd();
    }

    public void ShowAd()
    {
        if (rewardedAd.IsAdReady())
        {
            Debug.Log("Showing rewarded ad");
            rewardedAd.ShowAd();
        }
        else
        {
            Debug.LogWarning("Rewarded ad is not ready yet");
        }
    }

    // True once a rewarded ad has finished loading and can be shown.
    // Use this to decide whether to offer the ad before calling ShowAd().
    public bool IsAdReady() => rewardedAd != null && rewardedAd.IsAdReady();

    // Event Callbacks
    private void OnAdLoaded(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Rewarded ad loaded successfully");
    }

    private void OnAdLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"Rewarded ad failed to load: {error.ErrorMessage}");
        // Retry loading after a delay
        Invoke(nameof(LoadAd), 30f);
    }

    private void OnAdDisplayed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Rewarded ad displayed");
    }

    private void OnAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogError($"Rewarded ad failed to display: {error.ErrorMessage}");
        // Load a new ad
        LoadAd();
    }

    private void OnAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        Debug.Log($"User earned reward: {reward.Amount} {reward.Name}");
        // Grant the reward to the user
        GrantReward(reward);
        // Notify any external listener (e.g. HintSystem) that the reward was earned
        OnRewardGranted?.Invoke();
    }

    private void OnAdClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Rewarded ad closed");
        // LoadAd() is not called here automatically. If your game preloads rewarded ads
        // eagerly (so one is always ready), call LoadAd() here. If load is triggered by
        // a player action, wait for that action instead.
    }

    private void OnAdClicked(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Rewarded ad clicked");
    }

    private void OnAdInfoChanged(LevelPlayAdInfo adInfo)
    {
        // Optional: log or update analytics with the latest revenue estimate
        Debug.Log($"Rewarded ad info changed - network: {adInfo.AdNetwork}, revenue: ${adInfo.Revenue}");
    }

    private void GrantReward(LevelPlayReward reward)
    {
        // Implement your reward logic here
        Debug.Log($"Granting {reward.Amount} {reward.Name} to the user");
    }
}
```

## Multiple Placements

One ad unit, several dashboard placements, reward routed by placement:

```csharp
private string currentPlacement;

public void ShowAdForHints() => ShowAdWithPlacement("hints");
public void ShowAdForExtraLives() => ShowAdWithPlacement("extra_lives");

private void ShowAdWithPlacement(string placementName)
{
    if (!rewardedAd.IsAdReady()) return;
    if (LevelPlayRewardedAd.IsPlacementCapped(placementName)) return;
    currentPlacement = placementName;
    rewardedAd.ShowAd(placementName: placementName);
}

private void OnAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
{
    switch (currentPlacement)
    {
        case "hints": GrantHints(reward); break;
        case "extra_lives": GrantExtraLives(reward); break;
    }
}

private void OnAdClosed(LevelPlayAdInfo adInfo)
{
    currentPlacement = null;
    rewardedAd.LoadAd();
}

private void OnAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
{
    currentPlacement = null;
    rewardedAd.LoadAd();
}
```

## Rewarded-Specific API

| Member | Signature | Notes |
|---|---|---|
| Constructors | `new LevelPlayRewardedAd(string adUnitId)`, `new LevelPlayRewardedAd(string adUnitId, LevelPlayRewardedAd.Config config)` | Only after `OnInitSuccess` |
| Builder | `LevelPlayRewardedAd.Config.Builder` → `SetBidFloor(double)` → `Build()` returns `LevelPlayRewardedAd.Config` | |
| `GetReward` | `LevelPlayReward GetReward(string placementName = null)` | null returns the default reward; use it to label UI ("Watch ad to earn 5 coins") |
| `IsAdReady` | `bool IsAdReady()` | Check before enabling the "watch ad" button |
| `IsPlacementCapped` | `static bool LevelPlayRewardedAd.IsPlacementCapped(string placementName)` | |
| `ShowAd` | `ShowAd()`, `ShowAd(placementName: string)` | |
| `OnAdRewarded` | `event Action<LevelPlayAdInfo, LevelPlayReward>` | The only place to grant the reward |
| `OnAdClosed` | `event Action<LevelPlayAdInfo>` | Resume game; reload only if preloading eagerly |
| `OnAdInfoChanged` | `event Action<LevelPlayAdInfo>` | See [ad-format-common.md](ad-format-common.md#onadinfochanged) |

```csharp
LevelPlayReward defaultReward = rewardedAd.GetReward();
LevelPlayReward placementReward = rewardedAd.GetReward("extra_lives");
Debug.Log($"{placementReward.Amount} {placementReward.Name}");
```

### LevelPlayReward

| Property | Type | Example |
|---|---|---|
| `Name` | string | `"coins"`, `"life"` |
| `Amount` | int | `10`, `1` |

## Loading Strategy

Unlike legacy IronSource rewarded video (SDK-managed auto-loading), `LevelPlayRewardedAd` requires an explicit `LoadAd()`. Create the object and subscribe in `OnInitSuccess`; loading is a separate step.

**Explicit load (matches the official sample):** load from a publisher-controlled trigger (button, scene entry, gameplay moment).

```csharp
void OnInitSuccess(LevelPlayConfiguration config)
{
    rewardedAd = new LevelPlayRewardedAd(adUnitId);
    rewardedAd.OnAdLoaded += OnAdLoaded;
    rewardedAd.OnAdClosed += OnAdClosed;
}

public void OnPlayerReachesRewardedAdOpportunity()
{
    rewardedAd.LoadAd();
}
```

**Eager preload (optional, conscious choice):** call `LoadAd()` right after creation in `OnInitSuccess` and again in `OnAdClosed`, so one is always ready. Valid, but it hides the manual-load requirement; do not treat it as automatic legacy behavior.

## Reward Granting

- Grant in `OnAdRewarded`, never in `OnAdClosed`: users may close early.
- `OnAdRewarded` and `OnAdClosed` are asynchronous; `OnAdRewarded` may fire **after** `OnAdClosed`. Do not write `OnAdClosed` logic that assumes the reward was already granted.
- For server-verified grants see the `build-live-game` skill (Cloud Code).

## UI Integration

```csharp
public Button watchAdButton;

void Update()
{
    watchAdButton.interactable = rewardedAd != null && rewardedAd.IsAdReady();
}

void OnAdLoaded(LevelPlayAdInfo adInfo) => watchAdButton.interactable = true;
void OnAdLoadFailed(LevelPlayAdError error) => watchAdButton.interactable = false;
```

Reading `IsAdReady()` in `Update()` is fine; calling `LoadAd()` there is not (error 1037).

## Common Issues

| Issue | Causes | Fix |
|---|---|---|
| Reward not granted | Granting in `OnAdClosed`; user closed early; reward logic bug | Grant in `OnAdRewarded` (fires once the user earned it) |
| Button always disabled | `IsAdReady()` misused; load failed with no retry; subscriptions broken | Enable in `OnAdLoaded`, retry in `OnAdLoadFailed`, log every callback |
| Multiple reward grants | `OnAdRewarded` subscribed more than once; no per-ad grant flag | Subscribe once, unsubscribe in `OnDestroy()`, track "granted for this ad" |
| Error 1037 / not loading | See [ad-format-common.md](ad-format-common.md#common-issues) | |

## Testing Checklist

Shared items: [ad-format-common.md](ad-format-common.md#shared-testing-checklist). Plus:

- [ ] `OnAdRewarded` fires and the reward is granted exactly once
- [ ] Watch-ad button enables when ready, disables when not
- [ ] `GetReward()` values shown in UI match the dashboard
