---
name: interaction-system
description: Builds a player interaction system for Unity 6 3D games - an IInteractable interface, target detection by camera SphereCast or OverlapSphereNonAlloc with distance, angle, priority and line-of-sight scoring plus hysteresis, a world-anchored prompt with keyboard or gamepad glyphs from the Input System (onActionChange, GetBindingDisplayString control paths), press and hold-to-interact, interaction locks, and pickups, doors and containers that are server-authoritative in Netcode for GameObjects co-op ([Rpc(SendTo.Server)] requests validated for distance and state, NetworkVariable results, no prediction). Use for "press E to interact", picking up items, opening doors, hold to loot, interaction prompt glyphs, the wrong object highlighted, or two co-op players grabbing the same item. Not for item or inventory data and UI, combat targeting, Input System setup and rebinding (input-system-actions), general device glyph mapping (steam-deck-controller), or general NGO setup (netcode-for-gameobjects).
license: MIT
metadata:
  category: gameplay-systems
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.OverlapSphereNonAlloc.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.SphereCast.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.Linecast.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera.WorldToScreenPoint.html, https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/ActionBindings.html, https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Interactions.html, https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/api/UnityEngine.InputSystem.PlayerInput.html, https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/api/UnityEngine.InputSystem.InputSystem.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/message-system/rpc.html, https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/networkvariable.html"
  unity: "6000.0+"
---

# Interaction System

## Workflow

1. **Layers.** Add an `Interactable` layer for interaction colliders (triggers are fine) and decide which layers block sight (`Default`, `Environment`). The player's own layer is removed from both masks automatically.
2. **Copy the scripts:**
   - [scripts/Interactable.cs](scripts/Interactable.cs): read first; `IInteractable`, `InteractionRequest`, `IInteractionExecutor` and the `Interactable` base for local-only objects (notes, signs, local audio logs).
   - [scripts/Interactor.cs](scripts/Interactor.cs): read when tuning detection or input; camera ray, proximity scoring, hysteresis, line of sight, press and hold, locks, pending result timeout.
   - [scripts/InteractionPrompt.cs](scripts/InteractionPrompt.cs): read when building the prompt UI; world anchoring, TMP labels, hold fill, keyboard vs gamepad glyphs.
   - [scripts/NetworkInteractable.cs](scripts/NetworkInteractable.cs) and [scripts/NetworkInteractor.cs](scripts/NetworkInteractor.cs): read when the game is co-op; base class whose `Interact` only runs on the server, and the owner-to-server request with validation and a result RPC.
   - [scripts/NetworkDoor.cs](scripts/NetworkDoor.cs), [scripts/NetworkPickup.cs](scripts/NetworkPickup.cs), [scripts/NetworkContainer.cs](scripts/NetworkContainer.cs): read when adding those object types; replicated open/locked state, server despawn after a successful hand-off, single-user occupancy lock.
3. **Player prefab:** `Interactor` (+ `NetworkInteractor` in co-op) on the player root, `eye` at head height, `viewOrigin` left empty to use `Camera.main` (the output camera, not a `CinemachineCamera`).
4. **Pick a detection mode:** `CameraRay` for first-person or tight over-the-shoulder, `Proximity` for third-person with a free camera, `CameraRayThenProximity` (default) when the player can both aim at objects and walk up to them.
5. **Interact action:** a Button action `Interact` in the `Player` map, bound per control scheme (`Keyboard&Mouse`: E, `Gamepad`: buttonWest or buttonSouth). No Hold interaction on the action; hold timing is per object (`HoldDuration`) and done in `Interactor`. See `input-system-actions`.
6. **Prompt:** one Screen Space Overlay canvas, `InteractionPrompt` wired to the local player's `Interactor` after spawn. Gamepad glyph sprites keyed by control path (`buttonSouth`, `leftTrigger`). On Steam Deck pick the glyph set as described in `steam-deck-controller`.
7. **Objects:** derive from `NetworkInteractable` for anything that changes shared world state; derive from `Interactable` only for purely local effects.
8. **Test:** two Multiplayer Play Mode players grabbing one pickup in the same frame, opening one container together, a late joiner seeing doors already open, holding while walking away, and a client with 200 ms simulated latency.

## Writing an interactable

```csharp
public sealed class NetworkCampfire : NetworkInteractable
{
    private readonly NetworkVariable<bool> _lit = new();

    public override string Prompt => this._lit.Value ? "Extinguish" : "Light";

    protected override bool OnInteractServer(in InteractionRequest request)
    {
        this._lit.Value = !this._lit.Value;
        return true;
    }
}
```

