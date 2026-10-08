# ZString API reference

Checked against ZString 2.6.0 (Unity package). Namespace `Cysharp.Text`.

## `static class ZString`

| Member | Returns | Notes |
|---|---|---|
| `CreateStringBuilder()` | `Utf16ValueStringBuilder` | pooled (`ArrayPool<char>.Shared`, 32K chars); nest-safe |
| `CreateStringBuilder(bool notNested)` | `Utf16ValueStringBuilder` | `true` uses the thread-static scratch buffer; not re-entrant |
| `CreateUtf8StringBuilder()` / `(bool notNested)` | `Utf8ValueStringBuilder` | UTF-8 bytes, same pooling rules |
| `Concat<T1..T16>(T1 arg1, ...)` | `string` | no boxing; uses the notNested buffer internally |
| `Concat<T>(params T[] / List<T> / ReadOnlySpan<T> / IEnumerable<T> / ...)` | `string` | collection overloads |
| `Format<T1..T16>(string format, ...)` | `string` | also `ReadOnlySpan<char> format` overloads; uses the notNested buffer |
| `Join<T>(char or string separator, T[] / List<T> / ReadOnlySpan<T> / IEnumerable<T> / IList<T> / IReadOnlyList<T> / ...)` | `string` | plus `Join(char/string, params string[])` |
| `PrepareUtf16<T1..T16>(string format)` | `Utf16PreparedFormat<T1..T16>` | parse once, format many times |
| `PrepareUtf8<T1..T16>(string format)` | `Utf8PreparedFormat<T1..T16>` | |
| `Utf8Format<T1..T16>(IBufferWriter<byte> bufferWriter, string format, ...)` | `void` | write UTF-8 straight into a buffer writer |

## `struct Utf16ValueStringBuilder : IDisposable, IBufferWriter<char>`

| Member | Notes |
|---|---|
| `new Utf16ValueStringBuilder(bool disposeImmediately)` | `true` = thread-static scratch buffer (same as `notNested`) |
| `Length` | chars written |
| `AsSpan()`, `AsMemory()`, `AsArraySegment()` | view the written chars without allocating |
| `ToString()` | the one allocation |
| `TryCopyTo(Span<char> destination, out int charsWritten)` | copy out |
| `Append(char)`, `Append(char, int repeatCount)`, `Append(string)`, `Append(string, int startIndex, int count)`, `Append(char[], int, int)`, `Append(ReadOnlySpan<char>)` | non-generic fast paths |
| `Append<T>(T value)` | generic, formatter-cache based, no boxing for supported types |
| `Append(int value, string format)` (and the same for each built-in numeric / date / Guid type) | standard .NET format strings; there is no generic `Append<T>(T, string)` |
| `AppendLine()`, `AppendLine(char/string/ReadOnlySpan<char>)`, `AppendLine<T>(T)`, `AppendLine(<builtin> value, string format)` | `Environment.NewLine` |
| `AppendFormat<T1..T16>(string format, ...)` | also `ReadOnlySpan<char>` format |
| `AppendJoin<T>(char or string separator, ...)` | collection overloads like `ZString.Join` |
| `Insert(int index, string value[, int count])`, `Insert(int, ReadOnlySpan<char>, int count)` | |
| `Remove(int startIndex, int length)` | |
| `Replace(char, char[, int, int])`, `Replace(string, string[, int, int])`, `Replace(ReadOnlySpan<char>, ReadOnlySpan<char>[, int, int])`, `ReplaceAt(char newChar, int replaceIndex)` | in-place |
| `Clear()` | reset length, keep buffer |
| `GetSpan(int)`, `GetMemory(int)`, `Advance(int)` | `IBufferWriter<char>` |
| `Dispose()` | return buffer to pool / release scratch buffer |
| `static RegisterTryFormat<T>(TryFormat<T> formatMethod)` | `delegate bool TryFormat<T>(T value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format)` |
| `static EnableNullableFormat<T>() where T : struct` | adds a `T?` formatter |

