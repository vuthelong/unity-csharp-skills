using System;
using UnityEngine;
using UnityEngine.Audio;

public enum VoiceStealMode
{
    None,
    Oldest,
    Quietest
}

[CreateAssetMenu(fileName = "AudioCue", menuName = "Audio/Audio Cue")]
public sealed class AudioCue : ScriptableObject
{
    #region Fields
    [Header("Clips")]
    [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Mix")]
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField] private Vector2 volumeRange = new(0.9f, 1f);
    [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);
    [SerializeField, Range(0, 256)] private int priority = 128;
    [SerializeField] private bool loop;
    [SerializeField] private bool ignoreListenerPause;

    [Header("Voice limiting")]
    [SerializeField, Min(0)] private int maxInstances = 4;
    [SerializeField] private VoiceStealMode stealMode = VoiceStealMode.Oldest;
    [SerializeField, Min(0f)] private float cooldown = 0.05f;

    [Header("Spatial")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    [SerializeField] private AnimationCurve customRolloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField, Min(0.01f)] private float minDistance = 1f;
    [SerializeField, Min(0.01f)] private float maxDistance = 30f;
    [SerializeField, Range(0f, 360f)] private float spread;
    [SerializeField, Range(0f, 5f)] private float dopplerLevel = 1f;
    [SerializeField, Range(0f, 1.1f)] private float reverbZoneMix = 1f;

    [NonSerialized] private int _lastIndex = -1;
    #endregion

    #region Properties
    public AudioMixerGroup MixerGroup => this.mixerGroup;
    public int Priority => this.priority;
    public bool Loop => this.loop;
    public bool IgnoreListenerPause => this.ignoreListenerPause;
    public int MaxInstances => this.maxInstances;
    public VoiceStealMode StealMode => this.stealMode;
    public float Cooldown => this.cooldown;
    public bool IsSpatial => this.spatialBlend > 0f;
    public int ClipCount => this.clips.Length;
    #endregion

    #region Public Methods
    public AudioClip PickClip()
    {
        var count = this.clips.Length;
        if (count == 0) return null;
        if (count == 1) return this.clips[0];

        var index = UnityEngine.Random.Range(0, count);
        if (this.avoidImmediateRepeat && index == this._lastIndex)
        {
            index = (index + UnityEngine.Random.Range(1, count)) % count;
        }

        this._lastIndex = index;
        return this.clips[index];
    }

    public float PickVolume() => UnityEngine.Random.Range(this.volumeRange.x, this.volumeRange.y);

    public float PickPitch() => UnityEngine.Random.Range(this.pitchRange.x, this.pitchRange.y);

    public void ApplyTo(AudioSource source)
    {
        source.outputAudioMixerGroup = this.mixerGroup;
        source.priority = this.priority;
        source.loop = this.loop;
        source.ignoreListenerPause = this.ignoreListenerPause;
        source.spatialBlend = this.spatialBlend;
        source.rolloffMode = this.rolloffMode;
        source.minDistance = this.minDistance;
        source.maxDistance = this.maxDistance;
        source.spread = this.spread;
        source.dopplerLevel = this.dopplerLevel;
        source.reverbZoneMix = this.reverbZoneMix;

        if (this.rolloffMode == AudioRolloffMode.Custom)
        {
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, this.customRolloff);
        }
    }
    #endregion

    #region Unity Callbacks
    private void OnValidate()
    {
        if (this.volumeRange.y < this.volumeRange.x) this.volumeRange.y = this.volumeRange.x;
        if (this.pitchRange.y < this.pitchRange.x) this.pitchRange.y = this.pitchRange.x;
        if (this.pitchRange.x <= 0f) this.pitchRange.x = 0.01f;
        if (this.maxDistance < this.minDistance) this.maxDistance = this.minDistance;
    }
    #endregion
}
