# UnityScreenNavigator API (1.8.0)

Verified against `Assets/UnityScreenNavigator/Runtime` of github.com/Haruma-K/UnityScreenNavigator. All transition methods return `AsyncProcessHandle` (`yield return`, `.Task`, `.OnTerminate`).

## Contents

- [Namespaces](#namespaces)
- [PageContainer](#pagecontainer)
- [ModalContainer](#modalcontainer)
- [SheetContainer](#sheetcontainer)
- [Screen components](#screen-components)
- [Lifecycle overrides](#lifecycle-overrides)
- [External lifecycle hooks](#external-lifecycle-hooks)
- [Container callback receivers](#container-callback-receivers)
- [Asset loaders](#asset-loaders)
- [Settings asset](#settings-asset)

## Namespaces

| Types | Namespace |
|---|---|
| `Page`, `PageContainer`, `IPageLifecycleEvent`, `IPageContainerCallbackReceiver` | `UnityScreenNavigator.Runtime.Core.Page` |
| `Modal`, `ModalContainer`, `ModalBackdrop`, `IModalLifecycleEvent`, `IModalContainerCallbackReceiver` | `UnityScreenNavigator.Runtime.Core.Modal` |
| `Sheet`, `SheetContainer`, `ISheetLifecycleEvent`, `ISheetContainerCallbackReceiver` | `UnityScreenNavigator.Runtime.Core.Sheet` |
| `TransitionAnimationObject`, `TransitionAnimationBehaviour`, `SimpleTransitionAnimationObject` | `UnityScreenNavigator.Runtime.Core.Shared` |
| `IAssetLoader`, `AssetLoaderObject`, `AssetLoadHandle<T>`, `PreloadedAssetLoader` | `UnityScreenNavigator.Runtime.Foundation.AssetLoader` |
| `AsyncProcessHandle` | `UnityScreenNavigator.Runtime.Foundation.Coroutine` |

## PageContainer

```csharp
AsyncProcessHandle Push(string resourceKey, bool playAnimation, bool stack = true,
    string pageId = null, bool loadAsync = true, Action<(string pageId, Page page)> onLoad = null);
AsyncProcessHandle Push(Type pageType, string resourceKey, bool playAnimation, ...same optional args);
AsyncProcessHandle Push<TPage>(string resourceKey, bool playAnimation, bool stack = true,
    string pageId = null, bool loadAsync = true, Action<(string pageId, TPage page)> onLoad = null) where TPage : Page;
AsyncProcessHandle Pop(bool playAnimation, int popCount = 1);
AsyncProcessHandle Pop(bool playAnimation, string destinationPageId);
AsyncProcessHandle Preload(string resourceKey, bool loadAsync = true);
bool IsPreloadRequested(string resourceKey);
bool IsPreloaded(string resourceKey);
void ReleasePreloaded(string resourceKey);

static PageContainer Of(Transform transform, bool useCache = true);
static PageContainer Of(RectTransform rectTransform, bool useCache = true);
static PageContainer Find(string containerName);
static List<PageContainer> Instances { get; }

IReadOnlyList<string> OrderedPagesIds { get; }
IReadOnlyDictionary<string, Page> Pages { get; }
bool IsInTransition { get; }
bool Interactable { get; set; }
IAssetLoader AssetLoader { get; set; }
void AddCallbackReceiver(IPageContainerCallbackReceiver receiver);
void RemoveCallbackReceiver(IPageContainerCallbackReceiver receiver);
```

Ids default to `Guid.NewGuid().ToString()`. `Preload` of an already-preloaded key and `ReleasePreloaded` of an unknown key throw `InvalidOperationException`. `Pop(bool, string)` throws if the id is not in the stack.

## ModalContainer

Same as `PageContainer` except `Push` has no `stack` parameter:

```csharp
AsyncProcessHandle Push(string resourceKey, bool playAnimation, string modalId = null,
    bool loadAsync = true, Action<(string modalId, Modal modal)> onLoad = null);
AsyncProcessHandle Push<TModal>(...) where TModal : Modal;
AsyncProcessHandle Pop(bool playAnimation, int popCount = 1);
AsyncProcessHandle Pop(bool playAnimation, string destinationModalId);
IReadOnlyList<string> OrderedModalIds { get; }
IReadOnlyDictionary<string, Modal> Modals { get; }
```

Inspector: `Name`, `Backdrop Strategy`, `Override Backdrop Prefab`.

## SheetContainer

```csharp
AsyncProcessHandle Register(string resourceKey, Action<(string sheetId, Sheet sheet)> onLoad = null,
    bool loadAsync = true, string sheetId = null);
AsyncProcessHandle Register(Type sheetType, string resourceKey, ...same optional args);
AsyncProcessHandle Register<TSheet>(string resourceKey, Action<(string sheetId, TSheet sheet)> onLoad = null,
    bool loadAsync = true, string sheetId = null) where TSheet : Sheet;
AsyncProcessHandle Show(string sheetId, bool playAnimation);
AsyncProcessHandle ShowByResourceKey(string resourceKey, bool playAnimation);
AsyncProcessHandle Hide(bool playAnimation);
void UnregisterAll();

string ActiveSheetId { get; }
Sheet ActiveSheet { get; }
IReadOnlyDictionary<string, Sheet> Sheets { get; }
```

Note the argument order differs from `Push`: `onLoad` comes **before** `loadAsync` in `Register`. `ShowByResourceKey` maps the key to the most recently registered sheet with that key; register the same key twice only when you track ids.

## Screen components

| Property | Page | Modal | Sheet |
|---|---|---|---|
| `Identifier` (partner matching; defaults to prefab name via `UsePrefabNameAsIdentifier`) | yes | yes | yes (explicit field) |
| `RenderingOrder` (higher draws in front during transitions) | yes | no (newest on top) | yes |
| `AnimationContainer` | yes | yes | yes |
| `IsTransitioning`, `TransitionAnimationType?`, `TransitionAnimationProgress`, `event TransitionAnimationProgressChanged` | yes | yes | yes |

## Lifecycle overrides

Coroutine form (default). With the `USN_USE_ASYNC_METHODS` define on **every** platform, each `IEnumerator` becomes `Task`.

```csharp
public class SomePage : Page
{
    public override IEnumerator Initialize() { yield break; }
    public override IEnumerator WillPushEnter() { yield break; }
    public override void DidPushEnter() { }
    public override IEnumerator WillPushExit() { yield break; }
    public override void DidPushExit() { }
    public override IEnumerator WillPopEnter() { yield break; }
    public override void DidPopEnter() { }
    public override IEnumerator WillPopExit() { yield break; }
    public override void DidPopExit() { }
    public override IEnumerator Cleanup() { yield break; }
}
```

`Modal` has the identical set. `Sheet` has `Initialize`, `WillEnter`, `DidEnter`, `WillExit`, `DidExit`, `Cleanup`.

Timing: `onLoad` callback → `Initialize` (after instantiate) → `Will*` (before the animation) → animation → `Did*` → `Cleanup` (before the instance is destroyed and its asset released).

## External lifecycle hooks

```csharp
page.AddLifecycleEvent(IPageLifecycleEvent lifecycleEvent, int priority = 0);
page.RemoveLifecycleEvent(IPageLifecycleEvent lifecycleEvent);
page.AddLifecycleEvent(initialize: ..., onWillPushEnter: ..., onDidPushEnter: ..., onWillPushExit: ...,
    onDidPushExit: ..., onWillPopEnter: ..., onDidPopEnter: ..., onWillPopExit: ..., onDidPopExit: ...,
    onCleanup: ..., priority: 0);
```

The delegate overload exists in `Func<IEnumerator>` and `Func<Task>` forms; `Did*` take `Action`. Priority < 0 runs before the screen's own overrides, > 0 after. `Modal` mirrors this; `Sheet` uses `onWillEnter`/`onDidEnter`/`onWillExit`/`onDidExit`.

## Container callback receivers

```csharp
public interface IPageContainerCallbackReceiver
{
    void BeforePush(Page enterPage, Page exitPage);
    void AfterPush(Page enterPage, Page exitPage);
    void BeforePop(Page enterPage, Page exitPage);
    void AfterPop(Page enterPage, Page exitPage);
}
```

`IModalContainerCallbackReceiver` is identical with `Modal`. `ISheetContainerCallbackReceiver` has `BeforeShow(Sheet enterSheet, Sheet exitSheet)`, `AfterShow(...)`, `BeforeHide(Sheet exitSheet)`, `AfterHide(Sheet exitSheet)`. A MonoBehaviour implementing the interface on the container's GameObject is registered automatically; extension methods also accept delegates (`container.AddCallbackReceiver(onBeforePush: ...)`).

## Asset loaders

```csharp
public interface IAssetLoader
{
    AssetLoadHandle<T> Load<T>(string key) where T : Object;
    AssetLoadHandle<T> LoadAsync<T>(string key) where T : Object;
    void Release(AssetLoadHandle handle);
}
```

| Loader | Create | Notes |
|---|---|---|
| `ResourcesAssetLoaderObject` | `Assets > Create > Resource Loader > Resources Asset Loader` | Default behavior |
| `AddressableAssetLoaderObject` | `Assets > Create > Resource Loader > Addressable Asset Loader` | Compiled only when `com.unity.addressables` is installed (`USN_USE_ADDRESSABLES`); sync load needs 1.17.4+ |
| `PreloadedAssetLoaderObject` | `Assets > Create > Resource Loader > Preloaded Asset Loader` | Key → prefab list in the asset |
| `PreloadedAssetLoader` | `new PreloadedAssetLoader()` | Runtime: fill `PreloadedObjects` or call `AddObject(obj)` (key = object name), then assign to `container.AssetLoader` |
| Custom | Subclass `AssetLoaderObject` | E.g. load a scene and return the screen root |

Assign globally in Settings (`Asset Loader`) or per container via `container.AssetLoader`.

## Settings asset

`Assets > Create > Screen Navigator Settings` creates `UnityScreenNavigatorSettings` and adds it to **Player Settings > Preloaded Assets** (only one is allowed; a second create throws). The class is `internal`; configure it in the Inspector, not from code.

| Field | Default | Purpose |
|---|---|---|
| Sheet Enter/Exit, Page Push/Pop Enter/Exit, Modal Enter/Exit, Modal Backdrop Enter/Exit Animation | built-in | Global `TransitionAnimationObject`s |
| Modal Backdrop Prefab | built-in black 50% | Global backdrop |
| Asset Loader | Resources | Global `AssetLoaderObject` |
| Enable Interaction In Transition | false | Allow clicks during transitions |
| Control Interactions Of All Containers | true | When interaction is disabled, block all containers (true) or only the transitioning one (false) |
| Call Cleanup When Destroy | true | Run `Cleanup` on remaining screens when a container is destroyed |
