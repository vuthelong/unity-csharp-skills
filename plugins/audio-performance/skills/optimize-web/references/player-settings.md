# Web Player Settings

## Pre-flight snippet

One `eval` call. Remove the `wasm2023` line on 6000.0.

```csharp
var target = UnityEditor.Build.NamedBuildTarget.WebGL;
var w = new System.Collections.Generic.List<string>();
w.Add($"activeBuildTarget={UnityEditor.EditorUserBuildSettings.activeBuildTarget}");
w.Add($"compressionFormat={UnityEditor.PlayerSettings.WebGL.compressionFormat}");
w.Add($"decompressionFallback={UnityEditor.PlayerSettings.WebGL.decompressionFallback}");
w.Add($"stripEngineCode={UnityEditor.PlayerSettings.stripEngineCode}");
w.Add($"stripUnusedMeshComponents={UnityEditor.PlayerSettings.stripUnusedMeshComponents}");
w.Add($"managedStrippingLevel={UnityEditor.PlayerSettings.GetManagedStrippingLevel(target)}");
w.Add($"il2cppCodeGeneration={UnityEditor.PlayerSettings.GetIl2CppCodeGeneration(target)}");
w.Add($"apiCompatibilityLevel={UnityEditor.PlayerSettings.GetApiCompatibilityLevel(target)}");
w.Add($"exceptionSupport={UnityEditor.PlayerSettings.WebGL.exceptionSupport}");
w.Add($"debugSymbolMode={UnityEditor.PlayerSettings.WebGL.debugSymbolMode}");
w.Add($"dataCaching={UnityEditor.PlayerSettings.WebGL.dataCaching}");
w.Add($"wasm2023={UnityEditor.PlayerSettings.WebGL.wasm2023}");
w.Add($"initialMemorySize={UnityEditor.PlayerSettings.WebGL.initialMemorySize}");
w.Add($"maximumMemorySize={UnityEditor.PlayerSettings.WebGL.maximumMemorySize}");
w.Add($"memoryGrowthMode={UnityEditor.PlayerSettings.WebGL.memoryGrowthMode}");
w.Add($"graphicsAPIs={string.Join(",", UnityEditor.PlayerSettings.GetGraphicsAPIs(UnityEditor.BuildTarget.WebGL))}");
w.Add($"targetFrameRate={UnityEngine.Application.targetFrameRate}");
w.Add($"vSyncCount={UnityEngine.QualitySettings.vSyncCount}");
return string.Join("\n", w);
```

Separate call (needs the Web build-support module):

```csharp
return $"codeOptimization={UnityEditor.WebGL.UserBuildSettings.codeOptimization}";
```

## Release recommendations

| Setting | Release | Notes |
|---|---|---|
| Compression Format | Brotli (HTTPS) / Gzip (HTTP) | |
| Decompression Fallback | Off | On only if the host cannot send `Content-Encoding` |
| Strip Engine Code | On | |
| Managed Stripping Level | High (release), Medium (dev) | Preserve reflected types with `link.xml` |
| Code Optimization | Disk Size with LTO (release), Build Times (dev) | Per machine, in `Library/` |
| IL2CPP Code Generation | Optimize Size | Smaller Wasm, slight runtime cost |
| WebAssembly 2023 | On when the browser baseline allows | 6000.1+ |
| Enable Exceptions | None; Explicitly Thrown Only if `try/catch` is required | |
| API Compatibility Level | .NET Standard 2.1 | Smaller than .NET Framework |
| Debug Symbols | Off | Development builds only |
| Data Caching | On | IndexedDB cache for repeat loads |
| Strip Unused Mesh Components | On | |
| Initial Memory Size | Measured peak estimate | Growth copies the heap |
| Memory Growth Mode | Geometric | |
| Maximum Memory Size | 2048 MB default; up to 4096 for heavy 3D | Older Firefox and Chrome < 119 struggle above 2048 |
| vSyncCount | 0 | |
| targetFrameRate | -1 | |

## Compression and server

| Compression | Use when | Notes |
|---|---|---|
| Brotli | HTTPS or localhost | Best ratio; secure contexts only |
| Gzip | HTTP, legacy CDNs | Universal |
| None | Local `file://` testing | Never ship |

Server must:

- Serve `.br` with `Content-Encoding: br` and `.gz` with `Content-Encoding: gzip`.
- Send `Content-Type: application/wasm` for `.wasm` (required for streaming instantiation) and `application/javascript` for `.js`.
- Use HTTP/2 or HTTP/3.
- Send long cache lifetimes for content-hashed files (enable Name Files As Hashes in Publishing Settings).

## Exception support

| Mode | Size | Use |
|---|---|---|
| None | Smallest | Release where uncaught exceptions abort |
| Explicitly Thrown Only | Moderate | Projects that catch exceptions |
| Full (with or without stack trace) | Largest, slowest | Debugging only |

With Wasm 2023 enabled, native Wasm exception handling replaces the JavaScript-based model and is smaller and faster.

## Shader stripping

Edit > Project Settings > Graphics:

| Setting | Value |
|---|---|
| Lightmap Modes | Automatic |
| Fog Modes | Automatic |
| Instancing Variants | Strip Unused |
| Batch Renderer Group Variants | Strip All when BRG / GPU Resident Drawer is not used |
| Always Included Shaders | Remove anything unreferenced |

For URP, also turn off unused features on the URP Asset and Renderer (they gate variants). Verify after stripping that no referenced shader went pink.

## WebGPU

Unity 6 ships WebGPU as an opt-in graphics API: Player > Other Settings > disable Auto Graphics API, add WebGPU above WebGL2. Keep WebGL2 in the list as fallback for browsers without WebGPU. Compute shaders and VFX Graph GPU features need WebGPU; WebGL2 has no compute.
