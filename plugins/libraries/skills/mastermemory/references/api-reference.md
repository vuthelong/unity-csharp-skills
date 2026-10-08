# MasterMemory API reference (v3.0.4)

Verified against `src/MasterMemory.Annotations/Attributes.cs`, `src/MasterMemory/*.cs`, `src/MasterMemory.SourceGenerator/GeneratorCore/*.tt`, `GenerationContext.cs`, `CodeGenerator.cs` and `MasterMemoryGeneratorOptions.cs`. `{T}` is the row class name, `{Key}` is the generated key name, and `{Prefix}` is `PrefixClassName`.

## Attributes (namespace `MasterMemory`)

| Attribute | Target | Arguments | Effect |
|---|---|---|---|
| `[MemoryTable(string tableName)]` | class / record | table name stored in the binary | Marks a table. The class name may change, but the table name may not. |
| `[PrimaryKey(int keyOrder = 0)]` | property | `keyOrder` literal | Required, one logical key per table. Put it on several properties for a composite key, ordered by `keyOrder`. |
| `[SecondaryKey(int indexNo, int keyOrder = 0)]` | property, AllowMultiple | `indexNo`, `keyOrder` literals | Adds index `indexNo`. Properties sharing an `indexNo` form a composite index. Use one attribute list per `[SecondaryKey]`. |
| `[NonUnique]` | property, AllowMultiple | none | Makes the key in the **same attribute list** non-unique. `FindBy` then returns `RangeView<T>`. |
| `[StringComparisonOption(StringComparison)]` | property | `StringComparison` member | Comparer for the key in the **same attribute list**, written after it. Applies only to single `string` keys. Default `StringComparer.Ordinal`. |
| `[MessagePackObject(true)]` / `[MessagePackObject]` + `[Key(n)]` | class | MessagePack | Required for serialization. |
| `[IgnoreMember]` (MessagePack) or `[IgnoreDataMember]` | property | none | Excludes computed properties from columns and metadata. |

Assembly-level options (the attribute is emitted `internal` into each assembly by the generator):

```csharp
[assembly: MasterMemory.MasterMemoryGeneratorOptions(
    Namespace = "MyGame.MasterData",
    IsReturnNullIfKeyNotFound = false,
    PrefixClassName = "")]
```

| Option | Default | Effect |
|---|---|---|
| `Namespace` | `RootNamespace` build property, else `MasterMemory` (in Unity, effectively `MasterMemory`) | Namespace of `MemoryDatabase`, `DatabaseBuilder`, `ImmutableBuilder`, `MasterMemoryResolver`. Tables go into `{Namespace}.Tables`. |
| `IsReturnNullIfKeyNotFound` | `false` | `true`: unique `FindBy{Key}` returns `T?` / null instead of throwing `KeyNotFoundException`. |
| `PrefixClassName` | `""` | `Foo` gives `FooMemoryDatabase`, `FooDatabaseBuilder`, `FooImmutableBuilder`, `FooMasterMemoryResolver`. Table classes are not prefixed. |

Generator diagnostics: MAM001 (no `[PrimaryKey]`), MAM002 (duplicate primary key, effectively dead code), MAM003 (two `[SecondaryKey]` in one attribute list).

## Key naming

| Declaration | Method suffix | Key parameter type |
|---|---|---|
| `[PrimaryKey] int Id` | `ById` | `int` |
| `[PrimaryKey(0)] int A`, `[PrimaryKey(1)] int B` | `ByAAndB` | `(int A, int B)` |
| `[PrimaryKey(1)] int A`, `[PrimaryKey(0)] int B` | `ByBAndA` | `(int B, int A)` |
| `[SecondaryKey(0)] int Lv` | `ByLv` | `int` |

## Generated table `{Namespace}.Tables.{T}Table` (sealed partial, base `TableBase<T>`)

Per key (the primary key and each secondary index):

| Member | Unique key | `[NonUnique]` key |
|---|---|---|
| `FindBy{Key}(key)` | `T`. Throws `KeyNotFoundException` on a miss (or returns `T?` / null with `IsReturnNullIfKeyNotFound`). | `RangeView<T>`, empty on a miss |
| `TryFindBy{Key}(key, out T result)` | `bool` | not generated |
| `FindClosestBy{Key}(key, bool selectLower = true)` | `T?`, the nearest lower (or higher) row | `RangeView<T>` |
| `FindRangeBy{Key}(min, max, bool ascendant = true)` | `RangeView<T>`, inclusive | `RangeView<T>`, inclusive |

Per secondary index only: `RangeView<T> SortBy{Key}`, all rows in that index's order.

Common members from `TableBase<T>`:

| Member | Notes |
|---|---|
| `int Count` | row count |
| `RangeView<T> All` / `AllReverse` | primary-key order |
| `T[] GetRawDataUnsafe()` | the backing array; never mutate it |
| `Func<T, TKey> PrimaryKeySelector` | generated per table |
| `partial void OnAfterConstruct()` | implement in your own `partial class {T}Table` in namespace `{Namespace}.Tables` to precompute caches |

## `RangeView<T>` (readonly struct: `IEnumerable<T>`, `IReadOnlyList<T>`, `IList<T>` read-only)

