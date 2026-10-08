using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public interface IGameDataRow
    {
        int Id { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GameDataRefAttribute : Attribute
    {
        #region Properties

        public Type RowType { get; }

        public bool Optional { get; set; }

        #endregion

        #region Public Methods

        public GameDataRefAttribute(Type rowType)
        {
            RowType = rowType;
        }

        #endregion
    }

    public abstract class GameDataTable : ScriptableObject
    {
        #region Fields

        [SerializeField] private int schemaVersion;

        #endregion

        #region Properties

        public abstract Type RowType { get; }

        public abstract int RowCount { get; }

        public virtual int CodeSchemaVersion => 1;

        public int DataSchemaVersion => this.schemaVersion;

        #endregion

        #region Public Methods

        public abstract IGameDataRow GetRow(int index);

        public abstract IGameDataLookup CreateLookup();

        public abstract void AppendTo(IGameDataLookup lookup);

#if UNITY_EDITOR
        public bool StampSchemaVersion()
        {
            if (this.schemaVersion == CodeSchemaVersion) return false;

            this.schemaVersion = CodeSchemaVersion;
            return true;
        }
#endif

        #endregion
    }

    public abstract class GameDataTable<TRow> : GameDataTable where TRow : class, IGameDataRow
    {
        #region Fields

        [SerializeField] protected TRow[] rows = Array.Empty<TRow>();

        #endregion

        #region Properties

        public IReadOnlyList<TRow> Rows => this.rows;

        public override Type RowType => typeof(TRow);

        public override int RowCount => this.rows == null ? 0 : this.rows.Length;

        #endregion

        #region Public Methods

        public override IGameDataRow GetRow(int index) => this.rows[index];

        public override IGameDataLookup CreateLookup() => new GameDataLookup<TRow>();

        public override void AppendTo(IGameDataLookup lookup)
        {
            if (lookup is not GameDataLookup<TRow> typed)
            {
                Debug.LogError($"{name}: lookup for {lookup.RowType.Name} cannot hold {typeof(TRow).Name}", this);
                return;
            }

            typed.Add(this.rows, name);
        }

        #endregion
    }

    public interface IGameDataLookup
    {
        Type RowType { get; }

        int Count { get; }

        void Clear();
    }

    public sealed class GameDataLookup<TRow> : IGameDataLookup where TRow : class, IGameDataRow
    {
        #region Fields

        private readonly Dictionary<int, TRow> _byId = new();
        private readonly List<TRow> _all = new();

        #endregion

        #region Properties

        public Type RowType => typeof(TRow);

        public int Count => this._all.Count;

        public IReadOnlyList<TRow> All => this._all;

        #endregion

        #region Public Methods

        public bool TryGet(int id, out TRow row) => this._byId.TryGetValue(id, out row);

        public void Add(TRow[] source, string sourceName)
        {
            if (source == null) return;

            for (var i = 0; i < source.Length; i++)
            {
                var row = source[i];
                if (row == null) continue;

                if (!this._byId.TryAdd(row.Id, row))
                {
                    Debug.LogError($"Duplicate {typeof(TRow).Name} id {row.Id} in {sourceName}; keeping the first one.");
                    continue;
                }

                this._all.Add(row);
            }
        }

        public void Clear()
        {
            this._byId.Clear();
            this._all.Clear();
        }

        #endregion
    }
}
