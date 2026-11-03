using UnityEngine;
using UnityEngine.Audio;

public enum AudioChannel
{
    Master,
    Music,
    Sfx,
    Voice
}

public sealed class AudioSettingsStore
{
    #region Fields

    public const float MinDb = -80f;
    public const int ChannelCount = 4;

    private const float MinLinear = 0.0001f;
    private const string MuteKey = "audio.muted";

    private static readonly string[] PrefKeys = { "audio.master", "audio.music", "audio.sfx", "audio.voice" };

    private readonly AudioMixer _mixer;
    private readonly string[] _parameters;
    private readonly float[] _volumes;
    private readonly float _defaultVolume;

    private bool _muted;
    private bool _usePlayerPrefs;

    #endregion

    #region Properties

    public bool IsMuted => this._muted;

    #endregion

    #region Persistence

    public AudioSettingsStore(
        AudioMixer mixer,
        string masterParameter = "MasterVolume",
        string musicParameter = "MusicVolume",
        string sfxParameter = "SfxVolume",
        string voiceParameter = "VoiceVolume",
        float defaultVolume = 0.8f)
    {
        this._mixer = mixer;
        this._parameters = new[] { masterParameter, musicParameter, sfxParameter, voiceParameter };
        this._volumes = new float[ChannelCount];
        this._defaultVolume = Mathf.Clamp01(defaultVolume);
        for (var i = 0; i < ChannelCount; i++)
        {
            this._volumes[i] = this._defaultVolume;
        }
    }

    public void Load(float master, float music, float sfx, float voice, bool muted)
    {
        this._usePlayerPrefs = false;
        this._volumes[(int)AudioChannel.Master] = Mathf.Clamp01(master);
        this._volumes[(int)AudioChannel.Music] = Mathf.Clamp01(music);
        this._volumes[(int)AudioChannel.Sfx] = Mathf.Clamp01(sfx);
        this._volumes[(int)AudioChannel.Voice] = Mathf.Clamp01(voice);
        this._muted = muted;
    }

    public void LoadFromPlayerPrefs()
    {
        this._usePlayerPrefs = true;
        for (var i = 0; i < ChannelCount; i++)
        {
            this._volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeys[i], this._defaultVolume));
        }

        this._muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
    }

    public void SaveToPlayerPrefs()
    {
        if (!this._usePlayerPrefs) return;

        for (var i = 0; i < ChannelCount; i++)
        {
            PlayerPrefs.SetFloat(PrefKeys[i], this._volumes[i]);
        }

        PlayerPrefs.SetInt(MuteKey, this._muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    #endregion

    #region Volume

    public void ApplyAll()
    {
        for (var i = 0; i < ChannelCount; i++)
        {
            ApplyChannel((AudioChannel)i);
        }
    }

    public float GetVolume(AudioChannel channel) => this._volumes[(int)channel];

    public void SetVolume(AudioChannel channel, float linear)
    {
        this._volumes[(int)channel] = Mathf.Clamp01(linear);
        ApplyChannel(channel);
    }

    public void SetMuted(bool muted)
    {
        this._muted = muted;
        ApplyChannel(AudioChannel.Master);
    }

    private void ApplyChannel(AudioChannel channel)
    {
        var index = (int)channel;
        if (string.IsNullOrEmpty(this._parameters[index])) return;

        var db = channel == AudioChannel.Master && this._muted ? MinDb : LinearToDb(this._volumes[index]);
        if (this._mixer.SetFloat(this._parameters[index], db)) return;

        Debug.LogError($"{nameof(AudioSettingsStore)}: exposed parameter '{this._parameters[index]}' not found on {this._mixer.name}", this._mixer);
    }

    #endregion

    #region Conversion

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

    #endregion
}
