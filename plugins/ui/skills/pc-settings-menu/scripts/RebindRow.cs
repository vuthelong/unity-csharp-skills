using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class RebindRow : MonoBehaviour
{
    #region Fields

    [Header("Binding")]
    [SerializeField] private InputActionReference actionReference;
    [SerializeField] private string bindingId;
    [SerializeField] private string cancelPath = "<Keyboard>/escape";
    [SerializeField] private string[] excludedPaths = Array.Empty<string>();
    [SerializeField] private bool rejectDuplicates = true;

    [Header("UI")]
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text bindingLabel;
    [SerializeField] private Button rebindButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private GameObject waitingIndicator;
    [SerializeField] private string waitingText = "Press a key...";

    private InputActionRebindingExtensions.RebindingOperation _operation;
    private bool _actionWasEnabled;
    private bool _rebindingComposite;
    private string _previousOverridePath;

    #endregion

    #region Events

    public event Action<RebindRow> RebindStarted;
    public event Action<RebindRow> RebindFinished;
    public event Action<RebindRow, string> RebindRejected;

    #endregion

    #region Properties

    public InputAction Action => this.actionReference != null ? this.actionReference.action : null;
    public bool IsRebinding => this._operation != null;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (this.rebindButton != null) this.rebindButton.onClick.AddListener(StartRebind);
        if (this.resetButton != null) this.resetButton.onClick.AddListener(ResetToDefault);
        SetWaiting(false);
        Refresh();
    }

    private void OnDisable()
    {
        if (this.rebindButton != null) this.rebindButton.onClick.RemoveListener(StartRebind);
        if (this.resetButton != null) this.resetButton.onClick.RemoveListener(ResetToDefault);
        CancelRebind();
    }

    #endregion

    #region Public API

    public void Refresh()
    {
        var action = this.Action;
        if (action == null) return;

        if (this.actionLabel != null) this.actionLabel.text = action.name;
        if (this.bindingLabel == null) return;

        var index = FindBindingIndex(action);
        this.bindingLabel.text = index >= 0 ? action.GetBindingDisplayString(index) : string.Empty;
    }

    public void StartRebind()
    {
        var action = this.Action;
        if (action == null || this.IsRebinding) return;

        var index = FindBindingIndex(action);
        if (index < 0)
        {
            Debug.LogError($"{nameof(RebindRow)}: binding '{this.bindingId}' not found on '{action.name}'", this);
            return;
        }

        this._actionWasEnabled = action.enabled;
        action.Disable();

        this._rebindingComposite = action.bindings[index].isComposite;
        StartOperation(action, this._rebindingComposite ? index + 1 : index);
        RebindStarted?.Invoke(this);
    }

    public void CancelRebind()
    {
        this._operation?.Cancel();
    }

    public void ResetToDefault()
    {
        var action = this.Action;
        if (action == null || this.IsRebinding) return;

        var index = FindBindingIndex(action);
        if (index < 0) return;

        action.RemoveBindingOverride(index);
        if (action.bindings[index].isComposite)
        {
            for (var part = index + 1; part < action.bindings.Count && action.bindings[part].isPartOfComposite; part++)
            {
                action.RemoveBindingOverride(part);
            }
        }

        Refresh();
        RebindFinished?.Invoke(this);
    }

    #endregion

    #region Rebinding

    private void StartOperation(InputAction action, int bindingIndex)
    {
        this._previousOverridePath = action.bindings[bindingIndex].overridePath;
        SetWaiting(true);

        var operation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough(this.cancelPath)
            .OnMatchWaitForAnother(0.1f)
            .OnCancel(_ => Finish(action))
            .OnComplete(_ => OnOperationComplete(action, bindingIndex));

        foreach (var path in this.excludedPaths)
        {
            if (!string.IsNullOrEmpty(path)) operation.WithControlsExcluding(path);
        }

        this._operation = operation;
        operation.Start();
    }

    private void OnOperationComplete(InputAction action, int bindingIndex)
    {
        DisposeOperation();

        if (this.rejectDuplicates && TryFindDuplicate(action, bindingIndex, out var duplicate))
        {
            RestorePrevious(action, bindingIndex);
            RebindRejected?.Invoke(this, duplicate);
            StartOperation(action, bindingIndex);
            return;
        }

        var next = bindingIndex + 1;
        if (this._rebindingComposite && next < action.bindings.Count && action.bindings[next].isPartOfComposite)
        {
            StartOperation(action, next);
            return;
        }

        Finish(action);
    }

    private void Finish(InputAction action)
    {
        DisposeOperation();
        this._rebindingComposite = false;
        SetWaiting(false);
        if (this._actionWasEnabled) action.Enable();
        Refresh();
        RebindFinished?.Invoke(this);
    }

    private void DisposeOperation()
    {
        var operation = this._operation;
        this._operation = null;
        operation?.Dispose();
    }

    private void RestorePrevious(InputAction action, int bindingIndex)
    {
        if (string.IsNullOrEmpty(this._previousOverridePath))
        {
            action.RemoveBindingOverride(bindingIndex);
            return;
        }

        action.ApplyBindingOverride(bindingIndex, this._previousOverridePath);
    }

    private static bool TryFindDuplicate(InputAction action, int bindingIndex, out string displayName)
    {
        displayName = null;
        var newBinding = action.bindings[bindingIndex];
        var path = newBinding.effectivePath;
        if (string.IsNullOrEmpty(path) || action.actionMap == null) return false;

        foreach (var other in action.actionMap.actions)
        {
            var bindings = other.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                if (other == action && i == bindingIndex) continue;
                if (bindings[i].isComposite) continue;
                if (!string.Equals(bindings[i].effectivePath, path, StringComparison.OrdinalIgnoreCase)) continue;

                displayName = $"{other.name}: {other.GetBindingDisplayString(i)}";
                return true;
            }
        }

        return false;
    }

    private int FindBindingIndex(InputAction action)
    {
        if (string.IsNullOrEmpty(this.bindingId)) return -1;

        var bindings = action.bindings;
        for (var i = 0; i < bindings.Count; i++)
        {
            if (string.Equals(bindings[i].id.ToString(), this.bindingId, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    private void SetWaiting(bool waiting)
    {
        if (this.waitingIndicator != null) this.waitingIndicator.SetActive(waiting);
        if (waiting && this.bindingLabel != null) this.bindingLabel.text = this.waitingText;
        if (this.rebindButton != null) this.rebindButton.interactable = !waiting;
        if (this.resetButton != null) this.resetButton.interactable = !waiting;
    }

    #endregion
}
