---
name: cpu-performance-profiling
description: Triages CPU-bound frames and hitches in Unity 6 with the Unity Profiler - profiling a development player instead of the Editor (Autoconnect Profiler, targeted Deep Profile), CPU Usage Timeline vs Hierarchy, main vs render thread, GC.Alloc samples with call stacks, ProfilerMarker and ProfilerRecorder instrumentation with an in-game stats overlay, Profile Analyzer (com.unity.performance.profile-analyzer) before/after comparison, and fixes for common hotspots (Update fan-out, LINQ/string/boxing allocations, GetComponent in hot paths, physics queries, Animator, Canvas rebuilds, Debug.Log in builds). Use for low FPS with a busy main thread, stutter or GC spikes, "the game hitches every few seconds", Steam Deck 30/60 fps budgets, or adding custom profiler markers. Not for GPU, draw-call or SetPass cost (optimize-3d-rendering), memory leaks or .snap captures (memory-snapshot-profiling), or network bandwidth and latency (network-debugging).
license: MIT
metadata:
  category: audio-performance
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://github.com/scenario-labs/skills/tree/main/skills/game-engines/unity/scenario-unity-performance, https://docs.unity3d.com/6000.0/Documentation/Manual/profiling-target-device.html, https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-build-settings-reference.html, https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-deep-profiling.html, https://docs.unity3d.com/6000.0/Documentation/Manual/ProfilerCPU.html, https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-markers.html, https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-counters-reference.html, https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-add-markers-code.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerMarker.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/FrameTimingManager.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/FrameTiming.html, https://docs.unity3d.com/6000.0/Documentation/Manual/performance-incremental-garbage-collection.html, https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.performance.profile-analyzer.html, https://docs.unity3d.com/Packages/com.unity.performance.profile-analyzer@1.4/manual/profile-analyzer-window.html, https://docs.unity3d.com/Packages/com.unity.performance.profile-analyzer@1.4/manual/compare-view.html"
  unity: "6000.0+"
---

# CPU Performance Profiling

Find out which CPU work makes a frame too long, change one thing, and prove the change with a before/after capture. The Editor is only for quick checks: Play mode adds `EditorLoop` time, Editor-only safety checks and their `GC.Alloc`s, and runs on desktop hardware.

## Frame budget

| Target | Budget per frame | Aim for main thread under |
|---|---|---|
| 60 fps (desktop, Steam Deck 60 Hz) | 16.67 ms | about 13 ms |
| 30 fps (Steam Deck battery / heavy scenes) | 33.33 ms | about 26 ms |

