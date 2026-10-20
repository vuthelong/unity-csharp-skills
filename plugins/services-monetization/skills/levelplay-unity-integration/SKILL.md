---
name: levelplay-unity-integration
description: Integrates LevelPlay ad mediation into Unity through the Ads Mediation package (com.unity.services.levelplay, SDK 9.x) - install/upgrade, Android Gradle and iOS CocoaPods resolution, init, rewarded/interstitial/banner ad units, GDPR/CCPA/COPPA and ATT, impression-level revenue, Test Suite. Use when adding or debugging ads in a Unity game, fixing CS0246 on Unity.Services.LevelPlay, or migrating from IronSource.Agent or Unity Ads (Advertisement Legacy). Not for in-app purchases.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: services-monetization
  sources: "Unity-Technologies/skills/skills/levelplay-unity-integration"
  unity: "6000.0+"
---

# LevelPlay Unity Integration

LevelPlay is Unity's ad mediation platform: one SDK, a unified auction across many networks and bidders. This file is the workflow and the decision points; API facts, full code and edge cases live in `references/`. Recommend freely from experience, but take API names, signatures and version cutoffs from the references and confirm them against the installed package rather than memory. Stay on the LevelPlay path; if asked about other mediation SDKs, point to their own docs without characterizing them. Generated C# is MonoBehaviour code for the user's project.

## 0. Detect Project State First

Read files instead of asking:

- `Assets/` and `ProjectSettings/` exist, otherwise you are not at a Unity project root.
- `Packages/manifest.json` → `dependencies["com.unity.services.levelplay"]` = requested version. `Packages/packages-lock.json` → same id with a concrete version = what actually resolved. The lock file is the truth.
- Existing ad code: grep for `IronSource.Agent`, `IronSourceEvents`, `UnityEngine.Advertisements` (legacy, migrate), and `LevelPlayRewardedAd|LevelPlayInterstitialAd|LevelPlayBannerAd|LevelPlay.Init` (current).
- Dependency manager: `Assets/MobileDependencyResolver`, `Assets/ExternalDependencyManager`, or `com.google.external-dependency-manager` in the manifest. Active build target and `Assets/Plugins/Android/AndroidManifest.xml`.

Then pick the track: **new integration** (steps 1-10), **migration** (section M), or **troubleshooting / adding a feature** (jump to the matching step or the issues table).

## Version Gates

Version-dependent APIs; branch on the resolved version from `packages-lock.json` (Network Manager shows it too):

| Feature | < 9.4.0 | 9.4.x | 9.5.0+ |
|---|---|---|---|
| GDPR | `LevelPlay.SetConsent(bool)` (`[Obsolete]`) | `LevelPlayPrivacySettings.SetGDPRConsents(Dictionary<string,bool>)` (current on 9.4.x, not legacy) | `LevelPlayPrivacySettings.SetGDPRConsent(bool)`; dictionary API becomes `[Obsolete]` |
| CCPA / COPPA | upgrade | `LevelPlayPrivacySettings.SetCCPA(bool)` / `SetCOPPA(bool)` | same |
| ILRD | global `LevelPlay.OnImpressionDataReady` | global event, subscribed **before** `LevelPlay.Init()` | per-ad `OnAdImpressionDataReady`; global event deprecated (compiler warning) |

If an API doesn't compile, recommend upgrading via **Ads Mediation > Network Manager** before working around it.

## 1-3. Install or Update

- Package Manager → Unity Registry/Services → search **Ads Mediation** (id `com.unity.services.levelplay`). Not **Ads IAP Mediation Adaptor** (separate IAP package) and not **Advertisement Legacy** (deprecated, incompatible). Import **Mobile Dependency Resolver** if prompted. Headless: `unity-package-management` skill.
- **Hard gate:** write no LevelPlay code until the id is in `packages-lock.json`. Missing from manifest = never installed; in manifest but not lock = Unity hasn't resolved yet (still importing or failed). Say which, and stop. Otherwise every symbol fails with `CS0246`.
- If you added the id to `manifest.json` yourself, the lock file won't show it until the Editor runs. Never hand-edit `packages-lock.json`; ask the user to open the Editor and re-read. Report the resolved version, never "installed" from memory or the user's recollection.
- Adapters and SDK updates: **Ads Mediation > Network Manager**.
- Ask the optimization goal (revenue / UX / balanced) only if format choice depends on it; default to balanced.

## 4. Native Dependencies and Platform

