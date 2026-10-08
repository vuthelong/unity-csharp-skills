using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CharacterAnimatorBridge : MonoBehaviour
{
    #region Fields

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");

    [SerializeField] private CharacterControllerMotor motor;
    [SerializeField] private float speedDampTime = 0.1f;

    private Animator _animator;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._animator = GetComponent<Animator>();
        if (this.motor == null) this.motor = GetComponentInParent<CharacterControllerMotor>();
        if (this.motor != null) return;

        Debug.LogError($"{nameof(CharacterAnimatorBridge)} needs a {nameof(CharacterControllerMotor)} on this object or a parent.", this);
        enabled = false;
    }

    private void Update()
    {
        var normalizedSpeed = this.motor.MaxSpeed > 0f ? this.motor.HorizontalSpeed / this.motor.MaxSpeed : 0f;
        this._animator.SetFloat(SpeedId, normalizedSpeed, this.speedDampTime, Time.deltaTime);
        this._animator.SetBool(GroundedId, this.motor.IsGrounded);
        this._animator.SetFloat(VerticalSpeedId, this.motor.VerticalVelocity);
    }

    #endregion

    #region Unity Callbacks

    private void OnAnimatorMove()
    {
        if (!enabled) return;
        this.motor.ApplyRootMotion(this._animator.deltaPosition, this._animator.deltaRotation);
    }

    #endregion
}
