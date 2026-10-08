# MemoryPack recipes for Unity

Examples follow the `csharp-unity` house style: `this.` on instance fields, `var`, `#region` layout, no LINQ.

## Save game with versioned schema

```csharp
using System.Collections.Generic;
using MemoryPack;
using UnityEngine;

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class SaveData
{
    #region Fields
    [MemoryPackOrder(0)] public int Version;
    [MemoryPackOrder(1)] public string PlayerName;
    [MemoryPackOrder(2)] public Vector3 Position;
    [MemoryPackOrder(3)] public Quaternion Rotation;
    [MemoryPackOrder(4)] public List<InventorySlot> Inventory = new();
    [MemoryPackOrder(6)] public Dictionary<string, bool> Flags = new();
    #endregion

    #region Callbacks
    [MemoryPackOnDeserialized]
    private void OnDeserialized()
    {
        if (this.Inventory == null) this.Inventory = new List<InventorySlot>();
        if (this.Flags == null) this.Flags = new Dictionary<string, bool>();
    }
    #endregion
}

[MemoryPackable]
public partial struct InventorySlot
{
    public int ItemId;
    public int Count;
}
```

Order 5 belonged to a deleted member and is never reused. `InventorySlot` is an unmanaged struct: it is copied as raw memory and can never change shape, so add a new struct type rather than editing it after release.

```csharp
using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;

public sealed class SaveService
{
    #region Fields
    private const int MaxSaveBytes = 16 * 1024 * 1024;
    private readonly string _path;
    #endregion

    #region Constructors
    public SaveService(string fileName)
    {
        this._path = Path.Combine(Application.persistentDataPath, fileName);
    }
    #endregion

    #region Public API
    public async UniTask SaveAsync(SaveData data, CancellationToken cancellationToken)
    {
        var bytes = MemoryPackSerializer.Serialize(data);
        var tempPath = this._path + ".tmp";
        await UniTask.SwitchToThreadPool();
        File.WriteAllBytes(tempPath, bytes);
        if (File.Exists(this._path)) File.Delete(this._path);
        File.Move(tempPath, this._path);
        await UniTask.SwitchToMainThread(cancellationToken);
    }

    public SaveData Load()
    {
        if (!File.Exists(this._path)) return new SaveData();

        var info = new FileInfo(this._path);
        if (info.Length > MaxSaveBytes) return new SaveData();

        try
        {
            var bytes = File.ReadAllBytes(this._path);
            return MemoryPackSerializer.Deserialize<SaveData>(bytes) ?? new SaveData();
        }
        catch (MemoryPackSerializationException ex)
        {
            Debug.LogException(ex);
            return new SaveData();
        }
    }
    #endregion
}
```

Serialize on the main thread (it reads live objects), write the file off the main thread. On WebGL there is no thread pool; skip the switch.

## Network payload with a union

```csharp
using MemoryPack;
using UnityEngine;

[MemoryPackable]
[MemoryPackUnion(0, typeof(MoveMessage))]
[MemoryPackUnion(1, typeof(ChatMessage))]
public partial interface INetMessage
{
}

[MemoryPackable]
public partial class MoveMessage : INetMessage
{
    public int EntityId;
    public Vector3 Position;
}

[MemoryPackable]
public partial class ChatMessage : INetMessage
{
    public string Text;
}

public static class NetCodec
{
    #region Public API
    public static byte[] Encode(INetMessage message)
    {
        return MemoryPackSerializer.Serialize(message);
    }

    public static INetMessage Decode(byte[] packet)
    {
        return MemoryPackSerializer.Deserialize<INetMessage>(packet);
    }
    #endregion
}
```

Dispatch with `switch (message) { case MoveMessage move: ... }`. Tags are wire identity: append new ones, never renumber. Reject packets above your protocol's size limit before decoding.

## Reusing instances (overwrite)

```csharp
private SaveData _cache = new();

public void Reload(byte[] bytes)
{
    MemoryPackSerializer.Deserialize(bytes, ref this._cache);
}
```

Reused when possible: the root and nested objects with parameterless constructors, same-length arrays, and collections with `Clear()` (`List<>`, `Dictionary<,>`, `HashSet<>`, `Queue<>`, `Stack<>`, ...). Anything else is replaced with a new instance.

