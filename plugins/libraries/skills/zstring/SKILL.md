---
name: zstring
description: "Builds strings without GC garbage in Unity using Cysharp ZString: Concat, Format, Join, UTF-16/UTF-8 value builders, prepared formats, TextMeshPro SetTextFormat/SetText, custom formatters. Use for per-frame UI text (score, timer, FPS), hot-path logging or network text, and when the Profiler shows GC Alloc from string.Format, interpolation or TMP SetText, or on NestedStringBuilderCreationException. Not for structured logging (see zlogger)."
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/ZString"
  unity: "6000.0+"
---

# ZString

Struct-based string builders that rent buffers from a thread-static scratch array or `ArrayPool<T>`, format primitives directly into the buffer, and allocate only the final `string` (or nothing, when the consumer accepts a `char[]` / span). Verified against ZString 2.6.0 (`src/ZString.Unity/Assets/Scripts/ZString`).

Read `references/api.md` when you need full member lists, the Utf8 format-string rules, or prepared-format and custom-formatter details.

## Workflow

1. Install:
   - Package Manager > Add package from git URL: `https://github.com/Cysharp/ZString.git?path=src/ZString.Unity/Assets/Scripts/ZString#2.6.0` (release tags are plain `x.y.z`), or import `ZString.Unity.*.unitypackage` from the GitHub releases.
   - The code calls `System.Runtime.CompilerServices.Unsafe`. The unitypackage ships `Assets/Plugins/System.Runtime.CompilerServices.Unsafe.dll` (6.0.0); the git package does NOT. Two copies cause "Multiple precompiled assemblies with the same name".
   - **System.Runtime.CompilerServices.Unsafe:** install once via NuGetForUnity (package `System.Runtime.CompilerServices.Unsafe` 6.0.0, usually pulled in as a dependency); if the project already has it from another route (a `.unitypackage` DLL, the `org.nuget.system.runtime.compilerservices.unsafe` UPM package), keep that one and don't add a second copy (duplicate-assembly errors).
2. Your asmdef references `ZString` (it is `autoReferenced`, so `Assembly-CSharp` sees it without changes).
3. `using Cysharp.Text;`.
4. Replace hot-path string building (per frame, per tick, per log line) with the patterns below. Leave one-off strings alone.
5. Confirm in the Profiler (GC Alloc column, Release code optimization) that only the final string, or nothing, is allocated.

## Pick the API

| Situation | Use |
|---|---|
| Join a few values once | `ZString.Concat(a, b, c)` (up to 16 generic args, no boxing, no `ToString()` per arg) |
| Format with placeholders | `ZString.Format("HP {0}/{1}", hp, max)` (up to 16 generic args) |
| Join a collection | `ZString.Join(", ", list)` (`char` or `string` separator; arrays, `List<T>`, `IEnumerable<T>`, spans) |
| Many appends, loops, conditionals | `using var sb = ZString.CreateStringBuilder();` then `sb.Append(...)`, `sb.ToString()` |
| Write UI text every frame | `tmpText.SetTextFormat("{0:0}", value)` or `tmpText.SetText(sb)`: zero allocation |
| Same format string called often | `static readonly Utf16PreparedFormat<int,int> Fmt = ZString.PrepareUtf16<int,int>("{0}:{1:00}");` then `Fmt.Format(m, s)` |
| Bytes for network, files, JSON | `using var sb = ZString.CreateUtf8StringBuilder();` then `CopyTo(IBufferWriter<byte>)`, `TryCopyTo(Span<byte>, out int)`, `WriteToAsync(Stream)` |
| An API needs a `TextWriter` | `using var w = new ZStringWriter();` then `w.ToString()` |

## Builder rules

- **Always dispose.** `CreateStringBuilder()` rents a 32K-char buffer from `ArrayPool<char>.Shared`. Without `Dispose()` the buffer never goes back to the pool, so every new builder rents (allocates) a fresh one. Use `using var sb = ...;` or `using (var sb = ...) { }`.
- **It is a mutable struct.** Passing it by value copies the index and buffer reference; appends in the callee are lost and a later `Dispose` can double-return the buffer. Pass `ref Utf16ValueStringBuilder` and dispose in `try/finally` (a `using` variable cannot be passed by `ref`).
- **Never return or store a builder.** Return `sb.ToString()` instead. A builder in a field outlives its pooled buffer contract.
- **Do not use it after `Dispose()`.** The buffer may already belong to another builder.
- **Boxing breaks it.** Casting to `IBufferWriter<char>` / `IBufferWriter<byte>` boxes a copy. Unbox back to the struct type to read the result: `using var unboxed = (Utf8ValueStringBuilder)boxed;`.

## notNested (thread-static) builders

`ZString.CreateStringBuilder(notNested: true)` and `new Utf16ValueStringBuilder(true)` use one thread-static scratch buffer per thread instead of `ArrayPool`. Slightly faster, but only one may be open per thread at a time.

