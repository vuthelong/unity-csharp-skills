using UnityEngine;
using UnityEngine.Rendering;

public enum AmbientDriveMode
{
    TrilightGradients,
    SkyboxThrottled
}

public sealed class SunMoonDriver : MonoBehaviour
{
    #region Fields

    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int AtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");
    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");

    [Header("Clock")]
    [SerializeField] private TimeOfDay timeOfDay;
    [SerializeField] private WeatherController weather;

    [Header("Orbit")]
    [SerializeField] private Light sun;
    [SerializeField] private Light moon;
    [SerializeField, Range(0f, 360f)] private float azimuth = 30f;
    [SerializeField, Range(-60f, 60f)] private float orbitTilt = 20f;
    [SerializeField, Range(0f, 15f)] private float horizonFadeDegrees = 6f;

    [Header("Light over the day (x = normalized time)")]
    [SerializeField] private Gradient sunColor = new();
    [SerializeField] private AnimationCurve sunIntensity = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
    [SerializeField] private Gradient moonColor = new();
    [SerializeField] private AnimationCurve moonIntensity = AnimationCurve.Constant(0f, 1f, 0.15f);

    [Header("Ambient")]
    [SerializeField] private AmbientDriveMode ambientMode = AmbientDriveMode.TrilightGradients;
    [SerializeField] private Gradient ambientSky = new();
    [SerializeField] private Gradient ambientEquator = new();
    [SerializeField] private Gradient ambientGround = new();
    [SerializeField] private AnimationCurve skyboxAmbientIntensity = AnimationCurve.Constant(0f, 1f, 1f);
    [SerializeField, Min(0.5f)] private float environmentUpdateInterval = 4f;
    [SerializeField, Range(0.001f, 0.1f)] private float environmentUpdateMinDelta = 0.01f;

    [Header("Skybox (procedural)")]
    [SerializeField] private Material skyboxTemplate;
    [SerializeField] private AnimationCurve skyExposure = AnimationCurve.Constant(0f, 1f, 1.3f);
    [SerializeField] private AnimationCurve atmosphereThickness = AnimationCurve.Constant(0f, 1f, 1f);
    [SerializeField] private Gradient skyTint = new();

    [Header("Fog")]
    [SerializeField] private bool driveFog = true;
    [SerializeField] private Gradient fogColor = new();
    [SerializeField] private AnimationCurve fogDensity = AnimationCurve.Constant(0f, 1f, 0.004f);

    [Header("Night grading")]
    [SerializeField] private Volume nightVolume;
    [SerializeField] private AnimationCurve nightVolumeWeight = AnimationCurve.Constant(0f, 1f, 0f);

    private Material _skyboxInstance;
    private Material _originalSkybox;
    private bool _hasExposure;
    private bool _hasThickness;
    private bool _hasTint;
    private float _lastEnvironmentUpdateTime = float.NegativeInfinity;
    private float _lastEnvironmentTime01 = -1f;
    private Light _activeSun;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (this.skyboxTemplate != null)
        {
            this._originalSkybox = RenderSettings.skybox;
            this._skyboxInstance = new Material(this.skyboxTemplate);
            this._hasExposure = this._skyboxInstance.HasProperty(ExposureId);
            this._hasThickness = this._skyboxInstance.HasProperty(AtmosphereThicknessId);
            this._hasTint = this._skyboxInstance.HasProperty(SkyTintId);
            RenderSettings.skybox = this._skyboxInstance;
        }

