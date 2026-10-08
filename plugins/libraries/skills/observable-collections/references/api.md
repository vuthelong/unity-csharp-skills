# ObservableCollections API reference

Namespace `ObservableCollections` (R3 extensions: same namespace, assembly `ObservableCollections.R3`). Checked against the repository's `src/` tree.

## Common interface

```csharp
public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);

public interface IObservableCollection<T> : IReadOnlyCollection<T>
{
    event NotifyCollectionChangedEventHandler<T>? CollectionChanged;
    object SyncRoot { get; }
    ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform);
}
```

Also `IReadOnlyObservableList<T>` (`IReadOnlyList<T>` + observable) and `IReadOnlyObservableDictionary<TKey, TValue>`; expose these from models so callers cannot mutate.

## Collections and members

| Type | Constructors | Mutators (all raise events) | Other |
|---|---|---|---|
| `ObservableList<T>` | `()`, `(int capacity)`, `(IEnumerable<T>)` | `Add`, `AddRange(T[] / ReadOnlySpan<T> / IEnumerable<T>)`, `Insert`, `InsertRange(...)`, `Remove`, `RemoveAt`, `RemoveRange(int index, int count)`, `Move(int oldIndex, int newIndex)`, `Clear`, `Sort()`, `Sort(IComparer<T>)`, `Sort(int, int, IComparer<T>)`, `Reverse()`, `Reverse(int, int)`, `this[int] set` | `IndexOf`, `Contains`, `ForEach(Action<T>)`, `CopyTo`, `CreateWritableView`, `ToWritableNotifyCollectionChanged(...)`, `ToNotifyCollectionChangedSlim([dispatcher])` |
| `ObservableDictionary<TKey, TValue>` | `()`, `(IEqualityComparer<TKey>?)`, `(int capacity, IEqualityComparer<TKey>?)`, `(IEnumerable<KeyValuePair<TKey,TValue>>[, comparer])` | `Add(key, value)`, `Add(KeyValuePair)`, `Remove(key)`, `Remove(KeyValuePair)`, `Clear`, `this[key] set` (Add or Replace) | `TryGetValue`, `ContainsKey`; `Keys` / `Values` via the `IDictionary` / `IReadOnlyDictionary` interfaces (explicit implementations) |
| `ObservableHashSet<T>` | `()`, `(int capacity[, comparer])`, `(IEqualityComparer<T>?)`, `(IEnumerable<T>[, comparer])` | `Add` (bool), `AddRange(...)`, `Remove`, `RemoveRange(...)`, `Clear` | `Contains`, `TryGetValue`, `IsSubsetOf`, `IsSupersetOf`, `IsProperSubsetOf`, `IsProperSupersetOf`, `Overlaps`, `SetEquals` |
| `ObservableQueue<T>` | `()`, `(int capacity)`, `(IEnumerable<T>)` | `Enqueue`, `EnqueueRange(...)`, `Dequeue`, `TryDequeue`, `DequeueRange(int count)`, `DequeueRange(Span<T> dest)`, `Clear` | `Peek`, `TryPeek`, `ToArray`, `TrimExcess` |
| `ObservableStack<T>` | `()`, `(int capacity)`, `(IEnumerable<T>)` | `Push`, `PushRange(...)`, `Pop`, `TryPop`, `PopRange(int count)`, `PopRange(Span<T> dest)`, `Clear` | `Peek`, `TryPeek`, `ToArray`, `TrimExcess` |
| `ObservableRingBuffer<T>` | `()`, `(IEnumerable<T>)` | `AddFirst`, `AddLast`, `AddLastRange(...)`, `RemoveFirst`, `RemoveLast`, `Clear`, `this[int] set` | `IndexOf`, `Contains`, `BinarySearch`, `ToArray` |
| `ObservableFixedSizeRingBuffer<T>` | `(int capacity)`, `(int capacity, IEnumerable<T>)` | same as ring buffer; adding to a full buffer first removes from the opposite end | same |

`RingBuffer<T>` (non-observable) and `AlternateIndexList<T>` are also public helpers.

## Event payloads

```csharp
public readonly ref struct NotifyCollectionChangedEventArgs<T>
{
    public readonly NotifyCollectionChangedAction Action;
    public readonly bool IsSingleItem;
    public readonly T NewItem;
    public readonly T OldItem;
    public readonly ReadOnlySpan<T> NewItems;
    public readonly ReadOnlySpan<T> OldItems;
    public readonly int NewStartingIndex;
    public readonly int OldStartingIndex;
    public readonly SortOperation<T> SortOperation;
}

public readonly struct SortOperation<T>
{
    public readonly int Index;
    public readonly int Count;
    public readonly IComparer<T>? Comparer;
    public bool IsReverse { get; }
    public bool IsClear { get; }
    public bool IsSort { get; }
}
```

| Action | Fields set |
|---|---|
| Add | `NewItem` / `NewItems`, `NewStartingIndex` |
| Remove | `OldItem` / `OldItems`, `OldStartingIndex` |
| Replace | `NewItem`, `OldItem` (and spans), both indices equal |
| Move | `NewItem`, `NewStartingIndex`, `OldStartingIndex` |
| Reset | `SortOperation` (`IsClear` for Clear, `IsReverse` with `Index`/`Count`, `IsSort` with `Index`/`Count`/`Comparer`) |

