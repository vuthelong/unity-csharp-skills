using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PauseService : MonoBehaviour
{
    #region Fields

    [SerializeField] private bool pauseOnFocusLoss = true;

    private readonly HashSet<object> _reasons = new();
    private readonly object _focusReason = new();
    private bool _simulationPauseAllowed = true;
    private bool _simulationFrozen;
    private float _timeScaleBeforePause = 1f;

    #endregion

    #region Properties

    public bool IsPaused => this._reasons.Count > 0;
    public bool IsSimulationFrozen => this._simulationFrozen;

    public bool SimulationPauseAllowed
    {
        get => this._simulationPauseAllowed;
        set
        {
            if (this._simulationPauseAllowed == value) return;

            this._simulationPauseAllowed = value;
            this.Apply();
        }
    }

    public event Action<bool> PausedChanged;

    #endregion

    #region Unity Lifecycle

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!this.pauseOnFocusLoss) return;

        if (hasFocus) this.Release(this._focusReason);
        else this.Acquire(this._focusReason);
    }

    private void OnDestroy()
    {
        this._reasons.Clear();
        this.Apply();
    }

    #endregion

    #region Pause

    public void Acquire(object reason)
    {
        if (reason == null) throw new ArgumentNullException(nameof(reason));

        var wasPaused = this.IsPaused;
        if (!this._reasons.Add(reason)) return;

        this.Apply();
        if (!wasPaused) this.PausedChanged?.Invoke(true);
    }

    public void Release(object reason)
    {
        if (reason == null || !this._reasons.Remove(reason)) return;

        this.Apply();
        if (!this.IsPaused) this.PausedChanged?.Invoke(false);
    }

    public void Set(object reason, bool paused)
    {
        if (paused) this.Acquire(reason);
        else this.Release(reason);
    }

    private void Apply()
    {
        var freeze = this.IsPaused && this._simulationPauseAllowed;
        if (freeze == this._simulationFrozen) return;

        this._simulationFrozen = freeze;
        if (freeze)
        {
            this._timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }
        else
        {
            Time.timeScale = this._timeScaleBeforePause;
            AudioListener.pause = false;
        }
    }

    #endregion
}
