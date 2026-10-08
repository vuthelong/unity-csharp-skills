# Async in Unity 6

## Choose the library

| Project has | Use |
|---|---|
| `com.cysharp.unitask` in `Packages/manifest.json` | UniTask everywhere; do not mix in `Awaitable` |
| No UniTask | Built-in `Awaitable` (UnityEngine namespace, Unity 2023.1+) |
| Pure C# library with no Unity dependency | `Task`/`ValueTask`, but marshal back to the main thread before touching Unity objects |

Do not add UniTask to a project just to follow this guide; ask first.

## UniTask

| Scenario | Return type | Call site |
|---|---|---|
| Caller awaits | `UniTask` / `UniTask<T>` | `await DoAsync(ct)` |
| Fire-and-forget | `UniTaskVoid` | `DoAsync(ct).Forget()` |

```csharp
private void Start() => LoadAsync(destroyCancellationToken).Forget();

private async UniTaskVoid LoadAsync(CancellationToken ct)
{
    await UniTask.Delay(500, cancellationToken: ct);
    Apply();
}
```

- `this.GetCancellationTokenOnDestroy()` (UniTask) and `destroyCancellationToken` (Unity) are equivalent; prefer the built-in one.
- `UniTask.Delay`, `UniTask.Yield(PlayerLoopTiming.FixedUpdate)`, `UniTask.WaitUntil`, `UniTask.WhenAll` cover most coroutine use.
- A `UniTask` can be awaited once. Call `.Preserve()` if you must await it twice.
- `UniTask.SwitchToThreadPool()` / `UniTask.SwitchToMainThread()` for background work.

## Awaitable (built-in)

```csharp
private async void Start()
{
    try
    {
        await Awaitable.WaitForSecondsAsync(0.5f, destroyCancellationToken);
        Apply();
    }
    catch (OperationCanceledException)
    {
    }
}

private async Awaitable<Texture2D> DownloadAsync(string url, CancellationToken ct)
{
    using var request = UnityWebRequestTexture.GetTexture(url);
    await request.SendWebRequest();
    ct.ThrowIfCancellationRequested();
    return DownloadHandlerTexture.GetContent(request);
}
```

`Start` may be declared `async void` or `async Awaitable`; Unity accepts both for message methods. Prefer `async Awaitable` so failures are observable.

Key API:
- `Awaitable.NextFrameAsync(ct)`, `Awaitable.EndOfFrameAsync(ct)`, `Awaitable.FixedUpdateAsync(ct)`
- `Awaitable.WaitForSecondsAsync(seconds, ct)` (scaled time)
- `Awaitable.BackgroundThreadAsync()` / `Awaitable.MainThreadAsync()`
- `await asyncOperation` for `AsyncOperation`, `ResourceRequest`, `SceneManager.LoadSceneAsync`, `UnityWebRequestAsyncOperation`
- `AwaitableCompletionSource` / `AwaitableCompletionSource<T>` to wrap callback APIs

Pitfalls:
- `Awaitable` instances are pooled. Never await the same instance twice and never keep a reference after awaiting it.
- No built-in `WhenAll`/`WhenAny`. Await sequentially, or wrap in `Task` only for non-Unity work.
- Cancellation throws `OperationCanceledException`. Catch it at the top-level fire-and-forget method, not inside every helper.

## Rules for both

1. Every await that can outlive its object takes a token: `destroyCancellationToken` for component-bound work, `Application.exitCancellationToken` for app-wide work.
2. After every await, assume the object may have been destroyed or disabled if no token was passed. Check `if (this == null) return;` when you cannot pass a token.
3. Never `async void` except Unity message methods and UI callbacks with a fixed `void` signature; wrap their bodies in `try/catch` and log with `Debug.LogException(e, this)`.
4. A `Task` that nobody awaits drops its exception silently. Never discard a `Task` with `_ = DoAsync();`.
5. Plain `Task` continuations keep running after you exit Play Mode in the Editor. Tie them to `Application.exitCancellationToken`.
6. Never block the main thread on async work: no `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, or spin-waits. Unity continuations run on the main thread, so this deadlocks the Editor or player.
7. On a background thread, touch only plain C# data and thread-safe APIs (`Debug.Log`, `Mathf`, math structs). Switch back to the main thread before using any `GameObject`, `Component`, `Transform`, asset, or `Time` API.
8. Domain reload kills in-flight Editor async work. Do not rely on an editor `async` method finishing across a script recompile.

## Coroutine vs async

| Use a coroutine when | Use async when |
|---|---|
| Simple sequenced visual timing tied to one GameObject's active state | The operation returns a value |
| Code must stop automatically when the GameObject deactivates | You need try/catch, cancellation, or composition |
| Existing codebase is coroutine-based | Wrapping callback or IO APIs |
