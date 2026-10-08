using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

public readonly struct AudioHandle : IEquatable<AudioHandle>
{
    #region Fields
    public static readonly AudioHandle None = default;

    public readonly int Id;
    #endregion

    #region Properties
    public bool IsValid => this.Id != 0;
    #endregion

    #region Public Methods
    public AudioHandle(int id) => this.Id = id;

    public bool Equals(AudioHandle other) => this.Id == other.Id;

    public override bool Equals(object obj) => obj is AudioHandle other && this.Id == other.Id;

    public override int GetHashCode() => this.Id;
    #endregion
}

public sealed class AudioService : IDisposable
{
    #region Fields
    private const float MinFade = 0.0001f;

    private readonly Transform _root;
    private readonly bool _ownsRoot;
    private readonly int _maxVoices;
    private readonly ObjectPool<Voice> _pool;
    private readonly List<Voice> _active;
    private readonly Dictionary<AudioCue, double> _lastPlayTime = new();
    private readonly Dictionary<AudioMixerGroup, AudioSource> _oneShotSources = new();
    private readonly AudioSource _defaultOneShotSource;
    private readonly MusicPlayer _music;

    private int _nextId;
    private bool _paused;
    private bool _disposed;
    #endregion

    #region Properties
    public Transform Root => this._root;
    public MusicPlayer Music => this._music;
    public int ActiveVoiceCount => this._active.Count;
    public bool IsPaused => this._paused;
    #endregion

    #region Public Methods
    public AudioService(Transform root, AudioMixerGroup musicGroup, int maxVoices = 24, int prewarm = 8)
    {
        if (root == null)
        {
            var rootObject = new GameObject(nameof(AudioService));
            Object.DontDestroyOnLoad(rootObject);
            root = rootObject.transform;
            this._ownsRoot = true;
        }

        this._root = root;
        this._maxVoices = Mathf.Max(1, maxVoices);
        this._active = new List<Voice>(this._maxVoices);
        this._pool = new ObjectPool<Voice>(
            CreateVoice,
            OnGetVoice,
            OnReleaseVoice,
            OnDestroyVoice,
            collectionCheck: false,
            defaultCapacity: this._maxVoices,
            maxSize: this._maxVoices);

        this._defaultOneShotSource = CreateOneShotSource("OneShot", null);
        this._music = new MusicPlayer(this._root, musicGroup);
        Prewarm(Mathf.Min(prewarm, this._maxVoices));
    }

    public AudioHandle Play(AudioCue cue, Vector3 position) => PlayInternal(cue, position, null);

    public AudioHandle Play(AudioCue cue, Transform follow)
    {
        if (follow == null) return AudioHandle.None;

        return PlayInternal(cue, follow.position, follow);
    }

    public AudioHandle Play2D(AudioCue cue) => PlayInternal(cue, this._root.position, null);

    public void PlayOneShot(AudioCue cue)
    {
        if (cue == null || this._disposed) return;
        if (!PassesCooldown(cue)) return;

        var clip = cue.PickClip();
        if (clip == null) return;

        var source = GetOneShotSource(cue.MixerGroup);
        source.PlayOneShot(clip, cue.PickVolume());
        this._lastPlayTime[cue] = Time.unscaledTimeAsDouble;
    }

    public bool IsPlaying(AudioHandle handle) => IndexOf(handle) >= 0;

    public void Stop(AudioHandle handle, float fadeOut = 0f)
    {
        var index = IndexOf(handle);
        if (index < 0) return;

        StopAt(index, fadeOut);
    }

    public void StopCue(AudioCue cue, float fadeOut = 0f)
    {
        for (var i = this._active.Count - 1; i >= 0; i--)
        {
            if (this._active[i].Cue == cue) StopAt(i, fadeOut);
        }
    }

    public void StopAll(float fadeOut = 0f)
    {
        for (var i = this._active.Count - 1; i >= 0; i--)
        {
            StopAt(i, fadeOut);
        }
    }

