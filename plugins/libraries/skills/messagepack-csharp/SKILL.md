---
name: messagepack-csharp
description: Writes and reviews binary serialization in Unity and .NET with MessagePack-CSharp v3 (NuGet MessagePack, UPM com.github.messagepack-csharp). Covers install via NuGetForUnity plus the MessagePack.Unity git URL, the v3 source generator (no mpc), GeneratedMessagePackResolver, [GeneratedMessagePackResolver] / [CompositeResolver], IL2CPP resolver registration with StaticCompositeResolver, [MessagePackObject] / [Key(int)] / [Key(string)] / keyAsPropertyName / [IgnoreMember] / [SerializationConstructor] / [Union], serialization callbacks, MessagePackSerializer Serialize / Deserialize / SerializeAsync / DeserializeAsync / ConvertToJson, options (WithResolver, Lz4BlockArray, MessagePackSecurity.UntrustedData), UnityResolver / UnityBlitResolver, custom IMessagePackFormatter<T>, typeless/contractless risks, version tolerance and v2 to v3 migration. Use when the user mentions MessagePack, msgpack, MessagePackObject, MagicOnion DTOs, save or network payload serialization, or "formatter not found" on IL2CPP.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/MessagePack-CSharp/MessagePack-CSharp"
  unity: "6000.0+"
---

# MessagePack-CSharp

Fast MessagePack (msgpack.org) serializer for C#. Verified against MessagePack-CSharp v3.1.11 (`src/MessagePack`, `src/MessagePack.Annotations`, `src/MessagePack.UnityClient`, `README.md`, `doc/migrating_v2-v3.md`). Unity minimum 2022.3.12f1.

Reference files, read when needed:

- `references/attributes-and-resolvers.md` - read for the full attribute table, built-in resolver table, options/security members, and the Unity type list.
- `references/recipes-and-migration.md` - read when writing a Unity bootstrap, a custom formatter, a composite resolver, Union hierarchies, streaming, save files, or migrating from v2 (mpc) to v3.

## Workflow

1. Install in Unity (both steps, not one):
   - NuGetForUnity (`https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity`), then `NuGet > Manage NuGet Packages`, install `MessagePack`. This brings `MessagePack.Annotations` and `MessagePackAnalyzer` (analyzers + source generator).
   - UPM git URL, same version as the NuGet package: `https://github.com/MessagePack-CSharp/MessagePack-CSharp.git?path=src/MessagePack.UnityClient/Assets/Scripts/MessagePack#v3.1.11` (package `com.github.messagepack-csharp`, asmdef `MessagePack.Unity`, `allowUnsafeCode: true`). Tags are `vX.Y.Z`.
   - .NET server sharing Unity types: NuGet `MessagePack.UnityShims` (shim `UnityEngine.Vector3` etc. plus the same `UnityResolver`).
2. Annotate data types: `[MessagePackObject]` + `[Key(0..n)]` on every serialized public member, `[IgnoreMember]` on the rest. Make types `partial` when private members are serialized.
3. Let the source generator produce formatters at compile time. `StandardResolver` already contains `SourceGeneratedFormatterResolver`, which finds them through the assembly-level `[GeneratedAssemblyMessagePackResolver]` attribute the generator emits.
4. Configure options once at startup (see bootstrap below) and pass them explicitly at call sites (analyzer MsgPack001 flags reliance on the mutable `DefaultOptions`).
5. Build IL2CPP and round-trip every DTO on device. The Editor (Mono) can fall back to Reflection.Emit formatters and hide missing generation.

## Contract

