---
name: optimize-web
description: Audits and optimizes Unity 6 WebGL and WebGPU builds for download size, startup time, browser memory, and runtime smoothness. Use when a web build is too large or slow to load, needs Brotli/Gzip and server Content-Encoding headers, Decompression Fallback, Strip Engine Code, Managed Stripping, Disk Size with LTO, Wasm 2023, exception support, Initial/Maximum Memory Size, Data Caching, shader variant stripping, the Web Stripping Tool, KTX2/Basis textures, Addressables on a CDN, targetFrameRate in the browser, iOS Safari memory crashes, or browser profiling with Chrome DevTools, Firefox Profiler, or Safari Web Inspector.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: audio-performance
  sources: "Unity-Technologies/skills/skills/optimize-web"
  unity: "6000.0+"
---

# Optimize Web Builds

Be thorough; quality over speed. Measure in a browser, never in Editor Play Mode.

## Execution path

Player Settings reads and writes run in a live Editor. Follow `unity-cli` to connect, and confirm `eval` is in the command catalog (from the `com.unity.pipeline` package). If it is missing, say so and stop the write steps.

- Read-only fallback: `ProjectSettings/ProjectSettings.asset` can be read, but never written. Its serialized names differ from the API, several values are per build target, and a hand edit silently disagrees with what the build uses.
- `eval` takes a statement block: no `using` (CS0210), fully qualified types (CS0246), `UnityEngine.Object` spelled out (CS0104).

### API names that trip people up

| Setting | Correct form | Does not exist |
|---|---|---|
| Managed stripping | `PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL)` | `PlayerSettings.managedStrippingLevel` |
| Wasm code optimization | `UnityEditor.WebGL.UserBuildSettings.codeOptimization` | `PlayerSettings.WebGL.codeOptimization`, `.optimizationLevel` |
| IL2CPP code generation | `PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL)` | a bare property |
| Wasm 2023 | `PlayerSettings.WebGL.wasm2023` | absent on 6000.0; available from 6000.1 |

`UserBuildSettings` lives in the Web build-support module; read it in its own `eval` and treat a resolution failure as "module not installed". It persists to `Library/EditorUserBuildSettings.asset` (gitignored), so it is per machine: teammates and CI do not inherit it. Apply it in the CI build script, and never look for it in `ProjectSettings.asset`.

`BuildTarget.WebGL` / `NamedBuildTarget.WebGL` is the target for both WebGL2 and WebGPU builds.

## 0. Pre-flight

Run the pre-flight snippet in [references/player-settings.md](references/player-settings.md). Confirm:

1. Active build target is `WebGL`; warn if not.
2. Compression format, decompression fallback, Strip Engine Code, managed stripping level.
3. Exception support and code optimization (separate call).
4. `Application.targetFrameRate`, `QualitySettings.vSyncCount`.
5. Data caching, debug symbols, initial/maximum memory size, growth mode, API compatibility level.
6. Graphics APIs: WebGL2 only, or WebGPU enabled.

## 1. Assess

1. After a build, have the user open the Build Report (Unity 6: **Window > General > Build Report** or the Build Report Inspector package) and list the largest code and asset contributors.
2. Ask how the build is hosted: does the server send `Content-Encoding: br` / `gzip` and `Content-Type: application/wasm`? HTTPS or HTTP?
3. Confirm `targetFrameRate` is `-1` and memory settings are sane.
4. Report findings before recommending anything.

## 2. Route the request

| User says | Default direction |
|---|---|
| "build too large", "download slow" | Strip Engine Code, Managed Stripping High, Disk Size with LTO, Brotli, Optimize Size |
| "slow startup", "Decompression Fallback" | Fallback off; fix server `Content-Encoding` |
| "stutter in Chrome/Safari" | Browser profiler first; Safari caps at 60 fps |
| "battery drain" | `targetFrameRate = -1`; `OnDemandRendering.renderFrameInterval` on idle screens |
| "exceptions too large" | None for release; Wasm 2023 exceptions if browser baseline allows |
| "CDN", "streaming content" | Addressables remote groups with Brotli/Gzip on the CDN |
| "memory growth slow", "iOS Safari crash" | Initial Memory Size near peak, Geometric growth; see iOS limits |
| "KTX", "Basis", "unknown GPU" | KTX2: ETC1S for size, UASTC for quality |
| "strip unused code/packages" | Web Stripping Tool, remove packages, shader stripping |
| "shader variants" | Graphics settings stripping, audit Always Included Shaders |
| "audio on web" | Mono, Vorbis, no mixer effects; see `optimize-audio` |
| "can't read Wasm stacks" | Profiling symbols in development builds |

