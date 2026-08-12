#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace YuzeToolkit
{
    public static class RoslynSourceGeneratorBuilder
    {
        private const string RoslynArchiveFileName = "UnityEvalToolRoslyn.zip";
        private const string SolutionFileName = "UnityEvalToolRoslyn.sln";
        private const string AnalyzerRelativePath = "Analyzers/UnityEvalTool.SourceGenerator.dll";

        public static RoslynBuildResult EnsureSourceExtracted()
        {
            return EnsureSourceExtractedAsync().GetAwaiter().GetResult();
        }

        public static async Task<RoslynBuildResult> EnsureSourceExtractedAsync()
        {
            try
            {
                var sourceDirectory = GetSourceDirectory();
                var solutionPath = Path.Combine(sourceDirectory, SolutionFileName);
                if (File.Exists(solutionPath))
                    return RoslynBuildResult.Succeeded($"Roslyn source is ready: {sourceDirectory}");

                Directory.CreateDirectory(sourceDirectory);
                var archivePath = Path.Combine(GetPackageRoot(), RoslynArchiveFileName);
                if (!File.Exists(archivePath))
                    return RoslynBuildResult.Failed($"Roslyn source archive was not found: {archivePath}");
                if (!UnityEvalToolZipArchiveValidator.TryValidateSourceArchive(archivePath, out var archiveError))
                    return RoslynBuildResult.Failed(archiveError);

                var extract = await ExtractArchiveAsync(archivePath, sourceDirectory, GetProjectRoot());
                if (extract.ExitCode != 0)
                    return RoslynBuildResult.Failed(
                        $"Failed to extract Roslyn source archive with exit code {extract.ExitCode}.\n{extract.Output}".Trim());
                if (!File.Exists(solutionPath))
                    return RoslynBuildResult.Failed($"Extracted Roslyn solution was not found: {solutionPath}");

                return RoslynBuildResult.Succeeded($"Roslyn source extracted: {sourceDirectory}", extract.Output);
            }
            catch (Exception ex)
            {
                return RoslynBuildResult.Failed(ex.Message);
            }
        }

        public static RoslynBuildResult BuildAnalyzer()
        {
            return BuildAnalyzerAsync().GetAwaiter().GetResult();
        }

        public static async Task<RoslynBuildResult> BuildAnalyzerAsync()
        {
            var ensureSource = await EnsureSourceExtractedAsync();
            if (!ensureSource.Success)
                return ensureSource;

            try
            {
                var projectRoot = GetProjectRoot();
                var packageRoot = GetPackageRoot();
                var sourceDirectory = GetSourceDirectory();
                var solutionPath = Path.Combine(sourceDirectory, SolutionFileName);
                var generatorProjectPath = Path.Combine(sourceDirectory, "src", "UnityEvalTool.SourceGenerator",
                    "UnityEvalTool.SourceGenerator.csproj");
                if (!File.Exists(generatorProjectPath))
                    return RoslynBuildResult.Failed($"Roslyn generator project was not found: {generatorProjectPath}");

                var dotnet = ResolveDotnetExecutable();
                var test = await EditorProcessRunner.RunAsync(dotnet, $"test {Quote(solutionPath)}", projectRoot);
                if (test.ExitCode != 0)
                    return RoslynBuildResult.Failed(
                        $"dotnet test failed with exit code {test.ExitCode}.\n{test.Output}".Trim());

                var build = await EditorProcessRunner.RunAsync(dotnet,
                    $"build {Quote(generatorProjectPath)} -c Release", projectRoot);
                if (build.ExitCode != 0)
                    return RoslynBuildResult.Failed(
                        $"dotnet build failed with exit code {build.ExitCode}.\n{build.Output}".Trim());

                var builtDll = Path.Combine(sourceDirectory, "src", "UnityEvalTool.SourceGenerator", "bin", "Release",
                    "netstandard2.0", "UnityEvalTool.SourceGenerator.dll");
                if (!File.Exists(builtDll))
                    return RoslynBuildResult.Failed($"Built analyzer DLL was not found: {builtDll}");

                var analyzerPath = Path.Combine(packageRoot, AnalyzerRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(analyzerPath)!);
                File.Copy(builtDll, analyzerPath, true);
                AssetDatabase.ImportAsset(GetAnalyzerAssetPath(), ImportAssetOptions.ForceUpdate);

                return RoslynBuildResult.Succeeded($"Roslyn analyzer built and copied: {analyzerPath}",
                    test.Output + build.Output);
            }
            catch (Exception ex)
            {
                return RoslynBuildResult.Failed(ex.Message);
            }
        }

        public static string GetSourceDirectory()
        {
            return Path.Combine(GetProjectRoot(), "Library", "UnityEvalTool", "UnityEvalToolRoslyn", "Source");
        }

        public static string GetAnalyzerAssetPath()
        {
            return UnityEvalToolPackagePathUtility.GetPackagePaths().GetAssetPath(AnalyzerRelativePath);
        }

        private static string GetProjectRoot()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrWhiteSpace(root) ? Application.dataPath : Path.GetFullPath(root);
        }

        private static string GetPackageRoot()
        {
            return UnityEvalToolPackagePathUtility.GetPackagePaths().FullRoot;
        }

        private static Task<EditorProcessResult> ExtractArchiveAsync(
            string archivePath,
            string destinationDirectory,
            string workingDirectory)
        {
            return Application.platform == RuntimePlatform.WindowsEditor
                ? EditorProcessRunner.RunAsync(
                    "powershell",
                    "-NoProfile -ExecutionPolicy Bypass -Command " +
                    Quote($"Expand-Archive -LiteralPath '{EscapePowerShellSingleQuotedString(archivePath)}' -DestinationPath '{EscapePowerShellSingleQuotedString(destinationDirectory)}' -Force"),
                    workingDirectory)
                : EditorProcessRunner.RunAsync("unzip", $"-oq {Quote(archivePath)} -d {Quote(destinationDirectory)}",
                    workingDirectory);
        }

        private static string EscapePowerShellSingleQuotedString(string value) =>
            value.Replace("'", "''");

        private static string ResolveDotnetExecutable()
        {
            var executableName = Application.platform == RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet";
            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var candidates = new[]
            {
                string.IsNullOrWhiteSpace(dotnetRoot) ? string.Empty : Path.Combine(dotnetRoot, executableName),
                string.IsNullOrWhiteSpace(home) ? string.Empty : Path.Combine(home, ".dotnet", executableName),
                string.IsNullOrWhiteSpace(programFiles) ? string.Empty : Path.Combine(programFiles, "dotnet", executableName),
                string.IsNullOrWhiteSpace(programFilesX86) ? string.Empty : Path.Combine(programFilesX86, "dotnet", executableName),
                "/opt/homebrew/bin/dotnet",
                "/usr/local/bin/dotnet",
                "/usr/bin/dotnet",
                executableName
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                if (Path.IsPathRooted(candidate) && File.Exists(candidate))
                    return candidate;
            }

            return executableName;
        }

        private static string Quote(string value) =>
            string.IsNullOrEmpty(value) || value.Contains(' ') ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;

    }

    public sealed class RoslynBuildResult
    {
        private RoslynBuildResult(bool success, string message, string output)
        {
            Success = success;
            Message = message;
            Output = output;
        }

        public bool Success { get; }

        public string Message { get; }

        public string Output { get; }

        public static RoslynBuildResult Succeeded(string message, string output = "") =>
            new(true, message, output);

        public static RoslynBuildResult Failed(string message) =>
            new(false, message, string.Empty);
    }
}
