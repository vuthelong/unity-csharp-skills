using System;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Patrol,
    Investigate,
    Chase,
    Attack,
    Return,
    Dead
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBrain : MonoBehaviour
{
    #region Fields

    private enum AttackPhase
    {
        None,
        Windup,
        Recovery
    }

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int AttackingId = Animator.StringToHash("Attacking");

    private static int BudgetFrame = -1;
    private static int TicksThisFrame;

    [SerializeField] private Perception perception;
    [SerializeField] private ThreatTable threatTable;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 4.5f;
    [SerializeField] private float repathDistance = 1f;
    [SerializeField] private float arriveTolerance = 0.3f;

    [Header("Patrol And Investigate")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWait = 2f;
    [SerializeField] private float investigateWait = 4f;
    [SerializeField] private float noiseMaxAge = 2f;

    [Header("Leash")]
    [SerializeField] private float leashRadius = 25f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float strikeRangeTolerance = 0.75f;
    [SerializeField] private float windup = 0.6f;
    [SerializeField] private float recovery = 0.8f;
    [SerializeField] private float cooldown = 1.5f;
    [SerializeField] private float turnSpeed = 360f;

    [Header("Threat")]
    [SerializeField] private float sightThreatPerSecond = 10f;
    [SerializeField] private float hearingThreat = 3f;

    [Header("Tick Rate")]
    [SerializeField] private float combatInterval = 0.1f;
    [SerializeField] private float nearInterval = 0.2f;
    [SerializeField] private float farInterval = 0.75f;
    [SerializeField] private float sleepInterval = 2f;
    [SerializeField] private float nearDistance = 20f;
    [SerializeField] private float sleepDistance = 80f;
    [SerializeField] private int maxTicksPerFrame = 8;

    private NavMeshAgent _agent;
    private EnemyState _state;
    private AttackPhase _attackPhase;
    private PerceptionTarget _attackTarget;
    private Vector3 _home;
    private Quaternion _homeRotation;
    private Vector3 _lastDestination;
    private float _nextTickTime;
    private float _lastTickTime;
    private float _waitUntil;
    private float _attackPhaseEnd;
    private float _nextAttackTime;
    private int _patrolIndex;
    private bool _patrolIssued;

    public event Action<EnemyState, EnemyState> StateChanged;
    public event Action<PerceptionTarget> AttackStarted;
    public event Action<PerceptionTarget> AttackStrike;

    #endregion

    #region Properties

    public EnemyState State => this._state;
    public PerceptionTarget CurrentTarget => this.threatTable.CurrentTarget;
    public bool IsLeashing => this._state == EnemyState.Return;
    public Vector3 Home => this._home;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._agent = GetComponent<NavMeshAgent>();
        if (this.perception == null) this.perception = GetComponent<Perception>();
        if (this.threatTable == null) this.threatTable = GetComponent<ThreatTable>();
        if (this.perception != null && this.threatTable != null) return;

        Debug.LogError($"{nameof(EnemyBrain)} needs {nameof(Perception)} and {nameof(ThreatTable)}.", this);
        enabled = false;
    }

    private void OnEnable() => Initialize();

    private void Update()
    {
        if (this._state == EnemyState.Dead) return;

        var now = Time.time;
        if (this._state == EnemyState.Attack) UpdateAttack(now);
        UpdateAnimator();

        if (now < this._nextTickTime || !TryConsumeTickBudget()) return;

        Tick(now);
    }

    #endregion

    #region Lifecycle Control

    public void ResetBrain(Vector3 home, Quaternion rotation)
    {
        this._agent.enabled = true;
        this._agent.Warp(home);
        transform.rotation = rotation;
        this.threatTable.Clear();
        this.perception.ForgetAll();
        this._patrolIndex = 0;
        this._state = EnemyState.Idle;
        Initialize();
    }

    public void Kill()
    {
        if (this._state == EnemyState.Dead) return;

        EndAttack();
        if (this._agent.isOnNavMesh) this._agent.ResetPath();
        this._agent.enabled = false;
        this.threatTable.Clear();
        SetState(EnemyState.Dead);
    }

    public void ForceTarget(PerceptionTarget target, float threat)
    {
        if (this._state == EnemyState.Dead || this._state == EnemyState.Return) return;

        this.perception.Remember(target, target.transform.position, Time.time);
        this.threatTable.AddThreat(target, threat);
    }

    private void Initialize()
    {
        this._home = transform.position;
        this._homeRotation = transform.rotation;
        this._lastTickTime = Time.time;
        this._nextTickTime = Time.time + UnityEngine.Random.Range(0f, this.nearInterval);
        this._nextAttackTime = 0f;
        this._attackPhase = AttackPhase.None;
        this._attackTarget = null;
        this._patrolIssued = false;
        SetState(HasPatrol() ? EnemyState.Patrol : EnemyState.Idle);
    }

    #endregion

    #region Decision Tick

    private void Tick(float now)
    {
        var deltaTime = now - this._lastTickTime;
        var previousTick = this._lastTickTime;
        this._lastTickTime = now;

        this.perception.Tick(now);
        if (this._state != EnemyState.Return) FeedThreat(deltaTime, previousTick);
        this.threatTable.Tick(deltaTime, transform.position, now);

        switch (this._state)
        {
            case EnemyState.Idle:
            case EnemyState.Patrol:
                TickCalm(now);
                break;
            case EnemyState.Investigate:
                TickInvestigate(now);
                break;
            case EnemyState.Chase:
                TickChase(now);
                break;
            case EnemyState.Return:
                TickReturn();
                break;
        }

        this._nextTickTime = now + ComputeInterval();
    }

    private void FeedThreat(float deltaTime, float previousTick)
    {
        for (var i = 0; i < this.perception.MemoryCount; i++)
        {
            var memory = this.perception.GetMemory(i);
            if (memory.VisibleNow) this.threatTable.AddThreat(memory.Target, this.sightThreatPerSecond * deltaTime);
            if (memory.LastHeardTime > previousTick) this.threatTable.AddThreat(memory.Target, this.hearingThreat);
        }
    }

    private void TickCalm(float now)
    {
        if (TryEngage()) return;
        if (this.perception.TryConsumeNoise(this.noiseMaxAge, out var noise))
        {
            BeginInvestigate(noise);
            return;
        }

        if (this._state != EnemyState.Patrol) return;
        if (!this._patrolIssued)
        {
            this._patrolIssued = MoveTo(this.patrolPoints[this._patrolIndex].position, this.walkSpeed, true);
            return;
        }

        if (!HasArrived()) return;
        if (this._waitUntil <= 0f)
        {
            this._waitUntil = now + this.patrolWait;
            return;
        }

        if (now < this._waitUntil) return;

        this._patrolIndex = (this._patrolIndex + 1) % this.patrolPoints.Length;
        this._waitUntil = 0f;
        this._patrolIssued = MoveTo(this.patrolPoints[this._patrolIndex].position, this.walkSpeed, true);
    }

    private void TickInvestigate(float now)
    {
        if (TryEngage()) return;
        if (this.perception.TryConsumeNoise(this.noiseMaxAge, out var noise)) BeginInvestigate(noise);
        if (!HasArrived()) return;

        if (this._waitUntil <= 0f)
        {
            this._waitUntil = now + this.investigateWait;
            return;
        }

        if (now >= this._waitUntil) BeginReturn();
    }

    private void TickChase(float now)
    {
        var target = this.threatTable.CurrentTarget;
        if (target == null)
        {
            BeginInvestigate(this._lastDestination);
            return;
        }

        if (IsBeyondLeash())
        {
            BeginReturn();
            return;
        }

        if (!this.perception.TryGetMemory(target, out var memory))
        {
            this.threatTable.Remove(target);
            BeginInvestigate(this._lastDestination);
            return;
        }

        if (!memory.VisibleNow)
        {
            MoveTo(memory.LastKnownPosition, this.runSpeed, false);
            if (!HasArrived()) return;

            this.perception.Forget(target);
            this.threatTable.Remove(target);
            BeginInvestigate(memory.LastKnownPosition);
            return;
        }

        var inRange = (target.transform.position - transform.position).sqrMagnitude <= this.attackRange * this.attackRange;
        if (inRange && now >= this._nextAttackTime)
        {
            BeginAttack(target, now);
            return;
        }

        MoveTo(target.transform.position, this.runSpeed, false);
    }

    private void TickReturn()
    {
        if (!HasArrived()) return;

        transform.rotation = this._homeRotation;
        SetState(HasPatrol() ? EnemyState.Patrol : EnemyState.Idle);
    }

    private bool TryEngage()
    {
        if (this.threatTable.CurrentTarget == null) return false;

        this._waitUntil = 0f;
        SetState(EnemyState.Chase);
        return true;
    }

    private void BeginInvestigate(Vector3 point)
    {
        this._waitUntil = 0f;
        MoveTo(point, this.walkSpeed, true);
        SetState(EnemyState.Investigate);
    }

    private void BeginReturn()
    {
        this.threatTable.Clear();
        this.perception.ForgetAll();
        this._waitUntil = 0f;
        MoveTo(this._home, this.runSpeed, true);
        SetState(EnemyState.Return);
    }

    #endregion

    #region Attack

    private void BeginAttack(PerceptionTarget target, float now)
    {
        this._attackTarget = target;
        this._attackPhase = AttackPhase.Windup;
        this._attackPhaseEnd = now + this.windup;
        this._agent.isStopped = true;
        this._agent.updateRotation = false;
        SetState(EnemyState.Attack);
        AttackStarted?.Invoke(target);
    }

    private void UpdateAttack(float now)
    {
        FaceAttackTarget();
        if (now < this._attackPhaseEnd) return;

        if (this._attackPhase == AttackPhase.Windup)
        {
            if (IsAttackTargetInStrikeRange()) AttackStrike?.Invoke(this._attackTarget);
            this._attackPhase = AttackPhase.Recovery;
            this._attackPhaseEnd = now + this.recovery;
            return;
        }

        this._nextAttackTime = now + this.cooldown;
        EndAttack();
        SetState(EnemyState.Chase);
    }

    private void EndAttack()
    {
        this._attackPhase = AttackPhase.None;
        this._attackTarget = null;
        if (!this._agent.enabled) return;

        this._agent.updateRotation = true;
        if (this._agent.isOnNavMesh) this._agent.isStopped = false;
    }

    private void FaceAttackTarget()
    {
        if (this._attackTarget == null) return;

        var direction = this._attackTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        var look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, this.turnSpeed * Time.deltaTime);
    }

    private bool IsAttackTargetInStrikeRange()
    {
        if (this._attackTarget == null || !this._attackTarget.IsTargetable) return false;

        var reach = this.attackRange + this.strikeRangeTolerance;
        return (this._attackTarget.transform.position - transform.position).sqrMagnitude <= reach * reach;
    }

    #endregion

    #region Navigation Helpers

    private bool MoveTo(Vector3 destination, float speed, bool force)
    {
        if (!this._agent.isOnNavMesh) return false;

        this._agent.speed = speed;
        var moved = (destination - this._lastDestination).sqrMagnitude >= this.repathDistance * this.repathDistance;
        if (!force && !moved && (this._agent.hasPath || this._agent.pathPending)) return true;

        this._lastDestination = destination;
        return this._agent.SetDestination(destination);
    }

    private bool HasArrived()
    {
        if (!this._agent.isOnNavMesh || this._agent.pathPending) return false;

        return this._agent.remainingDistance <= this._agent.stoppingDistance + this.arriveTolerance;
    }

    private bool HasPatrol() => this.patrolPoints != null && this.patrolPoints.Length > 0;

    private bool IsBeyondLeash() => (transform.position - this._home).sqrMagnitude > this.leashRadius * this.leashRadius;

    #endregion

    #region State And Scheduling

    private void SetState(EnemyState next)
    {
        var previous = this._state;
        this._state = next;
        if (next == EnemyState.Patrol && previous != EnemyState.Patrol)
        {
            this._waitUntil = 0f;
            this._patrolIssued = MoveTo(this.patrolPoints[this._patrolIndex].position, this.walkSpeed, true);
        }

        if (previous != next) StateChanged?.Invoke(previous, next);
    }

    private float ComputeInterval()
    {
        if (this._state == EnemyState.Chase || this._state == EnemyState.Attack) return this.combatInterval;
        if (!PerceptionTarget.TryGetNearestPlayerSqrDistance(transform.position, out var sqr)) return this.sleepInterval;
        if (sqr <= this.nearDistance * this.nearDistance) return this.nearInterval;

        return sqr <= this.sleepDistance * this.sleepDistance ? this.farInterval : this.sleepInterval;
    }

    private bool TryConsumeTickBudget()
    {
        var frame = Time.frameCount;
        if (frame != BudgetFrame)
        {
            BudgetFrame = frame;
            TicksThisFrame = 0;
        }

        if (TicksThisFrame >= this.maxTicksPerFrame) return false;

        TicksThisFrame++;
        return true;
    }

    private void UpdateAnimator()
    {
        if (this.animator == null) return;

        var speed = this._agent.enabled ? this._agent.velocity.magnitude : 0f;
        this.animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);
        this.animator.SetBool(AttackingId, this._attackPhase != AttackPhase.None);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        BudgetFrame = -1;
        TicksThisFrame = 0;
    }

    #endregion
}
