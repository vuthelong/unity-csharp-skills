# Server setup

Verified against MagicOnion 7.11.0 `docs/docs/quickstart-unity.md`, `fundamentals/https.md`, `fundamentals/authentication.md`, `advanced/magiconionoptions.md`, `src/MagicOnion.Server/Extensions/*`.

## Solution layout (PowerShell)

```pwsh
$MO_PROJECT_NAME="MyApp"

dotnet new gitignore
dotnet new grpc -o "src/$MO_PROJECT_NAME.Server" -n "$MO_PROJECT_NAME.Server"
dotnet new classlib -f netstandard2.1 -o "src/$MO_PROJECT_NAME.Shared" -n "$MO_PROJECT_NAME.Shared"

dotnet new sln -n "$MO_PROJECT_NAME"
dotnet sln add "src/$MO_PROJECT_NAME.Server" --in-root
dotnet sln add "src/$MO_PROJECT_NAME.Shared" --in-root

pushd "src/$MO_PROJECT_NAME.Server"
dotnet remove package Grpc.AspNetCore
dotnet add package MagicOnion.Server
dotnet add reference "../$MO_PROJECT_NAME.Shared"
popd

pushd "src/$MO_PROJECT_NAME.Shared"
dotnet add package MagicOnion.Abstractions
popd
```

Then delete the template's `Protos/`, `Services/GreeterService.cs`, `Class1.cs`, and any `<Protobuf Include=...>` item in the server csproj. Unity project goes in `src/MyApp.Unity` (create from Unity Hub). Template with everything wired: `https://github.com/Cysharp/MagicOnion.Template.Unity` (CC0, run `init.cmd MyApp` / `bash init.sh MyApp`).

| Package | Use |
|---|---|
| `MagicOnion.Server` | server (.NET 8+, not Native AOT) |
| `MagicOnion.Client` | .NET / Unity client, includes the source generator |
| `MagicOnion.Abstractions` | shared interface library |
| `MagicOnion` | meta package for server-to-server (both roles) |
| `MagicOnion.Serialization.MemoryPack` | MemoryPack serializer (preview) |
| `MagicOnion.Server.Redis` | Redis group backplane (`UseRedisGroup`) |

## Shared project as a Unity local package

`src/MyApp.Shared/package.json`:

```json
{
  "name": "com.yourco.myapp.shared.unity",
  "version": "1.0.0",
  "displayName": "MyApp.Shared.Unity",
  "description": "MyApp.Shared.Unity"
}
```

`src/MyApp.Shared/MyApp.Shared.Unity.asmdef`:

```json
{
    "name": "MyApp.Shared.Unity"
}
```

`src/MyApp.Shared/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <ArtifactsPath>$(MSBuildThisFileDirectory).artifacts</ArtifactsPath>
  </PropertyGroup>
</Project>
```

`src/MyApp.Shared/Directory.Build.targets`:

```xml
<Project>
  <ItemGroup>
    <None Remove="**\*.meta" />
  </ItemGroup>
  <ItemGroup>
    <None Remove=".artifacts\**\**.*" />
    <None Remove="obj\**\*.*;bin\**\*.*" />
    <Compile Remove=".artifacts\**\**.*" />
    <Compile Remove="bin\**\*.*;obj\**\*.*" />
    <EmbeddedResource Remove=".artifacts\**\**.*" />
    <EmbeddedResource Remove="bin\**\*.*;obj\**\*.*" />
  </ItemGroup>
</Project>
```

Build once and delete any leftover `bin`/`obj`; Unity imports `.cs` files it finds there. `.artifacts` starts with a dot, so Unity ignores it. Unity `Packages/manifest.json`: `"com.yourco.myapp.shared.unity": "file:../../MyApp.Shared"`.

