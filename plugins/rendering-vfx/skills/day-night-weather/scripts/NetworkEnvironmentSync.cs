using System;
using Unity.Netcode;
using UnityEngine;

public struct NetTimeAnchor : INetworkSerializable, IEquatable<NetTimeAnchor>
{
    public TimeAnchor value;

    public NetTimeAnchor(TimeAnchor value) => this.value = value;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref this.value.clockTime);
        serializer.SerializeValue(ref this.value.totalDays);
        serializer.SerializeValue(ref this.value.daysPerSecond);
    }

    public bool Equals(NetTimeAnchor other) => this.value.Equals(other.value);
}

public struct NetWeatherSnapshot : INetworkSerializable, IEquatable<NetWeatherSnapshot>
{
    public WeatherSnapshot value;

    public NetWeatherSnapshot(WeatherSnapshot value) => this.value = value;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        var from = (byte)this.value.from;
        var to = (byte)this.value.to;
        serializer.SerializeValue(ref from);
        serializer.SerializeValue(ref to);
        serializer.SerializeValue(ref this.value.transitionStart);
        serializer.SerializeValue(ref this.value.transitionSeconds);
        serializer.SerializeValue(ref this.value.nextChangeAt);
        this.value.from = (WeatherType)from;
        this.value.to = (WeatherType)to;
    }

    public bool Equals(NetWeatherSnapshot other) => this.value.Equals(other.value);
}

public sealed class NetworkEnvironmentSync : NetworkBehaviour
{
    #region Fields

    [SerializeField] private TimeOfDay timeOfDay;
    [SerializeField] private WeatherController weather;

    private readonly NetworkVariable<NetTimeAnchor> _time = new();
    private readonly NetworkVariable<NetWeatherSnapshot> _weather = new();

    private Func<double> _serverClock;

    #endregion

    #region Unity Lifecycle

    private void Awake() => this._serverClock = this.ReadServerTime;

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        if (this.IsServer)
        {
            var weatherSave = this.weather.CaptureSave();
            this.timeOfDay.SetClockSource(this._serverClock);
            this.weather.IsAuthority = true;
            this.timeOfDay.AnchorChanged += this.OnServerAnchorChanged;
            this.weather.SnapshotChanged += this.OnServerSnapshotChanged;
            this.weather.RestoreSave(weatherSave);
            this._time.Value = new NetTimeAnchor(this.timeOfDay.Anchor);
            return;
        }

        this.weather.IsAuthority = false;
        this.timeOfDay.SetClockSource(this._serverClock);
        this.timeOfDay.ApplyAnchor(this._time.Value.value, false);
        this.weather.ApplySnapshot(this._weather.Value.value, false);
        this._time.OnValueChanged += this.OnClientTimeChanged;
        this._weather.OnValueChanged += this.OnClientWeatherChanged;
    }

    public override void OnNetworkDespawn()
    {
        this.timeOfDay.AnchorChanged -= this.OnServerAnchorChanged;
        this.weather.SnapshotChanged -= this.OnServerSnapshotChanged;
        this._time.OnValueChanged -= this.OnClientTimeChanged;
        this._weather.OnValueChanged -= this.OnClientWeatherChanged;

        var weatherSave = this.weather.CaptureSave();
        this.timeOfDay.SetClockSource(null);
        this.weather.IsAuthority = true;
        this.weather.RestoreSave(weatherSave);
    }

    #endregion

    #region Sync

    private double ReadServerTime()
    {
        var manager = this.NetworkManager;
        return manager != null && manager.IsListening ? manager.ServerTime.Time : Time.timeAsDouble;
    }

    private void OnServerAnchorChanged(TimeAnchor anchor) => this._time.Value = new NetTimeAnchor(anchor);

    private void OnServerSnapshotChanged(WeatherSnapshot snapshot) => this._weather.Value = new NetWeatherSnapshot(snapshot);

    private void OnClientTimeChanged(NetTimeAnchor previous, NetTimeAnchor current) => this.timeOfDay.ApplyAnchor(current.value, false);

    private void OnClientWeatherChanged(NetWeatherSnapshot previous, NetWeatherSnapshot current) => this.weather.ApplySnapshot(current.value, false);

    #endregion
}
