using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace YuzeToolkit.UnityEvalTool.Broker;

[McpServerToolType]
internal sealed class UnityBrokerTools(BrokerRegistry registry)
{
    [McpServerTool(Name = "unity_status", UseStructuredContent = true)]
    [Description("Query Unity instances and state from the computer-level Broker. Call this before unity_connect. Waiting is event-driven and survives temporary Unity disconnects. ready returns when execution is available, including CompilationFailed repair mode; compilation-complete returns after compilation succeeds or fails. Always inspect phase, canEval, and compiler counts.")]
    public async Task<JsonElement> StatusAsync(
        [Description("Optional handle returned by unity_connect. Pass either this or instanceId when waiting.")] string connectionHandle = "",
        [Description("Optional instanceId returned by an earlier snapshot. Use this to wait before unity_connect, including while Unity is compiling or reloading.")] string instanceId = "",
        [Description("snapshot; ready (normal Ready or CompilationFailed repair mode); or compilation-complete (successful or failed terminal compilation). Always inspect the returned phase.")] string waitFor = "snapshot",
        [Description("Optional compilationCycleId from unity_status to match while waiting. Do not pass the Unity-side requestId returned by scheduleAssetRefresh.")] string requestId = "",
        [Description("Optional capturedAtUtc from a fresh unity_status snapshot taken immediately before the eval that requests compilation. compilation-complete then ignores older cycles.")] string observedAfterUtc = "",
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
    [Description("Select one Unity instance for subsequent eval calls. First call unity_status and pass its exact registryRevision. The opaque connectionHandle survives compilation and same-process Domain Reload even when registryRevision changes; reconnect only when the handle expires, is invalid, or the Unity process is replaced.")]
    public JsonElement Connect(
        [Description("Exact instanceId returned by unity_status.")] string instanceId,
        [Description("Exact registryRevision returned by the preceding unity_status call.")] long registryRevision)
    {
        var result = registry.Connect(instanceId, registryRevision);
        return JsonSerializer.SerializeToElement(result, BrokerJsonContext.Default.ConnectionLeaseResult);
    }

    [McpServerTool(Name = "eval", UseStructuredContent = true)]
    [Description("Execute JavaScript inside the Unity selected by unity_connect. Eval is rejected while Unity is compiling, reloading, importing, changing PlayMode, stalled, or disconnected. CompilationFailed is an executable repair mode backed by the last successfully loaded assemblies; use it to read errors, edit code, and request another refresh. Interrupted requests are never retried automatically.")]
    public Task<JsonElement> EvalAsync(
        [Description("Opaque handle returned by unity_connect.")] string connectionHandle,
        [Description("An async function declaration named execute, using the existing UnityEvalTool PuerTS/tool-module contract.")] string code,
        [Description("Unity-side execution timeout in seconds, from 1 to 600.")] int timeout = 30,
        [Description("Dispose and recreate this handle's persistent Unity-side PuerTS VM before execution.")] bool resetSession = false,
        CancellationToken cancellationToken = default) =>
        registry.ExecuteEvalAsync(connectionHandle, code, timeout, resetSession, cancellationToken);
}
