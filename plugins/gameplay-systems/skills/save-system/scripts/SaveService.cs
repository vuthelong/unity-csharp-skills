using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public enum SaveLoadSource
{
    None,
    Primary,
    Backup
}

public readonly struct SaveLoadResult<T>
{
    public SaveLoadResult(T data, SaveLoadSource source, int fileVersion)
    {
        this.Data = data;
        this.Source = source;
        this.FileVersion = fileVersion;
    }

    public T Data { get; }
    public SaveLoadSource Source { get; }
    public int FileVersion { get; }
    public bool Found => this.Source != SaveLoadSource.None;
}

public readonly struct SaveSlotInfo
{
    public SaveSlotInfo(string slot, DateTime lastWriteUtc, bool hasBackup)
    {
        this.Slot = slot;
        this.LastWriteUtc = lastWriteUtc;
        this.HasBackup = hasBackup;
    }

    public string Slot { get; }
    public DateTime LastWriteUtc { get; }
    public bool HasBackup { get; }
}

public sealed class SaveService
{
    #region Fields

    public const string Extension = ".json";
    public const string TempSuffix = ".tmp";
    public const string BackupSuffix = ".bak";
    public const string CorruptSuffix = ".corrupt";

    private const string VersionKey = "version";
    private const string SavedAtKey = "savedAtUtc";
    private const string DataKey = "data";

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly string _root;
    private readonly SaveMigrator _migrator;
    private readonly JsonSerializer _serializer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    #endregion

    #region Properties

    public string RootFolder => this._root;
    public int CurrentVersion => this._migrator.CurrentVersion;

    #endregion

    #region Construction

    public SaveService(SaveMigrator migrator, string subFolder = "Saves", JsonSerializerSettings settings = null)
    {
        this._migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        this._root = Path.Combine(Application.persistentDataPath, subFolder);
        this._serializer = JsonSerializer.Create(settings ?? CreateDefaultSettings());
        Directory.CreateDirectory(this._root);
    }

    public static JsonSerializerSettings CreateDefaultSettings() => new()
    {
        TypeNameHandling = TypeNameHandling.None,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Include,
        Formatting = Formatting.None
    };

    #endregion

    #region Paths

    public string GetSlotPath(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("Slot name is empty.", nameof(slot));
        if (slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slot.Contains('.'))
        {
            throw new ArgumentException($"Slot name '{slot}' contains invalid characters.", nameof(slot));
        }

        return Path.Combine(this._root, slot + Extension);
    }

    public bool Exists(string slot)
    {
        var path = GetSlotPath(slot);
        return File.Exists(path) || File.Exists(path + BackupSuffix);
    }

    public IReadOnlyList<SaveSlotInfo> ListSlots()
    {
        var result = new List<SaveSlotInfo>();
        if (!Directory.Exists(this._root)) return result;

        foreach (var path in Directory.GetFiles(this._root, "*" + Extension))
        {
            if (!string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase)) continue;

            var slot = Path.GetFileNameWithoutExtension(path);
            result.Add(new SaveSlotInfo(slot, File.GetLastWriteTimeUtc(path), File.Exists(path + BackupSuffix)));
        }

