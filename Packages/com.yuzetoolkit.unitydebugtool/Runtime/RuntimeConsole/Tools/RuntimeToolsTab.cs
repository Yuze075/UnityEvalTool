#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal sealed class RuntimeToolsTab : RuntimeConsoleTabBase
    {
        private const string ToolItemClass = "yuzu-runtime-tool-item";
        private const string ToolHeaderClass = "yuzu-runtime-tool-header";
        private const string ToolDescriptionClass = "yuzu-runtime-tool-description";
        private static readonly Color DisabledColor = new(0.4f, 0.44f, 0.5f);

        private readonly RuntimeConsolePanView _content = RuntimeConsoleUi.CreatePanView();
        private bool _dirty = true;

        public RuntimeToolsTab() : base("tools", "Tools", 30)
        {
            Build();
            EvalToolRegistry.Changed += MarkDirty;
        }

        public override void Tick()
        {
            if (_dirty)
                RefreshTools(false);
        }

        public override void Shutdown()
        {
            EvalToolRegistry.Changed -= MarkDirty;
            base.Shutdown();
        }

        private void Build()
        {
            var toolbar = RuntimeConsoleUi.CreateToolbar();
            Root.Add(toolbar);
            toolbar.Add(RuntimeConsoleUi.CreateButton("Refresh Tools",
                "Refresh registered C# tools and explicitly loaded JavaScript tools.", () => RefreshTools(true), 110));

            var hint = new Label("Complete runtime tool catalog and per-tool controls");
            hint.AddToClassList(RuntimeConsoleUss.LabelClass);
            hint.AddToClassList(RuntimeConsoleUss.MutedLabelClass);
            hint.style.marginLeft = 8;
            toolbar.Add(hint);

            var page = RuntimeConsoleUi.CreatePage();
            page.Add(_content.Root);
            Root.Add(page);
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void RefreshTools(bool forceRefresh)
        {
            _dirty = false;
            _content.Clear();
            List<EvalToolDescriptor> tools;
            try
            {
                tools = ReadCompleteCatalog(forceRefresh)
                    .Where(tool => !tool.EditorOnly)
                    .OrderBy(tool => tool.Path, StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _content.Add(RuntimeConsoleUi.CreateMessage(
                    "Runtime tool catalog could not be read: " + exception.Message,
                    RuntimeConsoleUi.ErrorColor));
                return;
            }

            if (tools.Count == 0)
            {
                _content.Add(RuntimeConsoleUi.CreateMessage("No runtime tools are registered.", RuntimeConsoleUi.WarningColor));
                return;
            }

            AddSection("C# Tools", tools.Where(tool => tool.Source.Equals("csharp", StringComparison.OrdinalIgnoreCase)).ToList(), tools);
            AddSection("JavaScript Tools", tools.Where(tool => tool.Source.Equals("js", StringComparison.OrdinalIgnoreCase)).ToList(), tools);
        }

        private void AddSection(string title, IReadOnlyList<EvalToolDescriptor> tools,
            IReadOnlyList<EvalToolDescriptor> completeCatalog)
        {
            if (tools.Count == 0) return;
            var section = RuntimeConsoleUi.CreateCard();
            RuntimeConsoleUi.AddTitle(section, $"{title} ({tools.Count})");
            foreach (var tool in tools)
                section.Add(CreateToolItem(tool, IsBlockedByAncestor(tool, completeCatalog)));
            _content.Add(section);
        }

        private VisualElement CreateToolItem(EvalToolDescriptor tool, bool blockedByAncestor)
        {
            var foldout = new Foldout
            {
                text = tool.Path,
                value = false,
                tooltip = tool.Description
            };
            foldout.AddToClassList(ToolItemClass);
            DisableKeyboardFocus(foldout);
            if (foldout.Q<Toggle>() is { } foldoutToggle)
                DisableKeyboardFocus(foldoutToggle);

            var header = new VisualElement();
            header.AddToClassList(ToolHeaderClass);
            foldout.Add(header);

            var description = new Label(string.IsNullOrWhiteSpace(tool.Description) ? "No description." : tool.Description);
            description.AddToClassList(ToolDescriptionClass);
            description.style.whiteSpace = WhiteSpace.Normal;
            header.Add(description);

            var enabled = RuntimeConsoleUi.CreateButton(string.Empty,
                blockedByAncestor
                    ? "This tool is unavailable because an ancestor tool is disabled. Enable the ancestor first."
                    : "Enable or disable importing and invoking this exact tool path.", () =>
                {
                    EvalToolRegistry.SetEnabled(tool.Path, !tool.Enabled);
                    MarkDirty();
                }, 92);
            enabled.SetEnabled(!blockedByAncestor);
            SetSwitchStyle(enabled, tool.Enabled, blockedByAncestor);
            header.Add(enabled);

            RuntimeConsoleUi.AddField(foldout, "Import Path").text = $"tools://{tool.Path}";
            RuntimeConsoleUi.AddField(foldout, "Source").text = tool.Source;
            RuntimeConsoleUi.AddField(foldout, "Availability").text = tool.EditorOnly ? "Editor only" : "Editor and Player";
            RuntimeConsoleUi.AddField(foldout, "Contents").text =
                $"{tool.Functions.Count} functions";
            AddFunctions(foldout, tool.Functions);
            return foldout;
        }

        private static void AddFunctions(VisualElement parent, IReadOnlyList<EvalToolFunctionDescriptor> functions)
        {
            var title = new Label($"Functions ({functions.Count})");
            title.AddToClassList(RuntimeConsoleUss.LabelClass);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 8;
            title.style.marginBottom = 4;
            parent.Add(title);

            if (functions.Count == 0)
            {
                var empty = new Label("No generated function metadata is available.");
                empty.AddToClassList(RuntimeConsoleUss.LabelClass);
                empty.AddToClassList(RuntimeConsoleUss.MutedLabelClass);
                parent.Add(empty);
                return;
            }

            foreach (var function in functions)
            {
                var signature = function.MethodName + "(" + string.Join(", ", function.Parameters.Select(FormatParameter)) + ")";
                var value = RuntimeConsoleUi.AddField(parent, signature);
                value.text = string.IsNullOrWhiteSpace(function.Description) ? "—" : function.Description;
                value.tooltip = BuildFunctionTooltip(function);
                if (function.RequiresConfirmation)
                    value.style.color = RuntimeConsoleUi.WarningColor;
            }
        }

        private static bool IsBlockedByAncestor(EvalToolDescriptor tool,
            IReadOnlyList<EvalToolDescriptor> completeCatalog)
        {
            var separator = tool.Path.LastIndexOf('/');
            while (separator > 0)
            {
                var parentPath = tool.Path.Substring(0, separator);
                var parent = completeCatalog.FirstOrDefault(candidate =>
                    string.Equals(candidate.Path, parentPath, StringComparison.Ordinal));
                if (parent != null && !parent.Enabled) return true;
                separator = parentPath.LastIndexOf('/');
            }

            return false;
        }

        private static string FormatParameter(EvalToolParameterDescriptor parameter)
        {
            var optional = parameter.Optional ? "?" : string.Empty;
            var defaultValue = parameter.Optional
                ? " = " + (parameter.DefaultValue == null
                    ? "null"
                    : Convert.ToString(parameter.DefaultValue, CultureInfo.InvariantCulture))
                : string.Empty;
            return $"{parameter.Name}{optional}: {parameter.Type}{defaultValue}";
        }

        private static string BuildFunctionTooltip(EvalToolFunctionDescriptor function)
        {
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(function.Description))
                details.Add(function.Description);
            if (function.Safety != EvalToolSafety.Unspecified)
                details.Add("Risk: " + function.RiskLevel + (function.RequiresConfirmation ? " (confirmation required)" : string.Empty));
            details.AddRange(function.Parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Description))
                .Select(parameter => parameter.Name + ": " + parameter.Description));
            return string.Join("\n", details);
        }

        private static void SetSwitchStyle(Button button, bool enabled, bool blockedByAncestor = false)
        {
            button.text = blockedByAncestor ? "○  Blocked" : enabled ? "●  Enabled" : "○  Disabled";
            button.style.backgroundColor = enabled && !blockedByAncestor
                ? RuntimeConsoleUi.RunningColor
                : DisabledColor;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 12;
            button.style.borderTopRightRadius = 12;
            button.style.borderBottomLeftRadius = 12;
            button.style.borderBottomRightRadius = 12;
        }

        internal static IReadOnlyList<EvalToolDescriptor> ReadCompleteCatalog(bool refresh)
        {
            var catalog = EvalToolRegistry.GetCliCatalog(refresh);
            if (!catalog.TryGetValue("tools", out var toolsValue) || EvalData.AsArray(toolsValue) is not { } tools)
                throw new InvalidOperationException("UnityEvalTool CLI catalog did not contain a tools array.");

            var result = new List<EvalToolDescriptor>(tools.Count);
            foreach (var toolValue in tools)
            {
                var tool = EvalData.AsObject(toolValue) ??
                           throw new InvalidOperationException("UnityEvalTool CLI catalog contained a malformed tool entry.");
                result.Add(ParseTool(tool));
            }

            return result;
        }

        private static EvalToolDescriptor ParseTool(Dictionary<string, object?> tool)
        {
            var name = EvalData.GetString(tool, "name") ?? throw new InvalidOperationException("Tool name is missing.");
            var path = EvalData.GetString(tool, "path") ?? throw new InvalidOperationException($"Tool '{name}' path is missing.");
            var source = EvalData.GetString(tool, "source") ?? "unknown";
            var functions = new List<EvalToolFunctionDescriptor>();
            if (tool.TryGetValue("functions", out var functionsValue) && EvalData.AsArray(functionsValue) is { } functionValues)
            {
                foreach (var functionValue in functionValues)
                {
                    var function = EvalData.AsObject(functionValue) ??
                                   throw new InvalidOperationException($"Tool '{path}' contained a malformed function entry.");
                    functions.Add(ParseFunction(path, function));
                }
            }

            return new EvalToolDescriptor(
                name,
                path,
                EvalData.GetString(tool, "description") ?? string.Empty,
                EvalData.GetBool(tool, "editorOnly"),
                EvalData.GetBool(tool, "enabled", true),
                source,
                functions);
        }

        private static EvalToolFunctionDescriptor ParseFunction(string toolPath, Dictionary<string, object?> function)
        {
            var methodName = EvalData.GetString(function, "methodName") ?? EvalData.GetString(function, "name") ??
                             throw new InvalidOperationException($"Tool '{toolPath}' contained a function without a name.");
            var parameters = new List<EvalToolParameterDescriptor>();
            if (function.TryGetValue("parameters", out var parametersValue) && EvalData.AsArray(parametersValue) is { } parameterValues)
            {
                foreach (var parameterValue in parameterValues)
                {
                    var parameter = EvalData.AsObject(parameterValue) ??
                                    throw new InvalidOperationException($"Function '{toolPath}.{methodName}' contained a malformed parameter.");
                    parameters.Add(new EvalToolParameterDescriptor(
                        EvalData.GetString(parameter, "name") ?? string.Empty,
                        EvalData.GetString(parameter, "type") ?? "object",
                        EvalData.GetBool(parameter, "optional"),
                        parameter.TryGetValue("defaultValue", out var defaultValue) ? defaultValue : null,
                        EvalData.GetString(parameter, "description") ?? string.Empty));
                }
            }

            return new EvalToolFunctionDescriptor(
                methodName,
                EvalData.GetString(function, "description") ?? string.Empty,
                parameters,
                ParseSafety(function));
        }

        internal static EvalToolSafety ParseSafety(Dictionary<string, object?> function)
        {
            if (!function.TryGetValue("safety", out var safetyValue))
                return EvalToolSafety.Unspecified;

            var safety = EvalData.AsObject(safetyValue) ??
                         throw new InvalidOperationException("Tool function safety metadata was not an object.");
            if (!safety.TryGetValue("flags", out var flagsValue) || EvalData.AsArray(flagsValue) is not { } flags)
                throw new InvalidOperationException("Tool function safety metadata did not contain a flags array.");

            var result = EvalToolSafety.Unspecified;
            foreach (var flagValue in flags)
            {
                var name = Convert.ToString(flagValue, CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(name) ||
                    !Enum.TryParse(name, ignoreCase: true, out EvalToolSafety flag) ||
                    flag == EvalToolSafety.Unspecified ||
                    !Enum.IsDefined(typeof(EvalToolSafety), flag) ||
                    !IsSingleFlag(flag))
                    throw new InvalidOperationException($"Tool function safety metadata contained unknown flag '{name ?? "null"}'.");

                result |= flag;
            }

            return result;
        }

        private static bool IsSingleFlag(EvalToolSafety flag)
        {
            var value = (int)flag;
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static void DisableKeyboardFocus(VisualElement element)
        {
            element.focusable = false;
            element.tabIndex = -1;
        }
    }
}
