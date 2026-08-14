#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit.UnityAgent
{
    internal enum AgentWorkspacePage
    {
        Conversation,
        CommandLine,
        DebugPanel,
        Log,
        SystemInfo
    }

    internal static class AgentWorkspaceUi
    {
        public static VisualElement Header(string title, string subtitle = "")
        {
            var header = new VisualElement();
            header.style.height = string.IsNullOrWhiteSpace(subtitle) ? 54 : 66;
            header.style.flexShrink = 0;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 20;
            header.style.paddingRight = 20;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = AgentUi.Border;
            var copy = new VisualElement { style = { flexGrow = 1, minWidth = 0 } };
            var heading = new Label(title);
            AgentUi.ApplyTypography(heading, AgentTypography.PageTitle);
            copy.Add(heading);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var help = new Label(subtitle);
                AgentUi.ApplyTypography(help, AgentTypography.Caption);
                help.style.color = AgentUi.Muted;
                copy.Add(help);
            }
            header.Add(copy);
            return header;
        }

        public static Label Empty(string text)
        {
            var label = new Label(text);
            AgentUi.ApplyTypography(label, AgentTypography.Body);
            label.style.color = AgentUi.Muted;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingTop = 48;
            label.style.paddingLeft = 20;
            label.style.paddingRight = 20;
            return label;
        }
    }

    internal sealed class AgentDebugWorkspaceView : VisualElement, IDisposable
    {
        private readonly VisualElement _tabs;
        private readonly VisualElement _content;
        private readonly Dictionary<DebugWindowRegistration, AgentButton> _buttons = new();
        private readonly Dictionary<DebugWindowRegistration, DebugWindowVisualInstance> _visuals = new();
        private readonly List<DebugWindowRegistration> _registrations = new();
        private DebugWindowRegistration? _active;
        private int _revision = -1;

        public AgentDebugWorkspaceView()
        {
            style.flexGrow = 1;
            style.minWidth = 0;
            style.minHeight = 0;
            Add(AgentWorkspaceUi.Header("Debug Panel", "Every registered DebugWindow is available as an independent tab."));
            _tabs = new VisualElement();
            _tabs.style.height = 44;
            _tabs.style.flexShrink = 0;
            _tabs.style.flexDirection = FlexDirection.Row;
            _tabs.style.alignItems = Align.Center;
            _tabs.style.paddingLeft = 14;
            _tabs.style.paddingRight = 14;
            _tabs.style.borderBottomWidth = 1;
            _tabs.style.borderBottomColor = AgentUi.Border;
            Add(_tabs);
            _content = new VisualElement { style = { flexGrow = 1, minHeight = 0, minWidth = 0 } };
            Add(_content);
            Rebuild();
        }

        public void Tick()
        {
            if (_revision != DebugWindowRegistry.Revision) Rebuild();
            foreach (var visual in _visuals.Values) visual.Refresh();
        }

        public void Dispose()
        {
            foreach (var visual in _visuals.Values) visual.Dispose();
            _registrations.Clear();
            _buttons.Clear();
            _visuals.Clear();
        }

        private void Rebuild()
        {
            var preferred = _active?.Title;
            foreach (var visual in _visuals.Values) visual.Dispose();
            _registrations.Clear();
            _buttons.Clear();
            _visuals.Clear();
            _tabs.Clear();
            _content.Clear();
            _active = null;
            _revision = DebugWindowRegistry.Revision;
            foreach (var registration in DebugWindowRegistry.RegisteredWindows)
            {
                var visual = registration.CreateVisualElement(false);
                _registrations.Add(registration);
                _visuals.Add(registration, visual);
                _content.Add(visual.VisualElement);
                var captured = registration;
                var button = AgentUi.Button(registration.Title, "Open this Debug Panel page.",
                    () => Select(captured), 0, AgentUi.Transparent, AgentUi.TextSecondary);
                button.style.height = 32;
                button.style.flexGrow = 0;
                button.style.marginRight = 6;
                _buttons.Add(registration, button);
                _tabs.Add(button);
            }
            if (_registrations.Count == 0)
            {
                _content.Add(AgentWorkspaceUi.Empty(
                    "No Debug Panel pages are registered. Pages appear here as soon as game systems register a DebugWindow."));
                return;
            }
            Select(_registrations.FirstOrDefault(value => value.Title == preferred) ?? _registrations[0]);
        }

        private void Select(DebugWindowRegistration registration)
        {
            _active = registration;
            foreach (var value in _registrations)
            {
                _visuals[value].VisualElement.style.display = ReferenceEquals(value, registration)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _buttons[value].SetPalette(ReferenceEquals(value, registration) ? AgentUi.Active : AgentUi.Transparent,
                    ReferenceEquals(value, registration) ? AgentUi.Accent : AgentUi.TextSecondary);
            }
        }
    }

    internal sealed class AgentSystemInfoWorkspaceView : VisualElement, IDisposable
    {
        private readonly VisualElement _sections;
        private readonly List<IUnityAgentWorkspaceSection> _liveSections = new();
        private int _revision = -1;

        public AgentSystemInfoWorkspaceView()
        {
            style.flexGrow = 1;
            style.minHeight = 0;
            style.minWidth = 0;
            Add(AgentWorkspaceUi.Header("System Info",
                "Live performance metrics are shown first, followed by system details."));
            var scroll = AgentUi.Scroll(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            _sections = scroll.contentContainer;
            _sections.style.paddingLeft = 20;
            _sections.style.paddingRight = 20;
            _sections.style.paddingTop = 16;
            _sections.style.paddingBottom = 20;
            _sections.style.alignItems = Align.FlexStart;
            Add(scroll);
            Rebuild();
        }

        public void Tick()
        {
            if (_revision != UnityAgentWorkspaceRegistry.Revision) Rebuild();
            foreach (var section in _liveSections) section.Tick();
        }

        public void Dispose() => ClearSections();

        private void Rebuild()
        {
            ClearSections();
            _revision = UnityAgentWorkspaceRegistry.Revision;
            foreach (var section in UnityAgentWorkspaceRegistry.CreateSystemInfoSections())
            {
                _liveSections.Add(section);
                section.Root.style.position = Position.Relative;
                section.Root.style.left = StyleKeyword.Auto;
                section.Root.style.right = StyleKeyword.Auto;
                section.Root.style.top = StyleKeyword.Auto;
                section.Root.style.bottom = StyleKeyword.Auto;
                section.Root.style.marginBottom = 16;
                _sections.Add(section.Root);
            }
            if (_liveSections.Count == 0)
                _sections.Add(AgentWorkspaceUi.Empty(
                    "System Info is waiting for the AgentTool SystemInfo and Performance modules to initialize."));
        }

        private void ClearSections()
        {
            for (var index = _liveSections.Count - 1; index >= 0; index--)
                _liveSections[index].Dispose();
            _liveSections.Clear();
            _sections.Clear();
        }
    }

    internal sealed class AgentLogWorkspaceView : VisualElement, IDisposable
    {
        private readonly RuntimeLogStore _store = new();
        private readonly ScrollView _list;
        private readonly Label _detail;
        private readonly AgentTextField _search;
        private readonly Dictionary<LogType, bool> _enabled = new();
        private readonly Dictionary<LogType, AgentButton> _filterButtons = new();
        private bool _groupRepeats;
        private bool _autoScroll = true;
        private bool _dirty = true;
        private DebugLogEntry? _selected;

        public AgentLogWorkspaceView()
        {
            style.flexGrow = 1;
            style.minWidth = 0;
            style.minHeight = 0;
            Add(AgentWorkspaceUi.Header("Log", "A runtime Console view backed by Unity's live log stream."));
            var toolbar = AgentUi.WrapRow();
            toolbar.style.flexShrink = 0;
            toolbar.style.minHeight = 46;
            toolbar.style.paddingLeft = 12;
            toolbar.style.paddingRight = 12;
            toolbar.style.paddingTop = 7;
            toolbar.style.paddingBottom = 7;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = AgentUi.Border;
            toolbar.Add(AgentUi.Button("Clear", "Clear captured logs.", ClearLogs, 64, AgentUi.Surface3));
            toolbar.Add(CreateToggle("Group", "Group identical message and stack trace pairs.",
                () => _groupRepeats, value => { _groupRepeats = value; _dirty = true; }));
            toolbar.Add(CreateToggle("Auto-scroll", "Keep the newest visible log at the bottom.",
                () => _autoScroll, value => _autoScroll = value));
            _search = new AgentTextField { Placeholder = "Search message or stack trace…" };
            _search.style.width = 220;
            _search.style.minWidth = 120;
            _search.style.flexGrow = 1;
            _search.RegisterValueChangedCallback(_ => _dirty = true);
            toolbar.Add(_search);
            AddFilter(toolbar, LogType.Log, "Log");
            AddFilter(toolbar, LogType.Warning, "Warn");
            AddFilter(toolbar, LogType.Error, "Error");
            var stack = AgentUi.CompactDropdown(Enum.GetNames(typeof(StackTraceLogType)), "Unity Stack Trace level");
            stack.style.width = 118;
            stack.SetValueWithoutNotify(Application.GetStackTraceLogType(LogType.Log).ToString());
            stack.RegisterValueChangedCallback(evt => SetStackTraceLevel(evt.newValue));
            toolbar.Add(stack);
            toolbar.Add(AgentUi.Button("Log file", "Open or reveal Unity's local log file.", OpenLogFile, 82,
                AgentUi.Surface3));
            Add(toolbar);

            var split = new VisualElement { style = { flexGrow = 1, minHeight = 0, minWidth = 0 } };
            _list = AgentUi.Scroll(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1;
            _list.style.minHeight = 100;
            split.Add(_list);
            _detail = new Label("Select a log entry to inspect its full message and stack trace.");
            _detail.enableRichText = false;
            _detail.style.height = 156;
            _detail.style.flexShrink = 0;
            _detail.style.whiteSpace = WhiteSpace.Normal;
            _detail.style.paddingLeft = 14;
            _detail.style.paddingRight = 14;
            _detail.style.paddingTop = 10;
            _detail.style.paddingBottom = 10;
            _detail.style.color = AgentUi.TextSecondary;
            _detail.style.backgroundColor = AgentUi.PanelInset;
            _detail.style.borderTopWidth = 1;
            _detail.style.borderTopColor = AgentUi.Border;
            split.Add(_detail);
            Add(split);
            _store.Subscribe();
        }

        public void Tick()
        {
            if (_store.Pump()) _dirty = true;
            if (_dirty) Rebuild();
        }

        public void Dispose() => _store.Dispose();

        private AgentButton CreateToggle(string text, string help, Func<bool> get, Action<bool> set)
        {
            AgentButton? button = null;
            void Sync()
            {
                var active = get();
                button!.SetPalette(active ? AgentUi.Active : AgentUi.Surface3,
                    active ? AgentUi.Accent : AgentUi.TextSecondary);
            }
            button = AgentUi.Button(text, help, () => { set(!get()); Sync(); }, 0, AgentUi.Surface3);
            button.style.flexGrow = 0;
            Sync();
            return button;
        }

        private void AddFilter(VisualElement toolbar, LogType type, string text)
        {
            _enabled[type] = true;
            var button = CreateToggle(text, $"Show {text} entries.", () => _enabled[type], value =>
            {
                _enabled[type] = value;
                _dirty = true;
            });
            _filterButtons[type] = button;
            toolbar.Add(button);
        }

        private void ClearLogs()
        {
            _store.Clear();
            _selected = null;
            _detail.text = "Select a log entry to inspect its full message and stack trace.";
            _dirty = true;
        }

        private void Rebuild()
        {
            _dirty = false;
            _list.Clear();
            var entries = _store.Entries.Where(Matches).ToList();
            IEnumerable<(DebugLogEntry Entry, int Count)> rows = entries.Select(value => (value, 1));
            if (_groupRepeats)
                rows = entries.GroupBy(value => $"{value.Type}\n{value.Message}\n{value.StackTrace}",
                        StringComparer.Ordinal)
                    .Select(group => (group.Last(), group.Count()));
            foreach (var row in rows) _list.Add(CreateRow(row.Entry, row.Count));
            UpdateFilterLabels();
            if (_autoScroll)
                _list.schedule.Execute(() => _list.scrollOffset =
                    new Vector2(_list.scrollOffset.x, _list.contentContainer.layout.height));
        }

        private VisualElement CreateRow(DebugLogEntry entry, int count)
        {
            var row = new VisualElement();
            row.style.minHeight = 34;
            row.style.flexShrink = 0;
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            row.style.paddingTop = 7;
            row.style.paddingBottom = 7;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = AgentUi.Border;
            row.style.backgroundColor = ReferenceEquals(entry, _selected) ? AgentUi.Selected : AgentUi.Transparent;
            var label = new Label($"[{entry.Time:HH:mm:ss}] {entry.Message}{(count > 1 ? $"  ×{count}" : string.Empty)}");
            label.enableRichText = false;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.color = entry.Type == LogType.Warning ? AgentUi.Warning : IsError(entry) ? AgentUi.Error : AgentUi.Text;
            row.Add(label);
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _selected = entry;
                _detail.text = FormatDetail(entry);
                _dirty = true;
                if (evt.clickCount >= 2) OpenSource(entry);
            });
            return row;
        }

        private bool Matches(DebugLogEntry entry)
        {
            if (entry.Type == LogType.Log && !_enabled[LogType.Log]) return false;
            if (entry.Type == LogType.Warning && !_enabled[LogType.Warning]) return false;
            if (IsError(entry) && !_enabled[LogType.Error]) return false;
            var search = _search.value;
            return string.IsNullOrWhiteSpace(search) ||
                   entry.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entry.StackTrace.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateFilterLabels()
        {
            _filterButtons[LogType.Log].text = "Log " + _store.Entries.Count(value => value.Type == LogType.Log);
            _filterButtons[LogType.Warning].text = "Warn " + _store.Entries.Count(value => value.Type == LogType.Warning);
            _filterButtons[LogType.Error].text = "Error " + _store.Entries.Count(IsError);
        }

        private static bool IsError(DebugLogEntry entry) =>
            entry.Type is LogType.Error or LogType.Exception or LogType.Assert;

        private static string FormatDetail(DebugLogEntry entry) => string.IsNullOrWhiteSpace(entry.StackTrace)
            ? entry.Message
            : entry.Message + Environment.NewLine + entry.StackTrace;

        private static void SetStackTraceLevel(string value)
        {
            if (!Enum.TryParse(value, out StackTraceLogType level)) return;
            foreach (var type in new[] { LogType.Log, LogType.Warning, LogType.Error, LogType.Assert, LogType.Exception })
                Application.SetStackTraceLogType(type, level);
        }

        private static void OpenSource(DebugLogEntry entry)
        {
#if UNITY_EDITOR
            if (TryParseSource(entry.StackTrace, out var path, out var line))
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, line);
            else
                GUIUtility.systemCopyBuffer = FormatDetail(entry);
#else
            GUIUtility.systemCopyBuffer = FormatDetail(entry);
#endif
        }

        private static bool TryParseSource(string stackTrace, out string path, out int line)
        {
            path = string.Empty;
            line = 0;
            if (string.IsNullOrWhiteSpace(stackTrace)) return false;
            var marker = stackTrace.IndexOf("(at ", StringComparison.Ordinal);
            if (marker < 0) return false;
            var end = stackTrace.IndexOf(')', marker + 4);
            if (end < 0) return false;
            var location = stackTrace.Substring(marker + 4, end - marker - 4);
            var separator = location.LastIndexOf(':');
            if (separator <= 0 || !int.TryParse(location.Substring(separator + 1), out line)) return false;
            path = location.Substring(0, separator);
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(AgentPaths.ProjectRoot, path));
            return File.Exists(path);
        }

        private static void OpenLogFile()
        {
            var path = Application.consoleLogPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Unity's local log file is not available.", path);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#else
            Application.OpenURL(new Uri(path).AbsoluteUri);
#endif
        }
    }

    internal sealed class AgentCommandLineWorkspaceView : VisualElement, IDisposable
    {
        private readonly VisualElement _sidebarList;
        private readonly Action _activate;
        private readonly ScrollView _history;
        private readonly AgentTextField _input;
        private readonly AgentButton _run;
        private readonly CommandLineStore _store;
        private readonly Dictionary<string, RuntimeCliRunner> _runners = new(StringComparer.Ordinal);
        private readonly List<CommandLineSessionDocument> _sessions;
        private CommandLineSessionDocument _pendingDocument = CreateDocument();
        private IVisualElementScheduledItem? _draftSaveItem;
        private CommandLineSessionDocument _selected;
        private bool _running;

        public AgentCommandLineWorkspaceView(UnityAgentHost host, VisualElement sidebarList, Action activate)
        {
            _sidebarList = sidebarList;
            _activate = activate;
            style.flexGrow = 1;
            style.minWidth = 0;
            style.minHeight = 0;
            _store = new CommandLineStore();
            _sessions = _store.Load();
            _selected = _sessions.FirstOrDefault(value => !value.IsArchived && value.Id == _store.LoadSelectedId()) ??
                        _sessions.Where(value => !value.IsArchived)
                            .OrderByDescending(value => value.IsPinned)
                            .ThenByDescending(value => value.UpdatedAtUtc).FirstOrDefault() ?? _pendingDocument;

            Add(AgentWorkspaceUi.Header("Command Line",
                "Transcripts persist beside Agent history; each JavaScript VM exists only for this Unity process."));
            _history = AgentUi.Scroll(ScrollViewMode.Vertical);
            _history.style.flexGrow = 1;
            _history.style.minHeight = 0;
            _history.contentContainer.style.paddingLeft = 20;
            _history.contentContainer.style.paddingRight = 20;
            _history.contentContainer.style.paddingTop = 16;
            _history.contentContainer.style.paddingBottom = 16;
            Add(_history);
            var composer = AgentUi.RoundedPanel(18);
            composer.style.flexShrink = 0;
            composer.style.marginLeft = 16;
            composer.style.marginRight = 16;
            composer.style.marginBottom = 12;
            composer.style.paddingLeft = 12;
            composer.style.paddingRight = 8;
            composer.style.paddingTop = 8;
            composer.style.paddingBottom = 8;
            composer.style.flexDirection = FlexDirection.Row;
            composer.style.alignItems = Align.Center;
            composer.style.backgroundColor = AgentUi.Composer;
            AgentUi.SetBorder(composer, AgentUi.Border1, 1);
            _input = new AgentTextField(surface: false) { Placeholder = "Enter one UnityEvalTool command…" };
            _input.style.flexGrow = 1;
            _input.style.minWidth = 0;
            _input.SetValueWithoutNotify(_selected.Draft);
            _input.RegisterValueChangedCallback(evt =>
            {
                _selected.Draft = evt.newValue;
                _draftSaveItem?.Pause();
                if (_sessions.Contains(_selected))
                    _draftSaveItem = schedule.Execute(SaveSelectedDraft).StartingIn(350);
            });
            _input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return || evt.shiftKey) return;
                _ = ExecuteAsync();
                evt.StopPropagation();
            });
            composer.Add(_input);
            _run = AgentUi.Button("Run", "Execute this command in the selected live VM.",
                () => _ = ExecuteAsync(), 72, AgentUi.Accent, AgentUi.AccentForeground, AgentIconKind.Send);
            composer.Add(_run);
            Add(composer);
            RefreshAll();
        }

        public void Tick() { }

        public void CreateSession()
        {
            SaveSelectedDraft();
            _selected = _pendingDocument;
            _input.SetValueWithoutNotify(_selected.Draft);
            RefreshAll();
        }

        public void Dispose()
        {
            SaveSelectedDraft();
            foreach (var runner in _runners.Values) runner.Dispose();
            _runners.Clear();
        }

        private async Task ExecuteAsync()
        {
            var command = _input.value.Trim();
            if (_running || command.Length == 0) return;
            _running = true;
            _run.SetEnabled(false);
            _input.SetEnabled(false);
            _selected.Draft = string.Empty;
            _input.SetValueWithoutNotify(string.Empty);
            if (!_sessions.Contains(_selected))
            {
                _sessions.Add(_selected);
                if (ReferenceEquals(_selected, _pendingDocument)) _pendingDocument = CreateDocument();
            }
            var entry = new CommandLineEntry(DateTime.UtcNow, true, command, string.Empty, LogType.Log);
            _selected.Entries.Add(entry);
            if (_selected.Title == "New command line")
                _selected.Title = command.Length <= 42 ? command : command.Substring(0, 39) + "...";
            _selected.UpdatedAtUtc = DateTime.UtcNow;
            Persist();
            RefreshHistory();
            try
            {
                if (!_runners.TryGetValue(_selected.Id, out var runner))
                {
                    runner = new RuntimeCliRunner(_selected.Id);
                    runner.Start();
                    _runners.Add(_selected.Id, runner);
                }
                var output = await runner.ExecuteLineAsync(command);
                _selected.Entries.Add(new CommandLineEntry(DateTime.UtcNow, false,
                    output.Message, output.StackTrace, output.LogType));
                _selected.UpdatedAtUtc = DateTime.UtcNow;
                Persist();
            }
            catch (Exception exception)
            {
                _selected.Entries.Add(new CommandLineEntry(DateTime.UtcNow, false,
                    exception.Message, exception.ToString(), LogType.Exception));
                _selected.UpdatedAtUtc = DateTime.UtcNow;
                Persist();
            }
            finally
            {
                _running = false;
                _run.SetEnabled(true);
                _input.SetEnabled(true);
                RefreshAll();
            }
        }

        private void RefreshAll()
        {
            RefreshSidebar();
            RefreshHistory();
        }

        private void RefreshSidebar()
        {
            _sidebarList.Clear();
            foreach (var session in _sessions.Where(value => !value.IsArchived)
                         .OrderByDescending(value => value.IsPinned)
                         .ThenBy(value => value.SortOrder)
                         .ThenByDescending(value => value.UpdatedAtUtc))
            {
                var item = new VisualElement();
                item.style.minHeight = 36;
                item.style.flexShrink = 0;
                item.style.flexDirection = FlexDirection.Row;
                item.style.alignItems = Align.Center;
                item.style.paddingLeft = 9;
                item.style.paddingRight = 4;
                item.style.marginBottom = 3;
                item.style.borderTopLeftRadius = 8;
                item.style.borderTopRightRadius = 8;
                item.style.borderBottomLeftRadius = 8;
                item.style.borderBottomRightRadius = 8;
                item.style.backgroundColor = session.Id == _selected.Id ? AgentUi.Selected : AgentUi.Transparent;
                var label = new Label(session.Title);
                label.style.flexGrow = 1;
                label.style.minWidth = 0;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                item.Add(label);
                item.Add(AgentUi.IconButton(AgentIconKind.Pin, session.IsPinned ? "Unpin" : "Pin",
                    () => SetOrganization(session, !session.IsPinned, false), 24,
                    AgentUi.Transparent, AgentUi.Muted));
                item.Add(AgentUi.IconButton(AgentIconKind.Archive, "Archive",
                    () => SetOrganization(session, session.IsPinned, true), 24,
                    AgentUi.Transparent, AgentUi.Muted));
                item.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    SaveSelectedDraft();
                    _activate();
                    _selected = session;
                    _input.SetValueWithoutNotify(session.Draft);
                    _store.SaveSelectedId(session.Id);
                    RefreshAll();
                });
                item.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button != 1) return;
                    AgentPopupMenu.Show(item, new[]
                    {
                        new AgentMenuItem(session.IsPinned ? "Unpin" : "Pin",
                            () => SetOrganization(session, !session.IsPinned, false)),
                        new AgentMenuItem("Archive", () => SetOrganization(session, session.IsPinned, true)),
                        new AgentMenuItem("Delete command line…", () => Delete(session),
                            dangerous: true, separatorBefore: true)
                    }, 230);
                    evt.StopPropagation();
                });
                _sidebarList.Add(item);
            }
        }

        private void RefreshHistory()
        {
            _history.Clear();
            if (_selected.Entries.Count == 0)
            {
                _history.Add(AgentWorkspaceUi.Empty(
                    "Run a Tool command, eval-js expression, or type help. Persisted output appears here."));
                return;
            }
            foreach (var entry in _selected.Entries)
            {
                var row = AgentUi.RoundedPanel(10);
                row.style.marginBottom = 8;
                row.style.paddingLeft = 12;
                row.style.paddingRight = 12;
                row.style.paddingTop = 9;
                row.style.paddingBottom = 9;
                row.style.backgroundColor = entry.IsInput ? AgentUi.Surface2 : AgentUi.PanelInset;
                var prefix = new Label(entry.IsInput ? "> COMMAND" : entry.Type.ToString().ToUpperInvariant());
                AgentUi.ApplyTypography(prefix, AgentTypography.Caption);
                prefix.style.color = entry.IsInput ? AgentUi.Accent :
                    entry.Type == LogType.Warning ? AgentUi.Warning :
                    entry.Type is LogType.Error or LogType.Exception or LogType.Assert ? AgentUi.Error : AgentUi.Muted;
                row.Add(prefix);
                var text = new Label(string.IsNullOrWhiteSpace(entry.StackTrace)
                    ? entry.Text
                    : entry.Text + Environment.NewLine + entry.StackTrace);
                text.enableRichText = false;
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.marginTop = 4;
                row.Add(text);
                _history.Add(row);
            }
            _history.schedule.Execute(() => _history.scrollOffset =
                new Vector2(_history.scrollOffset.x, _history.contentContainer.layout.height));
        }

        private void Delete(CommandLineSessionDocument session)
        {
            _sessions.Remove(session);
            _store.Delete(session.Id);
            if (_runners.Remove(session.Id, out var runner)) runner.Dispose();
            if (ReferenceEquals(_selected, session))
                _selected = _sessions.Where(value => !value.IsArchived)
                    .OrderByDescending(value => value.IsPinned)
                    .ThenByDescending(value => value.UpdatedAtUtc).FirstOrDefault() ?? _pendingDocument;
            _input.SetValueWithoutNotify(_selected.Draft);
            if (_sessions.Contains(_selected)) _store.SaveSelectedId(_selected.Id);
            RefreshAll();
        }

        private void SetOrganization(CommandLineSessionDocument session, bool pinned, bool archived)
        {
            session.IsPinned = pinned;
            session.IsArchived = archived;
            session.UpdatedAtUtc = DateTime.UtcNow;
            _store.Save(session);
            if (archived && ReferenceEquals(_selected, session))
            {
                _selected = _sessions.Where(value => !value.IsArchived && !ReferenceEquals(value, session))
                    .OrderByDescending(value => value.IsPinned)
                    .ThenByDescending(value => value.UpdatedAtUtc).FirstOrDefault() ?? _pendingDocument;
                _input.SetValueWithoutNotify(_selected.Draft);
                _store.SaveSelectedId(_sessions.Contains(_selected) ? _selected.Id : string.Empty);
            }
            RefreshAll();
        }

        private void SaveSelectedDraft()
        {
            _draftSaveItem?.Pause();
            _selected.Draft = _input.value;
            if (_sessions.Contains(_selected)) _store.Save(_selected);
        }

        private void Persist()
        {
            _store.Save(_selected);
            _store.SaveSelectedId(_selected.Id);
        }

        private static CommandLineSessionDocument CreateDocument() => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "New command line",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    internal sealed class CommandLineSessionDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = "New command line";
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public bool IsPinned { get; set; }
        public bool IsArchived { get; set; }
        public int SortOrder { get; set; }
        public string Draft { get; set; } = string.Empty;
        public List<CommandLineEntry> Entries { get; } = new();
    }

    internal readonly struct CommandLineEntry
    {
        public CommandLineEntry(DateTime timeUtc, bool isInput, string text, string stackTrace, LogType type)
        {
            TimeUtc = timeUtc;
            IsInput = isInput;
            Text = text;
            StackTrace = stackTrace;
            Type = type;
        }

        public DateTime TimeUtc { get; }
        public bool IsInput { get; }
        public string Text { get; }
        public string StackTrace { get; }
        public LogType Type { get; }
    }

    internal sealed class CommandLineStore
    {
        private const int SchemaVersion = 2;
        private readonly string _root;

        public CommandLineStore()
        {
            _root = Path.Combine(AgentPaths.SettingsRoot, AgentPaths.CommandLineHistoryFolderName);
            MigrateLegacyDirectory();
        }

        private void MigrateLegacyDirectory()
        {
            var legacy = Path.Combine(AgentPaths.LegacySettingsRoot,
                AgentPaths.CommandLineHistoryFolderName);
            if (!Directory.Exists(legacy) || AgentPaths.PathsEqual(legacy, _root)) return;
            Directory.CreateDirectory(_root);
            foreach (var source in Directory.EnumerateFiles(legacy, "*.json", SearchOption.TopDirectoryOnly))
            {
                var destination = Path.Combine(_root, Path.GetFileName(source));
                if (!File.Exists(destination)) File.Copy(source, destination);
            }
        }

        public List<CommandLineSessionDocument> Load()
        {
            if (!Directory.Exists(_root)) return new List<CommandLineSessionDocument>();
            var result = new List<CommandLineSessionDocument>();
            foreach (var path in Directory.EnumerateFiles(_root, "*.json")
                         .Where(value => !value.EndsWith("state.json", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                var root = AgentJson.ParseObject(File.ReadAllText(path));
                if (AgentJson.GetSchemaVersion(root) > SchemaVersion)
                    throw new FormatException($"Command Line document is newer than this build: {path}");
                var document = new CommandLineSessionDocument
                {
                    Id = AgentJson.GetString(root, "id"),
                    Title = AgentJson.GetString(root, "title", "New command line"),
                    CreatedAtUtc = AgentJson.GetDateTime(root, "createdAtUtc", DateTime.UtcNow),
                    UpdatedAtUtc = AgentJson.GetDateTime(root, "updatedAtUtc", DateTime.UtcNow),
                    IsPinned = EvalData.GetBool(root, "isPinned"),
                    IsArchived = EvalData.GetBool(root, "isArchived"),
                    SortOrder = Math.Max(0, EvalData.GetInt(root, "sortOrder")),
                    Draft = AgentJson.GetString(root, "draft")
                };
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), document.Id, StringComparison.Ordinal))
                    throw new FormatException($"Command Line file name and document id do not match: {path}");
                foreach (var value in AgentJson.GetObjectArray(root, "entries"))
                    document.Entries.Add(new CommandLineEntry(
                        AgentJson.GetDateTime(value, "timeUtc", DateTime.UtcNow),
                        EvalData.GetBool(value, "isInput"),
                        AgentJson.GetString(value, "text"),
                        AgentJson.GetString(value, "stackTrace"),
                        AgentJson.GetEnum(value, "type", LogType.Log)));
                result.Add(document);
            }
            return result;
        }

        public void Save(CommandLineSessionDocument document)
        {
            var entries = document.Entries.Select(value => (object?)AgentJson.Object(
                ("timeUtc", AgentJson.Utc(value.TimeUtc)),
                ("isInput", value.IsInput),
                ("text", value.Text),
                ("stackTrace", value.StackTrace),
                ("type", value.Type.ToString()))).ToList();
            var json = AgentJson.Stringify(AgentJson.Object(
                ("schemaVersion", SchemaVersion),
                ("id", document.Id),
                ("title", document.Title),
                ("createdAtUtc", AgentJson.Utc(document.CreatedAtUtc)),
                ("updatedAtUtc", AgentJson.Utc(document.UpdatedAtUtc)),
                ("isPinned", document.IsPinned),
                ("isArchived", document.IsArchived),
                ("sortOrder", document.SortOrder),
                ("draft", document.Draft),
                ("entries", entries)));
            WriteAtomic(Path.Combine(_root, document.Id + ".json"), json);
        }

        public void Delete(string id)
        {
            var path = Path.Combine(_root, id + ".json");
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }

        public string LoadSelectedId()
        {
            var path = Path.Combine(_root, "state.json");
            return File.Exists(path)
                ? AgentJson.GetString(AgentJson.ParseObject(File.ReadAllText(path)), "selectedSessionId")
                : string.Empty;
        }

        public void SaveSelectedId(string id) => WriteAtomic(Path.Combine(_root, "state.json"),
            AgentJson.Stringify(AgentJson.Object(("schemaVersion", SchemaVersion), ("selectedSessionId", id))));

        private static void WriteAtomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ??
                                      throw new InvalidOperationException("Command Line path has no directory."));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, text);
            if (File.Exists(path))
            {
                try { File.Replace(temporary, path, path + ".bak"); }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(path, path + ".bak", true);
                    File.Copy(temporary, path, true);
                    File.Delete(temporary);
                }
            }
            else File.Move(temporary, path);
        }
    }
}
