# Data mapping reference

Read when writing a row type, laying out a CSV, or debugging a value that bakes wrong. Verified against `Reader.cs`, `CsvReaderUtils.cs`, `CsvTemplateExporter.cs` and the upstream samples (package 2.3.2).

## Column names

- Header cells must be all lowercase (`IsValidKeyFormat`). Blank header cells are skipped.
- Each header is converted snake_case to camelCase (`drop_rate` -> `dropRate`). Each field name is converted the same way (leading underscores are dropped, so private `_rows` matches header `rows`). The two must be equal.
- The reader uses every instance field, public and non-public, found by `Type.GetFields`. Mark fields that are not in the CSV with `[CsvColumnIgnore]`.
- Use **Export CSV Template** on a `CsvInfo` (or `CsvTemplateExporter.BuildHeaderForConfig(className, fieldSetValue)` / `BuildHeader(type)` in editor code) to get the exact header row.

## Attributes (namespace `CsvReader`, plus `System.ComponentModel.DefaultValueAttribute`)

| Attribute | Target | Effect |
|---|---|---|
| `[CsvColumn(ColumnName = "max_amount")]` | field | Read this field from a differently named column. |
| `[CsvColumnIgnore]` | field | Skip the field. |
| `[CsvColumnFormat(ColumnFormat = "fc_{0}")]` | nested object or object-array field | Format string applied to the child type's column names. It replaces any inherited format; formats do not compose across levels. |
| `[CsvClassCustomSeparator(';')]` | class/struct | Cell separator for the whole file. Default `,`. |
| `[CsvClassCustomPrimitiveArray('~')]` | class/struct | Primitive arrays are packed into one cell, split by the given char (default `~`). Must differ from the cell separator. |
| `[Converter(typeof(MyConverter))]` / `[Converter(typeof(MyConverter), typeof(int))]` | field | Parse the cell as the converter's `TFrom` and store `Convert(value)` in the field. |
| `[DefaultValue(x)]` | field (or enum type) | Value used when the cell is empty. |

`CsvClassCustomSeparator` and `CsvClassCustomPrimitiveArray` are read only from the element type of the container field (the top-level row type). Putting them on a nested type has no effect.

## Supported field types

| Type | Cell format |
|---|---|
| `string` | Raw text. Quote with `"` to include the separator, quotes (`""`) or newlines. Inside quotes `\n` becomes a newline. |
| `int`, `long`, `short`, `byte`, `float`, `double`, `decimal`, ... | Parsed with `InvariantCulture` (`1.5`, never `1,5`). A `.` in an integer cell is parsed as float then converted. |
| `bool` | `1`/`0` or `true`/`false` (case-insensitive, trimmed). |
| `enum` | Exact member name, case-sensitive. |
| nested class/struct | Its fields are flat columns in the same row (optionally prefixed via `CsvColumnFormat`). |
| `T[]` of primitives | One value per row in the column (continuation rows), or one packed cell with `CsvClassCustomPrimitiveArray`. |
| `T[]` of objects | Multi-row block, see below. |
| converter target | Any type `TTo` produced by an `IConvert<TFrom, TTo>`. |

Not filled directly: `List<T>`, `Dictionary<K,V>`, `UnityEngine.Object` references, and `Vector3` or other engine structs. Nested types are resolved by full name through `Type.GetType` and then the assemblies in `CsvConfig.assemblyNames`, so a nested type from an unlisted assembly (including `UnityEngine`) throws "Type is null". Use a convert method, a converter, or your own `[Serializable]` struct in a listed assembly.

Empty cells: `[DefaultValue]` if present, otherwise `""`, `0`, `false`, or the enum default (enum's `[DefaultValue]`, else `0` if defined, else the first member).

## Container field shapes

- Array field (`HeroConfigItem[] items`): one element per data row whose first column is non-empty.
- Non-array object field (`Entry data`): only the first data row is read (useful for a single settings row).
- Primitive array field (`int[] ids`): reads the column named after `fieldSetValue`, one row per element.

## Nested arrays (multi-row records)

The first column of a type's fields (lowest column index) is its anchor. A new top-level record starts on each row whose anchor is non-empty. Rows with an empty top-level anchor continue the previous record and add elements to its nested arrays:

```csharp
public class RewardInfoList : ScriptableObject
{
    public RewardStageItem[] dataGroups;

    [Serializable] public struct RewardStageItem
    {
        public int stage;
        public RewardInfoItem[] rewardInfo;
    }

    [Serializable] public class RewardInfoItem
    {
        public bool isFirst;
        public Resource resource;
    }
}

[Serializable] public struct Resource
{
    public int resourceType;
    public int resourceId;
    [DefaultValue(int.MaxValue)] public long resourceNumber;
}
```

```
stage,is_first,resource_type,resource_id,resource_number
101001,TRUE,,1,
,FALSE,,2,
101002,TRUE,,1,
```

Result: two `RewardStageItem`s; the first has two `rewardInfo` entries. Empty `resource_number` becomes `int.MaxValue`.

Rules:
- Order columns parent-first: parent fields, then the nested type's columns.
- Every nested element row must have a non-empty value in the nested type's anchor column, or it is skipped.
- Two arrays of the same element type in one row type need distinct prefixes via `CsvColumnFormat` (sample: `rewards` uses `resource_*`, `firstClearRewards` uses `fc_resource_*`).

## Packed primitive arrays

```csharp
public class RatingDataListCustomPrimitiveArray : ScriptableObject
{
    public MapIdTrigger[] dataGroups;

    [CsvClassCustomPrimitiveArray('~')]
    [Serializable] public class MapIdTrigger
    {
        public int mapId;
        public int[] difficulty;
        public string[] description;
    }
}
```

```
map_id,difficulty,description
6,1~2~3,data_1
```

## Custom converters

```csharp
[Serializable] public struct CustomInt { public int value; }

public readonly struct CustomIntConverter : IConvert<int, CustomInt>
{
    public CustomInt Convert(object value) => new CustomInt { value = (int)value };
}

[Converter(typeof(CustomIntConverter))]
public CustomInt stage;
```

Requirements enforced by `CsvReaderUtils.TryGetConverter`:
- The converter is a struct, a class with a parameterless constructor, or a static class. Not an interface or a non-static abstract class.
- It implements `IConvert<TFrom, TTo>` where `TTo` equals the field type exactly. When it implements several, pass `fromType` in the attribute.
- It has a public method returning `TTo` with one `object` parameter (the `IConvert` method satisfies this).
- The cell is first parsed as `TFrom` (which must itself be a supported type), then passed in boxed.
- A converter on an array field receives the whole parsed array.

## Separators and quoting

The parser accepts RFC 4180 quoting (`"a,b"`, `""` for a literal quote, quoted newlines), strips a leading BOM, and treats a trailing separator as an empty last cell. Set the separator per file with `CsvClassCustomSeparator`; the Google Sheets downloader always writes `,`.
