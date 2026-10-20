# Interstitial Ads API Reference

Interstitials are full-screen video or static ads, closable by the user, shown at natural transitions: level complete, game over, returning to menu, session breaks. Never during active gameplay or mid-action.

Shared lifecycle, constructors, `Config.Builder` (`SetBidFloor`), shared events, `LevelPlayAdInfo` / `LevelPlayAdError`, threading, retry/backoff, error 1037, placement capping and cleanup are in [ad-format-common.md](ad-format-common.md). This file covers what is specific to `LevelPlayInterstitialAd`.

## Contents
- [Reference Implementation](#reference-implementation)
- [Placements with Frequency Capping](#placements-with-frequency-capping)
- [Interstitial-Specific API](#interstitial-specific-api)
- [Loading and User Flow](#loading-and-user-flow)
- [Frequency Capping](#frequency-capping)
- [Common Issues](#common-issues)
- [Testing Checklist](#testing-checklist)

## Reference Implementation

`InterstitialAdManager` below is the manager the patterns in [best-practices.md](best-practices.md) build on (public `LoadAd()`, `ShowAd()`, `IsAdReady()`).

```csharp
using UnityEngine;
using Unity.Services.LevelPlay;

public class InterstitialAdManager : MonoBehaviour
{
    private LevelPlayInterstitialAd interstitialAd;
    private string adUnitId = "YOUR_INTERSTITIAL_AD_UNIT_ID";

    void Start()
    {
        // Create the interstitial ad object using constructor
        interstitialAd = new LevelPlayInterstitialAd(adUnitId);

        // ILRD (SDK 9.5.0+): if impression-revenue tracking is enabled, also subscribe here —
        //   interstitialAd.OnAdImpressionDataReady += OnImpressionDataReady;   // see references/ilrd-api.md

        // Register event listeners
        interstitialAd.OnAdLoaded += OnAdLoaded;
        interstitialAd.OnAdLoadFailed += OnAdLoadFailed;
        interstitialAd.OnAdDisplayed += OnAdDisplayed;
        interstitialAd.OnAdDisplayFailed += OnAdDisplayFailed;
        interstitialAd.OnAdClicked += OnAdClicked;
        interstitialAd.OnAdClosed += OnAdClosed;
        interstitialAd.OnAdInfoChanged += OnAdInfoChanged;   // Fires when the loaded ad updates after a new auction result

        // Load the ad
        LoadAd();
    }

    void OnDestroy()
    {
        // Unregister event listeners
        if (interstitialAd != null)
        {
            interstitialAd.OnAdLoaded -= OnAdLoaded;
            interstitialAd.OnAdLoadFailed -= OnAdLoadFailed;
            interstitialAd.OnAdDisplayed -= OnAdDisplayed;
            interstitialAd.OnAdDisplayFailed -= OnAdDisplayFailed;
            interstitialAd.OnAdClicked -= OnAdClicked;
            interstitialAd.OnAdClosed -= OnAdClosed;
            interstitialAd.OnAdInfoChanged -= OnAdInfoChanged;
            // ILRD (9.5.0+): interstitialAd.OnAdImpressionDataReady -= OnImpressionDataReady;

            // Free the native ad and its resources when this manager is destroyed
            interstitialAd.DestroyAd();
        }
    }

    public void LoadAd()
    {
        Debug.Log("Loading interstitial ad...");
        interstitialAd.LoadAd();
    }

    public void ShowAd()
    {
        if (interstitialAd.IsAdReady())
        {
            Debug.Log("Showing interstitial ad");
            interstitialAd.ShowAd();
        }
        else
        {
            Debug.LogWarning("Interstitial ad is not ready yet");
        }
    }

    // True once an interstitial ad has finished loading and can be shown.
    // Use this to decide whether to show opportunistically before calling ShowAd().
    public bool IsAdReady() => interstitialAd != null && interstitialAd.IsAdReady();

    // Event Callbacks
    private void OnAdLoaded(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Interstitial ad loaded successfully");
    }

    private void OnAdLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"Interstitial ad failed to load: {error.ErrorMessage}");
        // Retry loading after a delay
        Invoke(nameof(LoadAd), 30f);
    }

    private void OnAdDisplayed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Interstitial ad displayed");
    }

    private void OnAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogError($"Interstitial ad failed to display: {error.ErrorMessage}");
        // Load a new ad
        LoadAd();
    }

    private void OnAdClicked(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Interstitial ad clicked");
    }

    private void OnAdClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Interstitial ad closed");
        // Load the next ad
        LoadAd();
    }

    private void OnAdInfoChanged(LevelPlayAdInfo adInfo)
    {
        // Optional: log or update analytics with the latest revenue estimate
        Debug.Log($"Interstitial ad info changed - network: {adInfo.AdNetwork}, revenue: ${adInfo.Revenue}");
    }
}
```

## Placements with Frequency Capping

Time-based cap plus dashboard placement capping in one show path:

```csharp
private const string PlacementLevelComplete = "level_complete";
private const string PlacementGameOver = "game_over";
private float minTimeBetweenAds = 300f;
private float lastAdTime = -999f;

public void ShowOnLevelComplete() => TryShowWithPlacement(PlacementLevelComplete);
public void ShowOnGameOver() => TryShowWithPlacement(PlacementGameOver);

private void TryShowWithPlacement(string placementName)
{
    if (Time.time - lastAdTime < minTimeBetweenAds) return;
    if (LevelPlayInterstitialAd.IsPlacementCapped(placementName)) return;
    if (interstitialAd.IsAdReady())
    {
        interstitialAd.ShowAd(placementName: placementName);
    }
    else
    {
        interstitialAd.LoadAd();
    }
}

private void OnAdDisplayed(LevelPlayAdInfo adInfo)
{
    lastAdTime = Time.time;
}
```

Record the timestamp in `OnAdDisplayed`, not when `ShowAd()` is called. Only call `LoadAd()` in the else-branch if no load is in flight (error 1037, see [ad-format-common.md](ad-format-common.md#error-1037-load-during-load)).

## Interstitial-Specific API

| Member | Signature | Notes |
|---|---|---|
| Constructors | `new LevelPlayInterstitialAd(string adUnitId)`, `new LevelPlayInterstitialAd(string adUnitId, LevelPlayInterstitialAd.Config config)` | Only after `OnInitSuccess` |
| Builder | `LevelPlayInterstitialAd.Config.Builder` → `SetBidFloor(double)` → `Build()` returns `LevelPlayInterstitialAd.Config` | |
| `IsAdReady` | `bool IsAdReady()` | Always check before `ShowAd()` |
| `IsPlacementCapped` | `static bool LevelPlayInterstitialAd.IsPlacementCapped(string placementName)` | |
| `ShowAd` | `ShowAd()`, `ShowAd(placementName: string)` | |
| `DestroyAd` | `void DestroyAd()` | Call in `OnDestroy()` |
| `OnAdClosed` | `event Action<LevelPlayAdInfo>` | Resume game and load the next ad immediately |
| `OnAdInfoChanged` | `event Action<LevelPlayAdInfo>` | See [ad-format-common.md](ad-format-common.md#onadinfochanged) |

No reward event. Error 1037 is documented as interstitial-specific in the error code table ("Cannot load an interstitial while another interstitial is loading").

## Loading and User Flow

Keep one interstitial ready for opportunistic moments: create and `LoadAd()` in `OnInitSuccess`, reload in `OnAdClosed` and after `OnAdDisplayFailed`.

```csharp
void OnInitSuccess(LevelPlayConfiguration config)
{
    interstitialAd = new LevelPlayInterstitialAd(adUnitId);
    interstitialAd.OnAdLoaded += OnAdLoaded;
    interstitialAd.OnAdClosed += OnAdClosed;
    interstitialAd.LoadAd();
}

void OnAdClosed(LevelPlayAdInfo adInfo)
{
    interstitialAd.LoadAd();
}
```

Never block progress waiting for an ad: advance the game first, then try to show.

```csharp
public void OnLevelComplete()
{
    LoadNextLevel();
    TryShowInterstitial();
}
```

## Frequency Capping

Time-based (above) or count-based (every N levels, see [best-practices.md](best-practices.md)). Recommended intervals:

| Strategy | Interval |
|---|---|
| Revenue-focused | 3-5 min |
| Balanced | 5-7 min |
| UX-focused | 7-10 min or never |

## Common Issues

| Issue | Fix |
|---|---|
| Showing too often | Time or session-count cap; record last show time in `OnAdDisplayed` |
| User flow blocked by ad | Load proactively, show opportunistically, never wait |
| Error 1037 / not loading / loaded but not showing | See [ad-format-common.md](ad-format-common.md#common-issues) |

## Testing Checklist

Shared items: [ad-format-common.md](ad-format-common.md#shared-testing-checklist). Plus:

- [ ] Shows only at transition points, never during active gameplay
- [ ] Frequency capping works as expected
- [ ] User can progress regardless of ad status
