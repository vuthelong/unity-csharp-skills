using Unity.Netcode;
using UnityEngine;

public abstract class NetworkInteractable : NetworkBehaviour, IInteractable
{
    #region Fields

    [SerializeField] private string prompt = "Interact";
    [SerializeField] private float holdDuration;
    [SerializeField] private int priority;
    [SerializeField] private float maxDistance = 2.5f;
    [SerializeField] private Transform interactionPoint;

    #endregion

    #region Properties

    public virtual string Prompt => this.prompt;
    public float HoldDuration => this.holdDuration;
    public int Priority => this.priority;
    public float MaxDistance => this.maxDistance;
    public Vector3 InteractionPoint => this.interactionPoint != null ? this.interactionPoint.position : transform.position;

    #endregion

    #region IInteractable

    public virtual bool CanInteract(in InteractionRequest request) => IsSpawned && isActiveAndEnabled;

    public bool Interact(in InteractionRequest request) => IsServer && this.CanInteract(request) && this.OnInteractServer(request);

    protected abstract bool OnInteractServer(in InteractionRequest request);

    #endregion
}