## `struct Utf8ValueStringBuilder : IDisposable, IBufferWriter<byte>`

Same shape as the Utf16 builder, with byte views (`AsSpan()` returns `ReadOnlySpan<byte>`) and these extras:

| Member | Notes |
|---|---|
| `CopyTo(IBufferWriter<byte> bufferWriter)` | |
| `TryCopyTo(Span<byte> destination, out int bytesWritten)` | |
| `WriteToAsync(Stream stream[, CancellationToken])` | returns `Task` |
| `AppendLiteral(ReadOnlySpan<byte> value)` | raw UTF-8 bytes |
| `ToString()` | decodes UTF-8 to `string` |
| `static RegisterTryFormat<T>(TryFormat<T>)` | `delegate bool TryFormat<T>(T value, Span<byte> destination, out int written, StandardFormat format)` |

UTF-8 formatting uses `System.Buffers.Text.Utf8Formatter` rules: a `StandardFormat` symbol plus optional precision (`D2`, `N`, `X`, `G`; `bool` supports `G`, `I`). It does not accept custom patterns like `0.00` or `yyyy-MM-dd`; split dates into parts and format each with `D2` / `D4`.

```csharp
using var sb = ZString.CreateUtf8StringBuilder();
sb.AppendFormat("{0:D2}:{1:D2}:{2:D2}", h, m, s);
sb.CopyTo(bufferWriter);
```

## Prepared formats

```csharp
private static readonly Utf16PreparedFormat<int, float> HpFormat =
    ZString.PrepareUtf16<int, float>("HP {0} ({1:P0})");

public string Describe(int hp, float ratio) => HpFormat.Format(hp, ratio);

public void AppendTo(ref Utf16ValueStringBuilder sb, int hp, float ratio) => HpFormat.FormatTo(ref sb, hp, ratio);
```

`Utf16PreparedFormat<...>` members: `FormatString`, `MinSize`, `Format(args)` returning `string`, `FormatTo<TBufferWriter>(ref TBufferWriter sb, args)` where `TBufferWriter : IBufferWriter<char>`. `Utf8PreparedFormat<...>` is the byte equivalent. Store prepared formats in `static readonly` fields; creating one per call defeats the purpose.

## `ZStringWriter : TextWriter`

Backed by a pooled `Utf16ValueStringBuilder`. Constructors: `ZStringWriter()`, `ZStringWriter(IFormatProvider)`. Overrides `Write` / `WriteLine` / async variants, including `Write(ReadOnlySpan<char>)`. `ToString()` returns the text. Always dispose it (`Dispose` / `Close` return the buffer).

To push a Utf16 builder into any existing `TextWriter`, write the span: `writer.Write(sb.AsSpan());`.

## `TextMeshProExtensions` (requires `ZSTRING_TEXTMESHPRO_SUPPORT`)

| Member | Notes |
|---|---|
| `SetText<T>(this TMP_Text text, T arg0)` | one value, notNested buffer, `SetCharArray` |
| `SetTextFormat<T0..T15>(this TMP_Text text, string format, ...)` | up to 16 args |
| `SetText(this TMP_Text text, Utf16ValueStringBuilder stringBuilder)` | copies `AsArraySegment()` via `SetCharArray` |

## Custom formatter example

```csharp
using System;
using Cysharp.Text;
using UnityEngine;

public static class ZStringFormatters
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        Utf16ValueStringBuilder.RegisterTryFormat((Vector2Int v, Span<char> dest, out int written, ReadOnlySpan<char> format) =>
        {
            written = 0;
            if (!v.x.TryFormat(dest, out var a, format)) return false;
            if (dest.Length < a + 1) return false;
            dest[a] = ',';
            if (!v.y.TryFormat(dest.Slice(a + 1), out var b, format)) return false;
            written = a + 1 + b;
            return true;
        });
    }
}
```

Return `false` when `dest` is too small; the builder grows its buffer and calls the formatter again.
