---
name: memory-snapshot-profiling
description: Captures, compares, and interprets Unity Memory Profiler snapshots (.snap) to find leaks, duplicate or oversized assets, and runtime memory growth in Unity 6. Use when memory climbs across scene loads, an asset or AudioClip/Texture is suspected to stay loaded, the user mentions com.unity.memoryprofiler, Memory Profiler window, .snap files, leaked managed shells, Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot, CaptureFlags, or wants live memory counters through ProfilerRecorder. Not for per-frame GC.Alloc hunting in the CPU Profiler alone.
license: MIT
metadata:
  category: audio-performance
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/memoryprofiler"
  unity: "6000.0+"
---

# Memory Snapshot Profiling

A snapshot is an offline capture of the whole managed and native memory state of one process at one moment. It answers "what is still allocated and who holds it", which the live Profiler Memory module (allocator byte counts) cannot.

## Rules

- Capture from a **development build on the target device** for numbers that matter. An Editor capture includes the Editor itself, Editor-only assets, and Play Mode overhead; use it only for quick structural checks.
- A snapshot shows one instant. Reach the exact state first (scene loaded, a few frames settled, GC run if you are hunting leaks).
- Compare two snapshots taken at the **same logical state** (for example main menu before and after one round trip into a level). Comparing different states shows expected differences, not leaks.
- Snapshots are large (hundreds of MB). Do not save them under `Assets/`; the package default is `<Project>/MemoryCaptures/`, which is outside the asset database. Add it to `.gitignore`.
- Hand interpretation to the user with the snapshot path; the Memory Profiler window is the analysis tool.

## Setup

1. Install `com.unity.memoryprofiler` (Package Manager > Unity Registry > Memory Profiler; use the version Unity 6 resolves, 1.1.x).
2. Build with **Development Build** enabled. For on-device capture, also enable **Autoconnect Profiler** or connect manually by IP from the target dropdown.
3. Open **Window > Analysis > Memory Profiler**, pick the target (Editor or connected player) in the capture dropdown, and click **Capture**.

## Leak workflow

1. Go to state A (for example main menu). Wait a few frames.
2. Capture snapshot 1.
3. Enter the suspect flow (load level, open screen, play match) and return to state A.
4. Optionally force cleanup in a development build: `Resources.UnloadUnusedAssets()` then `GC.Collect()`, to separate real leaks from pending collection.
5. Capture snapshot 2.
6. In the Memory Profiler window select both and open **Compare**. Look at:
   - **Unity Objects**, sorted by size delta: textures, meshes, AudioClips, materials from the level that should be gone.
   - **Leaked Managed Shells**: C# objects still referencing destroyed `UnityEngine.Object`s, usually via static fields, event subscriptions never removed, or caches. The References panel shows the holder chain.
   - **All Of Memory** delta by category: managed heap growth versus native versus graphics.
7. Repeat the round trip once more. Growth that recurs each cycle is a leak; one-time growth is usually caching or pooling.

## Single-snapshot audit

Open one snapshot and check:

- **Summary**: total resident vs allocated, managed heap reserved vs used (large unused reserved means fragmentation or a past spike), graphics memory.
- **Unity Objects** grouped by type: largest textures (Read/Write enabled doubles them; missing mipmaps or uncompressed formats), AudioClips (Decompress On Load on long clips; see `optimize-audio`), meshes with Read/Write, duplicate assets with the same name (the same asset pulled into several AssetBundles or Addressables groups).
- **Fonts and atlases**: oversized dynamic TMP atlases.

## Capture from script

Use for automated captures (soak tests, CI, a debug menu). Unity 6 API lives in `Unity.Profiling.Memory` (CoreModule). The older `UnityEngine.Profiling.Memory.Experimental.MemoryProfiler` is obsolete; do not use it in new code. Capture works only in development builds and the Editor. See [references/capture-and-counters.md](references/capture-and-counters.md) for the component, flags, and ProfilerRecorder counters.

## Correlate with CPU data

- The CPU Profiler (`GC.Alloc` samples, call stacks with **Call Stacks** recording on) shows what allocates per frame.
- The snapshot shows what stays allocated.
- Capture the CPU frame of a spike, then a snapshot right after, and correlate: the frame says what ran, the snapshot says what remained.

## Pitfalls

- Capturing the Editor while a player is connected: check the target dropdown.
- Capture fails or times out on very large heaps or low-memory devices: retry with fewer `CaptureFlags` (drop `NativeAllocations`) or capture earlier.
- Snapshots from a different Unity version may not open; capture and analyze with the same major version.
- Non-development builds cannot be captured; there is no profiler connection.

## References

- [references/capture-and-counters.md](references/capture-and-counters.md) — read when writing capture code or live memory counters.

## See also

- `optimize-audio` — reducing AudioClip memory once a snapshot shows it.
- `optimize-web` — Web-specific memory limits and browser memory tools.
