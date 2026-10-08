---
name: native-memory-array
description: Uses Cysharp NativeMemoryArray (Cysharp.Collections.NativeMemoryArray<T>) in Unity 6 for native-memory buffers that bypass the managed heap and the 2 GB array limit, exposed as Span<T>, Memory<T>, IBufferWriter<T>, ReadOnlySequence<T> and UnmanagedMemoryStream. Covers UPM git or NuGetForUnity install and the System.Runtime.CompilerServices.Unsafe DLL, the unmanaged constraint, skipZeroClear / addMemoryPressure, long Length and indexer, AsSpan / AsMemory slices, AsSpanSequence / AsMemorySequence chunking, GetPinnableReference and fixed, CreateBufferWriter for MemoryPack / MessagePack, large file IO, differences from Unity NativeArray<T> (Allocator, Jobs, Burst, safety handles), and pitfalls such as missing Dispose, use-after-dispose crashes, Span across await, and 32-bit WebGL limits. Use when the user mentions NativeMemoryArray, buffers over 2 GB, huge binary files, point clouds or video frames in C#, or IBufferWriter backed by native memory.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/NativeMemoryArray"
  unity: "6000.0+"
---

# NativeMemoryArray

`NativeMemoryArray<T> where T : unmanaged` is a `sealed class` that owns a block of native memory (`Marshal.AllocHGlobal` in Unity, `NativeMemory.Alloc` on .NET 6+) with a `long` length. It is for C#-side data processing and IO, not for the engine or the job system. Verified against the repository at UPM package 1.2.2 (`src/NativeMemoryArray.Unity/Assets/Plugins/NativeMemoryArray`) and the NuGet source (`src/NativeMemoryArray`).

Read `references/api.md` for the full member table and the differences between the UPM source and the NuGet DLL.

## Install

Pick one:

- UPM git URL: `https://github.com/Cysharp/NativeMemoryArray.git?path=src/NativeMemoryArray.Unity/Assets/Plugins/NativeMemoryArray` (package id `com.cysharp.nativememoryarray`, 1.2.2). Source package; its asmdef is literally named `NewAssembly` (auto-referenced, unsafe code allowed), so reference that name from your asmdef.
- NuGetForUnity: package `NativeMemoryArray` (netstandard2.1 DLL). It has a few extra members (`StealPointer`, `AsStream(offset, length)`, the `ReadFromAsync` / `WriteToAsync` / `WriteToFileAsync` extensions) that the UPM source compiles out.

DLL requirements on Unity 6: Unity's .NET Standard 2.1 / .NET Framework profiles already contain `Span<T>`, `Memory<T>`, `IBufferWriter<T>` and `ReadOnlySequence<T>`, so `System.Memory.dll` and `System.Buffers.dll` are not needed. `System.Runtime.CompilerServices.Unsafe.dll` IS needed and is not in the git package. The same rule applies as in `zstring`: add it once (from the release `.unitypackage`, NuGet `System.Runtime.CompilerServices.Unsafe` 6.0.0, or NuGetForUnity), and keep exactly one copy across ZString, MemoryPack, ZLogger and this package. The README's unitypackage still ships `System.Memory.dll` and `System.Buffers.dll`; delete them if they cause duplicate-type errors.

## Workflow

1. Allocate with `using var data = new NativeMemoryArray<byte>(length);` (zero-cleared) or `new NativeMemoryArray<byte>(length, skipZeroClear: true)` when you will overwrite everything.
2. Pass `addMemoryPressure: true` for large long-lived buffers so the GC knows about the native allocation.
3. Work through slices: `AsSpan(start, count)` for synchronous code, `AsMemory(start, count)` for async APIs, `foreach (var chunk in data)` / `AsMemorySequence(chunkSize)` to cover more than `int.MaxValue` elements.
4. Feed serializers through `CreateBufferWriter()` and readers through `AsReadOnlySequence()`.
5. Dispose deterministically: `using`, or `OnDestroy` for buffers held in fields.

## Large file read and write in Unity

