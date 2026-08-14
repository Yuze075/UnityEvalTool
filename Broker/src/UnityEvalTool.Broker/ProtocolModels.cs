using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzeToolkit.UnityEvalTool.Broker;

internal sealed record UnityRegistration(
    string AuthToken,
    string InstanceId,
    long ConnectionEpoch,
    int ProcessId,
    DateTimeOffset ProcessStartedAtUtc,
    string ProjectName,
    string ProjectPath,
    string UnityVersion,
    string PackageVersion,
    string Environment,
    UnityStatus Status);

internal sealed record UnityStatus(
    string Phase,
    bool CanEval,
    string BusyReason,
    long MainThreadTick,
    DateTimeOffset MainThreadTickAtUtc,
    bool IsPlaying,
    bool IsPaused,
    bool IsUpdating,
    string CompilationCycleId,
    int CompilerErrorCount,
    int CompilerWarningCount,
    DateTimeOffset? LastCompilationStartedAtUtc,
    DateTimeOffset? LastCompilationFinishedAtUtc,
    long VmGeneration);

internal sealed record UnityInstanceSnapshot(
    string InstanceId,
    long ConnectionEpoch,
    int ProcessId,
    DateTimeOffset ProcessStartedAtUtc,
    string ProjectName,
    string ProjectPath,
    string UnityVersion,
    string PackageVersion,
    string Environment,
    bool IsConnected,
    DateTimeOffset ConnectedAtUtc,
    DateTimeOffset LastTransportHeartbeatAtUtc,
    UnityStatus Status);

internal sealed record RegistrySnapshot(
    long RegistryRevision,
    DateTimeOffset CapturedAtUtc,
    int ConnectedCount,
    string? ConnectionHandle,
    UnityInstanceSnapshot? SelectedUnity,
    IReadOnlyList<UnityInstanceSnapshot> UnityInstances);

internal sealed record ConnectionLeaseResult(
    string ConnectionHandle,
    DateTimeOffset ExpiresAtUtc,
    UnityInstanceSnapshot Unity);

internal sealed record HealthSnapshot(
    string Status,
    string ProtocolVersion,
    string Endpoint,
    DateTimeOffset StartedAtUtc,
    long RegistryRevision,
    int ConnectedUnityCount,
    bool RequireToken);

internal sealed record UnityCommandRequest(
    string SessionId,
    string RequestId,
    string? Code,
    string? Line,
    int TimeoutSeconds,
    bool ResetSession);

internal sealed record ProtocolEnvelope(
    string Protocol,
    string Type,
    string? Id,
    string Method,
    JsonElement Payload,
    ProtocolError? Error);

internal sealed record ProtocolError(string Code, string Message, bool MayHaveExecuted);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UnityRegistration))]
[JsonSerializable(typeof(UnityStatus))]
[JsonSerializable(typeof(UnityInstanceSnapshot))]
[JsonSerializable(typeof(List<UnityInstanceSnapshot>))]
[JsonSerializable(typeof(RegistrySnapshot))]
[JsonSerializable(typeof(ConnectionLeaseResult))]
[JsonSerializable(typeof(HealthSnapshot))]
[JsonSerializable(typeof(UnityCommandRequest))]
[JsonSerializable(typeof(ProtocolEnvelope))]
[JsonSerializable(typeof(ProtocolError))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class BrokerJsonContext : JsonSerializerContext;
