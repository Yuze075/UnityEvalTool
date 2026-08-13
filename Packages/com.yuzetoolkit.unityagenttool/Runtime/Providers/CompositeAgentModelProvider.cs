#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YuzeToolkit.UnityAgent
{
    /// <summary>
    /// Keeps model HTTP protocols and the Codex Agent runtime behind the same host-facing contract.
    /// Codex App Server is intentionally not represented as an HTTP wire protocol because it owns
    /// a complete Agent loop and communicates over a local JSONL process channel.
    /// </summary>
    internal sealed class CompositeAgentModelProvider : IAgentModelProvider, IDisposable
    {
        private readonly HttpAgentModelProvider _http;
        private readonly CodexAppServerModelProvider _codex;

        public CompositeAgentModelProvider(
            IAgentSecretStore secretStore,
            AgentToolRegistry tools,
            AgentApprovalService approvals)
        {
            _http = new HttpAgentModelProvider(secretStore);
            _codex = new CodexAppServerModelProvider(tools, approvals);
        }

        public Task<AgentModelResponse> CompleteAsync(
            AgentProviderProfile profile,
            AgentModelRequest request,
            Action<AgentStreamEvent>? onEvent,
            CancellationToken cancellationToken)
        {
            return IsCodex(profile)
                ? _codex.CompleteAsync(profile, request, onEvent, cancellationToken)
                : _http.CompleteAsync(profile, request, onEvent, cancellationToken);
        }

        public Task<IReadOnlyList<string>> ListModelsAsync(
            AgentProviderProfile profile,
            CancellationToken cancellationToken)
        {
            return IsCodex(profile)
                ? _codex.ListModelsAsync(profile, cancellationToken)
                : _http.ListModelsAsync(profile, cancellationToken);
        }

        public async Task<AgentModelDiscoveryResult> DiscoverModelsAsync(
            AgentProviderProfile profile,
            CancellationToken cancellationToken)
        {
            if (!IsCodex(profile))
                return await _http.DiscoverModelsAsync(profile, cancellationToken).ConfigureAwait(false);
            var models = await _codex.ListModelsAsync(profile, cancellationToken).ConfigureAwait(false);
            return AgentProviderCatalog.MergeRemoteModels(profile, models);
        }

        public void Dispose()
        {
            _codex.Dispose();
            _http.Dispose();
        }

        public Task<AgentCodexAccountStatus> GetCodexAccountAsync(
            AgentProviderProfile profile,
            CancellationToken cancellationToken) =>
            _codex.GetAccountAsync(profile, cancellationToken);

        public Task<AgentCodexLogin> StartCodexLoginAsync(
            AgentProviderProfile profile,
            bool deviceCode,
            CancellationToken cancellationToken) =>
            _codex.StartLoginAsync(profile, deviceCode, cancellationToken);

        private static bool IsCodex(AgentProviderProfile profile) =>
            string.Equals(profile.Protocol, AgentProtocolIds.CodexAppServer, StringComparison.Ordinal);
    }
}
