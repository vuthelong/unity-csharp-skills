using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable]
public struct InputGlyph
{
    public string controlPath;
    public Sprite sprite;
}

public sealed class InteractionPrompt : MonoBehaviour
{
    #region Fields

    [Header("Source")]
    [SerializeField] private Interactor interactor;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private Camera worldCamera;

    [Header("Widgets")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform anchor;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField] private Image glyphImage;
    [SerializeField] private Image holdFill;
    [SerializeField] private float unavailableAlpha = 0.5f;

    [Header("Placement")]
    [SerializeField] private bool followTarget = true;
    [SerializeField] private Vector3 worldOffset = new(0f, 0.3f, 0f);

    [Header("Glyphs")]
    [SerializeField] private string keyboardMouseGroup = "Keyboard&Mouse";
    [SerializeField] private string gamepadGroup = "Gamepad";
    [SerializeField] private InputGlyph[] gamepadGlyphs = Array.Empty<InputGlyph>();

    private bool _usingGamepad;
    private bool _glyphDirty = true;
    private string _shownPrompt;

    #endregion

    #region Properties

    public bool UsingGamepad => this._usingGamepad;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (this.worldCamera == null) this.worldCamera = Camera.main;
        InputSystem.onActionChange += this.OnActionChange;
        if (this.interactor == null) return;

        this.interactor.FocusChanged += this.OnFocusChanged;
        this.interactor.HoldProgressChanged += this.OnHoldProgressChanged;
        this.OnFocusChanged(this.interactor.Focus);
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= this.OnActionChange;
        if (this.interactor == null) return;

        this.interactor.FocusChanged -= this.OnFocusChanged;
        this.interactor.HoldProgressChanged -= this.OnHoldProgressChanged;
    }

    private void LateUpdate()
    {
        var focus = this.interactor != null ? this.interactor.Focus : null;
        if (focus == null) return;

        if (this._glyphDirty) this.RefreshGlyph();
        this.group.alpha = this.interactor.FocusAvailable && !this.interactor.IsAwaitingResult ? 1f : this.unavailableAlpha;
        this.SetPromptText(focus.Prompt);

        if (!this.followTarget || this.worldCamera == null || this.anchor == null) return;

        var screen = this.worldCamera.WorldToScreenPoint(focus.InteractionPoint + this.worldOffset);
        var visible = screen.z > 0f;
        this.anchor.gameObject.SetActive(visible);
        if (visible) this.anchor.position = screen;
    }

    #endregion

    #region Glyphs

    public void SetGamepadGlyphs(InputGlyph[] glyphs)
    {
        this.gamepadGlyphs = glyphs ?? Array.Empty<InputGlyph>();
        this._glyphDirty = true;
    }

    private void OnActionChange(object source, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
        {
            this._glyphDirty = true;
            return;
        }

        if (change != InputActionChange.ActionPerformed || source is not InputAction action) return;

        var device = action.activeControl?.device;
        if (device == null) return;

        var gamepad = device is Gamepad;
        if (!gamepad && !(device is Keyboard) && !(device is Mouse)) return;
        if (gamepad == this._usingGamepad) return;

        this._usingGamepad = gamepad;
        this._glyphDirty = true;
    }

    private void RefreshGlyph()
    {
        this._glyphDirty = false;
        var action = this.interactAction != null ? this.interactAction.action : null;
        if (action == null) return;

        var group = this._usingGamepad ? this.gamepadGroup : this.keyboardMouseGroup;
        var bindingIndex = action.GetBindingIndex(InputBinding.MaskByGroup(group));
        if (bindingIndex < 0) bindingIndex = action.GetBindingIndex();

        var display = string.Empty;
        string controlPath = null;
        if (bindingIndex >= 0) display = action.GetBindingDisplayString(bindingIndex, out _, out controlPath);
        var sprite = this._usingGamepad ? this.FindGlyph(controlPath) : null;

        if (this.glyphImage != null)
        {
            this.glyphImage.sprite = sprite;
            this.glyphImage.enabled = sprite != null;
        }

        if (this.keyLabel != null)
        {
            this.keyLabel.text = display;
            this.keyLabel.enabled = sprite == null;
        }
    }

    private Sprite FindGlyph(string controlPath)
    {
        if (string.IsNullOrEmpty(controlPath)) return null;

        foreach (var glyph in this.gamepadGlyphs)
        {
            if (string.Equals(glyph.controlPath, controlPath, StringComparison.OrdinalIgnoreCase)) return glyph.sprite;
        }

        return null;
    }

    #endregion

    #region Interactor Events

    private void OnFocusChanged(IInteractable focus)
    {
        var visible = focus != null;
        this.group.alpha = visible ? 1f : 0f;
        this.group.blocksRaycasts = false;
        this.OnHoldProgressChanged(0f);
        if (!visible) return;

        this._glyphDirty = true;
        this.SetPromptText(focus.Prompt);
    }

    private void SetPromptText(string prompt)
    {
        if (string.Equals(prompt, this._shownPrompt, StringComparison.Ordinal)) return;

        this._shownPrompt = prompt;
        this.actionLabel.text = prompt;
    }

    private void OnHoldProgressChanged(float progress)
    {
        if (this.holdFill == null) return;

        var showFill = this.interactor != null && this.interactor.Focus != null && this.interactor.Focus.HoldDuration > 0f;
        this.holdFill.enabled = showFill;
        this.holdFill.fillAmount = progress;
    }

    #endregion
}
