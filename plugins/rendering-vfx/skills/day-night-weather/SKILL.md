---
name: day-night-weather
description: Builds a dynamic day-night cycle and weather system for Unity 6 URP - a normalized time-of-day clock with day length, time scale and sunrise/sunset/new-day events, sun and moon directional lights driven by Gradient and AnimationCurve, Trilight or skybox ambient with throttled DynamicGI.UpdateEnvironment, procedural skybox properties, fog, night Volume weight, a clear/overcast/rain/storm/snow state machine with timed blends, wetness and snow cover via Shader.SetGlobalFloat, and host-owned time and weather for Netcode for GameObjects co-op synced through NetworkVariable anchors and NetworkManager.ServerTime. Use for "add a day/night cycle", "make it rain", sun rotation, nights too dark, skybox ambient not updating, wet surfaces, sleep-until-morning, or clients seeing a different time of day. Not for static lighting and baking (unity-lighting), Volume overrides (urp-postprocessing), authoring rain or snow effects (unity-vfx-graph, unity-particle-system) or general NGO setup (netcode-for-gameobjects).
license: MIT
metadata:
  category: rendering-vfx
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RenderSettings.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/DynamicGI.UpdateEnvironment.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Light.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Shader.SetGlobalFloat.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/VFX.VisualEffect.SetFloat.html, https://docs.unity3d.com/6000.0/Documentation/Manual/shader-skybox-procedural.html, https://docs.unity3d.com/6000.0/Documentation/Manual/urp/probevolumes-bakedifferentlightingsetups.html, https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.0/api/UnityEngine.Rendering.Volume.html, https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Property-Types.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/api/Unity.Netcode.NetworkManager.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/api/Unity.Netcode.NetworkTime.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/advanced-topics/networktime-ticks.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/basics/networkvariable.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/advanced-topics/serialization/inetworkserializable.html"
  unity: "6000.0+"
---

# Day-Night Cycle and Weather (URP)

The clock is a pure function of a time source: `totalDays = anchor.totalDays + (clockNow - anchor.clockTime) * daysPerSecond`. Every visual reads the clock; nothing integrates `deltaTime`. That one rule makes pausing, skipping to morning, saving and co-op sync trivial.

## Workflow

1. **Decide the GI strategy first** (see `unity-lighting`). A moving sun needs realtime direct light. Baked lightmaps and baked probes freeze one time of day; read [references/gi-and-cost.md](references/gi-and-cost.md) before baking anything in an outdoor scene.
2. **Copy the scripts** (no dependencies beyond URP; the network script needs NGO):
   - [scripts/TimeOfDay.cs](scripts/TimeOfDay.cs): read when changing day length, time scale, skipping time or hooking dawn/dusk events. `TimeAnchor` clock model, `NewDay`, `Sunrise`, `Sunset`, `AnchorChanged`.
   - [scripts/SunMoonDriver.cs](scripts/SunMoonDriver.cs): read when tuning light color/intensity, ambient, skybox, fog or night grading. Drives two directional lights, `RenderSettings`, a runtime skybox material copy and a night `Volume.weight`.
   - [scripts/WeatherController.cs](scripts/WeatherController.cs): read when adding weather types or surface effects. Weighted state machine, timed blends, global shader floats, optional Particle System rain and snow.
   - [scripts/NetworkEnvironmentSync.cs](scripts/NetworkEnvironmentSync.cs): read when the game is online co-op. Host-owned anchors in `NetworkVariable`s, clock switched to `NetworkManager.ServerTime`.
3. **Scene setup.** One environment GameObject with `TimeOfDay`, `WeatherController`, `SunMoonDriver` (and `NetworkEnvironmentSync` + `NetworkObject` for co-op, as an in-scene placed object). Two Directional Lights (sun with soft shadows, moon with shadows off or low). Fog enabled with mode Exponential, because the driver writes `fogDensity`.
4. **Author curves against normalized time** (0 = midnight, 0.25 = default sunrise, 0.5 = noon, 0.75 = default sunset). Gradients hold at most 8 color keys; keep dawn and dusk keys close together for fast color shifts.
5. **Ambient.** Default `TrilightGradients` sets sky/equator/ground colors every frame at no GI cost. `SkyboxThrottled` uses the skybox for ambient and calls `DynamicGI.UpdateEnvironment()` at most every few seconds; see the cost rules below.
6. **Night look.** Put night grading (lower exposure, cooler white balance, less saturation) on a separate global `Volume` and let the driver set its `weight` from a curve. Override details live in `urp-postprocessing`.
7. **Weather.** Fill one `WeatherPreset` per type with its target `WeatherSample` and weighted `next` transitions. Drive rain/snow either through the optional Particle System slots or a VFX Graph binder (snippet in [references/weather-and-coop.md](references/weather-and-coop.md)).
8. **Save.** Store `TotalDays`, `TimeScale` and `WeatherController.CaptureSave()` in the host's world save (`save-system`). Never store raw clock times; they are session-relative.
9. **Co-op.** Read [references/weather-and-coop.md](references/weather-and-coop.md).

## Core usage

