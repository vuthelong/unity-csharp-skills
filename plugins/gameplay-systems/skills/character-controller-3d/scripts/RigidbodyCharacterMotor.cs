using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class RigidbodyCharacterMotor : MonoBehaviour
{
    #region Fields

    private const float ProbeStartOffset = 0.05f;
    private const float ProbeRadiusScale = 0.9f;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float groundAcceleration = 50f;
    [SerializeField] private float airAcceleration = 10f;
    [SerializeField] private float turnSpeed = 720f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float groundSnapSpeed = 0.5f;

    [Header("Ground")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeDistance = 0.15f;
    [SerializeField] private float maxSlopeAngle = 45f;

    [Header("Physics")]
    [SerializeField] private PhysicsMaterial characterMaterial;
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.Continuous;
    [SerializeField] private float airLinearDamping;

    private Rigidbody _rigidbody;
    private CapsuleCollider _capsule;
    private PhysicsMaterial _runtimeMaterial;
    private Vector2 _moveInput;
    private bool _sprintHeld;
    private float _lastJumpPressedTime = float.NegativeInfinity;
    private float _lastGroundedTime = float.NegativeInfinity;
    private bool _isGrounded;
    private bool _jumpedThisStep;
    private Vector3 _groundNormal = Vector3.up;
    private Rigidbody _groundBody;

    #endregion

    #region Properties

    public bool IsGrounded => this._isGrounded;
    public Vector3 Velocity => this._rigidbody.linearVelocity;
    public float HorizontalSpeed => Vector3.ProjectOnPlane(this._rigidbody.linearVelocity, Vector3.up).magnitude;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._rigidbody = GetComponent<Rigidbody>();
        this._capsule = GetComponent<CapsuleCollider>();
        if (this.cameraTransform == null && Camera.main != null) this.cameraTransform = Camera.main.transform;

        this._rigidbody.isKinematic = false;
        this._rigidbody.useGravity = true;
        this._rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        this._rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        this._rigidbody.collisionDetectionMode = this.collisionDetection;
        this._rigidbody.linearDamping = 0f;
        this._rigidbody.angularDamping = 0f;

        if (this.characterMaterial == null)
        {
            this._runtimeMaterial = new PhysicsMaterial("CharacterFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            this.characterMaterial = this._runtimeMaterial;
        }

        this._capsule.sharedMaterial = this.characterMaterial;
    }

    private void OnEnable()
    {
        EnableAction(this.moveAction);
        EnableAction(this.jumpAction);
        EnableAction(this.sprintAction);
    }

    private void Update()
    {
        this._moveInput = this.moveAction != null ? this.moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        this._sprintHeld = this.sprintAction != null && this.sprintAction.action.IsPressed();

        if (this.jumpAction != null && this.jumpAction.action.WasPressedThisFrame())
        {
            this._lastJumpPressedTime = Time.time;
        }
    }

    private void FixedUpdate()
    {
        var deltaTime = Time.fixedDeltaTime;
        this._jumpedThisStep = false;

        ProbeGround();
        TryJump();
        ApplyMovement(deltaTime);
        ApplyRotation(deltaTime);
    }

    private void OnDestroy()
    {
        if (this._runtimeMaterial != null) Destroy(this._runtimeMaterial);
    }

    #endregion

    #region Public Methods

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        this._rigidbody.position = position;
        this._rigidbody.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        this._rigidbody.linearVelocity = Vector3.zero;
    }

    #endregion

    #region Private Methods

    private static void EnableAction(InputActionReference reference)
    {
        if (reference == null || reference.action == null) return;
        reference.action.Enable();
    }

    private Vector3 GetCameraRelativeDirection()
    {
        var input = Vector2.ClampMagnitude(this._moveInput, 1f);
        if (this.cameraTransform == null) return new Vector3(input.x, 0f, input.y);

        var forward = Vector3.ProjectOnPlane(this.cameraTransform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(this.cameraTransform.up, Vector3.up);
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward);

        return forward * input.y + right * input.x;
    }

    private void ProbeGround()
    {
        var scale = transform.lossyScale;
        var radius = this._capsule.radius * Mathf.Max(scale.x, scale.z);
        var halfHeight = Mathf.Max(this._capsule.height * scale.y * 0.5f, radius);
        var center = this._rigidbody.position + this._rigidbody.rotation * Vector3.Scale(this._capsule.center, scale);
        var bottomSphereCenter = center + Vector3.down * (halfHeight - radius);

        var probeRadius = radius * ProbeRadiusScale;
        var origin = bottomSphereCenter + Vector3.up * ProbeStartOffset;
        var distance = ProbeStartOffset + (radius - probeRadius) + this.groundProbeDistance;

        var hasHit = Physics.SphereCast(origin, probeRadius, Vector3.down, out var hit, distance, this.groundMask, QueryTriggerInteraction.Ignore);
        var movingUp = Vector3.Dot(this._rigidbody.linearVelocity, Vector3.up) > 0.1f && Time.time - this._lastGroundedTime > this.coyoteTime;

        this._isGrounded = hasHit && !movingUp && Vector3.Angle(hit.normal, Vector3.up) <= this.maxSlopeAngle;
        this._groundNormal = this._isGrounded ? hit.normal : Vector3.up;
        this._groundBody = this._isGrounded ? hit.rigidbody : null;

        if (this._isGrounded) this._lastGroundedTime = Time.time;
    }

    private void TryJump()
    {
        var jumpBuffered = Time.time - this._lastJumpPressedTime <= this.jumpBufferTime;
        var withinCoyote = Time.time - this._lastGroundedTime <= this.coyoteTime;
        if (!jumpBuffered || !withinCoyote) return;

        var velocity = this._rigidbody.linearVelocity;
        velocity.y = Mathf.Sqrt(2f * this.jumpHeight * -Physics.gravity.y);
        this._rigidbody.linearVelocity = velocity;

        this._lastJumpPressedTime = float.NegativeInfinity;
        this._lastGroundedTime = float.NegativeInfinity;
        this._isGrounded = false;
        this._jumpedThisStep = true;
    }

    private void ApplyMovement(float deltaTime)
    {
        this._rigidbody.useGravity = !this._isGrounded;
        this._rigidbody.linearDamping = this._isGrounded ? 0f : this.airLinearDamping;

        var speed = this._sprintHeld ? this.sprintSpeed : this.walkSpeed;
        var desired = GetCameraRelativeDirection() * speed;
        var platformVelocity = this._groundBody != null ? this._groundBody.GetPointVelocity(this._rigidbody.position) : Vector3.zero;
        var velocity = this._rigidbody.linearVelocity - platformVelocity;

        if (this._isGrounded && !this._jumpedThisStep)
        {
            var alongGround = Vector3.ProjectOnPlane(desired, this._groundNormal).normalized * desired.magnitude;
            var current = Vector3.ProjectOnPlane(velocity, this._groundNormal);
            var next = Vector3.MoveTowards(current, alongGround, this.groundAcceleration * deltaTime);
            this._rigidbody.linearVelocity = next - this._groundNormal * this.groundSnapSpeed + platformVelocity;
            return;
        }

        var horizontal = new Vector3(velocity.x, 0f, velocity.z);
        var nextHorizontal = Vector3.MoveTowards(horizontal, desired, this.airAcceleration * deltaTime);
        this._rigidbody.linearVelocity = new Vector3(nextHorizontal.x, velocity.y, nextHorizontal.z) + platformVelocity;
    }

    private void ApplyRotation(float deltaTime)
    {
        var direction = GetCameraRelativeDirection();
        if (direction.sqrMagnitude < 0.0001f) return;

        var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        var nextRotation = Quaternion.RotateTowards(this._rigidbody.rotation, targetRotation, this.turnSpeed * deltaTime);
        this._rigidbody.MoveRotation(nextRotation);
    }

    #endregion

    #region Unity Callbacks

    private void OnValidate()
    {
        this.walkSpeed = Mathf.Max(0f, this.walkSpeed);
        this.sprintSpeed = Mathf.Max(this.walkSpeed, this.sprintSpeed);
        this.jumpHeight = Mathf.Max(0f, this.jumpHeight);
        this.maxSlopeAngle = Mathf.Clamp(this.maxSlopeAngle, 0f, 89f);
    }

    #endregion
}
