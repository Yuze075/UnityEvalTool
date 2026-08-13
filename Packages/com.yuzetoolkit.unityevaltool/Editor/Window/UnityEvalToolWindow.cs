#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal sealed class UnityEvalToolWindow : EditorWindow
    {
        private const string Endpoint = "http://127.0.0.1:2347/mcp";
        private const string StyleSheetPath = "Packages/com.yuzetoolkit.unityevaltool/Editor/Window/UnityEvalToolWindow.uss";
        private const string ToolPrefPrefix = nameof(YuzeToolkit) + ".McpTool.Enabled.";
        private const string ToolExpandedPrefPrefix = nameof(YuzeToolkit) + ".UnityEvalToolWindow.ToolExpanded.";
        private const int RefreshIntervalMilliseconds = 500;

        private static readonly Color AccentColor = new(0.32f, 0.67f, 0.98f);
        private static readonly Color RunningColor = new(0.28f, 0.76f, 0.5f);
        private static readonly Color WarningColor = new(0.98f, 0.48f, 0.2f);
        private static readonly Color StoppedColor = new(0.39f, 0.42f, 0.47f);

        private readonly HashSet<string> _expandedTools = new(StringComparer.Ordinal);
        private VisualElement _overviewRoot = null!;
        private VisualElement _toolsRoot = null!;
        private VisualElement _toolsList = null!;
        private Button _overviewTab = null!;
        private Button _toolsTab = null!;
        private Button _featureSwitch = null!;
        private Button _reconnectButton = null!;
        private Label _connectionBadge = null!;
        private Label _phaseBadge = null!;
        private Label _connection = null!;
        private Label _phase = null!;
        private Label _canEval = null!;
        private Label _busyReason = null!;
        private Label _playMode = null!;
        private Label _compilation = null!;
        private Label _compilationCycle = null!;
        private Label _lastCompilation = null!;
        private Label _instance = null!;
        private Label _connectionEpoch = null!;
        private Label _vmGeneration = null!;
        private Label _mainThread = null!;
        private Label _installation = null!;
        private TextField _toolSearch = null!;
        private Label _toolSearchPlaceholder = null!;
        private VisualElement _tooltipPopup = null!;
        private Label _tooltipText = null!;
        private bool _toolsViewDirty = true;

        private static readonly Color WindowBackground = new(0.055f, 0.059f, 0.067f);
        private static readonly Color CardBackground = new(0.086f, 0.09f, 0.102f);
        private static readonly Color BorderColor = new(0.18f, 0.19f, 0.22f);
        private static readonly Color MutedTextColor = new(0.59f, 0.61f, 0.66f);

        [MenuItem(nameof(YuzeToolkit) + "/UnityEvalTool")]
        public static void Open()
        {
            var window = GetWindow<UnityEvalToolWindow>("UnityEvalTool");
            window.minSize = new Vector2(700, 500);
            window.Show();
        }

        private void OnEnable()
        {
            EvalToolRegistry.Changed -= MarkToolsViewDirty;
            EvalToolRegistry.Changed += MarkToolsViewDirty;
        }

        private void OnDisable()
        {
            EvalToolRegistry.Changed -= MarkToolsViewDirty;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            root.AddToClassList("uet-root");
            root.style.backgroundColor = WindowBackground;
            root.style.flexDirection = FlexDirection.Column;
            root.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());

            BuildHeader(root);
            BuildTabBar(root);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 14;
            scroll.style.paddingRight = 14;
            scroll.style.paddingTop = 12;
            scroll.style.paddingBottom = 16;
            scroll.AddToClassList("uet-scroll");
            root.Add(scroll);

            _overviewRoot = new VisualElement();
            _toolsRoot = new VisualElement();
            scroll.Add(_overviewRoot);
            scroll.Add(_toolsRoot);
            BuildOverview();
            BuildToolsPage();
            SetActiveTab(false);
            BuildTooltipLayer(root);

            root.schedule.Execute(Refresh).Every(RefreshIntervalMilliseconds);
            Refresh();
        }

        private void BuildHeader(VisualElement root)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 18;
            header.style.paddingRight = 18;
            header.style.paddingTop = 14;
            header.style.paddingBottom = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = BorderColor;
            root.Add(header);

            var copy = new VisualElement { style = { flexGrow = 1, minWidth = 0 } };
            header.Add(copy);
            var title = new Label("UnityEvalTool");
            title.style.fontSize = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            copy.Add(title);

            var subtitle = new Label("One Broker connection for MCP, CLI and editor automation");
            subtitle.style.color = MutedTextColor;
            subtitle.style.marginTop = 2;
            copy.Add(subtitle);

            _connectionBadge = CreateBadge(string.Empty, StoppedColor);
            _connectionBadge.style.marginRight = 8;
            header.Add(_connectionBadge);
            _phaseBadge = CreateBadge(string.Empty, StoppedColor);
            _phaseBadge.style.marginRight = 12;
            header.Add(_phaseBadge);

            _featureSwitch = new Button(ToggleFeature);
            _featureSwitch.style.width = 112;
            _featureSwitch.style.height = 30;
            _featureSwitch.style.unityFontStyleAndWeight = FontStyle.Bold;
            _featureSwitch.style.borderTopLeftRadius = 15;
            _featureSwitch.style.borderTopRightRadius = 15;
            _featureSwitch.style.borderBottomLeftRadius = 15;
            _featureSwitch.style.borderBottomRightRadius = 15;
            _featureSwitch.AddToClassList("uet-button");
            _featureSwitch.AddToClassList("uet-switch");
            AttachTooltip(_featureSwitch, "Enable or disable this Unity process registration with the UnityEvalTool Broker.");
            header.Add(_featureSwitch);
        }

        private void BuildTabBar(VisualElement root)
        {
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.paddingLeft = 14;
            tabs.style.paddingRight = 14;
            tabs.style.paddingTop = 9;
            tabs.style.borderBottomWidth = 1;
            tabs.style.borderBottomColor = BorderColor;
            root.Add(tabs);

            _overviewTab = CreateTabButton("Overview", () => SetActiveTab(false));
            _toolsTab = CreateTabButton("Tools", () => SetActiveTab(true));
            tabs.Add(_overviewTab);
            tabs.Add(_toolsTab);
        }

        private void BuildOverview()
        {
            var intro = CreateNotice(
                "Unity connects outward to the computer-level Broker. MCP and CLI share this registration and do not open separate Unity ports.",
                "Connection model", false);
            intro.style.marginBottom = 10;
            _overviewRoot.Add(intro);

            var connectionCard = CreateCard("Connection", "Live registration and evaluation availability");
            _connection = AddField(connectionCard, "Broker connection");
            _phase = AddField(connectionCard, "Unity phase");
            _canEval = AddField(connectionCard, "Evaluation");
            _busyReason = AddField(connectionCard, "Busy reason");
            _playMode = AddField(connectionCard, "Editor state");
            _overviewRoot.Add(connectionCard);

            var controls = CreateToolbar();
            _reconnectButton = CreateButton("Reconnect", "Reconnect this Unity process to the Broker.", EditorBrokerBootstrap.Reconnect, 100);
            controls.Add(_reconnectButton);
            controls.Add(CreateButton("Copy MCP endpoint", "Copy the fixed Broker MCP endpoint.",
                () => EditorGUIUtility.systemCopyBuffer = Endpoint, 140));
            controls.Add(CreateButton("Open Broker folder", "Reveal the per-user UnityEvalTool installation folder.", OpenBrokerFolder, 140));
            connectionCard.Add(controls);

            var compilationCard = CreateCard("Compilation", "Latest compilation cycle published to the Broker");
            _compilation = AddField(compilationCard, "Result");
            _compilationCycle = AddField(compilationCard, "Cycle ID");
            _lastCompilation = AddField(compilationCard, "Last cycle");
            _overviewRoot.Add(compilationCard);

            var identityCard = CreateCard("Unity identity", "Stable process identity and reload generations");
            _instance = AddField(identityCard, "Instance ID");
            _connectionEpoch = AddField(identityCard, "Connection epoch");
            _vmGeneration = AddField(identityCard, "VM generation");
            _mainThread = AddField(identityCard, "Main thread heartbeat");
            _overviewRoot.Add(identityCard);

            var environmentCard = CreateCard("Environment", "External entry points used by agents and terminals");
            AddField(environmentCard, "MCP endpoint").text = Endpoint;
            _installation = AddField(environmentCard, "CLI installation");
            _overviewRoot.Add(environmentCard);

            var workflow = CreateNotice(
                "Agent workflow: unity_status → unity_connect → reuse handle → eval. Wait for compilation through unity_status; CompilationFailed remains executable for repair.",
                "Agent workflow", false);
            workflow.style.marginTop = 4;
            _overviewRoot.Add(workflow);
        }

        private void BuildToolsPage()
        {
            var toolbar = CreateToolbar();
            toolbar.style.marginTop = 0;
            toolbar.style.marginBottom = 10;
            _toolsRoot.Add(toolbar);

            _toolSearch = new TextField();
            _toolSearch.AddToClassList("uet-text-field");
            _toolSearch.RegisterCallback<FocusInEvent>(_ => _toolSearch.AddToClassList("uet-text-field--focus"));
            _toolSearch.RegisterCallback<FocusOutEvent>(_ => _toolSearch.RemoveFromClassList("uet-text-field--focus"));
            _toolSearch.RegisterCallback<ContextualMenuPopulateEvent>(evt => evt.StopImmediatePropagation(),
                TrickleDown.TrickleDown);
            AttachTooltip(_toolSearch, "Filter tools by name, path, source or description.");
            _toolSearch.style.flexGrow = 1;
            _toolSearch.style.minWidth = 180;
            _toolSearchPlaceholder = new Label("Filter tools…") { pickingMode = PickingMode.Ignore };
            _toolSearchPlaceholder.AddToClassList("uet-text-field__placeholder");
            _toolSearch.Add(_toolSearchPlaceholder);
            _toolSearch.RegisterValueChangedCallback(evt =>
            {
                _toolSearchPlaceholder.style.display = string.IsNullOrEmpty(evt.newValue)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                RefreshToolsView(false);
            });
            toolbar.Add(_toolSearch);
            toolbar.Add(CreateButton("Refresh registry", "Refresh C# metadata and explicitly loaded JavaScript tools.",
                () => RefreshToolsView(true), 120));

            _toolsList = new VisualElement();
            _toolsRoot.Add(_toolsList);
        }

        private void SetActiveTab(bool tools)
        {
            if (_overviewRoot == null || _toolsRoot == null) return;
            _overviewRoot.style.display = tools ? DisplayStyle.None : DisplayStyle.Flex;
            _toolsRoot.style.display = tools ? DisplayStyle.Flex : DisplayStyle.None;
            SetTabStyle(_overviewTab, !tools);
            SetTabStyle(_toolsTab, tools);
            if (tools && _toolsViewDirty)
                RefreshToolsView(false);
        }

        private void Refresh()
        {
            if (_connection == null) return;

            var enabled = EditorBrokerBootstrap.IsEnabled;
            var client = UnityBrokerClient.Shared;
            var connected = client.IsConnected;
            var running = client.IsRunning;
            var status = client.LatestStatus;
            var identity = client.Identity;

            var connectionText = !enabled ? "Disabled" : connected ? "Connected" : running ? "Reconnecting" : "Stopped";
            var connectionColor = connected ? RunningColor : enabled ? WarningColor : StoppedColor;
            _connection.text = connectionText;
            SetBadge(_connectionBadge, connectionText, connectionColor);
            _phase.text = status.Phase;
            SetBadge(_phaseBadge, status.Phase, status.CanEval ? RunningColor : WarningColor);
            _canEval.text = status.CanEval && connected
                ? string.Equals(status.Phase, "CompilationFailed", StringComparison.Ordinal) ? "Repair" : "Ready"
                : "Unavailable";
            _canEval.style.color = status.CanEval && connected ? RunningColor : WarningColor;
            _busyReason.text = string.IsNullOrWhiteSpace(status.BusyReason) ? "—" : status.BusyReason;
            _playMode.text = status.IsPlaying
                ? status.IsPaused ? "Play Mode / Paused" : "Play Mode"
                : status.IsUpdating ? "Edit Mode / Importing" : "Edit Mode";

            _compilation.text = $"{status.CompilerErrorCount} errors / {status.CompilerWarningCount} warnings";
            _compilation.style.color = status.CompilerErrorCount > 0 ? new Color(0.9f, 0.28f, 0.24f) : RunningColor;
            _compilationCycle.text = ShortId(status.CompilationCycleId);
            _lastCompilation.text = FormatCompilationTimes(status.LastCompilationStartedAtUtc, status.LastCompilationFinishedAtUtc);
            _instance.text = identity.InstanceId;
            _connectionEpoch.text = identity.ConnectionEpoch.ToString(CultureInfo.InvariantCulture);
            _vmGeneration.text = status.VmGeneration.ToString(CultureInfo.InvariantCulture);
            _mainThread.text = status.MainThreadTickAtUtc == default
                ? "No heartbeat"
                : $"tick {status.MainThreadTick} · {status.MainThreadTickAtUtc.ToLocalTime():HH:mm:ss}";
            _installation.text = GetInstallationStatus();
            _reconnectButton.SetEnabled(enabled);
            SetSwitchStyle(_featureSwitch, enabled, "Enabled", "Disabled");
        }

        private void RefreshToolsView(bool refreshMetadata)
        {
            if (_toolsList == null) return;
            _toolsViewDirty = false;
            if (refreshMetadata)
                _ = EvalToolRegistry.GetIndex(true);

            _toolsList.Clear();
            var filter = _toolSearch?.value?.Trim() ?? string.Empty;
            var tools = EvalToolRegistry.ListTools(false)
                .Where(tool => MatchesFilter(tool, filter))
                .OrderBy(tool => tool.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(tool => tool.Path, StringComparer.Ordinal)
                .ToList();

            if (tools.Count == 0)
            {
                _toolsList.Add(CreateNotice("No tools match the current filter.", "No results", true));
                return;
            }

            AddToolSection("C# Tools", tools.Where(tool => tool.Source.Equals("csharp", StringComparison.OrdinalIgnoreCase)).ToList());
            AddToolSection("JavaScript Tools", tools.Where(tool => tool.Source.Equals("js", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private void AddToolSection(string title, IReadOnlyList<EvalToolDescriptor> tools)
        {
            if (tools.Count == 0) return;
            var sectionHeader = new Label($"{title}  ·  {tools.Count}");
            sectionHeader.style.fontSize = 15;
            sectionHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionHeader.style.color = AccentColor;
            sectionHeader.style.marginTop = 3;
            sectionHeader.style.marginBottom = 7;
            _toolsList.Add(sectionHeader);
            foreach (var tool in tools)
                _toolsList.Add(CreateToolCard(tool));
        }

        private VisualElement CreateToolCard(EvalToolDescriptor tool)
        {
            var card = CreateCard(null, null);
            card.style.marginTop = 0;
            card.style.marginBottom = 8;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            card.Add(header);

            var expanded = IsToolExpanded(tool.Path);
            var expand = new Button(() =>
            {
                SetToolExpanded(tool.Path, !IsToolExpanded(tool.Path));
                RefreshToolsView(false);
            })
            {
                text = tool.Name
            };
            expand.style.flexGrow = 1;
            expand.style.unityTextAlign = TextAnchor.MiddleLeft;
            expand.style.unityFontStyleAndWeight = FontStyle.Bold;
            expand.style.fontSize = 14;
            expand.AddToClassList("uet-button");
            expand.AddToClassList("uet-tool-expand");
            expand.EnableInClassList("uet-tool-expand--open", expanded);
            var disclosure = new VisualElement { pickingMode = PickingMode.Ignore };
            disclosure.AddToClassList("uet-tool-expand__icon");
            expand.Insert(0, disclosure);
            AttachTooltip(expand, expanded ? "Collapse tool details." : "Expand tool details.");
            header.Add(expand);

            header.Add(CreatePill(tool.Source.ToUpperInvariant(), MutedTextColor));
            header.Add(CreatePill(tool.EditorOnly ? "EDITOR" : "RUNTIME", tool.EditorOnly ? AccentColor : RunningColor));
            var state = new Button(() =>
            {
                SetToolEnabled(tool.Path, !tool.Enabled);
                RefreshToolsView(false);
            });
            state.style.width = 96;
            state.style.height = 25;
            state.style.marginLeft = 8;
            state.AddToClassList("uet-button");
            state.AddToClassList("uet-switch");
            AttachTooltip(state, "Enable or disable importing and invoking this tool.");
            SetSwitchStyle(state, tool.Enabled, "Enabled", "Disabled");
            header.Add(state);

            var description = new Label(string.IsNullOrWhiteSpace(tool.Description) ? "No description." : tool.Description);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.color = MutedTextColor;
            description.style.marginLeft = 6;
            description.style.marginRight = 6;
            description.style.marginTop = 5;
            card.Add(description);

            if (!expanded) return card;

            var detail = new VisualElement();
            detail.style.marginTop = 9;
            detail.style.paddingTop = 8;
            detail.style.borderTopWidth = 1;
            detail.style.borderTopColor = BorderColor;
            card.Add(detail);
            AddField(detail, "Import path").text = "tools://" + tool.Path;
            AddField(detail, "Source").text = tool.Source;
            AddField(detail, "Availability").text = tool.EditorOnly ? "Unity Editor only" : "Editor and Player";
            AddField(detail, "Contents").text = $"{tool.Functions.Count} functions / {tool.SubTools.Count} sub tools";
            AddFunctions(detail, tool.Functions);
            AddSubTools(detail, tool.SubTools);
            return card;
        }

        private void AddFunctions(VisualElement parent, IReadOnlyList<EvalToolFunctionDescriptor> functions)
        {
            var title = CreateDetailTitle("Functions", functions.Count);
            parent.Add(title);
            if (functions.Count == 0)
            {
                parent.Add(CreateMutedLabel("No generated function metadata is available."));
                return;
            }

            foreach (var function in functions)
            {
                var row = new VisualElement();
                row.style.marginBottom = 7;
                row.style.paddingLeft = 8;
                row.style.paddingRight = 8;
                row.style.paddingTop = 6;
                row.style.paddingBottom = 6;
                row.style.backgroundColor = new Color(0.065f, 0.068f, 0.077f);
                row.style.borderTopLeftRadius = 5;
                row.style.borderTopRightRadius = 5;
                row.style.borderBottomLeftRadius = 5;
                row.style.borderBottomRightRadius = 5;

                var signature = new Label(function.MethodName + "(" + string.Join(", ", function.Parameters.Select(FormatParameter)) + ")");
                signature.style.unityFontStyleAndWeight = FontStyle.Bold;
                signature.style.whiteSpace = WhiteSpace.Normal;
                row.Add(signature);
                if (!string.IsNullOrWhiteSpace(function.Description))
                    row.Add(CreateMutedLabel(function.Description));
                if (function.Safety != EvalToolSafety.Unspecified)
                {
                    var safety = new Label($"Risk: {function.RiskLevel}" + (function.RequiresConfirmation ? " · confirmation required" : string.Empty));
                    safety.style.color = function.RequiresConfirmation ? WarningColor : MutedTextColor;
                    safety.style.marginTop = 3;
                    row.Add(safety);
                }
                foreach (var parameter in function.Parameters.Where(parameter => !string.IsNullOrWhiteSpace(parameter.Description)))
                    row.Add(CreateMutedLabel($"{parameter.Name} — {parameter.Description}"));
                parent.Add(row);
            }
        }

        private void AddSubTools(VisualElement parent, IReadOnlyList<EvalToolSummaryDescriptor> subTools)
        {
            if (subTools.Count == 0) return;
            parent.Add(CreateDetailTitle("Sub tools", subTools.Count));
            foreach (var subTool in subTools.OrderBy(value => value.Path, StringComparer.Ordinal))
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 5;
                row.style.paddingLeft = 8;
                row.style.paddingRight = 8;
                row.style.paddingTop = 5;
                row.style.paddingBottom = 5;
                row.style.borderLeftWidth = 2;
                row.style.borderLeftColor = AccentColor;

                var copy = new VisualElement { style = { flexGrow = 1, minWidth = 0 } };
                row.Add(copy);
                var name = new Label($"{subTool.Name}  ·  {subTool.FunctionCount} functions");
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                copy.Add(name);
                copy.Add(CreateMutedLabel("tools://" + subTool.Path));
                if (!string.IsNullOrWhiteSpace(subTool.Description))
                    copy.Add(CreateMutedLabel(subTool.Description));

                var state = new Button(() =>
                {
                    SetToolEnabled(subTool.Path, !subTool.Enabled);
                    RefreshToolsView(false);
                });
                state.style.width = 90;
                state.style.height = 24;
                state.AddToClassList("uet-button");
                state.AddToClassList("uet-switch");
                SetSwitchStyle(state, subTool.Enabled, "Enabled", "Disabled");
                row.Add(state);
                parent.Add(row);
            }
        }

        private VisualElement CreateCard(string? title, string? subtitle)
        {
            var card = new VisualElement();
            card.style.marginTop = 8;
            card.style.marginBottom = 4;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.backgroundColor = CardBackground;
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = BorderColor;
            card.style.borderRightColor = BorderColor;
            card.style.borderBottomColor = BorderColor;
            card.style.borderLeftColor = BorderColor;
            card.style.borderTopLeftRadius = 7;
            card.style.borderTopRightRadius = 7;
            card.style.borderBottomLeftRadius = 7;
            card.style.borderBottomRightRadius = 7;

            if (!string.IsNullOrWhiteSpace(title))
            {
                var heading = new Label(title!);
                heading.style.fontSize = 15;
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                card.Add(heading);
            }
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var hint = CreateMutedLabel(subtitle!);
                hint.style.marginBottom = 7;
                card.Add(hint);
            }
            return card;
        }

        private Label AddField(VisualElement parent, string name)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;
            parent.Add(row);
            var key = new Label(name);
            key.style.width = 150;
            key.style.flexShrink = 0;
            key.style.color = MutedTextColor;
            row.Add(key);
            var value = new Label("—");
            value.style.flexGrow = 1;
            value.style.whiteSpace = WhiteSpace.Normal;
            value.selection.isSelectable = true;
            row.Add(value);
            return value;
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginTop = 8;
            return toolbar;
        }

        private Button CreateButton(string text, string tooltip, Action action, int width)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("uet-button");
            AttachTooltip(button, tooltip);
            button.style.width = width;
            button.style.height = 32;
            button.style.marginRight = 6;
            return button;
        }

        private void BuildTooltipLayer(VisualElement root)
        {
            _tooltipPopup = new VisualElement { pickingMode = PickingMode.Ignore };
            _tooltipPopup.AddToClassList("uet-tooltip");
            _tooltipPopup.style.display = DisplayStyle.None;
            _tooltipText = new Label { pickingMode = PickingMode.Ignore };
            _tooltipText.AddToClassList("uet-tooltip__text");
            _tooltipPopup.Add(_tooltipText);
            root.Add(_tooltipPopup);
        }

        private void AttachTooltip(VisualElement target, string text)
        {
            target.tooltip = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;
            target.RegisterCallback<TooltipEvent>(evt =>
            {
                evt.tooltip = string.Empty;
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerEnterEvent>(evt => ShowTooltip(text, evt.position));
            target.RegisterCallback<PointerMoveEvent>(evt => PositionTooltip(evt.position));
            target.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            target.RegisterCallback<DetachFromPanelEvent>(_ => HideTooltip());
        }

        private void ShowTooltip(string text, Vector2 position)
        {
            if (_tooltipPopup == null || _tooltipText == null) return;
            _tooltipText.text = text;
            _tooltipPopup.style.display = DisplayStyle.Flex;
            PositionTooltip(position);
        }

        private void PositionTooltip(Vector2 position)
        {
            if (_tooltipPopup == null || _tooltipPopup.style.display == DisplayStyle.None) return;
            var maxLeft = Mathf.Max(8f, rootVisualElement.resolvedStyle.width - 328f);
            var maxTop = Mathf.Max(8f, rootVisualElement.resolvedStyle.height - 74f);
            _tooltipPopup.style.left = Mathf.Clamp(position.x + 12f, 8f, maxLeft);
            _tooltipPopup.style.top = Mathf.Clamp(position.y + 16f, 8f, maxTop);
        }

        private void HideTooltip()
        {
            if (_tooltipPopup != null)
                _tooltipPopup.style.display = DisplayStyle.None;
        }

        private Button CreateTabButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("uet-button");
            button.AddToClassList("uet-tab");
            button.style.width = 112;
            button.style.height = 28;
            button.style.marginRight = 6;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 0;
            button.style.borderBottomRightRadius = 0;
            return button;
        }

        private void SetTabStyle(Button button, bool active)
        {
            if (button == null) return;
            button.EnableInClassList("uet-tab--active", active);
            button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
        }

        private static Label CreateBadge(string text, Color color)
        {
            var badge = new Label(text);
            badge.style.height = 23;
            badge.style.paddingLeft = 9;
            badge.style.paddingRight = 9;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.borderTopLeftRadius = 12;
            badge.style.borderTopRightRadius = 12;
            badge.style.borderBottomLeftRadius = 12;
            badge.style.borderBottomRightRadius = 12;
            SetBadge(badge, text, color);
            return badge;
        }

        private static void SetBadge(Label badge, string text, Color color)
        {
            badge.text = text;
            badge.style.color = Color.white;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.85f);
        }

        private static Label CreatePill(string text, Color color)
        {
            var pill = new Label(text);
            pill.style.height = 20;
            pill.style.paddingLeft = 7;
            pill.style.paddingRight = 7;
            pill.style.marginLeft = 5;
            pill.style.unityTextAlign = TextAnchor.MiddleCenter;
            pill.style.fontSize = 11;
            pill.style.color = Color.white;
            pill.style.backgroundColor = new Color(color.r, color.g, color.b, 0.72f);
            pill.style.borderTopLeftRadius = 10;
            pill.style.borderTopRightRadius = 10;
            pill.style.borderBottomLeftRadius = 10;
            pill.style.borderBottomRightRadius = 10;
            return pill;
        }

        private Label CreateMutedLabel(string text)
        {
            var label = new Label(text);
            label.style.color = MutedTextColor;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 2;
            return label;
        }

        private Label CreateDetailTitle(string title, int count)
        {
            var label = new Label($"{title}  ·  {count}");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = AccentColor;
            label.style.marginTop = 10;
            label.style.marginBottom = 5;
            return label;
        }

        private static void SetSwitchStyle(Button button, bool enabled, string enabledText, string disabledText)
        {
            button.text = enabled ? enabledText : disabledText;
            button.EnableInClassList("uet-switch--on", enabled);
            button.EnableInClassList("uet-switch--off", !enabled);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 13;
            button.style.borderTopRightRadius = 13;
            button.style.borderBottomLeftRadius = 13;
            button.style.borderBottomRightRadius = 13;
        }

        private VisualElement CreateNotice(string message, string title, bool warning)
        {
            var notice = new VisualElement();
            notice.AddToClassList("uet-notice");
            notice.EnableInClassList("uet-notice--warning", warning);

            var heading = new Label(title);
            heading.AddToClassList("uet-notice__title");
            notice.Add(heading);

            var body = new Label(message);
            body.AddToClassList("uet-notice__body");
            notice.Add(body);
            return notice;
        }

        private void ToggleFeature()
        {
            EditorBrokerBootstrap.SetEnabled(!EditorBrokerBootstrap.IsEnabled);
            Refresh();
        }

        private static void SetToolEnabled(string path, bool enabled)
        {
            EditorPrefs.SetBool(ToolPrefPrefix + path, enabled);
            EvalToolRegistry.SetEnabled(path, enabled);
        }

        private bool IsToolExpanded(string path)
        {
            if (_expandedTools.Contains(path)) return true;
            if (!EditorPrefs.GetBool(ToolExpandedPrefPrefix + path, false)) return false;
            _expandedTools.Add(path);
            return true;
        }

        private void SetToolExpanded(string path, bool expanded)
        {
            if (expanded) _expandedTools.Add(path);
            else _expandedTools.Remove(path);
            EditorPrefs.SetBool(ToolExpandedPrefPrefix + path, expanded);
        }

        private void MarkToolsViewDirty()
        {
            _toolsViewDirty = true;
        }

        private static bool MatchesFilter(EvalToolDescriptor tool, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            return tool.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tool.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tool.Source.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tool.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tool.SubTools.Any(subTool =>
                       subTool.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       subTool.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       subTool.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
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

        private static string FormatCompilationTimes(string startedAt, string finishedAt)
        {
            var started = ParseLocalTime(startedAt);
            var finished = ParseLocalTime(finishedAt);
            if (started == "—" && finished == "—") return "No compilation recorded";
            return $"started {started} / finished {finished}";
        }

        private static string ParseLocalTime(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime)
                ? dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : "—";
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "—";
            return value.Length <= 12 ? value : value.Substring(0, 12) + "…";
        }

        private static void OpenBrokerFolder()
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unityevaltool");
            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory);
        }

        private static string GetInstallationStatus()
        {
            var metadata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unityevaltool", "install.json");
            if (!File.Exists(metadata)) return "Not installed · run the npm-installed `unity` command once";
            try
            {
                var root = EvalData.AsObject(LitJson.Parse(File.ReadAllText(metadata)));
                var executable = root == null ? string.Empty : EvalData.GetString(root, "executablePath") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(executable)) return "Invalid install metadata · executablePath is missing";
                return File.Exists(executable) ? executable : "Installed executable is missing · " + executable;
            }
            catch (Exception ex)
            {
                return "Invalid install metadata · " + ex.Message;
            }
        }
    }
}
