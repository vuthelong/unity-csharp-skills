# ZLogger recipes for Unity

All files that call `ZLog*` need `-langVersion:10` (or `preview`) in the `csc.rsp` next to their asmdef. Style follows `csharp-unity`: `this.` on instance fields, `var`, `#region`, no LINQ.

## Static LogManager with bootstrap and flush

```csharp
using System.IO;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ZLogger;
using ZLogger.Unity;

public static class LogManager
{
    #region Fields
    private static ILoggerFactory _factory;
    #endregion

    #region Public API
    public static ILoggerFactory Factory => _factory ??= CreateFactory();

    public static ILogger<T> GetLogger<T>() => Factory.CreateLogger<T>();

    public static ILogger GetLogger(string categoryName) => Factory.CreateLogger(categoryName);
    #endregion

    #region Bootstrap
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _factory = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        _ = Factory;
        Application.quitting += Shutdown;
    }

    private static ILoggerFactory CreateFactory()
    {
        var logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(logDirectory);

        return LoggerFactory.Create(logging =>
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            logging.SetMinimumLevel(LogLevel.Debug);
#else
            logging.SetMinimumLevel(LogLevel.Warning);
#endif
            logging.AddZLoggerUnityDebug(options =>
            {
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"[{0:short}] ", (in MessageTemplate template, in LogInfo info) => template.Format(info.LogLevel));
                    formatter.SetSuffixFormatter($" ({0})", (in MessageTemplate template, in LogInfo info) => template.Format(info.Category));
                });
            });

#if !UNITY_WEBGL
            logging.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (timestamp, sequenceNumber) =>
                    Path.Combine(logDirectory, $"{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:000}.log");
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB = 1024;
                options.UseJsonFormatter();
            });
#endif
        });
    }

    private static void Shutdown()
    {
        Application.quitting -= Shutdown;
        _factory?.Dispose();
        _factory = null;
    }
    #endregion
}
```

Usage:

```csharp
using Microsoft.Extensions.Logging;
using UnityEngine;
using ZLogger;

public sealed class EnemySpawner : MonoBehaviour
{
    #region Fields
    private static readonly ILogger<EnemySpawner> Logger = LogManager.GetLogger<EnemySpawner>();
    [SerializeField] private int maxEnemies = 20;
    private int _alive;
    #endregion

    #region Spawning
    private void Spawn(int enemyId, Vector3 position)
    {
        if (this._alive >= this.maxEnemies)
        {
            Logger.ZLogWarning($"Spawn cap {this.maxEnemies} reached, skipping {enemyId}", this);
            return;
        }

        this._alive++;
        Logger.ZLogDebug($"Spawned {enemyId:@enemyId} at {position}");
    }
    #endregion
}
```

`ILogger` here is `Microsoft.Extensions.Logging.ILogger`; add `using ILogger = Microsoft.Extensions.Logging.ILogger;` in files that also import `UnityEngine` and use the non-generic interface, because `UnityEngine.ILogger` exists too.

## VContainer registration

```csharp
using Microsoft.Extensions.Logging;
using VContainer;
using VContainer.Unity;

public sealed class RootLifetimeScope : LifetimeScope
{
    #region Configuration
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(LogManager.Factory);
        builder.Register(typeof(Logger<>), Lifetime.Singleton).As(typeof(ILogger<>));
        builder.Register<InventoryService>(Lifetime.Singleton);
    }
    #endregion
}

public sealed class InventoryService
{
    #region Fields
    private readonly ILogger<InventoryService> _logger;
    #endregion

    #region Constructors
    public InventoryService(ILogger<InventoryService> logger)
    {
        this._logger = logger;
    }
    #endregion
}
```

`Microsoft.Extensions.Logging.Logger<T>` has a public constructor taking `ILoggerFactory`, so an open-generic registration resolves any `ILogger<T>`. Register the factory as an instance so the container does not dispose it; the LogManager still owns disposal. Confirm open-generic syntax against `vcontainer`.

## Scopes

```csharp
logging.AddZLoggerUnityDebug(options => options.IncludeScopes = true);

using (Logger.BeginScope("{MatchId}", matchId))
{
    Logger.ZLogInformation($"Round {round} started");
}
```

With the JSON formatter, scope values appear as properties (`ScopeKeyValues` in `IncludeProperties`).

## Filtering

```csharp
var factory = LoggerFactory.Create(logging =>
{
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddFilter("Game.Network", LogLevel.Warning);
    logging.AddFilter<ZLoggerUnityDebugLoggerProvider>(null, LogLevel.Information);
    logging.AddZLoggerUnityDebug();
    logging.AddZLoggerFile(Path.Combine(Application.persistentDataPath, "debug.log"));
});
```

Category filters match by prefix of the full type name (`Game.Network.*`). The provider-specific filter keeps the Console quieter than the file.

## In-game log console

```csharp
logging.AddZLoggerInMemory(processor =>
{
    processor.MessageReceived += message => LogConsoleBuffer.Enqueue(message);
});
```

`MessageReceived` runs on the logging thread. Push into a thread-safe queue (for example `ConcurrentQueue<string>`) and drain it in `Update`; for a bounded on-screen log see `observable-collections` (`ObservableFixedSizeRingBuffer<T>`).

## Source-generated log methods

Requires `-langVersion:preview` in the asmdef's `csc.rsp`.

```csharp
using Microsoft.Extensions.Logging;
using ZLogger;

public static partial class NetworkLog
{
    #region Messages
    [ZLoggerMessage(LogLevel.Information, "Connected to {host} in {elapsedMs} ms")]
    public static partial void Connected(this ILogger logger, string host, long elapsedMs);

    [ZLoggerMessage(LogLevel.Error, "Packet {packetId} rejected: {reason}")]
    public static partial void PacketRejected(this ILogger logger, int packetId, string reason);
    #endregion
}
```

Call as `Logger.Connected(host, stopwatch.ElapsedMilliseconds);`. Attribute constructors: `()`, `(LogLevel level)`, `(string message)`, `(LogLevel level, string message)`, `(int eventId, LogLevel level, string message)`; properties `EventId`, `EventName`, `Level`, `Message`, `SkipEnabledCheck`. Placeholders support the same `:json` / format specifiers as `ZLog*`.

## Compiling logs out of release builds

A `[Conditional]` method removes the call and the evaluation of its arguments, but an interpolated string handler cannot be forwarded through a wrapper. Keep the wrapper's parameters plain:

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZLogger;

public static class DevLog
{
    #region Public API
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Trace<T>(ILogger logger, string label, T value)
    {
        logger.ZLogTrace($"{label}: {value}");
    }
    #endregion
}
```

`DevLog.Trace(Logger, "velocity", rb.linearVelocity);` disappears entirely from release players, including the argument evaluation. For everything else rely on `SetMinimumLevel`, which skips formatting but keeps the call.

## link.xml for aggressive stripping

```xml
<linker>
  <assembly fullname="Microsoft.Extensions.Logging" preserve="all"/>
  <assembly fullname="Microsoft.Extensions.Logging.Abstractions" preserve="all"/>
  <assembly fullname="Microsoft.Extensions.DependencyInjection" preserve="all"/>
  <assembly fullname="Microsoft.Extensions.Options" preserve="all"/>
  <assembly fullname="ZLogger" preserve="all"/>
</linker>
```

Add only if an IL2CPP build with Medium/High managed stripping throws `MissingMethodException` during `LoggerFactory.Create`.