```csharp
public sealed class SleepSystem : MonoBehaviour
{
    [SerializeField] private TimeOfDay timeOfDay;

    private void OnEnable() => this.timeOfDay.NewDay += this.OnNewDay;

    private void OnDisable() => this.timeOfDay.NewDay -= this.OnNewDay;

    public void Sleep() => this.timeOfDay.SetTimeOfDay(this.timeOfDay.Sunrise01 + 0.01f, true);

    private void OnNewDay(int day) => Debug.Log($"Day {day}");
}
```

`SetTimeOfDay`, `SetTotalDays` and `SetTimeScale` re-anchor the clock and raise `AnchorChanged`; only the host calls them in co-op. Skipping several phases in one frame raises one `NewDay` plus the event for the phase you land in, not every phase in between.

## Rules

- **Clock source.** Default is `Time.timeAsDouble` (scaled game time, so `Time.timeScale = 0` pauses the sky in single player). In co-op the sync script switches every peer to `NetworkManager.ServerTime.Time`. Keep `TimeAnchor` in `double`; a `float` clock loses sub-second precision after a few hours of uptime.
- **Sun direction.** `transform.forward` of a directional light is the light direction. The driver maps sunrise to pitch 0, noon to 90 and sunset to 180, then applies `azimuth` and `orbitTilt`. Moon = sun + 180.
- **One shadowed directional light.** The driver enables whichever of sun/moon is above the horizon and assigns it to `RenderSettings.sun` (the light the procedural skybox uses). URP treats a single directional light as the main light; how it chooses one when several are enabled is not documented on the pages this skill used, so keep exactly one enabled.
- **Skybox material.** The driver instantiates `skyboxTemplate` and assigns the copy to `RenderSettings.skybox`, so Play Mode never edits the asset. It writes `_Exposure`, `_AtmosphereThickness` and `_SkyTint` only when `Material.HasProperty` confirms them. Those are the built-in Procedural skybox property names as commonly found in its shader source; confirm them in the material's Debug Inspector, and adjust ids for a custom sky shader.
- **`DynamicGI.UpdateEnvironment()` cost.** The 6000.0 docs say it schedules a cubemap update; with `SystemInfo.supportsAsyncGPUReadback` it reads back asynchronously and may lag one or more frames, extra requests beyond what it can process are ignored, and without async readback it stalls the calling thread. Never call it per frame. Trilight ambient avoids it entirely.
- **Reflections.** Environment reflections come from the skybox capture; changing the sky does not update baked reflection probes. See [references/gi-and-cost.md](references/gi-and-cost.md).
- **Global shader properties.** `Shader.SetGlobalFloat` only reaches shaders whose material does not define the property. In Shader Graph set the property's Scope to Global; in HLSL declare it outside `UnityPerMaterial`. Write globals only when the value changes (the controller does).
- **No per-frame allocations.** Cache `Shader.PropertyToID` ids in static readonly fields; `Gradient.Evaluate` and `AnimationCurve.Evaluate` do not allocate.
- **Volume weight.** `Volume.weight` is 0-1 on the component. Changing `sharedProfile` edits the asset; use `weight` for blending and `profile` only for per-instance overrides.

## Pitfalls

| Symptom | Fix |
|---|---|
| Sky changes but objects keep daytime ambient | Skybox ambient needs `DynamicGI.UpdateEnvironment()`; use `SkyboxThrottled` or switch to Trilight |
| Frame hitch every few seconds | Environment update without async readback; raise the interval or use Trilight |
| Night is pitch black | Moon intensity curve, ambient night colors and exposure in the night Volume; never rely on baked GI at night |
| Interiors glow at night | Baked lightmaps or probes baked at noon; see APV lighting scenarios in the reference |
| Two sets of shadows at dusk | Both lights enabled; keep the driver in charge of `enabled` |
| Wet shader never changes | Property defined on the material or Shader Graph Scope not Global |
| Skybox asset modified after Play Mode | Some script set properties on the asset itself; only write to the runtime copy the driver creates |
| Clients see a different time | A client wrote the clock or integrates `deltaTime`; only the host re-anchors |
| Time jumps after loading a save | Saved a clock time instead of `TotalDays` |

## References

- [references/gi-and-cost.md](references/gi-and-cost.md): read when choosing ambient mode, using baked lighting, light or reflection probes, APV lighting scenarios, or budgeting shadow and GI cost.
- [references/weather-and-coop.md](references/weather-and-coop.md): read when adding weather types, rain/snow effects, wetness shaders, saving, or syncing time and weather in NGO co-op.

## Related skills

- `unity-lighting`: Light components, shadows, probes, baking.
- `urp-postprocessing`: night and storm grading overrides on the Volume.
- `unity-vfx-graph`, `unity-particle-system`: authoring the precipitation effects.
- `netcode-for-gameobjects`: NetworkObject spawning, NetworkVariable rules.
- `save-system`: host-owned world save that stores time and weather.
- `optimize-3d-rendering`: shadow distance, cascades and quality tiers.
- `audio-playback-system`: rain and thunder ambience driven from `WeatherController.Current`.
