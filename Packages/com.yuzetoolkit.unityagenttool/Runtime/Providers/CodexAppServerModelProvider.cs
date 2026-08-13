#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YuzeToolkit.UnityAgent
{
    /// <summary>
    /// Codex subscription backend implemented through the official local App Server protocol.
    /// Codex owns its internal loop; Unity supplies native tools and presents its events/approvals.
    /// </summary>
    internal sealed class CodexAppServerModelProvider : IAgentModelProvider, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly AgentToolRegistry _tools;
        private readonly AgentApprovalService _approvals;
        private readonly Dictionary<string, CodexAppServerClient> _clients = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CodexTurnOperation> _operations = new(StringComparer.Ordinal);
        private bool _disposed;

        public CodexAppServerModelProvider(AgentToolRegistry tools, AgentApprovalService approvals)
        {
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        }

        public async Task<AgentModelResponse> CompleteAsync(
            AgentProviderProfile profile,
            AgentModelRequest request,
            Action<AgentStreamEvent>? onEvent,
            CancellationToken cancellationToken)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (request == null) throw new ArgumentNullException(nameof(request));
            var client = GetClient(ResolveExecutable(profile));
            var threadId = await StartOrResumeThreadAsync(client, request, cancellationToken).ConfigureAwait(false);
            var operation = new CodexTurnOperation(client, threadId, request, onEvent, cancellationToken);
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (_operations.ContainsKey(threadId))
                    throw new InvalidOperationException($"Codex thread '{threadId}' already has an active turn.");
                _operations.Add(threadId, operation);
            }

            try
            {
                onEvent?.Invoke(new AgentStreamEvent(AgentStreamEventKind.RunStarted));
                var turnParameters = AgentJson.Object(
                    ("threadId", threadId),
                    ("input", AgentJson.Array(AgentJson.Object(
                        ("type", "text"),
                        ("text", BuildTurnInput(request))))));
                if (!string.IsNullOrWhiteSpace(request.Model)) turnParameters["model"] = request.Model;
                if (!IsDefaultValue(request.ReasoningEffort)) turnParameters["effort"] = request.ReasoningEffort;
                var started = await client.SendRequestAsync("turn/start", turnParameters, cancellationToken)
                    .ConfigureAwait(false);
                if (AgentJson.GetObject(started, "turn") is { } turn)
                    operation.TurnId = AgentJson.GetString(turn, "id");
                using var registration = cancellationToken.Register(() => operation.Completion.TrySetCanceled());
                var response = await operation.Completion.Task.ConfigureAwait(false);
                response.ProviderThreadId = threadId;
                return response;
            }
            catch (OperationCanceledException)
            {
                await TryInterruptAsync(client, operation).ConfigureAwait(false);
                throw;
            }
            finally
            {
                lock (_syncRoot) _operations.Remove(threadId);
            }
        }

        public async Task<IReadOnlyList<string>> ListModelsAsync(
            AgentProviderProfile profile,
            CancellationToken cancellationToken)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var client = GetClient(ResolveExecutable(profile));
            var models = new List<string>();
            string? cursor = null;
            do
            {
                var parameters = AgentJson.Object(("limit", 100));
                if (!string.IsNullOrWhiteSpace(cursor)) parameters["cursor"] = cursor;
                var result = await client.SendRequestAsync("model/list", parameters, cancellationToken)
                    .ConfigureAwait(false);
                foreach (var model in AgentJson.Objects(AgentJson.GetArray(result, "data")))
                {
                    var id = AgentJson.GetString(model, "model", AgentJson.GetString(model, "id"));
                    if (!string.IsNullOrWhiteSpace(id) && !models.Contains(id, StringComparer.Ordinal)) models.Add(id);
                }
                cursor = AgentJson.GetString(result, "nextCursor");
            } while (!string.IsNullOrWhiteSpace(cursor));
            return models;
        }

        public async Task<AgentCodexAccountStatus> GetAccountAsync(
            AgentProviderProfile profile,
            CancellationToken cancellationToken)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var client = GetClient(ResolveExecutable(profile));
            var result = await client.SendRequestAsync("account/read",
                AgentJson.Object(("refreshToken", false)), cancellationToken).ConfigureAwait(false);
            var account = AgentJson.GetObject(result, "account");
            return new AgentCodexAccountStatus
            {
                IsSignedIn = account != null,
                RequiresOpenAiAuth = EvalData.GetBool(result, "requiresOpenaiAuth"),
                AccountType = account == null ? string.Empty : AgentJson.GetString(account, "type"),
                Email = account == null ? string.Empty : AgentJson.GetString(account, "email"),
                PlanType = account == null ? string.Empty : AgentJson.GetString(account, "planType")
            };
        }

        public async Task<AgentCodexLogin> StartLoginAsync(
            AgentProviderProfile profile,
            bool deviceCode,
            CancellationToken cancellationToken)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var client = GetClient(ResolveExecutable(profile));
            var parameters = deviceCode
                ? AgentJson.Object(("type", "chatgptDeviceCode"))
                : AgentJson.Object(
                    ("type", "chatgpt"),
                    ("useHostedLoginSuccessPage", true),
                    ("appBrand", "chatgpt"));
            var result = await client.SendRequestAsync("account/login/start", parameters, cancellationToken)
                .ConfigureAwait(false);
            return new AgentCodexLogin
            {
                LoginId = AgentJson.GetString(result, "loginId"),
                AuthorizationUrl = deviceCode
                    ? AgentJson.GetString(result, "verificationUrl")
                    : AgentJson.GetString(result, "authUrl"),
                UserCode = AgentJson.GetString(result, "userCode")
            };
        }

        public void Dispose()
        {
            List<CodexAppServerClient> clients;
            lock (_syncRoot)
            {
                if (_disposed) return;
                _disposed = true;
                clients = _clients.Values.ToList();
                _clients.Clear();
                foreach (var operation in _operations.Values)
                    operation.Completion.TrySetCanceled();
                _operations.Clear();
            }
            foreach (var client in clients) client.Dispose();
        }

        private CodexAppServerClient GetClient(string executable)
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (_clients.TryGetValue(executable, out var existing)) return existing;
                var client = new CodexAppServerClient(executable);
                client.Notification += HandleNotification;
                client.FaultedWithSender += HandleClientFault;
                client.ServerRequest = (method, parameters, cancellationToken) =>
                    HandleServerRequestAsync(method, parameters, cancellationToken);
                _clients.Add(executable, client);
                return client;
            }
        }

        private async Task<string> StartOrResumeThreadAsync(
            CodexAppServerClient client,
            AgentModelRequest request,
            CancellationToken cancellationToken)
        {
            var parameters = CreateThreadParameters(request);
            string method;
            if (string.IsNullOrWhiteSpace(request.ProviderThreadId))
            {
                method = "thread/start";
                parameters["serviceName"] = "unity_agent_tool";
                parameters["dynamicTools"] = CreateDynamicToolSpecs();
            }
            else
            {
                method = "thread/resume";
                parameters["threadId"] = request.ProviderThreadId;
                // The field is experimental. Newer App Server versions restore it from the thread,
                // while versions that accept this override can refresh schemas after a Unity reload.
                parameters["dynamicTools"] = CreateDynamicToolSpecs();
            }

            var result = await client.SendRequestAsync(method, parameters, cancellationToken).ConfigureAwait(false);
            var thread = AgentJson.GetObject(result, "thread")
                         ?? throw new AgentProviderException($"Codex App Server '{method}' returned no thread.");
            var threadId = AgentJson.GetString(thread, "id");
            if (string.IsNullOrWhiteSpace(threadId))
                throw new AgentProviderException($"Codex App Server '{method}' returned an empty thread id.");
            return threadId;
        }

        private Dictionary<string, object?> CreateThreadParameters(AgentModelRequest request)
        {
            var fullAccess = request.PermissionMode == AgentPermissionMode.FullAccess;
            var parameters = AgentJson.Object(
                ("cwd", ResolveWorkingDirectory(request.WorkingDirectory)),
                ("approvalPolicy", fullAccess ? "never" : "on-request"),
                ("sandbox", fullAccess ? "danger-full-access" : "read-only"),
                ("developerInstructions", request.SystemPrompt));
            if (!string.IsNullOrWhiteSpace(request.Model)) parameters["model"] = request.Model;
            return parameters;
        }

        private List<object?> CreateDynamicToolSpecs()
        {
            return _tools.ListDescriptors().Select(descriptor => (object?)AgentJson.Object(
                ("type", "function"),
                ("name", descriptor.Name),
                ("description", descriptor.Description),
                ("inputSchema", descriptor.Parameters))).ToList();
        }

        private async Task<object?> HandleServerRequestAsync(
            string method,
            Dictionary<string, object?> parameters,
            CancellationToken clientCancellation)
        {
            var operation = FindOperation(parameters);
            switch (method)
            {
                case "item/tool/call":
                    return await ExecuteDynamicToolAsync(operation, parameters, clientCancellation)
                        .ConfigureAwait(false);
                case "item/commandExecution/requestApproval":
                {
                    var approved = await ApproveOperationAsync(operation, parameters, "codex_command",
                        "Codex requests permission to execute a command.", clientCancellation).ConfigureAwait(false);
                    return AgentJson.Object(("decision", approved ? "accept" : "decline"));
                }
                case "item/fileChange/requestApproval":
                {
                    var approved = await ApproveOperationAsync(operation, parameters, "codex_file_change",
                        "Codex requests permission to change files.", clientCancellation).ConfigureAwait(false);
                    return AgentJson.Object(("decision", approved ? "accept" : "decline"));
                }
                case "execCommandApproval":
                {
                    var approved = await ApproveOperationAsync(operation, parameters, "codex_command",
                        "Codex requests permission to execute a command.", clientCancellation).ConfigureAwait(false);
                    return AgentJson.Object(("decision", approved ? "approved" : "denied"));
                }
                case "applyPatchApproval":
                {
                    var approved = await ApproveOperationAsync(operation, parameters, "codex_file_change",
                        "Codex requests permission to apply a file patch.", clientCancellation).ConfigureAwait(false);
                    return AgentJson.Object(("decision", approved ? "approved" : "denied"));
                }
                case "item/tool/requestUserInput":
                    // This package exposes binary operation approvals only. Empty answers let Codex
                    // continue and explain that it needs the user to send a new chat message.
                    return AgentJson.Object(("answers", AgentJson.Object()));
                case "item/permissions/requestApproval":
                    // Never create a broad grant in ConfirmWrites: it would let later writes bypass
                    // the per-operation confirmation invariant. FullAccess already has unrestricted sandboxing.
                    return AgentJson.Object(("permissions", AgentJson.Object()), ("scope", "turn"));
                case "mcpServer/elicitation/request":
                    return AgentJson.Object(("action", "decline"), ("content", null));
                default:
                    throw new AgentProviderException($"Unsupported Codex App Server client request '{method}'.");
            }
        }

        private async Task<object?> ExecuteDynamicToolAsync(
            CodexTurnOperation? operation,
            Dictionary<string, object?> parameters,
            CancellationToken clientCancellation)
        {
            if (operation == null)
                return DynamicToolResponse(AgentToolResult.Error("No active Unity Agent turn owns this tool call."));
            var name = AgentJson.GetString(parameters, "tool");
            var callId = AgentJson.GetString(parameters, "callId", AgentJson.GetString(parameters, "itemId"));
            if (!_tools.TryGet(name, out var tool))
                return DynamicToolResponse(AgentToolResult.Error($"Unknown Agent Tool '{name}'."));
            Dictionary<string, object?> arguments;
            if (parameters.TryGetValue("arguments", out var rawArguments))
            {
                arguments = EvalData.AsObject(rawArguments) ??
                            (rawArguments is string json ? AgentToolArguments.Parse(json) : AgentJson.Object());
            }
            else
            {
                arguments = AgentJson.Object();
            }

            operation.OnEvent?.Invoke(new AgentStreamEvent(AgentStreamEventKind.ToolCallStarted, name, callId));
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                clientCancellation, operation.CancellationToken);
            if (operation.Request.PermissionMode == AgentPermissionMode.ConfirmWrites &&
                tool.Descriptor.Access != AgentToolAccess.ReadOnly)
            {
                var approved = await WaitForApprovalAsync(operation, callId, name,
                    AgentJson.Stringify(arguments), tool.Descriptor.Description, linkedCancellation.Token)
                    .ConfigureAwait(false);
                if (!approved) return DynamicToolResponse(AgentToolResult.Error($"User declined '{name}'."));
            }

            AgentToolResult result;
            try
            {
                result = await tool.ExecuteAsync(
                    new AgentToolContext(operation.Request.SessionId, operation.Request.WorkingDirectory,
                        operation.Request.DefaultToolTimeoutSeconds),
                    arguments,
                    linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                result = AgentToolResult.Error($"{exception.GetType().Name}: {exception.Message}");
            }
            return DynamicToolResponse(result);
        }

        private async Task<bool> ApproveOperationAsync(
            CodexTurnOperation? operation,
            Dictionary<string, object?> parameters,
            string toolName,
            string description,
            CancellationToken clientCancellation)
        {
            if (operation == null) return false;
            if (operation.Request.PermissionMode == AgentPermissionMode.FullAccess) return true;
            var callId = AgentJson.GetString(parameters, "approvalId",
                AgentJson.GetString(parameters, "itemId", AgentJson.GetString(parameters, "callId")));
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                clientCancellation, operation.CancellationToken);
            return await WaitForApprovalAsync(operation, callId, toolName, AgentJson.Stringify(parameters),
                description, linkedCancellation.Token).ConfigureAwait(false);
        }

        private Task<bool> WaitForApprovalAsync(
            CodexTurnOperation operation,
            string callId,
            string toolName,
            string argumentsJson,
            string description,
            CancellationToken cancellationToken)
        {
            return _approvals.WaitForDecisionAsync(new AgentApprovalRequest
            {
                SessionId = operation.Request.SessionId,
                ToolCallId = callId,
                ToolName = toolName,
                ArgumentsJson = argumentsJson,
                Description = description
            }, cancellationToken);
        }

        private void HandleNotification(string method, Dictionary<string, object?> parameters)
        {
            var operation = FindOperation(parameters);
            if (operation == null) return;
            if (method == "turn/started")
            {
                if (AgentJson.GetObject(parameters, "turn") is { } startedTurn)
                    operation.TurnId = AgentJson.GetString(startedTurn, "id", operation.TurnId);
                return;
            }
            if (method == "item/agentMessage/delta")
            {
                var delta = AgentJson.GetString(parameters, "delta");
                operation.Text.Append(delta);
                operation.OnEvent?.Invoke(new AgentStreamEvent(AgentStreamEventKind.TextDelta, delta));
                return;
            }
            if (method == "item/reasoning/summaryTextDelta" || method == "item/reasoning/textDelta")
            {
                var delta = AgentJson.GetString(parameters, "delta");
                operation.OnEvent?.Invoke(new AgentStreamEvent(AgentStreamEventKind.ReasoningDelta, delta));
                return;
            }
            if (method == "thread/tokenUsage/updated")
            {
                var usage = AgentJson.GetObject(parameters, "tokenUsage");
                var last = usage == null ? null : AgentJson.GetObject(usage, "last");
                if (last != null)
                {
                    operation.Usage.InputTokens = AgentJson.GetLong(last, "inputTokens");
                    operation.Usage.OutputTokens = AgentJson.GetLong(last, "outputTokens");
                    operation.OnEvent?.Invoke(new AgentStreamEvent(AgentStreamEventKind.UsageUpdated));
                }
                return;
            }
            if (method == "turn/completed") CompleteOperation(operation, parameters);
        }

        private void CompleteOperation(CodexTurnOperation operation, Dictionary<string, object?> parameters)
        {
            var turn = AgentJson.GetObject(parameters, "turn") ?? AgentJson.Object();
            var status = AgentJson.GetString(turn, "status");
            if (!string.Equals(status, "completed", StringComparison.Ordinal))
            {
                var error = AgentJson.GetObject(turn, "error");
                var message = error == null
                    ? $"Codex turn ended with status '{status}'."
                    : AgentJson.GetString(error, "message", $"Codex turn ended with status '{status}'.");
                operation.Completion.TrySetException(new AgentProviderException(message));
                return;
            }

            var finalMessages = new List<string>();
            foreach (var item in AgentJson.Objects(AgentJson.GetArray(turn, "items")))
            {
                if (AgentJson.GetString(item, "type") != "agentMessage") continue;
                var text = AgentJson.GetString(item, "text");
                if (!string.IsNullOrWhiteSpace(text)) finalMessages.Add(text);
            }
            var finalText = finalMessages.Count > 0 ? string.Join("\n", finalMessages) : operation.Text.ToString();
            operation.OnEvent?.Invoke(new AgentStreamEvent(AgentStreamEventKind.RunCompleted));
            operation.Completion.TrySetResult(new AgentModelResponse
            {
                Text = finalText,
                Usage = operation.Usage,
                FinishReason = status,
                ProviderThreadId = operation.ThreadId
            });
        }

        private CodexTurnOperation? FindOperation(Dictionary<string, object?> parameters)
        {
            var threadId = AgentJson.GetString(parameters, "threadId",
                AgentJson.GetString(parameters, "conversationId"));
            if (string.IsNullOrWhiteSpace(threadId)) return null;
            lock (_syncRoot)
                return _operations.TryGetValue(threadId, out var operation) ? operation : null;
        }

        private void HandleClientFault(CodexAppServerClient client, Exception exception)
        {
            List<CodexTurnOperation> operations;
            lock (_syncRoot)
            {
                operations = _operations.Values.Where(value => ReferenceEquals(value.Client, client)).ToList();
                var cached = _clients.FirstOrDefault(value => ReferenceEquals(value.Value, client));
                if (!string.IsNullOrEmpty(cached.Key)) _clients.Remove(cached.Key);
            }
            foreach (var operation in operations) operation.Completion.TrySetException(exception);
            _ = Task.Run(client.Dispose);
        }

        private static Dictionary<string, object?> DynamicToolResponse(AgentToolResult result)
        {
            return AgentJson.Object(
                ("success", !result.IsError),
                ("contentItems", AgentJson.Array(AgentJson.Object(
                    ("type", "inputText"),
                    ("text", result.Text)))));
        }

        private static async Task TryInterruptAsync(CodexAppServerClient client, CodexTurnOperation operation)
        {
            if (string.IsNullOrWhiteSpace(operation.TurnId)) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await client.SendRequestAsync("turn/interrupt", AgentJson.Object(
                    ("threadId", operation.ThreadId),
                    ("turnId", operation.TurnId)), timeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException ||
                                               exception is AgentProviderException ||
                                               exception is ObjectDisposedException)
            {
                // The local turn is already cancelled. This best-effort request only stops remote work sooner.
            }
        }

        private static string BuildTurnInput(AgentModelRequest request)
        {
            var latestUserIndex = -1;
            for (var index = request.Messages.Count - 1; index >= 0; index--)
            {
                if (request.Messages[index].Role != AgentMessageRole.User) continue;
                latestUserIndex = index;
                break;
            }
            if (latestUserIndex < 0)
                throw new AgentProviderException("Codex App Server turn requires a user message.");
            var latest = request.Messages[latestUserIndex].Text;
            if (!string.IsNullOrWhiteSpace(request.ProviderThreadId) || latestUserIndex == 0) return latest;

            var transcript = new StringBuilder();
            transcript.AppendLine("The Unity host was reloaded. Reconstruct context from this persisted transcript, then handle the current request.");
            transcript.AppendLine("<persisted_transcript>");
            for (var index = 0; index < latestUserIndex; index++)
            {
                var message = request.Messages[index];
                transcript.Append('[').Append(message.Role).Append("] ").AppendLine(message.Text);
                foreach (var call in message.ToolCalls)
                    transcript.Append("[ToolCall ").Append(call.Name).Append("] ").AppendLine(call.ArgumentsJson);
            }
            transcript.AppendLine("</persisted_transcript>");
            transcript.AppendLine("<current_request>");
            transcript.AppendLine(latest);
            transcript.Append("</current_request>");
            return transcript.ToString();
        }

        private static string ResolveWorkingDirectory(string workingDirectory)
        {
            var path = string.IsNullOrWhiteSpace(workingDirectory)
                ? Directory.GetCurrentDirectory()
                : workingDirectory;
            return Path.GetFullPath(path);
        }

        private static string ResolveExecutable(AgentProviderProfile profile)
        {
            var executable = string.IsNullOrWhiteSpace(profile.BaseUrl) ? "codex" : profile.BaseUrl.Trim();
            if (executable.IndexOf("://", StringComparison.Ordinal) >= 0)
                throw new AgentProviderException(
                    "Codex App Server profile Base URL field must contain the local codex executable name/path, not an HTTP URL.");
            return executable;
        }

        private static bool IsDefaultValue(string value) =>
            string.IsNullOrWhiteSpace(value) || string.Equals(value, "default", StringComparison.OrdinalIgnoreCase);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CodexAppServerModelProvider));
        }

        private sealed class CodexTurnOperation
        {
            public CodexTurnOperation(
                CodexAppServerClient client,
                string threadId,
                AgentModelRequest request,
                Action<AgentStreamEvent>? onEvent,
                CancellationToken cancellationToken)
            {
                Client = client;
                ThreadId = threadId;
                Request = request;
                OnEvent = onEvent;
                CancellationToken = cancellationToken;
            }

            public CodexAppServerClient Client { get; }

            public string ThreadId { get; }

            public AgentModelRequest Request { get; }

            public Action<AgentStreamEvent>? OnEvent { get; }

            public CancellationToken CancellationToken { get; }

            public TaskCompletionSource<AgentModelResponse> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public StringBuilder Text { get; } = new();

            public AgentUsage Usage { get; } = new();

            public string TurnId { get; set; } = string.Empty;
        }
    }
}