- Opening a second notNested builder before the first is disposed throws an `InvalidOperationException` (internal type `NestedStringBuilderCreationException`).
- `ZString.Concat`, `ZString.Format`, `ZString.Join`, `TextMeshProExtensions.SetText<T>` and `SetTextFormat` all use the notNested buffer internally. Calling any of them while you hold an open notNested builder (directly, or in a method or `ToString()` override you call while appending) throws.
- Use notNested only for short, flat blocks that end in `ToString()` or a copy and touch no other ZString call. Default `CreateStringBuilder()` (pooled, `notNested: false`) is safe to nest.

## TextMeshPro

`TextMeshProExtensions` (in `Cysharp.Text`) compile only when `ZSTRING_TEXTMESHPRO_SUPPORT` is defined. The `ZString` asmdef sets it through versionDefines when `com.unity.textmeshpro` is installed or when `com.unity.ugui` is 2.0.0 or newer, which is the Unity 6 case where TMP ships inside uGUI. If TMP lives in `Assets/` as source, add `ZSTRING_TEXTMESHPRO_SUPPORT` to Scripting Define Symbols. The asmdef references `Unity.TextMeshPro`.

| Extension on `TMP_Text` | Behaviour |
|---|---|
| `SetTextFormat(string format, T0 arg0, ... T15 arg15)` | formats into the thread-static buffer, then `SetCharArray`: no string allocated |
| `SetText<T>(T arg0)` | same for a single value |
| `SetText(Utf16ValueStringBuilder sb)` | copies the builder's buffer via `SetCharArray`; you still dispose `sb` |

```csharp
using Cysharp.Text;
using TMPro;
using UnityEngine;

public sealed class TimerLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    private float _elapsed;

    private void Update()
    {
        _elapsed += Time.deltaTime;
        var minutes = (int)(_elapsed / 60f);
        var seconds = _elapsed % 60f;
        _label.SetTextFormat("{0:00}:{1:00.0}", minutes, seconds);
    }
}
```

TMP's own `TMP_Text.SetText(string, float, ...)` overloads also avoid allocations for a few float args but use TMP's own `{0:2}` syntax; ZString accepts standard .NET format strings and any type. See `optimize-text-mesh-pro` for TMP-side costs (layout rebuilds, auto-size), which remain even with zero string garbage.

## ZString vs alternatives (Unity 6, C# 9)

| Option | Allocations | Use when |
|---|---|---|
| `$"x:{x}"` / `string.Format` | boxes value-type args, `params object[]`, final string | cold paths, editor tools, exceptions |
| `a + b + c` | one `ToString()` per non-string arg, often an array, final string | cold paths |
| `System.Text.StringBuilder` | builder + chunks, boxed `AppendFormat` args, final string | cold paths; cache-and-`Clear()` if you must |
| ZString `Concat` / `Format` / builder | final string only (zero with TMP / span consumers) | hot paths, per-frame UI, logging |
| `DefaultInterpolatedStringHandler` / custom interpolated handlers | n/a | needs C# 10; Unity 6 compiles C# 9 by default, but an asmdef can opt in with a `csc.rsp` containing `-langVersion:10` (as `zlogger` does). use it for your own handlers; ZString's `Format` / builder APIs do not need it |

The README benchmarks show ZString ahead of `+`, `string.Format` and `StringBuilder` for a mixed `int` / `string` concat; exact numbers depend on runtime and are not published as a table. Measure in your own Profiler.

## Pitfalls

- Thread safety: a builder is single-threaded. Pooled builders can be used from worker threads (each rents its own buffer). Thread-static buffers are per thread, so notNested is safe across threads but not re-entrant within one thread.
- Format semantics: Utf16 builders use .NET format strings (`{0:000}`, `{1:P}`, `{0:0.##}`). Utf8 builders use `StandardFormat` (`D2`, `N`, `X`, `G`); custom date patterns are not supported, so format date parts yourself.
- Types without a built-in formatter fall back to `ToString()` (allocates). Built-ins: all integer types, `float`, `double`, `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `string`, and enums (cached names). `bool` goes through `ToString()` (cached literals, no garbage); `char` has its own non-generic `Append(char)` overload, but a `char` passed as a generic `Format` / `Concat` argument falls back to `ToString()`. Register others with `Utf16ValueStringBuilder.RegisterTryFormat<T>(...)`; enable `T?` with `EnableNullableFormat<T>()`.
- `Vector3`, `Color` etc. have no built-in formatter: append components (`sb.Append(v.x); sb.Append(',')...`) or register a formatter.
- The Profiler shows allocations in Debug code optimization that disappear in Release; judge with Release.
- IL2CPP: no reflection-based code paths beyond enum name caching; the `System.Runtime.CompilerServices.Unsafe` DLL must be included for player builds (keep its import settings enabled for all platforms). `RegisterTryFormat<T>` overwrites `FormatterCache<T>.TryFormatDelegate`; register at startup (for example in `RuntimeInitializeOnLoadMethod`) so every call site uses the same formatter.
- `SetTextFormat` with more than 16 args does not exist; use a builder and `SetText(sb)`.

## Related skills

- `csharp-unity` for general allocation and GC rules.
- `optimize-text-mesh-pro` for TMP rendering and layout costs.
- `unitask` and `r3` (Cysharp siblings) often pair with ZString for UI binding.
- `native-memory-array` for UTF-8 output larger than 2 GB or kept off the managed heap (`CreateBufferWriter()` as the `IBufferWriter<byte>` target).
