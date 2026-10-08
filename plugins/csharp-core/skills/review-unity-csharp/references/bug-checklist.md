# Correctness bug checklist

Logic and lifecycle bugs. Leaks are in `leak-checklist.md`; style rules are in the `csharp-unity` skill.

## `?.` and pattern null checks on `UnityEngine.Object`

Unity overrides `==` so a destroyed object compares equal to `null` while the C# wrapper still exists. `?.`, `??`, `??=`, `is null` and `is not null` check the real reference and skip the override, so they call into destroyed objects (`MissingReferenceException`).

```csharp
this.target?.TakeDamage(10);

if (this.target != null) this.target.TakeDamage(10);
```

The first line is the bug; the second is the fix. Plain C# objects and interfaces that are not implemented by a `UnityEngine.Object` are safe with `?.`. An interface field holding a MonoBehaviour is not safe: cast or compare via `(this.damageable as Object) != null`.

## Missing component check

```csharp
this._rb = GetComponent<Rigidbody>();
this._rb.AddForce(Vector3.up);
```

Fix with `[RequireComponent(typeof(Rigidbody))]`, or:

```csharp
if (!TryGetComponent(out this._rb))
{
    Debug.LogError($"{nameof(Jumper)} requires a Rigidbody", this);
    enabled = false;
    return;
}
```

## Timestep misuse

- `Time.deltaTime` inside `FixedUpdate` is fine: Unity returns the fixed step there.
- `Time.fixedDeltaTime` inside `Update` is a bug: frame time differs from the physics step.
- Setting `Rigidbody.position`/`linearVelocity` or calling `MovePosition`/`AddForce` every frame from `Update` runs out of step with physics (jitter, inconsistent force at different frame rates). Read input in `Update`, apply in `FixedUpdate`. One-shot impulses (`ForceMode.Impulse`) from `Update` are acceptable.
- `Time.timeScale = 0` stops `FixedUpdate` and scaled timers; UI animations during pause need `Time.unscaledDeltaTime` / `WaitForSecondsRealtime`.

## Mutating a struct copy

```csharp
foreach (var enemy in this.enemyStates)
{
    enemy.ApplyDamage(10);
}
```

For `List<EnemyState>` where `EnemyState` is a struct the mutating method compiles but changes only the copy (a direct field write on the iteration variable is compile error CS1654). The same happens with `list[i].ApplyDamage(10)` and with mutating methods called on a property that returns a struct (`this.Config.Boost()`); direct field assignment through those (`list[i].health = 0`) is compile error CS1612.

```csharp
for (var i = 0; i < this.enemyStates.Count; i++)
{
    var state = this.enemyStates[i];
    state.health -= 10;
    this.enemyStates[i] = state;
}
```

Unity-specific: `transform.position.x = 5f` is a compile error for the same reason; `var p = transform.position; p.x = 5f; transform.position = p;`. Particle system modules (`ps.main`) are structs that proxy to the native object, so modifying a local copy of the module does apply.

## Modifying a collection during `foreach`

```csharp
foreach (var enemy in this._active)
{
    if (enemy.IsDead) this._active.Remove(enemy);
}
```

Throws `InvalidOperationException`. Iterate backwards with an index, or use `RemoveAll`. Watch for indirect modification: an event raised inside the loop whose handler adds/removes from the same list.

## Cross-object initialization order

Within one object Unity guarantees `Awake` -> `OnEnable` -> `Start`. Across objects, all `Awake`/`OnEnable` calls of objects in a loaded scene run before any `Start`, but the order among different objects' `Awake` calls is undefined. Reading another object's state in `Awake` is a latent bug. Fix: initialize self in `Awake`, read others in `Start`, or set `[DefaultExecutionOrder(-100)]` on the provider, or use an explicit bootstrap.

Objects instantiated at runtime run `Awake`/`OnEnable` immediately inside `Instantiate`, before it returns.

## Async lifetime bugs

- `async void` methods cannot be awaited; exceptions go to the Unity synchronization context (logged) but the caller never sees them and cannot react. Allowed only for Unity messages and fixed-signature UI handlers, with an internal `try/catch`.
- A `Task` that nobody awaits (`_ = LoadAsync();`) swallows its exception silently.
- An await without a lifetime token resumes after the object is destroyed and touches it (`MissingReferenceException`) or keeps running after exiting Play Mode in the Editor.
- Awaiting the same `Awaitable` twice is undefined; instances are pooled and reused.
- Blocking on async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) on the main thread deadlocks because continuations need the main thread.

## Main-thread-only API