Keep roughly 20 % headroom for spikes, thermal throttling and the co-op host, which runs server logic for every player on top of its own client. Measure the Steam Deck separately; it is the low end of the PC target. Deck notes: [references/profiler-workflow.md](references/profiler-workflow.md#steam-deck).

## Workflow

1. **Fix the scenario.** Same save, same camera path, same player count (profile the host with clients connected, not a solo session). Write down the frame time you see now.
2. **Build a development player.** File > Build Profiles > your platform > enable **Development Build** and **Autoconnect Profiler**. Leave **Deep Profiling Support** off for normal captures. Enable Player Settings > Resolution and Presentation > **Run In Background** so capture continues when the player loses focus.
3. **Capture.** Window > Analysis > Profiler. With Autoconnect the player connects on launch; otherwise pick it in the target dropdown next to Record (or `<Enter IP>`). Record a few hundred frames of the problem, then stop and save the capture (`.data`).
4. **CPU or GPU?** Switch the CPU Usage details pane to **Timeline** and look at the main and render threads together:
   - Main thread busy in `PlayerLoop` work, render thread idle in `Gfx.WaitForCommands`: CPU main-thread bound. Continue here.
   - Main thread in `Gfx.WaitForPresentOnGfxThread` while the render thread sits in `Gfx.PresentFrame`: GPU bound. Go to `optimize-3d-rendering`.
   - Render thread busy preparing commands (`Camera.Render`, batching): CPU render-thread bound, also `optimize-3d-rendering`.
   - `WaitForTargetFPS` on the main thread is idle time from VSync or `Application.targetFrameRate`, not cost.
5. **Find the cost.** Switch to **Hierarchy**, Thread = Main Thread, sort by **Self ms**. Use **Inverted Hierarchy** when the cost is many small samples spread across systems. Read [references/profiler-workflow.md](references/profiler-workflow.md) for the views and the marker table.
6. **Spikes.** Click the spike in the chart. Typical causes: `GC.Collect`, a burst of `GC.Alloc`, `Instantiate`/`Destroy` waves, scene or asset loading, `Physics.Simulate` running several times in one frame, `Mono.JIT` on first use (Mono only).
7. **Narrow it down.** Wrap suspects in `ProfilerMarker`s (below). Use Deep Profile only on a small, targeted repro; it slows everything and can run out of memory on a full game.
8. **Fix one thing**, capture the same scenario again, and compare medians in Profile Analyzer (Window > Analysis > Profile Analyzer > **Compare**, **Pull Data** from each capture). Keep both `.data` files with the `.pdata`.
9. **Verify on the Steam Deck** before calling it done.

## GC allocations

- Target **0 B GC Alloc per frame** in steady gameplay. Allocation spikes on events (spawning, opening a menu) are fine if they are small and rare.
- Hierarchy: sort by the **GC Alloc** column; the `GC.Alloc` sample shows under the method that allocated. Turn on call stack recording for `GC.Alloc` in the Profiler toolbar to see the managed call stack in the Timeline tooltip.
- Ignore allocations under `EditorOnly` samples; they exist only in Play mode. Confirm in a player.
- Incremental GC (Project Settings > Player > Configuration > **Incremental GC**, on by default) spreads collection across frames and uses idle time when VSync or `targetFrameRate` is set. It does not make GC cheaper and adds write-barrier overhead. Keep it on unless a capture shows the marking phase never finishing.
- Steady per-frame allocation is a CPU problem (this skill). Memory that grows and never returns is a leak: use `memory-snapshot-profiling`.

## Instrument your own code

```csharp
using Unity.Profiling;

public sealed class EnemyDirector
{
    private static readonly ProfilerMarker s_Think = new(ProfilerCategory.Scripts, "EnemyDirector.Think");
    private static readonly ProfilerMarker s_Path = new(ProfilerCategory.Scripts, "EnemyDirector.Path");

    public void Tick()
    {
        using (s_Think.Auto())
        {
            s_Path.Begin();
            RepathAgents();
            s_Path.End();
        }
    }

    private void RepathAgents()
    {
    }
}
```

- `Begin`/`End` are compiled out of non-development builds; `Auto()` stays but returns a no-op scope with minimal cost.
- Sample metadata (`ProfilerMarker<int>` with up to three numeric parameters, `Begin(string)` with a string parameter) comes from the Profiling Core package: add `com.unity.profiling.core` by name, it is not listed in the Package Manager UI.
- Keep markers `static readonly`. Do not put `/` in marker names (the chart highlight breaks). Do not `await` or `yield` inside a marked scope; the Console logs an error.
- `ProfilerRecorder` reads markers and counters at runtime, including in release players for some counters. Use it for an on-screen overlay or automated performance tests.

In-game overlay: [scripts/FrameStatsOverlay.cs](scripts/FrameStatsOverlay.cs). Read when you need frame, main-thread, render-thread and GPU times against the budget, a CPU/GPU-bound hint, GC bytes per frame and selected marker times on a device without the Editor attached. It uses `FrameTimingManager` and `ProfilerRecorder`, writes into a `TMP_Text` through `SetCharArray` (no per-frame GC) and disables itself in non-development builds by default. Release players need Player Settings > **Frame Timing Stats** for frame timings, and `GC Allocated In Frame` is unavailable outside development players.

## Hotspots to check first

| Profiler shows | Usual cause | Fix |
|---|---|---|
| `BehaviourUpdate` large, thousands of tiny `Update` samples | One `Update` per object | One manager loop over plain C# objects |
| `GC.Alloc` every frame under gameplay code | LINQ, string building, boxing, closures, `new` in loops | Cache, reuse, update text only on change |
| `GetComponent`, `FindMainCamera`, `Find*` in per-frame code | Lookups in hot paths | Cache in `Awake`, inject references |
| `Physics.Simulate` / `FixedBehaviourUpdate` repeated in one frame | Fixed timestep catching up | Fewer bodies and contacts, cheaper colliders, review Fixed Timestep |
| `Physics.Raycast*` / `Overlap*` heavy | Many queries, `*All` variants allocating | `NonAlloc` variants, layer masks, fewer queries |
| `Director.PrepareFrame`, `Animators.*` | Many Animators, off-screen animation | Culling Mode, fewer layers, one Animator per hierarchy |
| `Canvas.*` / layout samples | Canvas rebuild on every change | Split dynamic UI into its own Canvas, no layout groups on hot elements |
| `Debug.Log` / stack trace samples in a player | Logging left on in builds | Strip logs from release builds |

Fixes with code: [references/hotspots.md](references/hotspots.md).

## Rules

- Name the bound first (main thread, render thread, GPU) before touching anything: removing garbage does not speed up a frame bound by SetPass calls or GPU time.
- Never optimize from Editor numbers alone. Confirm the problem and the fix in a development player on target hardware.
- One change per capture. Record the median frame time before and after; a single frame proves nothing. Compare about 200 frames per side, interleave A/B runs, and count a change only when the sign is the same across runs; counts (allocations, draw calls) are exact, milliseconds drift with other processes.
- Discard the first launch of a new build; it is several times slower than later launches.
- For worlds, profile the worst viewpoint: teleport to anchor points, turn 360 degrees, keep the worst heading.
- Read **Self ms** to find who does the work and **Total ms** to find which system owns it.
- Close Profiler modules you do not need; collecting their stats costs time inside the player.
- Do not leave Deep Profiling Support on in builds you measure; it changes timings for every script call.
- `GC.Alloc` sample time is not the cost: the Profiler records only a timestamp and size, the bill comes later in `GC.Collect`. Count bytes and calls per frame and target 0 B in gameplay.
- Spreading work over frames lowers the peak, not the total. Fix data layout first (one manager loop over arrays instead of thousands of `Update` calls), then amortize.
- Reach for Burst and the Job System only after a capture shows one hot, data-parallel loop (hundreds of agents, per-vertex work). Then check the `JobHandle.Complete` sync points with Flow Events in the Timeline.

## Pitfalls

| Symptom | Fix |
|---|---|
| Player never appears in the target dropdown | Development Build off, firewall blocking the Editor, or different network; use `<Enter IP>` |
| Capture full of `EditorLoop` | You profiled Play mode in the Editor; profile the player |
| Frame times look fine but the game feels choppy | Look at max and spike frames, not the average; check `GC.Collect` and fixed-timestep catch-up |
| Deep Profile capture freezes or runs out of memory | Profile a smaller repro, or add `ProfilerMarker`s instead |
| Overlay shows `n/a` for a marker | Name or category not found; check the exact marker name in the Profiler Hierarchy |
| `GC Alloc` differs between Editor and player | Editor-only checks allocate; trust the player |
| `ProfilerRecorder` reads 0 forever | created without `ProfilerRecorderOptions.StartImmediately`; use `ProfilerRecorder.StartNew` |
| Allocations appear with a feature off | a closure in a rarely taken branch captures locals for the whole method; move the captured locals into the branch |
| Spawn spike in `Physics.BakePhysXCollisionMeshData` | MeshCollider added at runtime; enable Prebake Collision Meshes or `Physics.BakeMesh` in a job |
| `InstantiateAsync` did not remove the hitch | the cost moved to a later frame; pool and prewarm behind a transition |

## References

- [references/profiler-workflow.md](references/profiler-workflow.md): read when connecting the Profiler to a player or Steam Deck, reading Timeline vs Hierarchy, looking up a built-in marker, recording call stacks, or comparing captures in Profile Analyzer.
- [references/hotspots.md](references/hotspots.md): read when a capture points at Update fan-out, allocations, component lookups, physics queries, Animator, uGUI rebuilds or logging, and you need the fix pattern.

## Related skills

- `optimize-3d-rendering`: GPU-bound and render-thread-bound frames, draw calls, SetPass calls.
- `memory-snapshot-profiling`: leaks and memory growth.
- `network-debugging`: bandwidth and latency in co-op sessions.
- `steam-deck-controller`: Deck input and Deck Verified checks.
- `csharp-unity` and `review-unity-csharp`: GC-free patterns and code review.
- `physics-3d-collision`, `animation-authoring`, `ui-ugui`, `optimize-text-mesh-pro`, `zlogger`: system-specific fixes.
