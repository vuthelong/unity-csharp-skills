# Steam lobby API (Steamworks.NET)

Read when choosing a lobby type, reading or writing lobby or member data, sending lobby chat, listing lobbies, or mapping a callback to its struct fields.

Signatures below match Steamworks.NET `2025.164.1` (`com.rlabrecque.steamworks.net`, generated from the Steamworks SDK). Everything lives in the `Steamworks` namespace as static methods on `SteamMatchmaking`, `SteamFriends`, `SteamApps` and `SteamUser`.

## Lobby types (`ELobbyType`)

| Value | Who can find or join | Use for |
|---|---|---|
| `k_ELobbyTypePrivate` | Only invited users; not returned by `RequestLobbyList` | Invite-only co-op |
| `k_ELobbyTypeFriendsOnly` | Friends and invitees; not in the public list; joiners can invite their own friends | Default for friends co-op |
| `k_ELobbyTypePublic` | Friends and `RequestLobbyList` results | Server-browser style matchmaking |
| `k_ELobbyTypeInvisible` | Returned by search, not shown to friends | Grouping a party while it searches; a user can be in one regular lobby plus up to two invisible ones |

Change the type later with `SetLobbyType(lobby, type)`. `SetLobbyJoinable(lobby, false)` blocks all joins, including friends and invitees, until set back to true (defaults to joinable). Use it when the session starts if late join is not supported.

## Core calls

| Call | Returns | Notes |
|---|---|---|
| `CreateLobby(ELobbyType type, int cMaxMembers)` | `SteamAPICall_t` → `LobbyCreated_t` | `cMaxMembers` max 250. The creator is joined on success and also gets `LobbyEnter_t` and `LobbyDataUpdate_t` callbacks |
| `JoinLobby(CSteamID lobby)` | `SteamAPICall_t` → `LobbyEnter_t` | Lobby data is available as soon as it completes |
| `LeaveLobby(CSteamID lobby)` | `void` | Immediate locally; other members get `LobbyChatUpdate_t` |
| `GetLobbyOwner(CSteamID lobby)` | `CSteamID` | Only valid while you are a member; `Nil` otherwise. Ownership moves to another member automatically when the owner leaves |
| `SetLobbyOwner(lobby, newOwner)` | `bool` | Owner only |
| `GetNumLobbyMembers(lobby)` / `GetLobbyMemberByIndex(lobby, i)` | `int` / `CSteamID` | Members only |
| `GetLobbyMemberLimit(lobby)` / `SetLobbyMemberLimit(lobby, max)` | `int` / `bool` | |
| `InviteUserToLobby(lobby, friend)` | `bool` | Sends an invite without opening the overlay |
| `SteamFriends.ActivateGameOverlayInviteDialog(lobby)` | `void` | Overlay friend picker; invites are for that lobby |

Register call results with a `CallResult<T>` kept in a field:

```csharp
private CallResult<LobbyCreated_t> _lobbyCreated;

private void OnEnable() => this._lobbyCreated = CallResult<LobbyCreated_t>.Create(this.OnLobbyCreated);
private void OnDisable() => this._lobbyCreated?.Dispose();

public void Host() => this._lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4));

private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
{
    if (ioFailure || result.m_eResult != EResult.k_EResultOK) return;
    var lobby = new CSteamID(result.m_ulSteamIDLobby);
}
```

## Lobby data and member data

