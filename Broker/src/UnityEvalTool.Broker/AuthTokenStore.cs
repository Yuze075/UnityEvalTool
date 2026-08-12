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

    public string GetOrCreateToken()
    {
        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(_token)) return _token;
            if (File.Exists(_filePath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(_filePath));
                if (document.RootElement.TryGetProperty("token", out var tokenElement))
                {
                    var existing = tokenElement.GetString();
                    if (!string.IsNullOrWhiteSpace(existing)) return _token = existing;
                }
            }

            _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(_filePath, "{\"token\":\"" + _token + "\"}\n");
            TryRestrictPermissions(_filePath);
            return _token;
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

    private static void TryRestrictPermissions(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
