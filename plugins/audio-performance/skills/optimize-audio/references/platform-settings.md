# Audio platform settings

## Compression format matrix

| Platform | Recommended | Notes |
|---|---|---|
| PC / Mac / Linux | Vorbis, quality 0.5–0.7 | 0.7–0.85 for dialogue; default 0.5 is often audible on voice |
| Android | Vorbis | Software decode; ADPCM for very short, frequently played SFX |
| iOS | Vorbis or MP3 | `AudioCompressionFormat.AAC` exists in the enum but is not offered by the iOS importer; read the override back after setting it |
| Xbox | XMA | Platform override |
| PlayStation | ATRAC9 | Platform override |
| Web | Vorbis | Browser decodes; the Web importer exposes a reduced format list, so read the override back |

ADPCM: ~3.5x smaller than PCM, very cheap to decode, adds noise. Good for footsteps, impacts, UI clicks. Bad for music or anything tonal.
PCM: no decode cost, largest size. Only for tiny clips where latency matters.

## Sample rate

| Use case | Rate |
|---|---|
| Desktop/console music and voice | 44100 or 48000 Hz (match `AudioSettings.outputSampleRate`) |
| Desktop/console SFX | 44100 Hz |
| Mobile/Web SFX and UI blips | 22050 Hz |
| Mobile dialogue | 22050 or 44100 Hz |

Decoded PCM bytes = seconds × sampleRate × channels × 2. Halving the rate or forcing mono halves it.

## Load Type decision table

| Load Type | Behavior | Use for |
|---|---|---|
| Decompress On Load | Decoded to PCM at load; no per-play decode CPU | Short, frequent SFX (decoded size under ~200 KB) |
| Compressed In Memory | Kept compressed; decoded while playing | Medium clips played occasionally; most SFX on memory-tight targets |
| Streaming | Decoded from disk on the fly; ~200 KB buffer per playing voice | Music, long ambience, VO |

Mismatch flags:

- Decompress On Load on a clip over ~1 MB or longer than ~5 s: memory bloat (Vorbis decoded to PCM is ~10x).
- Streaming on a clip with many simultaneous voices: one stream and I/O cost per voice.
- Streaming or large Compressed In Memory without `loadInBackground`: main-thread stall on first load.
- `preloadAudioData` on rarely used clips: lengthens scene load. In Unity 6 set it on `AudioImporterSampleSettings`.

## DSP buffer size

Project Settings > Audio > DSP Buffer Size, or at runtime through `AudioSettings.GetConfiguration()` / `AudioSettings.Reset(config)` (resets all playing audio).

| Setting | Buffer | Use |
|---|---|---|
| Best Latency | 256 | Rhythm games, live synthesis |
| Good Latency | 512 | General gameplay |
| Best Performance | 1024 | Ambient, cinematic, battery-saving mobile |

64 or 128 samples raises per-tick overhead and risks crackle on mobile. Recommend Good Latency or Best Performance unless latency is a gameplay requirement.