        result.Sort((a, b) => b.LastWriteUtc.CompareTo(a.LastWriteUtc));
        return result;
    }

    public void Delete(string slot)
    {
        var path = GetSlotPath(slot);
        DeleteIfExists(path);
        DeleteIfExists(path + BackupSuffix);
        DeleteIfExists(path + TempSuffix);
    }

    #endregion

    #region Save

    public void Save<T>(string slot, T snapshot)
    {
        var path = GetSlotPath(slot);
        var savedAt = DateTime.UtcNow;
        this._gate.Wait();
        try
        {
            WriteFile(path, snapshot, savedAt);
        }
        finally
        {
            this._gate.Release();
        }
    }

    public async Awaitable SaveAsync<T>(string slot, T snapshot, CancellationToken cancellationToken)
    {
        var path = GetSlotPath(slot);
        var savedAt = DateTime.UtcNow;
        await this._gate.WaitAsync(cancellationToken);
        try
        {
            await Awaitable.BackgroundThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();
            WriteFile(path, snapshot, savedAt);
        }
        finally
        {
            this._gate.Release();
            await Awaitable.MainThreadAsync();
        }
    }

    private void WriteFile<T>(string path, T snapshot, DateTime savedAt)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var tempPath = path + TempSuffix;
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using (var text = new StreamWriter(stream, Utf8NoBom, 64 * 1024, true))
            using (var writer = new JsonTextWriter(text))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(VersionKey);
                writer.WriteValue(this._migrator.CurrentVersion);
                writer.WritePropertyName(SavedAtKey);
                writer.WriteValue(savedAt);
                writer.WritePropertyName(DataKey);
                this._serializer.Serialize(writer, snapshot);
                writer.WriteEndObject();
            }

            stream.Flush(true);
        }

        ReplaceAtomically(tempPath, path, path + BackupSuffix);
    }

    private static void ReplaceAtomically(string tempPath, string path, string backupPath)
    {
        if (!File.Exists(path))
        {
            File.Move(tempPath, path);
            return;
        }

        try
        {
            File.Replace(tempPath, path, backupPath, true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(path, backupPath, true);
            File.Delete(path);
            File.Move(tempPath, path);
        }
    }

    #endregion

    #region Load

    public SaveLoadResult<T> Load<T>(string slot)
    {
        var path = GetSlotPath(slot);
        this._gate.Wait();
        try
        {
            return ReadWithFallback<T>(path);
        }
        finally
        {
            this._gate.Release();
        }
    }

    public async Awaitable<SaveLoadResult<T>> LoadAsync<T>(string slot, CancellationToken cancellationToken)
    {
        var path = GetSlotPath(slot);
        await this._gate.WaitAsync(cancellationToken);
        try
        {
            await Awaitable.BackgroundThreadAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return ReadWithFallback<T>(path);
        }
        finally
        {
            this._gate.Release();
            await Awaitable.MainThreadAsync();
        }
    }

    private SaveLoadResult<T> ReadWithFallback<T>(string path)
    {
        var backupPath = path + BackupSuffix;
        Exception primaryError = null;

        if (File.Exists(path))
        {
            try
            {
                return ReadFile<T>(path, SaveLoadSource.Primary);
            }
            catch (SaveVersionException)
            {
                throw;
            }
            catch (Exception e) when (e is JsonException || e is IOException || e is InvalidDataException || e is InvalidCastException || e is FormatException)
            {
                primaryError = e;
            }
        }

        if (!File.Exists(backupPath))
        {
            if (primaryError != null) throw new InvalidDataException($"Save '{path}' is unreadable and has no backup.", primaryError);
            return default;
        }

        var result = ReadFile<T>(backupPath, SaveLoadSource.Backup);
        if (primaryError != null) QuarantinePrimary(path);
        Debug.LogWarning($"{nameof(SaveService)}: loaded backup for '{Path.GetFileName(path)}'. {primaryError?.Message}");
        return result;
    }

    private SaveLoadResult<T> ReadFile<T>(string path, SaveLoadSource source)
    {
        JObject root;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var text = new StreamReader(stream, Utf8NoBom, true))
        using (var reader = new JsonTextReader(text))
        {
            root = JObject.Load(reader);
        }

        var versionToken = root[VersionKey];
        if (versionToken == null || versionToken.Type != JTokenType.Integer)
        {
            throw new InvalidDataException($"Save '{path}' has no integer '{VersionKey}'.");
        }

        if (!(root[DataKey] is JObject data))
        {
            throw new InvalidDataException($"Save '{path}' has no '{DataKey}' object.");
        }

        var fileVersion = versionToken.Value<int>();
        this._migrator.Migrate(data, fileVersion);
        var value = data.ToObject<T>(this._serializer);
        if (value == null) throw new InvalidDataException($"Save '{path}' deserialized to null.");

        return new SaveLoadResult<T>(value, source, fileVersion);
    }

    private static void QuarantinePrimary(string path)
    {
        var corruptPath = path + CorruptSuffix;
        DeleteIfExists(corruptPath);
        File.Move(path, corruptPath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    #endregion
}