`System.IO.RandomAccess` (the README's scatter/gather example) is .NET 6+ only and does not exist in Unity. Use streams:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Collections;

public static class NativeFileIO
{
    #region Public API
    public static async Task<NativeMemoryArray<byte>> ReadAllAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var buffer = new NativeMemoryArray<byte>(stream.Length, skipZeroClear: true);
        try
        {
            var writer = buffer.CreateBufferWriter();
            int read;
            while ((read = await stream.ReadAsync(writer.GetMemory(), cancellationToken)) != 0)
            {
                writer.Advance(read);
            }
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    public static async Task WriteAllAsync(NativeMemoryArray<byte> buffer, string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        foreach (var chunk in buffer.AsMemorySequence())
        {
            await stream.WriteAsync(chunk, cancellationToken);
        }
    }
    #endregion
}
```

With the NuGet DLL you can call the built-in `buffer.ReadFromAsync(stream)` / `buffer.WriteToFileAsync(path)` extensions instead.

## Serializer interop

```csharp
using var buffer = new NativeMemoryArray<byte>(64L * 1024 * 1024, skipZeroClear: true);
var writer = buffer.CreateBufferWriter();
MemoryPackSerializer.Serialize(writer, snapshot);
```

The writer is a class, so it satisfies MemoryPack's Unity constraint `TBufferWriter : class, IBufferWriter<byte>` (see `memorypack`). MessagePack-CSharp accepts the same writer and `AsReadOnlySequence()` for deserialization (see `messagepack-csharp`). The writer cannot grow: the array's length is the capacity. `GetSpan(sizeHint)` / `GetMemory(sizeHint)` throw `InvalidOperationException` when `sizeHint` exceeds the remaining space and return an empty buffer once full. The returned `IBufferWriter<T>` does not expose how much was written (its type is internal), so record the payload length yourself, for example with a small wrapper `IBufferWriter<byte>` that sums `Advance(count)` before forwarding.

## NativeArray<T> vs NativeMemoryArray<T>

| | Unity `NativeArray<T>` / `NativeList<T>` | `NativeMemoryArray<T>` |
|---|---|---|
| Purpose | data shared with the engine, Jobs and Burst | C#-side buffers for Span/stream/serializer IO |
| Type | `struct` handle | `sealed class` (can be a field, passed to async methods) |
| Length | `int` | `long` (beyond 2 GB) |
| Allocation | `Allocator.Temp` / `TempJob` / `Persistent`, leak detection | `Marshal.AllocHGlobal`, finalizer as backstop |
| Safety | `AtomicSafetyHandle`, job dependency checks, dispose sentinel | none: bounds-checked indexer and slices, no use-after-dispose check |
| Engine APIs | `Mesh.SetVertexBufferData`, `Texture2D.GetRawTextureData<T>`, `AsyncGPUReadback`, `JobHandle` | none; copy through `AsSpan()` (or `NativeArray.AsSpan()` / `NativeArrayUnsafeUtility`) |
| Burst | yes | no |

Use `NativeArray<T>` whenever data goes into Jobs, Burst or engine APIs. Use `NativeMemoryArray<T>` for multi-GB buffers, file and network IO, and serializer buffers that should stay off the managed heap.

## Pitfalls

- **Forgetting Dispose.** The finalizer frees the memory eventually, but only when the GC collects the small managed wrapper, which may be much later than the native memory pressure warrants (pass `addMemoryPressure: true` to help). Treat a missing `Dispose` as a leak.
- **Use after Dispose crashes the process.** The indexer and `AsSpan` check bounds but not disposal; they read freed native memory (access violation or silent corruption), not `ObjectDisposedException`. Null out fields after disposing and never keep `Span` / `Memory` slices beyond the array's lifetime.
- **Span across await.** `Span<T>` cannot live across `await` or in fields; use `AsMemory(...)` slices for async code. Span and Memory slices are limited to `int.MaxValue` elements: `AsSpan()` with no length throws `ArgumentOutOfRange`/overflow when the array is larger. Use `TryGetFullSpan(out var span)` or chunked sequences.
- **`AsStream(long offset)` in the UPM source** passes the full array length as the stream length even with an offset, so reading to the end runs past the buffer. Use `AsStream()` from offset 0, or the NuGet build's `AsStream(offset, length)`.
- **32-bit targets.** WebGL (wasm32) and 32-bit Android have a 2-4 GB address space and WebGL's heap is capped by the build's memory settings; multi-GB arrays only work on 64-bit platforms. `length * sizeof(T)` must also fit in `IntPtr`.
- **IL2CPP.** Works (no reflection); requires the Unsafe DLL to be included for the target platform. Keep "Allow unsafe code" on for assemblies that use `fixed`.
- **Thread safety.** None. Concurrent writers to overlapping ranges race; parallel work on disjoint `AsSpan(start, count)` slices is fine. `Dispose` while another thread reads is a use-after-free.
- **No `CopyFrom` / `CopyTo` methods** exist; copy with spans: `source.AsSpan().CopyTo(array.AsSpan(offset, source.Length))`.

## Related skills

- `memorypack`, `messagepack-csharp` for serializers writing into the buffer.
- `zstring` for the shared Unsafe DLL rule; `unitask` for async file IO in Unity.
- `csharp-unity` for house style.
