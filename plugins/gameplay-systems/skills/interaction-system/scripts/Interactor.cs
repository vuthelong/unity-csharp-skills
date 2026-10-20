using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum InteractionDetectionMode
{
    CameraRay,
    Proximity,
    CameraRayThenProximity
}

public sealed class Interactor : MonoBehaviour
{
    #region Fields

    private const int OverlapBufferSize = 32;

    [Header("References")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform eye;
    [SerializeField] private Transform viewOrigin;
    [SerializeField] private InputActionReference interactAction;

    [Header("Detection")]
    [SerializeField] private InteractionDetectionMode mode = InteractionDetectionMode.CameraRayThenProximity;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private LayerMask occluderMask = ~0;
    [SerializeField] private float rayReach = 3f;
    [SerializeField] private float rayRadius = 0.1f;
    [SerializeField] private float proximityRadius = 2f;
    [SerializeField] private float maxViewAngle = 75f;

    [Header("Scoring")]
    [SerializeField] private float distanceWeight = 1f;
    [SerializeField] private float angleWeight = 1f;
    [SerializeField] private float priorityWeight = 10f;
    [SerializeField] private float switchMargin = 0.15f;

    [Header("Network")]
    [SerializeField] private float resultTimeout = 2f;

    private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
    private readonly HashSet<object> _locks = new();
    private IInteractable _focus;
    private Component _focusComponent;
    private IInteractable _holdTarget;
    private float _holdElapsed;
    private bool _holdConsumed;
    private float _pendingSince = -1f;

    #endregion

    #region Properties

    public IInteractable Focus => this._focusComponent != null ? this._focus : null;
    public bool FocusAvailable => this.Focus != null && this.Focus.CanInteract(this.CreateRequest());
    public bool IsLocked => this._locks.Count > 0;
    public bool IsAwaitingResult => this._pendingSince >= 0f;
    public float HoldProgress { get; private set; }
    public IInteractionExecutor Executor { get; set; }
    public ulong LocalClientId { get; set; }
    public Transform Body => this.body;

    public event Action<IInteractable> FocusChanged;
    public event Action<float> HoldProgressChanged;
    public event Action<IInteractable> Submitted;
    public event Action<IInteractable, bool> Completed;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (this.body == null) this.body = transform;
        if (this.eye == null) this.eye = this.body;

        var ownLayer = this.body.gameObject.layer;
        if (ownLayer != 0)
        {
            this.interactableMask &= ~(1 << ownLayer);
            this.occluderMask &= ~(1 << ownLayer);
        }
    }

    private void OnEnable()
    {
        if (this.viewOrigin == null && Camera.main != null) this.viewOrigin = Camera.main.transform;
        if (this.interactAction != null && this.interactAction.action != null) this.interactAction.action.Enable();
    }

    private void OnDisable()
    {
        this.SetFocus(null, null);
        this.ResetHold();
        this._pendingSince = -1f;
    }

    private void Update()
    {
        if (this.IsAwaitingResult && Time.unscaledTime - this._pendingSince > this.resultTimeout) this.Complete(this._focus, false);

        if (this.IsLocked)
        {
            this.SetFocus(null, null);
            this.ResetHold();
            return;
        }

        this.DetectFocus();
        this.UpdateInput(Time.deltaTime);
    }

    #endregion

    #region Locks

    public void AddLock(object owner)
    {
        if (owner != null) this._locks.Add(owner);
    }

    public void RemoveLock(object owner)
    {
        if (owner != null) this._locks.Remove(owner);
    }

    #endregion

    #region Detection

    private void DetectFocus()
    {
        IInteractable best = null;
        Component bestComponent = null;

        if (this.mode != InteractionDetectionMode.Proximity) this.TryCameraRay(out best, out bestComponent);
        if (best == null && this.mode != InteractionDetectionMode.CameraRay) this.TryProximity(out best, out bestComponent);

        this.SetFocus(best, bestComponent);
    }

    private bool TryCameraRay(out IInteractable result, out Component component)
    {
        result = null;
        component = null;
        if (this.viewOrigin == null) return false;

        var origin = this.viewOrigin.position;
        var direction = this.viewOrigin.forward;
        var length = Vector3.Distance(origin, this.eye.position) + this.rayReach;
        var mask = this.interactableMask | this.occluderMask;
        if (!Physics.SphereCast(origin, this.rayRadius, direction, out var hit, length, mask, QueryTriggerInteraction.Collide)) return false;

        var candidate = hit.collider.GetComponentInParent<IInteractable>();
        if (candidate == null || !this.IsInReach(candidate) || !this.HasLineOfSight(candidate, hit.collider)) return false;

        result = candidate;
        component = candidate as Component;
        return true;
    }

