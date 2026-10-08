# Recipes and v2 to v3 migration

Verified against MessagePack-CSharp v3.1.11 `README.md`, `doc/migrating_v2-v3.md`, `doc/analyzers/*`, `src/MessagePack/Resolvers/StaticCompositeResolver.cs`, `tests/MessagePack.GeneratedCode.Tests/CompositeResolverTests.cs`.

## Custom formatter for a type you cannot annotate

```csharp
using MessagePack;
using MessagePack.Formatters;

public readonly struct ItemId
{
    public readonly uint Value;

    public ItemId(uint value)
    {
        this.Value = value;
    }
}

internal sealed class ItemIdFormatter : IMessagePackFormatter<ItemId>
{
    public static readonly ItemIdFormatter Instance = new ItemIdFormatter();

    public void Serialize(ref MessagePackWriter writer, ItemId value, MessagePackSerializerOptions options)
    {
        writer.Write(value.Value);
    }

    public ItemId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return new ItemId(reader.ReadUInt32());
    }
}
```

- `internal` (or `public`) plus a public parameterless constructor or `public static readonly Instance` is enough for the source generator to add it to the assembly's generated resolver (MsgPack010/013).
- For reference types, handle null: `if (value == null) { writer.WriteNil(); return; }` and `if (reader.TryReadNil()) { return null; }` (MsgPack014).
- For nested values use `options.Resolver.GetFormatterWithVerify<TField>().Serialize(ref writer, field, options)`.
- When reading untrusted data inside a custom formatter for a container, wrap with `options.Security.DepthStep(ref reader)` and `reader.Depth--` in `finally`, as the built-in formatters do.
- Alternative per member: `[Key(0)] [MessagePackFormatter(typeof(ItemIdFormatter))] public ItemId Id;`.

## Union hierarchy

```csharp
using MessagePack;

[Union(0, typeof(MoveCommand))]
[Union(1, typeof(AttackCommand))]
public interface ICommand
{
}

[MessagePackObject]
public sealed class MoveCommand : ICommand
{
    [Key(0)] public float X { get; set; }
    [Key(1)] public float Y { get; set; }
}

[MessagePackObject]
public sealed class AttackCommand : ICommand
{
    [Key(0)] public int TargetId { get; set; }
}
```

`MessagePackSerializer.Serialize<ICommand>(cmd, options)` writes `[0, [x, y]]`. Deserialize with the base type and switch on the result. Abstract base classes work the same way; their keyed members share the index space with the subclasses (subclass keys start after the base keys). An unknown union key from a newer peer deserializes to `null` (generated formatter skips it), so null-check results and ship readers before writers.

## Compile-time composite resolver

```csharp
using MessagePack;
using MessagePack.Resolvers;

[CompositeResolver(typeof(ItemIdFormatter), typeof(MessagePack.Unity.UnityResolver), typeof(StandardResolver))]
internal partial class GameResolver
{
}
```

Use `GameResolver.Instance` in `WithResolver`. `IncludeLocalFormatters = true` adds every formatter declared in the same assembly. Compared with `StaticCompositeResolver`, it has no global state and no "register before first use" rule, which suits libraries.

## Save file with UniTask

```csharp
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePack;
using UnityEngine;

public sealed class SaveStore
{
    #region Fields
    private readonly string _path;
    private readonly MessagePackSerializerOptions _options;
    #endregion

    #region Constructors
    public SaveStore(string fileName, MessagePackSerializerOptions options)
    {
        this._path = Path.Combine(Application.persistentDataPath, fileName);
        this._options = options
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithSecurity(MessagePackSecurity.UntrustedData);
    }
    #endregion

    #region Public API
    public async UniTask SaveAsync(PlayerSave save, CancellationToken cancellationToken)
    {
        var tempPath = this._path + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            await MessagePackSerializer.SerializeAsync(stream, save, this._options, cancellationToken);
        }

        if (File.Exists(this._path))
        {
            File.Delete(this._path);
        }

        File.Move(tempPath, this._path);
    }

    public async UniTask<PlayerSave> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(this._path))
        {
            return new PlayerSave();
        }

        using (var stream = new FileStream(this._path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
        {
            return await MessagePackSerializer.DeserializeAsync<PlayerSave>(stream, this._options, cancellationToken);
        }
    }
    #endregion
}
```

- Save files are user-editable, so treat them as untrusted.
- Write to a temp file and swap, so a crash mid-write does not corrupt the only copy.
- On WebGL, `persistentDataPath` is IndexedDB-backed and has no threads; the async API still works but runs on the main thread.

