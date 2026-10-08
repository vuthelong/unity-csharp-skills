---
name: observable-collections
description: Builds data-driven Unity UI on Cysharp ObservableCollections - generic, low-allocation observable collections (ObservableList, ObservableDictionary, ObservableHashSet, ObservableQueue, ObservableStack, ObservableRingBuffer, ObservableFixedSizeRingBuffer) with struct NotifyCollectionChangedEventArgs<T>, synchronized views (CreateView, ISynchronizedView, AttachFilter / ResetFilter, Filtered / Unfiltered, ViewChanged), ToViewList / ToNotifyCollectionChanged for IList binding, writable views, and R3 integration (ObserveAdd, ObserveRemove, ObserveReplace, ObserveMove, ObserveReset, ObserveCountChanged). Covers NuGetForUnity install, SyncRoot thread safety, inventory lists driving uGUI or UI Toolkit ListView, pooled item views, and pitfalls such as undisposed views, Clear losing views, and mutating inside callbacks. Use when the user mentions ObservableCollections, ObservableList, ISynchronizedView, CreateView, collection change notifications, or syncing a list model to UI rows.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/ObservableCollections"
  unity: "6000.0+"
---

# ObservableCollections

Observable collections whose change events are generic `readonly ref struct`s (`NotifyCollectionChangedEventArgs<T>` with `ReadOnlySpan<T>` ranges), plus synchronized views that map each item to a view object once and keep it in sync, including sort, reverse and filters. Namespace `ObservableCollections`. Verified against the repository source (`src/ObservableCollections`, `src/ObservableCollections.R3`).

Read `references/api.md` for full member lists of every collection, the view interfaces, event payloads and R3 event structs.

## Workflow

1. Install with NuGetForUnity (there is no UPM package): `NuGet > Manage NuGet Packages`, install `ObservableCollections`; install `ObservableCollections.R3` too if you use R3 (it pulls `R3`; also add `R3.Unity` per `r3`). The netstandard builds depend on `System.Runtime.CompilerServices.Unsafe` (and `System.Memory` for netstandard2.0), which NuGetForUnity resolves; keep only one copy of the Unsafe DLL in the project.
2. Hold the model as an `ObservableList<T>` (or another collection) in a plain C# class or presenter.
3. For UI, create a view: `var view = list.CreateView(item => SpawnRow(item));`. The transform runs once per added item.
4. Handle `view.ViewChanged` to destroy/despawn rows on Remove and to reorder/reactivate on Move, Reset (sort, reverse, clear, filter).
5. Dispose every view and every `ToViewList()` / `ToNotifyCollectionChanged()` list in `OnDestroy` (or with R3 `AddTo`).

## Collections

| Type | Notes |
|---|---|
| `ObservableList<T>` | `Add`, `AddRange`, `Insert`, `InsertRange`, `Remove`, `RemoveAt`, `RemoveRange`, `Move`, `Sort`, `Reverse`, indexer set (Replace) |
| `ObservableDictionary<TKey, TValue>` | items are `KeyValuePair<TKey, TValue>`; indexer set raises Replace |
| `ObservableHashSet<T>` | `Add` returns bool, `AddRange`, `RemoveRange`, set queries |
| `ObservableQueue<T>` | `Enqueue`, `EnqueueRange`, `Dequeue`, `DequeueRange`, `TryDequeue` |
| `ObservableStack<T>` | `Push`, `PushRange`, `Pop`, `PopRange`, `TryPop` |
| `ObservableRingBuffer<T>` | growable deque: `AddFirst`, `AddLast`, `AddLastRange`, `RemoveFirst`, `RemoveLast` |
| `ObservableFixedSizeRingBuffer<T>` | `new(capacity)`; `AddLast` on a full buffer raises Remove(index 0) then Add. Ideal for chat / combat logs |

There is no `ObservableSortedDictionary`. Use `ObservableList<T>.Sort(comparer)` or keep a sorted view yourself.

## Change events

```csharp
list.CollectionChanged += this.OnInventoryChanged;

private void OnInventoryChanged(in NotifyCollectionChangedEventArgs<ItemStack> e)
{
    switch (e.Action)
    {
        case NotifyCollectionChangedAction.Add:
            if (e.IsSingleItem) this.AddRow(e.NewItem, e.NewStartingIndex);
            else for (var i = 0; i < e.NewItems.Length; i++) this.AddRow(e.NewItems[i], e.NewStartingIndex + i);
            break;
        case NotifyCollectionChangedAction.Reset:
            if (e.SortOperation.IsClear) this.ClearRows();
            break;
    }
}
```

