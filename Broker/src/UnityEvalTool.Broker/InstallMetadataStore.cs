using System.Text.Json.Nodes;

namespace YuzeToolkit.UnityEvalTool.Broker;

internal static class InstallMetadataStore
{
    public static string ConfigDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                throw new InvalidOperationException("The current user profile directory is unavailable.");
            return Path.Combine(home, ".unityevaltool");
        }
    }

    public static string? GetCurrentExecutable()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath)) return null;
        var fileName = Path.GetFileNameWithoutExtension(processPath);
        if (!string.Equals(fileName, "unity", StringComparison.OrdinalIgnoreCase)) return null;
        return Path.GetFullPath(processPath);
    }

    public static void RegisterCurrentExecutable()
    {
        var executable = GetCurrentExecutable();
        if (executable == null) return;
        Directory.CreateDirectory(ConfigDirectory);
        var path = Path.Combine(ConfigDirectory, "install.json");
        var document = new JsonObject
        {
            ["executablePath"] = executable,
            ["version"] = "2.0.0",
            ["updatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };
        File.WriteAllText(path, document.ToJsonString() + Environment.NewLine);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
