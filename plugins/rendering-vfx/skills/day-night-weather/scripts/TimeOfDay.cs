using System;
using UnityEngine;

public enum DayPhase
{
    Night,
    Day,
    Evening
}

[Serializable]
public struct TimeAnchor : IEquatable<TimeAnchor>
{
    public double clockTime;
    public double totalDays;
    public double daysPerSecond;

    public TimeAnchor(double clockTime, double totalDays, double daysPerSecond)
    {
        this.clockTime = clockTime;
        this.totalDays = totalDays;
        this.daysPerSecond = daysPerSecond;
    }

    public double Evaluate(double now) => this.totalDays + (now - this.clockTime) * this.daysPerSecond;

    public bool Equals(TimeAnchor other) =>
        this.clockTime.Equals(other.clockTime) && this.totalDays.Equals(other.totalDays) && this.daysPerSecond.Equals(other.daysPerSecond);

    public override bool Equals(object obj) => obj is TimeAnchor other && this.Equals(other);

    public override int GetHashCode() => HashCode.Combine(this.clockTime, this.totalDays, this.daysPerSecond);
}

[DefaultExecutionOrder(-100)]
public sealed class TimeOfDay : MonoBehaviour
{
    #region Fields

    [SerializeField, Min(0.1f)] private float dayLengthMinutes = 24f;
    [SerializeField, Min(0f)] private float timeScale = 1f;
    [SerializeField, Range(0f, 1f)] private float startTime01 = 0.3f;
    [SerializeField, Range(0f, 1f)] private float sunrise01 = 0.25f;
    [SerializeField, Range(0f, 1f)] private float sunset01 = 0.75f;

    private static readonly Func<double> LocalClock = () => Time.timeAsDouble;

    private Func<double> _clock = LocalClock;
    private TimeAnchor _anchor;
    private double _totalDays;
    private long _phaseKey = long.MinValue;

    #endregion

    #region Events

    public event Action<int> NewDay;
    public event Action Sunrise;
    public event Action Sunset;
    public event Action<TimeAnchor> AnchorChanged;

    #endregion

    #region Properties

    public double ClockNow => this._clock();
    public TimeAnchor Anchor => this._anchor;
    public double TotalDays => this._totalDays;
    public int Day => (int)Math.Floor(this._totalDays);
    public float Normalized => (float)(this._totalDays - Math.Floor(this._totalDays));
    public float Hours => this.Normalized * 24f;
    public float Sunrise01 => this.sunrise01;
    public float Sunset01 => this.sunset01;
    public float TimeScale => this.timeScale;
    public float DayLengthMinutes => this.dayLengthMinutes;
    public bool IsDaytime => this.PhaseOf(this.Normalized) == DayPhase.Day;
    public double DaysPerSecond => this.timeScale / (this.dayLengthMinutes * 60.0);

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._anchor = new TimeAnchor(this._clock(), this.startTime01, this.DaysPerSecond);
        this.Evaluate(false);
    }

    private void Update() => this.Evaluate(true);

    private void OnValidate()
    {
        if (this.sunset01 < this.sunrise01)
        {
            this.sunset01 = this.sunrise01;
        }
    }

    #endregion

    #region Public API

    public void SetClockSource(Func<double> clock)
    {
        var days = this._anchor.Evaluate(this._clock());
        this._clock = clock ?? LocalClock;
        this.ApplyAnchor(new TimeAnchor(this._clock(), days, this._anchor.daysPerSecond), false);
    }

    public void SetTotalDays(double totalDays)
    {
        this.ApplyAnchor(new TimeAnchor(this._clock(), Math.Max(0.0, totalDays), this.DaysPerSecond), true);
    }

    public void SetTimeOfDay(float normalized, bool advanceToNextDayIfPast)
    {
        var day = Math.Floor(this._totalDays);
        if (advanceToNextDayIfPast && normalized <= this.Normalized)
        {
            day += 1.0;
        }

        this.SetTotalDays(day + Mathf.Repeat(normalized, 1f));
    }

    public void SetTimeScale(float scale)
    {
        this.timeScale = Mathf.Max(0f, scale);
        var now = this._clock();
        this.ApplyAnchor(new TimeAnchor(now, this._anchor.Evaluate(now), this.DaysPerSecond), true);
    }

    public void ApplyAnchor(TimeAnchor anchor, bool notify)
    {
        this._anchor = anchor;
        this.Evaluate(false);
        if (notify)
        {
            this.AnchorChanged?.Invoke(anchor);
        }
    }

    public DayPhase PhaseOf(float normalized)
    {
        if (normalized < this.sunrise01)
        {
            return DayPhase.Night;
        }

        return normalized < this.sunset01 ? DayPhase.Day : DayPhase.Evening;
    }

    #endregion

    #region Evaluation

    private void Evaluate(bool raiseEvents)
    {
        this._totalDays = Math.Max(0.0, this._anchor.Evaluate(this._clock()));
        var day = (long)Math.Floor(this._totalDays);
        var phase = this.PhaseOf(this.Normalized);
        var key = day * 3 + (long)phase;
        var previous = this._phaseKey;
        this._phaseKey = key;

        if (!raiseEvents || previous == long.MinValue || key <= previous)
        {
            return;
        }

        if (day != previous / 3)
        {
            this.NewDay?.Invoke((int)day);
        }

        if (phase == DayPhase.Day)
        {
            this.Sunrise?.Invoke();
        }
        else if (phase == DayPhase.Evening)
        {
            this.Sunset?.Invoke();
        }
    }

    #endregion
}
