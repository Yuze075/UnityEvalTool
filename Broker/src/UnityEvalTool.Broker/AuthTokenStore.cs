using System.Security.Cryptography;
using System.Text.Json;

namespace YuzeToolkit.UnityEvalTool.Broker;

internal sealed class AuthTokenStore
{
    private readonly string _filePath;
    private readonly object _syncRoot = new();
    private string? _token;

    public AuthTokenStore()
    {
        var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userRoot))
            throw new InvalidOperationException("The current user profile directory is unavailable.");
        _filePath = Path.Combine(userRoot, ".unityevaltool", "auth.json");
    }

    internal AuthTokenStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Auth token path is required.", nameof(filePath));
        _filePath = Path.GetFullPath(filePath);
    }

    public string GetOrCreateToken()
    {
        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(_token)) return _token;
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            TryRestrictDirectoryPermissions(directory);
            if (File.Exists(_filePath))
            {
                TryRestrictPermissions(_filePath);
                return _token = ReadToken(_filePath);
            }

            using var publicationMutex = CreatePublicationMutex(_filePath);
            var ownsPublicationMutex = false;
            try
            {
                try
                {
                    publicationMutex.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    // The previous publisher terminated; ownership is still granted to this process.
                }
                ownsPublicationMutex = true;
                if (File.Exists(_filePath))
                {
                    TryRestrictPermissions(_filePath);
                    return _token = ReadToken(_filePath);
                }

                var candidate = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                var temporaryPath = Path.Combine(directory, ".auth." + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.WriteAllText(temporaryPath, "{\"token\":\"" + candidate + "\"}\n");
                    TryRestrictPermissions(temporaryPath);
                    File.Move(temporaryPath, _filePath, overwrite: false);
                    return _token = candidate;
                }
                finally
                {
                    try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                    catch (IOException) { }
                }
            }
            finally
            {
                if (ownsPublicationMutex) publicationMutex.ReleaseMutex();
            }
        }
    }

    public string? TryReadExistingToken()
    {
        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(_token)) return _token;
            if (!File.Exists(_filePath)) return null;
            TryRestrictPermissions(_filePath);
            return _token = ReadToken(_filePath);
        }
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var expected = GetOrCreateToken();
        var left = System.Text.Encoding.UTF8.GetBytes(token);
        var right = System.Text.Encoding.UTF8.GetBytes(expected);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public string FilePath => _filePath;

    private static Mutex CreatePublicationMutex(string path)
    {
        var identity = OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
        return new Mutex(false, "UnityEvalTool.AuthToken." + hash);
    }

    private static string ReadToken(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("token", out var tokenElement))
            {
                var existing = tokenElement.GetString();
                if (!string.IsNullOrWhiteSpace(existing)) return existing;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"UnityEvalTool auth token file '{path}' is invalid JSON.", ex);
        }

        throw new InvalidDataException($"UnityEvalTool auth token file '{path}' does not contain a non-empty token.");
    }

    private static void TryRestrictPermissions(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void TryRestrictDirectoryPermissions(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