```csharp
using System;
using MessagePack;

[MessagePackObject(AllowPrivate = true)]
public sealed partial class PlayerSave : IMessagePackSerializationCallbackReceiver
{
    [Key(0)] public int Version { get; set; } = 2;
    [Key(1)] public string Name { get; set; } = "";
    [Key(2)] public int Level { get; set; }
    [Key(3)] private long _lastPlayedUnixMs;
    [Key(4)] public InventoryItem[] Items { get; set; } = Array.Empty<InventoryItem>();

    [IgnoreMember] public DateTimeOffset LastPlayed => DateTimeOffset.FromUnixTimeMilliseconds(this._lastPlayedUnixMs);

    public void Touch(DateTimeOffset now) => this._lastPlayedUnixMs = now.ToUnixTimeMilliseconds();

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (this.Items == null)
        {
            this.Items = Array.Empty<InventoryItem>();
        }
    }
}

[MessagePackObject]
public readonly struct InventoryItem
{
    [Key(0)] public readonly int ItemId;
    [Key(1)] public readonly int Count;

    [SerializationConstructor]
    public InventoryItem(int itemId, int count)
    {
        this.ItemId = itemId;
        this.Count = count;
    }
}
```

- Int keys serialize as a msgpack array (smallest, fastest). String keys (`[Key("name")]` or `[MessagePackObject(keyAsPropertyName: true)]`) serialize as a map: larger and slower, but self-describing and friendlier to other languages and to debugging.
- Do not mix int and string keys in one type. Keys in a derived class must not collide with the base class keys (one flat array/map).
- `[SerializationConstructor]` picks the constructor; parameters match int keys by position and string keys by name. Without it, the best-matching constructor is chosen or `can't find matched constructor parameter` is thrown.
- `[Union(key, typeof(Sub))]` on an interface or abstract class enables polymorphism; payload is `[key, object]`. Never reuse or renumber union keys.
- `IMessagePackSerializationCallbackReceiver` gives `OnBeforeSerialize` / `OnAfterDeserialize`; use it to repair defaults, not for heavy work.
- Non-public keyed members need a `partial` type (MsgPack011, the generated formatter is nested inside it) and `AllowPrivate = true` (MsgPack015, also lets the dynamic fallback see them); `SuppressSourceGeneration = true` opts a type out of AOT generation (it then needs Reflection.Emit, so it fails on IL2CPP).

## API

```csharp
var options = MessagePackSerializerOptions.Standard;
byte[] bytes = MessagePackSerializer.Serialize(save, options);
PlayerSave loaded = MessagePackSerializer.Deserialize<PlayerSave>(bytes, options);
string json = MessagePackSerializer.ConvertToJson(bytes, options);
```

| Need | Call |
|---|---|
| bytes | `Serialize<T>(T, options, ct)` -> `byte[]`; `Deserialize<T>(ReadOnlyMemory<byte>, options, ct)` |
| pooled buffer | `Serialize<T>(IBufferWriter<byte>, T, options, ct)`; `Deserialize<T>(in ReadOnlySequence<byte>, options, ct)` |
| stream | `Serialize<T>(Stream, T, ...)`, `SerializeAsync<T>(Stream, T, options, ct)` -> `Task`; `Deserialize<T>(Stream, ...)`, `DeserializeAsync<T>(Stream, options, ct)` -> `ValueTask<T>` |
| several objects in one stream | `MessagePackStreamReader.ReadAsync(ct)` loop |
| debug | `ConvertToJson(bytes)`, `SerializeToJson(obj)`, `ConvertFromJson(json)` |
| runtime `Type` | non-generic overloads `Serialize(Type, ...)`, `DeserializeAsync(Type, Stream, ...)` |

Prefer `Deserialize(ReadOnlyMemory<byte>)` over the `Stream` overload; the stream version copies into a sequence first. `Serialize` returning `byte[]` copies out of the pool; write into an `IBufferWriter<byte>` on hot paths.

## Options

```csharp
var options = MessagePackSerializerOptions.Standard
    .WithResolver(StaticCompositeResolver.Instance)
    .WithCompression(MessagePackCompression.Lz4BlockArray)
    .WithSecurity(MessagePackSecurity.UntrustedData);
```