Shared code compiles in two worlds: netstandard2.1 (C# from the .NET SDK) and Unity (C# 9). Avoid C# 10+ syntax (file-scoped namespaces, `record struct`, global usings) in Shared.

Optional: `https://github.com/Cysharp/SlnMerge.git?path=src` merges `MyApp.sln` into the Unity-generated solution via `MyApp.Unity.sln.mergesettings` (`<SlnMergeSettings><MergeTargetSolution>..\..\MyApp.sln</MergeTargetSolution></SlnMergeSettings>`), giving cross-project navigation and debugging.

## Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMagicOnion(options =>
{
    options.IsReturnExceptionStackTraceInErrorDetail = builder.Environment.IsDevelopment();
    options.EnableStreamingHubHeartbeat = true;
    options.StreamingHubHeartbeatInterval = TimeSpan.FromSeconds(15);
    options.StreamingHubHeartbeatTimeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();
app.MapMagicOnionService();
app.Run();
```

- `AddMagicOnion(...)` overloads: `(Action<MagicOnionOptions>?)`, `(Assembly[] searchAssemblies, ...)`, `(IEnumerable<Type> searchTypes, ...)`. It calls `services.AddGrpc()` internally and returns `IMagicOnionServerBuilder`.
- `MapMagicOnionService()` maps every service/hub found; `MapMagicOnionService<T>()`, `MapMagicOnionService(params Type[])`, `MapMagicOnionService(params Assembly[])` restrict it. It returns `IEndpointConventionBuilder`, so `.RequireAuthorization()` etc. work.
- Services and hubs get constructor injection from the ASP.NET Core container.
- gRPC message size: `builder.Services.AddGrpc(o => { o.MaxReceiveMessageSize = 8 * 1024 * 1024; });` (default 4 MB). Match on the client with `GrpcChannelOptions.MaxReceiveMessageSize` / `MaxSendMessageSize`.

### MagicOnionOptions

| Property | Default | Notes |
|---|---|---|
| `GlobalFilters` | empty | Unary filters, also run once at hub connect |
| `GlobalStreamingHubFilters` | empty | per hub method call |
| `IsReturnExceptionStackTraceInErrorDetail` | false | dev only; leaks stack traces |
| `EnableCurrentContext` | false | `ServiceContext.Current` via AsyncLocal |
| `EnableStreamingHubHeartbeat` | false | with `StreamingHubHeartbeatInterval` / `StreamingHubHeartbeatTimeout` |
| `ClientResultsDefaultTimeout` | 5 s | server-to-client `Task<T>` receiver calls |
| `StreamingHubResponseQueueMaxLength` | 1024 | per connection; overflow aborts the call |
| `StreamingHubResponseQueueMaxSize` | 16 MiB | per connection; one oversized message also aborts |

Also bindable from the `MagicOnion` section of `appsettings.json`.

## HTTP/2 and TLS

gRPC requires HTTP/2. Two options:

| Mode | Server | Unity client |
|---|---|---|
| TLS (production) | HTTPS endpoint, `Http1AndHttp2` or `Http2`, real certificate | `https://` address, no `Http2Only`; YetAnotherHttpHandler validates against its own bundled roots |
| h2c cleartext (dev, or TLS terminated by an HTTP/2-capable proxy that speaks h2c upstream) | endpoint with `Protocols: Http2` only | `http://` address, `YetAnotherHttpHandler { Http2Only = true }` |

```json
{
  "Kestrel": {
    "Endpoints": {
      "Grpc":  { "Url": "http://0.0.0.0:5000",  "Protocols": "Http2" },
      "Https": { "Url": "https://0.0.0.0:5001", "Protocols": "Http1AndHttp2" }
    }
  }
}
```

Or in code: `builder.WebHost.ConfigureKestrel(o => o.ConfigureEndpointDefaults(e => e.Protocols = HttpProtocols.Http2));`. A cleartext port cannot serve HTTP/1 and HTTP/2 at once (no ALPN without TLS); use separate ports.

- `localhost` from a phone means the phone. Use the PC's LAN IP and bind Kestrel to `0.0.0.0`.
- iOS ATS and Android 9+ block cleartext by default for platform HTTP stacks; YetAnotherHttpHandler uses its own native stack, but store review and production builds should still use TLS.
- Development certificate errors (`invalid peer certificate: UnknownIssuer`): use the `http://` h2c port in development, or configure YetAnotherHttpHandler's root certificates (see its README "Advanced").
- Load balancers must forward HTTP/2 end to end (AWS ALB with gRPC target group, GCP HTTP(S) LB with HTTP/2 backends, nginx `grpc_pass`). Long-lived hub streams need idle timeouts above your heartbeat interval.

## Authentication

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(secret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };
    });
