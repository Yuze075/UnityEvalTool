#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YuzeToolkit
{
    public static partial class EvalToolRegistry
    {
        public static Dictionary<string, object?> GetIndex(bool refresh)
        {
            if (refresh) RefreshToolMetadataCaches();
            var tools = ListTools(false);
            return EvalData.Obj(
                ("toolImportPrefix", "tools://"),
                ("tools", tools.Select(ToSummaryJson).Cast<object?>().ToList()),
                ("description", BuildDescription(tools))
            );
        }

        public static Dictionary<string, object?> GetToolDetails(string path, bool refresh)
        {
            if (refresh) RefreshToolMetadataCaches();
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Tool path is required.");
            if (TryResolveCSharp(path, out var csharpTool))
                return ToJson(ToDescriptor(csharpTool));
            if (TryGetJsDescriptor(path, out var jsTool))
                return ToJson(jsTool);
            throw new InvalidOperationException($"Tool '{path}' was not found or is no longer available.");
        }

        public static Dictionary<string, object?> GetCliCatalog(bool refresh)
        {
            if (refresh) RefreshToolMetadataCaches();
            var tools = FlattenTools(ListTools(false));
            return EvalData.Obj(
                ("version", "2.0"),
                ("tools", tools.Select(ToCliJson).Cast<object?>().ToList()),
                ("commands", tools.SelectMany(ToCliCommands).Cast<object?>().ToList())
            );
        }

        public static IReadOnlyList<EvalToolDescriptor> ListTools(bool refresh = false)
        {
            if (refresh) RefreshToolMetadataCaches();
            return ListCSharpRoots()
                .Select(ToDescriptor)
                .Concat(ListJsRoots().Select(ToDescriptor))
                .OrderBy(tool => tool.Path, StringComparer.Ordinal)
                .ToList();
        }

        public static bool TryGetFunctionDescriptor(string toolPath, string functionName, out EvalToolFunctionDescriptor descriptor)
        {
            descriptor = null!;
            if (!TryResolveCSharp(toolPath, out var tool))
                return false;

            descriptor = tool.Functions.FirstOrDefault(function =>
                string.Equals(function.MethodName, functionName, StringComparison.Ordinal))!;
            return descriptor != null;
        }

        private static IReadOnlyList<EvalToolDescriptor> FlattenTools(IReadOnlyList<EvalToolDescriptor> roots)
        {
            var result = new List<EvalToolDescriptor>();
            var queue = new Queue<string>(roots.Select(root => root.Path));
            var seen = new HashSet<string>(StringComparer.Ordinal);

            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                if (!seen.Add(path)) continue;
                if (!TryGetDescriptor(path, out var descriptor)) continue;
                result.Add(descriptor);
                foreach (var subTool in descriptor.SubTools)
                    queue.Enqueue(subTool.Path);
            }

            return result;
        }

        private static bool TryGetDescriptor(string path, out EvalToolDescriptor descriptor)
        {
            if (TryResolveCSharp(path, out var csharpTool))
            {
                descriptor = ToDescriptor(csharpTool);
                return true;
            }

            return TryGetJsDescriptor(path, out descriptor);
        }

        private static EvalToolDescriptor ToDescriptor(ResolvedTool tool)
        {
            return new EvalToolDescriptor(
                tool.Name,
                tool.Path,
                tool.Description,
                IsEditorOnlyAssembly(tool.ToolType),
                IsEnabled(tool.Path),
                "csharp",
                tool.Functions.Select(function => EvalToolSafetyUtility.Apply(tool.Path, function)).ToList(),
                tool.Instance.SubTools.Select(subTool => ToSummaryDescriptor(subTool, tool.Path + "/" + subTool.Name)).ToList()
            );
        }

        private static EvalToolDescriptor ToDescriptor(JsToolRegistration tool) =>
            new(
                tool.Name,
                tool.Name,
                tool.Description,
                false,
                IsEnabled(tool.Name),
                "js",
                EvalToolFunctionDescriptor.Empty);

        private static EvalToolSummaryDescriptor ToSummaryDescriptor(IEvalTool tool, string path)
        {
            return new EvalToolSummaryDescriptor(
                tool.Name,
                path,
                tool.Description,
                IsEditorOnlyAssembly(tool.GetType()),
                IsEnabled(path),
                "csharp",
                tool.Functions.Count);
        }

        private static bool IsEditorOnlyAssembly(Type toolType) =>
            toolType.Assembly.GetName().Name?.IndexOf(".Editor", StringComparison.OrdinalIgnoreCase) >= 0;

        private static Dictionary<string, object?> ToJson(EvalToolDescriptor descriptor)
        {
            var result = EvalData.Obj(
                ("name", descriptor.Name),
                ("path", descriptor.Path),
                ("importPath", "tools://" + descriptor.Path),
                ("description", descriptor.Description),
                ("editorOnly", descriptor.EditorOnly),
                ("enabled", descriptor.Enabled),
                ("source", descriptor.Source),
                ("functions", descriptor.Functions.Select(ToFunctionJson).Cast<object?>().ToList())
            );
            if (descriptor.SubTools.Count > 0)
                result["subTools"] = descriptor.SubTools.Select(ToSummaryJson).Cast<object?>().ToList();
            return result;
        }

        private static Dictionary<string, object?> ToFunctionJson(EvalToolFunctionDescriptor function)
        {
            var result = EvalData.Obj(
                ("name", function.MethodName),
                ("methodName", function.MethodName),
                ("description", function.Description),
                ("safety", EvalToolSafetyUtility.ToJson(function.Safety)),
                ("riskLevel", function.RiskLevel),
                ("requiresConfirmation", function.RequiresConfirmation),
                ("parameters", function.Parameters.Select(parameter => ToParameterJson(function, parameter)).Cast<object?>().ToList())
            );
            AddConditionalSafetyMetadata(result, function);
            return result;
        }

        private static Dictionary<string, object?> ToParameterJson(EvalToolFunctionDescriptor function, EvalToolParameterDescriptor parameter) =>
            EvalData.Obj(
                ("name", parameter.Name),
                ("type", parameter.Type),
                ("optional", parameter.Optional),
                ("defaultValue", parameter.DefaultValue),
                ("description", GetParameterDescription(function, parameter))
            );

        private static Dictionary<string, object?> ToSummaryJson(EvalToolDescriptor descriptor) =>
            EvalData.Obj(
                ("name", descriptor.Name),
                ("path", descriptor.Path),
                ("importPath", "tools://" + descriptor.Path),
                ("description", descriptor.Description),
                ("editorOnly", descriptor.EditorOnly),
                ("enabled", descriptor.Enabled),
                ("source", descriptor.Source),
                ("functionCount", descriptor.Functions.Count)
            );

        private static Dictionary<string, object?> ToSummaryJson(EvalToolSummaryDescriptor descriptor) =>
            EvalData.Obj(
                ("name", descriptor.Name),
                ("path", descriptor.Path),
                ("importPath", "tools://" + descriptor.Path),
                ("description", descriptor.Description),
                ("editorOnly", descriptor.EditorOnly),
                ("enabled", descriptor.Enabled),
                ("source", descriptor.Source),
                ("functionCount", descriptor.FunctionCount)
            );

        private static Dictionary<string, object?> ToCliJson(EvalToolDescriptor descriptor) =>
            EvalData.Obj(
                ("name", descriptor.Name),
                ("path", descriptor.Path),
                ("importPath", "tools://" + descriptor.Path),
                ("description", descriptor.Description),
                ("editorOnly", descriptor.EditorOnly),
                ("enabled", descriptor.Enabled),
                ("source", descriptor.Source),
                ("functions", descriptor.Functions.Select(function => (object?)ToCliFunctionJson(descriptor, function)).ToList())
            );

        private static IEnumerable<object?> ToCliCommands(EvalToolDescriptor descriptor)
        {
            foreach (var function in descriptor.Functions)
                yield return ToCliFunctionJson(descriptor, function);
        }

        private static Dictionary<string, object?> ToCliFunctionJson(EvalToolDescriptor descriptor, EvalToolFunctionDescriptor function)
        {
            var result = EvalData.Obj(
                ("toolName", descriptor.Name),
                ("toolPath", descriptor.Path),
                ("name", function.MethodName),
                ("methodName", function.MethodName),
                ("command", descriptor.Path),
                ("description", function.Description),
                ("usage", BuildCliUsage(descriptor, function)),
                ("importPath", "tools://" + descriptor.Path),
                ("editorOnly", descriptor.EditorOnly),
                ("enabled", descriptor.Enabled),
                ("source", descriptor.Source),
                ("safety", EvalToolSafetyUtility.ToJson(function.Safety)),
                ("riskLevel", function.RiskLevel),
                ("requiresConfirmation", function.RequiresConfirmation),
                ("parameters", function.Parameters.Select(parameter => (object?)EvalData.Obj(
                    ("name", parameter.Name),
                    ("type", parameter.Type),
                    ("optional", parameter.Optional),
                    ("defaultValue", parameter.DefaultValue),
                    ("flags", BuildParameterFlags(parameter, function.Parameters)),
                    ("description", GetParameterDescription(function, parameter))
                )).ToList())
            );
            AddConditionalSafetyMetadata(result, function);
            return result;
        }

        private static void AddConditionalSafetyMetadata(Dictionary<string, object?> result, EvalToolFunctionDescriptor function)
        {
            var parameterNames = function.Parameters.Select(parameter => parameter.Name).ToList();
            var hasConfirmDangerous = parameterNames.Any(name =>
                string.Equals(name, "confirmDangerous", StringComparison.OrdinalIgnoreCase));
            var hasConfirmOverwrite = parameterNames.Any(name =>
                string.Equals(name, "confirmOverwrite", StringComparison.OrdinalIgnoreCase));
            if (hasConfirmDangerous)
            {
                result["conditionalRequiresConfirmation"] = true;
                result["conditionalReflectionDangerous"] = true;
                result["conditionalSafetyNote"] =
                    "Non-public, static, or reflection-heavy branches require confirmDangerous: true.";
            }
            else if (hasConfirmOverwrite)
            {
                result["conditionalRequiresConfirmation"] = true;
                result["conditionalDestructive"] = true;
                result["conditionalSafetyNote"] =
                    "Replacing an existing asset or file requires confirmOverwrite: true.";
            }
        }

        private static string GetParameterDescription(EvalToolFunctionDescriptor function, EvalToolParameterDescriptor parameter)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Description))
                return parameter.Description;

            if (parameter.Name == "refresh" &&
                (function.MethodName == "listTools" || function.MethodName == "getToolDetails"))
                return "Whether to rebuild the tool catalog before returning metadata.";
            if (parameter.Name == "name" &&
                (function.MethodName == "getToolDetails" || function.MethodName == "setToolEnabled"))
                return "Tool path or tool name, such as Runtime/Objects.";

            return parameter.Name switch
            {
                "target" => "GameObject, Component, Unity object, instance id, exact name/path, or selector object.",
                "path" => "Project-relative asset path, scene path, menu path, or output path depending on the command.",
                "from" => "Source project-relative asset path.",
                "to" => "Destination project-relative asset path.",
                "filter" => "Unity AssetDatabase search filter.",
                "folders" => "Optional folder path or array of folder paths that limits the search scope.",
                "limit" => "Maximum number of results to return. Values <= 0 mean no explicit limit where supported.",
                "count" => "Maximum number of entries to return.",
                "type" => "C# type name, component type name, log type, or mode depending on the command.",
                "index" => "Zero-based component or array index. -1 means default selection where supported.",
                "value" => "Value to assign or format.",
                "values" => "Object map of memberName -> value.",
                "changes" => "Array of {propertyPath,value} entries or object map of propertyPath -> value.",
                "propertyPath" => "Unity serialized property path.",
                "propertyPathKey" => "Unity serialized property path.",
                "member" => "Public field or property name on the selected component.",
                "method" => "Method name to invoke.",
                "args" => "Positional argument array for the method call.",
                "confirm" => "Must be true for operations with destructive or broad side effects.",
                "confirmOverwrite" => "Must be true when replacing an existing asset or file.",
                "confirmDangerous" => "Must be true for non-public, static, or reflection-heavy operations.",
                "includeInactive" => "Whether inactive scene objects are included.",
                "includeComponents" => "Whether GameObject summaries include component summaries.",
                "includeProperties" => "Whether importer summaries include serialized importer properties.",
                "includeNonPublic" => "Whether non-public members or methods are included.",
                "includeStatic" => "Whether static members are included.",
                "recursive" => "Whether dependency lookup includes nested dependencies.",
                "refresh" => "Whether to refresh AssetDatabase after the file edit.",
                "saveAndReimport" => "Whether to call SaveAndReimport after importer edits.",
                "isPlaying" => "Desired Editor play mode state.",
                "isPaused" => "Desired Editor pause state.",
                "active" => "Desired GameObject active state.",
                "name" => "Name to assign or lookup.",
                "by" => "Lookup mode: name, path, tag, or component.",
                "parent" => "Parent GameObject selector. Null means no parent where supported.",
                "worldPositionStays" => "Whether world transform is preserved when changing parent.",
                "position" => "World position as {x,y,z} or [x,y,z].",
                "localPosition" => "Local position as {x,y,z} or [x,y,z].",
                "rotationEuler" => "World Euler rotation as {x,y,z} or [x,y,z].",
                "localRotationEuler" => "Local Euler rotation as {x,y,z} or [x,y,z].",
                "localScale" => "Local scale as {x,y,z} or [x,y,z].",
                "primitive" => "Unity PrimitiveType name, or empty for a plain GameObject.",
                "layer" => "Unity layer integer. int.MinValue leaves it unchanged where supported.",
                "tag" => "Unity tag string. Empty leaves it unchanged where supported.",
                "mode" => "Command-specific mode string.",
                "className" => "C# class name to generate. Empty uses the file name.",
                "namespaceName" => "Optional C# namespace for generated scripts.",
                "shaderName" => "Shader name used when creating a material.",
                "properties" => "Object map of material/importer/serialized properties depending on the command.",
                "packageId" => "Package Manager package id or git URL to add.",
                "packageName" => "Package name to remove or search.",
                "testName" => "Optional test filter name.",
                "locationPathName" => "Build output path passed to BuildPipeline.",
                "host" => "Host/IP address to bind or connect.",
                "port" => "Port number. 0 means choose an available port where supported.",
                "token" => "Bearer token for authenticated local connections.",
                "requireToken" => "Whether token authentication is required.",
                "enabled" => "Desired enabled state.",
                "depth" => "Maximum object formatting or hierarchy traversal depth.",
                "size" => "New serialized array size.",
                "fullName" => "Full C# type name.",
                "query" => "Text query used to filter results.",
                _ => $"Parameter '{parameter.Name}' of type {parameter.Type}."
            };
        }

        private static string BuildCliUsage(EvalToolDescriptor descriptor, EvalToolFunctionDescriptor function)
        {
            var builder = new StringBuilder();
            builder.Append(descriptor.Path);
            builder.Append(' ');
            builder.Append(function.MethodName);
            foreach (var parameter in function.Parameters)
            {
                builder.Append(' ');
                builder.Append(parameter.Optional ? '[' : '<');
                builder.Append("--");
                builder.Append(ToKebabCase(parameter.Name));
                if (!IsBoolType(parameter.Type))
                {
                    builder.Append(' ');
                    builder.Append(parameter.Type);
                }
                builder.Append(parameter.Optional ? ']' : '>');
            }
            return builder.ToString();
        }

        private static bool IsBoolType(string type) =>
            string.Equals(type.TrimEnd('?'), "bool", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type.TrimEnd('?'), "boolean", StringComparison.OrdinalIgnoreCase);

        private static List<object?> BuildParameterFlags(
            EvalToolParameterDescriptor parameter,
            IReadOnlyList<EvalToolParameterDescriptor> allParameters)
        {
            var flags = new List<object?>();
            var parameterName = parameter.Name;
            if (!string.IsNullOrWhiteSpace(parameterName))
            {
                AddUnique(flags, "--" + ToKebabCase(parameterName));
                AddUnique(flags, "--" + parameterName);
                var shortFlag = "-" + char.ToLowerInvariant(parameterName[0]);
                var shortFlagIsUnique = allParameters.Count(other =>
                    !string.IsNullOrWhiteSpace(other.Name) &&
                    char.ToLowerInvariant(other.Name[0]) == char.ToLowerInvariant(parameterName[0])) == 1;
                if (shortFlagIsUnique && !IsReservedCliShortFlag(shortFlag))
                    AddUnique(flags, shortFlag);
            }
            return flags;
        }

        private static bool IsReservedCliShortFlag(string flag) =>
            string.Equals(flag, "-h", StringComparison.OrdinalIgnoreCase);

        private static void AddUnique(List<object?> flags, string flag)
        {
            if (!flags.Contains(flag))
                flags.Add(flag);
        }

        private static string ToKebabCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            var builder = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) builder.Append('-');
                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(c == '_' ? '-' : c);
                }
            }
            return builder.ToString();
        }

        private static string BuildDescription(IReadOnlyList<EvalToolDescriptor> tools)
        {
            var lines = tools.Select(tool =>
            {
                var tags = new List<string> { tool.Source };
                if (tool.EditorOnly) tags.Add("Editor-only");
                if (!tool.Enabled) tags.Add("disabled");
                return $"- {tool.Path}: `tools://{tool.Path}` [{string.Join(", ", tags)}] - {tool.Description}";
            });

            const string puertsInteropTutorial = @"Direct PuerTS C# interop tutorial:
- Use full C# names under `CS`; alias long names locally when that makes code clearer.
- JS/TypeScript generics are not C# runtime generics. Use `puer.$generic(openGenericType, ...typeArgs)` with arity suffixes such as `List$1` and `Dictionary$2`.
- JS `[]` only indexes native JS objects. Use `get_Item` and `set_Item` for C# arrays, `List<T>`, `Dictionary<TKey,TValue>`, and custom indexers.
- Use `puer.$typeof(Type)` for `System.Type` parameters, CLR `op_*` names for overloaded operators, `puer.$ref`/`puer.$unref` for ref/out, and `await puer.$promise(task)` for C# Task.

One compact teaching example:
```javascript
async function execute() {
  // 1) Basic C# access through CS: alias long names, create C# objects, read/write properties, call methods.
  const Vector3 = CS.UnityEngine.Vector3;
  const go = new CS.UnityEngine.GameObject('Temp');
  go.name = 'TempFromPuerTS';

  // 2) typeof: Unity APIs that expect System.Type need puer.$typeof(...), not C# typeof(...).
  go.AddComponent(puer.$typeof(CS.UnityEngine.ParticleSystem));

  // 3) Runtime generics: close C# generic types with puer.$generic(Type$N, ...typeArgs).
  const ListInt = puer.$generic(CS.System.Collections.Generic.List$1, CS.System.Int32);
  const list = new ListInt();
  list.Add(10);

  // 4) C# indexers: do not use list[0] or dict['hp']; use get_Item/set_Item.
  list.set_Item(0, 20);
  const first = list.get_Item(0);

  const DictStringInt = puer.$generic(
    CS.System.Collections.Generic.Dictionary$2,
    CS.System.String,
    CS.System.Int32
  );
  const dict = new DictStringInt();
  dict.set_Item('hp', 100);

  // 5) Operator overloads: call CLR op_* methods because JS operators do not dispatch C# overloads.
  const doubledUp = Vector3.op_Multiply(Vector3.up, 2);

  // 6) ref/out pattern: replace ExampleApi.TryGetValue with the real C# method you are calling.
  // const outValue = puer.$ref();
  // const ok = CS.ExampleApi.TryGetValue('key', outValue);
  // const value = puer.$unref(outValue);

  // 7) Async C# Task pattern: wrap the returned Task before await.
  // const task = CS.ExampleApi.LoadCountAsync();
  // const count = await puer.$promise(task);

  return {
    name: go.name,
    first,
    hp: dict.get_Item('hp'),
    hasParticleSystem: go.GetComponent(puer.$typeof(CS.UnityEngine.ParticleSystem)) !== null,
    doubledUp: doubledUp.ToString()
  };
}
```";

            return @$"Within the Broker's `eval` tool, UnityEvalTool exposes loader-backed Unity helper modules.

Inside `eval`, import Unity helper tools with the `tools://` protocol:

```javascript
async function execute() {{
  const index = await import('tools://');
  const runtime = await import('tools://Runtime');
  return {{ tools: index.listTools(), state: runtime.getState() }};
}}
```

Discovery:
- `tools://` returns the concise root tool index module.
- `index.listTools()` returns concise root tool summaries only.
- Call `index.getToolDetails('Tool/Path')` when you need that tool's method descriptions, direct sub tools, parameter order, defaults, and safety flags.
- Import a concrete tool such as `tools://Editor/Assets`; its exported `functions` array contains method metadata.
- Tool details include `subTools` only when direct child tools exist. If `subTools` is absent, do not invent or explore child paths.

Available tools:
{string.Join("\n", lines)}

Call pattern:
- C# tools are imported as direct method modules, for example `const runtime = await import('tools://Runtime'); runtime.getState()`.
- Generated C# tool functions use positional arguments in the order shown by `functions[].parameters`.
- Prefer helper tools for common Unity workflows; use direct PuerTS `CS.*` interop only when no helper covers the task.
- Plain imports such as `import('path/test.mjs')` use the configured JavaScript loader and never fall back to Tool lookup.

{puertsInteropTutorial}
".Trim();
        }
    }
}
