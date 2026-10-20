using System;
using System.Collections.Generic;
using UnityEngine;

public class MeleeHitDetector : MonoBehaviour
{
    #region Fields

    [SerializeField] private Health self;
    [SerializeField] private Transform[] bladePoints;
    [SerializeField] private float bladeRadius = 0.15f;
    [SerializeField] private LayerMask hurtboxMask;
    [SerializeField] private LayerMask occluderMask;
    [SerializeField] private int bufferSize = 16;

    private readonly HashSet<Health> _hitThisSwing = new();

    private Collider[] _buffer;
    private Vector3[] _previousPositions;
    private bool _swinging;
    private float _damage;
    private DamageType _damageType;

    public event Action<Hurtbox, DamageInfo, float> Hit;

    #endregion

    #region Properties

    public bool IsSwinging => this._swinging;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (this.self == null) this.self = GetComponentInParent<Health>();
        this._buffer = new Collider[Mathf.Max(1, this.bufferSize)];
        this._previousPositions = new Vector3[this.bladePoints != null ? this.bladePoints.Length : 0];
    }

    private void OnDisable() => EndSwing();

    private void LateUpdate()
    {
        if (!this._swinging) return;

        for (var i = 0; i < this.bladePoints.Length; i++)
        {
            var current = this.bladePoints[i].position;
            var previous = this._previousPositions[i];
            var count = Physics.OverlapCapsuleNonAlloc(previous, current, this.bladeRadius, this._buffer, this.hurtboxMask, QueryTriggerInteraction.Collide);
            var direction = current - previous;
            for (var c = 0; c < count; c++)
            {
                TryHit(this._buffer[c], current, direction);
                this._buffer[c] = null;
            }

            this._previousPositions[i] = current;
        }
    }

    #endregion

    #region Swing

    public void BeginSwing(float damage, DamageType damageType)
    {
        this._damage = damage;
        this._damageType = damageType;
        this._hitThisSwing.Clear();
        for (var i = 0; i < this._previousPositions.Length; i++) this._previousPositions[i] = this.bladePoints[i].position;
        this._swinging = this._previousPositions.Length > 0;
    }

    public void EndSwing()
    {
        this._swinging = false;
        this._hitThisSwing.Clear();
    }

    public int StrikeArc(Vector3 origin, Vector3 forward, float range, float angleDegrees, float damage, DamageType damageType)
    {
        this._damage = damage;
        this._damageType = damageType;
        this._hitThisSwing.Clear();

        var hits = 0;
        var halfAngle = angleDegrees * 0.5f;
        var count = Physics.OverlapSphereNonAlloc(origin, range, this._buffer, this.hurtboxMask, QueryTriggerInteraction.Collide);
        for (var i = 0; i < count; i++)
        {
            var candidate = this._buffer[i];
            this._buffer[i] = null;
            var closest = candidate.ClosestPoint(origin);
            var toTarget = closest - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(forward, toTarget) > halfAngle) continue;
            if (IsOccluded(origin, closest)) continue;
            if (TryHit(candidate, closest, toTarget)) hits++;
        }

        this._hitThisSwing.Clear();
        return hits;
    }

    private bool TryHit(Collider candidate, Vector3 reference, Vector3 direction)
    {
        if (!candidate.TryGetComponent<Hurtbox>(out var hurtbox) || !hurtbox.enabled) return false;

        var owner = hurtbox.Owner;
        if (owner == null || owner == this.self || !owner.IsAlive) return false;
        if (!this._hitThisSwing.Add(owner)) return false;

        var point = candidate.ClosestPoint(reference);
        var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        var source = this.self != null ? this.self.gameObject : gameObject;
        var team = this.self != null ? this.self.Team : CombatTeam.Neutral;
        var info = new DamageInfo(this._damage, this._damageType, source, team, point, dir);
        var applied = hurtbox.ApplyHit(info);
        Hit?.Invoke(hurtbox, info, applied);
        return true;
    }

    private bool IsOccluded(Vector3 from, Vector3 to)
    {
        if (this.occluderMask.value == 0) return false;

        var delta = to - from;
        var distance = delta.magnitude;
        if (distance < 0.01f) return false;

        return Physics.Raycast(from, delta / distance, out _, distance, this.occluderMask, QueryTriggerInteraction.Ignore);
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        if (this.bladePoints == null) return;

        Gizmos.color = this._swinging ? Color.red : Color.white;
        for (var i = 0; i < this.bladePoints.Length; i++)
        {
            if (this.bladePoints[i] != null) Gizmos.DrawWireSphere(this.bladePoints[i].position, this.bladeRadius);
        }
    }

    #endregion
}
