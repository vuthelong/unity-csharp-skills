# Profiler workflow

Read when connecting the Profiler to a player or Steam Deck, reading Timeline vs Hierarchy, looking up a built-in marker, recording call stacks, or comparing captures in Profile Analyzer.

## Connect to a player

| Setting (File > Build Profiles) | Effect |
|---|---|
| Development Build | Required for a Profiler connection and for `ProfilerMarker.Begin/End` samples |
| Autoconnect Profiler | Bakes the Editor's IP into the player; the player connects on start |
| Deep Profiling Support | Instruments every script method from player start. Useful for startup time only; adds overhead to everything |

- Without Autoconnect: Window > Analysis > Profiler > target dropdown next to Record > pick the player, or `<Enter IP>` and type the device IP (optionally the port).
- Player Settings > Resolution and Presentation > **Run In Background** keeps the player running, and recording, when it loses focus.
- Profiler window toolbar: **Deep Profile** applies to Editor sessions; for players it needs Deep Profiling Support in the build.
- Save captures with the Profiler's save button (`.data`). Name them `<scenario>_<change>_<date>.data` so before/after pairs stay together.

## Steam Deck

- Check the symptom first with the Deck's own performance overlay (Quick Access menu > Performance). It shows frame rate and frame time without a development build.
- A Windows build usually runs on the Deck through Proton; a native Linux build needs the Linux Build Support module. Either way, build with Development Build and Autoconnect Profiler, put the Deck on the same LAN as the Editor PC and allow the Unity Editor through the PC firewall. If autoconnect fails, connect with `<Enter IP>` using the Deck's IP (Settings > Internet).
- Whether every Profiler feature behaves the same under Proton as on native Windows is not documented by Unity; check call stacks and GPU timings on your setup before relying on them.
- Copy development builds to the Deck through a private Steam branch (see `steam-build-release`) or Valve's devkit tooling for Steam Deck; check Valve's Steam Deck developer documentation for the current tool.
- Test at the frame limit players will use (the Deck lets players cap at 30 or 60 from the Quick Access menu), on battery, after 10 minutes of play. A frame that is fine on minute 1 and slow on minute 10 is a thermal or power budget problem.

## CPU Usage module views

| View | Use for |
|---|---|
| Timeline | All threads on one time axis. The only view that shows how the main thread, render thread and job workers line up. Turn on **Show Flow Events** (More menu) to see which sample scheduled or waited for jobs |
| Hierarchy | Merged call tree per thread, sortable by Total, Self, Calls, GC Alloc, Time ms, Self ms |
| Raw Hierarchy | Same as Hierarchy but every call stack listed separately (Calls is always 1) |
| Inverted Hierarchy | Grouped by marker with callers below; finds many small costs spread across the frame |

- Hierarchy views show one thread at a time (Thread dropdown). Time ms only counts the selected thread, so jobs and render-thread work are invisible there.
- **Details > Calls** lists callers and callees of the selected sample; **Related data** lists the `UnityEngine.Object`s attached to the sample.
- The **Warning** column flags known expensive calls (for example `Rigidbody.SetKinematic` recreating a non-convex MeshCollider, `Animation.AddClip` rebuilding state).
- Leave **Live** off while recording; it adds Editor overhead.

## Markers worth knowing

| Marker | Meaning |
|---|---|
| `PlayerLoop` | Everything the player does in a frame |
| `EditorLoop` | Editor overhead; only present when profiling Play mode |
| `BehaviourUpdate`, `FixedBehaviourUpdate`, `PreLateUpdate.ScriptRunBehaviourLateUpdate` | All `Update`, `FixedUpdate`, `LateUpdate` methods |
| `CoroutinesDelayedCalls` | Coroutines after their first yield |
| `WaitForTargetFPS` | Waiting for VSync or `Application.targetFrameRate`; idle time |
| `Gfx.WaitForPresentOnGfxThread` | Main thread waits for the render thread to present; check what the render thread does |
| `Gfx.PresentFrame` (render thread) | Waiting for the GPU and VSync: GPU bound if large |
| `Gfx.WaitForCommands` (render thread) | Render thread idle, waiting for the main thread: main-thread bound |
| `Gfx.WaitForRenderThread` | Main thread waits for the render thread to drain commands |
| `GC.Alloc` | Managed allocation |
| `GC.Collect` | Garbage collection; spreads over frames with incremental GC |
| `Mono.JIT` | First-call compilation, Mono backend only |
| `JobHandle.Complete`, `WaitForJobGroupID` | Main thread blocked on jobs (sync points) |
| `Physics.Simulate`, `Physics.FetchResults`, `Physics.ProcessReports` | Simulation, result collection, collision/trigger callbacks |
| `Physics.UpdateBodies` | Writing rigidbody poses back to Transforms |
| `Director.PrepareFrame`, `Animators.ProcessGraph`, `Animators.WriteTransforms` | Animator state machines, clip evaluation, transform writes |

Counters for scripts and overlays: `CPU Main Thread Frame Time`, `CPU Render Thread Frame Time`, `CPU Total Frame Time`, `GPU Frame Time` (available in release players when Frame Timing Stats is enabled), `GC Allocated In Frame` and `GC Allocation In Frame Count` (development players only), `GC Reserved Memory`.

## Deep profiling

- Deep Profile injects instrumentation into every script method. Expect the game to run several times slower and memory use to climb; large projects may not survive it.
- Use it on a 10-30 second repro of one feature, then convert what you learned into permanent `ProfilerMarker`s.
- Without Deep Profile you still see the first script call under each Unity callback and every instrumented Unity API call inside it (for example `Camera.main` shows as `FindMainCamera`).

## Call stacks for allocations

- In the Profiler toolbar, enable call stack recording for `GC.Alloc` (the same option exists for `UnsafeUtility.Malloc` and `JobHandle.Complete`).
- Select the `GC.Alloc` sample in Timeline; the tooltip shows the managed call stack. **Copy** puts it on the clipboard for a bug report.
- Call stack recording adds overhead; turn it off for timing captures.

## Profile Analyzer (before/after)

Package `com.unity.performance.profile-analyzer`, 1.4.x for Unity 6000.0. Window > Analysis > Profile Analyzer.

1. Load or record the "before" capture in the Profiler, open Profile Analyzer, **Compare** view, **Pull Data** into the left set.
2. Load the "after" capture in the Profiler and pull it into the right set.
3. Select the same frame range in both (drag in the frame control).
4. Filter Thread = Main Thread; set the marker columns to **Time and count** or **GC Allocations**.
5. Read the Median columns and the diff; report median frame time and the top changed markers.
6. **Save** writes `.pdata`. It does not contain the raw Profiler data, so keep the `.data` files next to it. **Export** writes CSV for spreadsheets or CI.

## ProfilerRecorder notes

- `ProfilerRecorder.StartNew(category, name, capacity)` starts recording; `Dispose` it when done (it owns native memory). `Valid` is false if the name was not found.
- `LastValue` is the value for the last completed frame; `CurrentValue` is the live value; `GetSample(i)` and `CopyTo` read the history up to `Capacity`.
- Default options sum all samples in a frame and wrap around when capacity is reached.
- Marker times are in nanoseconds. `FrameTimingManager` results arrive with a four-frame delay and report milliseconds.
