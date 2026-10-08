---
name: zlogger
description: Sets up Cysharp ZLogger v2 (Microsoft.Extensions.Logging provider with zero-allocation UTF-8 interpolated logging) in Unity 6. Covers NuGetForUnity install, the ZLogger.Unity UPM git URL, the csc.rsp -langVersion:10 requirement for interpolated string handlers (Unity compiles C# 9 by default), LoggerFactory.Create with AddZLoggerUnityDebug, AddZLoggerFile, AddZLoggerRollingFile, AddZLoggerStream, AddZLoggerInMemory, ILogger<T> categories, ZLogTrace..ZLogCritical, UsePlainTextFormatter prefix/suffix, UseJsonFormatter structured logs, BeginScope, AddFilter by category and level, [ZLoggerMessage] source-generated loggers, a static LogManager, VContainer registration, flushing on Application.quitting, persistentDataPath log files, release-build stripping, and Debug.Log cost comparison. Use when the user mentions ZLogger, ZLogInformation, Microsoft.Extensions.Logging in Unity, structured or file logging, log rotation, or replacing Debug.Log for performance.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/ZLogger"
  unity: "6000.0+"
---

# ZLogger

ZLogger v2 is a `Microsoft.Extensions.Logging` (MEL) provider. `ZLog*` extension methods take an interpolated string handler that writes the template and arguments straight to UTF-8 buffers without boxing or building a `string`. Verified against ZLogger 2.5.10 (`src/ZLogger`, `src/ZLogger.Unity`).

Read `references/recipes.md` for the bootstrap/LogManager, VContainer registration, file logging, formatter, scope, filter, source-generator and release-stripping code.

## Install (Unity 6)

1. Install NuGetForUnity, then `NuGet > Manage NuGet Packages` and install `ZLogger`. It pulls `Microsoft.Extensions.Logging` 8.x, `Microsoft.Extensions.DependencyInjection`, `System.Text.Json`, `System.Threading.Channels`, `Utf8StringInterpolation`, `Microsoft.Bcl.TimeProvider`, `Microsoft.Bcl.AsyncInterfaces` and related DLLs. Keep exactly one copy of shared DLLs such as `System.Runtime.CompilerServices.Unsafe` (also used by `zstring`, `memorypack`).
2. Add the Unity provider: Package Manager > Add package from git URL `https://github.com/Cysharp/ZLogger.git?path=src/ZLogger.Unity/Assets/ZLogger.Unity` (package id `com.cysharp.zlogger`, 2.5.10, `unity: 2022.3.12f1+`). Its asmdef `ZLogger.Unity` precompile-references the NuGet DLLs, so step 1 comes first. Namespace `ZLogger.Unity` provides `AddZLoggerUnityDebug`.
3. Enable C# 10 for every assembly that calls `ZLog*`. Unity 6 compiles C# 9 by default, and `ZLog*` methods only accept `ref Z...InterpolatedStringHandler` parameters (C# 10 interpolated string handlers); there is no `string` overload. Under C# 9 a call like `logger.ZLogInformation($"...")` does not compile.
   - Put a `csc.rsp` containing `-langVersion:10 -nullable` next to each asmdef that logs, or in `Assets/` for the predefined `Assembly-CSharp`. Unity's bundled Roslyn accepts it (README: Unity 2022.2+ ships a .NET 6 SDK compiler).
   - For `[ZLoggerMessage]` source-generated methods use `-langVersion:preview` (C# 11 features, Unity 2022.3.12f1+).
   - ZLogger.Unity ships its own `csc.rsp` (`-langVersion:10 -nullable`) for its asmdef only; it does not change your assemblies.
   - IDE: install Cysharp CsprojModifier and add a `.props` with `<LangVersion>10.0</LangVersion>` (or 11) so Rider/VS match the compiler.
4. If the Editor reports assembly version conflicts after the NuGet install, untick Player Settings > Other Settings > Assembly Version Validation.

Plain MEL calls (`logger.LogInformation("...")`) and `logger.Log(...)` work under C# 9, but they box arguments and are formatted to `string` (`IsFormatLogImmediatelyInStandardLog = true`), losing ZLogger's advantages.

## Workflow

1. Create one `ILoggerFactory` at startup with `LoggerFactory.Create(logging => { ... })`, store it in a static `LogManager` (or register it in VContainer, see `vcontainer`).
2. Per class: `private static readonly ILogger<Player> Logger = LogManager.GetLogger<Player>();`. The category is the type's full name.
3. Log with `Logger.ZLogInformation($"Spawned {enemyId} at {position}");` and pass a `UnityEngine.Object` as `context` to make the Console entry ping it: `Logger.ZLogWarning($"Missing clip {clipName}", this);`.
4. Dispose the factory on `Application.quitting` so file providers flush.

## Providers

| Builder extension | Output | Threading |
|---|---|---|
| `AddZLoggerUnityDebug([Action<ZLoggerUnityDebugOptions>])` | `Debug.Log` / `LogWarning` / `LogError` / `LogException` with context | synchronous on the calling thread |
| `AddZLoggerFile(string filePath[, Action<ZLoggerFileOptions>])` | append to one file | background `Task.Run` writer via `Channel` |
| `AddZLoggerRollingFile(Action<ZLoggerRollingFileOptions>)` / `(Func<DateTimeOffset,int,string> filePathSelector, RollingInterval, int rollSizeKB)` | rotating files | background |
| `AddZLoggerStream(Stream stream[, configure])` | any stream | background |
| `AddZLoggerInMemory(Action<InMemoryObservableLogProcessor>)` | `processor.MessageReceived += string` for in-game consoles | synchronous |
| `AddZLoggerLogProcessor(IAsyncLogProcessor)` | custom sink | yours |
| `AddZLoggerConsole(...)` | stdout | useful for dedicated servers / batchmode only |

Each provider has its own `ZLoggerOptions` (formatter, `IncludeScopes`, `CaptureThreadInfo`, `TimeProvider`, `InternalErrorLogger`, `FullMode` = `BackgroundBufferFullMode.Grow | Block | Drop`, `BackgroundBufferCapacity`). `ZLoggerUnityDebugOptions.PrettyStacktrace` (default `true`) appends a cleaned stack trace when the Player's stack-trace setting for that log type is not `None`.

## Logging API

- Level methods: `ZLogTrace`, `ZLogDebug`, `ZLogInformation`, `ZLogWarning`, `ZLogError`, `ZLogCritical`, and `ZLog(LogLevel, ...)`. Overloads add `EventId` and/or `Exception` before the message; all take `object? context = null` after it plus caller info.
- Disabled levels are cheap: the handler constructor checks `IsEnabled` and the interpolation holes are not formatted.
- Structured names: holes become properties named after the expression (`{player.Id}` -> `player.Id`); rename with `{id:@userId}`, combine with a format `{now:@date:yyyy-MM-dd}`; dump an object as JSON with `{user:json}`.
- Formatters: `options.UsePlainTextFormatter(f => { f.SetPrefixFormatter($"{0:short}|", (in MessageTemplate t, in LogInfo i) => t.Format(i.LogLevel)); })` and `SetSuffixFormatter`, `SetExceptionFormatter`. `options.UseJsonFormatter(f => { f.IncludeProperties = IncludeProperties.ParameterKeyValues; })`. One formatter per provider; add two providers for text + JSON.
- Scopes: set `options.IncludeScopes = true`, then `using (logger.BeginScope("{SessionId}", sessionId)) { ... }`.
- Filtering (MEL): `logging.SetMinimumLevel(LogLevel.Information)`, `logging.AddFilter("Game.Network", LogLevel.Warning)`, `logging.AddFilter<ZLoggerUnityDebugLoggerProvider>(null, LogLevel.Warning)`.

## Release builds

- Set `SetMinimumLevel(LogLevel.Warning)` (or higher) in player builds: disabled calls skip formatting, but the call and the `IsEnabled` check remain.
- To remove calls completely, wrap with `[System.Diagnostics.Conditional("ZLOGGER_VERBOSE")]` static methods that accept a pre-built message or take the logger and arguments, and define the symbol only in development builds. A `[Conditional]` method cannot forward an interpolated string handler, so keep it simple: see recipes.
- IL2CPP: providers are created through factory lambdas, but MEL builds `LoggerFactory` through `Microsoft.Extensions.DependencyInjection`, which activates types via reflection. If you get `MissingMethodException` with Managed Stripping Level Medium/High, add a `link.xml` preserving `Microsoft.Extensions.Logging`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options` and `ZLogger`.

## ZLogger vs Debug.Log

| | `Debug.Log($"...")` | ZLogger UnityDebug | ZLogger file |
|---|---|---|---|
| Message string | allocated, args boxed | still allocated once (Unity API needs a `string`), args not boxed | none; UTF-8 bytes pooled |
| Stack trace | captured per call per Player setting (expensive) | same Unity capture, plus `PrettyStacktrace` adds a managed `StackTrace` | none |
| Filtering | none built in | per category / level / provider | same |
| Structured output | no | JSON formatter | JSON / plain text |

The biggest per-call cost of `Debug.Log` in players is stack-trace capture: set Project Settings > Player > Stack Trace to `None` for `Log` (keep `ScriptOnly` for errors) and turn `PrettyStacktrace` off if you do. For hot-path diagnostics in builds, prefer a file or in-memory provider over UnityDebug.

## Pitfalls

- **Not disposing the factory loses file logs.** File, RollingFile and Stream providers queue entries to a background writer; `loggerFactory.Dispose()` drains and flushes. Dispose on `Application.quitting` (and in Editor when exiting Play Mode). Crashes still lose the tail; log critical errors to UnityDebug too.
- **Hot paths.** Even zero-allocation logging costs formatting and I/O. Gate per-frame logs with `if (Logger.IsEnabled(LogLevel.Trace))` or keep them at Trace and raise the minimum level.
- **WebGL.** File, RollingFile and Stream providers start their writer with `Task.Run`; WebGL has no threads, and its file system is IndexedDB-backed. Use UnityDebug or InMemory on WebGL.
- **Threads.** UnityDebug runs synchronously on the logging thread. `Debug.Log` is callable from worker threads, but the `context` object and any Unity API you touch while building the message are main-thread only. InMemory `MessageReceived` handlers also run on the logging thread; marshal to the main thread before touching UI (see `unitask`).
- **C# 9 call sites.** Forgetting `csc.rsp` in an asmdef folder produces "cannot convert from string to ref ZLogger...InterpolatedStringHandler". Add the file next to that asmdef.
- **Domain reload disabled.** A static factory survives Play Mode exits when Enter Play Mode Options skip domain reload. Reset it in `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- **Rolling size units.** `rollSizeKB` / `RollingSizeKB` is in kilobytes (default `512 * 1024`, 512 MB); pass `1024` for 1 MB.

## Related skills

- `zstring` for building non-log strings without garbage.
- `csharp-unity` for house style; `vcontainer` for DI registration; `unitask` for main-thread marshaling.
