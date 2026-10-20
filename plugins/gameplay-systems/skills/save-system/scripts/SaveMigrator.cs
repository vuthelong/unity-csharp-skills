using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public interface ISaveMigration
{
    int FromVersion { get; }

    void Apply(JObject data);
}

public sealed class SaveMigration : ISaveMigration
{
    #region Fields

    private readonly Action<JObject> _apply;

    #endregion

    #region Properties

    public int FromVersion { get; }

    #endregion

    #region Construction

    public SaveMigration(int fromVersion, Action<JObject> apply)
    {
        this.FromVersion = fromVersion;
        this._apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    public void Apply(JObject data) => this._apply(data);

    #endregion
}

public sealed class SaveVersionException : Exception
{
    public SaveVersionException(string message) : base(message)
    {
    }
}

public sealed class SaveMigrator
{
    #region Fields

    private readonly Dictionary<int, ISaveMigration> _steps = new();

    #endregion

    #region Properties

    public int CurrentVersion { get; }

    #endregion

    #region Construction

    public SaveMigrator(int currentVersion, IEnumerable<ISaveMigration> migrations)
    {
        if (currentVersion < 1) throw new ArgumentOutOfRangeException(nameof(currentVersion));

        this.CurrentVersion = currentVersion;
        foreach (var migration in migrations)
        {
            if (migration.FromVersion < 1 || migration.FromVersion >= currentVersion)
            {
                throw new ArgumentException($"Migration from version {migration.FromVersion} is outside 1..{currentVersion - 1}.");
            }

            if (!this._steps.TryAdd(migration.FromVersion, migration))
            {
                throw new ArgumentException($"Duplicate migration from version {migration.FromVersion}.");
            }
        }

        for (var version = 1; version < currentVersion; version++)
        {
            if (!this._steps.ContainsKey(version))
            {
                throw new ArgumentException($"Missing migration from version {version} to {version + 1}.");
            }
        }
    }

    #endregion

    #region Migration

    public bool NeedsMigration(int fileVersion) => fileVersion < this.CurrentVersion;

    public JObject Migrate(JObject data, int fileVersion)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (fileVersion < 1) throw new SaveVersionException($"Save version {fileVersion} is invalid.");
        if (fileVersion > this.CurrentVersion)
        {
            throw new SaveVersionException($"Save version {fileVersion} is newer than this build ({this.CurrentVersion}).");
        }

        for (var version = fileVersion; version < this.CurrentVersion; version++)
        {
            this._steps[version].Apply(data);
        }

        return data;
    }

    #endregion
}
