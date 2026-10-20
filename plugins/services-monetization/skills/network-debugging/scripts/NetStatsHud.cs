using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NetStatsHud : MonoBehaviour
{
    private const int RttWindow = 40;
    private const int MaxClientsShown = 8;

    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private TMP_Text label;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
    [SerializeField] private bool developmentBuildsOnly = true;

    private readonly char[] _text = new char[1536];
    private readonly char[] _digits = new char[20];
    private readonly ulong[] _rttSamples = new ulong[RttWindow];

    private NetworkManager _subscribed;
    private NetworkTransport _transport;
    private string _transportName = string.Empty;
    private int _length;
    private float _nextRefresh;
    private int _rttHead;
    private int _rttFilled;
    private int _connects;
    private int _disconnects;
    private int _transportFailures;
    private bool _hasLastEvent;
    private ConnectionEvent _lastEvent;
    private ulong _lastEventClientId;

    private void Awake()
    {
        if (developmentBuildsOnly && !Debug.isDebugBuild)
        {
            if (label != null)
            {
                label.enabled = false;
            }

            enabled = false;
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        NetworkManager manager = networkManager != null ? networkManager : NetworkManager.Singleton;
        if (manager != _subscribed)
        {
            Unsubscribe();
            Subscribe(manager);
        }

        if (Time.unscaledTime < _nextRefresh || label == null)
        {
            return;
        }

        _nextRefresh = Time.unscaledTime + refreshInterval;
        Compose(manager);
        label.SetCharArray(_text, 0, _length);
    }

    private void Subscribe(NetworkManager manager)
    {
        if (manager == null)
        {
            return;
        }

        manager.OnConnectionEvent += HandleConnectionEvent;
        manager.OnTransportFailure += HandleTransportFailure;
        _subscribed = manager;
    }

    private void Unsubscribe()
    {
        if (_subscribed != null)
        {
            _subscribed.OnConnectionEvent -= HandleConnectionEvent;
            _subscribed.OnTransportFailure -= HandleTransportFailure;
        }

        _subscribed = null;
    }

    private void HandleConnectionEvent(NetworkManager manager, ConnectionEventData data)
    {
        if (data.EventType == ConnectionEvent.ClientConnected || data.EventType == ConnectionEvent.PeerConnected)
        {
            _connects++;
        }
        else
        {
            _disconnects++;
        }

        _hasLastEvent = true;
        _lastEvent = data.EventType;
        _lastEventClientId = data.ClientId;
    }

    private void HandleTransportFailure()
    {
        _transportFailures++;
    }

    private void Compose(NetworkManager manager)
    {
        _length = 0;
        if (manager == null)
        {
            Append("NGO: no NetworkManager");
            return;
        }

        Append("NGO ");
        Append(RoleOf(manager));
        if (manager.IsListening)
        {
            Append("  id ");
            AppendInt((long)manager.LocalClientId);
        }

        NetworkTransport transport = manager.NetworkConfig != null ? manager.NetworkConfig.NetworkTransport : null;
        if (transport != _transport)
        {
            _transport = transport;
            _transportName = transport != null ? transport.GetType().Name : "none";
            _rttFilled = 0;
        }

        Append("\nTransport ");
        Append(_transportName);

        if (manager.IsListening)
        {
            AppendTiming(manager);
            if (manager.IsServer)
            {
                AppendServerRtts(manager);
            }
            else if (manager.IsConnectedClient)
            {
                AppendClientRtt();
            }
        }

        Append("\nConnects ");
        AppendInt(_connects);
        Append("  disconnects ");
        AppendInt(_disconnects);
        Append("  transport failures ");
        AppendInt(_transportFailures);

        if (_hasLastEvent)
        {
            Append("\nLast event ");
            Append(EventName(_lastEvent));
            Append(" client ");
            AppendInt((long)_lastEventClientId);
        }

        string reason = manager.DisconnectReason;
        if (!string.IsNullOrEmpty(reason))
        {
            Append("\nDisconnect reason ");
            Append(reason);
        }
    }

    private void AppendTiming(NetworkManager manager)
    {
        NetworkTime local = manager.LocalTime;
        NetworkTime server = manager.ServerTime;
        Append("\nTick rate ");
        AppendInt(manager.NetworkConfig.TickRate);
        Append(" Hz  server tick ");
        AppendInt(server.Tick);
        Append("  local-server ");
        AppendInt((long)Math.Round((local.Time - server.Time) * 1000.0));
        Append(" ms");
    }

    private void AppendServerRtts(NetworkManager manager)
    {
        var ids = manager.ConnectedClientsIds;
        int remote = 0;
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] != NetworkManager.ServerClientId)
            {
                remote++;
            }
        }

        Append("\nRemote clients ");
        AppendInt(remote);

        int shown = 0;
        for (int i = 0; i < ids.Count && shown < MaxClientsShown; i++)
        {
            ulong id = ids[i];
            if (id == NetworkManager.ServerClientId)
            {
                continue;
            }

            Append("\n  client ");
            AppendInt((long)id);
            Append(" rtt ");
            AppendRtt(_transport != null ? _transport.GetCurrentRtt(id) : 0UL);
            shown++;
        }
    }

    private void AppendClientRtt()
    {
        ulong rtt = _transport != null ? _transport.GetCurrentRtt(NetworkManager.ServerClientId) : 0UL;
        _rttHead = (_rttHead + 1) % RttWindow;
        _rttSamples[_rttHead] = rtt;
        _rttFilled = Math.Min(_rttFilled + 1, RttWindow);

        ulong min = ulong.MaxValue;
        ulong max = 0;
        ulong sum = 0;
        for (int i = 0; i < _rttFilled; i++)
        {
            ulong v = _rttSamples[i];
            sum += v;
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }

        Append("\nRTT to host ");
        AppendRtt(rtt);
        if (rtt > 0)
        {
            Append("  avg ");
            AppendInt((long)(sum / (ulong)_rttFilled));
            Append("  min ");
            AppendInt((long)min);
            Append("  max ");
            AppendInt((long)max);
        }
    }

    private void AppendRtt(ulong rtt)
    {
        if (rtt == 0)
        {
            Append("n/a (transport reports 0)");
            return;
        }

        AppendInt((long)rtt);
        Append(" ms");
    }

    private static string RoleOf(NetworkManager manager)
    {
        if (!manager.IsListening)
        {
            return "offline";
        }

        if (manager.IsHost)
        {
            return "host";
        }

        if (manager.IsServer)
        {
            return "server";
        }

        return manager.IsConnectedClient ? "client" : "client (connecting)";
    }

    private static string EventName(ConnectionEvent value)
    {
        switch (value)
        {
            case ConnectionEvent.ClientConnected:
                return "ClientConnected";
            case ConnectionEvent.PeerConnected:
                return "PeerConnected";
            case ConnectionEvent.ClientDisconnected:
                return "ClientDisconnected";
            case ConnectionEvent.PeerDisconnected:
                return "PeerDisconnected";
            default:
                return "Unknown";
        }
    }

    private void Append(string value)
    {
        for (int i = 0; i < value.Length && _length < _text.Length; i++)
        {
            _text[_length++] = value[i];
        }
    }

    private void AppendChar(char c)
    {
        if (_length < _text.Length)
        {
            _text[_length++] = c;
        }
    }

    private void AppendInt(long value)
    {
        if (value < 0)
        {
            AppendChar('-');
            value = -value;
        }

        int count = 0;
        do
        {
            _digits[count++] = (char)('0' + (int)(value % 10));
            value /= 10;
        }
        while (value > 0 && count < _digits.Length);

        while (count > 0)
        {
            AppendChar(_digits[--count]);
        }
    }
}