## 3. Optimize

### One-click release settings

Offer [scripts/WebOptimizer.cs](scripts/WebOptimizer.cs) for every new web project. It is a project file with a `[MenuItem]`, not `eval` input: save it under `Assets/Editor/`, let Unity compile, then run

```csharp
UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Apply Web Release Settings");
```

Adapt it first: keep `ExplicitlyThrownExceptionsOnly` if the project relies on `try/catch`; switch to Gzip for HTTP hosting. For a single one-off change, an inline `eval` is fine.

### Save, then verify

Player Settings are in-memory objects until saved. A read-back in the same session returns whatever you just assigned whether or not it reached disk, so a run that skips the save reports success and loses everything when the Editor closes.

After every write:

1. `UnityEditor.AssetDatabase.SaveAssets()` (the script does this; inline writes must too).
2. Read the values back and report them, not "applied successfully".
3. Name the build target for every per-target value.

Do not "prove" the save is unnecessary in batch mode: `-quit` saves settings on exit, so both a saving and a non-saving script pass there. The defect only shows in a live Editor.

### Settings, compression, stripping, textures

Read [references/player-settings.md](references/player-settings.md) for the full release table, compression and server rules, exception modes, and shader stripping. Read [references/assets-and-delivery.md](references/assets-and-delivery.md) for package removal, the Web Stripping Tool, quality levels, KTX2, Addressables streaming, video, and audio.

### Frame pacing

- `Application.targetFrameRate = -1` and `QualitySettings.vSyncCount = 0`: the browser drives frames through `requestAnimationFrame`. A fixed 60 conflicts with it and causes uneven pacing.
- Safari caps WebGL at 60 fps.
- On idle or menu screens set `UnityEngine.Rendering.OnDemandRendering.renderFrameInterval` to 6–12 to save battery; reset to 1 on input.

### Profiling and memory

Read [references/profiling-and-memory.md](references/profiling-and-memory.md) when diagnosing stutter, memory growth, Safari/iOS crashes, or unreadable Wasm stacks. It covers browser tools, [scripts/WebProfilingBuildProcessor.cs](scripts/WebProfilingBuildProcessor.cs), Emscripten profilers, `about:memory`, and iOS limits.

## 4. Validate

1. Re-run the pre-flight snippet; report values per target.
2. Rebuild and compare Build Report sizes and the compressed `.wasm` / `.data` sizes against the baseline.
3. Test in Chrome and Safari at minimum (GC and JIT differ); Firefox for cache limits.
4. Stop after 3 iterations and ask for feedback.

## 5. Troubleshooting

**Still large after Strip Engine Code.** Managed Stripping below High; reflection-heavy plug-ins need a `link.xml` rather than a lower level; Exceptions set to Full; unused packages (Input System, Physics 2D, AI Navigation) still installed.

**Brotli not working.** Server not sending `Content-Encoding: br`, or hosted over plain HTTP (browsers only accept Brotli in secure contexts). Fix headers or use Gzip. Fallback On is a last resort: it adds a JS decompressor and slows startup.

**Stutter in Safari only.** Fixed `targetFrameRate`; shader constructs Safari's WebGL handles differently; memory pressure near the iOS limit.

**Memory growth slow path.** Each Wasm heap growth copies the buffer. Raise Initial Memory Size to the measured peak; use Geometric growth.

**Firefox not caching.** Cache entries above `browser.cache.disk.max_entry_size` (~50 MB) are skipped. Split content into Addressables bundles under ~50 MB.

**Local testing.** Serve over HTTP with correct MIME types (`python -m http.server 8000 -d <build>` or `npx serve <build>`). For Brotli builds use HTTPS (self-signed cert) or `localhost`.

## 6. Complete

- Report download size delta, every setting changed with values, and server configuration status.
- List follow-ups: CDN for Addressables remote groups, Safari device testing, Wasm 2023 once the browser baseline allows, `codeOptimization` in CI.

## See also

- `optimize-audio` — audio import settings; Web plays audio through the browser and skips mixer effects.
- `memory-snapshot-profiling` — Unity-side memory snapshots from a development web build.
- `unity-cli` — Editor connection and `eval`.
- Addressables package docs for remote groups; Graphics settings for shader stripping.
