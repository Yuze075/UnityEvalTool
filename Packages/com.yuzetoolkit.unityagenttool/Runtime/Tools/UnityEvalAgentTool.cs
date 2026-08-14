#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YuzeToolkit.UnityAgent
{
    public sealed class AgentUnityEvalService : IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, EvalSession> _sessions = new(StringComparer.Ordinal);
        private readonly EvalExecutor _executor;
        private bool _disposed;

        public AgentUnityEvalService(int defaultTimeoutSeconds = 30)
        {
            _executor = new EvalExecutor(new EvalOptions
            {
                DefaultEvalTimeoutSeconds = Math.Min(600, Math.Max(1, defaultTimeoutSeconds))
            });
        }

        public async Task<AgentToolResult> ExecuteAsync(
            string agentSessionId,
            string code,
            int timeoutSeconds,
            bool resetSession,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(agentSessionId))
                throw new ArgumentException("Agent session id is required.", nameof(agentSessionId));
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Eval code is required.", nameof(code));
            cancellationToken.ThrowIfCancellationRequested();
            EvalSession session;
            lock (_syncRoot)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(AgentUnityEvalService));
                if (!_sessions.TryGetValue(agentSessionId, out session!))
                {
                    session = new EvalSession("agent-" + agentSessionId, "agent-1", "UnityAgentTool");
                    _sessions.Add(agentSessionId, session);
                }
            }

            var response = await _executor.ExecuteAsync(
                session,
                Guid.NewGuid().ToString("N"),
                EvalData.Obj(
                    ("code", code),
                    ("timeout", Math.Min(600, Math.Max(1, timeoutSeconds))),
                    ("resetSession", resetSession)),
                cancellationToken).ConfigureAwait(false);
            var isError = EvalData.GetBool(response, "isError");
            if (isError && cancellationToken.IsCancellationRequested)
                cancellationToken.ThrowIfCancellationRequested();
            var text = new StringBuilder();
            var content = EvalData.AsArray(response.TryGetValue("content", out var raw) ? raw : null);
            if (content != null)
            {
                foreach (var value in content)
                {
                    var item = EvalData.AsObject(value);
                    if (item == null) continue;
                    if (AgentJson.GetString(item, "type") == "text")
                    {
                        if (text.Length > 0) text.AppendLine();
                        text.Append(AgentJson.GetString(item, "text"));
                    }
                    else
                    {
                        if (text.Length > 0) text.AppendLine();
                        text.Append(AgentJson.Stringify(item));
                    }
                }
            }

            var resultText = text.Length == 0 ? AgentJson.Stringify(response) : text.ToString();
            return isError ? AgentToolResult.Error(resultText) : AgentToolResult.Success(resultText);
        }

        public void ReleaseSession(string agentSessionId)
        {
            if (string.IsNullOrWhiteSpace(agentSessionId)) return;
            EvalSession? session = null;
            lock (_syncRoot)
            {
                if (_sessions.TryGetValue(agentSessionId, out session))
                    _sessions.Remove(agentSessionId);
            }
            session?.Dispose();
        }

        public void Dispose()
        {
            List<EvalSession> sessions;
            lock (_syncRoot)
            {
                if (_disposed) return;
                _disposed = true;
                sessions = new List<EvalSession>(_sessions.Values);
                _sessions.Clear();
            }
            foreach (var session in sessions) session.Dispose();
        }
    }

    internal sealed class UnityEvalJsAgentTool : IAgentTool
    {
        private readonly AgentUnityEvalService _service;

        public UnityEvalJsAgentTool(AgentUnityEvalService service)
        {
            _service = service;
            Descriptor = new AgentToolDescriptor(
                "unity_eval_js",
                "Run JavaScript directly in the current Unity process. Define async function execute() and return concise " +
                "serializable data. The PuerTS VM persists for this conversation unless resetSession is true. For unfamiliar " +
                "Unity work, import tools:// to discover root modules and details, then import only the relevant module; generated " +
                "tool methods use positional parameters. Prefer those modules and use CS.* only for uncovered APIs. If an Editor " +
                "action schedules compilation, return immediately; the Agent host will resume this conversation afterward. " +
                "This direct tool does not use Broker, MCP, or CLI.",
                AgentToolAccess.Write,
                AgentToolArguments.ObjectSchema(AgentJson.Object(
                        ("code", AgentToolArguments.StringProperty(
                            "JavaScript containing async function execute() { ... }.")),
                        ("timeoutSeconds", AgentToolArguments.IntegerProperty("Cooperative timeout in seconds.", 1)),
                        ("resetSession", AgentToolArguments.BooleanProperty(
                            "Reset this conversation's persistent JavaScript VM before execution."))),
                    "code"));
        }

        public AgentToolDescriptor Descriptor { get; }

        public Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var code = AgentToolArguments.RequiredString(arguments, "code");
            var timeout = Math.Min(600,
                Math.Max(1, AgentToolArguments.OptionalInt(arguments, "timeoutSeconds",
                    context.DefaultTimeoutSeconds)));
            var reset = AgentToolArguments.OptionalBool(arguments, "resetSession");
            return _service.ExecuteAsync(context.SessionId, code, timeout, reset, cancellationToken);
        }
    }
}
