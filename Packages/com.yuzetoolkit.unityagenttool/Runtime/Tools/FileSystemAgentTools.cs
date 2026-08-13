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
    internal static class AgentPath
    {
        public static string Resolve(AgentToolContext context, string value)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Path is required.", nameof(value));
            var combined = Path.IsPathRooted(value)
                ? value
                : Path.Combine(string.IsNullOrWhiteSpace(context.WorkingDirectory)
                    ? AgentPaths.ProjectRoot
                    : context.WorkingDirectory, value);
            return Path.GetFullPath(combined);
        }

        public static bool IsSame(string first, string second) =>
            string.Equals(NormalizeForComparison(first), NormalizeForComparison(second), PathComparison);

        public static bool IsDescendant(string candidate, string parent)
        {
            var normalizedCandidate = NormalizeForComparison(candidate);
            var normalizedParent = NormalizeForComparison(parent);
            var prefix = EndsInDirectorySeparator(normalizedParent)
                ? normalizedParent
                : normalizedParent + Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(prefix, PathComparison);
        }

        public static bool IsReparsePoint(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        public static Dictionary<string, object?> Describe(string path)
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                return AgentJson.Object(
                    ("path", info.FullName),
                    ("kind", "file"),
                    ("length", info.Length),
                    ("createdAtUtc", AgentJson.Utc(info.CreationTimeUtc)),
                    ("modifiedAtUtc", AgentJson.Utc(info.LastWriteTimeUtc)),
                    ("attributes", info.Attributes.ToString()));
            }
            if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);
                return AgentJson.Object(
                    ("path", info.FullName),
                    ("kind", "directory"),
                    ("createdAtUtc", AgentJson.Utc(info.CreationTimeUtc)),
                    ("modifiedAtUtc", AgentJson.Utc(info.LastWriteTimeUtc)),
                    ("attributes", info.Attributes.ToString()));
            }
            return AgentJson.Object(("path", path), ("kind", "missing"));
        }

        private static string NormalizeForComparison(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var rootLength = (Path.GetPathRoot(fullPath) ?? string.Empty).Length;
            var length = fullPath.Length;
            while (length > rootLength && IsDirectorySeparator(fullPath[length - 1])) length--;
            return length == fullPath.Length ? fullPath : fullPath.Substring(0, length);
        }

        private static bool EndsInDirectorySeparator(string path) =>
            path.Length > 0 && IsDirectorySeparator(path[path.Length - 1]);

        private static bool IsDirectorySeparator(char value) =>
            value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;

        private static StringComparison PathComparison =>
            Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    internal abstract class FileSystemAgentToolBase : IAgentTool
    {
        protected FileSystemAgentToolBase(AgentToolDescriptor descriptor)
        {
            Descriptor = descriptor;
        }

        public AgentToolDescriptor Descriptor { get; }

        public abstract Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken);

        protected static Task<AgentToolResult> Run(
            Func<CancellationToken, AgentToolResult> action,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return action(cancellationToken);
            }, cancellationToken);
        }

        protected static Dictionary<string, object?> PathProperties()
        {
            return AgentJson.Object(("path", AgentToolArguments.StringProperty(
                "Absolute path or a path relative to the conversation working directory.")));
        }
    }

    internal sealed class ReadFileAgentTool : FileSystemAgentToolBase
    {
        private const int DefaultMaxCharacters = 200_000;
        private const int MaximumMaxCharacters = 1_000_000;

        public ReadFileAgentTool() : base(new AgentToolDescriptor(
            "file_read_text",
            "Read a UTF-8 or BOM-identified text file without loading the complete file into memory.",
            AgentToolAccess.ReadOnly,
            AgentToolArguments.ObjectSchema(AgentJson.Object(
                    ("path", AgentToolArguments.StringProperty("File path.")),
                    ("offset", AgentToolArguments.IntegerProperty("Character offset to start reading from.")),
                    ("maxCharacters", AgentToolArguments.IntegerProperty("Maximum characters returned (up to 1,000,000)."))),
                "path")))
        {
        }

        public override Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var path = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "path"));
            var offset = Math.Max(0, AgentToolArguments.OptionalInt(arguments, "offset", 0));
            var maxCharacters = Math.Min(MaximumMaxCharacters,
                Math.Max(1, AgentToolArguments.OptionalInt(arguments, "maxCharacters", DefaultMaxCharacters)));
            return Run(token =>
            {
                if (!File.Exists(path)) return AgentToolResult.Error($"File does not exist: {path}");
                var content = new StringBuilder(Math.Min(maxCharacters, 16_384));
                var buffer = new char[8_192];
                long scannedCharacters = 0;
                var truncated = false;
                using (var reader = new StreamReader(path, Encoding.UTF8, true))
                {
                    int read;
                    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        var chunkStart = scannedCharacters;
                        scannedCharacters += read;
                        if (content.Length >= maxCharacters || scannedCharacters <= offset) continue;
                        var start = (int)Math.Max(0L, (long)offset - chunkStart);
                        var count = Math.Min(read - start, maxCharacters - content.Length);
                        if (count > 0) content.Append(buffer, start, count);
                        if (content.Length < maxCharacters) continue;
                        truncated = scannedCharacters > (long)offset + content.Length || reader.Peek() >= 0;
                        if (truncated) break;
                    }
                }

                var actualOffset = Math.Min((long)offset, scannedCharacters);
                return AgentToolResult.Success(AgentJson.Stringify(AgentJson.Object(
                    ("path", path),
                    ("offset", actualOffset),
                    ("characters", content.Length),
                    ("scannedCharacters", scannedCharacters),
                    ("totalCharacters", truncated ? null : (object?)scannedCharacters),
                    ("truncated", truncated),
                    ("content", content.ToString()))));
            }, cancellationToken);
        }
    }

    internal sealed class ListDirectoryAgentTool : FileSystemAgentToolBase
    {
        private const int MaximumEntries = 10_000;

        public ListDirectoryAgentTool() : base(new AgentToolDescriptor(
            "directory_list",
            "List files and folders in a directory. Recursive listing does not traverse symbolic-link directories.",
            AgentToolAccess.ReadOnly,
            AgentToolArguments.ObjectSchema(AgentJson.Object(
                    ("path", AgentToolArguments.StringProperty("Directory path.")),
                    ("recursive", AgentToolArguments.BooleanProperty("Recursively enumerate descendants.")),
                    ("maxEntries", AgentToolArguments.IntegerProperty("Maximum entries returned (up to 10,000)."))),
                "path")))
        {
        }

        public override Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var path = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "path"));
            var recursive = AgentToolArguments.OptionalBool(arguments, "recursive");
            var maxEntries = Math.Min(MaximumEntries,
                Math.Max(1, AgentToolArguments.OptionalInt(arguments, "maxEntries", 1000)));
            return Run(token =>
            {
                if (!Directory.Exists(path)) return AgentToolResult.Error($"Directory does not exist: {path}");
                var entries = EnumerateEntries(path, recursive, maxEntries + 1, token);
                var truncated = entries.Count > maxEntries;
                if (truncated) entries.RemoveAt(entries.Count - 1);
                return AgentToolResult.Success(AgentJson.Stringify(AgentJson.Object(
                    ("path", path),
                    ("recursive", recursive),
                    ("truncated", truncated),
                    ("entries", entries.Select(entry => (object?)AgentPath.Describe(entry)).ToList()))));
            }, cancellationToken);
        }

        private static List<string> EnumerateEntries(
            string root,
            bool recursive,
            int limit,
            CancellationToken cancellationToken)
        {
            var result = new List<string>(Math.Min(limit, 1024));
            var pendingDirectories = new Queue<string>();
            pendingDirectories.Enqueue(root);
            while (pendingDirectories.Count > 0 && result.Count < limit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pendingDirectories.Dequeue();
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Add(entry);
                    if (result.Count >= limit) break;
                    if (recursive && Directory.Exists(entry) && !AgentPath.IsReparsePoint(entry))
                        pendingDirectories.Enqueue(entry);
                }
                if (!recursive) break;
            }
            return result;
        }
    }

    internal sealed class FileInfoAgentTool : FileSystemAgentToolBase
    {
        public FileInfoAgentTool() : base(new AgentToolDescriptor(
            "path_info",
            "Get file or directory metadata without modifying it.",
            AgentToolAccess.ReadOnly,
            AgentToolArguments.ObjectSchema(PathProperties(), "path")))
        {
        }

        public override Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var path = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "path"));
            return Run(_ => AgentToolResult.Success(AgentJson.Stringify(AgentPath.Describe(path))), cancellationToken);
        }
    }

    internal sealed class WriteFileAgentTool : FileSystemAgentToolBase
    {
        public WriteFileAgentTool() : base(new AgentToolDescriptor(
            "file_write_text",
            "Create, overwrite or append a UTF-8 text file. Parent directories can be created automatically.",
            AgentToolAccess.Write,
            AgentToolArguments.ObjectSchema(AgentJson.Object(
                    ("path", AgentToolArguments.StringProperty("File path.")),
                    ("content", AgentToolArguments.StringProperty("Complete text content to write.")),
                    ("append", AgentToolArguments.BooleanProperty("Append instead of overwriting.")),
                    ("createParent", AgentToolArguments.BooleanProperty("Create missing parent directories."))),
                "path", "content")))
        {
        }

        public override Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var path = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "path"));
            var content = AgentToolArguments.RequiredText(arguments, "content");
            var append = AgentToolArguments.OptionalBool(arguments, "append");
            var createParent = AgentToolArguments.OptionalBool(arguments, "createParent", true);
            return Run(token =>
            {
                var parent = Path.GetDirectoryName(path);
                if (createParent && !string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                token.ThrowIfCancellationRequested();
                var encoding = new UTF8Encoding(false);
                if (append) File.AppendAllText(path, content, encoding); else File.WriteAllText(path, content, encoding);
                return AgentToolResult.Success(AgentJson.Stringify(AgentPath.Describe(path)));
            }, cancellationToken);
        }
    }

    internal sealed class CreateDirectoryAgentTool : FileSystemAgentToolBase
    {
        public CreateDirectoryAgentTool() : base(new AgentToolDescriptor(
            "directory_create",
            "Create a directory, including missing parents.",
            AgentToolAccess.Write,
            AgentToolArguments.ObjectSchema(PathProperties(), "path")))
        {
        }

        public override Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var path = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "path"));
            return Run(_ =>
            {
                Directory.CreateDirectory(path);
                return AgentToolResult.Success(AgentJson.Stringify(AgentPath.Describe(path)));
            }, cancellationToken);
        }
    }

    internal sealed class DeletePathAgentTool : FileSystemAgentToolBase
    {
        public DeletePathAgentTool() : base(new AgentToolDescriptor(
            "path_delete",
            "Delete a file or directory. Directory deletion can be recursive.",
            AgentToolAccess.Write,
            AgentToolArguments.ObjectSchema(AgentJson.Object(
                    ("path", AgentToolArguments.StringProperty("File or directory path.")),
                    ("recursive", AgentToolArguments.BooleanProperty("Delete a non-empty directory recursively."))),
                "path")))
        {
        }

        public override Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var path = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "path"));
            var recursive = AgentToolArguments.OptionalBool(arguments, "recursive");
            return Run(token =>
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive && !AgentPath.IsReparsePoint(path));
                }
                else
                {
                    return AgentToolResult.Error($"Path does not exist: {path}");
                }
                return AgentToolResult.Success($"Deleted: {path}");
            }, cancellationToken);
        }
    }

    internal sealed class CopyPathAgentTool : FileSystemAgentToolBase
    {
        public CopyPathAgentTool() : base(new AgentToolDescriptor(
            "path_copy",
            "Copy a file or directory to another non-overlapping path.",
            AgentToolAccess.Write,
            AgentToolArguments.ObjectSchema(AgentJson.Object(
                    ("source", AgentToolArguments.StringProperty("Source file or directory.")),
                    ("destination", AgentToolArguments.StringProperty("Destination path.")),
                    ("overwrite", AgentToolArguments.BooleanProperty("Replace existing destination files."))),
                "source", "destination")))
        {
        }

        public override Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var source = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "source"));
            var destination = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "destination"));
            var overwrite = AgentToolArguments.OptionalBool(arguments, "overwrite");
            return Run(token =>
            {
                var sourceIsFile = File.Exists(source);
                var sourceIsDirectory = Directory.Exists(source);
                if (!sourceIsFile && !sourceIsDirectory)
                    return AgentToolResult.Error($"Source does not exist: {source}");
                if (AgentPath.IsSame(source, destination))
                    return AgentToolResult.Error("Source and destination resolve to the same path.");

                if (sourceIsFile)
                {
                    var parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                    token.ThrowIfCancellationRequested();
                    File.Copy(source, destination, overwrite);
                }
                else
                {
                    if (AgentPath.IsDescendant(destination, source) || AgentPath.IsDescendant(source, destination))
                        return AgentToolResult.Error("Source and destination directories must not overlap.");
                    if (AgentPath.IsReparsePoint(source))
                        return AgentToolResult.Error("Copying a symbolic-link directory is not supported.");
                    CopyDirectory(source, destination, overwrite, token);
                }
                return AgentToolResult.Success(AgentJson.Stringify(AgentPath.Describe(destination)));
            }, cancellationToken);
        }

        private static void CopyDirectory(
            string source,
            string destination,
            bool overwrite,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite);
            }
            foreach (var directory in Directory.EnumerateDirectories(source))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (AgentPath.IsReparsePoint(directory))
                    throw new InvalidOperationException($"Directory copy does not traverse symbolic links: {directory}");
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), overwrite,
                    cancellationToken);
            }
        }
    }

    internal sealed class MovePathAgentTool : FileSystemAgentToolBase
    {
        public MovePathAgentTool() : base(new AgentToolDescriptor(
            "path_move",
            "Move or rename a file or directory.",
            AgentToolAccess.Write,
            AgentToolArguments.ObjectSchema(AgentJson.Object(
                    ("source", AgentToolArguments.StringProperty("Source file or directory.")),
                    ("destination", AgentToolArguments.StringProperty("Destination path.")),
                    ("overwrite", AgentToolArguments.BooleanProperty("Replace an existing destination."))),
                "source", "destination")))
        {
        }

        public override Task<AgentToolResult> ExecuteAsync(
            AgentToolContext context,
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken)
        {
            var source = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "source"));
            var destination = AgentPath.Resolve(context, AgentToolArguments.RequiredString(arguments, "destination"));
            var overwrite = AgentToolArguments.OptionalBool(arguments, "overwrite");
            return Run(token =>
            {
                var sourceIsFile = File.Exists(source);
                var sourceIsDirectory = Directory.Exists(source);
                if (!sourceIsFile && !sourceIsDirectory)
                    return AgentToolResult.Error($"Source does not exist: {source}");
                if (AgentPath.IsSame(source, destination))
                    return AgentToolResult.Success(AgentJson.Stringify(AgentPath.Describe(source)));
                if (sourceIsDirectory &&
                    (AgentPath.IsDescendant(destination, source) || AgentPath.IsDescendant(source, destination)))
                    return AgentToolResult.Error("Source and destination directories must not overlap.");

                var destinationIsFile = File.Exists(destination);
                var destinationIsDirectory = Directory.Exists(destination);
                if ((destinationIsFile || destinationIsDirectory) && !overwrite)
                    return AgentToolResult.Error($"Destination already exists: {destination}");

                var parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                token.ThrowIfCancellationRequested();

                string? displacedDestination = null;
                if (destinationIsFile || destinationIsDirectory)
                {
                    displacedDestination = destination + ".unityagent-" + Guid.NewGuid().ToString("N") + ".backup";
                    if (destinationIsFile) File.Move(destination, displacedDestination);
                    else Directory.Move(destination, displacedDestination);
                }

                try
                {
                    if (sourceIsFile) File.Move(source, destination);
                    else Directory.Move(source, destination);
                }
                catch (Exception moveError) when (moveError is IOException ||
                                                  moveError is UnauthorizedAccessException ||
                                                  moveError is InvalidOperationException)
                {
                    if (displacedDestination == null) throw;
                    try
                    {
                        if (File.Exists(displacedDestination)) File.Move(displacedDestination, destination);
                        else if (Directory.Exists(displacedDestination)) Directory.Move(displacedDestination, destination);
                    }
                    catch (Exception restoreError) when (restoreError is IOException ||
                                                         restoreError is UnauthorizedAccessException ||
                                                         restoreError is InvalidOperationException)
                    {
                        throw new AggregateException(
                            $"Move failed and the previous destination could not be restored from '{displacedDestination}'.",
                            moveError, restoreError);
                    }
                    throw;
                }

                if (displacedDestination != null)
                {
                    if (File.Exists(displacedDestination)) File.Delete(displacedDestination);
                    else if (Directory.Exists(displacedDestination))
                        Directory.Delete(displacedDestination, !AgentPath.IsReparsePoint(displacedDestination));
                }
                return AgentToolResult.Success(AgentJson.Stringify(AgentPath.Describe(destination)));
            }, cancellationToken);
        }
    }
}
