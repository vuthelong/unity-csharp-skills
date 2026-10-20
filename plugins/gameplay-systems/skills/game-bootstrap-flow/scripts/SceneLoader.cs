using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneLoader : MonoBehaviour, IProgress<float>
{
    #region Fields

    private const float ActivationProgress = 0.9f;

    [SerializeField] private int bootSceneBuildIndex;
    [SerializeField] private CanvasGroup loadingScreen;
    [SerializeField] private Image progressFill;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float minimumScreenTime = 0.75f;

    private bool _isLoading;
    private float _displayedProgress;

    #endregion

    #region Properties

    public int BootSceneBuildIndex => this.bootSceneBuildIndex;
    public bool IsLoading => this._isLoading;

    public event Action<string> LoadStarted;
    public event Action<string> LoadCompleted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        this.SetScreenVisible(0f);
    }

    #endregion

    #region Loading

    public Awaitable LoadSceneAsync(string sceneName, CancellationToken cancellationToken) =>
        this.RunWithLoadingScreenAsync(sceneName, (progress, token) => LoadBuiltInSceneAsync(sceneName, LoadSceneMode.Single, progress), cancellationToken);

    public async Awaitable RunWithLoadingScreenAsync(string label, Func<IProgress<float>, CancellationToken, Awaitable> load, CancellationToken cancellationToken)
    {
        if (load == null) throw new ArgumentNullException(nameof(load));
        if (this._isLoading) throw new InvalidOperationException($"{nameof(SceneLoader)} is already loading.");

        this._isLoading = true;
        this.LoadStarted?.Invoke(label);
        try
        {
            this.Report(0f);
            await this.FadeAsync(1f, cancellationToken);
            var shownAt = Time.realtimeSinceStartup;

            await load(this, cancellationToken);
            this.Report(1f);

            while (Time.realtimeSinceStartup - shownAt < this.minimumScreenTime)
            {
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            await this.FadeAsync(0f, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (this != null) this.SetScreenVisible(0f);
            throw;
        }
        finally
        {
            this._isLoading = false;
        }

        this.LoadCompleted?.Invoke(label);
    }

    public static async Awaitable LoadBuiltInSceneAsync(string sceneName, LoadSceneMode mode, IProgress<float> progress)
    {
        var operation = SceneManager.LoadSceneAsync(sceneName, mode);
        if (operation == null) throw new ArgumentException($"Scene '{sceneName}' is not in the scene list of the active Build Profile.", nameof(sceneName));

        operation.allowSceneActivation = false;
        try
        {
            while (operation.progress < ActivationProgress)
            {
                progress?.Report(operation.progress / ActivationProgress);
                await Awaitable.NextFrameAsync();
            }
        }
        finally
        {
            operation.allowSceneActivation = true;
        }

        while (!operation.isDone)
        {
            await Awaitable.NextFrameAsync();
        }

        if (mode == LoadSceneMode.Additive)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid()) SceneManager.SetActiveScene(scene);
        }

        progress?.Report(1f);
    }

    public static async Awaitable UnloadSceneAsync(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return;

        var operation = SceneManager.UnloadSceneAsync(scene);
        if (operation == null) return;

        while (!operation.isDone)
        {
            await Awaitable.NextFrameAsync();
        }
    }

    #endregion

    #region Loading Screen

    void IProgress<float>.Report(float value) => this.Report(value);

    private void Report(float value)
    {
        this._displayedProgress = value <= 0f ? 0f : Mathf.Max(this._displayedProgress, Mathf.Clamp01(value));
        if (this.progressFill != null) this.progressFill.fillAmount = this._displayedProgress;
    }

    private async Awaitable FadeAsync(float target, CancellationToken cancellationToken)
    {
        if (this.loadingScreen == null) return;

        this.loadingScreen.blocksRaycasts = true;
        var start = this.loadingScreen.alpha;
        var elapsed = 0f;
        while (elapsed < this.fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            this.loadingScreen.alpha = Mathf.Lerp(start, target, elapsed / this.fadeDuration);
            await Awaitable.NextFrameAsync(cancellationToken);
        }

        this.SetScreenVisible(target);
    }

    private void SetScreenVisible(float alpha)
    {
        if (this.loadingScreen == null) return;

        this.loadingScreen.alpha = alpha;
        this.loadingScreen.blocksRaycasts = alpha > 0f;
        this.loadingScreen.interactable = false;
    }

    #endregion
}
