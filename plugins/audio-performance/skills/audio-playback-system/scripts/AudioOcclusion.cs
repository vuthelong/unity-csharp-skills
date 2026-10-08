using UnityEngine;

[RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
public sealed class AudioOcclusion : MonoBehaviour
{
    #region Fields
    private const float OpenCutoff = 22000f;
    private const float FilterOffThreshold = 0.001f;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

    [SerializeField] private Transform listener;
    [SerializeField] private LayerMask occluderMask = ~0;
    [SerializeField, Min(0.02f)] private float checkInterval = 0.15f;
    [SerializeField, Min(1)] private int hitsForFullOcclusion = 2;
    [SerializeField, Range(10f, 22000f)] private float occludedCutoff = 1200f;
    [SerializeField, Range(0f, 1f)] private float occludedVolume = 0.5f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.2f;

    private AudioSource _source;
    private AudioLowPassFilter _filter;
    private float _baseVolume;
    private float _target;
    private float _current;
    private float _nextCheck;
    #endregion

    #region Properties
    public float Occlusion => this._current;

    public float BaseVolume
    {
        get => this._baseVolume;
        set => this._baseVolume = Mathf.Clamp01(value);
    }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        this._source = GetComponent<AudioSource>();
        this._filter = GetComponent<AudioLowPassFilter>();
        this._baseVolume = this._source.volume;
    }

    private void OnEnable()
    {
        this._nextCheck = Time.unscaledTime + Random.Range(0f, this.checkInterval);
        this._target = 0f;
        this._current = 0f;
        ApplyOcclusion();
    }

    private void Start()
    {
        if (this.listener != null) return;

        var found = FindAnyObjectByType<AudioListener>();
        if (found != null) this.listener = found.transform;
    }

    private void Update()
    {
        if (this.listener == null || !this._source.isPlaying) return;

        var now = Time.unscaledTime;
        if (now >= this._nextCheck)
        {
            this._nextCheck = now + this.checkInterval;
            this._target = MeasureOcclusion();
        }

        if (Mathf.Approximately(this._current, this._target)) return;

        this._current = Mathf.MoveTowards(this._current, this._target, Time.unscaledDeltaTime / this.smoothTime);
        ApplyOcclusion();
    }

    private void OnDisable()
    {
        this._source.volume = this._baseVolume;
        this._filter.enabled = false;
    }
    #endregion

    #region Public Methods
    public void SetListener(Transform target) => this.listener = target;
    #endregion

    #region Private Methods
    private float MeasureOcclusion()
    {
        var from = this.listener.position;
        var delta = transform.position - from;
        var distance = delta.magnitude;
        if (distance < 0.01f || distance > this._source.maxDistance) return 0f;

        var hits = Physics.RaycastNonAlloc(from, delta / distance, HitBuffer, distance, this.occluderMask, QueryTriggerInteraction.Ignore);
        return Mathf.Clamp01(hits / (float)this.hitsForFullOcclusion);
    }

    private void ApplyOcclusion()
    {
        var occluded = this._current > FilterOffThreshold;
        this._filter.enabled = occluded;
        this._filter.cutoffFrequency = OpenCutoff * Mathf.Pow(this.occludedCutoff / OpenCutoff, this._current);
        this._source.volume = this._baseVolume * Mathf.Lerp(1f, this.occludedVolume, this._current);
    }
    #endregion
}
