using System;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

public sealed class MusicPlayer : IDisposable
{
    #region Fields
    private const double ScheduleLead = 0.1;
    private const float HalfPi = Mathf.PI * 0.5f;

    private readonly Deck[] _decks = new Deck[2];

    private int _current = -1;
    private int _fadeIn = -1;
    private int _fadeOut = -1;
    private float _fadeOutStart;
    private float _fadeDuration;
    private float _fadeElapsed;

    private float _duckGain = 1f;
    private float _duckTarget = 1f;
    private float _duckSpeed;

    private AudioClip[] _playlist;
    private int _playlistIndex = -1;
    private bool _shuffle;
    private float _playlistFade;

    private bool _paused;
    #endregion

    #region Properties
    public AudioClip CurrentClip => this._current >= 0 ? this._decks[this._current].BodyClip : null;
    public bool IsPlaying => this._current >= 0;
    public bool IsPaused => this._paused;
    #endregion

    #region Public Methods
    public MusicPlayer(Transform parent, AudioMixerGroup group, bool ignoreListenerPause = false)
    {
        for (var i = 0; i < this._decks.Length; i++)
        {
            this._decks[i] = new Deck(
                CreateSource(parent, $"Music_{i}_Intro", group, ignoreListenerPause),
                CreateSource(parent, $"Music_{i}_Body", group, ignoreListenerPause));
        }
    }

    public void Play(AudioClip clip, float fadeSeconds = 1f, bool loop = true)
    {
        this._playlist = null;
        if (this._current >= 0 && this._decks[this._current].BodyClip == clip) return;

        PlayInternal(null, clip, fadeSeconds, loop);
    }

    public void PlayWithIntro(AudioClip intro, AudioClip loopBody, float fadeSeconds = 1f)
    {
        this._playlist = null;
        PlayInternal(intro, loopBody, fadeSeconds, true);
    }

    public void PlayPlaylist(AudioClip[] tracks, bool shuffle, float fadeSeconds = 2f)
    {
        if (tracks == null || tracks.Length == 0) return;

        this._playlist = tracks;
        this._shuffle = shuffle;
        this._playlistFade = fadeSeconds;
        this._playlistIndex = -1;
        AdvancePlaylist();
    }

    public void Stop(float fadeSeconds = 1f)
    {
        this._playlist = null;
        if (this._current < 0) return;

        BeginFade(-1, this._current, fadeSeconds);
        this._current = -1;
    }

    public void Duck(float targetGain, float seconds)
    {
        this._duckTarget = Mathf.Clamp01(targetGain);
        var distance = Mathf.Abs(this._duckTarget - this._duckGain);
        this._duckSpeed = seconds <= 0f ? float.MaxValue : distance / seconds;
    }

    public void Pause()
    {
        if (this._paused) return;

        this._paused = true;
        for (var i = 0; i < this._decks.Length; i++)
        {
            this._decks[i].Pause();
        }
    }

    public void Resume()
    {
        if (!this._paused) return;

        this._paused = false;
        for (var i = 0; i < this._decks.Length; i++)
        {
            this._decks[i].Resume();
        }
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (this._paused) return;

        var dspTime = AudioSettings.dspTime;
        for (var i = 0; i < this._decks.Length; i++)
        {
            this._decks[i].UpdatePending(dspTime);
        }

        if (this._duckGain != this._duckTarget)
        {
            this._duckGain = Mathf.MoveTowards(this._duckGain, this._duckTarget, this._duckSpeed * unscaledDeltaTime);
        }

        TickFade(unscaledDeltaTime);
        TickPlaylist(dspTime);
        ApplyVolumes();
    }

    public void Dispose()
    {
        for (var i = 0; i < this._decks.Length; i++)
        {
            this._decks[i].StopAll();
        }

        this._current = -1;
        this._fadeIn = -1;
        this._fadeOut = -1;
    }
    #endregion

    #region Private Methods
    private void PlayInternal(AudioClip intro, AudioClip body, float fadeSeconds, bool loop)
    {
        if (body == null) return;

        var next = this._current == 0 ? 1 : 0;
        var deck = this._decks[next];
        deck.StopAll();

        var start = AudioSettings.dspTime + ScheduleLead;
        deck.Schedule(intro, body, loop, start);

        BeginFade(next, this._current, fadeSeconds);
        this._current = next;
    }

    private void BeginFade(int fadeIn, int fadeOut, float duration)
    {
        if (this._fadeOut >= 0 && this._fadeOut != fadeIn && this._fadeOut != fadeOut)
        {
            this._decks[this._fadeOut].StopAll();
        }

        this._fadeIn = fadeIn;
        this._fadeOut = fadeOut;
        this._fadeOutStart = fadeOut >= 0 ? this._decks[fadeOut].Gain : 0f;
        this._fadeDuration = Mathf.Max(0f, duration);
        this._fadeElapsed = 0f;

        if (fadeIn >= 0) this._decks[fadeIn].Gain = this._fadeDuration > 0f ? 0f : 1f;
        if (this._fadeDuration <= 0f) FinishFade();
    }

