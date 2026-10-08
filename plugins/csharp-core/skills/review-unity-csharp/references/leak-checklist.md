# Memory and resource leak checklist

Two kinds of leak need different detection:

- **Managed (GC) leaks**: a reference keeps an object reachable past its lifetime (subscriptions, static collections, closures). A destroyed MonoBehaviour that is still referenced becomes a "leaked managed shell": the native side is gone but the C# object and everything it references stays in memory.
- **Native leaks**: a C# wrapper (`Texture2D`, `Mesh`, `Material`, `RenderTexture`, `NativeArray`, `GraphicsBuffer`, Addressables handle) owns native memory the GC does not see. Native memory grows while the managed heap looks flat.

Review technique: find each allocation/subscription, then its cleanup. No cleanup on every exit path = leak.

## Allocate / cleanup pairing table

| Allocates / subscribes | Must be paired with | Usually in |
|---|---|---|
| `source.Event += Handler` | `source.Event -= Handler` | `OnEnable` / `OnDisable` |
| `unityEvent.AddListener(x)` | `RemoveListener(x)` (`RemoveAllListeners` only on events you own) | `OnEnable` / `OnDisable` |
| `inputAction.performed += h` | `-= h`, and `Disable()`/`Dispose()` on actions you created | `OnEnable` / `OnDisable` |
| `SceneManager.sceneLoaded += h`, `Application.logMessageReceived += h`, `Application.quitting += h` | `-= h` | `OnDisable` / `OnDestroy` |
| `InvokeRepeating` / `Invoke` | `CancelInvoke()` | `OnDisable` |
| `StartCoroutine` on another object's MonoBehaviour | `StopCoroutine(handle)` | `OnDisable` / `OnDestroy` |
| `renderer.material` getter, `Instantiate(material)`, `new Material(...)` | `Destroy(instance)` | `OnDestroy` |
| `meshFilter.mesh` getter, `new Mesh()`, `Instantiate(mesh)` | `Destroy(mesh)` | `OnDestroy` |
| `new Texture2D`, `Sprite.Create`, `ScriptableObject.CreateInstance`, `new AudioClip` via `AudioClip.Create` | `Destroy(obj)` | when replaced and in `OnDestroy` |
| `DownloadHandlerTexture.GetContent` / `UnityWebRequestTexture` result | `Destroy(texture)` | when replaced |
| `RenderTexture.GetTemporary` | `RenderTexture.ReleaseTemporary(rt)` | same scope |
| `new RenderTexture(...)` | `rt.Release(); Destroy(rt);` | `OnDestroy` |
| `new ComputeBuffer` / `new GraphicsBuffer` | `Release()` / `Dispose()` | `OnDisable` / `OnDestroy` |
| `new NativeArray<T>(n, Allocator.Persistent / TempJob)` | `Dispose()` (or `Dispose(jobHandle)`) | `OnDestroy` / after the job completes |
| `new CommandBuffer()` | `Release()` | `OnDestroy` |
| `Addressables.LoadAssetAsync` | `Addressables.Release(handle)` | when the asset is no longer used |
| `Addressables.InstantiateAsync` | `Addressables.ReleaseInstance(go)` (not `Destroy`) | when the instance goes away |
| `AssetBundle.LoadFromFile*` | `bundle.Unload(false/true)` | when its assets are done |
| `UnityWebRequest.Get/Post/...` | `Dispose()` or `using` | after completion |
| `new CancellationTokenSource()` | `Cancel()` + `Dispose()` | `OnDisable` / `OnDestroy` |
| Tween (`DOTween .DO*`, LeanTween, PrimeTween) on a target | `Kill()` / `SetLink(gameObject)` | `OnDestroy` |
| `SceneManager.LoadSceneAsync(..., Additive)` | `SceneManager.UnloadSceneAsync` | when content is done |
| `GCHandle.Alloc` | `GCHandle.Free` | immediately after use |
| Global state changes (`Time.timeScale`, `Cursor.lockState`, `Physics.simulationMode`, `QualitySettings`) | restore | `OnDisable` |

`Resources.UnloadUnusedAssets()` frees unreferenced assets but cannot free objects still referenced from managed code, and it is expensive. It is not a fix for missing `Destroy` calls.

## Pattern detail

### 1. Event subscription without unsubscription

The most common Unity leak. If the publisher outlives the subscriber (static event, singleton, ScriptableObject channel, `SceneManager`), the subscriber stays reachable and is still invoked after destruction, typically throwing `MissingReferenceException`.

```csharp
private void OnEnable() => GameEvents.ScoreChanged += HandleScoreChanged;

private void OnDisable() => GameEvents.ScoreChanged -= HandleScoreChanged;
```

Severity: high when the publisher is static, a singleton, a ScriptableObject or `DontDestroyOnLoad`; medium when publisher and subscriber die together.

Lambdas cannot be unsubscribed unless stored: `button.onClick.AddListener(() => Buy(id))` with no stored delegate is never removable. Store the delegate in a field.

### 2. Native objects never destroyed

```csharp
private void ApplySkin(Color32[] pixels)
{
    var tex = new Texture2D(this.width, this.height);
    tex.SetPixels32(pixels);
    tex.Apply();
    this._renderer.material.mainTexture = tex;
}
```

Every call leaks a texture and the first call also creates a material instance. Keep the texture in a field, `Destroy` the previous one before replacing, destroy both in `OnDestroy`, and prefer `MaterialPropertyBlock` over `renderer.material`.