## Views

```csharp
public delegate void NotifyViewChangedEventHandler<T, TView>(in SynchronizedViewChangedEventArgs<T, TView> e);

public interface ISynchronizedView<T, TView> : IReadOnlyCollection<TView>, IDisposable
{
    object SyncRoot { get; }
    ISynchronizedViewFilter<T, TView> Filter { get; }
    IEnumerable<(T Value, TView View)> Filtered { get; }
    IEnumerable<(T Value, TView View)> Unfiltered { get; }
    int UnfilteredCount { get; }
    event NotifyViewChangedEventHandler<T, TView>? ViewChanged;
    event Action<RejectedViewChangedAction, int, int>? RejectedViewChanged;
    event Action<NotifyCollectionChangedAction>? CollectionStateChanged;
    void AttachFilter(ISynchronizedViewFilter<T, TView> filter);
    void ResetFilter();
    ISynchronizedViewList<TView> ToViewList();
    NotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged();
    NotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher);
}
```

`SynchronizedViewChangedEventArgs<T, TView>` mirrors the collection args with `(T Value, TView View) NewItem / OldItem` and spans `NewValues`, `NewViews`, `OldValues`, `OldViews`.

Filters: `ISynchronizedViewFilter<T, TView> { bool IsMatch(T value, TView view); }`; extension `AttachFilter(Func<T, bool>)` and `AttachFilter(Func<T, TView, bool>)`. With no filter attached, `Filter` is a pass-through (`IsMatch` returns true). `RejectedViewChangedAction`: `Add`, `Remove`, `Move`.

Shortcuts on any `IObservableCollection<T>` (no filter, faster): `ToViewList()`, `ToViewList(transform)`, `ToNotifyCollectionChanged()`, `ToNotifyCollectionChanged(dispatcher)`, `ToNotifyCollectionChanged(transform[, dispatcher])`.

`NotifyCollectionChangedSynchronizedViewList<TView>` (abstract class): `IList<TView>`, `IList`, `IReadOnlyList<TView>`, `INotifyCollectionChanged`, `INotifyPropertyChanged`, `IDisposable`; `SyncRoot`, indexer, `CollectionChanged`, `PropertyChanged`. Read-only unless created from a writable view.

Dispatchers: `ICollectionEventDispatcher.Post(CollectionEventDispatcherEventArgs ev)`; built-in `SynchronizationContextCollectionEventDispatcher.Current` captures `SynchronizationContext.Current` when the type is first initialized and throws `InvalidOperationException` if that happens on a thread without a context, so touch it first on Unity's main thread (or construct `new SynchronizationContextCollectionEventDispatcher(context)` yourself). It sends synchronously when called on a thread with a context and posts otherwise. Call `ev.Invoke()` inside your dispatcher.

## Writable views (`ObservableList<T>`)

- `CreateWritableView<TView>(Func<T, TView> transform)` returns `IWritableSynchronizedView<T, TView>` with `GetAt`, `SetViewAt`, `SetToSourceCollection`, `AddToSourceCollection`, `InsertIntoSourceCollection`, `RemoveFromSourceCollection`, `RemoveAtSourceCollection`, `ClearSourceCollection`, `ToWritableViewList(converter)`, `ToWritableNotifyCollectionChanged(...)`.
- `delegate T WritableViewChangedEventHandler<T, TView>(TView newView, T originalValue, ref bool setValue)`: `setValue == true` means "set" (return the updated original; set `setValue = false` to skip writing back), `false` means "add" (return a new `T`).

## R3 extensions (`ObservableCollections.R3`)

| Method (on `IObservableCollection<T>` and `ISynchronizedView<T, TView>`) | Element |
|---|---|
| `ObserveChanged(ct)` | `CollectionChangedEvent<T>` / `ViewChangedEvent<T, TView>` |
| `ObserveAdd(ct)` | `CollectionAddEvent<T>(int Index, T Value)` |
| `ObserveRemove(ct)` | `CollectionRemoveEvent<T>(int Index, T Value)` |
| `ObserveReplace(ct)` | `CollectionReplaceEvent<T>(int Index, T OldValue, T NewValue)` |
| `ObserveMove(ct)` | `CollectionMoveEvent<T>(int OldIndex, int NewIndex, T Value)` |
| `ObserveReset(ct)` | `CollectionResetEvent<T>` (`IsClear`, `IsSort`, `IsReverse`, `Index`, `Count`) |
| `ObserveClear(ct)` | `Unit` |
| `ObserveReverse(ct)` | `(int Index, int Count)` |
| `ObserveSort(ct)` | `(int Index, int Count, IComparer<T>? Comparer)` |
| `ObserveCountChanged(bool notifyCurrentCount = false, ct)` | `int` |
| `ObserveRejected(ct)` (views only) | `RejectedViewChangedEvent(Action, NewIndex, OldIndex)` |

For views, Add / Remove / Replace / Move elements are `(T Value, TView View)` tuples. Dictionaries add `ObserveDictionaryAdd` (`DictionaryAddEvent<TKey,TValue>(Key, Value)`), `ObserveDictionaryRemove`, `ObserveDictionaryReplace` (`Key, OldValue, NewValue`). Range changes emit one element per item.
