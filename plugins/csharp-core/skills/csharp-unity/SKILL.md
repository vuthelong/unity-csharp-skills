---
name: csharp-unity
description: Writes and refactors Unity 6 C# code to a consistent house style (naming, `this.` on fields, `var`, `#region` layout, early return, no LINQ) with GC-free, lifecycle-correct patterns. Use for any Unity C# authoring task - MonoBehaviour, ScriptableObject, editor scripts, custom inspectors, game systems, state machines, object pooling, events, coroutines, async/await (UniTask or Awaitable), serialization ([SerializeField], [SerializeReference], [FormerlySerializedAs]), Unity Test Framework tests, and fixing obsolete APIs (FindObjectOfType, Rigidbody.velocity, GetInstanceID). Triggers on "write a script", "make a component", "refactor this MonoBehaviour", SOLID/patterns in Unity, GC allocations, object pooling. For reviewing existing code for bugs and leaks use review-unity-csharp; for compile errors, domain reload and editing scene/asset files use unity-editor-safety.
license: MIT
metadata:
  category: csharp-core
  sources: "github.com/vuthelong/unity-csharp-skills (original)"
  unity: "6000.0+"
---

# C# for Unity 6

## Workflow

1. Detect the project context before writing code:
   - Unity version in `ProjectSettings/ProjectVersion.txt` (API availability differs across 6000.x minors).
   - Async library: `com.cysharp.unitask` in `Packages/manifest.json` means UniTask; otherwise use the built-in `Awaitable`.
   - Input handling: `activeInputHandler` in `ProjectSettings/ProjectSettings.asset` (0 = legacy, 1 = Input System, 2 = both).
   - Style: always apply the house style below, regardless of `.editorconfig` or the style of surrounding files.
   - Assembly definitions: put new code in the asmdef that owns the folder; editor code goes in an `Editor` folder or an Editor-only asmdef.
2. Write the code following the rules below. Prefer the smallest set of components with one responsibility each.
3. Check Unity 6 API drift against [references/unity6-api-changes.md](references/unity6-api-changes.md) before using any API you are not sure about.
4. Make sure it compiles: Unity 6 compiles with C# 9. See "Language version" below.
5. After saving `.cs` files, verify the Editor recompiled cleanly (see `unity-editor-safety`).

## Core principles

1. Early return: guard at the top, no deep nesting.
2. One responsibility per class; depend on interfaces or serialized references, not global lookups.
3. No comments that restate the code. Rename before you comment. Only comment a non-obvious *why*.
4. Optimize by default: no per-frame allocations, cached component lookups, no per-frame `Find*`.
5. No LINQ anywhere, including editor tooling, build scripts and tests. Plain `for`/`foreach` loops.
6. `this.` on every instance-field access; never on properties, locals, parameters or statics.
7. `var` for locals whenever the right-hand side makes the type obvious.

## Naming

| Element | Convention | Example |
|---|---|---|
| Class / struct / enum | PascalCase | `EnemyController` |
| Interface | `I` + PascalCase | `IDamageable` |
| Public field / property | PascalCase | `MaxHealth` |
| Private non-serialized field | `_camelCase` | `_currentHealth` |
| `[SerializeField] private` field | camelCase, no underscore | `moveSpeed` |
| Const / static readonly | PascalCase | `MaxSpeed`, `TickDelay` |
| Method | PascalCase | `TakeDamage()` |
| Local / parameter | camelCase | `damageAmount` |
| Async method | `Async` suffix | `LoadLevelAsync()` |

Serialized fields are always `[SerializeField] private`, never `public`, never without `private`, never with `_`:

```csharp
[SerializeField] private Color rightColor;
```

## `this.` rule

| Target | `this.`? |
|---|---|
| Private field (`_name`) | Always |
| `[SerializeField] private` field | Always |
| Public field | Always |
| Property | Never |
| Local / parameter | Never |
| Static field | Never; use `ClassName.Field` or the bare name inside the class |

```csharp
public int Current => this._health;
public void SetSpeed(float speed) => this._speed = speed;
```

## `var` rule

```csharp
for (var i = 0; i < count; i++) { }
var ratio = (point.x - xMin) / width;
```

Keep the explicit type when `var` hides meaning (`int result = Compute();` where the return type is unclear). Fields and properties always declare explicit types.

## `#region` layout

Every class wraps members in top-level regions, in this order, with these exact names:

| # | Region | Contents |
|---|---|---|
| 1 | `Fields` | Constants, statics, serialized fields, private fields, events |
| 2 | `Properties` | All properties |
| 3 | `Unity Lifecycle` | `Awake`, `OnEnable`, `Start`, `Update`, `FixedUpdate`, `LateUpdate`, `OnDisable`, `OnDestroy` |
| 4 | `Public Methods` | Constructors and public non-Unity methods |
| 5 | `Private Methods` | Private / protected helpers |
| 6 | `Unity Callbacks` | Collision, trigger, animation events, UI callbacks, `OnValidate`, `OnDrawGizmos` |

