using System;
using System.Collections;
using UnityEngine;

public enum HealthState
{
    Alive,
    Downed,
    Dead
}

[Serializable]
public struct DamageResistance
{
    [SerializeField] private DamageType type;
    [SerializeField] private float multiplier;

    public DamageType Type => this.type;
    public float Multiplier => this.multiplier;
}

public class Health : MonoBehaviour
{
    #region Fields

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private CombatTeam team = CombatTeam.Enemies;
    [SerializeField] private CombatRules rules;
    [SerializeField] private DamageEventChannel damageChannel;
    [SerializeField] private DamageResistance[] resistances;

    [Header("Invulnerability")]
    [SerializeField] private float invulnerabilityAfterHit;
    [SerializeField] private float invulnerabilityAfterRevive = 2f;

    [Header("Downed")]
    [SerializeField] private bool canBeDowned;
    [SerializeField] private float bleedOutSeconds = 30f;
    [SerializeField, Range(0.05f, 1f)] private float reviveHealthFraction = 0.3f;

    private float _current;
    private HealthState _state;
    private float _invulnerableUntil;
    private Coroutine _bleedOut;
    private WaitForSeconds _bleedOutWait;

    public event Action<DamageInfo, float> Damaged;
    public event Action<float, float> HealthChanged;
    public event Action<HealthState, HealthState> StateChanged;
    public event Action<DamageInfo> Died;

    #endregion

    #region Properties

    public float Current => this._current;
    public float Max => this.maxHealth;
    public float Normalized => this.maxHealth > 0f ? this._current / this.maxHealth : 0f;
    public HealthState State => this._state;
    public bool IsAlive => this._state == HealthState.Alive;
    public bool IsInvulnerable => Time.time < this._invulnerableUntil;
    public CombatTeam Team => this.team;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._current = this.maxHealth;
        this._bleedOutWait = new WaitForSeconds(this.bleedOutSeconds);
    }

    private void OnDisable()
    {
        if (this._bleedOut == null) return;

        StopCoroutine(this._bleedOut);
        this._bleedOut = null;
    }

    #endregion

    #region Damage And Healing

    public float ApplyDamage(in DamageInfo info)
    {
        if (this._state != HealthState.Alive || info.Amount <= 0f) return 0f;
        if (!info.IgnoresInvulnerability && IsInvulnerable) return 0f;

        var amount = info.Amount * ResistanceFor(info.Type);
        var isSelf = info.Source == gameObject;
        if (this.rules != null && !this.rules.TryScale(info.SourceTeam, this.team, isSelf, ref amount)) return 0f;
        if (amount <= 0f) return 0f;

        var previous = this._current;
        this._current = Mathf.Max(0f, this._current - amount);
        var applied = previous - this._current;
        var lethal = this._current <= 0f;
        if (this.invulnerabilityAfterHit > 0f) SetInvulnerable(this.invulnerabilityAfterHit);

        Damaged?.Invoke(info, applied);
        HealthChanged?.Invoke(previous, this._current);
        if (this.damageChannel != null) this.damageChannel.Raise(new DamageEvent(this, info, applied, lethal));

        if (!lethal) return applied;

        if (this.canBeDowned) EnterDowned();
        else Die(info);
        return applied;
    }

    public float Heal(float amount)
    {
        if (this._state != HealthState.Alive || amount <= 0f) return 0f;

        var previous = this._current;
        this._current = Mathf.Min(this.maxHealth, this._current + amount);
        if (this._current > previous) HealthChanged?.Invoke(previous, this._current);
        return this._current - previous;
    }

    public void SetInvulnerable(float seconds) => this._invulnerableUntil = Mathf.Max(this._invulnerableUntil, Time.time + seconds);

    private float ResistanceFor(DamageType type)
    {
        if (type == DamageType.True || this.resistances == null) return 1f;

        for (var i = 0; i < this.resistances.Length; i++)
        {
            if (this.resistances[i].Type == type) return Mathf.Max(0f, this.resistances[i].Multiplier);
        }

        return 1f;
    }

    #endregion

    #region Death And Revive

    public void Kill(GameObject source)
    {
        if (this._state == HealthState.Dead) return;

        var info = new DamageInfo(this._current, DamageType.True, source, CombatTeam.Neutral, transform.position, Vector3.zero, true);
        var previous = this._current;
        this._current = 0f;
        if (previous > 0f) HealthChanged?.Invoke(previous, 0f);
        Die(info);
    }

    public bool Revive(float healthFraction = -1f)
    {
        if (this._state != HealthState.Downed) return false;

        StopBleedOut();
        var fraction = healthFraction > 0f ? healthFraction : this.reviveHealthFraction;
        this._current = Mathf.Max(1f, this.maxHealth * fraction);
        SetInvulnerable(this.invulnerabilityAfterRevive);
        HealthChanged?.Invoke(0f, this._current);
        SetState(HealthState.Alive);
        return true;
    }

    public void ResetHealth()
    {
        StopBleedOut();
        var previous = this._current;
        this._current = this.maxHealth;
        this._invulnerableUntil = 0f;
        HealthChanged?.Invoke(previous, this._current);
        SetState(HealthState.Alive);
    }

    public void ApplyNetworkState(float current, HealthState state)
    {
        var previous = this._current;
        this._current = Mathf.Clamp(current, 0f, this.maxHealth);
        if (!Mathf.Approximately(previous, this._current)) HealthChanged?.Invoke(previous, this._current);
        SetState(state);
    }

    private void EnterDowned()
    {
        SetState(HealthState.Downed);
        StopBleedOut();
        this._bleedOut = StartCoroutine(BleedOut());
    }

    private void Die(in DamageInfo info)
    {
        StopBleedOut();
        SetState(HealthState.Dead);
        Died?.Invoke(info);
    }

    private IEnumerator BleedOut()
    {
        yield return this._bleedOutWait;
        this._bleedOut = null;
        var info = new DamageInfo(0f, DamageType.True, null, CombatTeam.Neutral, transform.position, Vector3.zero, true);
        Die(info);
    }

    private void StopBleedOut()
    {
        if (this._bleedOut == null) return;

        StopCoroutine(this._bleedOut);
        this._bleedOut = null;
    }

    private void SetState(HealthState next)
    {
        if (next == this._state) return;

        var previous = this._state;
        this._state = next;
        StateChanged?.Invoke(previous, next);
    }

    #endregion
}
