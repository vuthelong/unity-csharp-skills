# GI, probes and cost with a moving sun

Read when choosing ambient mode, using baked lighting, light or reflection probes, APV lighting scenarios, or budgeting shadow and GI cost.

## What follows the time of day and what does not

| Lighting term | Follows the driver? | Notes |
|---|---|---|
| Sun/moon direct light and shadows (Realtime) | Yes | The only term that is fully dynamic |
| Trilight / Flat ambient (`RenderSettings.ambientSkyColor`, `ambientEquatorColor`, `ambientGroundColor`, `ambientLight`) | Yes, every frame | Cheap; no occlusion, so interiors stay as bright as outdoors |
| Skybox ambient (`AmbientMode.Skybox`) | Only after `DynamicGI.UpdateEnvironment()` | Throttle; see below |
| Default reflection (skybox cubemap) | Only after the environment update | Same call |
| Baked lightmaps, Mixed lights' baked part | No | Baked for one sun position |
| Light Probe Groups | No | Baked indirect light |
| APV, single scenario | No | Baked indirect light |
| APV lighting scenarios | Blends between baked sets | Indirect only, see below |
| Baked reflection probes | No | Captured once |
| Realtime reflection probes | When re-rendered | Expensive |

## Recommended setup for an outdoor survival game

1. Sun and moon as **Realtime** directional lights. Do not use Mixed for the sun: the baked part is wrong at every other time of day.
2. Outdoor ambient: Trilight gradients from `SunMoonDriver`, tuned per time of day.
3. Interiors and caves need darkening that Trilight cannot provide. Options, cheapest first:
   - Local post Volume or lower ambient via a trigger when entering an interior (gameplay-side switch).
   - APV with **Sky Occlusion** (6000.0+, see `unity-lighting`) so probes darken where the sky is hidden. Check in the current URP manual whether sky occlusion responds to runtime sky color changes in your version before relying on it.
   - APV lighting scenarios (below).
4. Reflections: skybox reflection outdoors, refreshed together with the environment update; for a few key interiors a realtime probe refreshed on time-of-day steps (snippet below), or a baked probe accepted as approximate.

## Throttling the environment update

`SunMoonDriver` in `SkyboxThrottled` mode calls `DynamicGI.UpdateEnvironment()` only when both conditions hold: `environmentUpdateInterval` unscaled seconds have passed and the normalized time moved by at least `environmentUpdateMinDelta`. The 6000.0 Scripting Reference states:

- The call schedules an update of the environment cubemap.
- With `SystemInfo.supportsAsyncGPUReadback`, the readback is asynchronous and may lag one or more frames.
- Only a limited number of readbacks can be scheduled; requests made faster than Unity can process are ignored without an error.
- Without async readback the readback stalls the current thread.
- `DynamicGI.SetEnvironmentData` is the alternative for supplying the data yourself.

Profile the call on the slowest target (Steam Deck) before choosing an interval. Ambient intensity (`RenderSettings.ambientIntensity`) can still change every frame between updates to hide steps.

## APV lighting scenarios (URP 17)

From the Unity 6.0 URP manual ("Bake different lighting setups with Lighting Scenarios"):

- Enable on the active URP Asset: **Lighting > Light Probe Lighting > Lighting Scenarios**, and **Scenario Blending** for runtime blending.
- In the Lighting window's Adaptive Probe Volumes panel, add scenarios (for example `Noon`, `Dusk`, `Night`), select each and Generate Lighting with the matching sun setup.
- At runtime: `ProbeReferenceVolume.instance.lightingScenario` selects the active scenario, `ProbeReferenceVolume.instance.BlendLightingScenario(string, float)` blends toward another with a 0-1 factor and should be called every frame while blending, `numberOfCellsBlendedPerFrame` spreads the cost.
- Scenarios store **indirect** lighting only. Direct light still comes from the realtime sun.
- Blending needs identical probe count and positions in every scenario. Bake later scenarios with **Probe Placement > Probe Positions = Don't Recalculate**.

A blend only goes between two scenarios at a time, so map the day into segments (night to dawn, dawn to noon, noon to dusk, dusk to night) and switch `lightingScenario` at segment boundaries. Verify memory and blend cost on the Deck; each scenario adds baked data to the build. The manual page used here does not list platform requirements for blending; check the URP 17 APV documentation for your target.

## Realtime reflection probe on time steps

```csharp
public sealed class InteriorProbeRefresher : MonoBehaviour
{
    [SerializeField] private ReflectionProbe probe;
    [SerializeField] private TimeOfDay timeOfDay;
    [SerializeField, Range(0.01f, 0.25f)] private float refreshEveryDayFraction = 0.05f;

    private float _lastRefresh01 = -1f;

    private void Update()
    {
        var t = this.timeOfDay.Normalized;
        if (this._lastRefresh01 >= 0f && Mathf.Abs(Mathf.DeltaAngle(t * 360f, this._lastRefresh01 * 360f)) / 360f < this.refreshEveryDayFraction)
        {
            return;
        }

        this._lastRefresh01 = t;
        this.probe.RenderProbe();
    }
}
```

Set the probe to Realtime, refresh mode Via Scripting, a small resolution and time slicing (see `unity-lighting`). Each refresh renders the scene into six faces.

## Shadow cost

- The main directional light's shadow cost is the cascades: Max Distance, cascade count and resolution on the URP Asset. For slow exploration games a shorter Max Distance with 2-3 cascades usually beats 4 cascades at long range; confirm with the Frame Debugger (`optimize-3d-rendering`).
- Long shadows at dawn and dusk stretch across more of the cascade; expect the worst shadow quality there. Raise normal bias slightly rather than resolution.
- The moon rarely needs shadows. Use none or hard shadows at low strength (`Light.shadowStrength`).
- Disabling a light that is below the horizon (the driver does this) removes its shadow pass entirely; a zero-intensity light that stays enabled may still cost.
- Per-quality tiers: lower shadow distance and resolution on the Deck preset (`pc-settings-menu` applies the quality level).

## Fog

The driver writes `RenderSettings.fogColor` and `RenderSettings.fogDensity`. `fogDensity` is the density of exponential fog, so set the scene's fog mode to Exponential or ExponentialSquared. Linear fog uses `fogStartDistance` / `fogEndDistance` instead; adapt `UpdateFog` if you prefer it. Only shaders that apply URP fog are affected; custom shaders must call `MixFog`.
