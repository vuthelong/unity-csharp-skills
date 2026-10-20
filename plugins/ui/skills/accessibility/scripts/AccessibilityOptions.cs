using System;
using UnityEngine;

public enum InputHoldMode
{
    Hold,
    Toggle
}

public enum ColorVisionPreset
{
    Default,
    Protan,
    Deutan,
    Tritan,
    Custom
}

[Serializable]
public sealed class AccessibilityOptions
{
    #region Fields

    public const float MinTextScale = 0.8f;
    public const float MaxTextScale = 2f;
    public const float MinSubtitleScale = 0.75f;
    public const float MaxSubtitleScale = 2.5f;

    public bool subtitles = true;
    public bool soundCaptions;
    public bool speakerNames = true;
    public bool speakerColors = true;
    public float subtitleScale = 1f;
    public float subtitleBackgroundOpacity = 0.6f;
    public float subtitleReadingSpeed = 1f;

    public float textScale = 1f;
    public float uiScale = 1f;
    public bool highContrastUi;
    public bool dyslexiaFriendlyFont;

    public ColorVisionPreset colorVision = ColorVisionPreset.Default;
    public Color customAllyColor = new(0.2f, 0.6f, 1f);
    public Color customDangerColor = new(1f, 0.55f, 0f);
    public Color customInteractColor = new(1f, 1f, 1f);

    public float cameraShake = 1f;
    public float headBob = 1f;
    public bool motionBlur = true;
    public bool depthOfField = true;
    public float fieldOfView = 70f;
    public bool reduceFlashing;

    public InputHoldMode sprintMode = InputHoldMode.Hold;
    public InputHoldMode crouchMode = InputHoldMode.Toggle;
    public InputHoldMode aimMode = InputHoldMode.Hold;
    public bool replaceButtonMashingWithHold;

    public float playerDamageTaken = 1f;
    public float survivalNeedsRate = 1f;
    public bool skipTimedInputs;
    public bool aimAssist;

    public bool menuNarration;

    #endregion

    #region Static state

    private static AccessibilityOptions current = new();

    public static AccessibilityOptions Current => current;

    public static event Action<AccessibilityOptions> Changed;

    public static void Apply(AccessibilityOptions options)
    {
        current = (options ?? new AccessibilityOptions()).Clone();
        current.Sanitize();
        Changed?.Invoke(current);
    }

    #endregion

    #region Helpers

    public AccessibilityOptions Clone() => (AccessibilityOptions)this.MemberwiseClone();

    public void Sanitize()
    {
        this.subtitleScale = Clamp(this.subtitleScale, MinSubtitleScale, MaxSubtitleScale, 1f);
        this.subtitleBackgroundOpacity = Clamp(this.subtitleBackgroundOpacity, 0f, 1f, 0.6f);
        this.subtitleReadingSpeed = Clamp(this.subtitleReadingSpeed, 0.5f, 2f, 1f);
        this.textScale = Clamp(this.textScale, MinTextScale, MaxTextScale, 1f);
        this.uiScale = Clamp(this.uiScale, MinTextScale, MaxTextScale, 1f);
        this.cameraShake = Clamp(this.cameraShake, 0f, 1f, 1f);
        this.headBob = Clamp(this.headBob, 0f, 1f, 1f);
        this.fieldOfView = Clamp(this.fieldOfView, 50f, 110f, 70f);
        this.playerDamageTaken = Clamp(this.playerDamageTaken, 0f, 2f, 1f);
        this.survivalNeedsRate = Clamp(this.survivalNeedsRate, 0f, 2f, 1f);
    }

    public static bool ResolveHold(InputHoldMode mode, bool pressedThisFrame, bool isPressed, ref bool toggled)
    {
        if (mode == InputHoldMode.Hold)
        {
            toggled = false;
            return isPressed;
        }

        if (pressedThisFrame)
        {
            toggled = !toggled;
        }

        return toggled;
    }

    private static float Clamp(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

    #endregion
}
