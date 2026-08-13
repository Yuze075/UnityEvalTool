#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YuzeToolkit
{
    internal sealed class RuntimeCliRunner : IDisposable
    {
        private readonly EvalCliCommandService _cliService = new(new EvalExecutor(new EvalOptions()));
        private EvalSession? _cliSession;
        private CancellationTokenSource? _cancellation;

        public void Start()
        {
            _cliSession ??= new EvalSession("debug-panel-cli", "cli", "debug-panel");
            _cancellation ??= new CancellationTokenSource();
        }

        public async Task<CliOutput> ExecuteLineAsync(string line)
        {
            if (_cliSession == null || _cancellation == null)
                return new CliOutput("CLI session is not available.", string.Empty, LogType.Error);

            try
            {
                var response = await _cliService.ExecuteLineAsync(
                    _cliSession,
                    Guid.NewGuid().ToString("N"),
                    line,
                    _cancellation.Token);

                var text = response.TryGetValue("text", out var value)
                    ? Convert.ToString(value) ?? string.Empty
                    : LitJson.Stringify(response);
                return new CliOutput(text, string.Empty, LogType.Log);
            }
            catch (OperationCanceledException)
            {
                return new CliOutput("CLI command was canceled.", string.Empty, LogType.Warning);
            }
            catch (Exception ex)
            {
                return new CliOutput(ex.Message, ex.ToString(), LogType.Exception);
            }
        }

        public void Dispose()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _cliSession?.Dispose();
            _cliSession = null;
        }
    }

    internal readonly struct CliOutput
    {
        public CliOutput(string message, string stackTrace, LogType logType)
        {
            Message = message;
            StackTrace = stackTrace;
            LogType = logType;
        }

        public string Message { get; }

        public string StackTrace { get; }

        public LogType LogType { get; }
    }
}
