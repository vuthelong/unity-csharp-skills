using Unity.Netcode;
using UnityEngine;

public sealed class NetworkDoor : NetworkInteractable
{
    #region Fields

    [SerializeField] private Transform hinge;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float swingSpeed = 180f;
    [SerializeField] private bool startLocked;
    [SerializeField] private string openPrompt = "Open";
    [SerializeField] private string closePrompt = "Close";
    [SerializeField] private string lockedPrompt = "Locked";

    private readonly NetworkVariable<bool> _isOpen = new();
    private readonly NetworkVariable<bool> _isLocked = new();
    private Quaternion _closedRotation;

    #endregion

    #region Properties

    public bool IsOpen => this._isOpen.Value;
    public bool IsLocked => this._isLocked.Value;

    public override string Prompt => this._isLocked.Value ? this.lockedPrompt : this._isOpen.Value ? this.closePrompt : this.openPrompt;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (this.hinge == null) this.hinge = transform;
        this._closedRotation = this.hinge.localRotation;
    }

    private void Update()
    {
        var target = this._isOpen.Value ? this._closedRotation * Quaternion.Euler(0f, this.openAngle, 0f) : this._closedRotation;
        this.hinge.localRotation = Quaternion.RotateTowards(this.hinge.localRotation, target, this.swingSpeed * Time.deltaTime);
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        if (IsServer) this._isLocked.Value = this.startLocked;
    }

    #endregion

    #region Interaction

    public override bool CanInteract(in InteractionRequest request) => base.CanInteract(request) && !this._isLocked.Value;

    protected override bool OnInteractServer(in InteractionRequest request)
    {
        this._isOpen.Value = !this._isOpen.Value;
        return true;
    }

    public void SetLockedServer(bool locked)
    {
        if (IsServer) this._isLocked.Value = locked;
    }

    #endregion
}
