using Unity.Netcode;
using UnityEngine;

public interface IPickupReceiver
{
    bool TryReceive(string itemId, int amount);
}

public sealed class NetworkPickup : NetworkInteractable
{
    #region Fields

    [SerializeField] private string itemId;
    [SerializeField] private int amount = 1;

    private bool _taken;

    #endregion

    #region Properties

    public string ItemId => this.itemId;
    public int Amount => this.amount;

    #endregion

    #region Interaction

    public override bool CanInteract(in InteractionRequest request) => base.CanInteract(request) && !this._taken;

    protected override bool OnInteractServer(in InteractionRequest request)
    {
        var receiver = request.Instigator != null ? request.Instigator.GetComponentInParent<IPickupReceiver>() : null;
        if (receiver == null || !receiver.TryReceive(this.itemId, this.amount)) return false;

        this._taken = true;
        NetworkObject.Despawn(true);
        return true;
    }

    #endregion
}
