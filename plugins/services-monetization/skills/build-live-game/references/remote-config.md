# Remote Config Reference

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
- [FetchConfigsAsync API](#fetchconfigsasync-api)
- [.rc File Format](#rc-file-format)
- [Game Overrides](#game-overrides)
- [Common Patterns](#common-patterns)
- [Code Template -- Load JSON Config](#code-template--load-json-config)

## Overview

Server-side game configuration. Store game definitions (achievement lists, battle pass tiers, shop catalogs) as JSON entries updatable without a client build. Accessed via `RemoteConfigService.Instance`.

- **Namespace:** `Unity.Services.RemoteConfig`
- **Package:** `com.unity.remote-config`

## How It Works

- Configuration is stored as key-value entries in the Unity Dashboard or deployed via `.rc` files through the Deployment Window.
- The client fetches all config at once using `FetchConfigsAsync`.
- Values are strongly typed: `string`, `bool`, `int`, `float`, `long`, and `JSON`.

## FetchConfigsAsync API

```csharp
struct UserAttributes {}
struct AppAttributes {}
await RemoteConfigService.Instance.FetchConfigsAsync(
    new UserAttributes(), new AppAttributes());
var config = RemoteConfigService.Instance.appConfig;
string val = config.GetString("key");
int num = config.GetInt("key");
bool flag = config.GetBool("key");
var token = config.config["key"];
var obj = token.ToObject<MyType>();
```

- Read values from `RemoteConfigService.Instance.appConfig` (a `RuntimeConfig`) after the fetch completes. `RuntimeConfig` exposes the typed getters (`GetString`, `GetInt`, `GetBool`, `GetFloat`, `GetLong`, `GetJson`, each with an optional default); its `config` property is the raw `JObject`, so JSON entries are read with `appConfig.config["key"]`.
- `UserAttributes` and `AppAttributes` are empty structs unless you use Game Overrides (audience targeting).
- Every `FetchConfigsAsync` call sends a request; it does not short-circuit on unchanged attributes. `appConfig` holds the last successfully fetched values, which are also cached on device so they are available offline or when a fetch fails (check `appConfig.origin`: `Default`, `Cached`, or `Remote`). Verify these details against the installed `com.unity.remote-config` version.

## .rc File Format

```json
{
  "$schema": "https://ugs-config-schemas.unity3d.com/v1/remote-config.schema.json",
  "entries": {
    "my_string_key": "hello",
    "my_int_key": 42,
    "my_json_key": { "nested": true }
  },
  "types": {
    "my_json_key": "JSON"
  }
}
```

- **Create via:** Right-click in Project window > Assets > Create > Services > Remote Config
- **Deploy via:** the Deployment Window (Services > Deployment)
- The `types` block is needed only for JSON entries; `string`, `int`, `bool`, `float`, and `long` are auto-detected.

## Game Overrides

Game Overrides (via `com.unity.services.tooling`) override Remote Config values for specific player segments. Remote Config itself does not provide A/B testing -- Game Overrides layer on top of it.

- Game Overrides are defined as `.ugo` files.
- Used for A/B testing and audience targeting.
- Configured in the Unity Dashboard or deployed via `.ugo` files through the Deployment Window.
- When active, `FetchConfigsAsync` returns the overridden values automatically.

## Common Patterns

- **Game definitions** (achievements, battle pass tiers) stored as JSON arrays under a single key.
- **Feature flags** stored as booleans for toggling game features server-side.
- **Version-specific config** with server-side updates -- change game balance without shipping a new client build.

## Code Template -- Load JSON Config

```csharp
using Unity.Services.RemoteConfig;
using Newtonsoft.Json.Linq;

struct UserAttributes {}
struct AppAttributes {}

async Task<List<T>> LoadJsonConfigAsync<T>(string key)
{
    await RemoteConfigService.Instance.FetchConfigsAsync(
        new UserAttributes(), new AppAttributes());
    var token = RemoteConfigService.Instance.appConfig.config[key];
    return token.ToObject<List<T>>();
}
```