    public void SetPaused(bool paused)
    {
        this._paused = paused;
        AudioListener.pause = paused;
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (this._disposed) return;

        for (var i = this._active.Count - 1; i >= 0; i--)
        {
            var voice = this._active[i];
            if (voice.Source == null)
            {
                RemoveAt(i);
                continue;
            }

            if (voice.Following)
            {
                if (voice.Follow != null) voice.Transform.position = voice.Follow.position;
                else voice.Following = false;
            }

            if (voice.Stopping)
            {
                voice.FadeElapsed += unscaledDeltaTime;
                var t = voice.FadeElapsed / voice.FadeDuration;
                if (t >= 1f)
                {
                    ReleaseAt(i);
                    continue;
                }

                voice.Source.volume = voice.FadeFrom * (1f - t);
                continue;
            }

            if (AudioListener.pause && !voice.Source.ignoreListenerPause) continue;
            if (voice.Source.clip != null && voice.Source.clip.loadState == AudioDataLoadState.Loading) continue;
            if (voice.Source.isPlaying) continue;

            ReleaseAt(i);
        }

        this._music.Tick(unscaledDeltaTime);
    }

    public void Dispose()
    {
        if (this._disposed) return;

        this._disposed = true;
        for (var i = this._active.Count - 1; i >= 0; i--)
        {
            ReleaseAt(i);
        }

        this._pool.Clear();
        this._music.Dispose();

        if (this._ownsRoot && this._root != null) Object.Destroy(this._root.gameObject);
    }
    #endregion

    #region Private Methods
    private AudioHandle PlayInternal(AudioCue cue, Vector3 position, Transform follow)
    {
        if (cue == null || this._disposed) return AudioHandle.None;
        if (!PassesCooldown(cue)) return AudioHandle.None;

        var clip = cue.PickClip();
        if (clip == null) return AudioHandle.None;
        if (!TryMakeRoomForCue(cue)) return AudioHandle.None;
        if (!TryMakeRoomGlobal(cue.Priority)) return AudioHandle.None;

        var voice = this._pool.Get();
        var now = Time.unscaledTimeAsDouble;

        voice.Id = ++this._nextId;
        if (voice.Id == 0) voice.Id = ++this._nextId;
        voice.Cue = cue;
        voice.Follow = follow;
        voice.Following = follow != null;
        voice.StartTime = now;
        voice.Stopping = false;
        voice.Transform.position = position;

        var source = voice.Source;
        cue.ApplyTo(source);
        source.clip = clip;
        source.volume = cue.PickVolume();
        source.pitch = cue.PickPitch();
        source.Play();

        this._active.Add(voice);
        this._lastPlayTime[cue] = now;
        return new AudioHandle(voice.Id);
    }

    private bool PassesCooldown(AudioCue cue)
    {
        if (cue.Cooldown <= 0f) return true;
        if (!this._lastPlayTime.TryGetValue(cue, out var last)) return true;

        return Time.unscaledTimeAsDouble - last >= cue.Cooldown;
    }

    private bool TryMakeRoomForCue(AudioCue cue)
    {
        if (cue.MaxInstances <= 0) return true;

        var count = 0;
        var victim = -1;
        for (var i = 0; i < this._active.Count; i++)
        {
            var voice = this._active[i];
            if (voice.Cue != cue || voice.Stopping) continue;

            count++;
            if (victim < 0 || IsBetterVictim(voice, this._active[victim], cue.StealMode)) victim = i;
        }

        if (count < cue.MaxInstances) return true;
        if (cue.StealMode == VoiceStealMode.None || victim < 0) return false;

        ReleaseAt(victim);
        return true;
    }

