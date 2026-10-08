# Transport, platforms and alternatives

Verified against MagicOnion 7.11.0 `docs/docs/supported-platforms.md`, `installation/unity.md`, `integration/unity-webgl.md`, `integration/blazor.md`, `fundamentals/aot.md`, `advanced/memorypack.md`.

## Client platform matrix

| Target | Status | Transport | Codegen |
|---|---|---|---|
| Unity Editor (Mono) | supported | YetAnotherHttpHandler + `Grpc.Net.Client` | dynamic (Reflection.Emit) works, generated code preferred |
| Windows / macOS / Linux standalone, Mono | supported | YetAnotherHttpHandler | dynamic or generated |
| Windows / macOS standalone, IL2CPP | supported | YetAnotherHttpHandler | generated only (`[MagicOnionClientGeneration]`) |
| iOS (IL2CPP) | supported | YetAnotherHttpHandler (native library) | generated only |
| Android (IL2CPP) | supported | YetAnotherHttpHandler | generated only |
| WebGL | experimental | GrpcWebSocketBridge (gRPC over WebSocket) on client and server | generated only; no client heartbeat |
| Consoles | not supported | - | - |
| Dedicated server build of Unity as a MagicOnion client | same as its desktop target | YetAnotherHttpHandler | per scripting backend |

- `MagicOnionClientFactoryProvider.Default` is the dynamic factory where `RuntimeFeature.IsDynamicCodeSupported` (netstandard2.0 build always selects it); on IL2CPP only generated factories work, and the generated initializer registers them automatically at load.
- The C-core `Grpc.Core` Unity package is discontinued; do not mix it with YetAnotherHttpHandler.
- Unity's built-in `UnityWebRequest` and Mono's `HttpClientHandler` do not provide HTTP/2 with trailers, which gRPC needs; that is why YetAnotherHttpHandler exists.

## Transport packages

| Piece | Install | Key types |
|---|---|---|
| YetAnotherHttpHandler | UPM `https://github.com/Cysharp/YetAnotherHttpHandler.git?path=src/YetAnotherHttpHandler#1.11.6` + NuGet `System.IO.Pipelines` | `Cysharp.Net.Http.YetAnotherHttpHandler` (`Http2Only`, root certificate options) |
| grpc-dotnet client | NuGet `Grpc.Net.Client` (transitive from `MagicOnion.Client`) | `GrpcChannel`, `GrpcChannelOptions` (`HttpHandler`, `DisposeHttpClient`, `MaxReceiveMessageSize`) |
| MagicOnion Unity integration | UPM `https://github.com/Cysharp/MagicOnion.git?path=src/MagicOnion.Client.Unity/Assets/Scripts/MagicOnion.Client.Unity#7.11.0` | `MagicOnion.GrpcChannelx`, `MagicOnion.Unity.GrpcChannelProviderHost`, `DefaultGrpcChannelProvider`, `GrpcNetClientGrpcChannelProvider`, `GrpcChannelTarget` |
| WebGL bridge | see `https://github.com/Cysharp/GrpcWebSocketBridge` (latest tag 1.4.1) | client channel over WebSocket; server middleware in ASP.NET Core |

`GrpcChannelx` members: `ForAddress(string)`, `ForAddress(Uri)`, `ForTarget(GrpcChannelTarget)`, `Dispose()`, `DisposeAsync()`, `TargetUri`, `Id`. Diagnostics (`Window > MagicOnion > gRPC Channels`, byte counters) exist in the Editor or with the `MAGICONION_ENABLE_CHANNEL_DIAGNOSTICS` define. `GrpcChannelx.ConnectAsync` is obsolete and does nothing.

## WebGL

