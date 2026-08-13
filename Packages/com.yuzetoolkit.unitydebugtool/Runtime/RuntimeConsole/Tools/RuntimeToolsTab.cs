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
            if (forceRefresh)
                _ = EvalToolRegistry.GetIndex(true);

            _content.Clear();
            var tools = EvalToolRegistry.ListTools(false)
                .Where(tool => !tool.EditorOnly)
                .OrderBy(tool => tool.Path, StringComparer.Ordinal)
                .ToList();
            if (tools.Count == 0)
            {
                _content.Add(RuntimeConsoleUi.CreateMessage("No runtime tools are registered.", RuntimeConsoleUi.WarningColor));
                return;
            }

            AddSection("C# Tools", tools.Where(tool => tool.Source.Equals("csharp", StringComparison.OrdinalIgnoreCase)).ToList());
            AddSection("JavaScript Tools", tools.Where(tool => tool.Source.Equals("js", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void AddSection(string title, IReadOnlyList<EvalToolDescriptor> tools)
        {
            if (tools.Count == 0) return;
            var section = RuntimeConsoleUi.CreateCard();
            RuntimeConsoleUi.AddTitle(section, $"{title} ({tools.Count})");
            foreach (var tool in tools)
                section.Add(CreateToolItem(tool));
            _content.Add(section);
        }

        private VisualElement CreateToolItem(EvalToolDescriptor tool)
        {
            var foldout = new Foldout
            {
                text = tool.Name,
                value = false,
                tooltip = tool.Description
            };
            foldout.AddToClassList(ToolItemClass);

            var header = new VisualElement();
            header.AddToClassList(ToolHeaderClass);
            foldout.Add(header);

            var description = new Label(string.IsNullOrWhiteSpace(tool.Description) ? "No description." : tool.Description);
            description.AddToClassList(ToolDescriptionClass);
            description.style.whiteSpace = WhiteSpace.Normal;
            header.Add(description);

            var enabled = RuntimeConsoleUi.CreateButton(string.Empty,
                "Enable or disable importing and invoking this tool.", () =>
                {
                    EvalToolRegistry.SetEnabled(tool.Path, !tool.Enabled);
                    MarkDirty();
                }, 92);
            SetSwitchStyle(enabled, tool.Enabled);
            header.Add(enabled);

            RuntimeConsoleUi.AddField(foldout, "Import Path").text = $"tools://{tool.Path}";
            RuntimeConsoleUi.AddField(foldout, "Source").text = tool.Source;
            RuntimeConsoleUi.AddField(foldout, "Availability").text = tool.EditorOnly ? "Editor only" : "Editor and Player";
            RuntimeConsoleUi.AddField(foldout, "Contents").text =
                $"{tool.Functions.Count} functions / {tool.SubTools.Count} sub tools";
            AddFunctions(foldout, tool.Functions);
            AddSubTools(foldout, tool.SubTools);
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

        private static void AddSubTools(VisualElement parent, IReadOnlyList<EvalToolSummaryDescriptor> subTools)
        {
            if (subTools.Count == 0) return;
            var title = new Label($"Sub Tools ({subTools.Count})");
            title.AddToClassList(RuntimeConsoleUss.LabelClass);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 8;
            title.style.marginBottom = 4;
            parent.Add(title);

            foreach (var subTool in subTools.Where(tool => !tool.EditorOnly).OrderBy(tool => tool.Path, StringComparer.Ordinal))
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 5;
                parent.Add(row);

                var details = new VisualElement { style = { flexGrow = 1, minWidth = 0 } };
                row.Add(details);
                var name = new Label($"{subTool.Name} · {subTool.FunctionCount} functions");
                name.AddToClassList(RuntimeConsoleUss.LabelClass);
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                details.Add(name);
                var path = new Label("tools://" + subTool.Path);
                path.AddToClassList(RuntimeConsoleUss.LabelClass);
                path.AddToClassList(RuntimeConsoleUss.MutedLabelClass);
                details.Add(path);
                if (!string.IsNullOrWhiteSpace(subTool.Description))
                {
                    var description = new Label(subTool.Description);
                    description.AddToClassList(RuntimeConsoleUss.LabelClass);
                    description.AddToClassList(RuntimeConsoleUss.MutedLabelClass);
                    description.style.whiteSpace = WhiteSpace.Normal;
                    details.Add(description);
                }

                var enabled = RuntimeConsoleUi.CreateButton(string.Empty,
                    "Enable or disable importing and invoking this sub tool.", () =>
                    {
                        EvalToolRegistry.SetEnabled(subTool.Path, !subTool.Enabled);
                    }, 92);
                SetSwitchStyle(enabled, subTool.Enabled);
                row.Add(enabled);
            }
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

        private static void SetSwitchStyle(Button button, bool enabled)
        {
            button.text = enabled ? "●  Enabled" : "○  Disabled";
            button.style.backgroundColor = enabled ? RuntimeConsoleUi.RunningColor : DisabledColor;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 12;
            button.style.borderTopRightRadius = 12;
            button.style.borderBottomLeftRadius = 12;
            button.style.borderBottomRightRadius = 12;
        }
    }
}