- `CanInteract` runs on the client (to grey out the prompt) and again on the server (to decide). Keep it a pure function of replicated state and the request.
- `OnInteractServer` runs only on the server, after distance and `CanInteract` checks. Write results to `NetworkVariable`s, spawn or despawn objects; return `false` when the action did not happen (inventory full).
- Visuals follow replicated state on every peer (`NetworkDoor.Update` swings the hinge toward `_isOpen.Value`), so late joiners see the correct state.

## Rules

- **The server owns every shared result.** The client sends "I want to interact with X"; it never sends "I opened X" or "I received item Y". The request carries only a `NetworkBehaviourReference`.
- **Validate on the server:** sender is the interactor's owner (`InvokePermission = RpcInvokePermission.Owner`, NGO 2.7.0+), target exists and is spawned, distance from the server-side player position to `InteractionPoint` within `MaxDistance` plus a small tolerance for owner-authoritative movement lag, `CanInteract` true, request rate limited.
- **No prediction.** Show feedback for the request state instead: the prompt dims while `IsAwaitingResult`, then the replicated change (door swings, item vanishes) or a rejection cue arrives. A 2 s timeout clears the pending state if the result never arrives.
- **Concurrent requests resolve in arrival order on the server.** The second player to grab a pickup gets `Rejected` because the first one already despawned it.
- **State for late joiners lives in `NetworkVariable`s**, not in RPCs. One-off cosmetic events may use `[Rpc(SendTo.ClientsAndHost)]`, but never as the only record of a change.
- **Locks come in two kinds.** Interactor locks (`AddLock(owner)` while a menu, dialogue or crafting UI is open) stop detection and clear focus. Object locks (`NetworkDoor` locked, `NetworkContainer` in use) make `CanInteract` false and change the prompt. Release container occupancy on close, distance and disconnect.
- **Detection uses non-allocating queries.** `OverlapSphereNonAlloc` with a fixed buffer (it does not grow; a full buffer means missed colliders), `GetComponentInParent<IInteractable>()` on the hit collider, `QueryTriggerInteraction.Collide` for the interactable query and `Ignore` for line-of-sight.
- **Destroyed targets:** the interactor keeps the `Component` beside the interface reference, because an interface reference to a destroyed `MonoBehaviour` does not compare equal to `null`.
- **Hold timing is client-side UX.** The server does not trust it. If a hold must cost time (lockpicking, harvesting), make it a server-side timed action that the server starts and cancels.
- **Prompt text is a localization key** in shipping code (see `localization`); the samples use plain strings.

## References

- [references/detection-and-scoring.md](references/detection-and-scoring.md): read when choosing a detection mode, tuning scores and hysteresis, fixing the wrong object being picked, or profiling detection.
- [references/prompt-and-glyphs.md](references/prompt-and-glyphs.md): read when building the prompt, switching glyphs between keyboard and gamepad, showing hold progress, or handling rebinding.
- [references/coop-interactions.md](references/coop-interactions.md): read when making interactions networked, writing pickups, doors or containers for co-op, or debugging rejected or duplicated interactions.

## Pitfalls

| Symptom | Fix |
|---|---|
| Prompt flickers between two nearby objects | Raise `switchMargin`; check that both objects do not share one `InteractionPoint` |
| Camera ray always hits the player | Player layer in the masks; the interactor strips its own layer only when it is not `Default` |
| Prompt shows through walls | Occluder layers missing from `occluderMask` |
| Item picked up twice or duplicated | Client applied the result locally; move the effect into `OnInteractServer` |
| `RpcException` / "must be spawned" when pressing interact | Interacting before `OnNetworkSpawn`; `NetworkInteractor` enables the interactor only after spawn |
| Late joiner sees a closed door that is open | Door state sent by RPC; keep it in a `NetworkVariable` |
| Container stays "In use" forever | Occupant disconnected or walked away; release on `OnConnectionEvent` and on distance (done in `NetworkContainer`) |
| Prompt shows keyboard keys while using a gamepad | Prompt not listening to `InputSystem.onActionChange`, or the binding has no `Gamepad` group |
| Glyph wrong on Steam Deck or PlayStation pad | Steam Input emulation presents an Xbox pad; choose the glyph table as in `steam-deck-controller` |
| Interaction ignored right after another | Server rate limit (`minRequestInterval`) or the previous request still pending |

## Related skills

- `input-system-actions`: Interact action, control schemes, rebinding.
- `netcode-for-gameobjects`: RPC rules, `NetworkVariable`, spawning and ownership.
- `steam-deck-controller`: Deck-correct glyph selection.
- `physics-3d-collision`: layers, triggers and query misses.
- `character-controller-3d`: where the `eye` and body transforms come from.
- `ui-ugui`: canvas setup for the prompt.
- `localization`: localized prompt strings.
- `game-data-pipeline`: item ids used by pickups.
- `csharp-unity`: house style for the scripts.
