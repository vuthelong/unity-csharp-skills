using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MasterMemory;
using MasterMemory.Meta;
using MessagePack.Resolvers;
using UnityEditor;
using UnityEngine;

namespace MyGame.MasterData.Editor
{
    public static class MasterMemoryCsvBuilder
    {
        #region Fields
        public const string CsvFolder = "Assets/MasterData/Csv";
        public const string OutputPath = "Assets/MasterData/Build/master.bytes";

        private const char ArraySeparator = '|';

        public static event Action<byte[]> Built;
        #endregion

        #region Public Methods
        [MenuItem("Tools/Master Data/Build MasterMemory Binary")]
        public static void BuildFromMenu()
        {
            if (TryBuild(out var error))
            {
                Debug.Log($"[MasterMemory] Built {OutputPath}");
                return;
            }

            Debug.LogError($"[MasterMemory] Build failed:\n{error}");
        }

        public static bool TryBuild(out string error)
        {
            try
            {
                var resolver = CompositeResolver.Create(MasterMemoryResolver.Instance, StandardResolver.Instance);
                var builder = new DatabaseBuilder(resolver);

                foreach (var table in MemoryDatabase.GetMetaDatabase().GetTableInfos())
                {
                    var csvPath = CsvFolder + "/" + table.TableName + ".csv";
                    if (!File.Exists(csvPath))
                    {
                        error = $"Missing CSV for table '{table.TableName}': {csvPath}";
                        return false;
                    }

                    builder.AppendDynamic(table.DataType, ReadTable(table, csvPath));
                }

                var bytes = builder.Build();
                var validation = new MemoryDatabase(bytes, formatterResolver: resolver).Validate();
                if (validation.IsValidationFailed)
                {
                    error = validation.FormatFailedResults();
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                File.WriteAllBytes(OutputPath, bytes);
                AssetDatabase.ImportAsset(OutputPath);

                Built?.Invoke(bytes);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }
        #endregion

        #region Private Methods
        private static List<object> ReadTable(MetaTable table, string csvPath)
        {
            var records = ParseCsv(File.ReadAllText(csvPath, Encoding.UTF8));
            var rows = new List<object>(Math.Max(0, records.Count - 1));
            if (records.Count == 0) return rows;

            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerCells = records[0];
            for (var i = 0; i < headerCells.Count; i++)
            {
                header[headerCells[i].Trim()] = i;
            }

            var properties = table.Properties;
            var columns = new int[properties.Count];
            for (var p = 0; p < properties.Count; p++)
            {
                var property = properties[p];
                if (!header.TryGetValue(property.NameSnakeCase, out columns[p])
                    && !header.TryGetValue(property.Name, out columns[p]))
                {
                    throw new InvalidDataException($"{csvPath}: missing column '{property.NameSnakeCase}' for {table.DataType.Name}.{property.Name}");
                }

                if (property.PropertyInfo.SetMethod == null)
                {
                    throw new InvalidDataException($"{table.DataType.Name}.{property.Name} needs an init or private set accessor.");
                }
            }

            for (var r = 1; r < records.Count; r++)
            {
                var cells = records[r];
                if (IsBlank(cells)) continue;

                var row = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(table.DataType);
                for (var p = 0; p < properties.Count; p++)
                {
                    var property = properties[p].PropertyInfo;
                    var raw = columns[p] < cells.Count ? cells[columns[p]] : string.Empty;
                    try
                    {
                        property.SetValue(row, ParseValue(property.PropertyType, raw));
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidDataException($"{csvPath} line {r + 1}, column '{properties[p].NameSnakeCase}': cannot parse '{raw}' as {property.PropertyType.Name}", exception);
                    }
                }

                rows.Add(row);
            }

            return rows;
        }

        private static object ParseValue(Type type, string raw)
        {
            if (type == typeof(string)) return raw;

            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
            {
                return string.IsNullOrWhiteSpace(raw) ? null : ParseValue(nullable, raw);
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (string.IsNullOrWhiteSpace(raw)) return Array.CreateInstance(elementType, 0);

                var parts = raw.Split(ArraySeparator);
                var array = Array.CreateInstance(elementType, parts.Length);
                for (var i = 0; i < parts.Length; i++)
                {
                    array.SetValue(ParseValue(elementType, parts[i].Trim()), i);
                }

                return array;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            raw = raw.Trim();
            if (type.IsEnum) return Enum.Parse(type, raw, true);
            if (type == typeof(bool))
            {
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag)) return flag != 0;
                return bool.Parse(raw);
            }

            if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(TimeSpan)) return TimeSpan.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(Guid)) return Guid.Parse(raw);

            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var cell = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (inQuotes)
                {
                    if (c != '"')
                    {
                        cell.Append(c);
                        continue;
                    }

                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        record.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        record.Add(cell.ToString());
                        cell.Clear();
                        records.Add(record);
                        record = new List<string>();
                        break;
                    case '﻿':
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }

            if (cell.Length > 0 || record.Count > 0)
            {
                record.Add(cell.ToString());
                records.Add(record);
            }

            return records;
        }

        private static bool IsBlank(List<string> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(cells[i])) return false;
            }

            return true;
        }
        #endregion
    }

    public sealed class MasterMemoryCsvPostprocessor : AssetPostprocessor
    {
        #region Fields
        private static bool _pending;
        #endregion

        #region Private Methods
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (_pending) return;
            if (!ContainsCsv(importedAssets) && !ContainsCsv(deletedAssets) && !ContainsCsv(movedAssets)) return;

            _pending = true;
            EditorApplication.delayCall += Rebuild;
        }

        private static void Rebuild()
        {
            _pending = false;
            MasterMemoryCsvBuilder.BuildFromMenu();
        }

        private static bool ContainsCsv(string[] paths)
        {
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (path.StartsWith(MasterMemoryCsvBuilder.CsvFolder, StringComparison.Ordinal)
                    && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
    }

    [InitializeOnLoad]
    public static class MasterMemoryHotReload
    {
        #region Public Methods
        static MasterMemoryHotReload()
        {
            MasterMemoryCsvBuilder.Built -= OnBuilt;
            MasterMemoryCsvBuilder.Built += OnBuilt;
        }
        #endregion

        #region Private Methods
        private static void OnBuilt(byte[] bytes)
        {
            if (!EditorApplication.isPlaying) return;

            MasterDataService.Load(bytes);
            Debug.Log("[MasterMemory] Hot-reloaded master data in Play Mode.");
        }
        #endregion
    }
}