- Options are immutable; every `WithX` returns a copy. Build once, cache in a `static readonly` field.
- `Lz4BlockArray` (recommended) or `Lz4Block` uses msgpack ext codes 98/99; other languages cannot read it without the same scheme. Worth it for string-key data and save files; little gain on small int-key packets.
- `MessagePackSecurity.UntrustedData`: hash-collision-resistant dictionaries, max object depth 500, max decompressed size 64 MiB. Use it for anything from the network, user-editable files, or mod content. It hardens, it does not authenticate; add a MAC/signature if tampering matters.

## Unity resolvers

- The UPM package's `MessagePack.Unity.MessagePackInitializer` sets `DefaultOptions = Standard.WithResolver(UnityResolver.InstanceWithStandardResolver)` at `SubsystemRegistration`. Anything that later assigns `DefaultOptions` with its own resolver (MagicOnion bootstrap, your own) drops it unless you add `UnityResolver.Instance` back.
- `UnityResolver` (`MessagePack.Unity`): `Vector2/3/4`, `Quaternion`, `Color`, `Color32`, `Bounds`, `Rect`, `Matrix4x4`, `AnimationCurve`, `Keyframe`, `Gradient`, `RectOffset`, `LayerMask`, `Vector2Int`, `Vector3Int`, `RangeInt`, `RectInt`, `BoundsInt`, plus their nullable, array and list forms.
- `UnityBlitResolver` / `UnityBlitWithPrimitiveArrayResolver` (`MessagePack.Unity.Extension`): raw memory blit of `Vector3[]` etc. (ext codes 30-39), about 20x faster than `JsonUtility`; .NET-and-Unity only, same endianness assumed.
- Custom formatter for a type you do not own: implement `IMessagePackFormatter<T>`, make it `internal` or `public` with a public parameterless constructor or `public static readonly Instance`, and it is added to the generated resolver automatically. See `references/recipes-and-migration.md`.

## Bootstrap (Unity, IL2CPP-safe)

```csharp
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Unity;
using UnityEngine;

public static class MessagePackBootstrap
{
    #region Fields
    private static bool _registered;
    #endregion

    #region Properties
    public static MessagePackSerializerOptions Options { get; private set; } = MessagePackSerializerOptions.Standard;
    public static MessagePackSerializerOptions UntrustedOptions { get; private set; } = MessagePackSerializerOptions.Standard;
    #endregion

    #region Initialization
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (!_registered)
        {
            StaticCompositeResolver.Instance.Register(
                GeneratedMessagePackResolver.Instance,
                UnityResolver.Instance,
                StandardResolver.Instance);
            _registered = true;
        }

        Options = MessagePackSerializerOptions.Standard.WithResolver(StaticCompositeResolver.Instance);
        UntrustedOptions = Options.WithSecurity(MessagePackSecurity.UntrustedData);
        MessagePackSerializer.DefaultOptions = Options;
    }
    #endregion
}
```

- `GeneratedMessagePackResolver` (namespace `MessagePack`, internal `partial class`) is generated per assembly that has `[MessagePackObject]` types or custom formatters; put the bootstrap in the asmdef that owns your DTOs, or drop that line. It holds only that assembly's formatters. Types in other asmdefs are still found through `StandardResolver` -> `SourceGeneratedFormatterResolver`. Rename it with `[GeneratedMessagePackResolver] partial class MyResolver { }`; aggregate formatters/resolvers at compile time with `[CompositeResolver(typeof(...), ...)] partial class MyCompositeResolver { }`.
- `StaticCompositeResolver.Register` throws after the first `GetFormatter` call, and the static instance survives Play sessions when domain reload is disabled. The `_registered` guard handles both.

## Typeless and contractless