## Streams with several messages

```csharp
using (var reader = new MessagePackStreamReader(stream))
{
    while (await reader.ReadAsync(cancellationToken) is ReadOnlySequence<byte> message)
    {
        var packet = MessagePackSerializer.Deserialize<Packet>(message, options, cancellationToken);
    }
}
```

Each `Serialize(stream, value)` call appends one self-delimiting msgpack value; `MessagePackStreamReader` splits them back.

## Debugging payloads

- `MessagePackSerializer.ConvertToJson(bytes, options)` prints int-key objects as arrays (`[99,"hoge"]`) and string-key objects as maps. Pass the same options when the payload is LZ4-compressed.
- `SerializeToJson(obj, options)` shows what would be written without a round trip.
- Never ship JSON conversion in hot paths; it allocates heavily.

## Migrating v2 to v3

| v2 | v3 |
|---|---|
| `mpc` CLI (or the Unity "MessagePack > CodeGenerator" window) generates formatters and a resolver into a file | Roslyn source generator inside `MessagePackAnalyzer` (a dependency of `MessagePack`) generates them every compile; delete mpc scripts and the old generated `.cs` files |
| Generated resolver typically `MessagePack.Resolvers.GeneratedResolver` from mpc, registered by hand | `MessagePack.GeneratedMessagePackResolver` per assembly, discovered automatically by `StandardResolver` via `SourceGeneratedFormatterResolver`; register explicitly only to restrict or reorder |
| `MessagePackAnalyzer.json` lists extra formattable types | `[assembly: MessagePackAssumedFormattable(typeof(T))]`, `[assembly: MessagePackKnownFormatter(typeof(F))]`, `[GeneratedMessagePackResolver]` |
| Unity: `.unitypackage` from GitHub Releases with source | NuGetForUnity `MessagePack` + UPM `...?path=src/MessagePack.UnityClient/Assets/Scripts/MessagePack#vX.Y.Z` |
| Unity 2018/2019/2021 supported | Unity 2022.3.12f1 minimum |
| Custom formatters any accessibility, registered by hand | `internal`/`public` formatters are auto-included in the generated resolver; keep explicit registration only for formatters in other assemblies |
| `[MessagePackObject]` types with private keyed members used `*AllowPrivate` resolvers | make the type `partial` and set `AllowPrivate = true` so the generated formatter can reach private members |
| Analyzer optional | analyzer always on; new MsgPack0xx warnings appear after upgrading, treat them as the AOT checklist |

Steps:

1. Remove the old `Assets/Plugins/MessagePack` (or `Assets/Scripts/MessagePack`) source folder and any `.unitypackage` remnants, including the old `MessagePack.Unity` asmdef copies.
2. Install the NuGet package and the UPM package at the same `X.Y.Z`.
3. Delete mpc-generated files and the build step that ran mpc; rebuild so the source generator runs.
4. Replace references to the old generated resolver name with `GeneratedMessagePackResolver.Instance` (or name your own with `[GeneratedMessagePackResolver]`).
5. Convert `MessagePackAnalyzer.json` entries to assembly attributes, then delete the JSON file.
6. Fix analyzer warnings: add `partial`, `AllowPrivate`, accessible formatter instances.
7. The migration guide lists no wire-format changes for the same contracts, but verify by deserializing stored v2 blobs (saves, cached payloads) in a test before shipping.

v1 to v2 (only for very old projects): the API moved to `MessagePackSerializerOptions`, `IMessagePackFormatter<T>` switched to `ref MessagePackWriter` / `ref MessagePackReader`, and `LZ4MessagePackSerializer` became `WithCompression(...)`. See `doc/migrating_v1-v2.md` in the repo.

## IL2CPP checklist

1. Every DTO and every nested member type has `[MessagePackObject]` (or a built-in/custom formatter); zero MsgPack003/004/008/016 warnings.
2. One bootstrap sets `DefaultOptions` and caches explicit options; it includes `StandardResolver` (or the generated resolvers of every DTO assembly) and `UnityResolver` when Unity types are used.
3. No `Typeless`, `Contractless*`, `DynamicObjectResolver*` or `SuppressSourceGeneration` on paths that run in the player.
4. Managed stripping: generated resolvers are reached through the assembly attribute and a public static `Instance` field (annotated `DynamicallyAccessedMembers`); if a stripping level of High drops them, add a `link.xml` that preserves the DTO assembly.
5. Round-trip test in a development IL2CPP build on each target.
