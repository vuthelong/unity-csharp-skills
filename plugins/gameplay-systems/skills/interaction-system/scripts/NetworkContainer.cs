using System;
using Unity.Netcode;
using UnityEngine;

public sealed class NetworkContainer : NetworkInteractable
{
    #region Fields

    public const ulong NoUser = ulong.MaxValue;

    [SerializeField] private string inUsePrompt = "In use";
    [SerializeField] private float autoCloseDistance = 4f;

    private readonly NetworkVariable<ulong> _user = new(NoUser);
    private Transform _userBody;

    #endregion

    #region Properties

    public ulong User => this._user.Value;
    public bool IsInUse => this._user.Value != NoUser;
    public bool IsOpenLocally => IsSpawned && this._user.Value == NetworkManager.LocalClientId;

    public override string Prompt => this.IsInUse && !this.IsOpenLocally ? this.inUsePrompt : base.Prompt;

    public event Action<NetworkContainer, bool> LocalOpenChanged;

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        this._user.OnValueChanged += this.OnUserChanged;
        if (IsServer) NetworkManager.OnConnectionEvent += this.OnConnectionEvent;
    }

    public override void OnNetworkDespawn()
    {
        this._user.OnValueChanged -= this.OnUserChanged;
        if (NetworkManager != null) NetworkManager.OnConnectionEvent -= this.OnConnectionEvent;
        if (this.IsOpenLocally) this.LocalOpenChanged?.Invoke(this, false);
    }

    private void Update()
    {
        if (!IsServer || !this.IsInUse) return;

        if (this._userBody == null || (this._userBody.position - this.InteractionPoint).sqrMagnitude > this.autoCloseDistance * this.autoCloseDistance)
        {
            this.ReleaseServer();
        }
    }

    #endregion

    #region Interaction

    public override bool CanInteract(in InteractionRequest request) =>
        base.CanInteract(request) && (this._user.Value == NoUser || this._user.Value == request.ClientId);

    protected override bool OnInteractServer(in InteractionRequest request)
    {
        if (this._user.Value == request.ClientId)
        {
            this.ReleaseServer();
            return true;
        }

        this._user.Value = request.ClientId;
        this._userBody = request.Instigator != null ? request.Instigator.transform : null;
        return true;
    }

    public void Close()
    {
        if (this.IsOpenLocally) this.ReleaseRpc();
    }

    [Rpc(SendTo.Server)]
    private void ReleaseRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == this._user.Value) this.ReleaseServer();
    }

    private void ReleaseServer()
    {
        this._user.Value = NoUser;
        this._userBody = null;
    }

    private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
    {
        if (data.EventType == ConnectionEvent.ClientDisconnected && data.ClientId == this._user.Value) this.ReleaseServer();
    }

    private void OnUserChanged(ulong previous, ulong current)
    {
        var local = NetworkManager.LocalClientId;
        if (previous == local) this.LocalOpenChanged?.Invoke(this, false);
        if (current == local) this.LocalOpenChanged?.Invoke(this, true);
    }

    #endregion
}
