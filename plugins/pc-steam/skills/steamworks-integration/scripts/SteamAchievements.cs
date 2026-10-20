#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

[DisallowMultipleComponent]
public class SteamAchievements : MonoBehaviour
{
#if !DISABLESTEAMWORKS
    #region Fields

    [SerializeField] private float minStoreInterval = 60f;

    private Callback<UserStatsStored_t> _statsStored;
    private Callback<UserAchievementStored_t> _achievementStored;
    private ulong _gameId;
    private bool _ready;
    private bool _statsDirty;
    private float _lastStoreTime = float.NegativeInfinity;

    #endregion

    #region Properties

    public bool IsReady => this._ready && SteamBootstrap.IsInitialized;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (!SteamBootstrap.IsInitialized) return;

        this._gameId = new CGameID(SteamUtils.GetAppID()).m_GameID;
        this._statsStored = Callback<UserStatsStored_t>.Create(this.OnStatsStored);
        this._achievementStored = Callback<UserAchievementStored_t>.Create(this.OnAchievementStored);
        this._ready = true;
    }

    private void Update()
    {
        if (!this._statsDirty || !this.IsReady) return;
        if (Time.unscaledTime - this._lastStoreTime < this.minStoreInterval) return;
        this.Store();
    }

    private void OnApplicationQuit()
    {
        if (this._statsDirty && this.IsReady) this.Store();
    }

    private void OnDestroy()
    {
        this._statsStored?.Dispose();
        this._achievementStored?.Dispose();
        this._statsStored = null;
        this._achievementStored = null;
        this._ready = false;
    }

    #endregion

    #region Achievements

    public bool IsUnlocked(string apiName)
    {
        return this.IsReady && SteamUserStats.GetAchievement(apiName, out var achieved) && achieved;
    }

    public void Unlock(string apiName)
    {
        if (!this.IsReady) return;
        if (!SteamUserStats.GetAchievement(apiName, out var achieved))
        {
            Debug.LogWarning($"{nameof(SteamAchievements)}: unknown achievement '{apiName}'. Check the API name and that the definitions are published.", this);
            return;
        }

        if (achieved) return;
        if (SteamUserStats.SetAchievement(apiName)) this.Store();
    }

    public void ShowProgress(string apiName, uint current, uint max)
    {
        if (!this.IsReady || current == 0 || current >= max) return;
        SteamUserStats.IndicateAchievementProgress(apiName, current, max);
    }

    #endregion

    #region Stats

    public bool TryGetStat(string apiName, out int value)
    {
        value = 0;
        return this.IsReady && SteamUserStats.GetStat(apiName, out value);
    }

    public bool TryGetStat(string apiName, out float value)
    {
        value = 0f;
        return this.IsReady && SteamUserStats.GetStat(apiName, out value);
    }

    public void SetStat(string apiName, int value)
    {
        if (this.IsReady && SteamUserStats.SetStat(apiName, value)) this._statsDirty = true;
    }

    public void SetStat(string apiName, float value)
    {
        if (this.IsReady && SteamUserStats.SetStat(apiName, value)) this._statsDirty = true;
    }

    public void IncrementStat(string apiName, int delta = 1)
    {
        if (this.TryGetStat(apiName, out int current)) this.SetStat(apiName, current + delta);
    }

    public void StoreNow()
    {
        if (this.IsReady) this.Store();
    }

    #endregion

    #region Storing

    private void Store()
    {
        this._lastStoreTime = Time.unscaledTime;
        if (SteamUserStats.StoreStats()) this._statsDirty = false;
    }

    private void OnStatsStored(UserStatsStored_t callback)
    {
        if (callback.m_nGameID != this._gameId) return;
        if (callback.m_eResult == EResult.k_EResultOK) return;

        if (callback.m_eResult == EResult.k_EResultInvalidParam)
        {
            Debug.LogWarning($"{nameof(SteamAchievements)}: Steam rejected one or more stat values and sent back the server values. Re-read stats before writing again.", this);
            return;
        }

        Debug.LogWarning($"{nameof(SteamAchievements)}: StoreStats failed with {callback.m_eResult}.", this);
        this._statsDirty = true;
    }

    private void OnAchievementStored(UserAchievementStored_t callback)
    {
        if (callback.m_nGameID != this._gameId) return;
        if (Debug.isDebugBuild) Debug.Log($"{nameof(SteamAchievements)}: stored '{callback.m_rgchAchievementName}' ({callback.m_nCurProgress}/{callback.m_nMaxProgress}).", this);
    }

    #endregion

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    #region Debug

    [ContextMenu("Reset All Stats And Achievements")]
    private void ResetAllForTesting()
    {
        if (this.IsReady) SteamUserStats.ResetAllStats(true);
    }

    #endregion
#endif
#else
    #region Properties

    public bool IsReady => false;

    #endregion

    #region Achievements

    public bool IsUnlocked(string apiName) => false;
    public void Unlock(string apiName) { }
    public void ShowProgress(string apiName, uint current, uint max) { }

    #endregion

    #region Stats

    public bool TryGetStat(string apiName, out int value)
    {
        value = 0;
        return false;
    }

    public bool TryGetStat(string apiName, out float value)
    {
        value = 0f;
        return false;
    }

    public void SetStat(string apiName, int value) { }
    public void SetStat(string apiName, float value) { }
    public void IncrementStat(string apiName, int delta = 1) { }
    public void StoreNow() { }

    #endregion
#endif
}
