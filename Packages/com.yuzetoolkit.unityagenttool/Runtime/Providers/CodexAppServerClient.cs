#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YuzeToolkit.UnityAgent
{
    /// <summary>
    /// Minimal Codex App Server JSONL client. The protocol intentionally omits the jsonrpc field.
    /// </summary>
    internal sealed class CodexAppServerClient : IDisposable
    {
        private const int MaxDiagnosticCharacters = 32 * 1024;

        private readonly object _syncRoot = new();
        private readonly string _executable;
        private readonly Dictionary<string, TaskCompletionSource<Dictionary<string, object?>>> _pending =
            new(StringComparer.Ordinal);
        private readonly SemaphoreSlim _initializeGate = new(1, 1);
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly StringBuilder _standardError = new();
        private Process? _process;
        private StreamWriter? _input;
        private Task? _readLoop;
        private long _nextRequestId;
        private bool _initialized;
        private bool _disposed;

        public CodexAppServerClient(string executable)
        {
            _executable = string.IsNullOrWhiteSpace(executable) ? "codex" : executable.Trim();
        }

        public event Action<string, Dictionary<string, object?>>? Notification;

        public event Action<CodexAppServerClient, Exception>? FaultedWithSender;

        public Func<string, Dictionary<string, object?>, CancellationToken, Task<object?>>? ServerRequest { get; set; }

        public async Task<Dictionary<string, object?>> SendRequestAsync(
            string method,
            Dictionary<string, object?> parameters,
            CancellationToken cancellationToken)
        {
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            return await SendRequestCoreAsync(method, parameters, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendNotificationAsync(
            string method,
            Dictionary<string, object?> parameters,
            CancellationToken cancellationToken)
        {
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            await WriteAsync(AgentJson.Object(("method", method), ("params", parameters)), cancellationToken)
                .ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            FaultPending(new ObjectDisposedException(nameof(CodexAppServerClient)));

            Process? process;
            lock (_syncRoot) process = _process;
            if (process != null)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
                process.Dispose();
            }

            _initializeGate.Dispose();
            _writeGate.Dispose();
            _lifetime.Dispose();
        }

        private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (_initialized) return;
            await _initializeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_initialized) return;
                StartProcess();
                await SendRequestCoreAsync("initialize", AgentJson.Object(
                        ("clientInfo", AgentJson.Object(
                            ("name", "UnityAgentTool"),
                            ("title", "Unity Agent Tool"),
                            ("version", "0.1.0"))),
                        ("capabilities", AgentJson.Object(("experimentalApi", true)))), cancellationToken)
                    .ConfigureAwait(false);
                await WriteAsync(AgentJson.Object(("method", "initialized"), ("params", AgentJson.Object())),
                    cancellationToken).ConfigureAwait(false);
                _initialized = true;
            }
            catch
            {
                StopFailedProcess();
                throw;
            }
            finally
            {
                _initializeGate.Release();
            }
        }

        private void StartProcess()
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (_process != null && !_process.HasExited) return;
                var startInfo = new ProcessStartInfo
                {
                    FileName = _executable,
                    Arguments = "app-server --stdio",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                try
                {
                    if (!process.Start())
                        throw new AgentProviderException($"Could not start Codex executable '{_executable}'.");
                }
                catch (Exception exception) when (exception is InvalidOperationException ||
                                                   exception is System.ComponentModel.Win32Exception)
                {
                    process.Dispose();
                    throw new AgentProviderException(
                        $"Could not start Codex App Server using '{_executable}'. Install/sign in to Codex or configure its executable path.",
                        exception);
                }

                process.Exited += (_, _) => OnProcessExited(process);
                process.ErrorDataReceived += (_, args) => AppendStandardError(args.Data);
                process.BeginErrorReadLine();
                _process = process;
                _input = process.StandardInput;
                _input.AutoFlush = true;
                _readLoop = ReadLoopAsync(process, _lifetime.Token);
            }
        }

        private async Task<Dictionary<string, object?>> SendRequestCoreAsync(
            string method,
            Dictionary<string, object?> parameters,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var id = Interlocked.Increment(ref _nextRequestId).ToString(CultureInfo.InvariantCulture);
            var completion = new TaskCompletionSource<Dictionary<string, object?>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_syncRoot)
            {
                if (_pending.ContainsKey(id)) throw new InvalidOperationException($"Duplicate request id '{id}'.");
                _pending.Add(id, completion);
            }

            try
            {
                await WriteAsync(AgentJson.Object(
                    ("id", long.Parse(id, CultureInfo.InvariantCulture)),
                    ("method", method),
                    ("params", parameters)), cancellationToken).ConfigureAwait(false);
                using var registration = cancellationToken.Register(() => completion.TrySetCanceled());
                return await completion.Task.ConfigureAwait(false);
            }
            finally
            {
                lock (_syncRoot) _pending.Remove(id);
            }
        }

        private async Task WriteAsync(Dictionary<string, object?> payload, CancellationToken cancellationToken)
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                StreamWriter input;
                lock (_syncRoot)
                {
                    ThrowIfDisposed();
                    input = _input ?? throw new AgentProviderException("Codex App Server is not running.");
                }
                await input.WriteLineAsync(AgentJson.Stringify(payload)).ConfigureAwait(false);
                await input.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                throw CreateExitedException("Failed to write to Codex App Server.", exception);
            }
            finally
            {
                _writeGate.Release();
            }
        }

        private async Task ReadLoopAsync(Process process, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    Dictionary<string, object?> message;
                    try
                    {
                        message = AgentJson.ParseObject(line);
                    }
                    catch (Exception exception)
                    {
                        throw new AgentProviderException("Codex App Server wrote an invalid JSONL message.", exception);
                    }
                    Dispatch(message);
                }

                if (!cancellationToken.IsCancellationRequested)
                    FaultPending(CreateExitedException("Codex App Server closed its output stream."));
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                FaultPending(exception is AgentProviderException
                    ? exception
                    : CreateExitedException("Codex App Server read loop failed.", exception));
            }
        }

        private void Dispatch(Dictionary<string, object?> message)
        {
            var hasId = message.TryGetValue("id", out var rawId) && rawId != null;
            var method = AgentJson.GetString(message, "method");
            if (hasId && string.IsNullOrWhiteSpace(method))
            {
                var id = Convert.ToString(rawId, CultureInfo.InvariantCulture) ?? string.Empty;
                TaskCompletionSource<Dictionary<string, object?>>? completion;
                lock (_syncRoot) _pending.TryGetValue(id, out completion);
                if (completion == null) return;
                if (AgentJson.GetObject(message, "error") is { } error)
                {
                    var code = AgentJson.GetLong(error, "code");
                    var text = AgentJson.GetString(error, "message", "Unknown Codex App Server error.");
                    completion.TrySetException(new AgentProviderException($"Codex App Server error {code}: {text}"));
                }
                else
                {
                    completion.TrySetResult(AgentJson.GetObject(message, "result") ?? AgentJson.Object());
                }
                return;
            }

            var parameters = AgentJson.GetObject(message, "params") ?? AgentJson.Object();
            if (hasId)
            {
                _ = HandleServerRequestAsync(rawId!, method, parameters);
                return;
            }
            if (!string.IsNullOrWhiteSpace(method)) Notification?.Invoke(method, parameters);
        }

        private async Task HandleServerRequestAsync(
            object rawId,
            string method,
            Dictionary<string, object?> parameters)
        {
            object? result;
            try
            {
                var handler = ServerRequest;
                if (handler == null)
                    throw new AgentProviderException($"Codex App Server requested unsupported client method '{method}'.");
                result = await handler(method, parameters, _lifetime.Token).ConfigureAwait(false);
                await WriteAsync(AgentJson.Object(("id", rawId), ("result", result ?? AgentJson.Object())),
                    _lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await TryWriteServerErrorAsync(rawId, -32800, "Client request was cancelled.").ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await TryWriteServerErrorAsync(rawId, -32603, exception.Message).ConfigureAwait(false);
            }
        }

        private async Task TryWriteServerErrorAsync(object rawId, int code, string message)
        {
            try
            {
                await WriteAsync(AgentJson.Object(
                    ("id", rawId),
                    ("error", AgentJson.Object(("code", code), ("message", message)))),
                    _lifetime.Token).ConfigureAwait(false);
            }
            catch
            {
                // The original transport failure is already terminal; there is no peer left to receive this error.
            }
        }

        private void OnProcessExited(Process process)
        {
            if (_disposed) return;
            int? exitCode = null;
            try
            {
                exitCode = process.ExitCode;
            }
            catch (InvalidOperationException)
            {
            }
            FaultPending(CreateExitedException(
                exitCode.HasValue ? $"Codex App Server exited with code {exitCode.Value}." : "Codex App Server exited."));
        }

        private void AppendStandardError(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;
            lock (_syncRoot)
            {
                if (_standardError.Length >= MaxDiagnosticCharacters) return;
                var remaining = MaxDiagnosticCharacters - _standardError.Length;
                _standardError.AppendLine(line.Length <= remaining ? line : line.Substring(0, remaining));
            }
        }

        private AgentProviderException CreateExitedException(string message, Exception? innerException = null)
        {
            string diagnostic;
            lock (_syncRoot) diagnostic = _standardError.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(diagnostic)) message += "\n" + diagnostic;
            return innerException == null
                ? new AgentProviderException(message)
                : new AgentProviderException(message, innerException);
        }

        private void FaultPending(Exception exception)
        {
            List<TaskCompletionSource<Dictionary<string, object?>>> pending;
            lock (_syncRoot) pending = new List<TaskCompletionSource<Dictionary<string, object?>>>(_pending.Values);
            foreach (var completion in pending) completion.TrySetException(exception);
            FaultedWithSender?.Invoke(this, exception);
        }

        private void StopFailedProcess()
        {
            Process? process;
            lock (_syncRoot)
            {
                process = _process;
                _process = null;
                _input = null;
                _initialized = false;
            }
            if (process == null) return;
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch
            {
                // Initialization already failed; process cleanup cannot change that outcome.
            }
            process.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CodexAppServerClient));
        }
    }
}