| Call | Who | Limits |
|---|---|---|
| `SetLobbyData(lobby, key, value)` | Owner only | Key up to `k_nMaxLobbyKeyLength` (255 in Steamworks.NET constants), value up to `k_cubChatMetadataMax` (8192 bytes in Steamworks.NET constants) |
| `GetLobbyData(lobby, key)` | Members, search results, or after `RequestLobbyData` | Returns `""` when unset or the lobby id is invalid |
| `DeleteLobbyData(lobby, key)` | Owner only | |
| `GetLobbyDataCount` / `GetLobbyDataByIndex` | Members | Enumerate all keys |
| `SetLobbyMemberData(lobby, key, value)` | The local user, for themselves | Same key and value limits |
| `GetLobbyMemberData(lobby, user, key)` | Members | Per-player state such as ready flag, chosen character |
| `RequestLobbyData(lobby)` | Non-members | Fetches data for a lobby you are not in (for example a friend's lobby from `GetFriendGamePlayed`); completion arrives as `LobbyDataUpdate_t` |

Suggested keys for co-op: `host_steam_id`, `build_version`, `game_mode`, `state` (`lobby` / `in_game`). Lobby data is visible to anyone who can see the lobby: never put secrets, auth tickets or IP addresses in it. Valve's matchmaking guide advises adding only metadata that is used for search; keep the set small.

## Lobby chat

- `SendLobbyChatMsg(lobby, byte[] body, int length)` broadcasts to all members, including the sender. The body can be binary or text, up to 4 KB; for text send UTF-8 bytes plus a null terminator (length = byte count + 1).
- `LobbyChatMsg_t` arrives with `m_ulSteamIDLobby`, `m_ulSteamIDUser`, `m_eChatEntryType`, `m_iChatID`. Read the payload with `GetLobbyChatEntry(lobby, (int)m_iChatID, out CSteamID sender, byte[] buffer, buffer.Length, out EChatEntryType type)`; it returns the byte count.
- Use lobby chat for pre-game text and simple "ready" pings. Use NGO messages once the session runs.

## Callbacks

| Struct | Fires when | Fields used |
|---|---|---|
| `LobbyCreated_t` | `CreateLobby` completes (call result and callback) | `m_eResult`, `m_ulSteamIDLobby` |
| `LobbyEnter_t` | You enter a lobby (also after creating one) | `m_ulSteamIDLobby`, `m_EChatRoomEnterResponse` (cast to `EChatRoomEnterResponse`), `m_bLocked` |
| `LobbyDataUpdate_t` | Lobby or member data changed, or `RequestLobbyData` finished | `m_ulSteamIDLobby`, `m_ulSteamIDMember` (equals the lobby id for lobby-level data), `m_bSuccess` |
| `LobbyChatUpdate_t` | A member joined, left, disconnected, was kicked or banned | `m_ulSteamIDUserChanged`, `m_ulSteamIDMakingChange`, `m_rgfChatMemberStateChange` (bit flags of `EChatMemberStateChange`) |
| `LobbyChatMsg_t` | A lobby chat message arrived | see above |
| `LobbyMatchList_t` | `RequestLobbyList` completes | `m_nLobbiesMatching`, then `GetLobbyByIndex(i)` |
| `GameLobbyJoinRequested_t` | Player accepts a lobby invite or uses "Join Game" while the game runs | `m_steamIDLobby`, `m_steamIDFriend` |
| `GameRichPresenceJoinRequested_t` | Player joins through a friend's rich presence `connect` or `InviteUserToGame` while the game runs | `m_rgchConnect`, `m_steamIDFriend` |
| `NewUrlLaunchParameters_t` | A `steam://run/<appid>//...` URL runs while the game is open | none; re-read `GetLaunchCommandLine` |

`EChatRoomEnterResponse` values you will see: `Success`, `DoesntExist`, `NotAllowed`, `Full`, `Error`, `Banned`, `Limited`, `ClanDisabled`, `CommunityBan`, `MemberBlockedYou`, `YouBlockedMember`, `RatelimitExceeded` (all prefixed `k_EChatRoomEnterResponse`). Show `Full` and `DoesntExist` with their own message.

## Launch and join paths

| Situation | What arrives | Handle with |
|---|---|---|
| Game running, lobby invite accepted | `GameLobbyJoinRequested_t` | `JoinLobby(m_steamIDLobby)` |
| Game running, "Join Game" via rich presence | `GameRichPresenceJoinRequested_t` | Parse `m_rgchConnect` |
| Game not running | Process launched with `+connect_lobby <64-bit lobby id>` | Parse `Environment.GetCommandLineArgs()` at startup |
| App has "Use launch command line" enabled (Installation > General in Steamworks) | Rich presence joins are passed through Steam instead of the OS command line | `SteamApps.GetLaunchCommandLine(out string cmd, bufferSize)` |

Register `GameLobbyJoinRequested_t` and `GameRichPresenceJoinRequested_t` on a persistent object created before the main menu, so invites accepted during loading are not lost.

## Rich presence

- `SteamFriends.SetRichPresence(key, value)`; an empty value removes the key. `ClearRichPresence()` removes all keys.
- `connect`: the command line a friend's game receives when joining through the friends list. Set it to `+connect_lobby <lobbyId>` while in a session.
- `steam_display`: a localization token shown in the friends list; tokens are uploaded in the Steamworks rich presence localization file (see `steamworks-integration`).
- `steam_player_group` and `steam_player_group_size`: let the Steam client show party members together.
- Limits: key length `k_cchMaxRichPresenceKeyLength` (64) and value length `k_cchMaxRichPresenceValueLength` (256) in Steamworks.NET constants. Valve's page says 20 keys per user while the SDK constant `k_cchMaxRichPresenceKeys` is 30; stay well under 20 and check the current documentation if you need more.

## Lobby list (public lobbies only)

Add filters, then call `RequestLobbyList()` and wait for `LobbyMatchList_t`:

```csharp
SteamMatchmaking.AddRequestLobbyListStringFilter("build_version", Application.version, ELobbyComparison.k_ELobbyComparisonEqual);
SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterDefault);
this._lobbyList.Set(SteamMatchmaking.RequestLobbyList());
```

Filters are cleared by each `RequestLobbyList` call, so add them again every time. Full lobbies are never returned. With AppID 480 every developer's test lobbies share the same list: always filter on a game-specific key.
