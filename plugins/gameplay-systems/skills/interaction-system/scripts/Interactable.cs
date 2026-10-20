using UnityEngine;
using UnityEngine.Events;

public readonly struct InteractionRequest
{
    public InteractionRequest(GameObject instigator, ulong clientId)
    {
        this.Instigator = instigator;
        this.ClientId = clientId;
    }

    public GameObject Instigator { get; }
    public ulong ClientId { get; }
    public Vector3 InstigatorPosition => this.Instigator != null ? this.Instigator.transform.position : Vector3.zero;
}

public interface IInteractable
{
    string Prompt { get; }
    float HoldDuration { get; }
    int Priority { get; }
    float MaxDistance { get; }
    Vector3 InteractionPoint { get; }

    bool CanInteract(in InteractionRequest request);

    bool Interact(in InteractionRequest request);
}

public interface IInteractionExecutor
{
    bool Execute(IInteractable target, Interactor interactor);
}

public abstract class Interactable : MonoBehaviour, IInteractable
{
    #region Fields

    [SerializeField] private string prompt = "Interact";
    [SerializeField] private float holdDuration;
    [SerializeField] private int priority;
    [SerializeField] private float maxDistance = 2.5f;
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private UnityEvent interacted;

    #endregion

    #region Properties

    public virtual string Prompt => this.prompt;
    public float HoldDuration => this.holdDuration;
    public int Priority => this.priority;
    public float MaxDistance => this.maxDistance;
    public Vector3 InteractionPoint => this.interactionPoint != null ? this.interactionPoint.position : transform.position;

    #endregion

    #region IInteractable

    public virtual bool CanInteract(in InteractionRequest request) => isActiveAndEnabled;

    public bool Interact(in InteractionRequest request)
    {
        if (!this.CanInteract(request) || !this.OnInteract(request)) return false;

        this.interacted?.Invoke();
        return true;
    }

    protected abstract bool OnInteract(in InteractionRequest request);

    #endregion
}