    private void TickFade(float deltaTime)
    {
        if (this._fadeIn < 0 && this._fadeOut < 0) return;

        this._fadeElapsed += deltaTime;
        var t = Mathf.Clamp01(this._fadeElapsed / this._fadeDuration);

        if (this._fadeIn >= 0) this._decks[this._fadeIn].Gain = Mathf.Sin(t * HalfPi);
        if (this._fadeOut >= 0) this._decks[this._fadeOut].Gain = this._fadeOutStart * Mathf.Cos(t * HalfPi);

        if (t >= 1f) FinishFade();
    }

    private void FinishFade()
    {
        if (this._fadeIn >= 0) this._decks[this._fadeIn].Gain = 1f;
        if (this._fadeOut >= 0)
        {
            this._decks[this._fadeOut].Gain = 0f;
            this._decks[this._fadeOut].StopAll();
        }

        this._fadeIn = -1;
        this._fadeOut = -1;
    }

    private void TickPlaylist(double dspTime)
    {
        if (this._playlist == null || this._current < 0) return;

        var deck = this._decks[this._current];
        if (dspTime < deck.EndDsp - this._playlistFade) return;

        AdvancePlaylist();
    }

    private void AdvancePlaylist()
    {
        var count = this._playlist.Length;
        var index = (this._playlistIndex + 1) % count;
        if (this._shuffle && count > 1)
        {
            index = UnityEngine.Random.Range(0, count);
            if (index == this._playlistIndex) index = (index + UnityEngine.Random.Range(1, count)) % count;
        }

        this._playlistIndex = index;
        PlayInternal(null, this._playlist[index], this._playlistFade, false);
    }

    private void ApplyVolumes()
    {
        for (var i = 0; i < this._decks.Length; i++)
        {
            this._decks[i].SetVolume(this._decks[i].Gain * this._duckGain);
        }
    }

    private static AudioSource CreateSource(Transform parent, string name, AudioMixerGroup group, bool ignoreListenerPause)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 0;
        source.volume = 0f;
        source.outputAudioMixerGroup = group;
        source.ignoreListenerPause = ignoreListenerPause;
        return source;
    }
    #endregion

    private sealed class Deck
    {
        #region Fields
        public readonly AudioSource Intro;
        public readonly AudioSource Body;

        public float Gain;
        public AudioClip BodyClip;
        public double EndDsp = double.MaxValue;

        private bool _bodyPending;
        private double _bodyStartDsp;
        #endregion

        #region Public Methods
        public Deck(AudioSource intro, AudioSource body)
        {
            this.Intro = intro;
            this.Body = body;
        }

        public void Schedule(AudioClip intro, AudioClip body, bool loop, double start)
        {
            this.BodyClip = body;
            this.Body.clip = body;
            this.Body.loop = loop;

            var bodyStart = start;
            if (intro != null)
            {
                this.Intro.clip = intro;
                this.Intro.loop = false;
                this.Intro.PlayScheduled(start);
                bodyStart = start + intro.samples / (double)intro.frequency;
            }

            this.Body.PlayScheduled(bodyStart);
            this._bodyPending = true;
            this._bodyStartDsp = bodyStart;
            this.EndDsp = loop ? double.MaxValue : bodyStart + body.samples / (double)body.frequency;
        }

        public void UpdatePending(double dspTime)
        {
            if (this._bodyPending && dspTime >= this._bodyStartDsp) this._bodyPending = false;
        }

        public void Pause()
        {
            if (this._bodyPending)
            {
                this.Body.Stop();
                this.EndDsp = double.MaxValue;
            }
            else
            {
                this.Body.Pause();
            }

            this.Intro.Pause();
        }

        public void Resume()
        {
            if (this.BodyClip == null) return;

            if (!this._bodyPending)
            {
                this.Body.UnPause();
                if (!this.Body.loop)
                {
                    var remaining = (this.BodyClip.samples - this.Body.timeSamples) / (double)this.BodyClip.frequency;
                    this.EndDsp = AudioSettings.dspTime + remaining;
                }

                return;
            }

            var start = AudioSettings.dspTime + ScheduleLead;
            var introClip = this.Intro.clip;
            if (introClip != null)
            {
                var position = this.Intro.timeSamples;
                var introRemaining = (introClip.samples - position) / (double)introClip.frequency;
                this.Intro.Stop();
                this.Intro.timeSamples = position;
                this.Intro.PlayScheduled(start);
                start += introRemaining;
            }

            this.Body.PlayScheduled(start);
            this._bodyStartDsp = start;
            if (!this.Body.loop) this.EndDsp = start + this.BodyClip.samples / (double)this.BodyClip.frequency;
        }

        public void SetVolume(float volume)
        {
            this.Intro.volume = volume;
            this.Body.volume = volume;
        }

        public void StopAll()
        {
            this.Intro.Stop();
            this.Body.Stop();
            this.Intro.clip = null;
            this.Body.clip = null;
            this.BodyClip = null;
            this.Gain = 0f;
            this._bodyPending = false;
            this.EndDsp = double.MaxValue;
        }
        #endregion
    }
}
