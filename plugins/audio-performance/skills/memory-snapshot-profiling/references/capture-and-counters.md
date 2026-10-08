# Capture API and live counters

Unity 6000.x. Project code, not `eval` input.

## Snapshot capture component

`MemoryProfiler.TakeSnapshot` is asynchronous: it returns immediately and invokes the callback after the `.snap` is written. Do not read or upload the file before the callback reports success.

```csharp
using System;
using System.IO;
using Unity.Profiling.Memory;
using UnityEngine;

public sealed class SnapshotCapture : MonoBehaviour
{
    [SerializeField] private CaptureFlags _flags =
        CaptureFlags.ManagedObjects | CaptureFlags.NativeObjects | CaptureFlags.NativeAllocations;

    private bool _busy;

    public void Capture(string label)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (_busy)
        {
            return;
        }

        _busy = true;
        string directory = Path.Combine(Application.persistentDataPath, "MemoryCaptures");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{label}_{DateTime.Now:yyyyMMdd_HHmmss}.snap");
        MemoryProfiler.TakeSnapshot(path, OnFinished, _flags);
#endif
    }

    private void OnFinished(string path, bool success)
    {
        _busy = false;
        if (success)
        {
            Debug.Log($"Memory snapshot written: {path}");
        }
        else
        {
            Debug.LogError($"Memory snapshot failed: {path}");
        }
    }
}
```

Pull snapshots from a device with `adb pull` (Android) or the Files app / Xcode container download (iOS), then import them in the Memory Profiler window (**Import** button).

An overload adds a screenshot callback: `TakeSnapshot(path, finishCallback, screenshotCallback, flags)`; the screenshot callback receives the path, a success flag, and the captured frame.

## CaptureFlags

| Flag | Captures | Cost |
|---|---|---|
| `ManagedObjects` | Managed heap and object graph | Needed for leaked shells and reference chains |
| `NativeObjects` | Unity native objects (textures, meshes, clips) | Needed for asset audits |
| `NativeAllocations` | Native allocation details | Largest and slowest; drop first on low-memory devices |
| `NativeAllocationSites` | Callstacks of native allocations | Requires native callstack support; slow |
| `NativeStackTraces` | Native stack traces | Slow; deep engine investigations only |

## Live counters with ProfilerRecorder

Cheap enough for an on-screen debug overlay in development builds. Counters exist in release builds too, but some report 0 outside development builds.

```csharp
using Unity.Profiling;
using UnityEngine;

public sealed class MemoryCounters : MonoBehaviour
{
    private ProfilerRecorder _totalUsed;
    private ProfilerRecorder _gcUsed;
    private ProfilerRecorder _gcReserved;
    private ProfilerRecorder _gfxUsed;
    private ProfilerRecorder _audioUsed;
    private ProfilerRecorder _gcAllocInFrame;

    private void OnEnable()
    {
        _totalUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        _gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
        _gcReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
        _gfxUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
        _audioUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Audio Used Memory");
        _gcAllocInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    }

    private void OnDisable()
    {
        _totalUsed.Dispose();
        _gcUsed.Dispose();
        _gcReserved.Dispose();
        _gfxUsed.Dispose();
        _audioUsed.Dispose();
        _gcAllocInFrame.Dispose();
    }

    public string Describe()
    {
        const float Mb = 1024f * 1024f;
        return $"Total {_totalUsed.LastValue / Mb:F1} MB | GC {_gcUsed.LastValue / Mb:F1}/{_gcReserved.LastValue / Mb:F1} MB | "
             + $"Gfx {_gfxUsed.LastValue / Mb:F1} MB | Audio {_audioUsed.LastValue / Mb:F1} MB | "
             + $"GC alloc/frame {_gcAllocInFrame.LastValue} B";
    }
}
```

Other useful counters in `ProfilerCategory.Memory`: `Total Reserved Memory`, `System Used Memory`, `Texture Memory`, `Mesh Memory`, `Video Used Memory`, `GC Allocation In Frame Count`. Dispose every recorder; an undisposed recorder leaks native memory.

For one-off reads without recorders: `UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()`, `GetMonoUsedSizeLong()`, `GetMonoHeapSizeLong()`, and `Profiler.GetRuntimeMemorySizeLong(obj)` for a single object's native size (development builds and Editor only).
