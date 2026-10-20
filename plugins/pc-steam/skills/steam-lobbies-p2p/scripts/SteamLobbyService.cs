#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using System;
using System.Globalization;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class SteamLobbyService : MonoBehaviour
{
    #region Fields

    public const string HostSteamIdKey = "host_steam_id";
    public const string BuildVersionKey = "build_version";

    private const string ConnectLobbyArgument = "+connect_lobby";
    private const string ConnectRichPresenceKey = "connect";
    private const int LaunchCommandLineBufferSize = 1024;

    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;
    [SerializeField, Range(1, 250)] private int maxPlayers = 4;
    [SerializeField] private bool joinFromLaunchArgumentsOnStart = true;

    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;
    private Callback<GameRichPresenceJoinRequested_t> _richPresenceJoinRequested;
    private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
    private CallResult<LobbyCreated_t> _lobbyCreated;
    private CallResult<LobbyEnter_t> _lobbyEntered;
    private CSteamID _lobbyId = CSteamID.Nil;
    private CSteamID _hostId = CSteamID.Nil;
    private bool _isBusy;

    #endregion

    #region Events

    public event Action<CSteamID> LobbyReady;
    public event Action<CSteamID> BeforeStartClient;
    public event Action<CSteamID, EChatMemberStateChange> MemberChanged;
    public event Action<string> Failed;
    public event Action<string> SessionEnded;

    #endregion

    #region Properties

    public CSteamID LobbyId => this._lobbyId;
    public CSteamID HostId => this._hostId;
    public bool InLobby => this._lobbyId != CSteamID.Nil;
    public bool IsBusy => this._isBusy;

    public static bool IsSteamReady => SteamBootstrap.IsInitialized;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (this.networkManager == null) this.networkManager = FindAnyObjectByType<NetworkManager>();
        if (this.networkManager != null) return;

        Debug.LogError($"{nameof(SteamLobbyService)} needs a {nameof(NetworkManager)} in the scene.", this);
        enabled = false;
    }

    private void OnEnable()
    {
        this._gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(this.OnGameLobbyJoinRequested);
        this._richPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(this.OnRichPresenceJoinRequested);
        this._lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(this.OnLobbyChatUpdate);
        this._lobbyCreated = CallResult<LobbyCreated_t>.Create(this.OnLobbyCreated);
        this._lobbyEntered = CallResult<LobbyEnter_t>.Create(this.OnLobbyEntered);
        this.networkManager.OnClientStopped += this.OnClientStopped;
    }

    private void Start()
    {
        if (!this.joinFromLaunchArgumentsOnStart || !IsSteamReady) return;
        if (TryGetLaunchLobby(out var lobbyId)) this.Join(lobbyId);
    }

    private void OnDisable()
    {
        this.Leave();
        if (this.networkManager != null) this.networkManager.OnClientStopped -= this.OnClientStopped;

        this._gameLobbyJoinRequested?.Dispose();
        this._richPresenceJoinRequested?.Dispose();
        this._lobbyChatUpdate?.Dispose();
        this._lobbyCreated?.Dispose();
        this._lobbyEntered?.Dispose();
        this._gameLobbyJoinRequested = null;
        this._richPresenceJoinRequested = null;
        this._lobbyChatUpdate = null;
        this._lobbyCreated = null;
        this._lobbyEntered = null;
        this._isBusy = false;
    }

    #endregion

    #region Public API

    public void Host()
    {
        if (!this.CanStartRequest()) return;
        if (this.InLobby || this.networkManager.IsListening)
        {
            this.Fail("Leave the current session before hosting.");
            return;
        }

        this._isBusy = true;
        this._lobbyCreated.Set(SteamMatchmaking.CreateLobby(this.lobbyType, this.maxPlayers));
    }

    public void Join(CSteamID lobbyId)
    {
        if (!lobbyId.IsLobby())
        {
            this.Fail($"{lobbyId} is not a lobby id.");
            return;
        }

        if (lobbyId == this._lobbyId) return;
        if (!this.CanStartRequest()) return;
        if (this.InLobby || this.networkManager.IsListening) this.Leave();

        this._isBusy = true;
        this._lobbyEntered.Set(SteamMatchmaking.JoinLobby(lobbyId));
    }

    public void OpenInviteOverlay()
    {
        if (!this.InLobby || !IsSteamReady) return;
        SteamFriends.ActivateGameOverlayInviteDialog(this._lobbyId);
    }

    public void Leave()
    {
        var lobbyId = this._lobbyId;
        this._lobbyId = CSteamID.Nil;
        this._hostId = CSteamID.Nil;

        if (this.networkManager != null && this.networkManager.IsListening) this.networkManager.Shutdown();
        if (lobbyId == CSteamID.Nil || !IsSteamReady) return;

        SteamMatchmaking.LeaveLobby(lobbyId);
        SteamFriends.SetRichPresence(ConnectRichPresenceKey, string.Empty);
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
    {
        this._isBusy = false;
        if (ioFailure || result.m_eResult != EResult.k_EResultOK)
        {
            this.Fail($"CreateLobby failed: {(ioFailure ? "IO failure" : result.m_eResult.ToString())}.");
            return;
        }

        this._lobbyId = new CSteamID(result.m_ulSteamIDLobby);
        this._hostId = SteamUser.GetSteamID();
        SteamMatchmaking.SetLobbyData(this._lobbyId, HostSteamIdKey, this._hostId.m_SteamID.ToString(CultureInfo.InvariantCulture));
        SteamMatchmaking.SetLobbyData(this._lobbyId, BuildVersionKey, Application.version);
        this.PublishConnectPresence();

        if (!this.networkManager.StartHost())
        {
            this.Leave();
            this.Fail("NetworkManager.StartHost failed.");
            return;
        }

        this.LobbyReady?.Invoke(this._lobbyId);
    }

    private void OnLobbyEntered(LobbyEnter_t result, bool ioFailure)
    {
        this._isBusy = false;
        var response = (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse;
        if (ioFailure || response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            this.Fail($"Could not join the lobby: {(ioFailure ? "IO failure" : response.ToString())}.");
            return;
        }

        this._lobbyId = new CSteamID(result.m_ulSteamIDLobby);
        var hostVersion = SteamMatchmaking.GetLobbyData(this._lobbyId, BuildVersionKey);
        if (hostVersion != Application.version)
        {
            this.Leave();
            this.Fail($"Version mismatch: host runs '{hostVersion}', this build is '{Application.version}'.");
            return;
        }

        if (!TryGetHost(this._lobbyId, out var hostId))
        {
            this.Leave();
            this.Fail("The lobby has no valid host.");
            return;
        }

        this._hostId = hostId;
        this.PublishConnectPresence();
        this.BeforeStartClient?.Invoke(hostId);

        if (!this.networkManager.StartClient())
        {
            this.Leave();
            this.Fail("NetworkManager.StartClient failed.");
            return;
        }

        this.LobbyReady?.Invoke(this._lobbyId);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t request)
    {
        this.Join(request.m_steamIDLobby);
    }

    private void OnRichPresenceJoinRequested(GameRichPresenceJoinRequested_t request)
    {
        var connect = request.m_rgchConnect;
        if (string.IsNullOrEmpty(connect)) return;
        if (TryParseConnectLobby(connect.Split(' ', StringSplitOptions.RemoveEmptyEntries), out var lobbyId)) this.Join(lobbyId);
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t update)
    {
        if (update.m_ulSteamIDLobby != this._lobbyId.m_SteamID) return;
        this.MemberChanged?.Invoke(new CSteamID(update.m_ulSteamIDUserChanged), (EChatMemberStateChange)update.m_rgfChatMemberStateChange);
    }

    #endregion

    #region Netcode

    private void OnClientStopped(bool wasHost)
    {
        if (!this.InLobby) return;

        var reason = this.networkManager.DisconnectReason;
        this.Leave();
        this.SessionEnded?.Invoke(string.IsNullOrEmpty(reason) ? (wasHost ? "Hosting stopped." : "Lost connection to the host.") : reason);
    }

    #endregion

    #region Helpers

    private bool CanStartRequest()
    {
        if (this._isBusy) return false;
        if (IsSteamReady) return true;

        this.Fail("Steam is not running or SteamBootstrap failed to initialize SteamAPI.");
        return false;
    }

    private void PublishConnectPresence()
    {
        SteamFriends.SetRichPresence(ConnectRichPresenceKey, $"{ConnectLobbyArgument} {this._lobbyId.m_SteamID.ToString(CultureInfo.InvariantCulture)}");
    }

    private void Fail(string message)
    {
        Debug.LogWarning($"[{nameof(SteamLobbyService)}] {message}", this);
        this.Failed?.Invoke(message);
    }

    private static bool TryGetHost(CSteamID lobbyId, out CSteamID hostId)
    {
        var raw = SteamMatchmaking.GetLobbyData(lobbyId, HostSteamIdKey);
        hostId = ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? new CSteamID(value)
            : SteamMatchmaking.GetLobbyOwner(lobbyId);
        return hostId.IsValid() && hostId != SteamUser.GetSteamID();
    }

    private static bool TryGetLaunchLobby(out CSteamID lobbyId)
    {
        if (TryParseConnectLobby(Environment.GetCommandLineArgs(), out lobbyId)) return true;
        if (SteamApps.GetLaunchCommandLine(out var commandLine, LaunchCommandLineBufferSize) <= 0 || string.IsNullOrEmpty(commandLine)) return false;
        return TryParseConnectLobby(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries), out lobbyId);
    }

    private static bool TryParseConnectLobby(string[] arguments, out CSteamID lobbyId)
    {
        for (var i = 0; i < arguments.Length - 1; i++)
        {
            if (!string.Equals(arguments[i], ConnectLobbyArgument, StringComparison.OrdinalIgnoreCase)) continue;
            if (!ulong.TryParse(arguments[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var value)) continue;

            lobbyId = new CSteamID(value);
            if (lobbyId.IsLobby()) return true;
        }

        lobbyId = CSteamID.Nil;
        return false;
    }

    #endregion
}
#endif
