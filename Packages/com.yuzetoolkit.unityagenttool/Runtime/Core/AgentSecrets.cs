#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace YuzeToolkit.UnityAgent
{
    public interface IAgentSecretStore
    {
        void SetSessionSecret(string profileId, string secret);

        void ClearSessionSecret(string profileId);

        bool HasLocalSecret(string profileId);

        void SaveLocalSecret(string profileId, string secret);

        void ClearLocalSecret(string profileId);

        string Resolve(AgentProviderProfile profile);
    }

    public sealed class AgentSecretStore : IAgentSecretStore
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, string> _sessionSecrets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _localSecrets = new(StringComparer.Ordinal);
        private readonly string _filePath;

        public AgentSecretStore()
            : this(ResolveDefaultSecretPath())
        {
        }

        private static string ResolveDefaultSecretPath()
        {
            var current = Path.Combine(AgentPaths.SettingsRoot, AgentPaths.SecretsFileName);
            var legacy = Path.Combine(AgentPaths.LegacySettingsRoot, AgentPaths.SecretsFileName);
            if (!File.Exists(current) && File.Exists(legacy))
            {
                Directory.CreateDirectory(AgentPaths.SettingsRoot);
                File.Copy(legacy, current);
            }
            return current;
        }

        public AgentSecretStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Secret configuration path is required.", nameof(filePath));
            _filePath = Path.GetFullPath(filePath);
            LoadLocalSecrets();
        }

        public void SetSessionSecret(string profileId, string secret)
        {
            ValidateProfileId(profileId);
            if (secret != null && secret.Length > 131_072)
                throw new ArgumentException("API key exceeds the 131,072 character limit.", nameof(secret));
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
            ValidateProfileId(profileId);
            lock (_syncRoot)
                _sessionSecrets.Remove(profileId);
        }

        public bool HasLocalSecret(string profileId)
        {
            ValidateProfileId(profileId);
            lock (_syncRoot)
                return _localSecrets.ContainsKey(profileId);
        }

        public void SaveLocalSecret(string profileId, string secret)
        {
            ValidateProfileId(profileId);
            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("API key is required.", nameof(secret));
            if (secret.Length > 131_072)
                throw new ArgumentException("API key exceeds the 131,072 character limit.", nameof(secret));
            lock (_syncRoot)
            {
                var hadPrevious = _localSecrets.TryGetValue(profileId, out var previous);
                _localSecrets[profileId] = secret;
                try
                {
                    SaveLocalSecrets();
                }
                catch
                {
                    if (hadPrevious) _localSecrets[profileId] = previous!;
                    else _localSecrets.Remove(profileId);
                    throw;
                }
            }
        }

        public void ClearLocalSecret(string profileId)
        {
            ValidateProfileId(profileId);
            lock (_syncRoot)
            {
                if (!_localSecrets.TryGetValue(profileId, out var previous)) return;
                _localSecrets.Remove(profileId);
                try
                {
                    SaveLocalSecrets();
                }
                catch
                {
                    _localSecrets[profileId] = previous;
                    throw;
                }
            }
        }

        public string Resolve(AgentProviderProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            lock (_syncRoot)
            {
                if (_sessionSecrets.TryGetValue(profile.Id, out var value))
                    return value;
                if (_localSecrets.TryGetValue(profile.Id, out value))
                    return value;
            }

            return string.IsNullOrWhiteSpace(profile.SecretEnvironmentVariable)
                ? string.Empty
                : Environment.GetEnvironmentVariable(profile.SecretEnvironmentVariable) ?? string.Empty;
        }

        private void LoadLocalSecrets()
        {
            lock (_syncRoot)
            {
                if (!File.Exists(_filePath)) return;
                var json = File.ReadAllText(_filePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    throw new InvalidDataException($"Agent secret configuration is empty: {_filePath}");
                try
                {
                    var root = AgentJson.ParseObject(json);
                    var version = AgentJson.GetSchemaVersion(root);
                    if (version != 1)
                        throw new FormatException($"Unsupported Agent secret schema version {version}.");
                    var profiles = AgentJson.GetOptionalObject(root, "profiles") ??
                                   throw new FormatException("Agent secret configuration is missing 'profiles'.");
                    foreach (var entry in profiles)
                    {
                        ValidateProfileId(entry.Key);
                        if (entry.Value is not string value || string.IsNullOrWhiteSpace(value))
                            throw new FormatException(
                                $"Agent secret profile '{entry.Key}' must contain a non-empty string.");
                        if (value.Length > 131_072)
                            throw new FormatException(
                                $"Agent secret profile '{entry.Key}' exceeds the 131,072 character limit.");
                        _localSecrets.Add(entry.Key, value);
                    }
                }
                catch (Exception exception) when (exception is FormatException or ArgumentException)
                {
                    throw new InvalidDataException(
                        $"Agent secret configuration is invalid: {_filePath}", exception);
                }
            }
        }

        private void SaveLocalSecrets()
        {
            var directory = Path.GetDirectoryName(_filePath) ??
                            throw new InvalidOperationException("Secret configuration path has no parent directory.");
            Directory.CreateDirectory(directory);
            var profiles = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var entry in _localSecrets.OrderBy(value => value.Key, StringComparer.Ordinal))
                profiles.Add(entry.Key, entry.Value);
            var json = AgentJson.Stringify(AgentJson.Object(
                ("schemaVersion", 1),
                ("profiles", profiles)));
            var temporary = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                RestrictFileAccess(temporary);
                if (File.Exists(_filePath))
                {
                    try
                    {
                        File.Replace(temporary, _filePath, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithoutFileReplace(temporary);
                    }
                }
                else
                {
                    File.Move(temporary, _filePath);
                }
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private void ReplaceWithoutFileReplace(string temporary)
        {
            var previous = _filePath + ".previous";
            try
            {
                if (File.Exists(previous)) File.Delete(previous);
                File.Move(_filePath, previous);
                File.Move(temporary, _filePath);
                File.Delete(previous);
            }
            catch
            {
                if (!File.Exists(_filePath) && File.Exists(previous))
                    File.Move(previous, _filePath);
                throw;
            }
        }

        private static void ValidateProfileId(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                throw new ArgumentException("Profile id is required.", nameof(profileId));
            if (profileId.Length > 256)
                throw new ArgumentException("Profile id exceeds the 256 character limit.", nameof(profileId));
            foreach (var character in profileId)
            {
                if (!char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.')
                    throw new ArgumentException("Profile id contains unsupported characters.", nameof(profileId));
            }
        }

        private static void RestrictFileAccess(string path)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            const uint ownerReadWrite = 0x180; // POSIX 0600
            if (Chmod(path, ownerReadWrite) != 0)
                throw new IOException(
                    $"Failed to restrict Agent secret configuration permissions: {path} " +
                    $"(errno {Marshal.GetLastWin32Error()}).");
#endif
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int Chmod(string path, uint mode);
#endif
    }
}
