using System;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class GameSettings
{
    #region Fields

    public const int CurrentVersion = 1;
    public const string FileName = "settings.json";

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public int version = CurrentVersion;

    public int width;
    public int height;
    public uint refreshNumerator;
    public uint refreshDenominator;
    public FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;

    public int qualityLevel = -1;
    public int vSyncCount = 1;
    public int targetFrameRate = -1;
    public float renderScale = 1f;

    public string bindingOverridesJson = string.Empty;

    #endregion

    #region Properties

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public RefreshRate RefreshRate => new() { numerator = this.refreshNumerator, denominator = this.refreshDenominator };

    public bool HasRefreshRate => this.refreshNumerator > 0 && this.refreshDenominator > 0;

    #endregion

    #region Factory

    public static GameSettings CreateDefaults()
    {
        var current = Screen.currentResolution;
        return new GameSettings
        {
            width = current.width,
            height = current.height,
            refreshNumerator = current.refreshRateRatio.numerator,
            refreshDenominator = current.refreshRateRatio.denominator,
            fullScreenMode = FullScreenMode.FullScreenWindow,
            qualityLevel = QualitySettings.GetQualityLevel(),
            vSyncCount = 1,
            targetFrameRate = -1,
            renderScale = 1f
        };
    }

    public GameSettings Clone() => (GameSettings)MemberwiseClone();

    public bool DisplayEquals(GameSettings other) =>
        other != null
        && this.width == other.width
        && this.height == other.height
        && this.fullScreenMode == other.fullScreenMode
        && this.refreshNumerator == other.refreshNumerator
        && this.refreshDenominator == other.refreshDenominator;

    public void SetResolution(Resolution resolution)
    {
        this.width = resolution.width;
        this.height = resolution.height;
        this.refreshNumerator = resolution.refreshRateRatio.numerator;
        this.refreshDenominator = resolution.refreshRateRatio.denominator;
    }

    #endregion

    #region Persistence

    public static GameSettings LoadOrDefault()
    {
        var path = FilePath;
        var settings = TryRead(path) ?? TryRead(path + ".bak") ?? CreateDefaults();
        settings.Sanitize();
        return settings;
    }

    public void Save()
    {
        this.version = CurrentVersion;
        var path = FilePath;
        var tempPath = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(tempPath, JsonUtility.ToJson(this, true), Utf8NoBom);

        if (!File.Exists(path))
        {
            File.Move(tempPath, path);
            return;
        }

        try
        {
            File.Replace(tempPath, path, path + ".bak", true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(path, path + ".bak", true);
            File.Delete(path);
            File.Move(tempPath, path);
        }
    }

    private static GameSettings TryRead(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path, Utf8NoBom);
            if (string.IsNullOrWhiteSpace(json)) return null;

            var settings = CreateDefaults();
            JsonUtility.FromJsonOverwrite(json, settings);
            return settings;
        }
        catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
        {
            Debug.LogWarning($"{nameof(GameSettings)}: could not read '{path}': {e.Message}");
            return null;
        }
    }

    private void Sanitize()
    {
        if (this.width <= 0 || this.height <= 0)
        {
            var current = Screen.currentResolution;
            this.width = current.width;
            this.height = current.height;
        }

        if (this.qualityLevel < 0 || this.qualityLevel >= QualitySettings.count) this.qualityLevel = QualitySettings.GetQualityLevel();
        this.vSyncCount = Mathf.Clamp(this.vSyncCount, 0, 4);
        if (this.targetFrameRate == 0 || this.targetFrameRate < -1) this.targetFrameRate = -1;
        this.renderScale = Mathf.Clamp(this.renderScale, 0.1f, 2f);
        this.bindingOverridesJson ??= string.Empty;
        this.version = CurrentVersion;
    }

    #endregion
}
