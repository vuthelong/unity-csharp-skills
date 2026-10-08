using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Data.Editor
{
    public static class GameDataValidator
    {
        #region Fields

        private const int MaxDepth = 4;
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        #endregion

        #region Public Methods

        public static List<string> Validate()
        {
            var errors = new List<string>();
            var tables = LoadTables();
            var idsByRowType = new Dictionary<Type, HashSet<int>>();

            for (var t = 0; t < tables.Count; t++)
            {
                CollectIds(tables[t], idsByRowType, errors);
            }

            for (var t = 0; t < tables.Count; t++)
            {
                var table = tables[t];
                for (var r = 0; r < table.RowCount; r++)
                {
                    var row = table.GetRow(r);
                    if (row == null) continue;

                    CheckReferences(row, $"{table.name} id {row.Id}", idsByRowType, errors, 0);
                }
            }

            return errors;
        }

        public static List<GameDataTable> LoadTables()
        {
            var tables = new List<GameDataTable>();
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { GameDataAddressables.GeneratedFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var table = AssetDatabase.LoadAssetAtPath<GameDataTable>(path);
                if (table != null) tables.Add(table);
            }

            return tables;
        }

        #endregion

        #region Private Methods

        [MenuItem("Tools/Game Data/Validate")]
        private static void ValidateFromMenu()
        {
            var errors = Validate();
            for (var i = 0; i < errors.Count; i++) Debug.LogError(errors[i]);

            if (errors.Count == 0) Debug.Log("GameData validation passed.");
        }

        private static void CollectIds(GameDataTable table, Dictionary<Type, HashSet<int>> idsByRowType, List<string> errors)
        {
            if (table.DataSchemaVersion != table.CodeSchemaVersion)
            {
                errors.Add($"{table.name}: schema {table.DataSchemaVersion} != code {table.CodeSchemaVersion}. Run Tools > Game Data > Sync Addressables.");
            }

            if (!idsByRowType.TryGetValue(table.RowType, out var ids))
            {
                ids = new HashSet<int>();
                idsByRowType.Add(table.RowType, ids);
            }

            for (var r = 0; r < table.RowCount; r++)
            {
                var row = table.GetRow(r);
                if (row == null)
                {
                    errors.Add($"{table.name}: row {r} is null.");
                    continue;
                }

                if (row.Id <= 0) errors.Add($"{table.name}: row {r} has id {row.Id}; ids must be > 0.");
                if (!ids.Add(row.Id)) errors.Add($"{table.name}: duplicate {table.RowType.Name} id {row.Id}.");
            }
        }

        private static void CheckReferences(object target, string context, Dictionary<Type, HashSet<int>> idsByRowType, List<string> errors, int depth)
        {
            if (target == null || depth > MaxDepth) return;

            var fields = target.GetType().GetFields(InstanceFields);
            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var value = field.GetValue(target);
                var reference = field.GetCustomAttribute<GameDataRefAttribute>();

                if (reference != null)
                {
                    CheckReferenceValue(value, reference, $"{context}.{field.Name}", idsByRowType, errors);
                    continue;
                }

                if (!IsNestedData(field.FieldType)) continue;

                if (value is IList list)
                {
                    for (var j = 0; j < list.Count; j++)
                    {
                        CheckReferences(list[j], $"{context}.{field.Name}[{j}]", idsByRowType, errors, depth + 1);
                    }

                    continue;
                }

                CheckReferences(value, $"{context}.{field.Name}", idsByRowType, errors, depth + 1);
            }
        }

        private static void CheckReferenceValue(object value, GameDataRefAttribute reference, string context, Dictionary<Type, HashSet<int>> idsByRowType, List<string> errors)
        {
            if (!idsByRowType.TryGetValue(reference.RowType, out var ids))
            {
                errors.Add($"{context}: no table holds {reference.RowType.Name} rows.");
                return;
            }

            if (value is int id)
            {
                CheckId(id, ids, reference, context, errors);
                return;
            }

            if (value is int[] idArray)
            {
                for (var i = 0; i < idArray.Length; i++)
                {
                    CheckId(idArray[i], ids, reference, $"{context}[{i}]", errors);
                }

                return;
            }

            errors.Add($"{context}: [GameDataRef] supports int and int[] only.");
        }

        private static void CheckId(int id, HashSet<int> ids, GameDataRefAttribute reference, string context, List<string> errors)
        {
            if (id == 0 && reference.Optional) return;
            if (ids.Contains(id)) return;

            errors.Add($"{context}: {reference.RowType.Name} id {id} does not exist.");
        }

        private static bool IsNestedData(Type type)
        {
            var elementType = type.IsArray ? type.GetElementType() : type;
            if (elementType == null) return false;
            if (elementType.IsPrimitive || elementType.IsEnum || elementType == typeof(string) || elementType == typeof(decimal)) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(elementType)) return false;

            return elementType.IsSerializable;
        }

        #endregion
    }

    public sealed class GameDataBuildValidation : IPreprocessBuildWithReport
    {
        #region Properties

        public int callbackOrder => -100;

        #endregion

        #region Public Methods

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = GameDataValidator.Validate();
            if (errors.Count == 0) return;

            for (var i = 0; i < errors.Count; i++) Debug.LogError(errors[i]);

            throw new BuildFailedException($"Game data validation failed with {errors.Count} error(s). See the Console.");
        }

        #endregion
    }
}
