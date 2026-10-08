# Web assets and delivery

## Remove unused resources

1. **Packages.** Audit `Packages/manifest.json` and Package Manager **In Project** and **Built-in** views. Remove what is unused; Input System, Physics, AI, and Timeline modules all cost Wasm size when referenced.
2. **Shaders.** See shader stripping in [player-settings.md](player-settings.md).
3. **Web Stripping Tool** (`com.unity.web.stripping-tool`). Profiles the Wasm binary, shows which engine submodules are used, and strips unused ones (for example 3D physics in a 2D game). Goes beyond Managed Stripping Level. Re-test every stripped feature path.

## Quality settings

- Use Low or Very Low as the Web default in Edit > Project Settings > Quality (set the Web column's default in the Levels matrix).
- `QualitySettings.SetQualityLevel` only changes the current level for the running session; to switch at startup on Web, call it under `#if UNITY_WEBGL && !UNITY_EDITOR`.
- Make a Web-specific level that disables real-time shadows, heavy post-processing, and high particle counts.

## KTX2 / Basis Universal

One file transcodes at load to the best GPU format (BC7 desktop, ASTC mobile, ETC2 older Android).

| Topic | Guidance |
|---|---|
| Package | `com.unity.cloud.ktx` (KTX for Unity) |
| When | Runtime-loaded textures (Addressables, bundles, downloads) for unknown GPUs |
| When not | Textures built into the player; Unity already picks formats at build time |
| Supercompression | ETC1S for size (albedo); UASTC for quality (normals, UI) |
| Encoding | Offline with `toktx` or `basisu`; never at runtime |
| Linear data | `--assign_oetf linear` for normals, masks, data maps |
| Mips | Generate at encode time (`--genmipmap`) |
| Orientation | Always `--lower_left_maps_to_s0t0` |
| Loading | `KtxTexture.LoadFromStreamingAssets` or `LoadFromBytes` after `UnityWebRequest` |
| Memory | GPU cost equals the transcoded format, not the KTX2 file size |

Commands: [../scripts/toktx-examples.sh](../scripts/toktx-examples.sh).

## Streaming content

- Addressables remote groups on a CDN with Brotli or Gzip.
- Keep the initial download under ~30 MB for instant play; stream levels after.
- Keep bundles under ~50 MB for Firefox caching.
- Prefer KTX2 bundles over per-GPU variants for streamed textures.
- Disable Read/Write on textures and meshes; it keeps a CPU copy in the Wasm heap.

## Video

Plays only from a URL (CORS-enabled server) or StreamingAssets, not from an imported VideoClip. iOS needs HTTP range request support. Use MP4/H.264.

## Audio

- Browsers play audio through the Web Audio API. AudioMixer groups work for volume, but mixer effects and most DSP filters are not processed. Test mixer-dependent behavior in a browser build.
- Force To Mono where stereo adds nothing; use Vorbis. If Firefox `about:memory` shows web audio above ~100 MB, clips are likely decompressed or stereo.
- Browsers block audio until a user gesture; start music after the first click or tap.
- See `optimize-audio` for import-setting recipes.

## Canvas and DPI

A canvas scaled by CSS renders at the new resolution. Use `devicePixelRatio` in the web template (or `config.devicePixelRatio`) to cap it on high-DPI phones instead of rendering at 3x.
