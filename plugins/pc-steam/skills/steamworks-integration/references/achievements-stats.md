# Achievements and stats

Read when defining achievements and stats, wiring them to gameplay, or testing them. API: `Steamworks.SteamUserStats` (`ISteamUserStats`). Script: `scripts/SteamAchievements.cs`.

## Partner site setup

1. Steamworks partner site > your app > Edit Steamworks Settings > Stats & Achievements.
2. Stats first. Each has an API name, a type and a "Set By" value:
   - `INT`: 32-bit signed integer. Optional min, max, max change, increment only, default.
   - `FLOAT`: 32-bit float.
   - `AVGRATE`: moving average computed by Steam (written with `UpdateAvgRateStat`).
   - Set By `Client` (default) lets the game client write it. `GS` / `Official GS` restrict writes to game servers, and the client can no longer set it. A listen-server co-op game has no game server, so keep `Client`.
3. Achievements: API name, localized display name and description, hidden flag, achieved and unachieved icons. An optional progress stat maps a stat range to the 0-100% progress bar shown by Steam.
4. Publish. Edits are drafts until published from the Publish tab. After publishing, restart the Steam client (or wait) before testing; if `GetAchievement` keeps returning false for a name you just added, the client has not picked up the new schema yet.
5. Before release, unlocked achievements do not show on the Steam Community or library pages. Show them in-game during testing if you need to see them.

The docs do not state whether Steam unlocks an achievement automatically when its progress stat reaches the maximum. Do not rely on it: call `SetAchievement` yourself.

## API

| Call | Notes |
|---|---|
| `GetStat(name, out int)` / `GetStat(name, out float)` | Returns false for an unknown name or wrong type. |
| `SetStat(name, int)` / `SetStat(name, float)` | Changes the in-memory value only. Cheap; call as often as needed. |
| `UpdateAvgRateStat(name, float countThisSession, double sessionLength)` | For `AVGRATE` stats. |
| `GetAchievement(name, out bool achieved)` | Returns false for an unknown API name. |
| `SetAchievement(name)` | In memory only until `StoreStats`. The unlock popup appears after the store. |
| `ClearAchievement(name)` | Relocks; for testing. |
| `IndicateAchievementProgress(name, cur, max)` | Shows a progress popup only. It does not store progress or unlock; `cur == max` does nothing useful. |
| `StoreStats()` | Uploads stats and achievements. Rate limited. Results arrive as `UserStatsStored_t`, plus one `UserAchievementStored_t` per newly unlocked achievement or progress popup. |
| `ResetAllStats(bool achievementsToo)` | Resets the current account, calls `StoreStats` itself. Development only. |
| `GetAchievementDisplayAttribute(name, "name" / "desc" / "hidden")` | Localized display strings for an in-game list. |
| `GetNumAchievements()`, `GetAchievementName(i)` | Enumerate definitions. |
| `RequestUserStats(steamId)` + `CallResult<UserStatsReceived_t>` | Another user's stats; not needed for the local user. |

`RequestCurrentStats()` is deprecated by Valve and absent from the Steamworks.NET wrapper (it is commented out in `isteamuserstats.cs` for SDK 1.64 and 1.65). The client loads the local user's stats before launch.

## When to call StoreStats

Valve's guidance: the call can be rate limited, space calls on the order of minutes, and store at major state changes such as end of a round or a map change.

- Achievement unlocks: store right away so the popup is immediate. Unlocks are rare, so this stays well under any limit. `SteamAchievements.Unlock` does this and skips achievements that are already unlocked.
- Stats: mark dirty and flush at checkpoints (level end, save, return to menu) and on quit. `SteamAchievements` flushes at most every `minStoreInterval` seconds (default 60) and in `OnApplicationQuit`. Call `StoreNow()` at checkpoints.
- `UserStatsStored_t.m_eResult == k_EResultInvalidParam`: Steam rejected one or more values (min/max/max change/increment only) and the server values replaced the local ones. Re-read stats before writing again.
- Filter callbacks by game: compare `m_nGameID` with `new CGameID(SteamUtils.GetAppID()).m_GameID`, as Valve's sample does.

## Gameplay wiring

- Keep achievement and stat API names in one static class of `const string`s, matching the partner site exactly (case-sensitive).
- Route unlocks through one service (`SteamAchievements`) so a non-Steam build can swap it for a no-op or another store's implementation.
- Co-op: every player unlocks on their own client for their own account. The host must tell clients about shared events (boss killed) through netcode, and each client calls `Unlock` locally. See `netcode-for-gameobjects`.
- Progress popups: call `ShowProgress` at meaningful steps (every 10 of 100), not on every increment.

## Testing and resetting

- Use your real AppID with the definitions published. AppID 480 has Spacewar's achievements, not yours.
- From code in a development build: `SteamUserStats.ResetAllStats(true)` (the context menu item on `SteamAchievements`).
- From the Steam console (`steam.exe -console`, Console tab):
  - `achievement_clear <appid> <achievement API name>`
  - `reset_all_stats <appid>`
- There is no global reset; each command affects only the logged-in account.
