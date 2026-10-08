# NativeMemoryArray API

Namespace `Cysharp.Collections`. Class `public sealed unsafe class NativeMemoryArray<T> : IDisposable where T : unmanaged`.

| Member | UPM source (1.2.2) | NuGet DLL | Notes |
|---|---|---|---|
| `NativeMemoryArray(long length, bool skipZeroClear = false, bool addMemoryPressure = false)` | yes | yes | zero-clears unless `skipZeroClear`; `addMemoryPressure` calls `GC.AddMemoryPressure` / `RemoveMemoryPressure` |
| `static NativeMemoryArray<T> Empty` | yes | yes | zero-length, already disposed |
| `long Length` | yes | yes | element count |
| `ref T this[long index]` | yes | yes | bounds-checked, no disposed check |
| `ref T GetPinnableReference()` | yes | yes | enables `fixed (T* p = array)`; `fixed (T* p = &array[i])` also works |
| `Span<T> AsSpan()`, `AsSpan(long start)`, `AsSpan(long start, int length)` | yes | yes | throws if the slice exceeds `int.MaxValue` elements |
| `Memory<T> AsMemory()`, `AsMemory(long start)`, `AsMemory(long start, int length)` | yes | yes | backed by a `MemoryManager<T>` over the pointer; usable in async code |
| `bool TryGetFullSpan(out Span<T> span)` | yes | yes | false when `Length > int.MaxValue` |
| `Stream AsStream()`, `AsStream(FileAccess)` | yes | yes | `UnmanagedMemoryStream`; use `FileAccess.Write` / `ReadWrite` to write |
| `Stream AsStream(long offset)`, `AsStream(long offset, FileAccess)` | yes | yes | stream length is the full array length, not `Length - offset` |
| `Stream AsStream(long offset, long length)`, `AsStream(long offset, long length, FileAccess)` | no | yes | correct bounded slice |
| `IBufferWriter<T> CreateBufferWriter()` | yes | yes | writes from index 0; fixed capacity = `Length`; written count not exposed |
| `SpanSequence AsSpanSequence(int chunkSize = int.MaxValue)` | yes | yes | `foreach (Span<T> chunk in ...)` |
| `MemorySequence AsMemorySequence(int chunkSize = int.MaxValue)` | yes | yes | `foreach (Memory<T> chunk in ...)` |
| `IReadOnlyList<Memory<T>> AsMemoryList(int chunkSize = int.MaxValue)` | yes | yes | for scatter/gather APIs |
| `IReadOnlyList<ReadOnlyMemory<T>> AsReadOnlyMemoryList(int chunkSize = int.MaxValue)` | yes | yes | |
| `ReadOnlySequence<T> AsReadOnlySequence(int chunkSize = int.MaxValue)` | yes | yes | for `SequenceReader<T>`, MessagePack, `Utf8JsonReader` |
| `SpanSequence GetEnumerator()` | yes | yes | `foreach (var chunk in array)` |
| `byte* StealPointer()` | no | yes | transfers ownership; `Dispose` then does not free |
| `void Dispose()` | yes | yes | frees memory, `GC.SuppressFinalize` |
| `~NativeMemoryArray()` | yes | yes | frees if not disposed |

## Extensions (`NativeMemoryArrayExtensions`)

Compiled only when `!NETSTANDARD2_0 && !UNITY_2019_1_OR_NEWER`: absent from the UPM source, present in the NuGet netstandard2.1 DLL.

| Method | Notes |
|---|---|
| `Task ReadFromAsync(this NativeMemoryArray<byte> buffer, Stream stream, IProgress<int>? progress = null, CancellationToken cancellationToken = default)` | fills from offset 0 |
| `Task WriteToFileAsync(this NativeMemoryArray<byte> buffer, string path, FileMode mode = FileMode.Create, IProgress<int>? progress = null, CancellationToken cancellationToken = default)` | |
| `Task WriteToAsync(this NativeMemoryArray<byte> buffer, Stream stream, int chunkSize = int.MaxValue, IProgress<int>? progress = null, CancellationToken cancellationToken = default)` | |

`System.IO.RandomAccess` overloads taking `IReadOnlyList<Memory<byte>>` are .NET 6+ APIs and are not available in Unity.

## Copying to and from Unity containers

```csharp
var source = new NativeArray<Vector3>(count, Allocator.Temp);
using var target = new NativeMemoryArray<Vector3>(count, skipZeroClear: true);
source.AsSpan().CopyTo(target.AsSpan());
source.Dispose();
```

`NativeArray<T>.AsSpan()` / `AsReadOnlySpan()` exist in Unity 6 (Collections not required). For the reverse direction, copy `target.AsSpan(start, length)` into `nativeArray.AsSpan()`.
