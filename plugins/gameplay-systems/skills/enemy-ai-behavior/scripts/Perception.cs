using UnityEngine;

public struct PerceivedTarget
{
    public PerceptionTarget Target;
    public Vector3 LastKnownPosition;
    public float LastSeenTime;
    public float LastHeardTime;
    public float LastSensedTime;
    public bool VisibleNow;
}

public class Perception : MonoBehaviour
{
    #region Fields

    [Header("Vision")]
    [SerializeField] private Transform eyes;
    [SerializeField] private float viewDistance = 20f;
    [SerializeField, Range(1f, 360f)] private float viewAngle = 120f;
    [SerializeField] private float proximityRadius = 2.5f;
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private LayerMask occluderMask = ~0;
    [SerializeField] private int candidateBufferSize = 16;

    [Header("Hearing")]
    [SerializeField] private float hearingMultiplier = 1f;

    [Header("Memory")]
    [SerializeField] private float memoryDuration = 8f;
    [SerializeField] private int memoryCapacity = 6;

    private Collider[] _candidates;
    private PerceivedTarget[] _memory;
    private int _memoryCount;
    private Vector3 _noisePoint;
    private float _noiseTime = float.NegativeInfinity;
    private bool _hasNoise;

    #endregion

    #region Properties

    public int MemoryCount => this._memoryCount;
    public Vector3 EyePosition => this.eyes != null ? this.eyes.position : transform.position + Vector3.up * 1.6f;
    public Vector3 EyeForward => this.eyes != null ? this.eyes.forward : transform.forward;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._candidates = new Collider[Mathf.Max(1, this.candidateBufferSize)];
        this._memory = new PerceivedTarget[Mathf.Max(1, this.memoryCapacity)];
    }

    private void OnEnable() => NoiseEvents.Emitted += this.OnNoise;

    private void OnDisable()
    {
        NoiseEvents.Emitted -= this.OnNoise;
        ForgetAll();
    }

    #endregion

    #region Sensing

    public void Tick(float now)
    {
        for (var i = 0; i < this._memoryCount; i++) this._memory[i].VisibleNow = false;

        ScanVision(now);
        ExpireMemory(now);
    }

    public bool CanSee(PerceptionTarget target)
    {
        if (target == null || !target.IsTargetable) return false;

        var eye = EyePosition;
        var toTarget = target.AimPosition - eye;
        var distance = toTarget.magnitude;
        if (distance < 0.001f) return true;
        if (distance > this.viewDistance * target.Visibility && distance > this.proximityRadius) return false;
        if (distance > this.proximityRadius && Vector3.Angle(EyeForward, toTarget) > this.viewAngle * 0.5f) return false;

        return !Physics.Raycast(eye, toTarget / distance, out _, distance, this.occluderMask, QueryTriggerInteraction.Ignore);
    }

    private void ScanVision(float now)
    {
        var count = Physics.OverlapSphereNonAlloc(EyePosition, this.viewDistance, this._candidates, this.targetMask, QueryTriggerInteraction.Collide);
        for (var i = 0; i < count; i++)
        {
            var candidate = this._candidates[i];
            this._candidates[i] = null;
            if (!candidate.TryGetComponent<PerceptionTarget>(out var target)) continue;
            if (TryGetMemory(target, out var known) && known.VisibleNow) continue;
            if (!CanSee(target)) continue;

            var index = Remember(target, target.transform.position, now);
            this._memory[index].VisibleNow = true;
            this._memory[index].LastSeenTime = now;
        }
    }

    private void OnNoise(NoiseEvent noise)
    {
        var radius = noise.Radius * this.hearingMultiplier;
        if ((noise.Position - transform.position).sqrMagnitude > radius * radius) return;

        var now = Time.time;
        if (noise.Source != null && noise.Source.IsTargetable)
        {
            var index = Remember(noise.Source, noise.Position, now);
            this._memory[index].LastHeardTime = now;
            return;
        }

        this._noisePoint = noise.Position;
        this._noiseTime = now;
        this._hasNoise = true;
    }

    #endregion

    #region Memory

    public PerceivedTarget GetMemory(int index) => this._memory[index];

    public bool TryGetMemory(PerceptionTarget target, out PerceivedTarget memory)
    {
        var index = IndexOf(target);
        memory = index >= 0 ? this._memory[index] : default;
        return index >= 0;
    }

    public int Remember(PerceptionTarget target, Vector3 position, float now)
    {
        var index = IndexOf(target);
        if (index < 0) index = AllocateSlot();

        ref var entry = ref this._memory[index];
        if (entry.Target != target)
        {
            entry = default;
            entry.Target = target;
            entry.LastSeenTime = float.NegativeInfinity;
            entry.LastHeardTime = float.NegativeInfinity;
        }

        entry.LastKnownPosition = position;
        entry.LastSensedTime = now;
        return index;
    }

    public void Forget(PerceptionTarget target)
    {
        var index = IndexOf(target);
        if (index >= 0) RemoveAt(index);
    }

    public void ForgetAll()
    {
        for (var i = 0; i < this._memoryCount; i++) this._memory[i] = default;
        this._memoryCount = 0;
        this._hasNoise = false;
    }

    public bool TryConsumeNoise(float maxAge, out Vector3 point)
    {
        point = this._noisePoint;
        if (!this._hasNoise) return false;

        this._hasNoise = false;
        return Time.time - this._noiseTime <= maxAge;
    }

    private int IndexOf(PerceptionTarget target)
    {
        for (var i = 0; i < this._memoryCount; i++)
        {
            if (this._memory[i].Target == target) return i;
        }

        return -1;
    }

    private int AllocateSlot()
    {
        if (this._memoryCount < this._memory.Length) return this._memoryCount++;

        var oldest = 0;
        for (var i = 1; i < this._memoryCount; i++)
        {
            if (this._memory[i].LastSensedTime < this._memory[oldest].LastSensedTime) oldest = i;
        }

        return oldest;
    }

    private void ExpireMemory(float now)
    {
        for (var i = this._memoryCount - 1; i >= 0; i--)
        {
            var entry = this._memory[i];
            var gone = entry.Target == null || !entry.Target.isActiveAndEnabled || !entry.Target.IsTargetable;
            if (gone || now - entry.LastSensedTime > this.memoryDuration) RemoveAt(i);
        }
    }

    private void RemoveAt(int index)
    {
        var last = this._memoryCount - 1;
        this._memory[index] = this._memory[last];
        this._memory[last] = default;
        this._memoryCount = last;
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        var eye = EyePosition;
        var forward = EyeForward;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(eye, this.proximityRadius);
        var half = this.viewAngle * 0.5f;
        Gizmos.DrawLine(eye, eye + Quaternion.AngleAxis(-half, Vector3.up) * forward * this.viewDistance);
        Gizmos.DrawLine(eye, eye + Quaternion.AngleAxis(half, Vector3.up) * forward * this.viewDistance);

        if (this._memory == null) return;

        for (var i = 0; i < this._memoryCount; i++)
        {
            Gizmos.color = this._memory[i].VisibleNow ? Color.red : Color.cyan;
            Gizmos.DrawLine(eye, this._memory[i].LastKnownPosition);
        }
    }

    #endregion
}
