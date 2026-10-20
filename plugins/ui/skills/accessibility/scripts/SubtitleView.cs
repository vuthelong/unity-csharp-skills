using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct SubtitleCue
{
    public SubtitleCue(string speaker, string text, Color speakerColor, float minSeconds = 0f, bool isSoundCaption = false)
    {
        this.Speaker = speaker;
        this.Text = text;
        this.SpeakerColor = speakerColor;
        this.MinSeconds = minSeconds;
        this.IsSoundCaption = isSoundCaption;
    }

    public string Speaker { get; }
    public string Text { get; }
    public Color SpeakerColor { get; }
    public float MinSeconds { get; }
    public bool IsSoundCaption { get; }

    public static SubtitleCue Caption(string text, float minSeconds = 0f) => new(null, text, Color.white, minSeconds, true);
}

public sealed class SubtitleView : MonoBehaviour
{
    #region Fields

    private const string HexDigits = "0123456789ABCDEF";

    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image background;
    [SerializeField] private GameObject root;
    [SerializeField, Min(8f)] private float baseFontSize = 36f;
    [SerializeField, Range(1, 4)] private int maxVisibleLines = 2;
    [SerializeField, Range(2, 32)] private int queueCapacity = 8;
    [SerializeField, Min(0.5f)] private float minimumSeconds = 1.5f;
    [SerializeField, Min(5f)] private float charactersPerSecond = 15f;
    [SerializeField, Min(0f)] private float lingerSeconds = 0.5f;
    [SerializeField] private bool pauseWithGame = true;

    private readonly StringBuilder _builder = new(256);
    private SubtitleCue[] _queue;
    private int _queueHead;
    private int _queueCount;
    private SubtitleCue[] _visible;
    private float[] _visibleUntil;
    private int _visibleCount;
    private bool _dirty;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (this.root == this.gameObject)
        {
            Debug.LogWarning("SubtitleView root must be a child object; hiding this object would stop Update.", this);
            this.root = null;
        }

        this._queue = new SubtitleCue[this.queueCapacity];
        this._visible = new SubtitleCue[this.maxVisibleLines];
        this._visibleUntil = new float[this.maxVisibleLines];
        this.SetRootVisible(false);
    }

    private void OnEnable()
    {
        AccessibilityOptions.Changed += this.OnOptionsChanged;
        this.OnOptionsChanged(AccessibilityOptions.Current);
    }

    private void OnDisable() => AccessibilityOptions.Changed -= this.OnOptionsChanged;

    private void Update()
    {
        var now = this.Now;
        for (var i = this._visibleCount - 1; i >= 0; i--)
        {
            if (now >= this._visibleUntil[i])
            {
                this.RemoveVisibleAt(i);
            }
        }

        while (this._visibleCount < this._visible.Length && this._queueCount > 0)
        {
            this.PromoteNext(now);
        }

        if (this._dirty)
        {
            this.Rebuild();
        }
    }

    #endregion

    #region Public API

    public void Show(in SubtitleCue cue)
    {
        var options = AccessibilityOptions.Current;
        if (string.IsNullOrEmpty(cue.Text) || (cue.IsSoundCaption ? !options.soundCaptions : !options.subtitles))
        {
            return;
        }

        if (this._queueCount == this._queue.Length)
        {
            this._queueHead = (this._queueHead + 1) % this._queue.Length;
            this._queueCount--;
        }

        this._queue[(this._queueHead + this._queueCount) % this._queue.Length] = cue;
        this._queueCount++;
    }

    public void ClearAll()
    {
        System.Array.Clear(this._queue, 0, this._queue.Length);
        System.Array.Clear(this._visible, 0, this._visible.Length);
        this._queueHead = 0;
        this._queueCount = 0;
        this._visibleCount = 0;
        this._dirty = true;
    }

    #endregion

    #region Queue

    private float Now => this.pauseWithGame ? Time.time : Time.unscaledTime;

    private void PromoteNext(float now)
    {
        var cue = this._queue[this._queueHead];
        this._queue[this._queueHead] = default;
        this._queueHead = (this._queueHead + 1) % this._queue.Length;
        this._queueCount--;

        var speed = this.charactersPerSecond * AccessibilityOptions.Current.subtitleReadingSpeed;
        var readSeconds = cue.Text.Length / Mathf.Max(1f, speed) + this.lingerSeconds;
        var seconds = Mathf.Max(this.minimumSeconds, Mathf.Max(cue.MinSeconds, readSeconds));

        this._visible[this._visibleCount] = cue;
        this._visibleUntil[this._visibleCount] = now + seconds;
        this._visibleCount++;
        this._dirty = true;
    }

    private void RemoveVisibleAt(int index)
    {
        for (var i = index; i < this._visibleCount - 1; i++)
        {
            this._visible[i] = this._visible[i + 1];
            this._visibleUntil[i] = this._visibleUntil[i + 1];
        }

        this._visibleCount--;
        this._visible[this._visibleCount] = default;
        this._dirty = true;
    }

    #endregion

    #region Rendering

    private void OnOptionsChanged(AccessibilityOptions options)
    {
        if (this.label != null)
        {
            this.label.fontSize = this.baseFontSize * options.subtitleScale;
        }

        if (this.background != null)
        {
            var color = this.background.color;
            color.a = options.subtitleBackgroundOpacity;
            this.background.color = color;
            this.background.enabled = options.subtitleBackgroundOpacity > 0.001f;
        }

        this._dirty = true;
    }

    private void Rebuild()
    {
        this._dirty = false;
        if (this._visibleCount == 0)
        {
            this.SetRootVisible(false);
            return;
        }

        if (this.label == null)
        {
            return;
        }

        var options = AccessibilityOptions.Current;
        this._builder.Clear();
        for (var i = 0; i < this._visibleCount; i++)
        {
            if (i > 0)
            {
                this._builder.Append('\n');
            }

            this.AppendCue(this._visible[i], options);
        }

        this.label.SetText(this._builder);
        this.SetRootVisible(true);
    }

    private void AppendCue(in SubtitleCue cue, AccessibilityOptions options)
    {
        if (cue.IsSoundCaption)
        {
            this._builder.Append("<i>[<noparse>").Append(cue.Text).Append("</noparse>]</i>");
            return;
        }

        if (options.speakerNames && !string.IsNullOrEmpty(cue.Speaker))
        {
            if (options.speakerColors)
            {
                this._builder.Append("<color=#");
                AppendHex(this._builder, cue.SpeakerColor);
                this._builder.Append('>');
            }

            this._builder.Append("<b><noparse>").Append(cue.Speaker).Append("</noparse>:</b> ");

            if (options.speakerColors)
            {
                this._builder.Append("</color>");
            }
        }

        this._builder.Append("<noparse>").Append(cue.Text).Append("</noparse>");
    }

    private static void AppendHex(StringBuilder builder, Color color)
    {
        Color32 c = color;
        AppendByte(builder, c.r);
        AppendByte(builder, c.g);
        AppendByte(builder, c.b);
    }

    private static void AppendByte(StringBuilder builder, byte value)
    {
        builder.Append(HexDigits[value >> 4]);
        builder.Append(HexDigits[value & 0xF]);
    }

    private void SetRootVisible(bool visible)
    {
        var target = this.root != null ? this.root : this.label != null ? this.label.gameObject : null;
        if (target != null && target != this.gameObject && target.activeSelf != visible)
        {
            target.SetActive(visible);
        }
    }

    #endregion
}
