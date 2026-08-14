#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YuzeToolkit.UnityAgent
{
    internal sealed class AgentLoop
    {
        private const int TargetRetainedMessageCount = 80;
        private const int CompactionMessageCount = 120;
        private const int MaximumRetainedContextCharacters = 8_000_000;
        private const int MaximumSummaryCharacters = 200_000;
        private const int MaximumMessageTextCharacters = 1_000_000;
        private const int MaximumProviderDataCharacters = 4_000_000;
        private const int MaximumToolArgumentsCharacters = 1_000_000;
        private const int MinimumTruncatedToolCharacters = 4_096;
        private readonly AgentToolRegistry _tools;
        private readonly AgentApprovalService _approvals;
        private readonly AgentInstructionService _instructions;
        private readonly IAgentModelProvider _provider;

        public AgentLoop(
            AgentToolRegistry tools,
            AgentApprovalService approvals,
            AgentInstructionService instructions,
            IAgentModelProvider provider)
        {
            _tools = tools;
            _approvals = approvals;
            _instructions = instructions;
            _provider = provider;
        }

        public async Task RunAsync(
            AgentSessionRuntime runtime,
            AgentSettingsDocument settings,
            AgentProviderProfile profile,
            Func<Task> save,
            Action changed,
            CancellationToken cancellationToken)
        {
            var instructions = await _instructions.LoadAsync(settings, runtime.Document.WorkingDirectory,
                cancellationToken).ConfigureAwait(false);
            // Editor and Player have different capabilities and therefore use independent
            // package-wide prompts. Conversations deliberately do not carry hidden overrides.
            var systemPrompt = (AgentPaths.IsEditor
                ? settings.EditorSystemPrompt
                : settings.RuntimeSystemPrompt) + instructions.Prompt;
            for (long step = 0; ; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (runtime.SyncRoot)
                {
                    runtime.Document.State = AgentSessionState.Running;
                    runtime.Document.CompletedSteps = (int)Math.Min(int.MaxValue, step);
                    runtime.Document.PendingApproval = null;
                    runtime.LiveText = string.Empty;
                    runtime.LiveReasoning = string.Empty;
                    runtime.Document.UpdatedAtUtc = DateTime.UtcNow;
                }
                changed();
                await save().ConfigureAwait(false);

                var request = BuildRequest(runtime, profile, systemPrompt, settings.DefaultToolTimeoutSeconds);
                var response = await _provider.CompleteAsync(profile, request, streamEvent =>
                {
                    lock (runtime.SyncRoot)
                    {
                        if (streamEvent.Kind == AgentStreamEventKind.TextDelta)
                            runtime.LiveText += streamEvent.Text;
                        else if (streamEvent.Kind == AgentStreamEventKind.ReasoningDelta)
                            runtime.LiveReasoning += streamEvent.Text;
                    }
                    changed();
                }, cancellationToken).ConfigureAwait(false);
                ValidateProviderResponse(response);

                lock (runtime.SyncRoot)
                {
                    runtime.LiveText = string.Empty;
                    runtime.LiveReasoning = string.Empty;
                    runtime.Document.Messages.Add(new AgentMessage
                    {
                        Role = AgentMessageRole.Assistant,
                        Text = response.Text,
                        ToolCalls = response.ToolCalls,
                        ProviderDataJson = response.ProviderDataJson
                    });
                    if (!string.IsNullOrWhiteSpace(response.ProviderThreadId))
                        runtime.Document.ProviderThreadId = response.ProviderThreadId;
                    runtime.Document.Usage.InputTokens += response.Usage.InputTokens;
                    runtime.Document.Usage.OutputTokens += response.Usage.OutputTokens;
                    runtime.Document.UpdatedAtUtc = DateTime.UtcNow;
                    CompactContextLocked(runtime.Document);
                }
                changed();
                await save().ConfigureAwait(false);

                if (response.ToolCalls.Count == 0)
                {
                    lock (runtime.SyncRoot)
                    {
                        runtime.Document.State = AgentSessionState.Completed;
                        runtime.Document.CompletedSteps = (int)Math.Min(int.MaxValue, step + 1);
                    }
                    changed();
                    await save().ConfigureAwait(false);
                    return;
                }

                foreach (var call in response.ToolCalls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await ExecuteToolAsync(runtime, call, settings.DefaultToolTimeoutSeconds, save,
                            changed, cancellationToken)
                        .ConfigureAwait(false);
                    result = BoundToolResult(result);
                    lock (runtime.SyncRoot)
                    {
                        runtime.Document.Messages.Add(new AgentMessage
                        {
                            Role = AgentMessageRole.Tool,
                            ToolCallId = call.Id,
                            ToolName = call.Name,
                            Text = result.Text,
                            IsError = result.IsError
                        });
                        runtime.Document.State = AgentSessionState.Running;
                        runtime.Document.PendingApproval = null;
                        runtime.Document.UpdatedAtUtc = DateTime.UtcNow;
                        CompactContextLocked(runtime.Document);
                    }
                    changed();
                    await save().ConfigureAwait(false);
                }
            }

        }

        private AgentModelRequest BuildRequest(
            AgentSessionRuntime runtime,
            AgentProviderProfile profile,
            string systemPrompt,
            int defaultToolTimeoutSeconds)
        {
            List<AgentMessage> messages;
            string model;
            string effort;
            lock (runtime.SyncRoot)
            {
                messages = ProjectMessages(runtime.Document);
                model = string.IsNullOrWhiteSpace(runtime.Document.Model) ? profile.Model : runtime.Document.Model;
                effort = string.IsNullOrWhiteSpace(runtime.Document.ReasoningEffort)
                    ? profile.ReasoningEffort
                    : runtime.Document.ReasoningEffort;
            }
            return new AgentModelRequest
            {
                SessionId = runtime.Document.Id,
                ProviderThreadId = runtime.Document.ProviderThreadId,
                WorkingDirectory = runtime.Document.WorkingDirectory,
                PermissionMode = runtime.Document.PermissionMode,
                DefaultToolTimeoutSeconds = Math.Max(1, defaultToolTimeoutSeconds),
                SystemPrompt = systemPrompt,
                Model = model,
                ReasoningEffort = effort,
                MaxOutputTokens = Math.Max(1, profile.MaxOutputTokens),
                Messages = messages,
                Tools = _tools.ListDescriptors()
            };
        }

        private async Task<AgentToolResult> ExecuteToolAsync(
            AgentSessionRuntime runtime,
            AgentToolCall call,
            int defaultToolTimeoutSeconds,
            Func<Task> save,
            Action changed,
            CancellationToken cancellationToken)
        {
            if (!_tools.TryGet(call.Name, out var tool))
                return AgentToolResult.Error($"Unknown Agent Tool '{call.Name}'.");

            Dictionary<string, object?> arguments;
            try
            {
                arguments = AgentToolArguments.Parse(call.ArgumentsJson);
            }
            catch (ArgumentException exception)
            {
                return AgentToolResult.Error(exception.Message);
            }

            AgentPermissionMode permissionMode;
            lock (runtime.SyncRoot) permissionMode = runtime.Document.PermissionMode;
            if (permissionMode == AgentPermissionMode.ConfirmWrites &&
                tool.Descriptor.Access != AgentToolAccess.ReadOnly)
            {
                var approval = new AgentApprovalRequest
                {
                    SessionId = runtime.Document.Id,
                    ToolCallId = call.Id,
                    ToolName = call.Name,
                    ArgumentsJson = call.ArgumentsJson,
                    Description = tool.Descriptor.Description
                };
                lock (runtime.SyncRoot)
                {
                    runtime.Document.State = AgentSessionState.AwaitingApproval;
                    runtime.Document.PendingApproval = approval;
                    runtime.Document.UpdatedAtUtc = DateTime.UtcNow;
                }
                var approved = await WaitForApprovalAsync(approval, save, changed, cancellationToken)
                    .ConfigureAwait(false);
                if (!approved)
                    return AgentToolResult.Error($"User declined '{call.Name}'.");
            }

            lock (runtime.SyncRoot)
            {
                runtime.Document.State = AgentSessionState.Running;
                runtime.Document.PendingApproval = null;
            }
            changed();
            await save().ConfigureAwait(false);
            try
            {
                return await tool.ExecuteAsync(
                    new AgentToolContext(runtime.Document.Id, runtime.Document.WorkingDirectory,
                        defaultToolTimeoutSeconds),
                    arguments,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return AgentToolResult.Error($"{exception.GetType().Name}: {exception.Message}");
            }
        }

        private Task<bool> WaitForApprovalAsync(
            AgentApprovalRequest approval,
            Func<Task> save,
            Action changed,
            CancellationToken cancellationToken)
        {
            return _approvals.WaitForDecisionAsync(approval, cancellationToken, async () =>
            {
                changed();
                await save().ConfigureAwait(false);
            });
        }

        private static List<AgentMessage> ProjectMessages(AgentSessionDocument document)
        {
            if (string.IsNullOrWhiteSpace(document.Summary))
                return new List<AgentMessage>(document.Messages);
            var messages = new List<AgentMessage>
            {
                new()
                {
                    Role = AgentMessageRole.User,
                    Text = "Earlier conversation summary:\n" + document.Summary
                }
            };
            messages.AddRange(document.Messages);
            return messages;
        }

        private static void CompactContextLocked(AgentSessionDocument document)
        {
            var messages = document.Messages;
            if (messages.Count == 0) return;

            var through = messages.Count > CompactionMessageCount
                ? messages.Count - TargetRetainedMessageCount
                : 0;
            long retainedCharacters = 0;
            for (var index = messages.Count - 1; index >= 0; index--)
            {
                retainedCharacters += EstimateCharacters(messages[index]);
                if (retainedCharacters <= MaximumRetainedContextCharacters) continue;
                through = Math.Max(through, index + 1);
                break;
            }

            through = Math.Min(through, Math.Max(0, messages.Count - 1));
            through = MoveToConversationBoundary(messages, through);
            if (through > 0)
            {
                var summary = new StringBuilder(document.Summary);
                for (var index = 0; index < through; index++)
                    AppendSummary(summary, messages[index]);
                document.Summary = BoundSummary(summary.ToString());
                document.SummarizedMessageCount = document.SummarizedMessageCount > int.MaxValue - through
                    ? int.MaxValue
                    : document.SummarizedMessageCount + through;
                messages.RemoveRange(0, through);
            }

            EnforceRetainedBudget(messages);
        }

        private static int MoveToConversationBoundary(IReadOnlyList<AgentMessage> messages, int candidate)
        {
            candidate = Math.Max(0, Math.Min(candidate, messages.Count));
            while (candidate > 0 && candidate < messages.Count &&
                   messages[candidate].Role == AgentMessageRole.Tool)
                candidate--;
            return candidate;
        }

        private static void ValidateProviderResponse(AgentModelResponse response)
        {
            if (response == null) throw new InvalidDataException("Model provider returned no response.");
            response.Text ??= string.Empty;
            response.ProviderDataJson ??= string.Empty;
            response.Usage ??= new AgentUsage();
            if (response.Text.Length > MaximumMessageTextCharacters)
                throw new InvalidDataException(
                    $"Model response text exceeds {MaximumMessageTextCharacters:N0} characters.");
            if (response.ProviderDataJson.Length > MaximumProviderDataCharacters)
                throw new InvalidDataException(
                    $"Model provider state exceeds {MaximumProviderDataCharacters:N0} characters.");
            if (response.ToolCalls == null)
                throw new InvalidDataException("Model provider returned a null tool-call collection.");
            long totalArguments = 0;
            foreach (var call in response.ToolCalls)
            {
                if (call == null) throw new InvalidDataException("Model provider returned a null tool call.");
                call.Id ??= string.Empty;
                call.Name ??= string.Empty;
                call.ArgumentsJson ??= "{}";
                call.ProviderItemId ??= string.Empty;
                if (call.ArgumentsJson.Length > MaximumToolArgumentsCharacters)
                    throw new InvalidDataException(
                        $"Tool call '{call.Name}' arguments exceed {MaximumToolArgumentsCharacters:N0} characters.");
                totalArguments += call.ArgumentsJson.Length;
                if (totalArguments > MaximumProviderDataCharacters)
                    throw new InvalidDataException(
                        $"Model tool-call arguments exceed {MaximumProviderDataCharacters:N0} characters in one response.");
            }
        }

        private static AgentToolResult BoundToolResult(AgentToolResult result)
        {
            if (result == null) return AgentToolResult.Error("Agent Tool returned no result.");
            result.Text ??= string.Empty;
            if (result.Text.Length <= MaximumMessageTextCharacters) return result;
            return new AgentToolResult
            {
                IsError = result.IsError,
                Text = ElideMiddle(result.Text, MaximumMessageTextCharacters,
                    "\n… UnityAgentTool truncated this tool result for bounded conversation storage …\n")
            };
        }

        private static void AppendSummary(StringBuilder summary, AgentMessage message)
        {
            summary.Append(message.Role).Append(": ");
            var text = message.Text.Replace('\r', ' ').Replace('\n', ' ');
            summary.Append(text.Length <= 500 ? text : text.Substring(0, 500) + "…");
            foreach (var call in message.ToolCalls)
                summary.Append(" [tool ").Append(call.Name).Append(']');
            summary.AppendLine();
        }

        private static string BoundSummary(string summary)
        {
            return summary.Length <= MaximumSummaryCharacters
                ? summary
                : ElideMiddle(summary, MaximumSummaryCharacters,
                    "\n… older mechanical summary content omitted to keep history bounded …\n");
        }

        private static void EnforceRetainedBudget(IReadOnlyList<AgentMessage> messages)
        {
            long total = messages.Sum(EstimateCharacters);
            if (total <= MaximumRetainedContextCharacters) return;

            // A large parallel tool batch is one logical conversation boundary and cannot be
            // removed before the next model call. Reduce its oldest verbose results instead.
            foreach (var message in messages.Where(value => value.Role == AgentMessageRole.Tool))
            {
                if (total <= MaximumRetainedContextCharacters) break;
                var reducible = Math.Max(0, message.Text.Length - MinimumTruncatedToolCharacters);
                if (reducible == 0) continue;
                var reduction = (int)Math.Min(reducible, total - MaximumRetainedContextCharacters);
                var target = message.Text.Length - reduction;
                message.Text = ElideMiddle(message.Text, target,
                    "\n… tool result compacted for context budget …\n");
                total = messages.Sum(EstimateCharacters);
            }

            if (total > MaximumRetainedContextCharacters)
                throw new InvalidDataException(
                    $"The active conversation boundary exceeds the {MaximumRetainedContextCharacters:N0} character context budget.");
        }

        private static long EstimateCharacters(AgentMessage message)
        {
            long result = message.Text.Length + message.ProviderDataJson.Length + message.ToolCallId.Length +
                          message.ToolName.Length + 256;
            foreach (var call in message.ToolCalls)
                result += call.Id.Length + call.Name.Length + call.ArgumentsJson.Length + call.ProviderItemId.Length + 128;
            return result;
        }

        private static string ElideMiddle(string value, int maximumCharacters, string marker)
        {
            if (maximumCharacters <= 0) return string.Empty;
            if (value.Length <= maximumCharacters) return value;
            if (marker.Length >= maximumCharacters) return marker.Substring(0, maximumCharacters);
            var available = maximumCharacters - marker.Length;
            var head = available / 2;
            var tail = available - head;
            return value.Substring(0, head) + marker + value.Substring(value.Length - tail, tail);
        }
    }

    internal sealed class AgentSessionRuntime : IDisposable
    {
        public AgentSessionRuntime(AgentSessionDocument document)
        {
            Document = document;
        }

        public object SyncRoot { get; } = new();

        public AgentSessionDocument Document { get; }

        public SemaphoreSlim TurnGate { get; } = new(1, 1);

        public CancellationTokenSource? ActiveCancellation { get; set; }

        public bool IsDeleting { get; set; }

        public string LiveText { get; set; } = string.Empty;

        public string LiveReasoning { get; set; } = string.Empty;

        public void Dispose()
        {
            ActiveCancellation?.Cancel();
            ActiveCancellation?.Dispose();
            TurnGate.Dispose();
        }
    }
}