- The args are a `readonly ref struct` passed by `in`: no allocation, but you cannot store them, capture them in a lambda, or use them after an `await`. Copy what you need.
- `NotifyCollectionChangedAction` is `System.Collections.Specialized.NotifyCollectionChangedAction`.
- Contract: single vs range is `IsSingleItem` (`NewItem`/`OldItem` vs `NewItems`/`OldItems`). Replace sets both. Move sets `NewStartingIndex` / `OldStartingIndex`. Reset carries `SortOperation` with `IsClear`, `IsReverse`, `IsSort`, `Index`, `Count`, `Comparer`.
- Events fire inside `lock (SyncRoot)` on the thread that mutated the collection.

## Views

| Member | Purpose |
|---|---|
| `CreateView<TView>(Func<T, TView> transform)` | view of `TView`, one per item, kept in source order |
| `view.ViewChanged` | `in SynchronizedViewChangedEventArgs<T, TView>`: `NewItem` / `OldItem` are `(T Value, TView View)` tuples; ranges in `NewValues` / `NewViews` / `OldValues` / `OldViews` |
| `view.AttachFilter(Func<T, bool>)` / `(Func<T, TView, bool>)` / `(ISynchronizedViewFilter<T, TView>)` | replaces the previous filter; raises a Reset |
| `view.ResetFilter()` | remove filter; raises a Reset |
| `view.Count` / `view.UnfilteredCount` | filtered vs total |
| `view.Filtered` / `view.Unfiltered` | `IEnumerable<(T Value, TView View)>` |
| `view.RejectedViewChanged` | `Action<RejectedViewChangedAction, int, int>` for changes hidden by the filter |
| `view.CollectionStateChanged` | `Action<NotifyCollectionChangedAction>` after any change; cheap "something changed" hook |
| `view.ToViewList()` | `ISynchronizedViewList<TView>`: indexer access (views themselves only enumerate) |
| `view.ToNotifyCollectionChanged([dispatcher])` | `NotifyCollectionChangedSynchronizedViewList<TView>`: implements `IList`, `IList<T>`, `INotifyCollectionChanged` |
| `list.CreateWritableView(transform)` + `ToWritableNotifyCollectionChanged(converter)` | two-way binding back into the source (`ObservableList<T>` only) |

Filtering does not destroy views: filtered-out items keep their `TView` (the transform is not re-run). Toggle visibility on Reset by walking `view.Unfiltered` and testing `view.Filter.IsMatch(value, view)`.

## Unity patterns

uGUI rows (pool instead of `Instantiate`/`Destroy`; see `zbase-pooling` and `ui-ugui`):

```csharp
using System.Collections.Specialized;
using ObservableCollections;
using UnityEngine;

public sealed class InventoryPanel : MonoBehaviour
{
    #region Fields
    [SerializeField] private InventoryRow rowPrefab;
    [SerializeField] private Transform content;
    private ObservableList<ItemStack> _items;
    private ISynchronizedView<ItemStack, InventoryRow> _view;
    #endregion

    #region Public API
    public void Bind(ObservableList<ItemStack> items)
    {
        this._items = items;
        this._view = items.CreateView(this.CreateRow);
        this._view.ViewChanged += this.OnViewChanged;
    }
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        if (this._view == null) return;
        this._view.ViewChanged -= this.OnViewChanged;
        this._view.Dispose();
    }
    #endregion

    #region View Sync
    private InventoryRow CreateRow(ItemStack item)
    {
        var row = Instantiate(this.rowPrefab, this.content);
        row.Show(item);
        return row;
    }

    private void OnViewChanged(in SynchronizedViewChangedEventArgs<ItemStack, InventoryRow> e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                this.SyncSiblingOrder();
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.IsSingleItem) Destroy(e.OldItem.View.gameObject);
                else for (var i = 0; i < e.OldViews.Length; i++) Destroy(e.OldViews[i].gameObject);
                break;
            case NotifyCollectionChangedAction.Replace:
                Destroy(e.OldItem.View.gameObject);
                this.SyncSiblingOrder();
                break;
            case NotifyCollectionChangedAction.Move:
            case NotifyCollectionChangedAction.Reset:
                this.SyncSiblingOrder();
                break;
        }
    }

    private void SyncSiblingOrder()
    {
        var index = 0;
        foreach (var pair in this._view.Unfiltered)
        {
            var visible = this._view.Filter.IsMatch(pair.Value, pair.View);
            pair.View.gameObject.SetActive(visible);
            pair.View.transform.SetSiblingIndex(index);
            index++;
        }
    }
    #endregion
}
```

