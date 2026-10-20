# Steam P2P checks

Read when a Steam P2P connection fails or drops, to tell relay from direct routes, read connection end reasons, add Steam fake lag, or plan two-account tests.

Assumes the SteamNetworkingSockets community transport and Steamworks.NET from `steam-lobbies-p2p`. Lobby flow, invites and transport install live there; this file covers diagnosing the connection itself.

## Relay (SDR) vs direct

- `CreateListenSocketP2P` / `ConnectP2P` connections are relayed through Valve's network (Steam Datagram Relay) by default. No port forwarding is needed and peers do not see each other's IP.
- Newer Steamworks SDKs can also negotiate direct (ICE / NAT-punched) routes. In Steamworks.NET 2025.164.1 the relevant options are `k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable` (with the `k_nSteamNetworkingConfig_P2P_Transport_ICE_Enable_*` constants: Disable, Relay, Private, Public, All), `k_ESteamNetworkingConfig_P2P_STUN_ServerList` and the `P2P_Transport_ICE_Penalty` / `P2P_Transport_SDR_Penalty` scores. Which route the default picks, and whether the community transport passes these options, is not documented; check `GetDetailedConnectionStatus` output (below) on a live connection before assuming either.
- For a relayed connection, RTT is client to relay to host, so two players close to each other can still see a noticeably higher ping than a direct LAN ping. Compare against the `m_nPing` Steam reports, not against a LAN baseline.

## Relay readiness

Before connecting, both peers need relay access:

```csharp
using Steamworks;
using UnityEngine;

public static class SteamRelayCheck
{
    public static bool IsReady(out string detail)
    {
        ESteamNetworkingAvailability availability = SteamNetworkingUtils.GetRelayNetworkStatus(out SteamRelayNetworkStatus_t status);
        detail = status.m_debugMsg;
        return availability == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current;
    }
}
```

- `Current` means usable. `Waiting` / `Attempting` / `NeverTried` mean still initializing; the client transport calls `InitRelayNetworkAccess()` itself, but calling it at startup (after `SteamAPI.Init`) gives relays time to warm up.
- `Failed`, `Previously`, `CannotTry`: the machine cannot reach Valve's relays. Retest on another network (mobile hotspot); corporate, school and some hotel networks block it. Show the player a message instead of a silent timeout.
- Call this only after `SteamBootstrap.IsInitialized` (see `steamworks-integration`).

## Connection end reasons

Steam reports why a connection ended in `SteamNetConnectionInfo_t.m_eEndReason` (an `ESteamNetConnectionEnd` value) plus a debug string `m_szEndDebug`, delivered with `SteamNetConnectionStatusChangedCallback_t`. The community transport consumes that callback internally; to see the reason, embed the package (copy it into `Packages/`) and log `m_info.m_eEndReason` and `m_info.m_szEndDebug` in its status-changed handler.

| Range / value | Meaning | Usual action |
|---|---|---|
| 1000-1999 `App_*` | Closed by the application normally | Expected on leave |
| 2000-2999 `AppException_*` | Closed by the application for an unusual reason | Check your own close paths |
| 3001 `Local_OfflineMode` | Steam is in offline mode | Ask the player to go online |
| 3002 `Local_ManyRelayConnectivity` | This machine cannot reach many or all relays | Local network problem; see relay readiness |
| 3004 `Local_NetworkConfig` | Could not fetch Steam's network config | Local network problem |
| 3005 `Local_Rights` | Steam rejected the request for lack of rights | Check app ownership / AppID |
| 4001 `Remote_Timeout` | Connection lost; our relay link looked fine | Remote side or path in between; host crash or quit |
| 4004 `Remote_NotLoggedIn` | Remote host is not logged into Steam | Host must be online |
| 4005 `Remote_NotRunningApp` | Remote host is not running this app | Wrong SteamID, host closed the game, or AppID mismatch (480 vs real) |
| 4006 `Remote_BadProtocolVersion` | Protocol version mismatch | Different Steam client or SDK versions |
| 5003 `Misc_Timeout` | Timed out, cause unknown | Combine with relay status on both sides |
| 5004 `Misc_RelayConnectivity` | Trouble talking to the relevant relay | Retry; check relay status |
| 5005 `Misc_SteamConnectivity` | Trouble talking to Steam | Steam outage or local network |

Values are from the Steamworks `steamnetworkingtypes` documentation; NGO's own `DisconnectReason` does not include them.

## Live connection stats

`SteamNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, 0, ref lanes)` fills `SteamNetConnectionRealTimeStatus_t` with `m_nPing`, `m_flConnectionQualityLocal` / `m_flConnectionQualityRemote` (fraction of packets delivered), `m_flOutBytesPerSec`, `m_flInBytesPerSec`, `m_cbPendingReliable` and `m_usecQueueTime`. `GetDetailedConnectionStatus(conn, out string text, size)` returns a multi-line diagnostic dump including the route. Both need the `HSteamNetConnection` handle, which the community transport keeps private; expose it in an embedded copy if you need these numbers. This is also the only source of RTT on that transport, because its `GetCurrentRtt` returns 0.

## Steam fake lag and loss

The Network Simulator does not affect Steam transports. Steam has global fake-network options; Steamworks.NET exposes them only through `SetConfigValue` with a pointer:

```csharp
using System;
using System.Runtime.InteropServices;
using Steamworks;

public static class SteamFakeNetwork
{
    public static void Apply(int lagMs, float lossPercent)
    {
        SetInt(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_FakePacketLag_Send, lagMs);
        SetFloat(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_FakePacketLoss_Send, lossPercent);
    }

    public static void Clear() => Apply(0, 0f);

    private static bool SetInt(ESteamNetworkingConfigValue key, int value)
    {
        IntPtr buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(buffer, value);
            return SteamNetworkingUtils.SetConfigValue(key, ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero, ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool SetFloat(ESteamNetworkingConfigValue key, float value)
    {
        IntPtr buffer = Marshal.AllocHGlobal(sizeof(float));
        try
        {
            Marshal.WriteInt32(buffer, BitConverter.SingleToInt32Bits(value));
            return SteamNetworkingUtils.SetConfigValue(key, ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero, ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Float, buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
```

- Apply on one peer (usually a client) in development builds only, after `SteamAPI.Init` and before connecting. The `_Recv` variants delay or drop incoming packets.
- These options are global to the process and documented as applying at a low level; they affect every Steam networking connection in that process.
- Guard calls with `#if DEVELOPMENT_BUILD || UNITY_EDITOR` so they cannot ship.

## Two-account test plan

Multiplayer Play Mode cannot test Steam: every virtual player shares one Steam login. Use two machines with two Steam accounts that are friends (full matrix in `steam-lobbies-p2p`, `references/testing.md`). Network-specific additions:

| Case | Expect |
|---|---|
| Both on the same LAN | Connects; note the route and RTT in the detailed status |
| Different networks (one on a phone hotspot) | Connects through relays; RTT higher but stable |
| One machine on a network that blocks relays | Relay status not `Current`; clear error to the player, no hang |
| Steam fake lag 150 ms + 3 % loss on the client | Same behaviour as the Network Simulator run on `UnityTransport` |
| Host kills the process | Client gets a timeout end reason and returns to the menu |
| Client pulls the network cable for longer than the timeout | Host removes the player; client returns to the menu with a reason |

Log on both machines: relay status at startup, SteamIDs of both peers, AppID, build version, the end reason and debug string on every closed connection, and NGO's `DisconnectReason`.
