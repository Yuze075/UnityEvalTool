#nullable enable
using System;
using System.Collections.Generic;

namespace YuzeToolkit.UnityAgent
{
    public interface IAgentSecretStore
    {
        void SetSessionSecret(string profileId, string secret);

        void ClearSessionSecret(string profileId);

        string Resolve(AgentProviderProfile profile);
    }

    public sealed class AgentSecretStore : IAgentSecretStore
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, string> _sessionSecrets = new(StringComparer.Ordinal);

        public void SetSessionSecret(string profileId, string secret)
        {
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile id is required.", nameof(profileId));
            lock (_syncRoot)
            {
                if (string.IsNullOrWhiteSpace(secret))
                    _sessionSecrets.Remove(profileId);
                else
                    _sessionSecrets[profileId] = secret;
            }
        }

        public void ClearSessionSecret(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile id is required.", nameof(profileId));
            lock (_syncRoot)
                _sessionSecrets.Remove(profileId);
        }

        public string Resolve(AgentProviderProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            lock (_syncRoot)
            {
                if (_sessionSecrets.TryGetValue(profile.Id, out var value))
                    return value;
            }

            return string.IsNullOrWhiteSpace(profile.SecretEnvironmentVariable)
                ? string.Empty
                : Environment.GetEnvironmentVariable(profile.SecretEnvironmentVariable) ?? string.Empty;
        }
    }
}
