# Banner Ads API Reference

Banners are rectangular ads that stay on screen (top, bottom, or custom position) until hidden or destroyed. They can distract during gameplay; best in menus, waiting screens and low-attention moments.

Shared lifecycle, constructors, `SetBidFloor`, shared events, `LevelPlayAdInfo` / `LevelPlayAdError`, threading, retry/backoff and cleanup are in [ad-format-common.md](ad-format-common.md). This file covers what is specific to `LevelPlayBannerAd`.

## Contents
- [How Banners Differ](#how-banners-differ)
- [Reference Implementation](#reference-implementation)
- [Banner-Specific API](#banner-specific-api)
- [Sizes](#sizes)
- [Positions](#positions)
- [Visibility, Refresh and Scenes](#visibility-refresh-and-scenes)
- [Common Issues](#common-issues)
- [Testing Checklist](#testing-checklist)

## How Banners Differ

- **No `IsAdReady()`**. A banner is ready once `OnAdLoaded` fires; call `ShowAd()` there or later.
- **One `LoadAd()` per object lifetime.** New creatives come from auto-refresh (dashboard settings); visibility is `ShowAd()`/`HideAd()`, never reloading. The only valid second `LoadAd()` on the same object is a retry after `OnAdLoadFailed`. After `DestroyAd()`, create a new object with its own single `LoadAd()`.
- Repeated loads don't raise error 1037 like full-screen formats, but waste resources and cause unexpected behavior.
- **No `OnAdClosed` / `OnAdInfoChanged`**; instead `OnAdExpanded`, `OnAdCollapsed`, `OnAdLeftApplication`.
- **Placement** is set in the config (`SetPlacementName`), not passed to `ShowAd()`.
- Default: `BANNER` (320x50), `BottomCenter`, `DisplayOnLoad = true` (shows automatically when loaded).
- Only one banner per ad unit active at a time. To change size or position, destroy and recreate.

## Reference Implementation

`BannerAdManager` below is the manager the patterns in [best-practices.md](best-practices.md) build on (public `ShowBanner()`, `HideBanner()`).

```csharp
using UnityEngine;
using Unity.Services.LevelPlay;

public class BannerAdManager : MonoBehaviour
{
    private LevelPlayBannerAd bannerAd;
    private string adUnitId = "YOUR_BANNER_AD_UNIT_ID";

    void Start()
    {
        // Create the banner ad object using constructor
        bannerAd = new LevelPlayBannerAd(adUnitId);

        // ILRD (SDK 9.5.0+): if impression-revenue tracking is enabled, also subscribe here —
        //   bannerAd.OnAdImpressionDataReady += OnImpressionDataReady;   // see references/ilrd-api.md

        // Register event listeners
        bannerAd.OnAdLoaded += OnAdLoaded;
        bannerAd.OnAdLoadFailed += OnAdLoadFailed;
        bannerAd.OnAdDisplayed += OnAdDisplayed;
        bannerAd.OnAdDisplayFailed += OnAdDisplayFailed;
        bannerAd.OnAdClicked += OnAdClicked;
        bannerAd.OnAdExpanded += OnAdExpanded;
        bannerAd.OnAdCollapsed += OnAdCollapsed;
        bannerAd.OnAdLeftApplication += OnAdLeftApplication;

        // Load and show banner
        LoadBanner();
    }

    void OnDestroy()
    {
        // Unregister event listeners
        if (bannerAd != null)
        {
            bannerAd.OnAdLoaded -= OnAdLoaded;
            bannerAd.OnAdLoadFailed -= OnAdLoadFailed;
            bannerAd.OnAdDisplayed -= OnAdDisplayed;
            bannerAd.OnAdDisplayFailed -= OnAdDisplayFailed;
            bannerAd.OnAdClicked -= OnAdClicked;
            bannerAd.OnAdExpanded -= OnAdExpanded;
            bannerAd.OnAdCollapsed -= OnAdCollapsed;
            bannerAd.OnAdLeftApplication -= OnAdLeftApplication;
            // ILRD (9.5.0+): bannerAd.OnAdImpressionDataReady -= OnImpressionDataReady;
        }

        // Destroy banner
        DestroyBanner();
    }

    public void LoadBanner()
    {
        Debug.Log("Loading banner ad...");
        bannerAd.LoadAd();
    }

    public void ShowBanner()
    {
        Debug.Log("Showing banner ad");
        bannerAd.ShowAd();
    }

    public void HideBanner()
    {
        Debug.Log("Hiding banner ad");
        bannerAd.HideAd();
    }

    public void DestroyBanner()
    {
        if (bannerAd != null)
        {
            Debug.Log("Destroying banner ad");
            bannerAd.DestroyAd();
            bannerAd = null;
        }
    }

    // Event Callbacks
    private void OnAdLoaded(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"Banner ad loaded - network: {adInfo.AdNetwork}, revenue: ${adInfo.Revenue}");
        // Banner is ready, can call ShowAd() if needed
    }

    private void OnAdLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"Banner ad load failed: {error.ErrorMessage}");
        // Retry loading after delay
        Invoke(nameof(LoadBanner), 60f);
    }

    private void OnAdDisplayed(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"Banner ad displayed - network: {adInfo.AdNetwork}, placement: {adInfo.PlacementName}");
    }

    private void OnAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogError($"Banner ad display failed: {error.ErrorMessage}");
        // Retry loading
        Invoke(nameof(LoadBanner), 60f);
    }

    private void OnAdClicked(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"Banner ad clicked - network: {adInfo.AdNetwork}");
    }

    private void OnAdExpanded(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"Banner ad expanded - network: {adInfo.AdNetwork}");
        // Optionally pause game if banner expands to full screen
    }

    private void OnAdCollapsed(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"Banner ad collapsed - network: {adInfo.AdNetwork}");
        // Resume game if paused
    }

    private void OnAdLeftApplication(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"Banner ad left application - network: {adInfo.AdNetwork}");
    }
}
```

## Banner-Specific API

### Constructors

| Constructor | Notes |
|---|---|
| `new LevelPlayBannerAd(string adUnitId)` | Default config (320x50, BottomCenter, display on load). Only after `LevelPlay.Init()` succeeds. |
| `new LevelPlayBannerAd(string adUnitId, LevelPlayBannerAd.Config config)` | Custom config |

### Config Builder (`LevelPlayBannerAd.Config.Builder`)

| Method | Notes |
|---|---|
| `SetSize(LevelPlayAdSize size)` | See [Sizes](#sizes) |
| `SetPosition(LevelPlayBannerPosition position)` | See [Positions](#positions) |
| `SetDisplayOnLoad(bool displayOnLoad)` | `true` auto-shows (default); `false` requires manual `ShowAd()` |
| `SetRespectSafeArea(bool respectSafeArea)` | Android only; avoids notches/cutouts |
| `SetPlacementName(string placementName)` | Reporting per banner placement, e.g. `"main_menu_banner"` |
| `SetBidFloor(double)` | Minimum bid in USD |
| `Build()` | Returns `LevelPlayBannerAd.Config` |

```csharp
var config = new LevelPlayBannerAd.Config.Builder()
    .SetSize(LevelPlayAdSize.LARGE)
    .SetPosition(LevelPlayBannerPosition.BottomCenter)
    .SetDisplayOnLoad(true)
    .SetRespectSafeArea(true)
    .Build();
bannerAd = new LevelPlayBannerAd(adUnitId, config);
```

### Methods

| Method | Notes |
|---|---|
| `LoadAd()` | Once per object; then `ShowAd()` unless `SetDisplayOnLoad(true)` |
| `ShowAd()` | Banner must be loaded |
| `HideAd()` | Hide without destroying; keeps it loaded for quick re-display |
| `DestroyAd()` | Free resources; when leaving the scene for good or switching size |
| `PauseAutoRefresh()` | While hidden but still loaded (e.g. gameplay) |
| `ResumeAutoRefresh()` | When visible again |

### Events

Shared events plus, all `event Action<LevelPlayAdInfo>`:

| Event | Fires when | Typical use |
|---|---|---|
| `OnAdExpanded` | Banner expands to full screen (e.g. after click) | Pause game |
| `OnAdCollapsed` | Expanded banner collapses back | Resume game |
| `OnAdLeftApplication` | Click takes the user out of the app | Analytics |

## Sizes

`LevelPlayAdSize` constants are **UPPERCASE** (`LevelPlayAdSize.BANNER`, not `.Banner`; lowercase is a common cause of "wrong size"/compile errors).

| Size | Dimensions |
|---|---|
| `LevelPlayAdSize.BANNER` | 320x50 (recommended default) |
| `LevelPlayAdSize.LARGE` | 320x90 |
| `LevelPlayAdSize.MEDIUM_RECTANGLE` | 300x250 |
| `LevelPlayAdSize.LEADERBOARD` | 728x90 |
| `LevelPlayAdSize.CreateAdaptiveAdSize()` | Adapts to screen width (recommended for cross-device) |
| `LevelPlayAdSize.CreateCustomBannerSize(int width, int height)` | Custom, e.g. `(300, 150)` |

`LevelPlayAdSize.Width` / `Height` (int, pixels; SDK 8.8.0+) give the actual dimensions, useful for offsetting UI around the banner.

## Positions

`LevelPlayBannerPosition`: `TopLeft`, `TopCenter`, `TopRight`, `CenterLeft`, `Center`, `CenterRight`, `BottomLeft`, `BottomCenter` (most common, least intrusive), `BottomRight`. Custom positioning is possible via a `Vector2` constructor.

## Visibility, Refresh and Scenes

- Temporary: `HideAd()` + `PauseAutoRefresh()`; back: `ShowAd()` + `ResumeAutoRefresh()`.
- Permanent: `DestroyAd()` (frees memory); don't keep banners loaded in scenes that never show them.
- UX-focused: hide during gameplay, show in menus. Revenue-focused: persistent, positioned carefully.
- Scene-based: persistent manager (`DontDestroyOnLoad`) subscribes to `SceneManager.sceneLoaded`, shows in an allow-list of scenes (e.g. `MainMenu`, `LevelSelect`, `Shop`), hides elsewhere; track a loaded flag set in `OnAdLoaded` and only `ShowAd()` when loaded. Unsubscribe `sceneLoaded` in `OnDestroy()`.

```csharp
void OnGameplayStart()
{
    bannerAd.HideAd();
    bannerAd.PauseAutoRefresh();
}

void OnGameplayEnd()
{
    bannerAd.ShowAd();
    bannerAd.ResumeAutoRefresh();
}
```

## Common Issues

| Issue | Causes | Fix |
|---|---|---|
| Repeated `LoadAd()` | Called in `Update()`, on scene re-entry, or to "refresh" | Load once after creation; retry only on failure |
| Overlaps UI | Banner height not accounted for | Offset UI using `LevelPlayAdSize.Height` (`RectTransform.anchoredPosition`, `offsetMin`); `SetRespectSafeArea(true)`; test multiple screen sizes |
| Not showing | Created before `OnInitSuccess`; `ShowAd()` before `OnAdLoaded`; hidden via `HideAd()`; `SetDisplayOnLoad(false)` without `ShowAd()` | Create after init, show after load, check visibility state |
| Shows in wrong scenes | No scene-based visibility | Scene allow-list, hide/destroy when leaving |
| Wrong size | Lowercase enum (`.Banner`) or size never set | Use UPPERCASE and `Config.Builder().SetSize(...)` |

## Testing Checklist

Shared items: [ad-format-common.md](ad-format-common.md#shared-testing-checklist). Plus:

- [ ] Correct position and size on different screen sizes/orientations
- [ ] Doesn't overlap important UI
- [ ] Hide/show works; auto-refresh pauses/resumes
- [ ] Destroyed when leaving its scenes
- [ ] Safe area respected on notched Android devices
