using System;
using UnityEngine;

public enum WeatherType : byte
{
    Clear,
    Overcast,
    Rain,
    Storm,
    Snow
}

[Serializable]
public struct WeatherSample
{
    public float sunMultiplier;
    public float fogDensityMultiplier;
    public Color fogTint;
    public float fogTintAmount;
    public float rain;
    public float snow;
    public float wind;
    public float wetness;
    public float snowCover;

    public static WeatherSample Neutral => new()
    {
        sunMultiplier = 1f,
        fogDensityMultiplier = 1f,
        fogTint = Color.gray,
        fogTintAmount = 0f
    };

    public static WeatherSample Lerp(in WeatherSample a, in WeatherSample b, float t) => new()
    {
        sunMultiplier = Mathf.Lerp(a.sunMultiplier, b.sunMultiplier, t),
        fogDensityMultiplier = Mathf.Lerp(a.fogDensityMultiplier, b.fogDensityMultiplier, t),
        fogTint = Color.Lerp(a.fogTint, b.fogTint, t),
        fogTintAmount = Mathf.Lerp(a.fogTintAmount, b.fogTintAmount, t),
        rain = Mathf.Lerp(a.rain, b.rain, t),
        snow = Mathf.Lerp(a.snow, b.snow, t),
        wind = Mathf.Lerp(a.wind, b.wind, t),
        wetness = Mathf.Lerp(a.wetness, b.wetness, t),
        snowCover = Mathf.Lerp(a.snowCover, b.snowCover, t)
    };
}

[Serializable]
public struct WeatherTransitionWeight
{
    public WeatherType to;
    [Min(0f)] public float weight;
}

[Serializable]
public sealed class WeatherPreset
{
    public WeatherType type;
    public WeatherSample sample = WeatherSample.Neutral;
    [Min(1f)] public float minDurationSeconds = 120f;
    [Min(1f)] public float maxDurationSeconds = 300f;
    [Min(0.1f)] public float transitionSeconds = 30f;
    public WeatherTransitionWeight[] next = Array.Empty<WeatherTransitionWeight>();
}

[Serializable]
public struct WeatherSnapshot : IEquatable<WeatherSnapshot>
{
    public WeatherType from;
    public WeatherType to;
    public double transitionStart;
    public float transitionSeconds;
    public double nextChangeAt;

    public bool Equals(WeatherSnapshot other) =>
        this.from == other.from && this.to == other.to && this.transitionStart.Equals(other.transitionStart) &&
        this.transitionSeconds.Equals(other.transitionSeconds) && this.nextChangeAt.Equals(other.nextChangeAt);

    public override bool Equals(object obj) => obj is WeatherSnapshot other && this.Equals(other);

    public override int GetHashCode() => HashCode.Combine((int)this.from, (int)this.to, this.transitionStart, this.transitionSeconds, this.nextChangeAt);
}

[Serializable]
public struct WeatherSaveData
{
    public WeatherType current;
    public float secondsUntilNextChange;
}

public sealed class WeatherController : MonoBehaviour
{
    #region Fields

    private static readonly int WetnessId = Shader.PropertyToID("_GlobalWetness");
    private static readonly int SnowCoverId = Shader.PropertyToID("_GlobalSnowCover");
    private static readonly int WindId = Shader.PropertyToID("_GlobalWind");

    [SerializeField] private TimeOfDay clock;
    [SerializeField] private WeatherPreset[] presets = Array.Empty<WeatherPreset>();
    [SerializeField] private WeatherType initial = WeatherType.Clear;
    [SerializeField, Min(1f)] private float surfaceLagFactor = 4f;
    [SerializeField] private int seed;

    [Header("Particle System precipitation (optional)")]
    [SerializeField] private ParticleSystem rainParticles;
    [SerializeField, Min(0f)] private float rainMaxRate = 2000f;
    [SerializeField] private ParticleSystem snowParticles;
    [SerializeField, Min(0f)] private float snowMaxRate = 800f;

    private readonly WeatherPreset[] _byType = new WeatherPreset[Enum.GetValues(typeof(WeatherType)).Length];
    private System.Random _random;
    private WeatherSnapshot _snapshot;
    private WeatherSample _current = WeatherSample.Neutral;
    private float _lastWetness = -1f;
    private float _lastSnowCover = -1f;
    private float _lastWind = -1f;

    #endregion

    #region Events

    public event Action<WeatherSnapshot> SnapshotChanged;

    #endregion

    #region Properties

    public bool IsAuthority { get; set; } = true;
    public WeatherSnapshot Snapshot => this._snapshot;
    public WeatherSample Current => this._current;
    public WeatherType CurrentType => this._snapshot.to;
    public float TransitionProgress => this.Progress(this.Now, this._snapshot.transitionSeconds);

    private double Now => this.clock != null ? this.clock.ClockNow : Time.timeAsDouble;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        foreach (var preset in this.presets)
        {
            if (preset != null)
            {
                this._byType[(int)preset.type] = preset;
            }
        }