Omit a region with no members. Never nest regions. Every class and struct uses regions, however small.

## Comments

- No method-header comments, except `/// <summary>` on public API consumed by other assemblies or packages.
- No "what" comments. A one-line `//` explaining a non-obvious *why* is allowed.
- No commented-out code and no `TODO` without an owner/issue.

## Early return

```csharp
public void TakeDamage(int amount)
{
    if (!this._isAlive || amount <= 0) return;

    this._health -= amount;
    if (this._health <= 0) Die();
}
```

## Unity rules

### Lookups and caching

- Cache `GetComponent` results in `Awake`. Use `TryGetComponent(out this._rb)` and fail loudly (`Debug.LogError(..., this)` + `enabled = false`) when a required component is missing, or add `[RequireComponent(typeof(Rigidbody))]`.
- Inject dependencies via `[SerializeField]` references, constructors on plain C# classes, or a DI container. Use `FindAnyObjectByType<T>()` (fastest, any instance) or `FindFirstObjectByType<T>()` only at startup. `FindObjectOfType` / `FindObjectsOfType` are obsolete in Unity 6.
- `Camera.main` is cached by Unity since 2020.2; calling it per frame is fine.
- Use `CompareTag(Tags.Player)` with string constants, never `tag == "Player"`.
- Cache `Animator.StringToHash` / `Shader.PropertyToID` results in `static readonly int` fields.

### Unity null semantics

`UnityEngine.Object` overrides `==` so destroyed objects compare equal to `null`. `?.`, `??`, `??=`, `is null` and `is not null` bypass that override and will touch destroyed objects. On Unity objects use `if (this.target != null)` or `if (this.target)`.

### Physics and timing

- Read input in `Update`; apply forces and `Rigidbody.MovePosition` in `FixedUpdate`.
- `Time.deltaTime` returns the fixed step inside `FixedUpdate`, so either works there; `Time.fixedDeltaTime` inside `Update` is always wrong.
- Unity 6 renamed `Rigidbody.velocity` to `linearVelocity`, `drag` to `linearDamping`, `angularDrag` to `angularDamping` (also on `Rigidbody2D`).
- Use non-allocating queries in hot paths: `Physics.RaycastNonAlloc`, `Physics.OverlapSphereNonAlloc` with a reused buffer.

### Coroutines

Cache yield instructions in static readonly fields; `yield return null` for one frame.

```csharp
private static readonly WaitForSeconds TickDelay = new(0.1f);

private IEnumerator TickRoutine()
{
    while (true)
    {
        Tick();
        yield return TickDelay;
    }
}
```

Coroutines stop when the GameObject is deactivated or destroyed, but not when only the component is disabled. Store the `Coroutine` handle and stop by handle, never by string.

### Async

Prefer async over coroutines for new code that returns values or chains work. Full guide with both libraries, cancellation and threading: [references/async.md](references/async.md).

- With UniTask: `UniTask`/`UniTask<T>` when awaited, `UniTaskVoid` + `.Forget()` for fire-and-forget.
- Without UniTask: `Awaitable`/`Awaitable<T>` (Unity 6 built-in). Never await the same `Awaitable` instance twice; instances are pooled.
- Never `async void` except for UI event handlers that must match a `void` signature, and then wrap the body in `try/catch`.
- Pass `destroyCancellationToken` into every await that can outlive the object.

### Object pooling

Use `UnityEngine.Pool.ObjectPool<T>` for anything spawned repeatedly:

```csharp
private ObjectPool<Bullet> _pool;

private void Awake()
{
    this._pool = new ObjectPool<Bullet>(
        createFunc: () => Instantiate(this.bulletPrefab),
        actionOnGet: b => b.gameObject.SetActive(true),
        actionOnRelease: b => b.gameObject.SetActive(false),
        actionOnDestroy: b => Destroy(b.gameObject),
        defaultCapacity: 20,
        maxSize: 200);
}
```

Reset all per-use state in `actionOnGet`/`actionOnRelease`. Pooled objects never receive `OnDestroy` between uses, so do cleanup in `OnDisable`. Also available: `ListPool<T>.Get()`, `HashSetPool<T>`, `DictionaryPool<TKey,TValue>` (dispose with `using`).

### Events

- Prefer C# `event Action<T>` for code-to-code subscriptions; `UnityEvent` only when designers wire it in the Inspector.
- Every `+=` / `AddListener` in `OnEnable` has a matching `-=` / `RemoveListener` in `OnDisable`.
- Never poll a flag in `Update` that a state change could raise as an event.

### Runtime-created assets

