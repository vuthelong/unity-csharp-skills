#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class SteamBootstrap : MonoBehaviour
{
#if !DISABLESTEAMWORKS
    #region Fields

    [SerializeField] private uint appId = 480;
    [SerializeField] private bool restartThroughSteam = true;
    [SerializeField] private bool quitWhenInitFails;

    private static SteamBootstrap _instance;
    private static bool _everInitialized;

    private Callback<GameOverlayActivated_t> _overlayActivated;

    #endregion

    #region Properties

    public static bool IsInitialized { get; private set; }
    public static ESteamAPIInitResult InitResult { get; private set; } = ESteamAPIInitResult.k_ESteamAPIInitResult_FailedGeneric;
    public static string InitError { get; private set; } = string.Empty;
    public static bool IsOverlayActive { get; private set; }

    public static event Action<bool> OverlayToggled;

    #endregion

    #region Unity Lifecycle

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _everInitialized = false;
        IsInitialized = false;
        IsOverlayActive = false;
        InitResult = ESteamAPIInitResult.k_ESteamAPIInitResult_FailedGeneric;
        InitError = string.Empty;
        OverlayToggled = null;
    }

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (_everInitialized)
        {
            Debug.LogError($"{nameof(SteamBootstrap)}: SteamAPI was already initialized once in this process; not initializing again.", this);
            return;
        }

        this.Initialize();
    }

    private void Update()
    {
        if (!IsInitialized) return;
        SteamAPI.RunCallbacks();
    }

    private void OnDestroy()
    {
        if (_instance != this) return;
        _instance = null;

        this._overlayActivated?.Dispose();
        this._overlayActivated = null;

        if (!IsInitialized) return;
        IsInitialized = false;
        SteamAPI.Shutdown();
    }

    #endregion

    #region Initialization

    private void Initialize()
    {
        if (!Packsize.Test()) Debug.LogError($"{nameof(SteamBootstrap)}: Packsize test failed; the wrong Steamworks.NET build is running on this platform.", this);
        if (!DllCheck.Test()) Debug.LogError($"{nameof(SteamBootstrap)}: DllCheck failed; a steam_api binary has the wrong version.", this);

        try
        {
            if (!Application.isEditor && this.restartThroughSteam && SteamAPI.RestartAppIfNecessary(new AppId_t(this.appId)))
            {
                Application.Quit();
                return;
            }

            InitResult = SteamAPI.InitEx(out var errorMessage);
            InitError = errorMessage ?? string.Empty;
        }
        catch (DllNotFoundException e)
        {
            InitResult = ESteamAPIInitResult.k_ESteamAPIInitResult_FailedGeneric;
            InitError = e.Message;
        }

        if (InitResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
        {
            Debug.LogWarning($"{nameof(SteamBootstrap)}: Steam unavailable ({InitResult}): {InitError}. Running without Steam features.", this);
            if (this.quitWhenInitFails && !Application.isEditor) Application.Quit();
            return;
        }

        IsInitialized = true;
        _everInitialized = true;
        this._overlayActivated = Callback<GameOverlayActivated_t>.Create(this.OnOverlayActivated);
    }

    #endregion

    #region Overlay

    private void OnOverlayActivated(GameOverlayActivated_t callback)
    {
        IsOverlayActive = callback.m_bActive != 0;
        OverlayToggled?.Invoke(IsOverlayActive);
    }

    #endregion
#else
    #region Properties

    public static bool IsInitialized => false;
    public static bool IsOverlayActive => false;

    public static event Action<bool> OverlayToggled
    {
        add { }
        remove { }
    }

    #endregion
#endif
}
