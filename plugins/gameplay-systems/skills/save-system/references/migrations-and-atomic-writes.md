# Migrations and atomic writes

Read when writing migration steps or tests, or reasoning about crash safety, `File.Replace` behavior and recovery.

## File layout

```
<persistentDataPath>/Saves/
  slot_1.json          current save (envelope)
  slot_1.json.bak      previous good save, rotated by File.Replace
  slot_1.json.tmp      only exists during a write or after a crash mid-write
  slot_1.json.corrupt  primary that failed to parse, kept for bug reports
```

`Application.persistentDataPath` per the Unity 6 manual: Windows `%userprofile%\AppData\LocalLow\<company>\<product>`, macOS player `~/Library/Application Support/unity.<company>.<product>` (the Editor path is used if that folder already exists), Linux `$XDG_CONFIG_HOME/unity3d/<company>/<product>` (falls back to `~/.config`). Changing Company Name or Product Name in Player Settings moves the folder, so existing players lose their saves; never rename after release without a copy step.

## Write sequence

1. Serialize the envelope into `slot.json.tmp` with `FileMode.Create` (truncates a leftover tmp).
2. `FileStream.Flush(true)` so the bytes reach the disk before the swap, not only the OS cache.
3. If `slot.json` does not exist: `File.Move(tmp, slot)`.
4. Otherwise `File.Replace(tmp, slot, slot.bak, ignoreMetadataErrors: true)`: the old `slot.json` becomes `slot.json.bak` (an existing `.bak` is overwritten) and `tmp` becomes `slot.json`.

Facts from the .NET `File.Replace` reference:

- Throws `FileNotFoundException` when the source or destination file does not exist, which is why step 3 exists.
- Throws when source and destination are on different volumes, so `.tmp` must sit next to the target.
- Passing `null` as the backup path skips the backup.
- Can throw `PlatformNotSupportedException` / `UnauthorizedAccessException` where the operation is unsupported. `SaveService` falls back to copy-to-bak, delete, move on `PlatformNotSupportedException`.

Unity runs its own Mono / IL2CPP class library, not the desktop .NET runtime, and the .NET reference does not describe how `File.Replace` maps to each OS. Test the full sequence in a player build on every shipping platform (Windows, Linux / Steam Deck, macOS), including a run where the slot does not exist yet.

`File.Move(string, string, bool overwrite)` is not available under the .NET Standard 2.1 API profile, so the scripts only call the two-argument `File.Move` on a path that does not exist.

## Crash outcomes

| Crash point | State on disk | Next load |
|---|---|---|
| During tmp write | old `slot.json`, partial `.tmp` | primary loads; tmp is truncated by the next save |
| After tmp, before swap | old `slot.json`, complete `.tmp` | primary loads (one save lost) |
| Inside fallback, after delete | `.bak`, `.tmp`, no primary | `.bak` loads |
| After swap | new `slot.json`, old `.bak` | primary loads |

A primary that exists but does not parse is moved to `.corrupt` after the backup loads, so the next `File.Replace` rotates a good file into `.bak`.

Windows can briefly lock a file that antivirus or a cloud sync client is scanning, which surfaces as `IOException` from `File.Replace`. If players report this, retry the swap a few times with a short delay inside the background section; the `.tmp` is complete at that point.

## Migration steps

Rules:

- `SaveMigrator(currentVersion, steps)` requires exactly one step for every version `1 .. currentVersion - 1`; the constructor throws otherwise, so a gap fails at startup instead of at a player's load.
- A step mutates the `data` object of the envelope in place. Use `JObject` APIs: rename (`data["New"] = data["Old"]; data.Remove("Old");`), default (`data["Hp"] ??= 100;`), restructure (`new JArray(...)`), convert (`(int)data["Gold"] * 100`).
- Never reference current DTO types inside a step. They change later; the step must keep producing the shape of its target version.
- Never edit a step that has shipped. Fix bugs with a new step.
- Bump `currentVersion` in the same commit that changes a DTO shape. Additive changes with safe defaults (new field, default value fine) need no step when `MissingMemberHandling.Ignore` and field initializers cover them, but still bump the version if a later step will need to know.

```csharp
var migrator = new SaveMigrator(4, new ISaveMigration[]
{
    new SaveMigration(1, data => data["PlayTimeSeconds"] ??= 0.0),
    new SaveMigration(2, data =>
    {
        data["Players"] = new JArray(data["Player"] ?? new JObject());
        data.Remove("Player");
    }),
    new SaveMigration(3, data =>
    {
        foreach (var player in data["Players"].Children<JObject>())
        {
            player["Gold"] = (long)(player["Gold"] ?? 0) * 100;
        }
    }),
});
```

## Testing

Keep one fixture file per shipped version under a test folder (for example `Assets/Tests/EditMode/SaveFixtures/v1.json`). Copy real saves from each release build, not hand-written JSON.

```csharp
[TestCase("v1.json")]
[TestCase("v2.json")]
[TestCase("v3.json")]
public void OldSave_MigratesToCurrent(string fixture)
{
    var root = JObject.Parse(File.ReadAllText(Path.Combine(FixtureFolder, fixture)));
    var data = (JObject)root["data"];
    Migrators.Create().Migrate(data, (int)root["version"]);
    var save = data.ToObject<GameSave>(JsonSerializer.Create(SaveService.CreateDefaultSettings()));
    Assert.That(save.Players, Is.Not.Empty);
}
```

Also test, against a temp folder: save then load round-trip; corrupt primary loads `.bak` and creates `.corrupt`; a version newer than `CurrentVersion` throws `SaveVersionException`; a leftover `.tmp` does not break the next save. `SaveService` reads `Application.persistentDataPath`, so construct it in Play Mode or Edit Mode tests running inside the Editor.
