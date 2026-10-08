using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Data
{
    public sealed class GameDataService : IDisposable
    {
        #region Fields

        public const string DefaultLabel = "gamedata";

        private readonly Dictionary<Type, IGameDataLookup> _lookups = new();
        private readonly Dictionary<Type, GameDataTable> _tables = new();
        private AsyncOperationHandle<IList<GameDataTable>> _handle;
        private bool _isLoading;

        public event Action Reloaded;

        #endregion

        #region Properties

        public bool IsLoaded { get; private set; }

        #endregion

        #region Public Methods

        public async UniTask LoadAsync(CancellationToken cancellationToken, string label = DefaultLabel)
        {
            if (IsLoaded) return;
            if (this._isLoading) throw new InvalidOperationException($"{nameof(GameDataService)} is already loading.");

            this._isLoading = true;
            try
            {
                this._handle = Addressables.LoadAssetsAsync<GameDataTable>((object)label, null);
                var tables = await this._handle.ToUniTask(cancellationToken: cancellationToken);
                Index(tables);
                IsLoaded = true;
            }
            catch
            {
                Unload();
                throw;
            }
            finally
            {
                this._isLoading = false;
            }
        }

        public async UniTask ReloadAsync(CancellationToken cancellationToken, string label = DefaultLabel)
        {
            Unload();
            await LoadAsync(cancellationToken, label);
            Reloaded?.Invoke();
        }

        public void Unload()
        {
            foreach (var lookup in this._lookups.Values) lookup.Clear();

            this._lookups.Clear();
            this._tables.Clear();
            IsLoaded = false;

            if (this._handle.IsValid()) Addressables.Release(this._handle);
            this._handle = default;
        }

        public bool TryGet<TRow>(int id, out TRow row) where TRow : class, IGameDataRow
        {
            row = null;
            return TryGetLookup<TRow>(out var lookup) && lookup.TryGet(id, out row);
        }

        public TRow Get<TRow>(int id) where TRow : class, IGameDataRow
        {
            if (TryGet<TRow>(id, out var row)) return row;

            throw new KeyNotFoundException($"{typeof(TRow).Name} id {id} not found.");
        }

        public IReadOnlyList<TRow> GetAll<TRow>() where TRow : class, IGameDataRow
        {
            if (TryGetLookup<TRow>(out var lookup)) return lookup.All;

            return Array.Empty<TRow>();
        }

        public TTable GetTable<TTable>() where TTable : GameDataTable
        {
            if (this._tables.TryGetValue(typeof(TTable), out var table)) return (TTable)table;

            throw new KeyNotFoundException($"Table {typeof(TTable).Name} is not loaded.");
        }

        public void Dispose() => Unload();

        #endregion

        #region Private Methods

        private void Index(IList<GameDataTable> tables)
        {
            for (var i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                if (table == null) continue;

                if (table.DataSchemaVersion != table.CodeSchemaVersion)
                {
                    Debug.LogError($"{table.name}: data schema {table.DataSchemaVersion} does not match code schema {table.CodeSchemaVersion}.", table);
                }

                this._tables[table.GetType()] = table;

                if (!this._lookups.TryGetValue(table.RowType, out var lookup))
                {
                    lookup = table.CreateLookup();
                    this._lookups.Add(table.RowType, lookup);
                }

                table.AppendTo(lookup);
            }
        }

        private bool TryGetLookup<TRow>(out GameDataLookup<TRow> lookup) where TRow : class, IGameDataRow
        {
            lookup = null;
            if (!IsLoaded) throw new InvalidOperationException($"{nameof(GameDataService)} is not loaded.");
            if (!this._lookups.TryGetValue(typeof(TRow), out var untyped)) return false;

            lookup = (GameDataLookup<TRow>)untyped;
            return true;
        }

        #endregion
    }
}
