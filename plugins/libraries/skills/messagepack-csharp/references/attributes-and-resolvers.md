# Attributes, resolvers and options

Verified against MessagePack-CSharp v3.1.11 `src/MessagePack.Annotations/*.cs`, `src/MessagePack/Resolvers/*.cs`, `src/MessagePack/MessagePackSerializerOptions.cs`, `src/MessagePack/MessagePackSecurity.cs`, `src/MessagePack.UnityClient/Assets/Scripts/MessagePack/*`.

## Data attributes (namespace `MessagePack`, package MessagePack.Annotations)

| Attribute | Target | Members | Notes |
|---|---|---|---|
| `[MessagePackObject]` | class, struct | ctor `(bool keyAsPropertyName = false)`; `KeyAsPropertyName`, `AllowPrivate`, `SuppressSourceGeneration` | required by `StandardResolver`; `keyAsPropertyName: true` makes every public member a string key named after the member |
| `[Key(int)]` / `[Key(string)]` | property, field | `IntKey`, `StringKey` | int keys -> msgpack array, string keys -> map; one style per type |
| `[IgnoreMember]` | property, field | - | excludes a public member (analyzer MsgPack004 requires `Key` or `IgnoreMember` on every public member) |
| `[SerializationConstructor]` | constructor | - | selects the deserializing constructor |
| `[Union(int key, Type subType)]` / `[Union(int key, string subType)]` | interface, class (abstract base) | `Key`, `SubType` | `AllowMultiple = true`; payload `[key, value]` |
| `[MessagePackFormatter(Type)]` / `(Type, params object[] args)` | type, member, parameter, return | `FormatterType`, `Arguments` | per-type or per-member custom formatter; found by `AttributeFormatterResolver` |
| `IMessagePackSerializationCallbackReceiver` | interface | `OnBeforeSerialize()`, `OnAfterDeserialize()` | implement on the DTO |
| `[DataContract]` / `[DataMember(Order/Name)]` / `[IgnoreDataMember]` | - | - | accepted by dynamic resolvers, but not by the analyzers or the source generator; avoid for IL2CPP |

## Generator and analyzer attributes

| Attribute | Where | Effect |
|---|---|---|
| `[GeneratedMessagePackResolver]` (`UseMapMode`) | on your `partial class` | renames/relocates the generated resolver (default `MessagePack.GeneratedMessagePackResolver`); `UseMapMode` forces map (string-key) layout |
| `[CompositeResolver(params Type[])]` (`IncludeLocalFormatters`) | on your `partial class` | generates a fast composite resolver over the listed formatters/resolvers at compile time |
| `[assembly: MessagePackKnownFormatter(typeof(MyFormatter))]` | assembly | declares a hand-written formatter so its `T` is not flagged and is included in the generated resolver |
| `[assembly: MessagePackAssumedFormattable(typeof(T))]` | assembly | promises a formatter for `T` exists at runtime (replaces v2 `MessagePackAnalyzer.json`) |
| `[ExcludeFormatterFromSourceGeneratedResolver]` | formatter class | keeps a formatter out of the generated resolver |
| `[assembly: MessagePack.Internal.GeneratedAssemblyMessagePackResolver(typeof(R), major, minor)]` | assembly, emitted by the generator | how `SourceGeneratedFormatterResolver` finds `R.Instance` for types of that assembly at runtime; do not write it by hand |

`[GeneratedMessagePackResolver]` and `[CompositeResolver]` are `[Conditional("NEVERDEFINED")]`; they exist only at compile time.

Useful analyzer IDs: MsgPack001 (implicit `DefaultOptions`), MsgPack003 (member type lacks `[MessagePackObject]`), MsgPack004 (public member without `Key`/`IgnoreMember`), MsgPack007 (constructor issues), MsgPack008 (AOT limitation), MsgPack009 (colliding formatters), MsgPack010/013 (formatter not accessible / no `Instance`), MsgPack011 (needs `partial`), MsgPack015 (needs `AllowPrivate`), MsgPack016 (`KeyAttribute`-derived attribute unsupported by AOT), MsgPack017 (`init` + initializer), MsgPack018 (duplicate names in map mode). Docs: `doc/analyzers/MsgPackNNN.md` in the repo.

## Built-in resolvers (namespace `MessagePack.Resolvers`)

| Resolver | Use | IL2CPP |
|---|---|---|
| `StandardResolver` (`.Instance`, `.Options`) | default: builtin -> attribute -> source-generated -> immutable collections -> expando -> dynamic generic -> (dynamic union, dynamic object when dynamic code is allowed) | yes, for source-generated types |
| `StandardResolverAllowPrivate` | as above, private members via dynamic code | generated types only |
| `ContractlessStandardResolver` / `...AllowPrivate` | unattributed types, string keys | no (Reflection.Emit) |
| `TypelessContractlessStandardResolver` | `MessagePackSerializer.Typeless`; embeds type names | no; unsafe for untrusted data |
| `BuiltinResolver` | primitives, BCL types | yes |
| `AttributeFormatterResolver` | `[MessagePackFormatter]` | yes |
| `SourceGeneratedFormatterResolver` | finds generated resolvers via assembly attribute | yes |
| `DynamicGenericResolver` | `List<>`, `Dictionary<,>`, tuples, arrays | uses reflection to close generics; test unusual generic shapes on device |
| `DynamicEnumAsStringResolver` | enums as names | reflection; test on device |
| `DynamicObjectResolver`, `DynamicUnionResolver`, `DynamicContractless*` | runtime IL generation | no |
| `NativeDateTimeResolver`, `NativeGuidResolver`, `NativeDecimalResolver` | .NET binary layouts, faster, keep `DateTimeKind` | yes; not interoperable |
| `PrimitiveObjectResolver` | `object` fields with primitive contents | yes |
| `CompositeResolver.Create(formatters, resolvers)` | ad-hoc composition with a lookup cache | works; lookup overhead |
| `StaticCompositeResolver.Instance.Register(...)` | app-wide composition, generic-static cache | yes; `Register` throws after first use |