`new Material/Texture2D/Mesh/RenderTexture`, `Instantiate(material)`, `renderer.material` and `meshFilter.mesh` getters create native objects you must `Destroy` in `OnDestroy`. Use `sharedMaterial` / `sharedMesh` for reads, `MaterialPropertyBlock` or `Renderer.SetPropertyBlock` for per-instance tweaks.

### Statics and Enter Play Mode Options

With domain reload disabled (Project Settings > Editor > Enter Play Mode Settings), statics and static events survive between Play sessions. Reset them explicitly:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics()
{
    Instance = null;
    ScoreChanged = null;
}
```

### Input

If `activeInputHandler` is Input System only, any `UnityEngine.Input.*` call throws `InvalidOperationException`. Use `UnityEngine.InputSystem` (`InputAction`, `Keyboard.current`) in that case. Unity 6 templates default to the Input System package.

## Serialization essentials

- Unity serializes public fields and `[SerializeField]` fields of serializable types. It does not serialize properties, `Dictionary`, multidimensional or jagged arrays, interfaces, or `static`/`readonly`/`const` fields.
- Use `[SerializeReference]` for polymorphic or interface-typed fields; plain `[SerializeField]` slices to the declared type.
- Renaming a serialized field loses data on every existing asset unless you add `[FormerlySerializedAs("oldName")]`.
- Auto-property serialization: `[field: SerializeField] public int Damage { get; private set; }`. Its serialized name is `<Damage>k__BackingField`.

Rules, callbacks (`ISerializationCallbackReceiver`, `OnValidate`) and ScriptableObject runtime-mutation pitfalls: [references/serialization.md](references/serialization.md).

## Performance checklist

| Rule | Detail |
|---|---|
| No `Find*`, `GetComponent` in `Update` | Cache in `Awake` |
| No per-frame `new List<>`, closures, string concatenation, boxing | Preallocate, reuse, `ListPool<T>`, `StringBuilder` |
| No LINQ | Plain loops |
| `CompareTag` | No string alloc |
| Non-alloc physics queries | Reused buffers |
| `sealed` leaf classes | Allows devirtualization under IL2CPP |
| Structs for small short-lived data | Avoid mutating copies (see review-unity-csharp) |
| Empty `Update` / `LateUpdate` | Delete them; each one costs a native-to-managed call |

## Language version

Unity 6 compiles C# 9.0. These do not compile: file-scoped namespaces, `global using`, `record struct`, `required` members, raw string literals, list patterns, primary constructors on classes. `record` and `init` accessors need a shim:

```csharp
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
```

Records and `init`-only members are not Unity-serializable; use them only for plain C# data.

## General C#

- `readonly` for fields assigned only in a constructor; `sealed` on leaf classes.
- `switch` expressions over `if/else if` chains.
- `nameof()` instead of string literals in logs and reflection: `Debug.LogError($"{nameof(PlayerHealth)} missing Rigidbody", this);`.
- Pass `this` as the context argument of `Debug.Log*` so the console entry pings the object.
- Use `[Conditional("UNITY_EDITOR")]` or `#if UNITY_EDITOR` around editor-only code in runtime assemblies; a runtime script that references `UnityEditor` breaks player builds.

## Template

```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class Mover : MonoBehaviour
{
    #region Fields
    [SerializeField] private float speed = 5f;

    private Rigidbody _rb;
    private Vector3 _direction;
    #endregion

    #region Unity Lifecycle
    private void Awake() => this._rb = GetComponent<Rigidbody>();

    private void FixedUpdate()
    {
        if (this._direction == Vector3.zero) return;

        this._rb.MovePosition(this._rb.position + this._direction * (this.speed * Time.fixedDeltaTime));
    }
    #endregion

    #region Public Methods
    public void SetDirection(Vector3 direction) => this._direction = direction.normalized;
    #endregion
}
```

## References

- [references/unity6-api-changes.md](references/unity6-api-changes.md) - read when touching any API that may be obsolete or renamed in 6000.x.
- [references/async.md](references/async.md) - read when writing async code, cancellation, background threads or replacing coroutines.
- [references/serialization.md](references/serialization.md) - read when designing serialized data, ScriptableObjects, or renaming fields.
- [references/patterns.md](references/patterns.md) - read when implementing a state machine, event channel, command/undo, service locator, or a logic/MonoBehaviour split.
- [references/editor-scripting.md](references/editor-scripting.md) - read when writing custom inspectors, editor windows, menu items or asset-processing scripts.
- [references/testing.md](references/testing.md) - read when writing EditMode/PlayMode tests.

## Related skills

- `review-unity-csharp` - audit existing code for bugs, leaks and these conventions.
- `unity-editor-safety` - compile-error recovery, domain reload, safe edits of scenes/prefabs/.meta.
- `ui-uitk`, `ui-ugui`, `ui-imgui` - UI-specific code.
