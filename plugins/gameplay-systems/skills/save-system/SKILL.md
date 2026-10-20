---
name: save-system
description: Builds a crash-safe save game system for Unity 6 - plain C# save DTOs serialized with Newtonsoft Json.NET (com.unity.nuget.newtonsoft-json), a version envelope with a tested migration chain, atomic writes (.tmp then File.Replace with a .bak fallback), capture on the main thread and file IO on a background thread with Awaitable, save slots and rotating autosaves under Application.persistentDataPath, and host-owned world saves for online co-op. Use for "add save/load", "save file corrupted after a crash", "old saves break after an update", autosave hitches, save slots, or deciding who saves in co-op. Not for static design data tables (game-data-pipeline), binary MemoryPack formats (memorypack), player settings or key bindings (pc-settings-menu), Steam Cloud setup (steamworks-integration) or network replication (netcode-for-gameobjects).
license: MIT
metadata:
  category: gameplay-systems
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-persistentDataPath.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.BackgroundThreadAsync.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.MainThreadAsync.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour-destroyCancellationToken.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-wantsToQuit.html, https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html, https://www.newtonsoft.com/json/help/html/SerializationSettings.htm, https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace"
  unity: "6000.0+"
---

# Save System

## Workflow

1. **Install the serializer.** Add `com.unity.nuget.newtonsoft-json` (3.2.x, Json.NET 13.0.x) by name in the Package Manager. Check `Packages/packages-lock.json` first; other Unity packages often pull it in already. Use `memorypack` instead only when save size or speed has been measured as a problem and every reader is C#.
2. **Write save DTOs.** Plain `[Serializable]` C# classes and structs with public fields or auto-properties. Never serialize a `MonoBehaviour`, `ScriptableObject`, `GameObject` or asset reference; store stable ids (string GUIDs, item ids from `game-data-pipeline`) and resolve them on load.
3. **Copy the scripts** and set the current version:
   - [scripts/SaveService.cs](scripts/SaveService.cs): read when wiring save/load; slot paths, version envelope, atomic write with `.bak`, backup fallback, sync and `Awaitable` APIs.
   - [scripts/SaveMigrator.cs](scripts/SaveMigrator.cs): read when changing a DTO shape; `JObject` migration steps chained by version.
4. **Capture on the main thread, write off it.** Build a fresh snapshot DTO from live state (transforms, components, inventories) on the main thread, then hand it to `SaveAsync`. Serialization and IO run on a thread-pool thread.
5. **Load before spawning.** Load the slot, then build the world from the DTO. Treat `SaveLoadSource.Backup` as a warning to show the player ("restored from backup").
6. **Slots and autosave.** Manual slots `slot_1..slot_n`, rotating autosaves `auto_0..auto_2` (write the oldest), plus one quick save. Name slots with letters, digits, `_` and `-` only.
7. **Test the migration chain** in Edit Mode tests with checked-in fixture files from every shipped version.
8. **Co-op:** only the host writes the world save; read [references/coop-and-cloud.md](references/coop-and-cloud.md).

## Save DTO and usage

```csharp
[Serializable]
public sealed class GameSave
{
    public string WorldId;
    public double PlayTimeSeconds;
    public WorldState World = new();
    public List<PlayerSave> Players = new();
}

[Serializable]
public struct SavedVector3
{
    public float X, Y, Z;
    public SavedVector3(Vector3 v) { X = v.x; Y = v.y; Z = v.z; }
    public Vector3 ToVector3() => new(X, Y, Z);
}
```

```csharp
public sealed class SaveController : MonoBehaviour
{
    private SaveService _saves;

    private void Awake()
    {
        var migrator = new SaveMigrator(3, new ISaveMigration[]
        {
            new SaveMigration(1, data => data["PlayTimeSeconds"] = 0.0),
            new SaveMigration(2, data => data["Players"] = new JArray(data["Player"] ?? new JObject())),
        });
        _saves = new SaveService(migrator);
    }

    public async Awaitable AutosaveAsync()
    {
        var snapshot = CaptureSnapshot();
        try
        {
            await _saves.SaveAsync("auto_0", snapshot, destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnApplicationQuit() => _saves.Save("auto_0", CaptureSnapshot());

    private GameSave CaptureSnapshot() => new GameSave();
}
```

