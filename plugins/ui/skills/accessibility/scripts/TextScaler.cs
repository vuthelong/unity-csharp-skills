using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public sealed class TextScaler : MonoBehaviour
{
    #region Fields

    [SerializeField, Min(1f)] private float minimumFontSize = 18f;
    [SerializeField] private bool useUiScaleInsteadOfTextScale;

    private TMP_Text _text;
    private float _baseSize;
    private float _baseMin;
    private float _baseMax;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this._text = this.GetComponent<TMP_Text>();
        this._baseSize = this._text.fontSize;
        this._baseMin = this._text.fontSizeMin;
        this._baseMax = this._text.fontSizeMax;
    }

    private void OnEnable()
    {
        AccessibilityOptions.Changed += this.Apply;
        this.Apply(AccessibilityOptions.Current);
    }

    private void OnDisable() => AccessibilityOptions.Changed -= this.Apply;

    #endregion

    #region Apply

    private void Apply(AccessibilityOptions options)
    {
        var scale = this.useUiScaleInsteadOfTextScale ? options.uiScale : options.textScale;
        if (this._text.enableAutoSizing)
        {
            this._text.fontSizeMin = Mathf.Max(this.minimumFontSize, this._baseMin * scale);
            this._text.fontSizeMax = Mathf.Max(this._text.fontSizeMin, this._baseMax * scale);
            return;
        }

        this._text.fontSize = Mathf.Max(this.minimumFontSize, this._baseSize * scale);
    }

    #endregion
}
