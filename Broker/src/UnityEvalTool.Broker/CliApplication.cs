using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace YuzeToolkit.UnityEvalTool.Broker;

internal static class CliApplication
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        InstallMetadataStore.RegisterCurrentExecutable();
        var command = args.Length == 0 ? string.Empty : args[0].ToLowerInvariant();
        if (command is "-h" or "--help" or "help")
        {
            Console.WriteLine(HelpText);
            return 0;
        }
        if (command == "service")
        {
            Console.WriteLine(await UserServiceManager.ExecuteAsync(args.Length > 1 ? args[1] : "status",
                cancellationToken));
            return 0;
        }
        if (command == "doctor") return await DoctorAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        if (command is "list" or "status")
        {
            PrintRegistry(await connection.ListAsync(cancellationToken));
            return 0;
        }

        string selector;
        IReadOnlyList<string> unityCommand;
        var enterConsole = args.Length == 0;
        if (command == "connect")
        {
            if (args.Length < 2) throw new InvalidOperationException("unity connect requires an instance id, prefix, or project path.");
            selector = args[1];
            var separator = Array.IndexOf(args, "--", 2);
            unityCommand = separator >= 0 ? args.Skip(separator + 1).ToArray() : Array.Empty<string>();
            enterConsole = unityCommand.Count == 0;
        }
        else
        {
            selector = string.Empty;
            unityCommand = args;
        }

        var registry = await connection.ListAsync(cancellationToken);
        var instance = ResolveInstance(registry, selector);
        var revision = registry.GetProperty("registryRevision").GetInt64();
        await connection.ConnectUnityAsync(instance.GetProperty("instanceId").GetString()!, revision, cancellationToken);
        if (!enterConsole)
        {
            await WaitUntilExecutableAsync(connection, cancellationToken);
            PrintCliResult(await connection.ExecuteAsync(RebuildCommandLine(unityCommand), cancellationToken));
            return 0;
        }

        await RunConsoleAsync(connection, instance, cancellationToken);
        return 0;
    }

    private static async Task<BrokerCliConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var token = new AuthTokenStore().GetOrCreateToken();
        Exception? lastError = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var connection = new BrokerCliConnection();
            try
            {
                await connection.ConnectAsync(token, cancellationToken);
                return connection;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await connection.DisposeAsync();
                if (attempt == 0) BrokerProcessUtility.StartDetachedBroker();
                await Task.Delay(100, cancellationToken);
            }
        }
        throw new BrokerOperationException(BrokerErrorCodes.BrokerUnavailable,
            $"Unable to connect to the local Broker on port {BrokerConstants.Port}: {lastError?.Message}");
    }

    private static async Task RunConsoleAsync(BrokerCliConnection connection, JsonElement initialInstance,
        CancellationToken cancellationToken)
    {
        var projectName = initialInstance.GetProperty("projectName").GetString() ?? "Unity";
        long lastVmGeneration = -1;
        Console.WriteLine($"Connected to {projectName}. Broker commands: :status, :wait, :switch, :help, :quit");
        while (!cancellationToken.IsCancellationRequested)
        {
            var status = await connection.StatusAsync("snapshot", 0, cancellationToken);
            var selected = status.GetProperty("selectedUnity");
            var phase = selected.ValueKind == JsonValueKind.Null
                ? "Disconnected"
                : selected.GetProperty("status").GetProperty("phase").GetString() ?? "Unknown";
            if (selected.ValueKind != JsonValueKind.Null)
            {
                var generation = selected.GetProperty("status").GetProperty("vmGeneration").GetInt64();
                if (lastVmGeneration >= 0 && generation != lastVmGeneration)
                    Console.WriteLine($"[Unity reconnected; PuerTS VM generation changed {lastVmGeneration} -> {generation}]");
                lastVmGeneration = generation;
                projectName = selected.GetProperty("projectName").GetString() ?? projectName;
            }
            Console.Write($"unity[{projectName}|{phase}]> ");
            var line = Console.ReadLine();
            if (line == null || line is ":quit" or ":exit" or ":disconnect") return;
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                switch (line.Trim())
                {
                    case ":help":
                        Console.WriteLine("Broker commands: :status, :wait, :switch, :help, :quit. All other input is parsed by Unity's existing CLI command service.");
                        continue;
                    case ":status":
                        PrintRegistry(status);
                        continue;
                    case ":wait":
                        PrintRegistry(await connection.StatusAsync("ready", 600, cancellationToken));
                        continue;
                    case ":switch":
                        var registry = await connection.ListAsync(cancellationToken);
                        PrintRegistry(registry);
                        Console.Write("instance> ");
                        var selector = Console.ReadLine() ?? string.Empty;
                        var target = ResolveInstance(registry, selector);
                        await connection.ConnectUnityAsync(target.GetProperty("instanceId").GetString()!,
                            registry.GetProperty("registryRevision").GetInt64(), cancellationToken);
                        projectName = target.GetProperty("projectName").GetString() ?? projectName;
                        lastVmGeneration = -1;
                        continue;
                }

                await WaitUntilExecutableAsync(connection, cancellationToken);
                PrintCliResult(await connection.ExecuteAsync(line, cancellationToken));
            }
            catch (BrokerOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }
    }

    private static async Task WaitUntilExecutableAsync(BrokerCliConnection connection,
        CancellationToken cancellationToken)
    {
        var snapshot = await connection.StatusAsync("snapshot", 0, cancellationToken);
        var selected = snapshot.GetProperty("selectedUnity");
        if (selected.ValueKind == JsonValueKind.Null)
            throw new BrokerOperationException(BrokerErrorCodes.UnityDisconnected, "Selected Unity is unavailable.");
        var status = selected.GetProperty("status");
        if (status.GetProperty("canEval").GetBoolean()) return;
        var phase = status.GetProperty("phase").GetString() ?? "Unknown";
        if (string.Equals(phase, "CompilationFailed", StringComparison.Ordinal))
            throw new BrokerOperationException(BrokerErrorCodes.CompilationFailed,
                $"Unity compilation failed with {status.GetProperty("compilerErrorCount").GetInt32()} error(s).");
        Console.WriteLine($"[Waiting for Unity: {phase}]");
        await connection.StatusAsync("ready", 600, cancellationToken);
    }

    private static JsonElement ResolveInstance(JsonElement registry, string selector)
    {
        var instances = registry.GetProperty("unityInstances").EnumerateArray()
            .Where(item => item.GetProperty("isConnected").GetBoolean()).Select(item => item.Clone()).ToList();
        if (instances.Count == 0)
            throw new BrokerOperationException(BrokerErrorCodes.UnityNotFound, "No connected Unity instances were found.");
        if (!string.IsNullOrWhiteSpace(selector))
        {
            var normalizedSelector = TryNormalizePath(selector);
            var matches = instances.Where(instance =>
                string.Equals(instance.GetProperty("instanceId").GetString(), selector, StringComparison.Ordinal) ||
                (instance.GetProperty("instanceId").GetString()?.StartsWith(selector, StringComparison.Ordinal) ?? false) ||
                string.Equals(TryNormalizePath(instance.GetProperty("projectPath").GetString() ?? string.Empty),
                    normalizedSelector, PathComparison)).ToList();
            return matches.Count switch
            {
                1 => matches[0],
                0 => throw new BrokerOperationException(BrokerErrorCodes.UnityNotFound,
                    $"No connected Unity matched '{selector}'."),
                _ => throw new BrokerOperationException(BrokerErrorCodes.InvalidRequest,
                    $"Selector '{selector}' matched multiple Unity instances; use the full instanceId.")
            };
        }

        var current = TryNormalizePath(Directory.GetCurrentDirectory());
        var pathMatches = instances.Where(instance => IsUnderPath(current,
                TryNormalizePath(instance.GetProperty("projectPath").GetString() ?? string.Empty)))
            .OrderByDescending(instance => instance.GetProperty("projectPath").GetString()?.Length ?? 0).ToList();
        if (pathMatches.Count > 0) return pathMatches[0];
        if (instances.Count == 1) return instances[0];
        throw new BrokerOperationException(BrokerErrorCodes.DiscoveryRequired,
            "Multiple Unity instances are connected and the current directory does not identify one. Use `unity list` then `unity connect <instanceId>`. ");
    }

    private static void PrintRegistry(JsonElement registry)
    {
        Console.WriteLine($"Registry revision: {registry.GetProperty("registryRevision").GetInt64()}");
        foreach (var instance in registry.GetProperty("unityInstances").EnumerateArray())
        {
            var status = instance.GetProperty("status");
            Console.WriteLine($"{instance.GetProperty("instanceId").GetString()}  " +
                              $"{instance.GetProperty("projectName").GetString()}  " +
                              $"{status.GetProperty("phase").GetString()}  " +
                              $"PID {instance.GetProperty("processId").GetInt32()}  " +
                              instance.GetProperty("projectPath").GetString());
        }
        if (registry.TryGetProperty("selectedUnity", out var selected) && selected.ValueKind != JsonValueKind.Null)
            Console.WriteLine("Selected: " + selected.GetProperty("instanceId").GetString());
    }

    private static void PrintCliResult(JsonElement result)
    {
        if (result.TryGetProperty("text", out var text) && !string.IsNullOrEmpty(text.GetString()))
            Console.WriteLine(text.GetString());
        else
            Console.WriteLine(result.GetRawText());
        if (result.TryGetProperty("success", out var success) && !success.GetBoolean())
            throw new InvalidOperationException(text.GetString() ?? "Unity CLI command failed.");
    }

    private static async Task<int> DoctorAsync(CancellationToken cancellationToken)
    {
        InstallMetadataStore.RegisterCurrentExecutable();
        Console.WriteLine("Executable: " + (InstallMetadataStore.GetCurrentExecutable() ?? "unpublished/dotnet host"));
        Console.WriteLine("Auth file: " + new AuthTokenStore().FilePath);
        Console.WriteLine("Broker endpoint: http://127.0.0.1:2347");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var health = await client.GetStringAsync($"http://{BrokerConstants.Host}:{BrokerConstants.Port}/health",
                cancellationToken);
            Console.WriteLine("Broker: " + health);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BrokerUnavailable: " + ex.Message);
            return 3;
        }
    }

    private static string RebuildCommandLine(IReadOnlyList<string> args) => string.Join(" ", args.Select(argument =>
        string.IsNullOrEmpty(argument) || argument.Any(char.IsWhiteSpace) || argument.Contains('"') || argument.Contains('\'')
            ? "\"" + argument.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : argument));

    private static string TryNormalizePath(string value)
    {
        try { return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return value; }
    }

    private static bool IsUnderPath(string path, string parent) =>
        string.Equals(path, parent, PathComparison) ||
        path.StartsWith(parent + Path.DirectorySeparatorChar, PathComparison) ||
        path.StartsWith(parent + Path.AltDirectorySeparatorChar, PathComparison);

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private const string HelpText = """
UnityEvalTool Broker CLI

  unity                         Auto-select Unity for the current directory and enter its console.
  unity list                    List registered Unity instances and states.
  unity status                  Alias for list.
  unity connect <instance>      Select by id/prefix/project path and enter a console.
  unity connect <instance> -- <command...>
                                Execute one Unity-side CLI command.
  unity <command...>            Auto-select by current directory and execute once.
  unity doctor                  Diagnose executable, auth file, port and Broker health.
  unity service <action>        install|uninstall|start|stop|restart|status.
  unity broker                  Run the foreground Broker host on 127.0.0.1:2347.

Inside a console, :status, :wait, :switch, :help and :quit are Broker commands.
Every other line is parsed inside Unity by the existing EvalCliCommandService.
""";
}
