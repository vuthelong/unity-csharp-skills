---
name: screen-navigator
description: Sets up and uses Haruma-K/UnityScreenNavigator (`com.harumak.unityscreennavigator`, uGUI only) for screen routing — PageContainer push/pop with back history, ModalContainer popups with backdrops, SheetContainer tabs — plus Page/Modal/Sheet lifecycle overrides, transition animations (SimpleTransitionAnimationObject, Timeline), Resources/Addressables/Preloaded asset loaders, preloading, and UnityScreenNavigatorSettings. Use when adding screen navigation, a page stack, a modal/popup/dialog system, tab sheets, or back-button handling to a uGUI project, or when editing code that uses PageContainer, ModalContainer, SheetContainer, Page, Modal, or Sheet.
license: MIT
metadata:
  category: ui
  sources: "github.com/vuthelong/unity-csharp-skills (original), github.com/Haruma-K/UnityScreenNavigator"
  unity: "6000.0+"
---

# UnityScreenNavigator

uGUI library for screen transitions, transition animations, history stacking, and screen lifecycle (load → initialize → enter/exit → cleanup → release). Package 1.8.x requires **Unity 6000.0+** and **uGUI**; UI Toolkit is not supported. For Canvas/RectTransform work inside screens see `ui-ugui`.

References:
- [references/api.md](references/api.md) — read when writing container calls, lifecycle overrides, callback receivers, or loaders; full signatures verified against 1.8.0 source.
- [references/transitions.md](references/transitions.md) — read when customizing transition animations, draw order, modal backdrops, or interaction blocking.

## Before writing code

1. Check `Packages/manifest.json` / `packages-lock.json` for `com.harumak.unityscreennavigator`, and search for a vendored copy (`class PageContainer`). Forks often change namespaces or switch lifecycles to UniTask; if one exists, read two existing `Page`/`Modal` subclasses and match their API over this skill.
2. Check Scripting Define Symbols for `USN_USE_ASYNC_METHODS` — it switches every lifecycle override from `IEnumerator` to `Task`. Writing the wrong signature fails to compile (`no suitable method found to override`).

## Install

```json
"com.harumak.unityscreennavigator": "https://github.com/Haruma-K/UnityScreenNavigator.git?path=/Assets/UnityScreenNavigator#1.8.0"
```

Pin a tag (`#1.8.0`) for reproducible builds. `USN_USE_ADDRESSABLES` and `USN_USE_TIMELINE` are defined automatically by the package's asmdef when `com.unity.addressables` / `com.unity.timeline` are installed — do not add them by hand.

## Container types

| Container | Screen component | Use for | History | Main calls |
|---|---|---|---|---|
| `PageContainer` | `Page` | Full-area screens | Stack | `Push`, `Pop`, `Preload` |
| `ModalContainer` | `Modal` | Dialogs over a click-blocking backdrop | Stack | `Push`, `Pop`, `Preload` |
| `SheetContainer` | `Sheet` | Tabs; one visible at a time, state kept | None | `Register`, `Show`/`ShowByResourceKey`, `Hide` |

Setup:
1. Under a Canvas, add a GameObject per container and size its RectTransform to the area screens should fill (modals: the full window, since the backdrop covers the container). Containers can be nested and need not be full-screen.
2. Build each screen as a prefab with `Page`/`Modal`/`Sheet` (or a subclass) on the root. The root is stretched to the container; for a modal with margins, put the visible panel in a smaller child.
3. Place prefabs under a `Resources` folder (default loader) or configure another loader (below). The resource key is the path relative to `Resources`, without extension.
4. Get a container with `PageContainer.Of(transform)` (nearest parent) or `PageContainer.Find("Name")` (Inspector `Name` field) — same for Modal/Sheet — or a serialized reference.

## Core usage

```csharp
using UnityScreenNavigator.Runtime.Core.Page;
using UnityScreenNavigator.Runtime.Core.Modal;
using UnityScreenNavigator.Runtime.Core.Sheet;

yield return pageContainer.Push("ShopPage", true);
yield return pageContainer.Push<ItemPage>("ItemPage", true, onLoad: x => x.page.Setup(itemId));
yield return pageContainer.Pop(true);

yield return modalContainer.Push<ConfirmModal>("ConfirmModal", true, onLoad: x => x.modal.Setup(message));
yield return modalContainer.Pop(true);

string inventoryId = null;
yield return sheetContainer.Register("InventorySheet", x => inventoryId = x.sheetId);
yield return sheetContainer.Show(inventoryId, false);
yield return sheetContainer.ShowByResourceKey("InventorySheet", true);
yield return sheetContainer.Hide(true);
```

Every call returns `AsyncProcessHandle`: `yield return handle`, `await handle.Task`, or `handle.OnTerminate += ...`. Wait only when the caller must sequence after the transition.