builder.Services.AddAuthorization();

app.UseAuthentication();
app.UseAuthorization();
app.MapMagicOnionService();
```

```csharp
[Authorize]
public class ProfileService : ServiceBase<IProfileService>, IProfileService
{
    public async UnaryResult<string> GetNameAsync()
        => Context.CallContext.GetHttpContext().User.Identity?.Name ?? "";

    [AllowAnonymous]
    public async UnaryResult<string> PingAsync() => "pong";
}
```

- Issue tokens from an `[AllowAnonymous]` Unary method or a separate HTTP endpoint (`System.IdentityModel.Tokens.Jwt`). Sample: `samples/JwtAuthentication` in the MagicOnion repo.
- `[Authorize]` on a `StreamingHubBase` class is checked at connect only.
- Without ASP.NET Core auth, write a `MagicOnionFilterAttribute` that reads `context.CallContext.GetHttpContext().Request.Headers["Authorization"]` and sets `context.Status = new Status(StatusCode.Unauthenticated, "...")` and returns without calling `next`.
- Read request metadata: `Context.CallContext.RequestHeaders.GetValue("x-key")`. Write response headers: `Context.CallContext.WriteResponseHeadersAsync(new Metadata { ... })`. Client reads them with `await unaryResult.ResponseHeadersAsync`.

## Filters

```csharp
public sealed class TimingFilter(ILogger<TimingFilter> logger) : IMagicOnionServiceFilter
{
    public async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation("{Service}/{Method} took {Elapsed}", context.ServiceName, context.MethodName, Stopwatch.GetElapsedTime(start));
        }
    }
}

public sealed class HubErrorFilterAttribute : StreamingHubFilterAttribute
{
    public override async ValueTask Invoke(StreamingHubContext context, Func<StreamingHubContext, ValueTask> next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not ReturnStatusException)
        {
            throw new ReturnStatusException(StatusCode.Internal, context.Path);
        }
    }
}
```

Register the DI-aware one globally with `options.GlobalFilters.Add<TimingFilter>()`, or per class with `[FromTypeFilter(typeof(TimingFilter))]` (created per use) / `[FromServiceFilter(typeof(TimingFilter))]` (resolved from DI, register it yourself). Order: ordered filters, then global, then class, then method. ASP.NET Core middleware runs before any filter.

## Errors

- `throw new ReturnStatusException((StatusCode)MyCode.Banned, "banned")` sends a status the client sees in `RpcException.Status.StatusCode`.
- `Context.CallContext.Status = new Status(...)` avoids throwing on hot paths.

## Scale-out groups

```csharp
builder.Services.AddMagicOnion()
    .UseRedisGroup(options => { options.ConnectionString = "localhost:6379"; }, registerAsDefault: true);
```

Without `registerAsDefault: true`, opt in per hub with `[GroupConfiguration(typeof(RedisGroupProvider))]`. A NATS provider (`Multicaster.Distributed.Nats`) also exists. For authoritative game rooms, prefer application-managed groups (`IMulticastGroupProvider.GetOrAddSynchronousGroup<Guid, IRoomHubReceiver>(name)`) owned by a singleton room service.

## Run

`dotnet run --project src/MyApp.Server`, note the `Now listening on: http://...` URL, and use that in `GrpcChannelx.ForAddress`. Deploy as a normal ASP.NET Core app (container, Kestrel behind an HTTP/2 load balancer).
