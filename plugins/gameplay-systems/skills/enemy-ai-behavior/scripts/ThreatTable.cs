using System;
using UnityEngine;

public class ThreatTable : MonoBehaviour
{
    #region Fields

    [SerializeField] private int capacity = 8;
    [SerializeField] private float decayPerSecond = 1f;
    [SerializeField] private float distanceFalloff = 0.05f;
    [SerializeField, Range(0f, 2f)] private float retargetMargin = 0.25f;
    [SerializeField] private float minRetargetInterval = 2f;
    [SerializeField] private float maxThreat = 1000f;

    private PerceptionTarget[] _targets;
    private float[] _threat;
    private int _count;
    private PerceptionTarget _current;
    private float _lastSwitchTime = float.NegativeInfinity;

    public event Action<PerceptionTarget, PerceptionTarget> TargetChanged;

    #endregion

    #region Properties

    public PerceptionTarget CurrentTarget => this._current;
    public int Count => this._count;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        var size = Mathf.Max(1, this.capacity);
        this._targets = new PerceptionTarget[size];
        this._threat = new float[size];
    }

    private void OnDisable() => Clear();

    #endregion

    #region Threat

    public void AddThreat(PerceptionTarget target, float amount)
    {
        if (target == null || !target.IsTargetable || amount <= 0f) return;

        var index = IndexOf(target);
        if (index < 0) index = AllocateSlot(target);
        this._threat[index] = Mathf.Min(this.maxThreat, this._threat[index] + amount);
    }

    public float GetThreat(PerceptionTarget target)
    {
        var index = IndexOf(target);
        return index >= 0 ? this._threat[index] : 0f;
    }

    public void Remove(PerceptionTarget target)
    {
        var index = IndexOf(target);
        if (index < 0) return;

        RemoveAt(index);
        if (target == this._current) SetCurrent(null, Time.time);
    }

    public void Clear()
    {
        for (var i = 0; i < this._count; i++) this._targets[i] = null;
        this._count = 0;
        SetCurrent(null, Time.time);
    }

    private int IndexOf(PerceptionTarget target)
    {
        for (var i = 0; i < this._count; i++)
        {
            if (this._targets[i] == target) return i;
        }

        return -1;
    }

    private int AllocateSlot(PerceptionTarget target)
    {
        var index = this._count;
        if (this._count < this._targets.Length)
        {
            this._count++;
        }
        else
        {
            index = 0;
            for (var i = 1; i < this._count; i++)
            {
                if (this._threat[i] < this._threat[index]) index = i;
            }
        }

        this._targets[index] = target;
        this._threat[index] = 0f;
        return index;
    }

    private void RemoveAt(int index)
    {
        var last = this._count - 1;
        this._targets[index] = this._targets[last];
        this._threat[index] = this._threat[last];
        this._targets[last] = null;
        this._count = last;
    }

    #endregion

    #region Target Selection

    public void Tick(float deltaTime, Vector3 position, float now)
    {
        for (var i = this._count - 1; i >= 0; i--)
        {
            var target = this._targets[i];
            this._threat[i] = Mathf.Max(0f, this._threat[i] - this.decayPerSecond * deltaTime);
            var invalid = target == null || !target.isActiveAndEnabled || !target.IsTargetable;
            if (invalid || this._threat[i] <= 0f) RemoveAt(i);
        }

        var bestIndex = -1;
        var bestScore = 0f;
        var currentScore = 0f;

        for (var i = 0; i < this._count; i++)
        {
            var target = this._targets[i];
            var score = Score(i, position);
            if (target == this._current) currentScore = score;
            if (score <= bestScore) continue;

            bestScore = score;
            bestIndex = i;
        }

        var best = bestIndex >= 0 ? this._targets[bestIndex] : null;
        if (best == this._current) return;

        var currentValid = this._current != null && IndexOf(this._current) >= 0;
        if (currentValid && best != null)
        {
            if (now - this._lastSwitchTime < this.minRetargetInterval) return;
            if (bestScore < currentScore * (1f + this.retargetMargin)) return;
        }

        SetCurrent(best, now);
    }

    private float Score(int index, Vector3 position)
    {
        var distance = Vector3.Distance(this._targets[index].transform.position, position);
        return this._threat[index] / (1f + distance * this.distanceFalloff);
    }

    private void SetCurrent(PerceptionTarget target, float now)
    {
        if (target == this._current) return;

        var previous = this._current;
        this._current = target;
        this._lastSwitchTime = now;
        TargetChanged?.Invoke(previous, target);
    }

    #endregion
}
