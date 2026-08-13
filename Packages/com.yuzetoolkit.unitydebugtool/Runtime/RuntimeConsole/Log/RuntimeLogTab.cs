#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal sealed class RuntimeLogTab : RuntimeConsoleTabBase
    {
        private const string RowClass = "yuzu-runtime-log-row";
        private const string RowSelectedClass = "yuzu-runtime-log-row-selected";
        private const string ListClass = "yuzu-runtime-log-list";
        private const string SplitterClass = "yuzu-runtime-log-splitter";
        private const string SplitterHandleClass = "yuzu-runtime-log-splitter-handle";
        private const string DetailClass = "yuzu-runtime-log-detail";
        private const string DetailTextClass = "yuzu-runtime-log-detail-text";
        private const string FilterButtonClass = "yuzu-runtime-log-filter-toggle";
        private const string FilterButtonActiveClass = "yuzu-runtime-log-filter-toggle-active";
        private const string FilterLogClass = "yuzu-runtime-log-filter-log";
        private const string FilterWarningClass = "yuzu-runtime-log-filter-warning";
        private const string FilterErrorClass = "yuzu-runtime-log-filter-error";
        private const string ToolbarButtonClass = "yuzu-runtime-log-toolbar-button";
        private const string ClearButtonClass = "yuzu-runtime-log-clear-button";
        private const string CollapseButtonClass = "yuzu-runtime-log-collapse-toggle";
        private const string SearchClass = "yuzu-runtime-log-search";
        private const string ToolbarSpacerClass = "yuzu-runtime-log-toolbar-spacer";
        private const string RowLogClass = "yuzu-runtime-log-row-log";
        private const string RowWarningClass = "yuzu-runtime-log-row-warning";
        private const string RowErrorClass = "yuzu-runtime-log-row-error";
        private const string RowMessageClass = "yuzu-runtime-log-row-message";

        private readonly RuntimeLogStore _store = new();
        private readonly Dictionary<LogType, Button> _typeButtons = new();
        private readonly Dictionary<LogType, bool> _typeEnabled = new();
        private readonly List<VisualElement> _rowElements = new();
        private RuntimeConsolePanView _list = null!;
        private RuntimeConsolePanView _detailPane = null!;
        private Label _detail = null!;
        private TextField _search = null!;
        private Button _collapse = null!;
        private bool _collapseEnabled;
        private DebugLogEntry? _selected;
        private int _maxEntries;
        private bool _dirty = true;

        public RuntimeLogTab(int maxEntries) : base("log", "Log", 0)
        {
            _maxEntries = maxEntries;
            Build();
            _store.MaxEntries = maxEntries;
            _store.Subscribe();
        }

        public override void Tick()
        {
            _store.MaxEntries = _maxEntries;
            if (_store.Pump())
                _dirty = true;

            if (_dirty)
                Rebuild();
        }

        public override void Shutdown()
        {
            _store.Dispose();
            base.Shutdown();
        }

        private void Build()
        {
            var toolbar = RuntimeConsoleUi.CreateToolbar();
            Root.Add(toolbar);

            var clearButton = RuntimeConsoleUi.CreateButton("Clear", "Clear all captured Unity logs.", Clear, 54);
            clearButton.AddToClassList(ToolbarButtonClass);
            clearButton.AddToClassList(ClearButtonClass);
            clearButton.style.width = StyleKeyword.Auto;
            toolbar.Add(clearButton);

            _collapse = new Button(() =>
            {
                _collapseEnabled = !_collapseEnabled;
                SyncFilterButton(_collapse, _collapseEnabled);
                MarkDirty();
            })
            {
                text = "Collapse",
                tooltip = "Collapse identical log messages."
            };
            _collapse.AddToClassList(ToolbarButtonClass);
            _collapse.AddToClassList(FilterButtonClass);
            _collapse.AddToClassList(CollapseButtonClass);
            toolbar.Add(_collapse);
            SyncFilterButton(_collapse, _collapseEnabled);

            var spacer = new VisualElement();
            spacer.AddToClassList(ToolbarSpacerClass);
            toolbar.Add(spacer);

            _search = new TextField { tooltip = "Filter logs by message or stack trace." };
            _search.AddToClassList(SearchClass);
            _search.style.width = 180;
            _search.style.maxWidth = 260;
            _search.style.minWidth = 88;
            _search.style.flexGrow = 1;
            _search.style.flexShrink = 1;
            _search.RegisterValueChangedCallback(_ => MarkDirty());
            toolbar.Add(_search);

            AddLogTypeButton(toolbar, LogType.Log, "Log");
            AddLogTypeButton(toolbar, LogType.Warning, "Warning");
            AddLogTypeButton(toolbar, LogType.Error, "Error");

            _list = RuntimeConsoleUi.CreatePanView();
            _list.Root.AddToClassList(ListClass);
            Root.Add(_list.Root);

            var splitter = new VisualElement { tooltip = "Drag to resize the log list and stack trace panes." };
            splitter.AddToClassList(SplitterClass);
            var splitterHandle = new VisualElement { name = "runtime-log-splitter-handle" };
            splitterHandle.AddToClassList(SplitterHandleClass);
            splitter.Add(splitterHandle);

            _detail = new Label();
            _detail.AddToClassList(DetailTextClass);
            _detailPane = RuntimeConsoleUi.CreatePanView();
            _detailPane.Root.AddToClassList(DetailClass);
            _detailPane.Add(_detail);
            splitter.AddManipulator(new RuntimeLogDetailResizeManipulator(splitter, _list.Root, _detailPane.Root));
            Root.Add(splitter);
            Root.Add(_detailPane.Root);
        }

        private void AddLogTypeButton(VisualElement parent, LogType type, string label)
        {
            _typeEnabled[type] = true;
            var button = new Button(() =>
            {
                _typeEnabled[type] = !_typeEnabled[type];
                SyncFilterButton(_typeButtons[type], _typeEnabled[type]);
                MarkDirty();
            })
            {
                tooltip = $"Show {label} entries."
            };
            button.AddToClassList(ToolbarButtonClass);
            button.AddToClassList(FilterButtonClass);
            button.AddToClassList(GetFilterClass(type));
            SetFilterLabel(button, type, 0);
            _typeButtons[type] = button;
            parent.Add(button);
            SyncFilterButton(button, _typeEnabled[type]);
        }

        private void Clear()
        {
            _store.Clear();
            _selected = null;
            _detail.text = string.Empty;
            _detailPane.ResetOffset();
            MarkDirty();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void Rebuild()
        {
            _dirty = false;
            _list.Clear();
            _rowElements.Clear();

            UpdateCounters();

            foreach (var item in GetDisplayEntries())
                _list.Add(CreateRow(item.Entry, item.Count));

            RefreshSelection();
        }

        private void UpdateCounters()
        {
            var logCount = _store.Entries.Count(entry => entry.Type == LogType.Log);
            var warningCount = _store.Entries.Count(entry => entry.Type == LogType.Warning);
            var errorCount = _store.Entries.Count(IsError);
            SetFilterLabel(_typeButtons[LogType.Log], LogType.Log, logCount);
            SetFilterLabel(_typeButtons[LogType.Warning], LogType.Warning, warningCount);
            SetFilterLabel(_typeButtons[LogType.Error], LogType.Error, errorCount);
            SyncFilterButton(_typeButtons[LogType.Log], _typeEnabled[LogType.Log]);
            SyncFilterButton(_typeButtons[LogType.Warning], _typeEnabled[LogType.Warning]);
            SyncFilterButton(_typeButtons[LogType.Error], _typeEnabled[LogType.Error]);
        }

        private IEnumerable<(DebugLogEntry Entry, int Count)> GetDisplayEntries()
        {
            var filtered = _store.Entries.Where(MatchesFilter);
            if (!_collapseEnabled)
                return filtered.Select(entry => (entry, 1)).ToList();

            return filtered
                .GroupBy(entry => $"{entry.Type}\n{entry.Message}\n{entry.StackTrace}", StringComparer.Ordinal)
                .Select(group => (group.Last(), group.Count()))
                .ToList();
        }

        private bool MatchesFilter(DebugLogEntry entry)
        {
            if (entry.Type == LogType.Log && !_typeEnabled[LogType.Log]) return false;
            if (entry.Type == LogType.Warning && !_typeEnabled[LogType.Warning]) return false;
            if (IsError(entry) && !_typeEnabled[LogType.Error]) return false;

            var search = _search.value;
            return string.IsNullOrWhiteSpace(search) ||
                   entry.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entry.StackTrace.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement CreateRow(DebugLogEntry entry, int count)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClass);
            row.AddToClassList(GetRowClass(entry));
            row.userData = entry;
            _rowElements.Add(row);

            var message = new Label(FormatMessage(entry, count));
            message.AddToClassList(RowMessageClass);
            row.Add(message);

            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                Select(entry);
                if (evt.clickCount >= 2)
                    GUIUtility.systemCopyBuffer = FormatDetail(entry);
            });

            return row;
        }

        private void Select(DebugLogEntry entry)
        {
            _selected = entry;
            _detail.text = FormatDetail(entry);
            _detailPane.ResetOffset();
            _detailPane.Refresh();
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            foreach (var row in _rowElements)
            {
                if (ReferenceEquals(row.userData, _selected))
                    row.AddToClassList(RowSelectedClass);
                else
                    row.RemoveFromClassList(RowSelectedClass);
            }
        }

        private static bool IsError(DebugLogEntry entry) =>
            entry.Type is LogType.Error or LogType.Exception or LogType.Assert;

        private static string FormatMessage(DebugLogEntry entry, int count)
        {
            var suffix = count > 1 ? $" ({count})" : string.Empty;
            return $"[{entry.Time:HH:mm:ss}] {entry.Message}{suffix}";
        }

        private static string FormatDetail(DebugLogEntry entry)
        {
            return string.IsNullOrWhiteSpace(entry.StackTrace)
                ? entry.Message
                : entry.Message + Environment.NewLine + entry.StackTrace;
        }

        private static void SetFilterLabel(Button button, LogType type, int count)
        {
            button.text = type switch
            {
                LogType.Warning => $"Warn {count}",
                LogType.Error => $"Error {count}",
                _ => $"Log {count}"
            };
        }

        private static void SyncFilterButton(VisualElement button, bool active)
        {
            if (active)
                button.AddToClassList(FilterButtonActiveClass);
            else
                button.RemoveFromClassList(FilterButtonActiveClass);
        }

        private static string GetFilterClass(LogType type)
        {
            return type switch
            {
                LogType.Warning => FilterWarningClass,
                LogType.Error => FilterErrorClass,
                _ => FilterLogClass
            };
        }

        private static string GetRowClass(DebugLogEntry entry)
        {
            return entry.Type switch
            {
                LogType.Warning => RowWarningClass,
                LogType.Error or LogType.Exception or LogType.Assert => RowErrorClass,
                _ => RowLogClass
            };
        }
    }

    internal sealed class RuntimeLogDetailResizeManipulator : PointerManipulator
    {
        private const string SplitterActiveClass = "yuzu-runtime-log-splitter-active";
        private const float InitialDetailHeight = 138f;
        private const float MinListHeight = 72f;
        private const float MinDetailHeight = 64f;

        private readonly VisualElement _listPane;
        private readonly VisualElement _detailPane;
        private bool _active;
        private Vector2 _startPointer;
        private float _startDetailHeight;
        private float _dragAreaHeight;
        private float _currentDetailHeight = InitialDetailHeight;

        public RuntimeLogDetailResizeManipulator(
            VisualElement resizeHandle,
            VisualElement listPane,
            VisualElement detailPane)
        {
            target = resizeHandle;
            _listPane = listPane;
            _detailPane = detailPane;
            _listPane.RegisterCallback<GeometryChangedEvent>(_ => ClampCurrentHeight());
            _detailPane.RegisterCallback<GeometryChangedEvent>(_ => ClampCurrentHeight());
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            _active = true;
            _startPointer = evt.position;
            _dragAreaHeight = DragAreaHeight();
            if (_dragAreaHeight <= 0f)
            {
                _active = false;
                return;
            }

            _startDetailHeight = CurrentDetailHeight();
            SetPanelHeights(_startDetailHeight);
            target.CapturePointer(evt.pointerId);
            target.AddToClassList(SplitterActiveClass);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_active || !target.HasPointerCapture(evt.pointerId)) return;

            var delta = (Vector2)evt.position - _startPointer;
            SetDetailHeight(_startDetailHeight - delta.y);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            Finish(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            Finish(evt.pointerId);
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _active = false;
            target.RemoveFromClassList(SplitterActiveClass);
        }

        private void Finish(int pointerId)
        {
            if (!_active) return;
            _active = false;
            target.RemoveFromClassList(SplitterActiveClass);
            if (target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);
        }

        private float MaxDetailHeight()
        {
            var availableHeight = _active ? _dragAreaHeight : DragAreaHeight();
            return Mathf.Max(MinDetailHeight, availableHeight - MinListHeight);
        }

        private float CurrentDetailHeight()
        {
            var resolvedHeight = Mathf.Max(0f, _detailPane.resolvedStyle.height);
            if (resolvedHeight > 0f && Mathf.Approximately(_currentDetailHeight, InitialDetailHeight))
                _currentDetailHeight = resolvedHeight;

            return Mathf.Clamp(_currentDetailHeight, MinDetailHeight, MaxDetailHeight());
        }

        private void SetDetailHeight(float height)
        {
            _currentDetailHeight = Mathf.Clamp(height, MinDetailHeight, MaxDetailHeight());
            SetPanelHeights(_currentDetailHeight);
        }

        private void ClampCurrentHeight()
        {
            if (_active) return;

            if (DragAreaHeight() <= 0f) return;

            var clamped = Mathf.Clamp(_currentDetailHeight, MinDetailHeight, MaxDetailHeight());
            if (Mathf.Approximately(clamped, _currentDetailHeight)) return;

            _currentDetailHeight = clamped;
            SetPanelHeights(_currentDetailHeight);
        }

        private float DragAreaHeight()
        {
            return Mathf.Max(0f, _listPane.resolvedStyle.height) +
                   Mathf.Max(0f, _detailPane.resolvedStyle.height);
        }

        private void SetPanelHeights(float detailHeight)
        {
            var areaHeight = _active ? _dragAreaHeight : DragAreaHeight();
            if (areaHeight <= 0f) return;

            var clampedDetailHeight = Mathf.Clamp(detailHeight, MinDetailHeight, Mathf.Max(MinDetailHeight, areaHeight - MinListHeight));

            _currentDetailHeight = clampedDetailHeight;
            _listPane.style.flexGrow = 1f;
            _listPane.style.height = StyleKeyword.Auto;
            _detailPane.style.flexGrow = 0f;
            _detailPane.style.height = clampedDetailHeight;
        }
    }
}