        RenderSettings.ambientMode = this.ambientMode == AmbientDriveMode.TrilightGradients ? AmbientMode.Trilight : AmbientMode.Skybox;
        this._lastEnvironmentTime01 = -1f;
    }

    private void OnDisable()
    {
        if (this._skyboxInstance != null)
        {
            if (RenderSettings.skybox == this._skyboxInstance)
            {
                RenderSettings.skybox = this._originalSkybox;
            }

            Destroy(this._skyboxInstance);
            this._skyboxInstance = null;
        }
    }

    private void LateUpdate()
    {
        if (this.timeOfDay == null)
        {
            return;
        }

        var t = this.timeOfDay.Normalized;
        var sample = this.weather != null ? this.weather.Current : WeatherSample.Neutral;

        this.UpdateOrbit(t, sample.sunMultiplier);
        this.UpdateAmbient(t, sample.sunMultiplier);
        this.UpdateSkybox(t);
        this.UpdateFog(t, sample);

        if (this.nightVolume != null)
        {
            this.nightVolume.weight = Mathf.Clamp01(this.nightVolumeWeight.Evaluate(t));
        }
    }

    #endregion

    #region Orbit and lights

    private void UpdateOrbit(float t, float weatherSunMultiplier)
    {
        var pitch = (t - this.timeOfDay.Sunrise01) / Mathf.Max(0.0001f, this.timeOfDay.Sunset01 - this.timeOfDay.Sunrise01) * 180f;
        if (t < this.timeOfDay.Sunrise01 || t >= this.timeOfDay.Sunset01)
        {
            var nightLength = 1f - (this.timeOfDay.Sunset01 - this.timeOfDay.Sunrise01);
            var intoNight = Mathf.Repeat(t - this.timeOfDay.Sunset01, 1f);
            pitch = 180f + intoNight / Mathf.Max(0.0001f, nightLength) * 180f;
        }

        var basis = Quaternion.Euler(0f, this.azimuth, 0f) * Quaternion.Euler(0f, 0f, this.orbitTilt);
        var sunElevation = Mathf.Sin(pitch * Mathf.Deg2Rad);
        var fade = Mathf.Max(0.0001f, Mathf.Sin(this.horizonFadeDegrees * Mathf.Deg2Rad));
        var sunVisibility = Mathf.Clamp01(sunElevation / fade);
        var moonVisibility = Mathf.Clamp01(-sunElevation / fade);

        Light main = null;
        if (this.sun != null)
        {
            this.sun.transform.rotation = basis * Quaternion.Euler(pitch, 0f, 0f);
            this.sun.color = this.sunColor.Evaluate(t);
            this.sun.intensity = this.sunIntensity.Evaluate(t) * sunVisibility * weatherSunMultiplier;
            var sunOn = sunVisibility > 0f;
            if (this.sun.enabled != sunOn)
            {
                this.sun.enabled = sunOn;
            }

            if (sunOn)
            {
                main = this.sun;
            }
        }

        if (this.moon != null)
        {
            this.moon.transform.rotation = basis * Quaternion.Euler(pitch + 180f, 0f, 0f);
            this.moon.color = this.moonColor.Evaluate(t);
            this.moon.intensity = this.moonIntensity.Evaluate(t) * moonVisibility * Mathf.Lerp(1f, weatherSunMultiplier, 0.5f);
            var moonOn = moonVisibility > 0f && main == null;
            if (this.moon.enabled != moonOn)
            {
                this.moon.enabled = moonOn;
            }

            if (moonOn)
            {
                main = this.moon;
            }
        }

        if (main != null && main != this._activeSun)
        {
            this._activeSun = main;
            RenderSettings.sun = main;
        }
    }

    #endregion

    #region Environment

    private void UpdateAmbient(float t, float weatherSunMultiplier)
    {
        if (this.ambientMode == AmbientDriveMode.TrilightGradients)
        {
            var dim = Mathf.Lerp(1f, weatherSunMultiplier, 0.6f);
            RenderSettings.ambientSkyColor = this.ambientSky.Evaluate(t) * dim;
            RenderSettings.ambientEquatorColor = this.ambientEquator.Evaluate(t) * dim;
            RenderSettings.ambientGroundColor = this.ambientGround.Evaluate(t) * dim;
            return;
        }

        RenderSettings.ambientIntensity = this.skyboxAmbientIntensity.Evaluate(t) * Mathf.Lerp(1f, weatherSunMultiplier, 0.6f);

        var now = Time.unscaledTime;
        var delta = Mathf.Abs(Mathf.DeltaAngle(t * 360f, this._lastEnvironmentTime01 * 360f)) / 360f;
        if (this._lastEnvironmentTime01 >= 0f && (now - this._lastEnvironmentUpdateTime < this.environmentUpdateInterval || delta < this.environmentUpdateMinDelta))
        {
            return;
        }

        this._lastEnvironmentUpdateTime = now;
        this._lastEnvironmentTime01 = t;
        DynamicGI.UpdateEnvironment();
    }

    private void UpdateSkybox(float t)
    {
        if (this._skyboxInstance == null)
        {
            return;
        }

        if (this._hasExposure)
        {
            this._skyboxInstance.SetFloat(ExposureId, this.skyExposure.Evaluate(t));
        }

        if (this._hasThickness)
        {
            this._skyboxInstance.SetFloat(AtmosphereThicknessId, this.atmosphereThickness.Evaluate(t));
        }

        if (this._hasTint)
        {
            this._skyboxInstance.SetColor(SkyTintId, this.skyTint.Evaluate(t));
        }
    }

    private void UpdateFog(float t, in WeatherSample sample)
    {
        if (!this.driveFog)
        {
            return;
        }

        RenderSettings.fogColor = Color.Lerp(this.fogColor.Evaluate(t), sample.fogTint, sample.fogTintAmount);
        RenderSettings.fogDensity = this.fogDensity.Evaluate(t) * sample.fogDensityMultiplier;
    }

    #endregion
}
