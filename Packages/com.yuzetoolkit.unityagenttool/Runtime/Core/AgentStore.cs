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
    public interface IAgentStore
    {
        Task<IReadOnlyList<AgentSessionDocument>> LoadSessionsAsync(CancellationToken cancellationToken);

        Task SaveSessionAsync(AgentSessionDocument session, CancellationToken cancellationToken);

        Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken);

        Task<AgentSettingsDocument> LoadSettingsAsync(CancellationToken cancellationToken);

        Task SaveSettingsAsync(AgentSettingsDocument settings, CancellationToken cancellationToken);
    }

    public sealed class FileAgentStore : IAgentStore, IDisposable
    {
        private const string LegacyMigrationMarkerName = ".legacy-store-migrated-v1";
        private const int MaximumDocumentCharacters = 64_000_000;
        private readonly string _settingsRootPath;
        private readonly bool _usesDefaultSettingsRoot;
        private readonly AgentProjectSettingsDocument _projectDefaults;
        private readonly SemaphoreSlim _ioGate = new(1, 1);
        private readonly string _sessionsPath;
        private bool _settingsLoaded;

        public FileAgentStore(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Storage root is required.", nameof(rootPath));
            _settingsRootPath = Path.GetFullPath(rootPath);
            _usesDefaultSettingsRoot = AgentPaths.PathsEqual(_settingsRootPath, GetDefaultRootPath());
            _projectDefaults = UnityAgentProjectSettings.Load();
            _sessionsPath = Path.Combine(_settingsRootPath, AgentPaths.AgentConversationsFolderName);
        }

        /// <summary>The fixed directory containing settings.json.</summary>
        public string RootPath => _settingsRootPath;

        /// <summary>The fixed directory containing Agent conversation documents.</summary>
        public string HistoryRootPath => _sessionsPath;

        public static string GetDefaultRootPath() => AgentPaths.SettingsRoot;

        public async Task<IReadOnlyList<AgentSessionDocument>> LoadSessionsAsync(
            CancellationToken cancellationToken)
        {
            await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Directory.Exists(_sessionsPath)) return (IReadOnlyList<AgentSessionDocument>)Array.Empty<AgentSessionDocument>();
                    var sessions = new List<AgentSessionDocument>();
                    var sessionsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var rewrites = new List<(string Path, AgentSessionDocument Session)>();
                    var paths = Directory.EnumerateFiles(_sessionsPath, "*", SearchOption.TopDirectoryOnly)
                        .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                       path.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase))
                        .Select(path => path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
                            ? path.Substring(0, path.Length - 4)
                            : path)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(path => path, StringComparer.Ordinal);
                    foreach (var path in paths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var requiresUpgrade = StoredSessionRequiresUpgrade(path, cancellationToken);
                        var restoreMissingPrimary = !File.Exists(path) && File.Exists(path + ".bak");
                        var session = ReadDocument(path, AgentDocumentCodec.DeserializeSession, cancellationToken);
                        ValidateLoadedSessionIdentity(path, session, sessionsById);
                        if (requiresUpgrade || restoreMissingPrimary)
                            rewrites.Add((path, session));
                        sessions.Add(session);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    // Validate the complete set before upgrading or restoring any document. A malformed
                    // later file must not leave the history directory partially rewritten.
                    foreach (var rewrite in rewrites)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        WriteAtomic(rewrite.Path, AgentDocumentCodec.SerializeSession(rewrite.Session),
                            cancellationToken);
                    }

                    return sessions.OrderByDescending(session => session.UpdatedAtUtc).ToList();
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ioGate.Release();
            }
        }

        public async Task SaveSessionAsync(AgentSessionDocument session, CancellationToken cancellationToken)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var fileName = ValidateId(session.Id) + ".json";
            var json = AgentDocumentCodec.SerializeSession(session);
            await WriteAsync(Path.Combine(_sessionsPath, fileName), json, cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            var path = Path.Combine(_sessionsPath, ValidateId(sessionId) + ".json");
            await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (File.Exists(path)) File.Delete(path);
                    var backup = path + ".bak";
                    if (File.Exists(backup)) File.Delete(backup);
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ioGate.Release();
            }
        }

        public async Task<AgentSettingsDocument> LoadSettingsAsync(CancellationToken cancellationToken)
        {
            var path = Path.Combine(_settingsRootPath, AgentPaths.SettingsFileName);
            await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(_settingsRootPath);
                    var legacyRoot = _usesDefaultSettingsRoot ? GetLegacyRootPath() : string.Empty;
                    var legacySettingsPath = string.IsNullOrEmpty(legacyRoot)
                        ? string.Empty
                        : Path.Combine(legacyRoot, AgentPaths.SettingsFileName);
                    AgentSettingsDocument settings;
                    if (File.Exists(path) || File.Exists(path + ".bak"))
                    {
                        var requiresUpgrade = StoredSettingsRequireUpgrade(path, cancellationToken);
                        var restoreMissingPrimary = !File.Exists(path) && File.Exists(path + ".bak");
                        settings = ReadDocument(path, AgentDocumentCodec.DeserializeSettings, cancellationToken);
                        if (requiresUpgrade || restoreMissingPrimary)
                            WriteAtomic(path, AgentDocumentCodec.SerializeSettings(settings), cancellationToken);
                    }
                    else if (!string.IsNullOrEmpty(legacySettingsPath) &&
                             (File.Exists(legacySettingsPath) || File.Exists(legacySettingsPath + ".bak")))
                    {
                        settings = ReadDocument(legacySettingsPath, AgentDocumentCodec.DeserializeSettings,
                            cancellationToken);
                        WriteAtomic(path, AgentDocumentCodec.SerializeSettings(settings), cancellationToken);
                    }
                    else
                    {
                        settings = CreateMachineDefaults();
                        // Materialize defaults immediately so users may edit the complete configuration file.
                        WriteAtomic(path, AgentDocumentCodec.SerializeSettings(settings), cancellationToken);
                    }

                    ApplyLoadedHistoryPath(legacyRoot, cancellationToken);
                    return settings;
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ioGate.Release();
            }
        }

        public async Task SaveSettingsAsync(AgentSettingsDocument settings, CancellationToken cancellationToken)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var json = AgentDocumentCodec.SerializeSettings(settings);
            await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WriteAtomic(Path.Combine(_settingsRootPath, AgentPaths.SettingsFileName), json,
                        cancellationToken);
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ioGate.Release();
            }
        }

        public void Dispose()
        {
            _ioGate.Dispose();
        }

        private async Task WriteAsync(string path, string json, CancellationToken cancellationToken)
        {
            await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() => WriteAtomic(path, json, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ioGate.Release();
            }
        }

        private static void WriteAtomic(string path, string json, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (json.Length > MaximumDocumentCharacters)
                throw new InvalidDataException(
                    $"Agent document exceeds the {MaximumDocumentCharacters:N0} character storage limit: {path}");
            var directory = Path.GetDirectoryName(path)
                            ?? throw new InvalidOperationException("Storage path has no parent directory.");
            Directory.CreateDirectory(directory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var backup = path + ".bak";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporary, path, backup);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithoutFileReplace(temporary, path, backup, cancellationToken);
                    }
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                TryDeleteTemporary(temporary);
            }
        }

        private static T ReadDocument<T>(
            string path,
            Func<string, T> deserialize,
            CancellationToken cancellationToken)
        {
            if (File.Exists(path))
            {
                try
                {
                    return deserialize(ReadStoredText(path, cancellationToken));
                }
                catch (Exception exception) when (IsRecoverableDocumentError(exception))
                {
                    var backupPath = path + ".bak";
                    var recovery = File.Exists(backupPath)
                        ? $" A backup is available at '{backupPath}'; restore it explicitly after reviewing the error."
                        : " No backup is available.";
                    throw new InvalidDataException($"Agent document is unreadable: {path}.{recovery}", exception);
                }
            }

            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    return deserialize(ReadStoredText(backup, cancellationToken));
                }
                catch (Exception backupError) when (IsRecoverableDocumentError(backupError))
                {
                    throw new InvalidDataException($"Agent document backup is unreadable: {backup}", backupError);
                }
            }

            throw new FileNotFoundException("Agent document was not found.", path);
        }

        private static string ReadStoredText(string path, CancellationToken cancellationToken)
        {
            var text = new StringBuilder(Math.Min(MaximumDocumentCharacters, 16_384));
            var buffer = new char[8_192];
            using var reader = new StreamReader(path, Encoding.UTF8, true);
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (text.Length + read > MaximumDocumentCharacters)
                    throw new InvalidDataException(
                        $"Agent document exceeds the {MaximumDocumentCharacters:N0} character storage limit: {path}");
                text.Append(buffer, 0, read);
            }
            return text.ToString();
        }

        private static bool IsRecoverableDocumentError(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is FormatException ||
                   exception is InvalidOperationException ||
                   exception is ArgumentException ||
                   exception is OverflowException;
        }

        private static void ReplaceWithoutFileReplace(
            string temporary,
            string path,
            string backup,
            CancellationToken cancellationToken)
        {
            File.Copy(path, backup, true);
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(temporary, path, true);
            File.Delete(temporary);
        }

        private static void TryDeleteTemporary(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // The destination write has already completed or failed; a stale unique temp file is recoverable.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve the original write result instead of replacing it with a cleanup-only failure.
            }
        }

        private static string ValidateId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Session id is required.", nameof(value));
            foreach (var character in value)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                    throw new ArgumentException("Session id contains unsupported characters.", nameof(value));
            }

            return value;
        }

        private static void ValidateLoadedSessionIdentity(
            string primaryPath,
            AgentSessionDocument session,
            IDictionary<string, string> sessionsById)
        {
            string documentId;
            try
            {
                documentId = ValidateId(session.Id);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"Agent session document '{primaryPath}' contains an invalid session id.", exception);
            }

            var fileName = Path.GetFileName(primaryPath);
            var expectedId = Path.GetFileNameWithoutExtension(fileName);
            try
            {
                ValidateId(expectedId);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"Agent session file name '{fileName}' does not contain a valid session id.", exception);
            }

            if (!string.Equals(documentId, expectedId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Agent session file name '{fileName}' identifies session '{expectedId}', " +
                    $"but the document id is '{documentId}'.");
            }

            if (sessionsById.TryGetValue(documentId, out var existingPath))
            {
                throw new InvalidDataException(
                    $"Duplicate agent session id '{documentId}' is stored in '{existingPath}' and '{primaryPath}'.");
            }
            sessionsById.Add(documentId, primaryPath);
        }

        private void ApplyLoadedHistoryPath(string legacyRoot, CancellationToken cancellationToken)
        {
            if (!_settingsLoaded && _usesDefaultSettingsRoot)
                MigrateLegacySessionsOnce(legacyRoot, cancellationToken);
            _settingsLoaded = true;
        }

        private void MigrateLegacySessionsOnce(string legacyRoot, CancellationToken cancellationToken)
        {
            var marker = Path.Combine(_settingsRootPath, LegacyMigrationMarkerName);
            if (File.Exists(marker)) return;
            var legacyCandidates = new[]
            {
                Path.Combine(AgentPaths.LegacySettingsRoot, AgentPaths.AgentConversationsFolderName),
                Path.Combine(AgentPaths.LegacySettingsRoot, "Sessions"),
                string.IsNullOrEmpty(legacyRoot) ? string.Empty : Path.Combine(legacyRoot, "Sessions"),
                Path.Combine(_settingsRootPath, "Sessions")
            };
            foreach (var source in legacyCandidates.Where(value => !string.IsNullOrEmpty(value)))
                CopySessionDocuments(source, _sessionsPath, cancellationToken);
            WriteAtomic(marker, "UnityAgentTool legacy store migration completed.\n", cancellationToken);
        }

        private AgentSettingsDocument CreateMachineDefaults()
        {
            var settings = AgentSettingsDocument.CreateDefault();
            _projectDefaults.ApplyTo(settings);
            return settings;
        }

        private static string GetLegacyRootPath()
        {
            var recent = AgentPaths.LegacySettingsRoot;
            if (File.Exists(Path.Combine(recent, AgentPaths.SettingsFileName)) ||
                File.Exists(Path.Combine(recent, AgentPaths.SettingsFileName) + ".bak")) return recent;
            return Path.GetFullPath(AgentPaths.IsEditor
                ? Path.Combine(AgentPaths.ProjectRoot, "Library", "UnityAgentTool")
                : Path.Combine(AgentPaths.GetBasePath(AgentPathBase.PersistentData), "UnityAgentTool"));
        }

        private static void CopySessionDocuments(
            string sourceDirectory,
            string destinationDirectory,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(sourceDirectory) || AgentPaths.PathsEqual(sourceDirectory, destinationDirectory))
                return;
            Directory.CreateDirectory(destinationDirectory);
            foreach (var source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly)
                         .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                        path.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
                if (File.Exists(destination))
                {
                    if (!FilesEqual(source, destination, cancellationToken))
                        throw new IOException(
                            $"Conversation history migration found conflicting documents: {source} and {destination}");
                    continue;
                }

                var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.Copy(source, temporary, false);
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Move(temporary, destination);
                }
                finally
                {
                    TryDeleteTemporary(temporary);
                }
            }
        }

        private static bool FilesEqual(string first, string second, CancellationToken cancellationToken)
        {
            var firstInfo = new FileInfo(first);
            var secondInfo = new FileInfo(second);
            if (firstInfo.Length != secondInfo.Length) return false;
            const int bufferSize = 8192;
            var firstBuffer = new byte[bufferSize];
            var secondBuffer = new byte[bufferSize];
            using var firstStream = File.OpenRead(first);
            using var secondStream = File.OpenRead(second);
            int read;
            while ((read = firstStream.Read(firstBuffer, 0, firstBuffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (secondStream.Read(secondBuffer, 0, read) != read) return false;
                for (var index = 0; index < read; index++)
                {
                    if (firstBuffer[index] != secondBuffer[index]) return false;
                }
            }
            return secondStream.ReadByte() < 0;
        }

        private static bool StoredSettingsRequireUpgrade(
            string primaryPath,
            CancellationToken cancellationToken)
        {
            var path = File.Exists(primaryPath) ? primaryPath : primaryPath + ".bak";
            if (!File.Exists(path)) return false;
            try
            {
                var root = AgentJson.ParseObject(ReadStoredText(path, cancellationToken));
                if (AgentJson.GetSchemaVersion(root) < AgentSettingsDocument.CurrentSchemaVersion ||
                    !root.ContainsKey("agentsRoots") ||
                    !root.ContainsKey("skillRoots") ||
                    !root.ContainsKey("editorSystemPrompt") ||
                    !root.ContainsKey("runtimeSystemPrompt")) return true;
                if (AgentPromptDefaults.IsPreviousEditorPrompt(
                        AgentJson.GetString(root, "editorSystemPrompt")) ||
                    AgentPromptDefaults.IsPreviousRuntimePrompt(
                        AgentJson.GetString(root, "runtimeSystemPrompt"))) return true;
                return AgentJson.GetObjectArray(root, "providerProfiles")
                    .Any(profile => !profile.ContainsKey("providerPresetId") ||
                                    string.IsNullOrWhiteSpace(
                                        AgentJson.GetString(profile, "providerPresetId")));
            }
            catch (Exception exception) when (IsRecoverableDocumentError(exception))
            {
                // ReadDocument owns the user-facing error and includes the explicit backup path.
                return false;
            }
        }

        private static bool StoredSessionRequiresUpgrade(
            string primaryPath,
            CancellationToken cancellationToken)
        {
            var path = File.Exists(primaryPath) ? primaryPath : primaryPath + ".bak";
            if (!File.Exists(path)) return false;
            try
            {
                var root = AgentJson.ParseObject(ReadStoredText(path, cancellationToken));
                return AgentJson.GetSchemaVersion(root) <
                       AgentSessionDocument.CurrentSchemaVersion;
            }
            catch (Exception exception) when (IsRecoverableDocumentError(exception))
            {
                // ReadDocument owns the user-facing error and includes the explicit backup path.
                return false;
            }
        }
    }
}