        this._random = this.seed != 0 ? new System.Random(this.seed) : new System.Random();
        var now = this.Now;
        this._snapshot = new WeatherSnapshot
        {
            from = this.initial,
            to = this.initial,
            transitionStart = now,
            transitionSeconds = 0.1f,
            nextChangeAt = now + this.RollDuration(this.initial)
        };
    }

    private void Update()
    {
        var now = this.Now;
        if (this.IsAuthority && now >= this._snapshot.nextChangeAt)
        {
            this.TransitionTo(this.PickNext(this._snapshot.to));
        }

        this.Evaluate(now);
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(WetnessId, 0f);
        Shader.SetGlobalFloat(SnowCoverId, 0f);
        Shader.SetGlobalFloat(WindId, 0f);
        this._lastWetness = this._lastSnowCover = this._lastWind = -1f;
    }

    #endregion

    #region Public API

    public void TransitionTo(WeatherType target)
    {
        var now = this.Now;
        var preset = this.Get(target);
        var settled = this.Progress(now, this._snapshot.transitionSeconds) >= 1f ? this._snapshot.to : this._snapshot.from;
        this.ApplySnapshot(new WeatherSnapshot
        {
            from = settled,
            to = target,
            transitionStart = now,
            transitionSeconds = preset != null ? preset.transitionSeconds : 30f,
            nextChangeAt = now + this.RollDuration(target)
        }, true);
    }

    public void ApplySnapshot(WeatherSnapshot snapshot, bool notify)
    {
        this._snapshot = snapshot;
        this.Evaluate(this.Now);
        if (notify)
        {
            this.SnapshotChanged?.Invoke(snapshot);
        }
    }

    public WeatherSaveData CaptureSave() => new()
    {
        current = this._snapshot.to,
        secondsUntilNextChange = (float)Math.Max(0.0, this._snapshot.nextChangeAt - this.Now)
    };

    public void RestoreSave(WeatherSaveData data)
    {
        var now = this.Now;
        this.ApplySnapshot(new WeatherSnapshot
        {
            from = data.current,
            to = data.current,
            transitionStart = now,
            transitionSeconds = 0.1f,
            nextChangeAt = now + Math.Max(1f, data.secondsUntilNextChange)
        }, true);
    }

    #endregion

    #region Evaluation

    private void Evaluate(double now)
    {
        var from = this.Get(this._snapshot.from);
        var to = this.Get(this._snapshot.to);
        var a = from != null ? from.sample : WeatherSample.Neutral;
        var b = to != null ? to.sample : WeatherSample.Neutral;

        var t = Mathf.SmoothStep(0f, 1f, this.Progress(now, this._snapshot.transitionSeconds));
        var surfaceT = this.Progress(now, this._snapshot.transitionSeconds * this.surfaceLagFactor);

        this._current = WeatherSample.Lerp(a, b, t);
        this._current.wetness = Mathf.Lerp(a.wetness, b.wetness, surfaceT);
        this._current.snowCover = Mathf.Lerp(a.snowCover, b.snowCover, surfaceT);

        SetGlobalIfChanged(WetnessId, this._current.wetness, ref this._lastWetness);
        SetGlobalIfChanged(SnowCoverId, this._current.snowCover, ref this._lastSnowCover);
        SetGlobalIfChanged(WindId, this._current.wind, ref this._lastWind);

        DriveParticles(this.rainParticles, this._current.rain * this.rainMaxRate);
        DriveParticles(this.snowParticles, this._current.snow * this.snowMaxRate);
    }

    private float Progress(double now, float seconds) =>
        Mathf.Clamp01((float)((now - this._snapshot.transitionStart) / Math.Max(0.01, seconds)));

    private static void SetGlobalIfChanged(int id, float value, ref float last)
    {
        if (Mathf.Abs(value - last) < 0.001f)
        {
            return;
        }

        last = value;
        Shader.SetGlobalFloat(id, value);
    }

    private static void DriveParticles(ParticleSystem system, float rate)
    {
        if (system == null)
        {
            return;
        }

        var emission = system.emission;
        emission.rateOverTimeMultiplier = rate;

        if (rate > 0.01f)
        {
            if (!system.isPlaying)
            {
                system.Play(true);
            }
        }
        else if (system.isPlaying)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    #endregion

    #region Scheduling

    private WeatherPreset Get(WeatherType type)
    {
        var index = (int)type;
        return index >= 0 && index < this._byType.Length ? this._byType[index] : null;
    }

    private float RollDuration(WeatherType type)
    {
        var preset = this.Get(type);
        if (preset == null)
        {
            return 300f;
        }

        var min = Mathf.Min(preset.minDurationSeconds, preset.maxDurationSeconds);
        var max = Mathf.Max(preset.minDurationSeconds, preset.maxDurationSeconds);
        return Mathf.Lerp(min, max, (float)this._random.NextDouble());
    }

    private WeatherType PickNext(WeatherType from)
    {
        var preset = this.Get(from);
        if (preset == null || preset.next.Length == 0)
        {
            return from;
        }

        var total = 0f;
        foreach (var option in preset.next)
        {
            total += option.weight;
        }

        if (total <= 0f)
        {
            return from;
        }

        var roll = (float)this._random.NextDouble() * total;
        foreach (var option in preset.next)
        {
            roll -= option.weight;
            if (roll <= 0f)
            {
                return option.to;
            }
        }

        return preset.next[preset.next.Length - 1].to;
    }

    #endregion
}
