---
name: initialize-ai-navigation
description: Sets up, scripts and troubleshoots Unity AI Navigation (com.unity.ai.navigation 2.x) in Unity 6 - NavMeshSurface baking (editor and runtime BuildNavMesh / UpdateNavMesh), NavMeshAgent, NavMeshObstacle carving, NavMeshLink, NavMeshModifier / ModifierVolume, agent types, areas, costs and area masks. Use when the user asks for pathfinding, walkable navmesh, patrol routes, click-to-move, follow/chase AI, jump or drop links, dynamic obstacles, procedural-level navmesh, or agent-driven animation, or reports "agent won't move", "path not found", PathPartial / PathInvalid, agents sliding through obstacles or jittering.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: gameplay-systems
  sources: "Unity-Technologies/skills/skills/initialize-ai-navigation, AlexeyPerov/Unity-Open-MCP/skills/extensions/navigation"
  unity: "6000.0+"
---

# Initialize AI Navigation

Full component tables, recipes (move-to, click-to-move, patrol, corner speed, animation coupling, async rebuild) and the troubleshooting tree are in [references/navigation-system.md](references/navigation-system.md). Read it before writing navigation scripts or diagnosing a failure.

## Routing

| User says | Do |
|---|---|
| "add navigation" / "set up nav" | Full setup: NavMeshSurface + bake + NavMeshAgent |
| "make this character navigate" / "add pathfinding" | NavMeshAgent + movement script; ensure a baked surface |
| "patrol between points" / "click to move" / "chase the player" | Agent + recipe script from the reference |
| "avoid obstacles" | NavMeshObstacle; carve for stationary blockers |
| "connect two areas" / "jump across" / "drop down" | NavMeshLink, or Generate Links on the surface |
| "different agent sizes" | Agent types + one NavMeshSurface per type |
| "areas and costs" / "restrict areas" | Areas, Modifier / ModifierVolume, `areaMask`, `SetAreaCost` |
| "procedural level" / "rebake at runtime" | `BuildNavMesh` once, `UpdateNavMesh` async afterwards |
| "animate while navigating" | Animator + agent coupling recipe |
| "agent won't move" / "path not found" | Troubleshooting tree in the reference |

## Workflow

### 0. Package check
Confirm `com.unity.ai.navigation` is in `Packages/manifest.json`. If missing, add it with a real version read from the Unity registry (`https://packages.unity.com/com.unity.ai.navigation`) or the Package Manager; never invent a version, because an unresolvable version fails silently. Unity 6 ships 2.x. See `unity-package-management` for headless installs.

The package components live in `Unity.AI.Navigation` (`NavMeshSurface`, `NavMeshLink`, `NavMeshModifier`, `NavMeshModifierVolume`); runtime types (`NavMeshAgent`, `NavMeshObstacle`, `NavMesh`, `NavMeshPath`) are in `UnityEngine.AI`. The legacy scene bake ("Navigation Static" + Navigation window bake) and the `OffMeshLink` component are obsolete; do not use them for new work.

### 1. Inspect what exists
Find existing `NavMeshSurface`, `NavMeshAgent`, `NavMeshObstacle`, `NavMeshLink` and `NavMeshModifier` components (live Editor via `unity-cli`, or search scene/prefab YAML). Check agent types in **Window > AI > Navigation > Agents**. Summarize before changing anything.

### 2. Gather missing details
Walkable geometry, agent size/type, behaviour (move-to, patrol, follow, click), dynamic obstacles, gaps/jumps, area costs. Ask when unclear.

### 3. Build in this order
1. **NavMeshSurface** on the level root. Agent Type, Collect Objects (All / Volume / Current Object Hierarchy / NavMeshModifier Component Only), Include Layers, Use Geometry (Render Meshes or Physics Colliders). Bake.
2. **NavMeshAgent** on each character. Agent Type must match a baked surface.
3. **NavMeshObstacle** on dynamic blockers. Carve only stationary or slow ones.
4. **NavMeshLink** for gaps the bake cannot infer. Both ends over NavMesh, Activated on.
5. **NavMeshModifier / ModifierVolume** to override areas or exclude objects (Mode = Remove Object skips an object from the bake).
6. **Scripts** from the reference recipes.

### 4. Validate
- The surface reports baked data (`surface.navMeshData != null`) and the Scene view shows the blue overlay. Empty bake: Volume mode with no overlapping geometry, geometry on an excluded layer, Physics Colliders mode with no colliders, or slope/step/radius settings that reject every surface.
- Agent Type on surface and agent match.
- Agents start on the NavMesh (`agent.isOnNavMesh`; fix spawns with `NavMesh.SamplePosition` + `agent.Warp`).
- Destinations resolve: `SetDestination` only paths in Play Mode; check `pathPending`, then `pathStatus` (`PathComplete` / `PathPartial` / `PathInvalid`).
- Links connected, obstacles carve as intended, area masks allow the route.
- No NavMeshAgent with a non-kinematic Rigidbody, and never an active Agent and Obstacle on the same GameObject.

### 5. Report
List surfaces (GameObject, agent type, collect mode, bake status), agents (speed, stopping distance, area mask), obstacles (shape, carve), links (ends, bidirectional, area), scripts attached, and manual follow-ups such as re-baking after geometry changes.

## Rules and pitfalls

- Guard arrival checks with `!agent.pathPending` before reading `remainingDistance`.
- Teleport with `agent.Warp(pos)`, never `transform.position`.
- Pause with `agent.isStopped = true`; clear with `agent.ResetPath()`. `Stop()` / `Resume()` do not exist.
- Disable `autoBraking` for continuous patrols.
- `BuildNavMesh()` blocks the main thread; at runtime prefer `UpdateNavMesh(surface.navMeshData)` (returns `AsyncOperation`) after the first build.
- Carving obstacles that move constantly are expensive; leave Carve off for vehicles and players and rely on avoidance.
- Area costs must be at least 1. Agent priority: lower number = more important.
- Click-to-move and other input in Unity 6 should use the Input System (see `input-system-actions`); legacy `Input` throws when only the new backend is active.

## Related skills

- `physics-3d-collision`: agent + Rigidbody/collider setups, ground raycasts.
- `animation-authoring`: blend trees and root motion driven by `agent.velocity`.
- `splines-paths`: spline-defined patrol routes sampled into agent destinations.
- `unity-cli`: inspecting or editing a live scene.
