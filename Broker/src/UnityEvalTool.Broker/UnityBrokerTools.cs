using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace YuzeToolkit.UnityEvalTool.Broker;

[McpServerToolType]
internal sealed class UnityBrokerTools(BrokerRegistry registry)
{
    [McpServerTool(Name = "unity_status", UseStructuredContent = true)]
    [Description("Query every Unity instance registered with the local Broker and the selected Unity for an optional connection handle. Call this before unity_connect. It can also wait for the selected Unity to become ready or finish compilation without invoking Unity eval.")]
    public async Task<JsonElement> StatusAsync(
        [Description("Optional handle returned by unity_connect. Pass either this or instanceId when waiting.")] string connectionHandle = "",
        [Description("Optional instanceId returned by an earlier snapshot. Use this to wait before unity_connect, including while Unity is compiling or reloading.")] string instanceId = "",
        [Description("snapshot, ready, or compilation-complete.")] string waitFor = "snapshot",
        [Description("Optional compilationCycleId from unity_status to match while waiting.")] string requestId = "",
        [Description("Optional capturedAtUtc from a snapshot taken before compilation was requested. compilation-complete then waits for a cycle that started after this marker, avoiding a pre-observation race.")] string observedAfterUtc = "",
        [Description("Wait timeout in seconds. Zero returns immediately.")] int timeoutSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? observedAfter = null;
        if (!string.IsNullOrWhiteSpace(observedAfterUtc))
        {
            if (!DateTimeOffset.TryParse(observedAfterUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                throw new BrokerOperationException(BrokerErrorCodes.InvalidRequest,
                    "observedAfterUtc must be an ISO-8601 timestamp returned as capturedAtUtc by unity_status.");
            observedAfter = parsed;
        }
        var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 0, 3600));
        var snapshot = await registry.WaitAsync(connectionHandle, instanceId, waitFor, requestId, observedAfter, timeout,
            cancellationToken);
        return JsonSerializer.SerializeToElement(snapshot, BrokerJsonContext.Default.RegistrySnapshot);
    }

    [McpServerTool(Name = "unity_connect", UseStructuredContent = true)]
    [Description("Select one Unity instance for subsequent eval calls. First call unity_status, choose an instanceId, and pass the exact registryRevision from that snapshot. The returned opaque connectionHandle is scoped to the caller's workflow; there is no Broker-global selected Unity.")]
    public JsonElement Connect(
        [Description("Exact instanceId returned by unity_status.")] string instanceId,
        [Description("Exact registryRevision returned by the preceding unity_status call.")] long registryRevision)
    {
        var result = registry.Connect(instanceId, registryRevision);
        return JsonSerializer.SerializeToElement(result, BrokerJsonContext.Default.ConnectionLeaseResult);
    }

    [McpServerTool(Name = "eval", UseStructuredContent = true)]
    [Description("Execute JavaScript inside the Unity selected by unity_connect. A valid connectionHandle is mandatory. Eval is rejected while Unity is compiling, reloading, importing, changing PlayMode, stalled, disconnected, or in CompilationFailed state. Interrupted eval requests are never retried automatically.")]
    public Task<JsonElement> EvalAsync(
        [Description("Opaque handle returned by unity_connect.")] string connectionHandle,
        [Description("An async function declaration named execute, using the existing UnityEvalTool PuerTS/tool-module contract.")] string code,
        [Description("Unity-side execution timeout in seconds, from 1 to 600.")] int timeout = 30,
        [Description("Dispose and recreate this handle's persistent Unity-side PuerTS VM before execution.")] bool resetSession = false,
        CancellationToken cancellationToken = default) =>
        registry.ExecuteEvalAsync(connectionHandle, code, timeout, resetSession, cancellationToken);
}
