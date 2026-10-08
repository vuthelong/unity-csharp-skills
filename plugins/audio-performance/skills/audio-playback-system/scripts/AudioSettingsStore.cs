using UnityEngine;
using UnityEngine.Audio;

public enum AudioChannel
{
    Master,
    Music,
    Sfx
}

public sealed class AudioSettingsStore
{
    #region Fields
    public const float MinDb = -80f;

    private const float MinLinear = 0.0001f;
    private const string MuteKey = "audio.muted";

    private static readonly string[] PrefKeys = { "audio.master", "audio.music", "audio.sfx" };

    private readonly AudioMixer _mixer;
    private readonly string[] _parameters;
    private readonly float[] _volumes;
    private readonly float _defaultVolume;

    private bool _muted;
    #endregion

    #region Properties
    public bool IsMuted => this._muted;
    #endregion

    #region Public Methods
    public AudioSettingsStore(
        AudioMixer mixer,
        string masterParameter = "MasterVolume",
        string musicParameter = "MusicVolume",
        string sfxParameter = "SfxVolume",
        float defaultVolume = 0.8f)
    {
        this._mixer = mixer;
        this._parameters = new[] { masterParameter, musicParameter, sfxParameter };
        this._volumes = new float[this._parameters.Length];
        this._defaultVolume = Mathf.Clamp01(defaultVolume);
    }

    public static float LinearToDb(float linear)
    {
        if (linear <= MinLinear) return MinDb;

        return Mathf.Max(MinDb, 20f * Mathf.Log10(linear));
    }

    public static float DbToLinear(float db)
    {
        if (db <= MinDb) return 0f;

        return Mathf.Pow(10f, db / 20f);
    }

    public void Load()
    {
        for (var i = 0; i < this._volumes.Length; i++)
        {
            this._volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeys[i], this._defaultVolume));
        }

        this._muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
    }

    public void ApplyAll()
    {
        for (var i = 0; i < this._volumes.Length; i++)
        {
            ApplyChannel((AudioChannel)i);
        }
    }

    public float GetVolume(AudioChannel channel) => this._volumes[(int)channel];

    public void SetVolume(AudioChannel channel, float linear)
    {
        var index = (int)channel;
        this._volumes[index] = Mathf.Clamp01(linear);
        PlayerPrefs.SetFloat(PrefKeys[index], this._volumes[index]);
        ApplyChannel(channel);
    }

    public void SetMuted(bool muted)
    {
        this._muted = muted;
        PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        ApplyChannel(AudioChannel.Master);
    }

    public void Save() => PlayerPrefs.Save();
    #endregion

    #region Private Methods
    private void ApplyChannel(AudioChannel channel)
    {
        var index = (int)channel;
        var db = channel == AudioChannel.Master && this._muted ? MinDb : LinearToDb(this._volumes[index]);
        if (this._mixer.SetFloat(this._parameters[index], db)) return;

        Debug.LogError($"{nameof(AudioSettingsStore)}: exposed parameter '{this._parameters[index]}' not found on {this._mixer.name}", this._mixer);
    }
    #endregion
}