`this[int]`, `Count`, `First`, `Last`, `Reverse`, `Any()`, `IndexOf`, `Contains`, `CopyTo`, `GetEnumerator()` (allocating `yield` iterator), `static Empty`. `First` / `Last` / the indexer throw `ArgumentOutOfRangeException` on an empty view.

## `{Prefix}MemoryDatabase` (sealed, base `MemoryDatabaseBase`)

| Member | Notes |
|---|---|
| `MemoryDatabase(byte[] databaseBinary, bool internString = true, IFormatterResolver? formatterResolver = null, int maxDegreeOfParallelism = 1)` | Deserializes every table. A null resolver uses `MessagePackSerializer.DefaultOptions.Resolver`. |
| `MemoryDatabase({T}Table ..., ...)` | builds from table instances (used by `ImmutableBuilder`) |
| `{T}Table {T}Table { get; }` | one property per table |
| `ImmutableBuilder ToImmutableBuilder()` | starts a copy-on-write edit |
| `DatabaseBuilder ToDatabaseBuilder()` / `ToDatabaseBuilder(IFormatterResolver)` | re-serialize the current snapshot |
| `ValidateResult Validate()` | removed by `DISABLE_MASTERMEMORY_VALIDATOR` |
| `static object? GetTable(MemoryDatabase db, string tableName)` | lookup by table name |
| `static MetaDatabase GetMetaDatabase()` | removed by `DISABLE_MASTERMEMORY_METADATABASE` |
| `static TableInfo[] GetTableInfo(byte[] bin, bool storeTableData = true)` | inherited static. `TableInfo.TableName`, `Size`, `DumpAsJson()` for debugging a binary. |

A table missing from the binary loads as an empty table without an error.

## `{Prefix}DatabaseBuilder` (sealed, base `DatabaseBuilderBase`)

| Member | Notes |
|---|---|
| `DatabaseBuilder()` / `DatabaseBuilder(IFormatterResolver? resolver)` | Without a resolver, it serializes with `MessagePackSerializer.DefaultOptions` + LZ4Block. |
| `DatabaseBuilder Append(IEnumerable<T> dataSource)` | One overload per table, chainable. Sorts by primary key. Appending a table twice throws `InvalidOperationException`. `null` is ignored. Duplicates are **not** rejected. |
| `byte[] Build()` / `void WriteToStream(Stream)` | header (table name to offset, count) + LZ4 MessagePack arrays |
| `DatabaseBuilderExtensions.AppendDynamic(this DatabaseBuilderBase, Type dataType, IList<object> tableData)` | Reflection-based append for importers driven by `MetaDatabase`. |

## `{Prefix}ImmutableBuilder`

```csharp
var builder = MasterDataService.Database.ToImmutableBuilder();
builder.Diff(new[] { patchedItem });
builder.RemoveItem(new[] { 1001, 1002 });
builder.ReplaceAll(new List<Drop>(newDrops));
var next = builder.Build();
```

| Member | Generated when |
|---|---|
| `void ReplaceAll(IList<T> data)` | every table |
| `void Diff(T[] addOrReplaceData)` | unique primary key only. It adds or replaces rows by primary key, and throws `ArgumentException` if the input has two rows with one key. |
| `void Remove{T}(TKey[] keys)` | unique primary key only |
| `MemoryDatabase Build()` | returns the new snapshot. The original database is untouched. |

## Validation (`MasterMemory`, `MasterMemory.Validation`)

```csharp
public interface IValidatable<TSelf> { void Validate(IValidator<TSelf> validator); }

public interface IValidator<T>
{
    ValidatableSet<T> GetTableSet();
    ReferenceSet<T, TRef> GetReferenceSet<TRef>();
    void Validate(Expression<Func<T, bool>> predicate);
    void Validate(Func<T, bool> predicate, string message);
    void ValidateAction(Expression<Func<bool>> predicate);
    void ValidateAction(Func<bool> predicate, string message);
    void Fail(string message);
    bool CallOnce();
}
```

- `ReferenceSet<T, TRef>`: `TableData`, `Exists(elementSelector, referenceSelector)`, `Exists(..., EqualityComparer<TProperty>)`.
- `ValidatableSet<T>`: `TableData`, `Unique(...)` (expression or func + message, optional comparer), `Sequential(selector, bool distinct = false)` for integer columns, `Where(Func<T, bool>)`.
- `ValidateResult`: `IsValidationFailed`, `FailedResults` (`FaildItem` with `Type`, `Message`, `Data`, spelled as in the source), `FormatFailedResults()`.
- `Validate()` first checks every unique key for duplicates, then calls `IValidatable<T>.Validate` once per row. `CallOnce()` is true only for the first row of a table.
- Expression-based checks compile expression trees. Run them in the Editor or CI, not in IL2CPP players.

## Metadata (`MasterMemory.Meta`)

| Type | Members |
|---|---|
| `MetaDatabase` | `Count`, `GetTableInfos()`, `GetTableInfo(string tableName)` |
| `MetaTable` | `DataType`, `TableType`, `TableName`, `Properties`, `Indexes` |
| `MetaProperty` | `PropertyInfo`, `Name`, `NameLowerCamel`, `NameSnakeCase` |
| `MetaIndex` | `IndexProperties`, `IsPrimaryIndex`, `IsUnique`, `Comparer`, `IsReturnRangeValue` |

Use metadata to drive generic importers (CSV headers via `NameSnakeCase`) and to export CSV templates.
