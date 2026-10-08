# MemoryPack attributes and schema rules

Namespace `MemoryPack`. Checked against `src/MemoryPack.Core/Attributes.cs` (1.21.4).

## Attributes

| Attribute | Target | Purpose |
|---|---|---|
| `[MemoryPackable]` | `partial` class, struct, record, interface | generate a formatter; overloads `(GenerateType generateType = GenerateType.Object)`, `(SerializeLayout)`, `(GenerateType, SerializeLayout)` |
| `[MemoryPackOrder(int order)]` | field / property | explicit position; required on every member for `VersionTolerant` and `CircularReference` (unless `SerializeLayout.Sequential`) and for `SerializeLayout.Explicit` |
| `[MemoryPackIgnore]` | public field / property | exclude |
| `[MemoryPackInclude]` | private field / property | include |
| `[MemoryPackConstructor]` | constructor | choose the deserialization constructor |
| `[MemoryPackUnion(ushort tag, Type type)]` | interface / abstract class | register a concrete subtype under a tag |
| `[MemoryPackUnionFormatter(Type type)]` | `partial class` in another assembly | declare a union outside the base type's assembly |
| `[MemoryPackAllowSerialize]` | field / property | silence diagnostics for a type whose formatter you register manually |
| `[SuppressDefaultInitialization]` | field / property | keep the initializer value when old data lacks the member; not allowed on readonly / init-only / required |
| `[MemoryPackOnSerializing]`, `[MemoryPackOnSerialized]` | static or instance method, any visibility | before / after write |
| `[MemoryPackOnDeserializing]`, `[MemoryPackOnDeserialized]` | static or instance method | before / after read; instance `OnDeserializing` runs only when deserializing into an existing instance with `ref` |
| `[GenerateTypeScript]` | type | TypeScript generation (not relevant in Unity) |

Callback signatures: parameterless, or `static void M<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, ref T? value) where TBufferWriter : class, IBufferWriter<byte>` for serializing callbacks and `static void M(ref MemoryPackReader reader, ref T? value)` for deserializing callbacks. Order: static methods first, then instance.

## `GenerateType`

| Value | Use |
|---|---|
| `Object` (default) | fastest; append-only evolution |
| `VersionTolerant` | per-member length prefix; members can be deleted; every member needs `[MemoryPackOrder]` unless `SerializeLayout.Sequential` is also passed (then deletion is not allowed) |
| `CircularReference` | version-tolerant plus reference tracking (shared and cyclic references round-trip as the same instance); parameterless constructor only; tracks only objects whose type is also `CircularReference` |
| `Collection` | for a type deriving from `List<T>`, `Dictionary<K,V>`, a set etc. |
| `NoGenerate` | mark an interface that gets its union from `[MemoryPackUnionFormatter]` elsewhere |

`SerializeLayout`: `Sequential` (declaration order) or `Explicit` (`[MemoryPackOrder]` decides).

## Member selection

Serialized by default: public fields, public readonly fields, public properties with any getter (including private set, get-only, init). Not serialized by default: private / protected / internal members. Inheritance: base members first, then derived.

## Constructor selection

1. A constructor with `[MemoryPackConstructor]`.
2. Otherwise, if no constructor is declared, the implicit parameterless one.
3. Otherwise, the single declared constructor (parameterless or parameterized, any visibility).
4. Several constructors without `[MemoryPackConstructor]` is a generator error.

Parameter names must match member names case-insensitively. Members not covered by constructor parameters are assigned after construction. Overwrite deserialization (`Deserialize(bytes, ref value)`) reuses instances only for types with a parameterless constructor.

## Version-tolerance rules in detail

`GenerateType.Object`:

- Add members only at the end (after all existing ones, including in the most-derived class).
- Never delete, reorder, or change the type of a member. Renaming is fine.
- Never change an unmanaged struct used anywhere in the graph.
- Old data, new type: missing trailing members become `default` (or keep the initializer with `[SuppressDefaultInitialization]`).
- New data, old type: fails. In client/server setups update the reader side first (README: client before server).

`GenerateType.VersionTolerant`:

- Every member has a stable `[MemoryPackOrder(n)]`. Add with a new number; delete freely; never reuse a deleted number.
- Gaps in numbering are allowed.
- Type changes and unmanaged-struct changes still break.

Union:

- Tags are the identity of subtypes. Add new tags; never renumber or reuse.

## Unity-specific attribute notes

- `[SerializeField]`, `[NonSerialized]`, `[FormerlySerializedAs]` mean nothing to MemoryPack. A `[SerializeField] private` field needs `[MemoryPackInclude]` to be saved.
- Generated code carries `MemoryPack.Internal.Preserve` attributes so IL2CPP managed stripping keeps the formatters.
- Union formatters declared with `[MemoryPackUnionFormatter]` rely on `ModuleInitializer`, which Unity does not run. Call `{FormatterClassName}Initializer.RegisterFormatter()` from a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]` method. Unions declared directly on the interface register through the type's generated static constructor and need nothing extra.
- Per-member formatter attributes (`MemoryPackCustomFormatterAttribute<T>` and the built-ins `Utf8StringFormatter`, `Utf16StringFormatter`, `InternStringFormatter`, `OrdinalIgnoreCaseStringDictionaryFormatter<T>`, `BitPackFormatter`, `BrotliFormatter`, `BrotliStringFormatter`, `BrotliFormatter<T>`, `MemoryPoolFormatter<T>`, `ReadOnlyMemoryPoolFormatter<T>`) are listed by the README as unsupported in Unity.