    private bool TryProximity(out IInteractable result, out Component component)
    {
        result = null;
        component = null;

        var center = this.body.position;
        var count = Physics.OverlapSphereNonAlloc(center, this.proximityRadius, this._overlapBuffer, this.interactableMask, QueryTriggerInteraction.Collide);
        var facing = this.GetFacing();
        var bestScore = float.NegativeInfinity;

        for (var i = 0; i < count; i++)
        {
            var collider = this._overlapBuffer[i];
            this._overlapBuffer[i] = null;

            var candidate = collider.GetComponentInParent<IInteractable>();
            if (candidate == null || !this.IsInReach(candidate)) continue;

            var toTarget = candidate.InteractionPoint - center;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            var angle = distance > 0.001f ? Vector3.Angle(facing, toTarget) : 0f;
            if (angle > this.maxViewAngle || !this.HasLineOfSight(candidate, collider)) continue;

            var score = candidate.Priority * this.priorityWeight
                        + (1f - distance / this.proximityRadius) * this.distanceWeight
                        + (1f - angle / this.maxViewAngle) * this.angleWeight;
            if (ReferenceEquals(candidate, this._focus)) score += this.switchMargin;
            if (score <= bestScore) continue;

            bestScore = score;
            result = candidate;
            component = candidate as Component;
        }

        return result != null;
    }

    private bool IsInReach(IInteractable candidate)
    {
        var distance = Vector3.Distance(this.body.position, candidate.InteractionPoint);
        return distance <= candidate.MaxDistance;
    }

    private bool HasLineOfSight(IInteractable candidate, Collider targetCollider)
    {
        var from = this.eye.position;
        var to = candidate.InteractionPoint;
        if (!Physics.Linecast(from, to, out var hit, this.occluderMask, QueryTriggerInteraction.Ignore)) return true;

        return hit.collider == targetCollider || hit.collider.GetComponentInParent<IInteractable>() == candidate;
    }

    private Vector3 GetFacing()
    {
        var forward = this.viewOrigin != null ? this.viewOrigin.forward : this.body.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : this.body.forward;
    }

    private void SetFocus(IInteractable focus, Component component)
    {
        if (component == null) focus = null;
        if (ReferenceEquals(focus, this._focus) && component == this._focusComponent) return;

        this._focus = focus;
        this._focusComponent = component;
        this.ResetHold();
        this.FocusChanged?.Invoke(focus);
    }

    #endregion

    #region Input

    private void UpdateInput(float deltaTime)
    {
        var action = this.interactAction != null ? this.interactAction.action : null;
        if (action == null) return;

        var target = this.Focus;
        if (action.WasPressedThisFrame())
        {
            this._holdTarget = target;
            this._holdElapsed = 0f;
            this._holdConsumed = false;
        }

        if (!action.IsPressed() || this._holdConsumed || this._holdTarget == null)
        {
            this.ResetHold();
            return;
        }

        if (!ReferenceEquals(target, this._holdTarget) || this.IsAwaitingResult || !this.FocusAvailable)
        {
            this.ResetHold();
            this._holdConsumed = true;
            return;
        }

        var duration = target.HoldDuration;
        this._holdElapsed += deltaTime;
        this.SetHoldProgress(duration > 0f ? Mathf.Clamp01(this._holdElapsed / duration) : 1f);
        if (this.HoldProgress < 1f) return;

        this._holdConsumed = true;
        this.Submit(target);
    }

    private void ResetHold()
    {
        this._holdTarget = null;
        this._holdElapsed = 0f;
        this.SetHoldProgress(0f);
    }

    private void SetHoldProgress(float value)
    {
        if (Mathf.Approximately(value, this.HoldProgress)) return;

        this.HoldProgress = value;
        this.HoldProgressChanged?.Invoke(value);
    }

    #endregion

    #region Execution

    public InteractionRequest CreateRequest() => new(this.body.gameObject, this.LocalClientId);

    private void Submit(IInteractable target)
    {
        this.Submitted?.Invoke(target);

        if (this.Executor != null && this.Executor.Execute(target, this))
        {
            this._pendingSince = Time.unscaledTime;
            return;
        }

        var accepted = target.Interact(this.CreateRequest());
        this.Complete(target, accepted);
    }

    public void Complete(IInteractable target, bool accepted)
    {
        this._pendingSince = -1f;
        this.ResetHold();
        this.Completed?.Invoke(target, accepted);
    }

    #endregion
}
