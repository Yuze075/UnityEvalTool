using System.Security;

namespace YuzeToolkit.UnityEvalTool.Broker;

internal static class UserServiceManager
{
    private const string ServiceId = "com.yuzetoolkit.unityevaltool";

    public static async Task<string> ExecuteAsync(string action, CancellationToken cancellationToken)
    {
        var executable = InstallMetadataStore.GetCurrentExecutable()
                         ?? throw new InvalidOperationException("Service management requires the published `unity` executable.");
        action = string.IsNullOrWhiteSpace(action) ? "status" : action.ToLowerInvariant();
        if (OperatingSystem.IsMacOS()) return await ExecuteMacAsync(action, executable, cancellationToken);
        if (OperatingSystem.IsLinux()) return await ExecuteLinuxAsync(action, executable, cancellationToken);
        if (OperatingSystem.IsWindows()) return await ExecuteWindowsAsync(action, executable, cancellationToken);
        throw new PlatformNotSupportedException("UnityEvalTool user services support macOS, Linux, and Windows.");
    }

    private static async Task<string> ExecuteMacAsync(string action, string executable,
        CancellationToken cancellationToken)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var plist = Path.Combine(home, "Library", "LaunchAgents", ServiceId + ".plist");
        var uid = (await BrokerProcessUtility.RunAsync("id", "-u", cancellationToken: cancellationToken)).Output.Trim();
        var domain = "gui/" + uid;
        var target = domain + "/" + ServiceId;
        switch (action)
        {
            case "install":
                Directory.CreateDirectory(Path.GetDirectoryName(plist)!);
                File.WriteAllText(plist, BuildLaunchAgent(executable));
                await BrokerProcessUtility.RunAsync("launchctl", $"bootout {Quote(domain)} {Quote(plist)}", false,
                    cancellationToken);
                await BrokerProcessUtility.RunAsync("launchctl", $"bootstrap {Quote(domain)} {Quote(plist)}", true,
                    cancellationToken);
                await BrokerProcessUtility.RunAsync("launchctl", $"kickstart -k {Quote(target)}", true,
                    cancellationToken);
                return $"Installed and started LaunchAgent {ServiceId}.";
            case "uninstall":
                await BrokerProcessUtility.RunAsync("launchctl", $"bootout {Quote(domain)} {Quote(plist)}", false,
                    cancellationToken);
                if (File.Exists(plist)) File.Delete(plist);
                return $"Removed LaunchAgent {ServiceId}.";
            case "start":
                var loaded = await BrokerProcessUtility.RunAsync("launchctl", $"print {Quote(target)}", false,
                    cancellationToken);
                if (loaded.ExitCode != 0)
                    await BrokerProcessUtility.RunAsync("launchctl", $"bootstrap {Quote(domain)} {Quote(plist)}", true,
                        cancellationToken);
                await BrokerProcessUtility.RunAsync("launchctl", $"kickstart {Quote(target)}", true,
                    cancellationToken);
                return $"Started LaunchAgent {ServiceId}.";
            case "restart":
                await BrokerProcessUtility.RunAsync("launchctl", $"kickstart -k {Quote(target)}", true,
                    cancellationToken);
                return $"Restarted LaunchAgent {ServiceId}.";
            case "stop":
                await BrokerProcessUtility.RunAsync("launchctl", $"bootout {Quote(domain)} {Quote(plist)}", false,
                    cancellationToken);
                return $"Stopped LaunchAgent {ServiceId}.";
            case "status":
                var status = await BrokerProcessUtility.RunAsync("launchctl", $"print {Quote(target)}", false,
                    cancellationToken);
                return status.ExitCode == 0 ? status.Output : $"LaunchAgent {ServiceId} is not loaded.";
            default:
                throw new InvalidOperationException("service action must be install, uninstall, start, stop, restart, or status.");
        }
    }

    private static async Task<string> ExecuteLinuxAsync(string action, string executable,
        CancellationToken cancellationToken)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var unitDirectory = Path.Combine(home, ".config", "systemd", "user");
        var unitPath = Path.Combine(unitDirectory, "unityevaltool.service");
        switch (action)
        {
            case "install":
                Directory.CreateDirectory(unitDirectory);
                File.WriteAllText(unitPath, BuildSystemdUnit(executable));
                await BrokerProcessUtility.RunAsync("systemctl", "--user daemon-reload", cancellationToken: cancellationToken);
                await BrokerProcessUtility.RunAsync("systemctl", "--user enable --now unityevaltool.service",
                    cancellationToken: cancellationToken);
                return "Installed and started systemd user unit unityevaltool.service.";
            case "uninstall":
                await BrokerProcessUtility.RunAsync("systemctl", "--user disable --now unityevaltool.service", false,
                    cancellationToken);
                if (File.Exists(unitPath)) File.Delete(unitPath);
                await BrokerProcessUtility.RunAsync("systemctl", "--user daemon-reload", false, cancellationToken);
                return "Removed systemd user unit unityevaltool.service.";
            case "start":
            case "restart":
                await BrokerProcessUtility.RunAsync("systemctl", $"--user {action} unityevaltool.service",
                    cancellationToken: cancellationToken);
                return action == "start"
                    ? "Started unityevaltool.service."
                    : "Restarted unityevaltool.service.";
            case "stop":
                await BrokerProcessUtility.RunAsync("systemctl", "--user stop unityevaltool.service",
                    cancellationToken: cancellationToken);
                return "Stopped unityevaltool.service.";
            case "status":
                return (await BrokerProcessUtility.RunAsync("systemctl",
                    "--user status unityevaltool.service --no-pager", false, cancellationToken)).Output;
            default:
                throw new InvalidOperationException("service action must be install, uninstall, start, stop, restart, or status.");
        }
    }

    private static async Task<string> ExecuteWindowsAsync(string action, string executable,
        CancellationToken cancellationToken)
    {
        const string taskName = "UnityEvalTool Broker";
        var taskRun = $"\\\"{executable}\\\" broker";
        switch (action)
        {
            case "install":
                await BrokerProcessUtility.RunAsync("schtasks",
                    $"/Create /F /SC ONLOGON /TN {Quote(taskName)} /TR {Quote(taskRun)}", cancellationToken: cancellationToken);
                await BrokerProcessUtility.RunAsync("schtasks", $"/Run /TN {Quote(taskName)}", cancellationToken: cancellationToken);
                return $"Installed and started current-user task '{taskName}'.";
            case "uninstall":
                await BrokerProcessUtility.RunAsync("schtasks", $"/Delete /F /TN {Quote(taskName)}", false,
                    cancellationToken);
                return $"Removed task '{taskName}'.";
            case "start":
            case "restart":
                if (action == "restart")
                    await BrokerProcessUtility.RunAsync("schtasks", $"/End /TN {Quote(taskName)}", false, cancellationToken);
                await BrokerProcessUtility.RunAsync("schtasks", $"/Run /TN {Quote(taskName)}", cancellationToken: cancellationToken);
                return $"Started task '{taskName}'.";
            case "stop":
                await BrokerProcessUtility.RunAsync("schtasks", $"/End /TN {Quote(taskName)}", false, cancellationToken);
                return $"Stopped task '{taskName}'.";
            case "status":
                return (await BrokerProcessUtility.RunAsync("schtasks", $"/Query /TN {Quote(taskName)} /V /FO LIST",
                    false, cancellationToken)).Output;
            default:
                throw new InvalidOperationException("service action must be install, uninstall, start, stop, restart, or status.");
        }
    }

    private static string BuildLaunchAgent(string executable)
    {
        var escaped = SecurityElement.Escape(executable) ?? executable;
        var logRoot = SecurityElement.Escape(InstallMetadataStore.ConfigDirectory) ?? InstallMetadataStore.ConfigDirectory;
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>Label</key><string>{ServiceId}</string>
<key>ProgramArguments</key><array><string>{escaped}</string><string>broker</string></array>
<key>RunAtLoad</key><true/><key>KeepAlive</key><true/>
<key>StandardOutPath</key><string>{logRoot}/broker.out.log</string>
<key>StandardErrorPath</key><string>{logRoot}/broker.err.log</string>
</dict></plist>
""";
    }

    internal static string BuildSystemdUnit(string executable) => $"""
[Unit]
Description=UnityEvalTool local Broker
After=default.target

[Service]
ExecStart={QuoteSystemdArgument(executable)} broker
Restart=on-failure
RestartSec=1

[Install]
WantedBy=default.target
""";

    private static string QuoteSystemdArgument(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new InvalidOperationException("The systemd executable path is empty or contains control characters.");
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("%", "%%", StringComparison.Ordinal) + "\"";
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
