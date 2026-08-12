#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace YuzeToolkit
{
    public static class UnityEvalToolZipArchiveValidator
    {
        private static readonly string[] ForbiddenBuildFolders = { "bin", "obj" };

        public static bool TryValidateSourceArchive(string archivePath, out string error)
        {
            error = string.Empty;
            try
            {
                using var stream = File.OpenRead(archivePath);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    if (!TryValidateEntry(entry.FullName, out error))
                        return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Failed to inspect source archive '{archivePath}': {ex.Message}";
                return false;
            }

            return true;
        }

        private static bool TryValidateEntry(string entryName, out string error)
        {
            error = string.Empty;
            var normalized = entryName.Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(normalized)) return true;

            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.Contains(":/", StringComparison.Ordinal) ||
                normalized.Contains(":\\", StringComparison.Ordinal))
            {
                error = $"Source archive contains an absolute path entry: {entryName}";
                return false;
            }

            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment == ".."))
            {
                error = $"Source archive contains a parent-directory entry: {entryName}";
                return false;
            }

            if (segments.Any(segment => ForbiddenBuildFolders.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            {
                error = $"Source archive must not contain build output folders such as bin or obj: {entryName}";
                return false;
            }

            return true;
        }
    }
}
