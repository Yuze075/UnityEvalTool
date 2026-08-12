#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Puerts;

namespace YuzeToolkit
{
    public static partial class EvalToolRegistry
    {
        private static readonly object JsMetadataSyncRoot = new();
        private static ScriptEnv? JsMetadataEnv;
        private const int JsMetadataTimeoutMilliseconds = 100;
        private const int JsMetadataMaxTicks = 16;

        public static bool TryRegisterJsTool(string modulePath) =>
            TryReadJsMetadata(modulePath, out var name, out var description) &&
            TryRegisterJsTool(modulePath, name, description);

        public static bool TryRegisterJsTool(string modulePath, string name, string description)
        {
            if (string.IsNullOrWhiteSpace(modulePath) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            var loader = EvalScriptLoader.Loader ?? new Puerts.DefaultLoader();
            if (!loader.FileExists(modulePath))
                return false;

            ValidateToolSegment(name);
            lock (SyncRoot)
            {
                if (CSharpRoots.ContainsKey(name) || JsRoots.ContainsKey(name)) return false;
                JsRoots.Add(name, new JsToolRegistration(modulePath, name, description));
                EnsureKnownNoLock(name);
            }

            ClearGeneratedModuleCache();
            Changed?.Invoke();
            return true;
        }

        public static string GetJsToolAuthoringPrompt()
        {
            return @"Create a UnityEvalTool loader-backed JavaScript Tool module.

Lifecycle rules:
- C# tools are compiled into Unity assemblies and are registered as IEvalTool instances.
- JavaScript tools are registered by module path during initialization. They are not added from strings or folders at runtime.
- `import(""tools://"")` reads cached root tool metadata. A concrete `tools://<path>` import loads the JS module only when needed.

Module requirements:
- Export const name as a non-empty path segment.
- Export const description as a non-empty string.
- Export const functions as an array of function descriptors. `parameters` is the only parameter metadata source.
- Every descriptor includes name or methodName, description, and parameters.
- Every descriptor methodName must match an exported JavaScript function.
- Optionally export const subTools as an array of direct child summaries. Omit subTools entirely when there are no direct children.
- Each sub tool summary includes name, path, and description.
- A parent with child tool instances should export `getSubTool(nameOrPath)`. Concrete imports such as `tools://Debug/Button` use it to resolve the child instance. If `getSubTool` is absent, the matching item in `subTools` is used directly.

Example:
```javascript
export const name = 'Debug';
export const description = 'Small JavaScript tool example.';
export const functions = [
  {
    name: 'echo',
    methodName: 'echo',
    description: 'Return the provided value.',
    parameters: [
      { name: 'value', type: 'string', optional: false, defaultValue: null, description: 'Value to echo.' }
    ]
  }
];

export function echo(value) {
  return { value };
}
```";
        }

        private static bool TryGetJsModuleSource(string path, out string source)
        {
            source = string.Empty;
            JsToolRegistration registration;
            string rootName;
            lock (SyncRoot)
            {
                rootName = GetRootName(path);
                if (!JsRoots.TryGetValue(rootName, out var tool)) return false;
                registration = tool;
            }

            var normalizedPath = NormalizePath(path);
            if (!TryReadJsDescriptor(registration.ModulePath, rootName, normalizedPath, out var descriptor))
                return false;

            source = BuildJsToolModuleSource(registration.ModulePath, rootName, descriptor);
            return true;
        }

        private static bool TryGetJsDescriptor(string path, out EvalToolDescriptor descriptor)
        {
            descriptor = null!;
            if (string.IsNullOrWhiteSpace(path)) return false;

            JsToolRegistration tool;
            lock (SyncRoot)
            {
                var rootName = GetRootName(path);
                if (!JsRoots.TryGetValue(rootName, out tool)) return false;
            }

            if (TryReadJsDescriptor(tool.ModulePath, tool.Name, NormalizePath(path), out descriptor))
                return true;

            if (string.Equals(NormalizePath(path), tool.Name, StringComparison.Ordinal))
            {
                descriptor = new EvalToolDescriptor(
                    tool.Name,
                    tool.Name,
                    tool.Description,
                    false,
                    IsEnabled(tool.Name),
                    "js",
                    EvalToolFunctionDescriptor.Empty);
            }

            return descriptor != null;
        }

        private static bool TryReadJsMetadata(string modulePath, out string name, out string description)
        {
            name = string.Empty;
            description = string.Empty;
            if (string.IsNullOrWhiteSpace(modulePath)) return false;

            lock (JsMetadataSyncRoot)
            {
                if (!TryRunJsMetadataRequest(BuildJsMetadataRunner(modulePath), "jsTool.metadata", out var payload))
                    return false;
                var data = EvalData.AsObject(LitJson.Parse(payload));
                if (data == null || !EvalData.GetBool(data, "success")) return false;
                name = EvalData.GetString(data, "name") ?? string.Empty;
                description = EvalData.GetString(data, "description") ?? string.Empty;
                return !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(description);
            }
        }

        private static bool TryReadJsDescriptor(string modulePath, string rootName, string path, out EvalToolDescriptor descriptor)
        {
            descriptor = null!;
            lock (JsMetadataSyncRoot)
            {
                if (!TryRunJsMetadataRequest(BuildJsDescriptorRunner(modulePath, rootName, path), "jsTool.descriptor",
                        out var payload))
                    return false;
                var data = EvalData.AsObject(LitJson.Parse(payload));
                if (data == null || !EvalData.GetBool(data, "success")) return false;
                var tool = EvalData.AsObject(data.TryGetValue("tool", out var toolValue) ? toolValue : null);
                if (tool == null) return false;
                descriptor = JsDescriptorFromJson(tool, path);
                return true;
            }
        }

        private static bool TryRunJsMetadataRequest(string source, string evalName, out string payload)
        {
            payload = string.Empty;
            var resultPayload = string.Empty;
            var env = EnsureJsMetadataEnv();
            var completed = false;

            try
            {
                var runner = env.Eval<Action<Action<string>>>(source, evalName);
                runner(result =>
                {
                    resultPayload = result;
                    completed = true;
                });

                var stopwatch = Stopwatch.StartNew();
                var tickCount = 0;
                while (!completed &&
                       tickCount < JsMetadataMaxTicks &&
                       stopwatch.ElapsedMilliseconds < JsMetadataTimeoutMilliseconds)
                {
                    TickJsMetadataEnv(env);
                    tickCount++;
                }
            }
            catch
            {
                ResetJsMetadataEnv();
                return false;
            }

            payload = resultPayload;
            return completed;
        }

        private static void TickJsMetadataEnv(ScriptEnv env)
        {
            if (MainThreadDispatcher.IsMainThread)
            {
                env.Tick();
                return;
            }

            MainThreadDispatcher.RunAsync(env.Tick).GetAwaiter().GetResult();
            Thread.Sleep(1);
        }

        private static bool JsToolPathMayExist(string path)
        {
            var normalized = NormalizePath(path);
            var rootName = GetRootName(normalized);
            lock (SyncRoot)
                return JsRoots.ContainsKey(rootName);
        }

        private static void RefreshToolMetadataCaches()
        {
            ClearGeneratedModuleCache();
            ResetJsMetadataEnv();
        }

        private static ScriptEnv EnsureJsMetadataEnv()
        {
            JsMetadataEnv ??= PuerTsBackendFactory.Create(new EvalScriptLoader());
            return JsMetadataEnv;
        }

        private static void ResetJsMetadataEnv()
        {
            try
            {
                JsMetadataEnv?.Dispose();
            }
            catch
            {
                // Metadata imports are best-effort; registration failure will surface as false.
            }
            finally
            {
                JsMetadataEnv = null;
            }
        }

        private static string BuildJsMetadataRunner(string modulePath)
        {
            return @"(function(onFinish) {
  import('" + EscapeJavaScriptString(modulePath) + @"')
    .then(function(module) {
      onFinish.Invoke(JSON.stringify({
        success: true,
        name: String(module.name || ''),
        description: String(module.description || '')
      }));
    })
    .catch(function(err) {
      onFinish.Invoke(JSON.stringify({
        success: false,
        error: String((err && err.message) || err)
      }));
    });
})";
        }

