# Damage events and feedback

Read when wiring health bars, damage numbers, hit sounds, hit stop, camera shake or VFX to damage events.

## Two event paths

| Path | Scope | Use for |
|---|---|---|
| `Health` C# events (`Damaged`, `HealthChanged`, `StateChanged`, `Died`) | One character | Its own health bar, hit reaction animation, flash material, AI threat, loot drop on death |
| `DamageEventChannel` (ScriptableObject asset with `event Action<DamageEvent> Raised`) | Global | Damage numbers, hit sounds, combat log, stats and achievements, screen shake when the local player is hit |

Both are plain C# events: zero allocation per raise when the payload is a struct and listeners are method groups subscribed once. Avoid `UnityEvent` on hot damage paths if many listeners are wired in the Inspector; it is fine for designer hooks on rare events (boss died).

ScriptableObject channel rules:

- Create one asset (`Create > Combat > Damage Event Channel`) and assign it to every `Health` and every listener.
- Listeners subscribe in `OnEnable` and unsubscribe in `OnDisable`. The asset outlives scenes; a destroyed listener that forgot to unsubscribe throws `MissingReferenceException` on the next raise.
- With domain reload disabled, subscriptions on the asset can survive exiting Play mode in the Editor; unsubscribing in `OnDisable` covers it.

## Health bar

```csharp
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private Image fill;

    private void OnEnable()
    {
        this.health.HealthChanged += this.OnHealthChanged;
        this.fill.fillAmount = this.health.Normalized;
    }

    private void OnDisable() => this.health.HealthChanged -= this.OnHealthChanged;

    private void OnHealthChanged(float previous, float current) => this.fill.fillAmount = this.health.Normalized;
}
```

`HealthChanged` also fires on clients from `ApplyNetworkState`, so the same bar works in co-op.

## Damage numbers and hit sounds

```csharp
using UnityEngine;
using UnityEngine.Pool;

public class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private DamageEventChannel channel;
    [SerializeField] private DamageNumber prefab;

    private ObjectPool<DamageNumber> _pool;

    private void Awake()
    {
        this._pool = new ObjectPool<DamageNumber>(
            () => Instantiate(this.prefab),
            number => number.gameObject.SetActive(true),
            number => number.gameObject.SetActive(false),
            number => Destroy(number.gameObject),
            true, 16, 64);
    }

    private void OnEnable() => this.channel.Raised += this.OnDamage;

    private void OnDisable() => this.channel.Raised -= this.OnDamage;

    private void OnDamage(DamageEvent damageEvent)
    {
        if (damageEvent.Applied <= 0f) return;

        var number = this._pool.Get();
        number.Show(damageEvent.Info.Point, damageEvent.Applied, damageEvent.Lethal, this._pool);
    }
}

public class DamageNumber : MonoBehaviour
{
    private IObjectPool<DamageNumber> _pool;
    private float _releaseTime;

    public void Show(Vector3 position, float amount, bool lethal, IObjectPool<DamageNumber> pool)
    {
        this._pool = pool;
        transform.position = position;
        this._releaseTime = Time.time + (lethal ? 1.2f : 0.8f);
    }

    private void Update()
    {
        transform.position += Vector3.up * Time.deltaTime;
        if (Time.time >= this._releaseTime) this._pool.Release(this);
    }
}
```

Format the number into a cached `TMP_Text` inside `Show` with the numeric format overload `SetText("{0:0}", amount)` instead of string concatenation or `ToString()`, which allocate a string per hit. Hit sounds: one listener on the channel picks a clip by `DamageType` and surface, plays it through the pooled audio system from `audio-playback-system`.

## Feel: hit stop, shake, flash

- **Hit stop** (freeze frames): pause the attacker's and victim's Animators (`animator.speed = 0` for 50-100 ms), not `Time.timeScale`, so co-op peers and UI keep running. Never change `Time.timeScale` from combat code in multiplayer.
- **Camera shake**: only for the local player's own hits and hurts; trigger a Cinemachine impulse (see `cinemachine-cameras`) from a listener that checks the victim or source is the local player.
- **Hit flash**: a `MaterialPropertyBlock` color pulse on the victim's renderers from the `Damaged` event; no material instances.
- **VFX**: spawn pooled impact effects at `DamageInfo.Point` oriented by `-DamageInfo.Direction` (see `unity-particle-system`).

## Co-op

Feedback is cosmetic and runs on every peer. On the server (and host) the events fire directly from `Health`. On clients, `NetworkHealth` (see `coop-networking.md`) re-raises the channel from a feedback RPC and calls `Health.ApplyNetworkState`, so listeners stay identical across single player, host and client. Keep feedback listeners free of gameplay side effects (no damage, no loot) so running them on clients is safe.