- `MessagePackSerializer.Typeless` / `TypelessContractlessStandardResolver` embed .NET type names (ext 100) and instantiate whatever type the payload names. Never use them on untrusted data (arbitrary type instantiation), they are .NET-only, renames break old blobs, and they rely on Reflection.Emit, so they fail on IL2CPP.
- `ContractlessStandardResolver` / `DynamicContractlessObjectResolver` serialize unattributed types with string keys via Reflection.Emit at runtime: no source generation, so IL2CPP throws, and every public member becomes part of the wire format by accident. Use explicit `[MessagePackObject]` contracts in Unity.
- `Deserialize<object>` / `dynamic` returns primitives, `object[]` and `IDictionary<object, object>`; fine for tooling, not for game code.

## Version tolerance (int keys)

- Missing keys deserialize to `default`; extra trailing keys are skipped. So: append new members with the next index, never reorder, never reuse an index of a removed member (mark it `[Obsolete]` or leave a gap), never change a member's type.
- Gaps cost a nil byte each (`[Key(3)]`, `[Key(10)]` writes 11 slots); keep keys dense.
- Store a schema `Version` field in save data so `OnAfterDeserialize` can migrate.
- Renaming a C# member is safe with int keys and breaks string-key data.

## MessagePack vs MemoryPack

| | MessagePack-CSharp | MemoryPack (`memorypack`) |
|---|---|---|
| Format | msgpack spec, readable from JS, Go, Python, Rust, C++ peers | C#-specific binary layout |
| Speed | very fast | faster (near memcpy for unmanaged structs) |
| Schema evolution | int/string keys, append-only | append-only; `VersionTolerant` mode for more |
| Debugging | `ConvertToJson` | none built-in |
| MagicOnion | default serializer | preview provider |

Choose MessagePack when any peer is not C#, when you need a documented wire format (web backend, tooling, analytics), or when using MagicOnion with defaults (see `magiconion`). Choose MemoryPack for C#-only hot paths and large struct arrays.

## Pitfalls

- **Works in Editor, `FormatterNotRegisteredException` / "formatter not found" on device**: the type was not source-generated (missing `[MessagePackObject]`, `SuppressSourceGeneration`, a `KeyAttribute`-derived attribute (MsgPack016), or the type lives in an assembly where the analyzer did not run), or `DefaultOptions` was replaced with a resolver that omits `StandardResolver` / the generated resolver. Fix the analyzer warnings; they are the AOT checklist.
- **Key reuse**: reusing an int key or union key for a different member silently deserializes old data into the wrong field. Treat keys as permanent.
- **Untrusted data**: default `TrustedData` allows hash-flooding and deep-nesting DoS; typeless allows type injection. Use `UntrustedData` for network/mod/save input and enforce a size limit before deserializing.
- **DateTime kinds**: the standard timestamp format calls `ToUniversalTime()` on write (so `Unspecified` is treated as local and shifted) and always returns `DateTimeKind.Utc`. Store `DateTimeOffset` or Unix milliseconds, or use `NativeDateTimeResolver` (.NET-only, keeps `Kind`).
- **String-key cost**: string keys and `keyAsPropertyName` roughly double payload size and slow deserialization (key lookup per member). Use int keys for network packets; reserve string keys for interop or human-diffable data.
- **Global `DefaultOptions`**: libraries and plugins that set it overwrite each other (MagicOnion, MessagePack.Unity initializer, your code). Set it in one bootstrap and pass explicit options elsewhere.
- **`Guid` and `decimal`** serialize as strings by default; `NativeGuidResolver` / `NativeDecimalResolver` are faster but not interoperable.
- **Generic `init` properties**: avoid `init` setters on generic types with public-only resolvers (CLR bug, see analyzer MsgPack017 for initializers).
- **Unity serialization overlap**: `[SerializeField] private` fields are not serialized by MessagePack unless keyed and accessible (`partial` type or `AllowPrivate`). Keep save DTOs separate from MonoBehaviours.

## Related skills

- `magiconion` - MessagePack is its default wire format.
- `memorypack` - C#-only alternative.
- `unitask` - awaiting `DeserializeAsync` / file IO without blocking the main thread.
- `csharp-unity` - house style for DTOs and bootstrap code.
