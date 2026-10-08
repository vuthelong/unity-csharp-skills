# Rendering diagnostics workflow

Read before changing any setting. Measure on the target device in a Development Build; Editor numbers include editor overhead and a different GPU.

## 1. CPU-bound or GPU-bound?

- **Profiler > CPU Usage**: look at the main thread and render thread. If `Gfx.WaitForPresentOnGfxThread` / `Gfx.WaitForGfxCommandsFromMainThread` / `WaitForTargetFPS` dominate, read them:
  - Main thread waiting on `Gfx.WaitForPresent...`: GPU-bound.
  - Render thread busy with `Gfx.*` / `Batch*` / `DrawBuffers`: CPU render submission bound (draw calls, SetPass calls, culling).
  - `Culling` / `SceneCulling` markers large: too many renderers, too many cameras, or occlusion culling cost.
- **Profiler Highlights module** (Unity 6): summarizes CPU vs GPU bound per frame. Verify availability in your editor version.
- **FrameTimingManager** (`FrameTimingManager.CaptureFrameTimings()` / `GetLatestTimings`): CPU main, CPU render and GPU frame times in builds. Requires Player Settings > Frame Timing Stats on. The Rendering Debugger "Display Stats" panel shows the same data.
- Change resolution or render scale: if frame time drops proportionally, you are fill-rate / pixel bound (post-processing, overdraw, shadows sampling, MSAA, HDR).

## 2. Rendering Statistics (Game view Stats)

| Field | Meaning |
|---|---|
| Batches | Draw calls after batching. With the SRP Batcher, batches are cheap; focus on SetPass calls. |
| Saved by batching | Static / dynamic / instancing merges |
| SetPass calls | Shader pass / material state changes. The main CPU cost indicator in URP. |
| Tris / Verts | Includes shadow passes and every camera |
| Shadow casters | Renderers drawn into shadow maps |

The Stats overlay is an Editor estimate. Use the Profiler **Rendering** module for per-frame counters in a player (Batches, SetPass, triangles, used textures, render textures, buffers).

## 3. Frame Debugger (Window > Analysis > Frame Debugger)

- Steps through every draw in the frame, grouped by pass (shadows, depth prepass, opaques, skybox, transparents, post).
- Selecting a draw shows the shader, keywords, properties and **why it was not batched with the previous one** (different material, different shader variant, SRP Batcher incompatible node, MaterialPropertyBlock, lightmap index, etc.).
- `SRP Batch` entries list how many draws share one setup.
- Connect to a Development Build player via the target dropdown to debug on device.
- Look for: duplicated shadow passes for tiny objects, unexpected depth/opaque copies, extra cameras, post-processing passes you did not intend.

## 4. Rendering Debugger (Window > Analysis > Rendering Debugger)

Available in Editor and in Development Builds (default shortcut: Ctrl+Backspace, or a three-finger double tap on touch devices).

- **Display Stats**: frame timing, CPU/GPU, per-pass timings (where supported).
- **Material**: material and vertex attribute views; validation modes for albedo and metallic ranges.
- **Lighting**: shadow cascades visualization, light complexity / Forward+ light tiles, lighting-only views.
- **Rendering**: Map overlays (Overdraw, Depth, Motion vectors, Wireframe), MSAA/HDR/post toggles to A/B cost on device, mipmap streaming visualization.
- **GPU Resident Drawer** panel: instance counts, culling and occlusion stats, and an occlusion test overlay.

Panel names differ slightly between URP versions; verify in your editor version.

## 5. Profiler GPU Usage module

- Add the GPU Usage module in the Profiler. It reports per-category GPU time (opaque, transparent, shadows, post, other).
- Not supported on every graphics API / platform, and it disables Graphics Jobs on some platforms while active. Unsupported combos show "GPU profiling is not supported". Verify for your platform and editor version.
- For precise GPU timing use the platform tool: Xcode GPU Frame Capture (Metal), Android GPU Inspector / Arm Performance Studio / Snapdragon Profiler (Vulkan, GLES), PIX or RenderDoc (desktop). RenderDoc can be launched from the Game view context menu once loaded.

## 6. Render Graph Viewer (Window > Analysis > Render Graph Viewer)

- Unity 6 URP executes through Render Graph. The viewer shows every pass, the resources it reads and writes, culled passes, and **merged native render passes**.
- On tile-based mobile GPUs, passes merged into one native render pass keep attachments in tile memory. A break in the merge (an opaque or depth texture copy, a custom pass reading the camera color as a texture, a resolution change) forces a store/load to system memory. Look for these breaks first on mobile.
- Hover a resource to see load/store actions and whether it is memoryless.
- Custom renderer features: check them with `validate-urp-render-graph-renderer-feature`.
- Render Graph Compatibility Mode (render graph off) is deprecated in Unity 6 and removed or hidden in later 6000.x minors; verify in your editor version before relying on it.

## 7. Memory

GPU memory (textures, meshes, render targets, shader variants) is a rendering cost too. Capture with the Memory Profiler; see `memory-snapshot-profiling`.

## Measurement hygiene

- Fix the scene and camera path (record a fly-through or use a fixed benchmark viewpoint).
- Disable VSync and `Application.targetFrameRate` caps when measuring headroom; re-enable for thermal tests.
- On mobile, test after 10+ minutes for thermal throttling. A frame time that is fine at minute 1 and doubles at minute 10 is a GPU or bandwidth budget problem.
- Change one setting at a time and record the before/after frame times.
