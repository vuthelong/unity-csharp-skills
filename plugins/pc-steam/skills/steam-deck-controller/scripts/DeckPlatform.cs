#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

[DisallowMultipleComponent]
public class DeckPlatform : MonoBehaviour
{
    #region Fields

    public static readonly Vector2Int DeckNativeResolution = new Vector2Int(1280, 800);

#if !DISABLESTEAMWORKS
    private static DeckPlatform _instance;

    private Callback<GamepadTextInputDismissed_t> _gamepadTextDismissed;
    private Callback<FloatingGamepadTextInputDismissed_t> _floatingTextDismissed;
    private Action<string> _pendingTextSubmit;
    private Action _pendingTextCancel;
    private bool _steamInputInitialized;
#endif

    #endregion

    #region Properties

    public static bool IsSteamDeck { get; private set; }
    public static bool IsBigPicture { get; private set; }
    public static bool IsProton { get; private set; }

#if !DISABLESTEAMWORKS
    public static event Action FloatingKeyboardClosed;
#else
    public static event Action FloatingKeyboardClosed
    {
        add { }
        remove { }
    }
#endif

    #endregion

    #region Unity Lifecycle

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsSteamDeck = false;
        IsBigPicture = false;
        IsProton = false;
#if !DISABLESTEAMWORKS
        FloatingKeyboardClosed = null;
        _instance = null;
#endif
    }

#if !DISABLESTEAMWORKS
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
    }

    private void Start()
    {
        if (!SteamBootstrap.IsInitialized) return;

#if STEAMWORKS_SDK_165_OR_NEWER
        IsSteamDeck = SteamUtils.IsRunningOnSteamHardware() == ESteamHardwareType.k_ESteamHardwareTypeSteamDeck;
        IsProton = SteamUtils.IsRunningUnderProton();
#else
        IsSteamDeck = SteamUtils.IsSteamRunningOnSteamDeck();
#endif
        IsBigPicture = SteamUtils.IsSteamInBigPictureMode();

        this._gamepadTextDismissed = Callback<GamepadTextInputDismissed_t>.Create(this.OnGamepadTextDismissed);
        this._floatingTextDismissed = Callback<FloatingGamepadTextInputDismissed_t>.Create(this.OnFloatingTextDismissed);
    }

    private void OnDisable()
    {
        if (!this._steamInputInitialized) return;
        this._steamInputInitialized = false;
        if (SteamBootstrap.IsInitialized) SteamInput.Shutdown();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        this._gamepadTextDismissed?.Dispose();
        this._floatingTextDismissed?.Dispose();
        this._gamepadTextDismissed = null;
        this._floatingTextDismissed = null;
        this._pendingTextSubmit = null;
        this._pendingTextCancel = null;
    }
#endif

    #endregion

    #region Text Input

    public static bool ShowFloatingKeyboard(RectTransform field, bool multiline = false)
    {
        if (field == null) return false;
        var canvas = field.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var corners = new Vector3[4];
        field.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return ShowFloatingKeyboard(Rect.MinMaxRect(min.x, min.y, max.x, max.y), multiline);
    }

    public static bool ShowFloatingKeyboard(Rect screenRect, bool multiline = false)
    {
#if !DISABLESTEAMWORKS
        if (_instance == null || !SteamBootstrap.IsInitialized) return false;
        var mode = multiline
            ? EFloatingGamepadTextInputMode.k_EFloatingGamepadTextInputModeModeMultipleLines
            : EFloatingGamepadTextInputMode.k_EFloatingGamepadTextInputModeModeSingleLine;
        var top = Mathf.RoundToInt(Screen.height - screenRect.yMax);
        return SteamUtils.ShowFloatingGamepadTextInput(mode, Mathf.RoundToInt(screenRect.xMin), top, Mathf.RoundToInt(screenRect.width), Mathf.RoundToInt(screenRect.height));
#else
        return false;
#endif
    }

    public static bool RequestGamepadText(string description, uint maxChars, string existingText, Action<string> onSubmit, Action onCancel = null, bool password = false)
    {
#if !DISABLESTEAMWORKS
        if (_instance == null || !SteamBootstrap.IsInitialized || onSubmit == null) return false;
        var inputMode = password ? EGamepadTextInputMode.k_EGamepadTextInputModePassword : EGamepadTextInputMode.k_EGamepadTextInputModeNormal;
        var shown = SteamUtils.ShowGamepadTextInput(inputMode, EGamepadTextInputLineMode.k_EGamepadTextInputLineModeSingleLine, description ?? string.Empty, maxChars, existingText ?? string.Empty);
        if (!shown) return false;
        _instance._pendingTextSubmit = onSubmit;
        _instance._pendingTextCancel = onCancel;
        return true;
#else
        return false;
#endif
    }

#if !DISABLESTEAMWORKS
    private void OnGamepadTextDismissed(GamepadTextInputDismissed_t callback)
    {
        var submit = this._pendingTextSubmit;
        var cancel = this._pendingTextCancel;
        this._pendingTextSubmit = null;
        this._pendingTextCancel = null;

        if (callback.m_bSubmitted && SteamUtils.GetEnteredGamepadTextInput(out var text, callback.m_unSubmittedText + 1))
        {
            submit?.Invoke(text);
            return;
        }

        cancel?.Invoke();
    }

    private void OnFloatingTextDismissed(FloatingGamepadTextInputDismissed_t callback)
    {
        FloatingKeyboardClosed?.Invoke();
    }
#endif

    #endregion

    #region Controller Type

#if !DISABLESTEAMWORKS
    public static bool TryGetSteamInputType(int gamepadIndex, out ESteamInputType inputType)
    {
        inputType = ESteamInputType.k_ESteamInputType_Unknown;
        if (_instance == null || !SteamBootstrap.IsInitialized) return false;

        if (!_instance._steamInputInitialized)
        {
            if (!SteamInput.Init(false)) return false;
            _instance._steamInputInitialized = true;
            SteamInput.RunFrame();
        }

        var handle = SteamInput.GetControllerForGamepadIndex(gamepadIndex);
        if (handle.m_InputHandle == 0) return false;
        inputType = SteamInput.GetInputTypeForHandle(handle);
        return inputType != ESteamInputType.k_ESteamInputType_Unknown;
    }
#endif

    #endregion
}
