// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:RUNTIME-LOADER]
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

public static class SpriteAtlasLateBinding
{
#if UNITY_ADDRESSABLES
    private static readonly Dictionary<string, AsyncOperationHandle<SpriteAtlas>> Handles = new();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
#if UNITY_ADDRESSABLES
        Handles.Clear();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
        SpriteAtlasManager.atlasRequested += OnAtlasRequested;
    }

    private static void OnAtlasRequested(string tag, Action<SpriteAtlas> callback)
    {
#if UNITY_ADDRESSABLES
        if (!Handles.TryGetValue(tag, out var handle) || !handle.IsValid())
        {
            handle = Addressables.LoadAssetAsync<SpriteAtlas>(tag);
            Handles[tag] = handle;
        }

        if (handle.IsDone)
        {
            Deliver(tag, handle, callback);
            return;
        }

        handle.Completed += completed => Deliver(tag, completed, callback);
#else
        Debug.LogError($"[SpriteAtlas] Late-binding requested '{tag}' but UNITY_ADDRESSABLES is not defined.");
#endif
    }

#if UNITY_ADDRESSABLES
    private static void Deliver(string tag, AsyncOperationHandle<SpriteAtlas> handle, Action<SpriteAtlas> callback)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            callback(handle.Result);
            return;
        }

        if (!Handles.TryGetValue(tag, out var stored) || !stored.Equals(handle))
            return;

        Debug.LogError($"[SpriteAtlas] Failed to load atlas '{tag}': {handle.OperationException}");
        Handles.Remove(tag);
        if (handle.IsValid())
            Addressables.Release(handle);
    }

    public static AsyncOperationHandle<SpriteAtlas> Preload(string atlasAddress)
    {
        if (!Handles.TryGetValue(atlasAddress, out var handle) || !handle.IsValid())
        {
            handle = Addressables.LoadAssetAsync<SpriteAtlas>(atlasAddress);
            Handles[atlasAddress] = handle;
        }
        return handle;
    }

    public static void Release(string atlasAddress)
    {
        if (Handles.TryGetValue(atlasAddress, out var handle))
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            Handles.Remove(atlasAddress);
        }
    }

    public static void ReleaseAll()
    {
        foreach (var handle in Handles.Values)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        Handles.Clear();
    }
#endif
}