**`SheetContainer.Show(string sheetId, ...)` takes the sheet id, not the resource key.** Ids are GUIDs unless you pass `sheetId:` to `Register`. Use `ShowByResourceKey` to show by prefab key (the upstream README's `Show("ExampleSheet")` example predates this split).

## Passing data

The library prescribes no data-passing mechanism. Use `onLoad` (fires after instantiate, before `Initialize` and the transition) to call a `Setup(...)` method on the new screen, or the project's existing DI. With `loadAsync: false`, `onLoad` runs in the same frame as the call. Do not invent an argument-marshalling layer if the project lacks one.

## Lifecycle

Override only what you need (signatures in `references/api.md`):

| Screen | Order on push/show | Order on pop/hide |
|---|---|---|
| `Page`, `Modal` | `Initialize` → `WillPushEnter` → `DidPushEnter` (outgoing: `WillPushExit`/`DidPushExit`) | `WillPopExit` → `DidPopExit` → `Cleanup` (revealed screen: `WillPopEnter`/`DidPopEnter`) |
| `Sheet` | `Initialize` (at Register) → `WillEnter` → `DidEnter` | `WillExit` → `DidExit` (sheet kept; `Cleanup` only on `UnregisterAll`/destroy) |

`Will*` and `Initialize`/`Cleanup` are `IEnumerator` (or `Task` with `USN_USE_ASYNC_METHODS`); `Did*` are `void`. Hooks without subclassing: `page.AddLifecycleEvent(onWillPushEnter: ...)` or `AddLifecycleEvent(IPageLifecycleEvent, priority)` (priority < 0 runs before the screen's own methods, > 0 after). Container-level hooks: `IPageContainerCallbackReceiver` (`BeforePush`/`AfterPush`/`BeforePop`/`AfterPop`) — a MonoBehaviour implementing it on the container GameObject registers itself.

## Loading

| Need | Do |
|---|---|
| Default | Prefabs under `Resources/` |
| Addressables | `Assets > Create > Resource Loader > Addressable Asset Loader`; assign to Settings `Asset Loader` (or a container's `AssetLoader` property). Keys become addresses |
| Prefab references, no Resources | `Assets > Create > Resource Loader > Preloaded Asset Loader` and list key + prefab |
| Scenes or custom storage | Subclass `AssetLoaderObject` (implements `IAssetLoader`) |
| Avoid hitches on heavy screens | `yield return container.Preload("ShopPage")` → `Push("ShopPage", ...)` → `ReleasePreloaded("ShopPage")` when no longer needed |
| Same-frame setup | `loadAsync: false` (Addressables loader needs 1.17.4+; sync Addressables loads can stall) |

Project settings live in a `UnityScreenNavigatorSettings` asset created via `Assets > Create > Screen Navigator Settings`; creation registers it in **Player Settings > Preloaded Assets**. Without that entry, builds silently use defaults (default animations, Resources loader).

## Other features

- `Pop(true, 2)` pops several; `Pop(true, "PageId")` pops back to an id (throws if not found). Assign ids with `Push(..., pageId: "Home")` / `modalId:`.
- `Push(..., stack: false)` keeps a page (e.g. a loading screen) out of history; it is destroyed when the next page is pushed.
- Skipped screens in a multi-pop get only `Cleanup`, no transition callbacks.

## Pitfalls

- **One transition per container at a time.** Push/Pop during a transition trips an `Assert` (stripped in release builds, leaving undefined behavior). Guard with `container.IsInTransition`, especially for back buttons and double taps.
- **Popped pages and modals are destroyed**; they cannot be reused. Preload for load cost; keep state outside the view, or use a Sheet for tab state.
- **Key mismatch** — the resource key is the `Resources`-relative path or Addressables address, not the class name.
- **Prefab root lacks the requested type** — the container silently `AddComponent`s it after instantiating, producing a component with no serialized references (null fields in `Initialize`). Put the subclass on the prefab root.
- **Input during transitions is blocked** by default (all containers). See `references/transitions.md` to scope or disable it.
- **Container `RectMask2D`** clips screens to the container; disable it if transitions slide content from outside.
- **Cleanup on destroy:** when a container is destroyed (scene unload), `Cleanup` runs for its remaining screens if Settings `Call Cleanup When Destroy` is on (default). Do not rely on `Cleanup` touching other destroyed objects.

## Generating a new screen

1. Pick the container (Page = full area + history, Modal = popup with backdrop, Sheet = tab). Ask if ambiguous.
2. Create the prefab under the loader's root with the subclass on the root GameObject.
3. Override only needed lifecycle methods with the signature style the project uses (coroutine vs `Task`).
4. Add the `Push`/`Register`+`Show` call at the real call site, with an `IsInTransition` guard for user-triggered navigation.
