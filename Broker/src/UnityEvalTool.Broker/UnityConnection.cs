using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace YuzeToolkit.UnityEvalTool.Broker;

internal sealed class UnityConnection : IAsyncDisposable
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtocolEnvelope>> _pending = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Action<UnityConnection, UnityStatus> _onStatus;
    private readonly Action<UnityConnection> _onHeartbeat;
    private int _disposed;

    public UnityConnection(WebSocket socket, UnityRegistration registration,
        Action<UnityConnection, UnityStatus> onStatus, Action<UnityConnection> onHeartbeat)
    {
        _socket = socket;
        Registration = registration;
        Status = registration.Status;
        ConnectedAtUtc = DateTimeOffset.UtcNow;
        LastTransportHeartbeatAtUtc = ConnectedAtUtc;
        _onStatus = onStatus;
        _onHeartbeat = onHeartbeat;
    }

    public UnityRegistration Registration { get; }
    public UnityStatus Status { get; private set; }
    public DateTimeOffset ConnectedAtUtc { get; }
    public DateTimeOffset LastTransportHeartbeatAtUtc { get; private set; }
    public bool IsConnected => _socket.State == WebSocketState.Open && Volatile.Read(ref _disposed) == 0;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        while (!linked.Token.IsCancellationRequested && _socket.State == WebSocketState.Open)
        {
            using var document = await WebSocketJson.ReceiveAsync(_socket, linked.Token);
            if (document == null) break;
            LastTransportHeartbeatAtUtc = DateTimeOffset.UtcNow;
            _onHeartbeat(this);
            var envelope = WebSocketJson.ParseEnvelope(document);
            if (string.Equals(envelope.Type, "response", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(envelope.Id) && _pending.TryRemove(envelope.Id, out var completion))
                    completion.TrySetResult(envelope);
                continue;
            }

            if (string.Equals(envelope.Type, "event", StringComparison.Ordinal) &&
                string.Equals(envelope.Method, "unity/status", StringComparison.Ordinal))
            {
                var status = envelope.Payload.Deserialize(BrokerJsonContext.Default.UnityStatus)
                             ?? throw new BrokerOperationException(BrokerErrorCodes.InvalidRequest, "Unity status payload is empty.");
                Status = status;
                _onStatus(this, status);
                continue;
            }

            if (string.Equals(envelope.Type, "event", StringComparison.Ordinal) &&
                string.Equals(envelope.Method, "unity/heartbeat", StringComparison.Ordinal))
                continue;
        }
    }

    public async Task<JsonElement> RequestAsync(string method, UnityCommandRequest request,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!IsConnected)
            throw new BrokerOperationException(BrokerErrorCodes.UnityDisconnected,
                $"Unity instance '{Registration.InstanceId}' is disconnected.");

        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<ProtocolEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion)) throw new InvalidOperationException("Duplicate Broker request id.");
        var payload = WebSocketJson.ToElement(request, BrokerJsonContext.Default.UnityCommandRequest);
        var message = WebSocketJson.CreateEnvelope("request", method, id, payload);
        var sent = false;
        try
        {
            await WebSocketJson.SendAsync(_socket, message, _sendGate, cancellationToken);
            sent = true;
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            var envelope = await completion.Task.WaitAsync(timeoutSource.Token);
            if (envelope.Error != null)
                throw new BrokerOperationException(envelope.Error.Code, envelope.Error.Message, envelope.Error.MayHaveExecuted);
            return envelope.Payload.Clone();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BrokerOperationException(sent ? BrokerErrorCodes.ExecutionOutcomeUnknown : BrokerErrorCodes.RequestTimedOut,
                sent
                    ? $"Unity did not return a response for '{method}'. The command may have executed."
                    : $"Unity request '{method}' timed out before it was sent.",
                sent);
        }
        catch (WebSocketException ex)
        {
            throw new BrokerOperationException(sent ? BrokerErrorCodes.ExecutionOutcomeUnknown : BrokerErrorCodes.UnityDisconnected,
                sent ? $"Unity connection was lost after '{method}' was sent: {ex.Message}" : ex.Message, sent);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public async Task ReleaseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var request = new UnityCommandRequest(sessionId, Guid.NewGuid().ToString("N"), null, null, 30, false);
        await RequestAsync("session/release", request, TimeSpan.FromSeconds(35), cancellationToken);
    }

    public UnityInstanceSnapshot ToSnapshot()
    {
        var status = Status;
        if (IsConnected && DateTimeOffset.UtcNow - status.MainThreadTickAtUtc > BrokerConstants.MainThreadStallAfter)
        {
            var reportedPhase = status.Phase;
            status = status with
            {
                Phase = "MainThreadStalled",
                CanEval = false,
                BusyReason = $"Unity transport is connected but the main thread heartbeat is stale. " +
                             $"The last Unity-reported phase was {reportedPhase}."
            };
        }

        var registration = Registration;
        return new UnityInstanceSnapshot(registration.InstanceId, registration.ConnectionEpoch,
            registration.ProcessId, registration.ProcessStartedAtUtc, registration.ProjectName,
            registration.ProjectPath, registration.UnityVersion, registration.PackageVersion,
            registration.Environment, IsConnected, ConnectedAtUtc, LastTransportHeartbeatAtUtc, status);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        foreach (var pending in _pending.Values)
            pending.TrySetException(new BrokerOperationException(BrokerErrorCodes.ExecutionOutcomeUnknown,
                "Unity disconnected while the request was pending.", true));
        _pending.Clear();
        try
        {
            if (_socket.State == WebSocketState.Open)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Broker connection closed", CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // The peer is already gone.
        }
        _socket.Dispose();
        _sendGate.Dispose();
        _lifetime.Dispose();
    }
}
