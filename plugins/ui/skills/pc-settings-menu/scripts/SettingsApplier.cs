using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SettingsApplier
{
    #region Fields

    private const float MinRenderScale = 0.1f;
    private const float MaxRenderScale = 2f;

    private static readonly Dictionary<UniversalRenderPipelineAsset, float> EditorRenderScales = new();

    #endregion

    #region Properties

    public static GameSettings Current { get; private set; }

    #endregion

    #region Bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        EditorRenderScales.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        Current = GameSettings.LoadOrDefault();
        ApplyAll(Current, InputSystem.actions);
        if (Application.isEditor) Application.quitting += RestoreEditorRenderScales;
    }

    public static void Commit(GameSettings settings, InputActionAsset actions)
    {
        if (actions != null) settings.bindingOverridesJson = actions.SaveBindingOverridesAsJson();
        settings.Save();
        Current = settings.Clone();
    }

    #endregion

    #region Apply

    public static void ApplyAll(GameSettings settings, InputActionAsset actions)
    {
        ApplyDisplay(settings);
        ApplyGraphics(settings);
        ApplyBindings(actions, settings.bindingOverridesJson);
    }

    public static void ApplyDisplay(GameSettings settings)
    {
        var mode = SupportedMode(settings.fullScreenMode);
        if (mode == FullScreenMode.ExclusiveFullScreen && settings.HasRefreshRate)
        {
            Screen.SetResolution(settings.width, settings.height, mode, settings.RefreshRate);
            return;
        }

        Screen.SetResolution(settings.width, settings.height, mode);
    }

    public static void ApplyGraphics(GameSettings settings)
    {
        var level = Mathf.Clamp(settings.qualityLevel, 0, QualitySettings.count - 1);
        if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);

        QualitySettings.vSyncCount = Mathf.Clamp(settings.vSyncCount, 0, 4);
        Application.targetFrameRate = QualitySettings.vSyncCount > 0 ? -1 : settings.targetFrameRate;

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            if (Application.isEditor && !EditorRenderScales.ContainsKey(urp)) EditorRenderScales[urp] = urp.renderScale;
            urp.renderScale = Mathf.Clamp(settings.renderScale, MinRenderScale, MaxRenderScale);
        }
    }

    public static void ApplyBindings(InputActionAsset actions, string overridesJson)
    {
        if (actions == null) return;

        if (string.IsNullOrEmpty(overridesJson))
        {
            actions.RemoveAllBindingOverrides();
            return;
        }

        actions.LoadBindingOverridesFromJson(overridesJson);
    }

    public static FullScreenMode SupportedMode(FullScreenMode requested)
    {
        var platform = Application.platform;
        var isWindows = platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor;
        var isMac = platform == RuntimePlatform.OSXPlayer || platform == RuntimePlatform.OSXEditor;

        return requested switch
        {
            FullScreenMode.ExclusiveFullScreen when !isWindows => FullScreenMode.FullScreenWindow,
            FullScreenMode.MaximizedWindow when !isWindows && !isMac => FullScreenMode.Windowed,
            _ => requested
        };
    }

    private static void RestoreEditorRenderScales()
    {
        Application.quitting -= RestoreEditorRenderScales;
        foreach (var pair in EditorRenderScales)
        {
            if (pair.Key != null) pair.Key.renderScale = pair.Value;
        }

        EditorRenderScales.Clear();
    }

    #endregion

    #region Display options

    public static List<Resolution> GetResolutionOptions()
    {
        var result = new List<Resolution>();
        var seen = new HashSet<long>();
        var all = Screen.resolutions;
        for (var i = all.Length - 1; i >= 0; i--)
        {
            var key = ((long)all[i].width << 32) | (uint)all[i].height;
            if (!seen.Add(key)) continue;

            result.Add(BestRefreshFor(all, all[i].width, all[i].height));
        }

        result.Sort((a, b) => a.width != b.width ? b.width.CompareTo(a.width) : b.height.CompareTo(a.height));
        return result;
    }

    public static List<RefreshRate> GetRefreshRates(int width, int height)
    {
        var result = new List<RefreshRate>();
        foreach (var resolution in Screen.resolutions)
        {
            if (resolution.width != width || resolution.height != height) continue;
            if (!result.Contains(resolution.refreshRateRatio)) result.Add(resolution.refreshRateRatio);
        }

        result.Sort((a, b) => b.value.CompareTo(a.value));
        return result;
    }

    private static Resolution BestRefreshFor(Resolution[] all, int width, int height)
    {
        var best = default(Resolution);
        var bestHz = double.MinValue;
        foreach (var resolution in all)
        {
            if (resolution.width != width || resolution.height != height) continue;
            if (resolution.refreshRateRatio.value <= bestHz) continue;

            best = resolution;
            bestHz = resolution.refreshRateRatio.value;
        }

        return best;
    }

    #endregion

    #region Confirmation

    public static async Awaitable<bool> ApplyDisplayWithConfirmationAsync(
        GameSettings previous,
        GameSettings candidate,
        DisplayChangeConfirmation confirmation,
        CancellationToken cancellationToken)
    {
        ApplyDisplay(candidate);
        bool accepted;
        try
        {
            accepted = await confirmation.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ApplyDisplay(previous);
            throw;
        }

        if (!accepted) ApplyDisplay(previous);
        return accepted;
    }

    #endregion
}

public sealed class DisplayChangeConfirmation
{
    #region Fields

    private readonly float _timeoutSeconds;
    private bool _decided;
    private bool _accepted;

    #endregion

    #region Properties

    public float RemainingSeconds { get; private set; }

    #endregion

    #region Construction

    public DisplayChangeConfirmation(float timeoutSeconds = 15f)
    {
        this._timeoutSeconds = Mathf.Max(1f, timeoutSeconds);
        this.RemainingSeconds = this._timeoutSeconds;
    }

    public void Accept() => Decide(true);

    public void Revert() => Decide(false);

    private void Decide(bool accepted)
    {
        if (this._decided) return;

        this._decided = true;
        this._accepted = accepted;
    }

    #endregion

    #region Wait

    public async Awaitable<bool> WaitAsync(CancellationToken cancellationToken)
    {
        var deadline = Time.unscaledTime + this._timeoutSeconds;
        while (!this._decided)
        {
            this.RemainingSeconds = Mathf.Max(0f, deadline - Time.unscaledTime);
            if (this.RemainingSeconds <= 0f) Decide(false);
            else await Awaitable.NextFrameAsync(cancellationToken);
        }

        this.RemainingSeconds = 0f;
        return this._accepted;
    }

    #endregion
}
