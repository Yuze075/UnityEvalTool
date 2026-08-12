#nullable enable
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YuzeToolkit
{
    public static class EditorProcessRunner
    {
        public static async Task<EditorProcessResult> RunAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            var exit = new TaskCompletionSource<int>();
            process.Exited += (_, _) => exit.TrySetResult(process.ExitCode);

            if (!process.Start())
                return new EditorProcessResult(-1, $"Failed to start process: {fileName}");

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch (Exception)
                {
                    // Process may have already exited.
                }

                exit.TrySetCanceled(cancellationToken);
            });

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            var exitCode = await exit.Task;
            var mergedOutput = new StringBuilder();
            mergedOutput.Append(await output);
            mergedOutput.Append(await error);
            return new EditorProcessResult(exitCode, mergedOutput.ToString());
        }
    }

    public readonly struct EditorProcessResult
    {
        public EditorProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
        }

        public int ExitCode { get; }

        public string Output { get; }
    }
}