- Target platform(s) decide dependency steps, ATT, and testing. Switch the active build target to Android/iOS (**File > Build Profiles**, "Build Settings" before Unity 6). LevelPlay does nothing on Standalone, including Editor mock ads.
- Resolve with MDR / UEDM / EDM4U for every target platform. Without it the code compiles in the Editor but Gradle/CocoaPods builds fail. Procedure, verification and the older Custom Main Gradle Template: [dependency-resolution.md](references/dependency-resolution.md).
- Android API 33+: `<uses-permission android:name="com.google.android.gms.permission.AD_ID"/>` in AndroidManifest.xml, else advertising ID access fails on Android 13+.
- iOS: SKAdNetwork IDs in Info.plist, privacy manifest: [ios-setup.md](references/ios-setup.md).

## 5-6. Credentials and AdMob

- Dashboard https://platform.ironsrc.com/ (new: [add app](https://docs.unity.com/en-us/grow/levelplay/platform/get-started/add-app), [create ad units](https://docs.unity.com/en-us/grow/levelplay/platform/get-started/ad-units)). App Key: **Apps** → the string under the app title. Ad unit IDs: **Ad units** → select app → per format. App Key is needed for init; ad unit IDs only for step 9.
- AdMob as a network: **Ads Mediation > Developer Settings > LevelPlay Mediation Settings** → Android and iOS AdMob app keys. Missing menu = package not installed or Editor needs a restart.

## 6.5. Privacy and ATT (before Init)

Technical guidance, not legal advice; which laws apply is the user's call ([Regulation Advanced Settings](https://docs.unity.com/en-us/grow/levelplay/sdk/unity/regulation-advanced-settings)). Every privacy call goes **before** `LevelPlay.Init()`; pick APIs from the version gates. Consent UI, combined regulations, network key list for `SetGDPRConsents`, legacy `SetConsent`: [privacy-settings.md](references/privacy-settings.md).

iOS, regardless of regulations: request ATT before `LevelPlay.Init()` (iOS 14.5+ requirement before tracking or reading IDFA; also affects personalized ads and fill). Code, including the `IEnumerator Start()` coroutine initializer: [ios-setup.md](references/ios-setup.md).

## 7. Initialize

- Re-check `packages-lock.json` before writing init code, even if checked earlier.
- Init early, once, on a persistent GameObject (`DontDestroyOnLoad`). Create ad objects only after `OnInitSuccess`. Success log: `LevelPlay SDK initialized successfully`.
- Placement: new `LevelPlayInitializer.cs`, the user's existing bootstrap, or a LevelPlay script it references. Recommend based on what the project already has. With ATT, use the iOS coroutine variant. Templates for each: [initialization-api.md](references/initialization-api.md).
- ILRD, if they use analytics/attribution (Firebase, AppsFlyer, Adjust, Singular, custom): 9.5.0+ add nothing to init (wire per ad in step 9); 9.4.x and earlier subscribe `LevelPlay.OnImpressionDataReady` before `Init` and unsubscribe in `OnDestroy()` (in the iOS coroutine, inside `InitializeLevelPlay()` right before `LevelPlay.Init(appKey)`). Wiring: [initialization-api.md](references/initialization-api.md); forwarding to platforms: [ilrd-api.md](references/ilrd-api.md). ILRD never fires with mock ads.
- User ID, segments, advanced config, error codes: [initialization-api.md](references/initialization-api.md).

## 8. Format Strategy

| Goal | Formats, in priority order |
|---|---|
| Revenue | Rewarded (several high-value moments) → Interstitial at transitions, cap 3-5 min → persistent Banner |
| Balanced (default) | Rewarded (2-3 placements) → Interstitial at breakpoints, cap 5-7 min → Banner in menus |
| UX | Rewarded only, user-initiated, no forced ads; interstitials sparingly at session boundaries; banners avoided or menu-only |

Benchmarks, placement moments, anti-patterns: [best-practices.md](references/best-practices.md).

## 9. Ad Units

