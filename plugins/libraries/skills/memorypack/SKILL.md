---
name: memorypack
description: Serializes C# objects to a compact binary format in Unity 6 with Cysharp MemoryPack (source-generated, reflection-free, IL2CPP-safe). Covers install via NuGetForUnity (MemoryPack core + generator) plus the MemoryPack.Unity UPM git URL, [MemoryPackable] partial types, GenerateType.Object / VersionTolerant / CircularReference / Collection, [MemoryPackOrder], [MemoryPackIgnore], [MemoryPackInclude], [MemoryPackConstructor], [MemoryPackUnion] and union formatters, OnSerializing/OnDeserialized callbacks, MemoryPackSerializer.Serialize / Deserialize with byte[], IBufferWriter<byte>, Stream and async APIs, overwrite deserialization, Unity type formatters (Vector3, Quaternion, Color, AnimationCurve, Gradient), MemoryPackFormatterProvider.Register, version-tolerance rules, Brotli compression, and save-game / network / binary game-data use. Use for MemoryPack, binary serialization, fast save files, replacing JsonUtility / Newtonsoft / MessagePack, MEMPACK diagnostics, or member-order bugs.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/MemoryPack"
  unity: "6000.0+"
---

# MemoryPack

Zero-encoding binary serializer: a Roslyn source generator writes the formatter for every `[MemoryPackable]` type, so there is no reflection or IL emit at runtime. The format carries no member names and no type info; the C# type is the schema. Verified against MemoryPack 1.21.4 (`src/MemoryPack.Core`, `src/MemoryPack.Generator`, `src/MemoryPack.Unity`).

Reference files, read when needed:

- `references/attributes.md` - read for the full attribute table, GenerateType / SerializeLayout semantics, constructor rules, callbacks, and the version-tolerance matrix.
- `references/recipes.md` - read for save-game, network payload, binary game-data, custom formatter, union, overwrite, and compression code.

## Workflow

1. Install the core from NuGet with NuGetForUnity (Unity 2022.3.12f1+ / Unity 6):
   - Add NuGetForUnity (its own UPM git URL / OpenUPM), then `NuGet > Manage NuGet Packages`, search `MemoryPack`, Install. That pulls `MemoryPack.Core`, `MemoryPack.Generator`, `System.Runtime.CompilerServices.Unsafe` and `System.Collections.Immutable`.
   - Check that `MemoryPack.Generator.dll` is imported as an analyzer: no platforms selected in its Inspector and the `RoslynAnalyzer` asset label set. NuGetForUnity normally does this; if generated code is missing, set it by hand (Unity manual: Roslyn analyzers and source generators).
   - On "Assembly Version Validation" conflicts, untick it in Player Settings > Other Settings > Configuration.
2. Add Unity type support: Package Manager > Add package from git URL `https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity` (pin with `#1.21.4`; tags are plain `x.y.z`). The package id is `com.cysharp.memorypack`; its asmdef `MemoryPack.Unity` precompile-references `MemoryPack.Core.dll`, so step 1 must come first.
3. Mark data types `[MemoryPackable] public partial class|struct|record ...`. Only `partial` types get generated code; the generator reports `MEMPACK001`..`MEMPACK035` diagnostics in the Console.
4. Serialize with `MemoryPackSerializer.Serialize(value)` and read back with `MemoryPackSerializer.Deserialize<T>(bytes)`.
5. Decide schema evolution up front: `GenerateType.Object` (default, append-only) or `GenerateType.VersionTolerant` (explicit `[MemoryPackOrder]`, can delete). Save files and shipped data need one of these rules followed forever.
6. Test round-trips in an IL2CPP player build on each target platform before shipping.

## Unity C# 9 limits

Unity 6 compiles C# 9, so the generator emits Unity-compatible code (no `scoped`, no static abstract members). In your own types:

- No `record struct`, no `required` members (C# 10/11). Plain `record` works.
- `init` accessors and `record` need `System.Runtime.CompilerServices.IsExternalInit`; MemoryPack's copy is `internal`. If the compiler reports it missing, add `namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }` to your assembly.
- `ModuleInitializer` does not run in Unity: union formatters declared with `[MemoryPackUnionFormatter]` must be registered manually (`{FormatterName}Initializer.RegisterFormatter()`) at startup.
- `MemoryPackCustomFormatterAttribute`-based member formatters (`[Utf16StringFormatter]`, `[BitPackFormatter]`, `[BrotliFormatter]`, ...) are not supported in Unity. Register type-level formatters with `MemoryPackFormatterProvider.Register` instead.
- Custom formatter overrides use `ref T? value` (no `scoped`) and an unconstrained `Serialize<TBufferWriter>` override (the base declares `where TBufferWriter : class, IBufferWriter<byte>` on netstandard2.1).

## Serialization API

| Call | Notes |
|---|---|
| `byte[] Serialize<T>(in T? value, MemoryPackSerializerOptions? options = default)` | simplest |
| `void Serialize<T, TBufferWriter>(in TBufferWriter bufferWriter, in T? value, options)` | fastest; `TBufferWriter : class, IBufferWriter<byte>` in Unity |
| `ValueTask SerializeAsync<T>(Stream stream, T? value, options, CancellationToken)` | serializes fully into a pooled buffer, then `WriteAsync` |
| `T? Deserialize<T>(ReadOnlySpan<byte> buffer, options)` / `(in ReadOnlySequence<byte>)` | |
| `int Deserialize<T>(ReadOnlySpan<byte> buffer, ref T? value, options)` | overwrite into an existing instance; returns bytes consumed |
| `ValueTask<T?> DeserializeAsync<T>(Stream stream, options, CancellationToken)` | reads to end of stream first |
| Non-generic `Serialize(Type, object?, ...)` / `Deserialize(Type, ...)` | reflection-free; for runtime-typed code |

`MemoryPackSerializerOptions.Default` / `.Utf8` (default, smaller for ASCII) / `.Utf16` (faster, larger). Deserialization detects the string encoding automatically.

## What gets serialized

- Public instance fields and properties (including `get`-only and private-set) of a `[MemoryPackable]` class/struct/record, in declaration order, base class first.
- `[MemoryPackIgnore]` removes a public member; `[MemoryPackInclude]` adds a private one. Private members are skipped otherwise.
- Unmanaged structs (no reference fields) are copied as raw memory including padding: attributes on them are ignored and their layout can never change.
- Members must themselves be serializable: primitives, `string`, enums, unmanaged structs, other `[MemoryPackable]` types, arrays, `List<>`, `Dictionary<,>`, `HashSet<>`, tuples, `Nullable<>` and the other built-ins listed in the README. Interfaces and abstract classes need `[MemoryPackUnion]`.
- MemoryPack.Unity registers formatters at `RuntimeInitializeLoadType.AfterAssembliesLoaded` for `Vector2/3/4`, `Quaternion`, `Color`, `Color32`, `Bounds`, `Rect`, `Keyframe`, `WrapMode`, `Matrix4x4`, `GradientColorKey`, `GradientAlphaKey`, `GradientMode`, `LayerMask`, `Vector2Int`, `Vector3Int`, `RangeInt`, `RectInt`, `BoundsInt` (plus their arrays, `List<>` and `Nullable<>`), and classes `AnimationCurve`, `Gradient`, `RectOffset`.
- `UnityEngine.Object` references (`GameObject`, `Sprite`, `ScriptableObject`) cannot be serialized; store an id or Addressables key instead.

## Version tolerance (summary)

| Change | `GenerateType.Object` | `GenerateType.VersionTolerant` |
|---|---|---|
| Add member at the end | OK (missing data reads as `default`) | OK with a new, unused `[MemoryPackOrder]` |
| Delete member | breaks | OK; never reuse its order number |
| Rename member | OK | OK |
| Reorder members | breaks | breaks (order number is identity) |
| Change member type | breaks | breaks |
| Change an unmanaged struct | breaks | breaks |

Old data read into a newer type sets missing members to `default`; add `[SuppressDefaultInitialization]` to keep the field initializer value. Version-tolerant payloads are slightly larger and slower. `GenerateType.CircularReference` behaves like version-tolerant, needs `[MemoryPackOrder]` on every member and a parameterless constructor, and preserves object identity.

## Comparison

| Serializer | Format | Unity notes | Pick when |
|---|---|---|---|
| `JsonUtility` | JSON, Unity serialization rules | no dictionaries, no polymorphism, no properties | small configs, editor data, human-readable |
| Newtonsoft (`com.unity.nuget.newtonsoft-json`) | JSON | reflection, IL2CPP stripping needs `link.xml` / `[Preserve]`, slow, GC heavy | interop with JSON APIs, flexible schemas |
| MessagePack-CSharp | MessagePack (self-describing, varint) | needs its own generator/AOT setup; cross-language | cross-language payloads, string-keyed tolerance |
| MemoryPack | C#-specific binary, no names | source-generated, fastest, smallest for floats/vectors | C#-only save games, client-server in C#, baked game data |

README benchmark claim: in Unity, MemoryPack is x3 to x10 faster than `JsonUtility`. MemoryPack ints are always 4 bytes (MessagePack uses varint), so int-heavy data can be larger; float/vector data is smaller.

## Pitfalls

- **Reordering or deleting members** in `GenerateType.Object` silently corrupts old saves (values shift into wrong members) or throws `MemoryPackSerializationException`. Freeze the order, append only, or switch to `VersionTolerant` before the first release.
- **Private fields** are not serialized unless marked `[MemoryPackInclude]`. `[SerializeField] private` Unity fields are not picked up automatically.
- **Interfaces / abstract members** without `[MemoryPackUnion]` fail generation. Union tags (`0..65535`, compact below 250) are part of the format: never reuse or renumber.
- **Endianness**: the format is little-endian and the implementation does not swap bytes. All current Unity targets are little-endian, so cross-platform saves work, but do not assume portability to big-endian hardware.
- **.NET 7+ server interop**: types with `[StructLayout(LayoutKind.Auto)]`, notably `DateTimeOffset` and `ValueTuple`, are not binary-compatible between Unity and .NET 7+. Avoid them in shared payloads; use `long` ticks or a small `[MemoryPackable]` struct.
- **Untrusted input** (downloaded saves, network packets): there is no type-name polymorphism, so a payload cannot instantiate arbitrary types, but a malformed length header can still allocate large arrays or throw. Cap payload size before deserializing, catch `MemoryPackSerializationException`, and authenticate or checksum data you did not produce.
- **Unity callbacks vs MemoryPack callbacks**: `ISerializationCallbackReceiver.OnBeforeSerialize` / `OnAfterDeserialize` are never called by MemoryPack. Use `[MemoryPackOnSerializing]`, `[MemoryPackOnSerialized]`, `[MemoryPackOnDeserializing]`, `[MemoryPackOnDeserialized]`. Instance `OnDeserializing` only runs when deserializing into an existing instance via `ref`.
- **No static constructor** on `[MemoryPackable]` types (the generator uses it). Write `static partial void StaticConstructor()` instead.
- **Duplicate `System.Runtime.CompilerServices.Unsafe.dll`** when another package (for example a ZString git install, see `zstring`) also ships it: keep exactly one copy.
- **Async APIs are not streaming**: `SerializeAsync` / `DeserializeAsync` buffer the whole payload. For large saves on the main thread, serialize to a pooled `IBufferWriter<byte>` and write the file on a worker thread (see `unitask` for thread switching; WebGL has no threads).
- **WebGL**: core serialization works (generated code, no threads or JIT needed). `Application.persistentDataPath` is IndexedDB-backed there, so verify that save files persist across reloads on your Unity version; Brotli availability on WebGL is unverified.

## Related skills

- `csharp-unity` for house style used in the recipes.
- `unitask` for async file IO and thread hopping around serialization.
- `zstring` for zero-allocation text output alongside binary data.
- `game-data-pipeline` for where binary baked data fits next to ScriptableObjects and CSV imports.
