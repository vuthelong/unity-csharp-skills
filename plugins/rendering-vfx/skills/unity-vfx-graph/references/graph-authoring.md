# VFX Graph authoring recipes and data sources

## Contents

- [Attributes](#attributes)
- [Recipes](#recipes)
- [Point Cache](#point-cache)
- [Signed Distance Fields](#signed-distance-fields)
- [Mesh and skinned mesh sampling](#mesh-and-skinned-mesh-sampling)
- [Shader Graph outputs](#shader-graph-outputs)
- [Strips and trails](#strips-and-trails)
- [Flipbooks](#flipbooks)
- [Timeline](#timeline)
- [Asset Inspector settings](#asset-inspector-settings)

## Attributes

Standard per-particle attributes (read with *Get Attribute*, write with *Set Attribute* blocks):

| Attribute | Type | Notes |
|---|---|---|
| `position`, `velocity`, `direction` | Vector3 | Velocity integrates into position automatically in Update unless *Update Position* is disabled on the context |
| `color`, `alpha` | Vector3, float | Color is RGB; HDR values above 1 feed Bloom |
| `size`, `scale` | float, Vector3 | Final size = size * scale |
| `lifetime`, `age`, `alive` | float, float, bool | Particles reap when `age > lifetime` (Update > *Reap Particles*) |
| `angle`, `angularVelocity`, `pivot` | Vector3 | Orientation for quads/meshes |
| `texIndex` | float | Flipbook frame |
| `particleId`, `seed`, `spawnIndex` | uint | Stable per-particle values for random and buffer indexing |
| `targetPosition`, `mass`, `oldPosition` | Vector3, float, Vector3 | Lines, physics forces, strips |

Custom attributes are declared in the Blackboard (17.x) and then behave like built-ins. Only attributes that some block writes or reads consume memory.

## Recipes

**Impact sparks (burst + gravity + collision)**
- Event `OnImpact` (sent from C# with `position`, `direction` payload) -> Spawn: Single Burst 30-60.
- Initialize: capacity 512, Set Position from source, Set Velocity Random in a cone around source `direction` (speed 4-10), Set Lifetime Random 0.3-0.8, Set Color HDR orange intensity 4.
- Update: Gravity, Linear Drag 0.5, Collision Shape (Plane) or Collide with Depth Buffer (needs URP Depth Texture).
- Output: Output Particle Quad, Orient: Along Velocity, Set Size over Life (curve to 0), Additive blend, Sort off.

**Ambient dust / embers**
- Constant Spawn Rate exposed as `SpawnRate`. Initialize: Set Position (Shape: Box) volume around the camera area, slow random velocity, long lifetime.
- Update: Turbulence (low intensity, low frequency) or Vector Field Force with a Texture3D.
- Output: small Additive quads, Set Alpha over Life fade-in/out. World space.

**Smoke column**
- Constant rate, Set Position (Shape: Circle) small radius, upward velocity, lifetime 3-6.
- Update: Turbulence, Linear Drag, Set Size over Life growing.
- Output: Lit Quad or Shader Graph output with a flipbook, Alpha blend, Sort on (needed for alpha), soft particles if depth texture available. Watch overdraw.

**Mesh dissolve**
- Point Cache from the mesh (or Set Position (Mesh) sampling) in Initialize; drive spawn with an exposed `Progress` float and Set Alive false where `position.y > Progress`.
- Pair with a Shader Graph dissolve on the mesh material using the same `Progress` value.

**Swarm toward targets**
- Exposed GraphicsBuffer of targets + count (see `csharp-api.md`), Sample Graphics Buffer by `particleId % count` in Update, Conform to Sphere around the sampled position or set `velocity` toward it.

## Point Cache

A `.pCache` asset stores a point cloud with attributes (position, normal, color, UV).

1. *Window > Visual Effects > Utilities > Point Cache Bake Tool*.
2. Source **Mesh** (sample vertices/surface, with normals/colors) or **Texture** (pixels above a threshold become points).
3. In the graph: *Point Cache* operator (outputs per-attribute Texture2D maps and point count) -> *Set Position from Map* / *Set Color from Map* blocks with Sample Mode Sequential or Random.

Use Point Cache for static shapes; for animated meshes use skinned mesh sampling instead.

## Signed Distance Fields

- Bake: *Window > Visual Effects > Utilities > SDF Bake Tool* (Texture3D asset), or `MeshToSDFBaker` at runtime.
- Use: *Set Position (Shape: Signed Distance Field)* to spawn on or inside a surface, *Conform to SDF* to attract particles to the surface, *Collision Shape* in SDF mode to collide.
- Every SDF block needs the SDF texture plus an **oriented box transform** (center, angles, size) matching the bake volume. Wrong box size is the usual "particles float off the shape" bug.
- Resolution 64 is a good default; 128+ costs 8x the memory and bake time.

## Mesh and skinned mesh sampling

- *Set Position (Mesh)* / *Sample Mesh* read vertices, surface, or edges of an exposed `Mesh`.
- *Set Position (Skinned Mesh)* / *Sample Skinned Mesh* take an exposed `SkinnedMeshRenderer` (`SetSkinnedMeshRenderer` from C#) and follow animation.
- For velocity inheritance from a skinned mesh, sample the previous frame position (Skinned Mesh sampling exposes current and previous) and compute velocity in the graph.

## Shader Graph outputs

1. Create a URP Shader Graph (Lit or Unlit target).
2. Graph Settings > *Support VFX Graph* on.
3. In a VFX Output context (Output Particle Shader Graph Quad/Mesh/Strip), assign the graph in the *Shader Graph* field.
4. Exposed Shader Graph properties appear as inputs on the Output context and can be driven per particle.
5. Shader Graph keywords on VFX outputs are limited; prefer properties and branches on uniform values.

## Strips and trails

- Use a *Particle Strip* system (Initialize Particle Strip / Update / Output Particle Strip Quad). Set *Strip Capacity* and *Particle Per Strip Count*.
- Spawn one strip per emitter; the strip index comes from `stripIndex` (set it from `spawnIndex` or from a GPU event's parent particle).
- For trails behind particles, send a GPU Event *Trigger Event Always* from the parent system into a strip system and inherit source position.
- Strips are always ordered by spawn; do not enable sorting.

## Flipbooks

- Import the sheet with Mip Maps on, Wrap Clamp, and the right sRGB flag (see `unity-texture-import`). Use *Window > Visual Effects > Utilities > Image Sequencer* to pack frames.
- Output UV Mode: Flipbook (or Flipbook Blend for smoother playback), set Flipbook Size; animate `texIndex` with *Flipbook Player* in Update.

## Timeline

Add a **Visual Effect Control Track** (17.x) bound to the `VisualEffect`. Clips send events at their start/end and support scrubbing with prewarm/reinit settings; use it for cinematics instead of animating `Play()` calls. Signals can also call `SendEvent` through a receiver.

## Asset Inspector settings

Select the `.vfx` asset:

| Setting | Effect |
|---|---|
| Culling Flags | Whether bounds are recomputed and simulation runs while invisible |
| Update Mode | Fixed Delta Time vs Delta Time; Fixed is deterministic but steps multiple times on slow frames |
| PreWarm Total Time / Step Count | Simulate before the first visible frame |
| Initial Event Name | Default `OnPlay` |
| Instancing | Batch identical instances; Inspector reports why it is disabled when it is |
| Output Render Order | Draw order between outputs of the same asset |