- Confirm init works first. Implement only the formats the user wants. If ad code already exists, read it before generating anything.
- Shared lifecycle, events, data types, retry/backoff, error 1037, cleanup: [ad-format-common.md](references/ad-format-common.md). Per format: [rewarded-api.md](references/rewarded-api.md), [interstitial-api.md](references/interstitial-api.md), [banner-api.md](references/banner-api.md). Code-gen rules (separate managers vs one `AdManager` vs snippets, review of existing code, adding formats later): [best-practices.md](references/best-practices.md#code-generation-guidelines-step-9).
- Gotchas:
  - `LoadAd()` is always explicit; no auto-load as in legacy rewarded video.
  - Full-screen: `IsAdReady()` before `ShowAd()`; with placements also `IsPlacementCapped(name)` (else 524/526). Never `LoadAd()` in `Update()` (1037).
  - Grant rewards in `OnAdRewarded`, never `OnAdClosed`. It may fire after `OnAdClosed`.
  - Banners: no `IsAdReady()`, one `LoadAd()` per object, `HideAd()` to toggle, `DestroyAd()` to tear down; `LevelPlayAdSize` constants are UPPERCASE.
  - `DestroyAd()` and full unsubscription in `OnDestroy()`; callbacks run on the main thread.
  - 9.5.0+ ILRD: subscribe `OnAdImpressionDataReady` right after creating each ad.
- Bid floors are optional (`Config.Builder().SetBidFloor(usd)`); most teams add them later with dashboard data.

## 10. Testing

- **Editor mock ads**: Play with an Android/iOS build target; any App Key works (prefer the real one). Loaded/displayed/rewarded/closed fire; failure, click and impression/ILRD do not, so test those on device.
- **Test Suite** (device, Development Build, production App Key): `LevelPlay.SetMetaData("is_test_suite", "enable");` before `LevelPlay.Init()`, `LevelPlay.LaunchTestSuite();` in `OnInitSuccess`. In the iOS coroutine, `SetMetaData` goes first inside `InitializeLevelPlay()`. Add to the existing initializer; remove both lines before release.
- Callback tables, templates, release checklist: [testing-and-validation.md](references/testing-and-validation.md).

## M. Migration

[migration-sdk-9.md](references/migration-sdk-9.md), scenarios: **A** SDK upgrade, **B** `IronSource.Agent` init → `LevelPlay.Init`, **C** ad-unit APIs per format (e.g. `destroyBanner()` maps to `HideAd()` for a toggle, `DestroyAd()` for teardown), **D** Maven Central build failure, **E** Unity Ads (Advertisement Legacy) → LevelPlay. Always finish with the **C5 completeness checklist**: it catches requirements that line-by-line translation misses. Then have the user check the console for compile errors; if you can't see it, list changed files and what to look for.

## Common Issues

| Symptom | Cause | Action |
|---|---|---|
| `CS0246` on `Unity.Services.LevelPlay` | Package not installed/resolved | Stop writing code; check the lock file; install (1-3); restart Editor |
| Compiles in Editor, Gradle/CocoaPods build fails | Native deps unresolved | Step 4; verify `Assets/Plugins/Android/`; rebuild |
| Android fails resolving `com.ironsource.sdk` from `android-sdk.is.com` | Repo shut down, moved to Maven Central | Scenario D: delete stale dependency XMLs, reinstall via Network Manager, verify no is.com refs |
| No ads in Editor | Build target is Standalone | Switch to Android/iOS |
| Ads not loading | Created before `OnInitSuccess`, wrong App Key/ad unit ID, offline | Fix order, verify IDs, test on device |
| Callbacks not firing | Late/missing subscriptions, destroyed object | Subscribe at creation, persistent GameObject |
| iOS/Android platform build errors | SKAdNetwork/ATT/frameworks, Play Services/manifest | [ios-setup.md](references/ios-setup.md), [troubleshooting.md](references/troubleshooting.md) |

Details: [troubleshooting.md](references/troubleshooting.md).

## Reference Map

| Need | File |
|---|---|
| Dependency managers, Gradle/CocoaPods, AD_ID | [dependency-resolution.md](references/dependency-resolution.md) |
| Init templates, ILRD init wiring, user ID, segments, error codes | [initialization-api.md](references/initialization-api.md) |
| GDPR/CCPA/COPPA, network keys, legacy consent | [privacy-settings.md](references/privacy-settings.md) |
| ATT, SKAdNetwork, privacy manifest, iOS initializer | [ios-setup.md](references/ios-setup.md) |
| Shared ad unit API and lifecycle | [ad-format-common.md](references/ad-format-common.md) |
| Rewarded / Interstitial / Banner specifics | [rewarded-api.md](references/rewarded-api.md), [interstitial-api.md](references/interstitial-api.md), [banner-api.md](references/banner-api.md) |
| Strategy, placement, frequency, code-gen rules, bid floors | [best-practices.md](references/best-practices.md) |
| ILRD to analytics platforms | [ilrd-api.md](references/ilrd-api.md) |
| Mock ads, Test Suite, release checklist | [testing-and-validation.md](references/testing-and-validation.md) |
| Errors and fixes | [troubleshooting.md](references/troubleshooting.md) |
| SDK upgrade, IronSource.Agent / Unity Ads migration, Maven Central | [migration-sdk-9.md](references/migration-sdk-9.md) |

## Related Skills

- `implement-in-app-purchases`: real-money purchases (the **Ads IAP Mediation Adaptor** package is not Unity IAP).
- `build-live-game`: Remote Config for ad caps and A/B tests, Cloud Code for server-verified rewards.
- `unity-package-management`: installing `com.unity.services.levelplay` headlessly.
