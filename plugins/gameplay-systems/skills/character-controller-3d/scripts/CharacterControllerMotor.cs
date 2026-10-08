using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterControllerMotor : MonoBehaviour
{
    #region Fields

    private const float GroundedStickVelocity = -2f;
    private const float ProbeStartOffset = 0.05f;
    private const float ProbeRadiusScale = 0.9f;
    private const float PushMaxVerticalDirection = -0.3f;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float groundAcceleration = 40f;
    [SerializeField] private float airAcceleration = 8f;
    [SerializeField] private float turnSpeed = 720f;

    [Header("Jump and Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float maxFallSpeed = -50f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Ground")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private LayerMask platformMask;
    [SerializeField] private float groundProbeDistance = 0.2f;
    [SerializeField] private float steepSlideSpeed = 6f;

    [Header("Root Motion")]
    [SerializeField] private bool useRootMotion;

    [Header("Pushing")]
    [SerializeField] private float pushPower = 2f;
    [SerializeField] private float maxPushMass = 50f;

    private CharacterController _controller;
    private Vector2 _moveInput;
    private bool _sprintHeld;
    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;
    private float _lastGroundedTime = float.NegativeInfinity;
    private float _lastJumpPressedTime = float.NegativeInfinity;
    private bool _hasGround;
    private bool _isGrounded;
    private Vector3 _groundNormal = Vector3.up;
    private Collider _groundCollider;
    private Transform _platform;
    private Vector3 _platformLocalPosition;
    private Quaternion _platformLastRotation;

    #endregion

    #region Properties

    public bool IsGrounded => this._isGrounded;
    public Vector3 Velocity => this._controller.velocity;
    public float HorizontalSpeed => new Vector3(this._horizontalVelocity.x, 0f, this._horizontalVelocity.z).magnitude;
    public float VerticalVelocity => this._verticalVelocity;
    public float MaxSpeed => this.sprintSpeed;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._controller = GetComponent<CharacterController>();
        if (this.cameraTransform == null && Camera.main != null) this.cameraTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        EnableAction(this.moveAction);
        EnableAction(this.jumpAction);
        EnableAction(this.sprintAction);
    }

    private void Update()
    {
        var deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        ReadInput();
        ApplyPlatformMotion();
        ProbeGround();
        UpdateVertical(deltaTime);
        RotateTowardsInput(deltaTime);

        if (this.useRootMotion) return;

        UpdateHorizontal(deltaTime);
        var horizontalMotion = this._horizontalVelocity * deltaTime;
        MoveController(horizontalMotion, deltaTime);
    }

    private void OnDisable()
    {
        this._platform = null;
    }

    #endregion

    #region Public Methods

    public void ApplyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
    {
        if (!this.useRootMotion || !enabled) return;

        var deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        var horizontalMotion = Vector3.ProjectOnPlane(deltaPosition, Vector3.up);
        if (this._isGrounded)
        {
            this._horizontalVelocity = horizontalMotion / deltaTime;
        }
        else
        {
            horizontalMotion = this._horizontalVelocity * deltaTime;
        }

        MoveController(horizontalMotion, deltaTime);

        var yaw = deltaRotation.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f) * transform.rotation;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        this._controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        this._controller.enabled = true;

        this._horizontalVelocity = Vector3.zero;
        this._verticalVelocity = 0f;
        this._platform = null;
    }

    public void AddVerticalImpulse(float velocity)
    {
        this._verticalVelocity = velocity;
        this._isGrounded = false;
        this._lastGroundedTime = float.NegativeInfinity;
    }

    #endregion

    #region Private Methods

    private static void EnableAction(InputActionReference reference)
    {
        if (reference == null || reference.action == null) return;
        reference.action.Enable();
    }

    private void ReadInput()
    {
        this._moveInput = this.moveAction != null ? this.moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        this._sprintHeld = this.sprintAction != null && this.sprintAction.action.IsPressed();

        if (this.jumpAction != null && this.jumpAction.action.WasPressedThisFrame())
        {
            this._lastJumpPressedTime = Time.time;
        }
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

    private void ApplyPlatformMotion()
    {
        if (this._platform == null) return;

        var targetPosition = this._platform.TransformPoint(this._platformLocalPosition);
        var platformDelta = targetPosition - transform.position;
        var rotationDelta = this._platform.rotation * Quaternion.Inverse(this._platformLastRotation);

        if (platformDelta.sqrMagnitude > 0f) this._controller.Move(platformDelta);

        var yaw = rotationDelta.eulerAngles.y;
        if (Mathf.Abs(yaw) > 0f) transform.rotation = Quaternion.Euler(0f, yaw, 0f) * transform.rotation;
    }

    private void CapturePlatform()
    {
        if (!this._hasGround || this._groundCollider == null || (this.platformMask.value & (1 << this._groundCollider.gameObject.layer)) == 0)
        {
            this._platform = null;
            return;
        }

        this._platform = this._groundCollider.transform;
        this._platformLocalPosition = this._platform.InverseTransformPoint(transform.position);
        this._platformLastRotation = this._platform.rotation;
    }

    private void ProbeGround()
    {
        var radius = this._controller.radius * ProbeRadiusScale;
        var bottomSphereCenter = transform.position + this._controller.center + Vector3.down * (this._controller.height * 0.5f - this._controller.radius);
        var origin = bottomSphereCenter + Vector3.up * ProbeStartOffset;
        var distance = ProbeStartOffset + (this._controller.radius - radius) + this._controller.skinWidth + this.groundProbeDistance;

        this._hasGround = Physics.SphereCast(origin, radius, Vector3.down, out var hit, distance, this.groundMask, QueryTriggerInteraction.Ignore);
        if (!this._hasGround)
        {
            this._hasGround = this._controller.isGrounded;
            this._isGrounded = this._hasGround;
            this._groundNormal = Vector3.up;
            this._groundCollider = null;
            return;
        }

        this._groundCollider = hit.collider;
        this._groundNormal = hit.normal;

        var faceRayOrigin = hit.point + Vector3.up * 0.1f;
        if (Physics.Raycast(faceRayOrigin, Vector3.down, out var faceHit, 0.2f, this.groundMask, QueryTriggerInteraction.Ignore))
        {
            this._groundNormal = faceHit.normal;
        }

        var slopeAngle = Vector3.Angle(this._groundNormal, Vector3.up);
        this._isGrounded = slopeAngle <= this._controller.slopeLimit && this._verticalVelocity <= 0f;
    }

    private void UpdateVertical(float deltaTime)
    {
        if (this._hasGround && this._verticalVelocity <= 0f)
        {
            this._verticalVelocity = GroundedStickVelocity;
        }
        else
        {
            this._verticalVelocity = Mathf.Max(this._verticalVelocity + this.gravity * deltaTime, this.maxFallSpeed);
        }

        if (this._isGrounded) this._lastGroundedTime = Time.time;

        var jumpBuffered = Time.time - this._lastJumpPressedTime <= this.jumpBufferTime;
        var withinCoyote = Time.time - this._lastGroundedTime <= this.coyoteTime;
        if (!jumpBuffered || !withinCoyote) return;

        this._verticalVelocity = Mathf.Sqrt(2f * this.jumpHeight * -this.gravity);
        this._lastJumpPressedTime = float.NegativeInfinity;
        this._lastGroundedTime = float.NegativeInfinity;
        this._isGrounded = false;
        this._hasGround = false;
        this._platform = null;
    }

    private void UpdateHorizontal(float deltaTime)
    {
        var speed = this._sprintHeld ? this.sprintSpeed : this.walkSpeed;
        var target = GetCameraRelativeDirection() * speed;
        var acceleration = this._isGrounded ? this.groundAcceleration : this.airAcceleration;
        this._horizontalVelocity = Vector3.MoveTowards(this._horizontalVelocity, target, acceleration * deltaTime);
    }

    private void RotateTowardsInput(float deltaTime)
    {
        var direction = GetCameraRelativeDirection();
        if (direction.sqrMagnitude < 0.0001f) return;

        var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, this.turnSpeed * deltaTime);
    }

    private void MoveController(Vector3 horizontalMotion, float deltaTime)
    {
        var motion = horizontalMotion;

        if (this._isGrounded && horizontalMotion.sqrMagnitude > 0f)
        {
            var alongGround = Vector3.ProjectOnPlane(horizontalMotion, this._groundNormal);
            motion = alongGround.normalized * horizontalMotion.magnitude;
        }

        if (this._hasGround && !this._isGrounded && this._verticalVelocity <= 0f)
        {
            var slideDirection = Vector3.ProjectOnPlane(Vector3.down, this._groundNormal).normalized;
            motion += slideDirection * (this.steepSlideSpeed * deltaTime);
        }

        motion += Vector3.up * (this._verticalVelocity * deltaTime);

        var flags = this._controller.Move(motion);
        if ((flags & CollisionFlags.Above) != 0 && this._verticalVelocity > 0f) this._verticalVelocity = 0f;

        CapturePlatform();
    }

    #endregion

    #region Unity Callbacks

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var body = hit.rigidbody;
        if (body == null || body.isKinematic || body.mass > this.maxPushMass) return;
        if (hit.moveDirection.y < PushMaxVerticalDirection) return;

        var pushVelocity = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z) * this.pushPower;
        body.linearVelocity = new Vector3(pushVelocity.x, body.linearVelocity.y, pushVelocity.z);
        body.WakeUp();
    }

    private void OnValidate()
    {
        this.walkSpeed = Mathf.Max(0f, this.walkSpeed);
        this.sprintSpeed = Mathf.Max(this.walkSpeed, this.sprintSpeed);
        this.gravity = Mathf.Min(-0.01f, this.gravity);
        this.jumpHeight = Mathf.Max(0f, this.jumpHeight);
    }

    #endregion
}
