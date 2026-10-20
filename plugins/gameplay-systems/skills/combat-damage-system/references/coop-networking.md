# Co-op networking for combat (Netcode for GameObjects 2.x)

Read when the game uses Netcode for GameObjects: attack intent RPCs, server hit detection, `NetworkHealth`, hit feedback RPCs, revive requests and latency tolerance. NGO basics (authority modes, RPC naming, `InvokePermission` from 2.7.0) are in `netcode-for-gameobjects`.

## Authority split

| Thing | Who decides | Replicated by |
|---|---|---|
| Player movement, attack animation | Owner | `NetworkTransform` / `NetworkAnimator` (Authority Mode Owner) |
| "I attack now, aiming here" | Owner sends intent | `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]` |
| Hit detection, damage, death, revive | Server | `NetworkVariable`s in `NetworkHealth` |
| Enemy attacks | Server (`enemy-ai-behavior`) | Animator parameters + `NetworkHealth` |
| Damage numbers, sounds, VFX | Every peer, cosmetic | Unreliable feedback RPC to `SendTo.NotServer` (host already has local events) |

Clients never call `Health.ApplyDamage`. Their `MeleeHitDetector` components stay disabled; only the server's copy runs queries.

## NetworkHealth

```csharp
using Unity.Netcode;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private DamageEventChannel feedbackChannel;

    private readonly NetworkVariable<float> _current = new();
    private readonly NetworkVariable<HealthState> _state = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            this._current.Value = this.health.Current;
            this._state.Value = this.health.State;
            this.health.HealthChanged += this.OnServerHealthChanged;
            this.health.StateChanged += this.OnServerStateChanged;
            this.health.Damaged += this.OnServerDamaged;
            return;
        }

        this._current.OnValueChanged += this.OnCurrentReplicated;
        this._state.OnValueChanged += this.OnStateReplicated;
        this.health.ApplyNetworkState(this._current.Value, this._state.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            this.health.HealthChanged -= this.OnServerHealthChanged;
            this.health.StateChanged -= this.OnServerStateChanged;
            this.health.Damaged -= this.OnServerDamaged;
            return;
        }

        this._current.OnValueChanged -= this.OnCurrentReplicated;
        this._state.OnValueChanged -= this.OnStateReplicated;
    }

    private void OnServerHealthChanged(float previous, float current) => this._current.Value = current;

    private void OnServerStateChanged(HealthState previous, HealthState next) => this._state.Value = next;

    private void OnServerDamaged(DamageInfo info, float applied)
    {
        if (applied <= 0f) return;
        HitFeedbackRpc(info.Point, info.Direction, applied, info.Type, this.health.Current <= 0f);
    }

    [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
    private void HitFeedbackRpc(Vector3 point, Vector3 direction, float amount, DamageType type, bool lethal)
    {
        if (this.feedbackChannel == null) return;
        var info = new DamageInfo(amount, type, null, CombatTeam.Neutral, point, direction);
        this.feedbackChannel.Raise(new DamageEvent(this.health, info, amount, lethal));
    }

    private void OnCurrentReplicated(float previous, float current) => this.health.ApplyNetworkState(current, this._state.Value);

    private void OnStateReplicated(HealthState previous, HealthState next) => this.health.ApplyNetworkState(this._current.Value, next);
}
```

- Health lives in `NetworkVariable`s (server write, everyone read, late joiners get the current value). The hit feedback is a one-off RPC; late joiners do not need old damage numbers.
- `SendTo.NotServer` skips the host, which already raised the channel from `Health.ApplyDamage`, so the host does not show numbers twice.
- `Health.Damaged` does not fire on clients (`ApplyNetworkState` raises only `HealthChanged` / `StateChanged`); client-side listeners that need hit data use the channel.
- Bleed-out and i-frame timers run on the server's `Health` only.

## Player melee: intent from owner, hit on server

```csharp
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerMeleeNetwork : NetworkBehaviour
{
    private static readonly int AttackId = Animator.StringToHash("Attack");

    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private MeleeHitDetector melee;
    [SerializeField] private Health health;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float reach = 2.4f;
    [SerializeField] private float arcDegrees = 120f;
    [SerializeField] private float cooldown = 0.8f;
    [SerializeField] private float maxAimDeviation = 45f;

    private float _nextOwnerAttack;
    private float _nextServerAttack;

    public void RequestAttack()
    {
        if (!IsOwner || !this.health.IsAlive || Time.time < this._nextOwnerAttack) return;

        this._nextOwnerAttack = Time.time + this.cooldown;
        this.networkAnimator.SetTrigger(AttackId);
    }

    public void OnAttackImpact()
    {
        if (IsOwner) StrikeRpc(transform.forward);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void StrikeRpc(Vector3 aim)
    {
        var now = Time.time;
        if (!this.health.IsAlive || now < this._nextServerAttack) return;

        this._nextServerAttack = now + this.cooldown * 0.75f;
        aim.y = 0f;
        var forward = transform.forward;
        if (aim.sqrMagnitude < 0.01f || Vector3.Angle(forward, aim) > this.maxAimDeviation) aim = forward;
        this.melee.StrikeArc(transform.position + Vector3.up, aim.normalized, this.reach, this.arcDegrees, this.damage, DamageType.Physical);
    }
}
```