The file on disk is an envelope: `{"version":3,"savedAtUtc":"...","data":{...}}`. The service stamps `version` from `SaveMigrator.CurrentVersion`; DTOs do not carry their own version field.

## Rules

- **The snapshot is owned by the save call.** `SaveAsync` serializes on a background thread, so the DTO must not share lists or objects with live game state and must not be mutated until the task completes. Build a new DTO per save.
- **No Unity API on the background thread.** Read `Application.persistentDataPath`, transforms and components on the main thread. `SaveService` captures the root path in its constructor.
- **Atomic write.** Write `<slot>.json.tmp`, flush to disk, then `File.Replace(tmp, slot, slot.bak)`. When the slot does not exist yet, `File.Replace` throws `FileNotFoundException`, so the script uses `File.Move` instead. Keep `.tmp` in the same folder: `File.Replace` throws across volumes.
- **Read falls back to `.bak`.** A corrupt primary is renamed to `.corrupt` before the next save, otherwise the next `File.Replace` would rotate the corrupt file into `.bak` and destroy the last good copy.
- **A save from a newer build** throws `SaveVersionException`; do not fall back to `.bak` and do not overwrite it. Tell the player.
- **Migrations are append-only.** Never edit a shipped step; add `FromVersion = n` to go to `n + 1`. Migrate `JObject` data, not typed DTOs, because the old type no longer exists.
- **Json.NET settings:** `TypeNameHandling.None` (polymorphic type names in user-editable files are a known deserialization risk), `ObjectCreationHandling.Replace` (the default reuses collections created by field initializers and appends to them).
- **Unity structs:** `Vector3`, `Quaternion` and `Color` expose computed properties such as `normalized` that Json.NET can try to serialize, which can produce self-referencing loop errors or bloated files. Use small DTO structs like `SavedVector3`.
- **IL2CPP stripping:** DTO types created only through reflection can lose constructors or members with a high Managed Stripping Level. Add `[UnityEngine.Scripting.Preserve]` or a `link.xml` entry and test a player build.
- **Quit saves are synchronous.** An `Awaitable` save started in `OnApplicationQuit` is not guaranteed to finish. Use `Save`, or cancel quit with `Application.wantsToQuit` until an async save completes.
- **Never block** the main thread on `SaveAsync` (`.GetAwaiter().GetResult()`). See `csharp-unity` (references/async.md).

## Pitfalls

| Symptom | Fix |
|---|---|
| Save is empty or truncated after a crash or power loss | Atomic write through `.tmp` + `File.Replace`; never `File.WriteAllText` on the live file |
| Lists double after every load | `ObjectCreationHandling.Replace` |
| `Self referencing loop detected for property 'normalized'` | Replace Unity structs in DTOs with plain structs |
| Autosave hitch | Snapshot on main thread, serialize and write with `SaveAsync` |
| `UnityException: ... can only be called from the main thread` | A Unity API was read inside the background section; move it into the snapshot step |
| Old saves load with missing data | Missing migration step or edited shipped step; add fixture tests per version |
| Works in Editor, fails in IL2CPP build | Stripped DTO members; `[Preserve]` or `link.xml` |
| Backup silently overwritten by a corrupt file | Quarantine the corrupt primary before the next save (done in `SaveService`) |

## References

- [references/migrations-and-atomic-writes.md](references/migrations-and-atomic-writes.md): read when writing migration steps or tests, or reasoning about crash safety, `File.Replace` platform behavior and recovery cases.
- [references/coop-and-cloud.md](references/coop-and-cloud.md): read when the game has online co-op, needs per-player data, or ships with Steam Auto-Cloud.

## Related skills

- `game-data-pipeline`: static design data the save references by id.
- `memorypack`: binary alternative serializer.
- `pc-settings-menu`: settings and key bindings are stored separately from saves.
- `steamworks-integration`: Steam Auto-Cloud configuration for the save folder.
- `netcode-for-gameobjects`: replicating loaded world state to clients.
- `csharp-unity`: async and house style for the scripts.
