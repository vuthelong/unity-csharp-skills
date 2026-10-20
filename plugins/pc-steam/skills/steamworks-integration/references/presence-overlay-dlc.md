# Rich presence, overlay, DLC and language

Read when showing "what the player is doing" in the friends list, reacting to the Steam overlay, opening overlay pages, checking DLC, or picking the game language. Gate every call on `SteamBootstrap.IsInitialized`.

## Rich presence

`SteamFriends.SetRichPresence(string key, string value)` returns false on failure. An empty or null value deletes a key; `SteamFriends.ClearRichPresence()` deletes all.

| Key | Meaning |
|---|---|
| `steam_display` | Localization token (`#Status_InMenu`) shown in the Steam client. If it is not a valid token, nothing is displayed. |
| `status` | Free text shown in the "view game info" dialog. Not localized. |
| `connect` | Command line a friend uses to join. Enables the Join button. |
| `steam_player_group` | Group id shared by players in the same party; the client groups them. |
| `steam_player_group_size` | Total players in that group. |
| any other key | Substitution variable for tokens (`%level%`). |

Limits from the docs: up to 20 keys per user, key length 64, value length 256. The SDK constant `k_cchMaxRichPresenceKeys` is 30 in SDK 1.64, which disagrees with the docs; stay within 20.

### Localization file

Upload under Edit Steamworks Settings > Community > Rich Presence. One file per language or several languages in one file; only languages present in an upload are overwritten.

```
"lang"
{
    "english"
    {
        "tokens"
        {
            "#Status_InMenu"     "In the main menu"
            "#Status_Exploring"  "Exploring {#Zone_%zone%}"
            "#Status_CoOp"       "Exploring {#Zone_%zone%} (%party% players)"
            "#Zone_Forest"       "the Old Forest"
            "#Zone_Caves"        "the Caves"
        }
    }
}
```

```csharp
SteamFriends.SetRichPresence("zone", "Forest");
SteamFriends.SetRichPresence("party", partySize.ToString());
SteamFriends.SetRichPresence("steam_display", partySize > 1 ? "#Status_CoOp" : "#Status_Exploring");
```

- Set substitution keys before `steam_display`. A token that references an unset key, or a token missing in both the user's language and English, displays nothing.
- Update on state changes (zone change, party change), not every frame.
- Test with the game running at `https://steamcommunity.com/dev/testrichpresence` while logged in on the web.

### Joining through `connect`

- If the game is running, a Join click or an accepted `InviteUserToGame` invite raises `GameRichPresenceJoinRequested_t` with `m_rgchConnect`.
- If it is not running, Steam launches it with the connect string on the command line. Read it with `SteamApps.GetLaunchCommandLine(out string cmd, 1024)`; for that, enable "Use launch command line" on the app's Installation > General page. `NewUrlLaunchParameters_t` fires when new launch parameters arrive while running.
- Lobby-based co-op uses `GameLobbyJoinRequested_t` and `+connect_lobby <id>` instead. That flow lives in `steam-lobbies-p2p`.

## Overlay

- `GameOverlayActivated_t` fires when the overlay opens or closes; `m_bActive` is a byte (1 opened, 0 closed). Valve suggests using it to pause single-player games. `SteamBootstrap.OverlayToggled` forwards it.
- In co-op, never pause the simulation. Show a pause menu locally, stop reading gameplay input, keep the network running.
- `SteamUtils.IsOverlayEnabled()` returns false for a few seconds after launch while the overlay hooks in. Check it just before relying on the overlay, not once at boot.
- The overlay needs the game to be launched from Steam (or with `steam_appid.txt` and Steam running) and usually does not appear inside the Unity Editor. Test in a build.
- Open pages:
  - `SteamFriends.ActivateGameOverlay("friends" | "community" | "players" | "settings" | "officialgamegroup" | "stats" | "achievements")`
  - `SteamFriends.ActivateGameOverlayToUser("steamid", steamId)` (other dialogs in the docs)
  - `SteamFriends.ActivateGameOverlayToWebPage(url)`
  - `SteamFriends.ActivateGameOverlayToStore(new AppId_t(dlcAppId), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None)` (`_AddToCart`, `_AddToCartAndShow` also exist)
- `SteamUtils.SetOverlayNotificationPosition(ENotificationPosition.k_EPositionBottomRight)` moves achievement and invite popups away from HUD elements; it resets each launch.

## DLC

| Call | Notes |
|---|---|
| `SteamApps.BIsDlcInstalled(new AppId_t(id))` | True when the user owns the DLC and it is installed. Valve: for simple client-side checks only, not for granting items. |
| `SteamApps.BIsSubscribedApp(new AppId_t(id))` | Ownership only. |
| `SteamApps.GetDLCCount()` / `BGetDLCDataByIndex(i, out AppId_t, out bool available, out string name, 128)` | Enumerate DLC. |
| `SteamApps.InstallDLC(new AppId_t(id))` | Ask Steam to install owned DLC. |
| `DlcInstalled_t` (`m_nAppID`) | Fires after the user gains ownership and the DLC finishes installing during a session. Re-check and unlock content without a restart. |

In co-op, decide per feature whether the host's DLC is enough (shared content) or each client must own it, and validate on the host.

## Game language

- `SteamApps.GetCurrentGameLanguage()` returns the API language code the user picked for this game, falling back to the Steam UI language, a related regional variant, English, then the first supported language. The value is always one of `GetAvailableGameLanguages()` (comma separated).
- `SteamUtils.GetSteamUILanguage()` is the client's UI language, not the game's.
- API codes are Steam's own names, not ISO codes: `english`, `schinese`, `tchinese`, `japanese`, `koreana`, `brazilian`, `spanish`, `latam`, `german`, `french`, `russian`, `vietnamese`. Full table: https://partner.steamgames.com/doc/store/localization/languages
- Map the code to a Unity Localization `Locale` once at startup (`localization` skill) and let an in-game language option override it.
