#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace YuzeToolkit
{
    public sealed class UnityEvalToolPackageLocator : ScriptableObject
    {
    }

    internal readonly struct UnityEvalToolPackagePaths
    {
        public UnityEvalToolPackagePaths(string assetRoot, string fullRoot)
        {
            AssetRoot = NormalizeAssetPath(assetRoot);
            FullRoot = Path.GetFullPath(fullRoot);
        }

        public string AssetRoot { get; }

        public string FullRoot { get; }

        public string GetAssetPath(string relativePath) =>
            NormalizeAssetPath(AssetRoot + "/" + relativePath.TrimStart('/', '\\'));

        public string GetFullPath(string relativePath) =>
            Path.GetFullPath(Path.Combine(FullRoot, relativePath));

        private static string NormalizeAssetPath(string path) =>
            path.Replace('\\', '/').TrimEnd('/');
    }

    internal static class UnityEvalToolPackagePathUtility
    {
        private const string PackageName = "com.yuzetoolkit.unityevaltool";

        public static UnityEvalToolPackagePaths GetPackagePaths()
        {
            var locatorAssetPath = GetLocatorAssetPath();
            if (!string.IsNullOrWhiteSpace(locatorAssetPath))
            {
                var packageInfo = PackageInfo.FindForAssetPath(locatorAssetPath);
                if (TryCreatePaths(packageInfo, locatorAssetPath, out var paths))
                    return paths;
            }

            var assemblyPackageInfo = PackageInfo.FindForAssembly(typeof(UnityEvalToolPackageLocator).Assembly);
            if (TryCreatePaths(assemblyPackageInfo, string.Empty, out var assemblyPaths))
                return assemblyPaths;

            var fallbackAssetRoot = "Packages/" + PackageName;
            return new UnityEvalToolPackagePaths(
                fallbackAssetRoot,
                Path.Combine(GetProjectRoot(), fallbackAssetRoot));
        }

        private static string GetLocatorAssetPath()
        {
            UnityEvalToolPackageLocator? locator = null;
            try
            {
                locator = ScriptableObject.CreateInstance<UnityEvalToolPackageLocator>();
                var script = MonoScript.FromScriptableObject(locator);
                return script != null ? AssetDatabase.GetAssetPath(script) : string.Empty;
            }
            finally
            {
                if (locator != null)
                    UnityEngine.Object.DestroyImmediate(locator);
            }
        }

        private static bool TryCreatePaths(PackageInfo? packageInfo, string assetPathHint,
            out UnityEvalToolPackagePaths paths)
        {
            paths = default;
            var assetRoot = !string.IsNullOrWhiteSpace(packageInfo?.assetPath)
                ? packageInfo!.assetPath
                : FindPackageAssetRoot(assetPathHint);
            var fullRoot = packageInfo == null ? string.Empty : packageInfo.resolvedPath;

            if (string.IsNullOrWhiteSpace(assetRoot) || string.IsNullOrWhiteSpace(fullRoot))
                return false;

            paths = new UnityEvalToolPackagePaths(assetRoot, fullRoot!);
            return true;
        }

        private static string FindPackageAssetRoot(string assetPath)
        {
            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
            while (!string.IsNullOrWhiteSpace(directory))
            {
                if (File.Exists(Path.Combine(GetProjectRoot(), directory, "package.json")))
                    return directory;
                directory = Path.GetDirectoryName(directory)?.Replace('\\', '/') ?? string.Empty;
            }

            return string.Empty;
        }

        private static string GetProjectRoot()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrWhiteSpace(root) ? Application.dataPath : Path.GetFullPath(root);
        }
    }
}