using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public static class AddressableSceneLoading
{
    #region Loading

    public static async Awaitable<AsyncOperationHandle<SceneInstance>> LoadAsync(object key, LoadSceneMode mode, IProgress<float> progress)
    {
        var handle = Addressables.LoadSceneAsync(key, mode, false);
        while (!handle.IsDone)
        {
            progress?.Report(handle.PercentComplete);
            await Awaitable.NextFrameAsync();
        }

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            var error = handle.OperationException;
            Addressables.Release(handle);
            throw new InvalidOperationException($"Addressable scene '{key}' failed to load.", error);
        }

        var activation = handle.Result.ActivateAsync();
        while (!activation.isDone)
        {
            await Awaitable.NextFrameAsync();
        }

        if (mode == LoadSceneMode.Additive) SceneManager.SetActiveScene(handle.Result.Scene);

        progress?.Report(1f);
        return handle;
    }

    public static async Awaitable UnloadAsync(AsyncOperationHandle<SceneInstance> handle)
    {
        if (!handle.IsValid()) return;

        var unload = Addressables.UnloadSceneAsync(handle, UnloadSceneOptions.None, true);
        while (!unload.IsDone)
        {
            await Awaitable.NextFrameAsync();
        }
    }

    #endregion
}
