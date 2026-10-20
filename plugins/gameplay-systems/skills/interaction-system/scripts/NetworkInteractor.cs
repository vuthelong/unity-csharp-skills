using Unity.Netcode;
using UnityEngine;

public enum InteractionResult : byte
{
    Accepted,
    Missing,
    OutOfRange,
    Rejected,
    TooFast
}

[RequireComponent(typeof(Interactor))]
public sealed class NetworkInteractor : NetworkBehaviour, IInteractionExecutor
{
    #region Fields

    [SerializeField] private float distanceTolerance = 0.75f;
    [SerializeField] private float minRequestInterval = 0.2f;

    private Interactor _interactor;
    private IInteractable _pendingTarget;
    private float _lastServerRequestTime = float.NegativeInfinity;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._interactor = GetComponent<Interactor>();
        this._interactor.enabled = false;
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        this._interactor.enabled = IsOwner;
        if (!IsOwner) return;

        this._interactor.LocalClientId = NetworkManager.LocalClientId;
        this._interactor.Executor = this;
    }

    public override void OnNetworkDespawn()
    {
        if (ReferenceEquals(this._interactor.Executor, this)) this._interactor.Executor = null;
        this._interactor.enabled = false;
        this._pendingTarget = null;
    }

    #endregion

    #region Client

    public bool Execute(IInteractable target, Interactor interactor)
    {
        if (!IsSpawned || !(target is NetworkBehaviour behaviour) || !behaviour.IsSpawned) return false;

        this._pendingTarget = target;
        this.RequestInteractRpc(new NetworkBehaviourReference(behaviour));
        return true;
    }

    [Rpc(SendTo.Owner)]
    private void InteractionResultRpc(InteractionResult result)
    {
        var target = this._pendingTarget;
        this._pendingTarget = null;
        this._interactor.Complete(target, result == InteractionResult.Accepted);
    }

    #endregion

    #region Server

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestInteractRpc(NetworkBehaviourReference targetReference, RpcParams rpcParams = default)
    {
        var result = this.ValidateAndInteract(targetReference, rpcParams.Receive.SenderClientId);
        this.InteractionResultRpc(result);
    }

    private InteractionResult ValidateAndInteract(NetworkBehaviourReference targetReference, ulong senderClientId)
    {
        if (senderClientId != OwnerClientId) return InteractionResult.Rejected;

        var now = Time.unscaledTime;
        if (now - this._lastServerRequestTime < this.minRequestInterval) return InteractionResult.TooFast;
        this._lastServerRequestTime = now;

        if (!targetReference.TryGet(out NetworkBehaviour behaviour) || !(behaviour is IInteractable target)) return InteractionResult.Missing;

        var reach = target.MaxDistance + this.distanceTolerance;
        if ((target.InteractionPoint - this._interactor.Body.position).sqrMagnitude > reach * reach) return InteractionResult.OutOfRange;

        var request = new InteractionRequest(this._interactor.Body.gameObject, senderClientId);
        if (!target.CanInteract(request)) return InteractionResult.Rejected;

        return target.Interact(request) ? InteractionResult.Accepted : InteractionResult.Rejected;
    }

    #endregion
}