## Binary game data instead of a large ScriptableObject

Bake tables in the Editor to a `.bytes` file, ship it as a `TextAsset` (Resources or Addressables), and deserialize once at startup:

```csharp
using MemoryPack;
using UnityEngine;

[MemoryPackable]
public partial class EnemyTable
{
    public EnemyRow[] Rows;
}

[MemoryPackable]
public partial struct EnemyRow
{
    public int Id;
    public int Hp;
    public float Speed;
}

public sealed class EnemyDatabase : MonoBehaviour
{
    #region Fields
    [SerializeField] private TextAsset tableAsset;
    private EnemyTable _table;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        this._table = MemoryPackSerializer.Deserialize<EnemyTable>(this.tableAsset.bytes);
    }
    #endregion
}
```

Editor bake step: build the `EnemyTable` from your source (CSV, spreadsheet, ScriptableObjects), then `File.WriteAllBytes("Assets/Data/enemies.bytes", MemoryPackSerializer.Serialize(table));` and `AssetDatabase.ImportAsset(...)`. See `game-data-pipeline` for source formats and `zbase-csv-reader` for CSV import. Trade-off: no Inspector editing of baked data and no asset references inside it, in exchange for fast loads and small files.

## Custom formatter for a type you do not own

```csharp
using System.Text.RegularExpressions;
using MemoryPack;
using UnityEngine;

public sealed class RegexFormatter : MemoryPackFormatter<Regex>
{
    #region Formatter
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, ref Regex value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(2);
        writer.WriteString(value.ToString());
        writer.WriteValue((int)value.Options);
    }

    public override void Deserialize(ref MemoryPackReader reader, ref Regex value)
    {
        if (!reader.TryReadObjectHeader(out var count))
        {
            value = null;
            return;
        }

        var pattern = reader.ReadString();
        var options = reader.ReadValue<int>();
        value = new Regex(pattern, (RegexOptions)options);
    }
    #endregion
}

public static class MemoryPackSetup
{
    #region Bootstrap
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Register()
    {
        if (MemoryPackFormatterProvider.IsRegistered<Regex>()) return;
        MemoryPackFormatterProvider.Register(new RegexFormatter());
    }
    #endregion
}
```

Mark members of that type `[MemoryPackAllowSerialize]` so the generator does not report them as unserializable. Unmanaged structs always use their raw memory layout and cannot get a custom formatter. Reference types follow the object-header pattern above (`WriteNullObjectHeader` / `WriteObjectHeader` / `TryReadObjectHeader`), as MemoryPack.Unity's `AnimationCurveFormatter` does. Other registration helpers: `MemoryPackFormatterProvider.RegisterCollection<TCollection, TElement>()`, `RegisterDictionary<TDictionary, TKey, TValue>()`, `RegisterSet<TSet, TElement>()`, `RegisterGenericType(Type, Type)`. Runtime-built unions: `MemoryPackFormatterProvider.Register(new DynamicUnionFormatter<IMyBase>((0, typeof(A)), (1, typeof(B))));`.

## Compression

```csharp
using MemoryPack;
using MemoryPack.Compression;

public static class CompressedCodec
{
    #region Public API
    public static byte[] Pack<T>(T value)
    {
        using var compressor = new BrotliCompressor();
        MemoryPackSerializer.Serialize(compressor, value);
        return compressor.ToArray();
    }

    public static T Unpack<T>(byte[] bytes)
    {
        using var decompressor = new BrotliDecompressor();
        var buffer = decompressor.Decompress(bytes);
        return MemoryPackSerializer.Deserialize<T>(buffer);
    }
    #endregion
}
```

On netstandard2.1 (Unity) `BrotliCompressor` is a class with constructors `(int quality = 1, int window = 22)` and `(CompressionLevel)`; default quality 1 is the fastest level. It wraps `System.IO.Compression.BrotliEncoder`, which needs a native Brotli implementation in the player runtime. Availability on Unity Mono / IL2CPP platforms is not documented by MemoryPack: test on each target, and fall back to `GZipStream` / `DeflateStream` (or no compression) where it throws.
