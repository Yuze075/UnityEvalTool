using System.Net.WebSockets;
using System.Text.Json;

namespace YuzeToolkit.UnityEvalTool.Broker;

internal static class CliWebSocketEndpoint
{
    public static async Task HandleAsync(HttpContext context, BrokerRegistry registry, AuthTokenStore tokens)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var sendGate = new SemaphoreSlim(1, 1);
        var consoleId = Guid.NewGuid().ToString("N");
        string? selectedHandle = null;
        var authorized = false;
        try
        {
            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                using var document = await WebSocketJson.ReceiveAsync(socket, context.RequestAborted);
                if (document == null) break;
                var envelope = WebSocketJson.ParseEnvelope(document);
                if (!string.Equals(envelope.Type, "request", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(envelope.Id))
                    throw new BrokerOperationException(BrokerErrorCodes.InvalidRequest,
                        "CLI messages must be requests with an id.");
                try
                {
                    JsonElement result;
                    if (!authorized)
                    {
                        if (!string.Equals(envelope.Method, "cli/hello", StringComparison.Ordinal))
                            throw new BrokerOperationException(BrokerErrorCodes.AuthenticationFailed,
                                "The first CLI request must be cli/hello.");
                        var token = envelope.Payload.TryGetProperty("authToken", out var tokenElement)
                            ? tokenElement.GetString()
                            : null;
                        if (!tokens.IsValid(token))
                            throw new BrokerOperationException(BrokerErrorCodes.AuthenticationFailed,
                                "CLI Broker token is invalid.");
                        authorized = true;
                        result = JsonDocument.Parse($"{{\"consoleId\":\"{consoleId}\",\"protocolVersion\":\"{BrokerConstants.ProtocolVersion}\"}}")
                            .RootElement.Clone();
                    }
                    else
                    {
                        result = envelope.Method switch
                        {
                            "unity/list" => Serialize(registry.GetSnapshot(selectedHandle)),
                            "unity/connect" => Connect(registry, envelope.Payload, out selectedHandle),
                            "unity/status" => await StatusAsync(registry, selectedHandle, envelope.Payload,
                                context.RequestAborted),
                            "cli/execute" => await ExecuteAsync(registry, selectedHandle, consoleId,
                                envelope.Payload, context.RequestAborted),
                            _ => throw new BrokerOperationException(BrokerErrorCodes.InvalidRequest,
                                $"Unknown CLI Broker method '{envelope.Method}'.")
                        };
                    }

                    await WebSocketJson.SendAsync(socket,
                        WebSocketJson.CreateEnvelope("response", envelope.Method, envelope.Id, result), sendGate,
                        context.RequestAborted);
                }
                catch (BrokerOperationException ex)
                {
                    await WebSocketJson.SendAsync(socket,
                        WebSocketJson.CreateEnvelope("response", envelope.Method, envelope.Id,
                            WebSocketJson.EmptyObject(), new ProtocolError(ex.Code, ex.Message, ex.MayHaveExecuted)),
                        sendGate, context.RequestAborted);
                }
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Host or peer shutdown.
        }
        catch (WebSocketException)
        {
            // Peer disconnected.
        }
    }

    private static JsonElement Connect(BrokerRegistry registry, JsonElement payload, out string handle)
    {
        var instanceId = payload.TryGetProperty("instanceId", out var instanceElement)
            ? instanceElement.GetString() ?? string.Empty
            : string.Empty;
        var revision = payload.TryGetProperty("registryRevision", out var revisionElement)
            ? revisionElement.GetInt64()
            : 0;
        var result = registry.Connect(instanceId, revision);
        handle = result.ConnectionHandle;
        return JsonSerializer.SerializeToElement(result, BrokerJsonContext.Default.ConnectionLeaseResult);
    }

    private static async Task<JsonElement> StatusAsync(BrokerRegistry registry, string? handle, JsonElement payload,
        CancellationToken cancellationToken)
    {
        var waitFor = payload.TryGetProperty("waitFor", out var waitElement)
            ? waitElement.GetString() ?? "snapshot"
            : "snapshot";
        var requestId = payload.TryGetProperty("requestId", out var requestElement)
            ? requestElement.GetString()
            : null;
        var timeout = payload.TryGetProperty("timeoutSeconds", out var timeoutElement)
            ? TimeSpan.FromSeconds(Math.Clamp(timeoutElement.GetInt32(), 0, 3600))
            : TimeSpan.Zero;
        var snapshot = await registry.WaitAsync(handle, null, waitFor, requestId, null, timeout, cancellationToken);
        return Serialize(snapshot);
    }

    private static async Task<JsonElement> ExecuteAsync(BrokerRegistry registry, string? handle, string consoleId,
        JsonElement payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new BrokerOperationException(BrokerErrorCodes.ConnectionHandleRequired,
                "Select a Unity instance before executing CLI commands.");
        var line = payload.TryGetProperty("line", out var lineElement)
            ? lineElement.GetString() ?? string.Empty
            : string.Empty;
        return await registry.ExecuteCliAsync(handle, consoleId, line, cancellationToken);
    }

    private static JsonElement Serialize(RegistrySnapshot value) =>
        JsonSerializer.SerializeToElement(value, BrokerJsonContext.Default.RegistrySnapshot);
}