`renderer.material`, `renderer.materials` and `meshFilter.mesh` create a per-renderer copy on first access. Reading them in loops or on many renderers multiplies copies. Use `sharedMaterial`/`sharedMesh` for reads.

### 3. Addressables and AssetBundles

Every `LoadAssetAsync`/`InstantiateAsync` increments a reference count. Grep for each and verify a `Release`/`ReleaseInstance` is reachable from the owner's teardown. `Destroy` on an Addressables-instantiated object does not release the handle.

### 4. Native containers and GPU buffers

`Allocator.Persistent` and `Allocator.TempJob` must be disposed on every path, including exceptions (`try/finally`). `TempJob` must be disposed within 4 frames or Unity logs a leak warning. `Allocator.Temp` is freed at frame end and must not be stored. Enable full leak stack traces in Preferences > Jobs > Leak Detection (`NativeLeakDetection.Mode`) to locate the allocation.

### 5. Growing registries and static collections

```csharp
private static readonly HashSet<Enemy> Active = new();

private void OnEnable() => Active.Add(this);
```

Without a matching `Remove` in `OnDisable`, destroyed enemies stay in the set forever. Pair `Add`/`Remove` in `OnEnable`/`OnDisable`, not `Awake`/`OnDestroy`, so pooled and deactivated objects are handled too. Use the object reference as the key; do not key by `GetInstanceID()` (compile error on 6000.5+). Clear static collections in a `SubsystemRegistration` reset hook.

### 6. Closures capturing `this` in long-lived owners

```csharp
UIManager.Instance.RegisterRefresh(() => Refresh());
```

The lambda captures `this` and the singleton holds it forever. Store the delegate and unregister it in `OnDisable`.

### 7. Async work without lifetime binding

```csharp
private void Start() => LoadAsync().Forget();

private async UniTaskVoid LoadAsync()
{
    await UniTask.Delay(5000);
    Apply();
}
```

If the object is destroyed during the delay, the continuation still runs, holds `this`, and touches a destroyed object. Pass `destroyCancellationToken` through every await. `CancellationTokenSource` instances you create must be disposed.

### 8. Coroutines outliving their scope

Coroutines stop when their MonoBehaviour's GameObject is deactivated or destroyed, but not when only the component is disabled. A coroutine started on a manager on behalf of another object keeps running and referencing that object after it is destroyed. Store the handle and stop it in the other object's teardown.

### 9. `OnDestroy` that never runs

Unity calls `OnDestroy` only on objects whose GameObject was active at some point (so `Awake` ran). A prefab instantiated inactive and destroyed before activation skips `OnDestroy`. Pooled objects are deactivated, not destroyed. Cleanup that must always run belongs in `OnDisable`, or must not depend on `Awake` having run.

## Grep sweep patterns

| Pattern | Check for |
|---|---|
| `\+=\s*[A-Za-z_.]+;` then `-=` in the same file | unpaired subscriptions |
| `AddListener\(` | `RemoveListener` |
| `sceneLoaded|logMessageReceived|quitting|performed\s*\+=` | matching `-=` |
| `InvokeRepeating\(` | `CancelInvoke` |
| `new (Texture2D|Material|Mesh|RenderTexture|ComputeBuffer|GraphicsBuffer|CommandBuffer)\(` | `Destroy`/`Release` |
| `\.material\b|\.materials\b|\.mesh\b` | instance creation, `Destroy` of the instance |
| `GetTemporary\(` | `ReleaseTemporary` |
| `Sprite\.Create|CreateInstance<|AudioClip\.Create` | `Destroy` |
| `LoadAssetAsync|InstantiateAsync|LoadSceneAsync` | `Release`/`ReleaseInstance`/`UnloadSceneAsync` |
| `Allocator\.(Persistent|TempJob)` | `Dispose` |
| `UnityWebRequest` | `using`/`Dispose` |
| `new CancellationTokenSource` | `Dispose` |
| `static .*(List|Dictionary|HashSet)<` | removal path and static reset |
| `\.DO[A-Z]\w*\(` | `Kill` / `SetLink` |
| `async void` | `UniTaskVoid` / `Awaitable` + token |

## Confirming a leak with the Memory Profiler

Static review finds candidates; a snapshot comparison proves them.

1. Install `com.unity.memoryprofiler` and open Window > Analysis > Memory Profiler.
2. Reach a stable baseline state (for example the main menu), capture snapshot A.
3. Perform the suspect cycle several times (load and unload a level, open and close a screen), return to the same state, capture snapshot B. Repeating the cycle separates leaks (grow per cycle) from one-time caches.
4. Compare A and B. Look for growing counts of a type, leaked managed shells (destroyed objects still referenced; the profiler lists their referencing path), and native allocations (textures, meshes, render textures) with no owning asset.
5. Follow the "References To" path of a leaked object back to the static, event or collection that holds it.

Capture tips:
- Snapshots can be hundreds of MB. Save them outside `Assets/` unless they are meant to be committed.
- Capture from a development player build when possible; Editor captures include Editor-only memory.
- Capture can be scripted with `Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot(path, (file, ok) => { ... })` (Unity 6 core API). It is asynchronous: the file exists only after the callback reports success.
- Pair a snapshot with a CPU Profiler frame capture of the spike: the frame shows what ran, the snapshot shows what stayed allocated.