    private bool TryMakeRoomGlobal(int priority)
    {
        if (this._active.Count < this._maxVoices) return true;

        var victim = -1;
        for (var i = 0; i < this._active.Count; i++)
        {
            var voice = this._active[i];
            if (victim < 0)
            {
                victim = i;
                continue;
            }

            var current = this._active[victim];
            if (voice.Stopping && !current.Stopping)
            {
                victim = i;
                continue;
            }

            if (voice.Cue.Priority > current.Cue.Priority) victim = i;
            else if (voice.Cue.Priority == current.Cue.Priority && voice.StartTime < current.StartTime) victim = i;
        }

        if (victim < 0) return false;

        var candidate = this._active[victim];
        if (!candidate.Stopping && candidate.Cue.Priority < priority) return false;

        ReleaseAt(victim);
        return true;
    }

    private bool IsBetterVictim(Voice candidate, Voice current, VoiceStealMode mode)
    {
        return mode switch
        {
            VoiceStealMode.Quietest => candidate.Source.volume < current.Source.volume,
            _ => candidate.StartTime < current.StartTime
        };
    }

    private void StopAt(int index, float fadeOut)
    {
        var voice = this._active[index];
        if (fadeOut <= MinFade || voice.Source == null)
        {
            ReleaseAt(index);
            return;
        }

        if (voice.Stopping) return;

        voice.Stopping = true;
        voice.FadeFrom = voice.Source.volume;
        voice.FadeDuration = fadeOut;
        voice.FadeElapsed = 0f;
    }

    private int IndexOf(AudioHandle handle)
    {
        if (!handle.IsValid) return -1;

        for (var i = 0; i < this._active.Count; i++)
        {
            if (this._active[i].Id == handle.Id) return i;
        }

        return -1;
    }

    private void ReleaseAt(int index)
    {
        var voice = this._active[index];
        RemoveAt(index);
        if (voice.Source == null) return;

        this._pool.Release(voice);
    }

    private void RemoveAt(int index)
    {
        var last = this._active.Count - 1;
        this._active[index] = this._active[last];
        this._active.RemoveAt(last);
    }

    private void Prewarm(int count)
    {
        var temp = ListPool<Voice>.Get();
        for (var i = 0; i < count; i++)
        {
            temp.Add(this._pool.Get());
        }

        for (var i = 0; i < temp.Count; i++)
        {
            this._pool.Release(temp[i]);
        }

        ListPool<Voice>.Release(temp);
    }

    private Voice CreateVoice()
    {
        var gameObject = new GameObject("Voice");
        gameObject.transform.SetParent(this._root, false);
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return new Voice(gameObject, source);
    }

    private AudioSource GetOneShotSource(AudioMixerGroup group)
    {
        if (group == null) return this._defaultOneShotSource;
        if (this._oneShotSources.TryGetValue(group, out var source) && source != null) return source;

        source = CreateOneShotSource(group.name, group);
        this._oneShotSources[group] = source;
        return source;
    }

    private AudioSource CreateOneShotSource(string label, AudioMixerGroup group)
    {
        var gameObject = new GameObject($"OneShot_{label}");
        gameObject.transform.SetParent(this._root, false);
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.outputAudioMixerGroup = group;
        return source;
    }

    private static void OnGetVoice(Voice voice) => voice.GameObject.SetActive(true);

    private static void OnReleaseVoice(Voice voice)
    {
        voice.Source.Stop();
        voice.Source.clip = null;
        voice.Cue = null;
        voice.Follow = null;
        voice.Following = false;
        voice.Stopping = false;
        voice.Id = 0;
        voice.GameObject.SetActive(false);
    }

    private static void OnDestroyVoice(Voice voice)
    {
        if (voice.GameObject != null) Object.Destroy(voice.GameObject);
    }
    #endregion

    private sealed class Voice
    {
        #region Fields
        public readonly GameObject GameObject;
        public readonly Transform Transform;
        public readonly AudioSource Source;

        public int Id;
        public AudioCue Cue;
        public Transform Follow;
        public bool Following;
        public double StartTime;
        public bool Stopping;
        public float FadeFrom;
        public float FadeDuration;
        public float FadeElapsed;
        #endregion

        #region Public Methods
        public Voice(GameObject gameObject, AudioSource source)
        {
            this.GameObject = gameObject;
            this.Transform = gameObject.transform;
            this.Source = source;
        }
        #endregion
    }
}