UI Toolkit `ListView` (see `ui-uitk`): `NotifyCollectionChangedSynchronizedViewList<T>` implements non-generic `IList`, so it can be `itemsSource` directly. ListView does not listen to `INotifyCollectionChanged`, so refresh on change:

```csharp
this._rows = this._items.ToNotifyCollectionChanged();
this._listView.itemsSource = this._rows;
this._rows.CollectionChanged += (_, _) => this._listView.RefreshItems();
```

Use `RefreshItems()` for content changes and `Rebuild()` after large structural changes. Dispose `this._rows` when the panel closes. ListView already virtualizes and recycles rows, so the transform should produce lightweight data, not GameObjects.

## R3 integration

With `ObservableCollections.R3` (namespace `ObservableCollections` + `R3`):

```csharp
this._items.ObserveAdd(this.destroyCancellationToken)
    .Subscribe(this, (e, self) => self.PlayPickupSfx(e.Value));

this._items.ObserveCountChanged(notifyCurrentCount: true)
    .Subscribe(this, (count, self) => self.countLabel.text = count.ToString())
    .AddTo(this);
```

`ObserveChanged`, `ObserveAdd`, `ObserveRemove`, `ObserveReplace`, `ObserveMove`, `ObserveReset`, `ObserveClear`, `ObserveReverse`, `ObserveSort`, `ObserveCountChanged(bool notifyCurrentCount = false)` exist on both collections and views (views add `ObserveRejected`). `ObservableDictionary` adds `ObserveDictionaryAdd` / `ObserveDictionaryRemove` / `ObserveDictionaryReplace`. Range operations are emitted one event per item. `ObserveReset` covers Clear, Reverse and Sort. Every method takes an optional `CancellationToken` that completes the stream. See `r3` for disposal rules.

## Pitfalls

- **Undisposed views leak.** A view subscribes to the collection's event; if the collection outlives the UI, the view (and every `TView` it holds) stays alive and keeps receiving events. Dispose views, view lists and notify lists in `OnDestroy`.
- **`Clear()` loses the views before you see them.** The view clears its internal list and then raises Reset, so `OldViews` is empty. Before clearing, iterate `view.Unfiltered` and release the rows, or call `list.RemoveRange(0, list.Count)` on an `ObservableList` to get a Remove event with `OldViews`.
- **Mutating inside a callback.** Events run inside `lock (SyncRoot)`. The lock is re-entrant on the same thread, so a nested `Add` works but raises nested events while the outer handler is mid-way; views and indices the outer handler captured are stale. Defer mutations (queue them, or R3 `.Delay` / UniTask `UniTask.Yield`).
- **Thread safety.** Mutations and enumeration-by-view are synchronized on `SyncRoot`, but handlers run on the mutating thread. Unity objects may only be touched on the main thread: mutate on the main thread, or marshal (R3 `ObserveOnMainThread`, a custom `ICollectionEventDispatcher`). For multi-step reads, `lock (list.SyncRoot) { ... }`. Never block the main thread on that lock while a worker raises events that wait for the main thread.
- **Sort and Move semantics.** `Move(oldIndex, newIndex)` raises one Move. `Sort` / `Reverse` raise a single Reset with `SortOperation.IsSort` / `IsReverse`, not per-item moves; views re-sort their items (including filtered-out ones) and you must re-order the UI yourself. R3 `ObserveMove` does not fire for sorts; use `ObserveSort` / `ObserveReset`.
- **Filters keep views alive.** A filtered-out item's `TView` still exists and is not re-created when it becomes visible again.
- **No LINQ in hot paths.** `CreateView` already projects; iterate `foreach` over `Filtered` / `Unfiltered` / the collection, and index `ReadOnlySpan` ranges with `for`. Enumerating an `ObservableList` holds `SyncRoot` for the whole `foreach` (no snapshot); mutating it mid-loop throws, and other threads block until the loop ends.
- **vs R3 `ReactiveProperty<List<T>>`.** A reactive property only fires when the reference is replaced (or `ForceNotify` is called), with no information about what changed, so every subscriber rebuilds everything. ObservableCollections reports item-level Add / Remove / Replace / Move / Reset with indices, and views keep per-item objects. Use `ReactiveProperty` for scalar state, ObservableCollections for lists the UI renders row by row.

## Related skills

- `r3` for subscriptions, `AddTo`, and main-thread scheduling.
- `ui-ugui` and `ui-uitk` for the row UI itself; `zbase-pooling` for pooling row GameObjects.
- `csharp-unity` for house style.
