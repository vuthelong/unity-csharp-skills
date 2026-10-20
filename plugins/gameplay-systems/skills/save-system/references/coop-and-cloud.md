# Co-op saves and cloud sync

Read when the game has online co-op (host-client), needs per-player data, or ships with Steam Auto-Cloud.

## Who saves what (host-client topology)

| Data | Owner | Saved by | Stored in |
|---|---|---|---|
| World state: terrain edits, buildings, containers, enemies, quests, time of day | Server (host) | Host only | Host's world slot |
| Each player's character in that world: position, health, inventory | Server (host) | Host, keyed by stable player id | Host's world slot, `Players` list |
| Portable character (if the design lets players carry a character between worlds) | Each player | That player's own machine | Separate character slot on the client |
| Settings and key bindings | Each player | Local machine | `pc-settings-menu`, not the save |

Rules:

- Clients never write the host's world. A client that disconnects keeps nothing from the world unless the design has a portable character.
- Key per-player entries by a stable account id (Steam ID, UGS player id), never by the netcode client id. NGO client ids are assigned per connection and do not identify a player across sessions.
- Save on the host only, from server-authoritative state. Snapshot after the network tick that applied the last change, not from client-predicted values.
- Load on the host before starting the session: load the slot, build the world from the DTO, then start hosting so late joiners receive state through replicated variables (see `netcode-for-gameobjects`).
- When a player joins, look up their entry by account id; create a default entry if missing. When they leave, copy their live state back into the world DTO before despawning their objects.
- Host autosave cadence: on a timer (for example every 5 minutes), on sleep or day change, and when a player leaves. Show a small saving indicator; the background write keeps the frame time stable.
- Portable characters are trivially editable by the client. Accept them only if the game is not competitive, or validate ranges on the host when a client joins.
- The host quitting ends the session in this topology. Save synchronously in the quit path before shutting down networking.

## Suggested DTO shape

```csharp
[Serializable]
public sealed class WorldSave
{
    public string WorldId;
    public string HostAccountId;
    public double PlayTimeSeconds;
    public WorldState World = new();
    public List<PlayerSave> Players = new();
}

[Serializable]
public sealed class PlayerSave
{
    public string AccountId;
    public string DisplayName;
    public SavedVector3 Position;
    public float Health;
    public List<ItemStackSave> Inventory = new();
}
```

## Cloud sync

- Keep every file under `Application.persistentDataPath`. Steam Auto-Cloud (configured in the Steamworks partner site, see `steamworks-integration`) can then sync that folder without code changes.
- Sync only `*.json` and `*.json.bak`. Exclude `*.tmp` and `*.corrupt` from the cloud patterns so a half-written or broken file never uploads.
- Cloud quotas are set per app in the partner site. Keep saves small: compact JSON (`Formatting.None`), no derived data, ids instead of copies of static data.
- Cloud sync replaces files between launches. Load saves after the platform layer has initialized, and never cache file contents across a session boundary.
- Two machines playing offline can produce conflicting saves. Conflict handling belongs to the cloud provider (check the current Steam Cloud documentation for how the client presents conflicts); do not try to merge files yourself.
