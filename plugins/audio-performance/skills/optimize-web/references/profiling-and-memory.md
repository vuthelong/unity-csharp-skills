# Web profiling and memory

Editor Play Mode does not represent browser runtime. Always measure a build in the browser, and test Chrome and Safari at minimum.

## Tools

| Tool | Use | Notes |
|---|---|---|
| Chrome DevTools > Performance | CPU flame graph, main-thread analysis | First stop for hitches |
| Chrome DevTools > Memory | Heap snapshots, allocation timeline | Compare before/after scene load |
| Chrome DevTools > Network | Download size, compression headers, cache hits | Confirm `Content-Encoding` |
| Firefox Profiler | Native + Wasm view, shareable URLs | Often better Wasm symbolication |
| Safari Web Inspector | macOS/iOS Safari | Required for Safari-only issues |
| Unity Profiler (Autoconnect Profiler, development build) | Unity markers: GC, rendering, scripts | Misses browser-side overhead |
| Spector.js | WebGL draw calls and state | No Frame Debugger on Web |

| Symptom | First | Second |
|---|---|---|
| Hitch / stutter | Chrome Performance | Firefox Profiler |
| Memory climbing | Chrome Memory | Unity Memory Profiler snapshot (see `memory-snapshot-profiling`) |
| Slow initial load | Chrome Network | Build Report |
| Safari-only issue | Safari Web Inspector | Compare in Chrome |

## Readable Wasm stacks

Browser profilers show mangled names by default. Options:

- Development builds: Player > Publishing Settings > Debug Symbols = External or Embedded.
- Function names only, minimal overhead: add [../scripts/WebProfilingBuildProcessor.cs](../scripts/WebProfilingBuildProcessor.cs) under `Assets/Editor/`. It adds `--compiler-flags=--profiling-funcs` to the IL2CPP arguments for WebGL development builds and removes it for release builds, preserving any other arguments.

## Emscripten profilers

Enable one at a time through `PlayerSettings.WebGL.emscriptenArgs`, development builds only:

| Flag | Shows |
|---|---|
| `--cpuprofiler` | CPU overlay |
| `--memoryprofiler` | Heap map (white allocated-unused, pink stack, blue dynamic, green fragmented) |
| `--threadprofiler` | Thread activity |

Clear the argument afterwards; it ships in the build otherwise.

## Firefox `about:memory`

Open `about:memory`, click **Measure**, find the tab: Wasm code, Wasm heap, `.data`, web audio. Wasm heap above ~300 MB risks crashes on older iOS.

## Memory directives

- Disable Read/Write on textures and meshes.
- Move assets out of `.data` into Addressables or AssetBundles.
- Use compressed texture formats (KTX2) to cut download and decoded size.
- Unload unused Addressables and call `Resources.UnloadUnusedAssets()` after level transitions.

## iOS Safari limits

- iOS < 18: WebContent process limit ~1.5 GB; Wasm memory capped at 2 GB; typed arrays share the pool. Older devices (iPhone X on iOS 16) fail heap growth around 512 MB, while an upfront 512 MB–1.5 GB Initial Memory Size succeeds.
- iOS 18+: limits largely lifted (iPhone 11 can allocate ~4 GB).
- Set Initial Memory Size to the target peak instead of relying on growth.
- Target a Wasm heap under ~200 MB for broad compatibility.