- `RequestAttack` is called by the owner's input; `OnAttackImpact` is an animation event on the owner's attack clip (it must live on the Animator's GameObject or be relayed). The owner's animation timing drives the hit, so the attack lands when the attacker sees the impact.
- The server validates: alive, cooldown (with slack for jitter: 75% of the client cooldown), aim close to the server's view of the facing. Everything else (who is in the arc, how much damage) is computed on the server.
- `NetworkAnimator.SetTrigger` is used instead of `Animator.SetTrigger` so remote peers see the swing (see `netcode-for-gameobjects`).
- The server copy of an owner-authoritative player is posed from replicated parameters; do not use bone-driven `BeginSwing` sweeps on the server for remote players unless their Animator runs with `AlwaysAnimate`. `StrikeArc` avoids the dependency.

## Hitscan in co-op

The owner sends `FireRpc(origin, direction)` with `InvokePermission = RpcInvokePermission.Owner`. The server clamps `origin` to within about 1 m of its copy of the muzzle, checks fire rate and ammo, runs `HitscanWeapon.Fire` (see `hit-detection.md`) and sends a cosmetic tracer with `[Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]` to everyone (the shooter included, or let the shooter draw its own tracer immediately and send to `NotOwner`).

## Revive requests

```csharp
using Unity.Netcode;
using UnityEngine;

public class ReviveNetwork : NetworkBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private float reviveSeconds = 4f;
    [SerializeField] private float maxDistance = 2.5f;

    private readonly NetworkVariable<float> _progress = new();
    private Health _reviver;

    public float Progress => this._progress.Value;

    [Rpc(SendTo.Server)]
    public void BeginReviveRpc(RpcParams rpcParams = default)
    {
        if (this.health.State != HealthState.Downed || this._reviver != null) return;
        if (!NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client)) return;
        if (client.PlayerObject == null || !client.PlayerObject.TryGetComponent<Health>(out var reviver)) return;
        if (!CanRevive(reviver)) return;

        this._reviver = reviver;
    }

    [Rpc(SendTo.Server)]
    public void CancelReviveRpc(RpcParams rpcParams = default)
    {
        if (this._reviver == null || this._reviver.GetComponent<NetworkObject>().OwnerClientId != rpcParams.Receive.SenderClientId) return;
        StopRevive();
    }

    private void Update()
    {
        if (!IsServer || this._reviver == null) return;
        if (this.health.State != HealthState.Downed || !CanRevive(this._reviver))
        {
            StopRevive();
            return;
        }

        this._progress.Value = Mathf.Min(1f, this._progress.Value + Time.deltaTime / this.reviveSeconds);
        if (this._progress.Value < 1f) return;

        this.health.Revive();
        StopRevive();
    }

    private bool CanRevive(Health reviver) =>
        reviver != this.health && reviver.IsAlive && (reviver.transform.position - transform.position).sqrMagnitude <= this.maxDistance * this.maxDistance;

    private void StopRevive()
    {
        this._reviver = null;
        this._progress.Value = 0f;
    }
}
```

The reviver calls `BeginReviveRpc` on the downed player's object, which it does not own, so the RPC keeps the default `InvokePermission` (Everyone) and the server validates the sender instead. The progress `NetworkVariable` drives both HUDs and late joiners.

## Latency tolerance for slow-paced co-op

This design does not rewind hitboxes (no lag compensation) by choice: it adds a history buffer per character and server-side rewinds, and for exploration/survival/RPG pacing the errors stay small if the design absorbs them.

- What a client sees of an enemy trails the server by roughly half the round trip plus `NetworkTransform` interpolation; what the server sees of a remote player trails that player's own view by about the same.
- **Player attacks enemies:** make reach and arcs generous (+0.3-0.5 m, 100-140 degrees) and keep enemies slow during their vulnerable windows. Melee hits feel fair at 100-150 ms round trip with these margins.
- **Enemies attack players:** telegraph with windups of 0.5 s or more; recheck range at the strike (`EnemyBrain` does). Extend dodge i-frames by roughly one round trip (the dodge RPC arrives late), or start the i-frames on the server from the dodge RPC and accept that.
- **Projectiles:** slow visible projectiles simulated on the server look correct to everyone; hitscan uses the server's view and may miss a target that looked hit on the shooter's screen. For hitscan-heavy designs, consider generous hurtboxes or client-reported hits that the server sanity-checks (distance, line of sight, fire rate) in friends-only co-op.
- Test with the Network Simulator at 150-200 ms round trip and 1-2% loss (see `netcode-for-gameobjects` testing reference).