Most `UnityEngine` API (GameObject, Component, Transform, assets, `Time`, `Application.persistentDataPath` on some platforms) throws `UnityException: ... can only be called from the main thread` or behaves unpredictably off the main thread. Flag Unity calls inside `Task.Run`, `Thread`, socket/IO callbacks, `Awaitable.BackgroundThreadAsync()` regions, and managed-object access inside job `Execute()`.

## Singleton issues

```csharp
public static GameManager Instance { get; private set; }
private void Awake() => Instance = this;
```

Reloading the scene creates a second instance and silently swaps `Instance`, orphaning subscribers of the old one. Fix: duplicate guard + `DontDestroyOnLoad` (root objects only) + clear `Instance` in `OnDestroy` when it is `this` + reset in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` hook.

## Coroutine stop mismatches

```csharp
this._routine = StartCoroutine(Tick());
StopCoroutine("Tick");
```

String-based stop only matches coroutines started with `StartCoroutine("Tick")`. Store the `Coroutine` handle and stop by handle. `StopCoroutine` must be called on the same MonoBehaviour that started it. Disabling only the component does not stop running coroutines; deactivating the GameObject does. `StartCoroutine` on an inactive GameObject does nothing except log an error.

## Static state and Enter Play Mode Options

Domain reload resets statics on recompile and, by default, on entering Play Mode. With domain reload disabled in Enter Play Mode Settings, statics, static events and singletons keep their values between Play sessions. Symptoms: duplicate event handlers, stale singleton pointing at a destroyed object, counters that start non-zero. Fix with a `SubsystemRegistration` reset hook for every mutable static. Static event handlers from Editor scripts must be re-subscribed in `[InitializeOnLoad]`.

## Exceptions inside Unity callbacks

Unity catches exceptions from `Update`, `OnTriggerEnter` and other messages, logs them, and keeps calling the method on later frames. A throw half-way through a multi-step mutation leaves the object inconsistent forever. Flag multi-step mutations that can throw before the state is consistent (validate first, then mutate).

## ScriptableObject runtime mutation

In the Editor, writing to a ScriptableObject asset's fields at runtime modifies the asset and the change survives exiting Play Mode (and is saved with the next `AssetDatabase.SaveAssets`). In builds the value resets every launch, so Editor and build behavior differ.

```csharp
this.itemData.currentStock -= 1;

var runtimeItem = Instantiate(this.itemData);
runtimeItem.currentStock -= 1;
```

## Serialization data loss

- Renaming a serialized field without `[FormerlySerializedAs("old")]` silently resets every prefab/scene/asset value to the default.
- Converting a field to `[field: SerializeField]` auto-property changes the serialized name to `<Name>k__BackingField`; needs `[field: FormerlySerializedAs("old")]`.
- Abstract/interface/polymorphic field marked `[SerializeField]` instead of `[SerializeReference]` slices or drops data.
- Moving or renaming a MonoBehaviour class so the file name no longer matches the class name breaks the script reference ("The associated script can not be loaded").
- `Dictionary` or property fields expected to persist but never serialized.

## `[Flags]` enums

Combining enum values with `|` without `[Flags]` compiles but `ToString()` and the Inspector show nonsense. Values that are not distinct powers of two collide (`A = 1, B = 2, C = 3` makes `A | B == C`). Use `1 << n` and include `None = 0`.

## Magic strings

`tag == "Enemey"` silently returns false forever. `CompareTag` with an undefined tag logs an error. Use constants for tags, layers (`LayerMask.NameToLayer` cached), scene names, and cache `Animator.StringToHash` / `Shader.PropertyToID` in static readonly ints.

## Input handling

With Active Input Handling set to Input System only, `UnityEngine.Input.GetAxis` and friends throw `InvalidOperationException` every frame. Check `ProjectSettings.asset` `activeInputHandler` before flagging or accepting legacy input code.

## Editor code in runtime assemblies

A runtime script with `using UnityEditor;` compiles in the Editor but breaks the player build. Require `#if UNITY_EDITOR` guards or move the code to an Editor assembly.

## Obsolete and removed API

Flag anything in the `csharp-unity` reference `unity6-api-changes.md`: `FindObjectOfType`, `FindObjectsOfType`, `Rigidbody.velocity`/`drag`/`angularDrag`, `PhysicMaterial`, `GraphicsSettings.renderPipelineAsset`, and `GetInstanceID()` which is a compile error (CS0619) on 6000.5+. Also flag C# 10+ syntax, which Unity 6's C# 9 compiler rejects.
