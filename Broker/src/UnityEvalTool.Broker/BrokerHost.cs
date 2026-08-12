using ModelContextProtocol.Server;

namespace YuzeToolkit.UnityEvalTool.Broker;

internal static class BrokerHost
{
    private static readonly DateTimeOffset StartedAtUtc = DateTimeOffset.UtcNow;

    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(BrokerConstants.Port));
        builder.Services.AddSingleton<AuthTokenStore>();
        builder.Services.AddSingleton<BrokerRegistry>();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, BrokerJsonContext.Default));
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<UnityBrokerTools>();

        var app = builder.Build();
        // Unity must read this token before it can open /unity, so create it eagerly.
        app.Services.GetRequiredService<AuthTokenStore>().GetOrCreateToken();
        app.UseHostFiltering();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(10) });
        app.Use(async (context, next) =>
        {
            if (!IsLoopbackHost(context.Request.Host.Host))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (!IsAllowedOrigin(context.Request.Headers.Origin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            if (context.Request.Path.StartsWithSegments("/mcp"))
            {
                var tokenStore = context.RequestServices.GetRequiredService<AuthTokenStore>();
                var authorization = context.Request.Headers.Authorization.ToString();
                var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authorization["Bearer ".Length..].Trim()
                    : context.Request.Headers["X-UnityEvalTool-Token"].ToString();
                if (!tokenStore.IsValid(token))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }

            await next(context);
        });

        app.MapGet("/health", (BrokerRegistry registry) =>
            new HealthSnapshot("ready", BrokerConstants.ProtocolVersion,
                $"http://{BrokerConstants.Host}:{BrokerConstants.Port}", StartedAtUtc,
                registry.Revision, registry.GetSnapshot().ConnectedCount));
        app.Map("/unity", UnityWebSocketEndpoint.HandleAsync);
        app.Map("/cli", CliWebSocketEndpoint.HandleAsync);
        app.MapMcp("/mcp");
        await app.RunAsync();
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.Ordinal) ||
        string.Equals(host, "::1", StringComparison.Ordinal) ||
        string.Equals(host, "[::1]", StringComparison.Ordinal);

    private static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return true;
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;
    }
}