Extra formatters: `IgnoreFormatter<T>` (writes nil), `StringInterningFormatter` (dedupe repeated strings), `TypelessFormatter`.

## Unity package (`com.github.messagepack-csharp`)

| Type | Namespace | Notes |
|---|---|---|
| `UnityResolver.Instance` / `.InstanceWithStandardResolver` | `MessagePack.Unity` | Vector2/3/4, Quaternion, Color, Color32, Bounds, Rect, Matrix4x4, AnimationCurve, Keyframe, Gradient, RectOffset, LayerMask, Vector2Int, Vector3Int, RangeInt, RectInt, BoundsInt (+ nullable, arrays, lists) |
| `UnityBlitResolver.Instance` | `MessagePack.Unity.Extension` | blit `Vector2[]`, `Vector3[]`, `Vector4[]`, `Quaternion[]`, `Color[]`, `Bounds[]`, `Rect[]` (ext 30-36) |
| `UnityBlitWithPrimitiveArrayResolver.Instance` | `MessagePack.Unity.Extension` | also `int[]`, `float[]`, `double[]` (ext 37-39) |
| `MessagePackInitializer` | `MessagePack.Unity` | sets `DefaultOptions` to `Standard.WithResolver(UnityResolver.InstanceWithStandardResolver)` at `SubsystemRegistration` and on Editor load |

Server side: NuGet `MessagePack.UnityShims` compiles the same `UnityResolver`, blit resolvers and shim structs in `namespace UnityEngine`.

## Options (`MessagePackSerializerOptions`)

| Member | Default | `With...` |
|---|---|---|
| `Resolver` | `StandardResolver` for `Standard` | `WithResolver(IFormatterResolver)` |
| `Compression` | `None` | `WithCompression(MessagePackCompression.Lz4BlockArray / Lz4Block / None)` |
| `CompressionMinLength` | 64 bytes | `WithCompressionMinLength(int)` |
| `Security` | `MessagePackSecurity.TrustedData` | `WithSecurity(MessagePackSecurity)` |
| `OldSpec` | null | `WithOldSpec(bool?)` (msgpack v1 spec for old peers; no DateTime) |
| `OmitAssemblyVersion` / `AllowAssemblyVersionMismatch` | false | typeless only |
| `SuggestedContiguousMemorySize` | 1 MiB | `WithSuggestedContiguousMemorySize(int)` |
| `SequencePool` | `SequencePool.Shared` | `WithPool(SequencePool)` |

`MessagePackSerializerOptions.Standard` is the static baseline; `MessagePackSerializer.DefaultOptions` is a mutable static used when you pass `null`.

## Security (`MessagePackSecurity`)

| Preset | HashCollisionResistant | MaximumObjectGraphDepth | MaximumDecompressedSize |
|---|---|---|---|
| `TrustedData` | false | 500 | `int.MaxValue` |
| `UntrustedData` | true | 500 | 64 MiB |

Tune with `WithMaximumObjectGraphDepth(int)`, `WithMaximumDecompressedSize(int)`, `WithHashCollisionResistant(bool)`.

## Reserved extension type codes

| Code | Use |
|---|---|
| -1 | msgpack timestamp (`DateTime`) |
| 30-39 | Unity blit arrays |
| 98 / 99 | `Lz4BlockArray` / `Lz4Block` |
| 100 | typeless type info |

Your own ext codes: 0-29 and 120-127.

## Built-in supported types (no attribute needed)

Primitives, enums, `Nullable<>`, `Lazy<>`, `string`, `TimeSpan`, `DateTime`, `DateTimeOffset`, `Guid`, `Uri`, `Version`, `StringBuilder`, `decimal`, `Type`, `BigInteger`, `Complex`, `Memory<byte>` family, `System.Numerics` vectors/matrices, arrays (up to rank 4), `ArraySegment<>`, `BitArray`, `KeyValuePair<,>`, `Tuple<>`, `ValueTuple<>`, `List<>`, `LinkedList<>`, `Queue<>`, `Stack<>`, `HashSet<>`, `ReadOnlyCollection<>`, `SortedList<,>`, collection interfaces, `Dictionary<,>` family, `ObservableCollection<>`, concurrent collections, immutable collections, and custom `ICollection<>` / `IDictionary<,>` with a parameterless constructor. `MessagePack.Nil` represents nil/void.
