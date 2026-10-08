# Cancellation, timeouts, errors and tests

All snippets assume `using System; using System.Threading; using Cysharp.Threading.Tasks; using UnityEngine;`.

## Lifetime token from the root

```csharp
public sealed class Spawner : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        await SpawnWavesAsync(destroyCancellationToken);
    }

    private async UniTask SpawnWavesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: cancellationToken);
            Spawn();
        }
    }

    private void Spawn() { }
}
```

Cancellation surfaces as `OperationCanceledException`, which `UniTaskVoid` swallows silently, so no try/catch is needed here.

## Restartable work tied to enable/disable

```csharp
public sealed class Blinker : MonoBehaviour
{
    private CancellationTokenSource _enableCts;

    private void OnEnable()
    {
        _enableCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        BlinkAsync(_enableCts.Token).Forget();
    }

    private void OnDisable()
    {
        _enableCts.Cancel();
        _enableCts.Dispose();
        _enableCts = null;
    }

    private async UniTask BlinkAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await UniTask.Delay(500, cancellationToken: cancellationToken);
        }
    }
}
```

Rules: one CTS per run, cancel before replacing, always `Dispose()`. A linked source must be disposed too, or it stays registered on the parent token.

## Cancel the previous request (latest-wins)

```csharp
private CancellationTokenSource _searchCts;

public void OnQueryChanged(string query)
{
    _searchCts?.Cancel();
    _searchCts?.Dispose();
    _searchCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
    SearchAsync(query, _searchCts.Token).Forget();
}
```

## Timeout with CancelAfterSlim

```csharp
using var timeoutCts = new CancellationTokenSource();
timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(5));
using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, destroyCancellationToken);

try
{
    using var req = UnityEngine.Networking.UnityWebRequest.Get(url);
    await req.SendWebRequest().WithCancellation(linked.Token);
}
catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
{
    Debug.LogWarning("Request timed out");
}
```

Use `DelayType.UnscaledDeltaTime` or `DelayType.Realtime` in `CancelAfterSlim` when the game can be paused with `Time.timeScale = 0`.

## Reusable TimeoutController

```csharp
private TimeoutController _timeout;

private void Awake() => _timeout = new TimeoutController(DelayType.UnscaledDeltaTime);
private void OnDestroy() => _timeout.Dispose();

private async UniTask<string> FetchAsync(string url)
{
    try
    {
        using var req = UnityEngine.Networking.UnityWebRequest.Get(url);
        await req.SendWebRequest().WithCancellation(_timeout.Timeout(TimeSpan.FromSeconds(5)));
        _timeout.Reset();
        return req.downloadHandler.text;
    }
    catch (OperationCanceledException) when (_timeout.IsTimeout())
    {
        return null;
    }
}
```

To combine with another source, pass a `CancellationTokenSource` (not a token): `new TimeoutController(linkCancellationTokenSource, delayType, delayTiming)`. Call `Reset()` after success or the timer keeps running and will fire later. One controller serves one operation at a time.

## Stop waiting without stopping the work

```csharp
var result = await LoadAsync().AttachExternalCancellation(cancellationToken);
```

The inner task keeps running; only the await is abandoned. Prefer passing the token into `LoadAsync` when you own it.

## Cancellation without exceptions

```csharp
var canceled = await UniTask.DelayFrame(30, cancellationToken: ct).SuppressCancellationThrow();
if (canceled) return;

var (isCanceled, text) = await GetTextAsync(ct).SuppressCancellationThrow();
```

`GetTextAsync` still throws internally if it awaits something that throws; suppression only skips the final rethrow.

## Exception handling

```csharp
try
{
    await DoWorkAsync(ct);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Debug.LogException(ex);
}
```

Global reporter (once, early):

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void HookUniTaskErrors()
{
    UniTaskScheduler.UnobservedTaskException += ex => Debug.LogException(ex);
}
```

Once you subscribe, UniTask stops writing the log itself, so log or report inside the handler.

## Callback API to UniTask

```csharp
public UniTask<bool> ShowDialogAsync(CancellationToken cancellationToken)
{
    var tcs = new UniTaskCompletionSource<bool>();
    _dialog.Open(onClose: ok => tcs.TrySetResult(ok));
    cancellationToken.RegisterWithoutCaptureExecutionContext(() =>
    {
        _dialog.Close();
        tcs.TrySetCanceled(cancellationToken);
    });
    return tcs.Task;
}
```

Use `AutoResetUniTaskCompletionSource<T>.Create()` in hot paths when exactly one caller awaits the task once; it returns to the pool after that await.

## Awaiting the same work from several places

```csharp
private AsyncLazy<Config> _config;

private void Awake() => _config = UniTask.Lazy(LoadConfigAsync);

public UniTask<Config> GetConfigAsync() => _config.Task;
```

Or, within one method, `var shared = LoadAsync().Preserve(); await shared; await shared;`.

## Event handlers

```csharp
button.onClick.AddListener(UniTask.UnityAction(async () =>
{
    await SaveAsync(destroyCancellationToken);
}));
```

Never write `button.onClick.AddListener(async () => ...)`: that is `async void`.

## Tests

```csharp
[UnityTest]
public IEnumerator Delay_RespectsUnscaledTime() => UniTask.ToCoroutine(async () =>
{
    Time.timeScale = 0f;
    try
    {
        await UniTask.Delay(100, DelayType.UnscaledDeltaTime);
    }
    finally
    {
        Time.timeScale = 1f;
    }
});
```

Edit Mode tests: all timings run on `EditorApplication.update` and `DeltaTime` delays switch to `Realtime` automatically.