- Browsers cannot open raw HTTP/2 gRPC streams, and Unity WebGL has no sockets or threads.
- gRPC-Web (`Grpc.AspNetCore.Web` + `Grpc.Net.Client.Web`) is an option for .NET Blazor clients but supports Unary only; StreamingHub needs duplex streaming, which gRPC-Web lacks. It is not documented for Unity WebGL.
- GrpcWebSocketBridge carries gRPC over WebSocket, supports StreamingHub, and is the documented Unity WebGL path. It is not compatible with standard gRPC-Web proxies or non-.NET gRPC-Web clients.
- On WebGL, the client restores the captured `SynchronizationContext` carefully (continuations must stay inline). Do not enable client heartbeat there.
- If WebGL is a primary target, weigh a WebSocket-native stack (Netcode for GameObjects with the WebSocket transport, or a custom WebSocket server) against the experimental status.

## Serializer options

| | MessagePack-CSharp (default) | MemoryPack (preview) |
|---|---|---|
| Package | built into `MagicOnion.Client` / `MagicOnion.Abstractions` | `MagicOnion.Serialization.MemoryPack` (client + server) + MemoryPack |
| Contract | `[MessagePackObject]`, `[Key(int)]` | `[MemoryPackable] partial` |
| Switch | default | `MagicOnionSerializerProvider.Default = MemoryPackMagicOnionSerializerProvider.Instance`, or per client: `MagicOnionClient.Create<T>(channel, MemoryPackMagicOnionSerializerProvider.Instance)` / `StreamingHubClient.ConnectAsync<,>(channel, receiver, serializerProvider: ...)` |
| Source generator | `[MagicOnionClientGeneration(typeof(X))]`, register `.Resolver` in `StaticCompositeResolver` | `[MagicOnionClientGeneration(typeof(X), Serializer = MagicOnionClientGenerationAttribute.GenerateSerializerType.MemoryPack)]` + `MagicOnionMemoryPackFormatterProvider.RegisterFormatters()` at startup |
| Interop | language-neutral format | C# only |
| Version tolerance | int keys; add new keys at the end | add members at the end; `[MemoryPackable(GenerateType.VersionTolerant)]` for more |

Unity types (`Vector3`, `Quaternion`) with MessagePack need the MessagePack Unity package on the client and the matching extension on the server; see `messagepack-csharp`. With MemoryPack, see `memorypack`.

## MagicOnion vs Netcode for GameObjects vs Unity Multiplayer Services

| | MagicOnion | Netcode for GameObjects (NGO) | Unity Multiplayer Services |
|---|---|---|---|
| Model | RPC + server push; your own ASP.NET Core server | GameObject replication (`NetworkObject`, `NetworkVariable`, RPCs) between Unity peers | managed backend: Sessions, Lobby, Relay, Matchmaker, Multiplay hosting |
| Server | plain .NET 8+ process, scales like a web app (Redis/NATS groups) | a Unity instance (host or dedicated server build) | Unity-hosted services; game server is NGO/Netcode for Entities or your own |
| Transport | gRPC over HTTP/2 (TCP); WebSocket bridge on WebGL | Unity Transport (UDP, WebSocket), Relay | Relay (UDP/DTLS, WebSocket) |
| Strength | reliable typed APIs, server-authoritative logic in .NET, shared C# contracts, turn-based / card / social / MMO-style backends | real-time physics/movement sync, prediction-friendly tick data, no separate backend code | matchmaking, NAT traversal, lobbies, hosting without running infrastructure |
| Weakness | TCP head-of-line blocking; no built-in state replication, interpolation or prediction | needs a Unity server; awkward for persistent backend logic | vendor service, per-use pricing; not an RPC framework |
| Use when | turn-based, async PvP, chat, matchmaking/backend APIs, slower real-time (<= ~20 Hz), or an existing .NET backend | fast action games needing sub-100 ms state sync | you need sessions/relay/lobbies quickly; see `setup-multiplayer-services` |

Common hybrid: MagicOnion for account, inventory, matchmaking and chat; NGO + Multiplayer Services Relay for the in-match simulation.