        private static string BuildJsDescriptorRunner(string modulePath, string rootName, string path)
        {
            return @"(function(onFinish) {
  function toArray(value) {
    return Array.isArray(value) ? value : [];
  }
  function functionDescriptor(fn) {
    const methodName = String((fn && (fn.methodName || fn.name)) || '');
    return {
      name: methodName,
      methodName,
      description: String((fn && fn.description) || ''),
      parameters: toArray(fn && fn.parameters).map(function(parameter) {
        return {
          name: String((parameter && parameter.name) || ''),
          type: String((parameter && parameter.type) || 'object'),
          optional: Boolean(parameter && parameter.optional),
          defaultValue: parameter ? parameter.defaultValue : null,
          description: String((parameter && parameter.description) || '')
        };
      })
    };
  }
  function summary(child, parentPath) {
    const name = String((child && child.name) || '');
    const path = String((child && child.path) || (parentPath ? parentPath + '/' + name : name));
    return {
      name,
      path,
      description: String((child && child.description) || ''),
      functionCount: toArray(child && child.functions).length
    };
  }
  function descriptor(tool, fallbackPath) {
    const name = String((tool && tool.name) || fallbackPath.split('/').pop() || '');
    const path = String((tool && tool.path) || fallbackPath);
    return {
      name,
      path,
      description: String((tool && tool.description) || ''),
      functions: toArray(tool && tool.functions).map(functionDescriptor),
      subTools: toArray(tool && tool.subTools).map(function(child) { return summary(child, path); })
    };
  }
  async function resolve(root) {
    const fullPath = '" + EscapeJavaScriptString(path) + @"';
    const segments = fullPath.split('/').filter(Boolean);
    const rootName = '" + EscapeJavaScriptString(rootName) + @"';
    if (segments.length === 0 || segments[0] !== rootName) throw new Error('JS tool root mismatch: ' + fullPath);
    let current = root;
    let currentPath = String(root.path || root.name || rootName);
    for (let i = 1; i < segments.length; i++) {
      const segment = segments[i];
      const children = toArray(current && current.subTools);
      const expectedPath = currentPath + '/' + segment;
      const child = children.find(function(candidate) {
        return String(candidate && candidate.name) === segment ||
          String(candidate && candidate.path) === expectedPath ||
          String(candidate && candidate.path) === segments.slice(0, i + 1).join('/');
      });
      if (!child) throw new Error('JS sub tool not found: ' + expectedPath);
      if (current && typeof current.getSubTool === 'function') {
        current = await current.getSubTool(segment);
      } else {
        current = child;
      }
      currentPath = String((current && current.path) || child.path || expectedPath);
    }
    return descriptor(current, fullPath);
  }
  import('" + EscapeJavaScriptString(modulePath) + @"')
    .then(resolve)
    .then(function(tool) {
      onFinish.Invoke(JSON.stringify({ success: true, tool }));
    })
    .catch(function(err) {
      onFinish.Invoke(JSON.stringify({ success: false, error: String((err && err.message) || err) }));
    });
})";
        }

        private static EvalToolDescriptor JsDescriptorFromJson(Dictionary<string, object?> data, string fallbackPath)
        {
            var path = EvalData.GetString(data, "path") ?? fallbackPath;
            var functions = new List<EvalToolFunctionDescriptor>();
            if (data.TryGetValue("functions", out var functionsValue) &&
                EvalData.AsArray(functionsValue) is { } functionList)
            {
                foreach (var functionValue in functionList)
                {
                    var function = EvalData.AsObject(functionValue);
                    if (function == null) continue;
                    var methodName = EvalData.GetString(function, "methodName") ??
                                     EvalData.GetString(function, "name") ??
                                     string.Empty;
                    if (string.IsNullOrWhiteSpace(methodName)) continue;

                    var parameters = new List<EvalToolParameterDescriptor>();
                    if (function.TryGetValue("parameters", out var parametersValue) &&
                        EvalData.AsArray(parametersValue) is { } parameterList)
                    {
                        foreach (var parameterValue in parameterList)
                        {
                            var parameter = EvalData.AsObject(parameterValue);
                            if (parameter == null) continue;
                            parameters.Add(new EvalToolParameterDescriptor(
                                EvalData.GetString(parameter, "name") ?? string.Empty,
                                EvalData.GetString(parameter, "type") ?? "object",
                                EvalData.GetBool(parameter, "optional"),
                                parameter.TryGetValue("defaultValue", out var defaultValue) ? defaultValue : null,
                                EvalData.GetString(parameter, "description") ?? string.Empty));
                        }
                    }

                    functions.Add(new EvalToolFunctionDescriptor(
                        methodName,
                        EvalData.GetString(function, "description") ?? string.Empty,
                        parameters));
                }
            }

            var subTools = new List<EvalToolSummaryDescriptor>();
            if (data.TryGetValue("subTools", out var subToolsValue) &&
                EvalData.AsArray(subToolsValue) is { } subToolList)
            {
                foreach (var subToolValue in subToolList)
                {
                    var subTool = EvalData.AsObject(subToolValue);
                    if (subTool == null) continue;
                    subTools.Add(new EvalToolSummaryDescriptor(
                        EvalData.GetString(subTool, "name") ?? string.Empty,
                        EvalData.GetString(subTool, "path") ?? string.Empty,
                        EvalData.GetString(subTool, "description") ?? string.Empty,
                        false,
                        IsEnabled(EvalData.GetString(subTool, "path") ?? string.Empty),
                        "js",
                        EvalData.GetInt(subTool, "functionCount")));
                }
            }

            return new EvalToolDescriptor(
                EvalData.GetString(data, "name") ?? path.Split('/').Last(),
                path,
                EvalData.GetString(data, "description") ?? string.Empty,
                false,
                IsEnabled(path),
                "js",
                functions.Select(function => EvalToolSafetyUtility.Apply(path, function)).ToList(),
                subTools);
        }

        private static string BuildJsToolModuleSource(string modulePath, string rootName, EvalToolDescriptor descriptor)
        {
            var functionsJson = LitJson.Stringify(descriptor.Functions.Select(function => EvalData.Obj(
                ("name", function.MethodName),
                ("methodName", function.MethodName),
                ("description", function.Description),
                ("safety", EvalToolSafetyUtility.ToJson(function.Safety)),
                ("riskLevel", function.RiskLevel),
                ("requiresConfirmation", function.RequiresConfirmation),
                ("parameters", function.Parameters.Select(parameter => (object?)EvalData.Obj(
                    ("name", parameter.Name),
                    ("type", parameter.Type),
                    ("optional", parameter.Optional),
                    ("defaultValue", parameter.DefaultValue),
                    ("description", parameter.Description)
                )).ToList())
            )).Cast<object?>().ToList());
            var subToolsJson = LitJson.Stringify(descriptor.SubTools.Select(subTool => EvalData.Obj(
                ("name", subTool.Name),
                ("path", subTool.Path),
                ("description", subTool.Description),
                ("functionCount", subTool.FunctionCount)
            )).Cast<object?>().ToList());

            var builder = new StringBuilder();
            builder.AppendLine($"import * as __root from '{EscapeJavaScriptString(modulePath)}';");
            builder.AppendLine($"const __path = '{EscapeJavaScriptString(descriptor.Path)}';");
            builder.AppendLine($"const __rootName = '{EscapeJavaScriptString(rootName)}';");
            builder.AppendLine(@"
function __toArray(value) {
  return Array.isArray(value) ? value : [];
}

	function __ensureEnabled() {
	  if (!CS.YuzeToolkit.EvalToolRegistry.IsEnabled(__path)) {
	    throw new Error('Eval JS tool ' + __path + ' is disabled.');
	  }
	}

async function __resolveTool() {
  __ensureEnabled();
  const segments = __path.split('/').filter(Boolean);
  let current = __root;
  let currentPath = String(__root.path || __root.name || __rootName);
  for (let i = 1; i < segments.length; i++) {
    const segment = segments[i];
    const expectedPath = currentPath + '/' + segment;
    const child = __toArray(current && current.subTools).find(candidate =>
      String(candidate && candidate.name) === segment ||
      String(candidate && candidate.path) === expectedPath ||
      String(candidate && candidate.path) === segments.slice(0, i + 1).join('/'));
    if (!child) throw new Error('JS sub tool not found: ' + expectedPath);
    current = current && typeof current.getSubTool === 'function'
      ? await current.getSubTool(segment)
      : child;
    currentPath = String((current && current.path) || child.path || expectedPath);
  }
  return current;
}
");
            builder.AppendLine($"export const name = '{EscapeJavaScriptString(descriptor.Name)}';");
            builder.AppendLine($"export const path = '{EscapeJavaScriptString(descriptor.Path)}';");
            builder.AppendLine($"export const description = '{EscapeJavaScriptString(descriptor.Description)}';");
            builder.AppendLine($"export const functions = {functionsJson};");
            if (descriptor.SubTools.Count > 0)
                builder.AppendLine($"export const subTools = {subToolsJson};");
            builder.AppendLine("export function isEnabled() {");
            builder.AppendLine("  return CS.YuzeToolkit.EvalToolRegistry.IsEnabled(__path);");
            builder.AppendLine("}");
            builder.AppendLine("export async function getSubTool(nameOrPath) {");
            builder.AppendLine("  const tool = await __resolveTool();");
            builder.AppendLine("  if (!tool || typeof tool.getSubTool !== 'function') throw new Error('JS tool has no sub tool resolver: ' + path);");
            builder.AppendLine("  return await tool.getSubTool(nameOrPath);");
            builder.AppendLine("}");
            builder.AppendLine("export async function invoke(methodName, ...args) {");
            builder.AppendLine("  const tool = await __resolveTool();");
            builder.AppendLine("  const fn = tool && tool[String(methodName)];");
            builder.AppendLine("  if (typeof fn !== 'function') throw new Error('JS tool function not found: ' + String(methodName));");
            builder.AppendLine("  return await fn.apply(tool, args);");
            builder.AppendLine("}");
            foreach (var function in descriptor.Functions)
            {
                if (!IsValidJavaScriptIdentifier(function.MethodName)) continue;
                builder.AppendLine($"export async function {function.MethodName}(...args) {{");
                builder.AppendLine($"  return await invoke('{EscapeJavaScriptString(function.MethodName)}', ...args);");
                builder.AppendLine("}");
            }

            return builder.ToString();
        }
    }
}
