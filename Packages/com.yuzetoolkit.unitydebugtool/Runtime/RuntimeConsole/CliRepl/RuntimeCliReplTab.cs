#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal sealed class RuntimeCliReplTab : RuntimeConsoleTabBase
    {
        private const string HistoryClass = "yuzu-runtime-cli-history";
        private const string HistoryRowClass = "yuzu-runtime-cli-history-row";
        private const string InputRowClass = "yuzu-runtime-cli-input-row";
        private const string InputFieldClass = "yuzu-runtime-cli-input-field";
        private const string RunButtonClass = "yuzu-runtime-cli-run-button";
        private const string EmptyClass = "yuzu-runtime-cli-empty";
        private const string EmptyTitleClass = "yuzu-runtime-cli-empty-title";
        private const string EmptyBodyClass = "yuzu-runtime-cli-empty-body";
        private const string EmptyActionsClass = "yuzu-runtime-cli-empty-actions";

        private readonly RuntimeCliRunner _runner = new();
        private readonly List<VisualElement> _rows = new();
        private readonly int _maxHistoryRows;
        private RuntimeConsolePanView _history = null!;
        private VisualElement _empty = null!;
        private TextField _input = null!;
        private Button _runButton = null!;
        private Button _clearButton = null!;
        private bool _running;
        private bool _shutdown;

        public RuntimeCliReplTab(int maxHistoryRows) : base("command-line", "Command Line", 10)
        {
            _maxHistoryRows = maxHistoryRows;
            _runner.Start();
            Build();
        }

        public override void Shutdown()
        {
            _shutdown = true;
            _runner.Dispose();
            base.Shutdown();
        }

        private void Build()
        {
            var toolbar = RuntimeConsoleUi.CreateToolbar();
            Root.Add(toolbar);
            _clearButton = RuntimeConsoleUi.CreateButton("Clear", "Clear CLI REPL history.", Clear, 62);
            _clearButton.SetEnabled(false);
            toolbar.Add(_clearButton);

            var hint = new Label("UnityEvalTool commands  ·  Enter to run  ·  Up/Down for history");
            hint.AddToClassList(RuntimeConsoleUss.LabelClass);
            hint.AddToClassList(RuntimeConsoleUss.MutedLabelClass);
            hint.style.marginLeft = 8;
            toolbar.Add(hint);

            _history = RuntimeConsoleUi.CreatePanView();
            _history.Root.AddToClassList(HistoryClass);
            Root.Add(_history.Root);
            _empty = new VisualElement();
            _empty.AddToClassList(EmptyClass);
            var emptyTitle = new Label("Start with a command") { enableRichText = false };
            emptyTitle.AddToClassList(EmptyTitleClass);
            _empty.Add(emptyTitle);
            var emptyBody = new Label("Run a built-in command or enter any UnityEvalTool command below.")
            {
                enableRichText = false
            };
            emptyBody.AddToClassList(EmptyBodyClass);
            _empty.Add(emptyBody);
            var emptyActions = new VisualElement();
            emptyActions.AddToClassList(EmptyActionsClass);
            emptyActions.Add(RuntimeConsoleUi.CreateButton("Run help", "Run help and list available commands.",
                () => RunSuggestedCommand("help"), 124));
            emptyActions.Add(RuntimeConsoleUi.CreateButton("Run tools", "Run tools and list available tool modules.",
                () => RunSuggestedCommand("tools"), 94));
            _empty.Add(emptyActions);
            _history.Add(_empty);

            var inputRow = new VisualElement();
            inputRow.AddToClassList(InputRowClass);
            Root.Add(inputRow);

            _input = new TextField();
            _input.tabIndex = -1;
            RuntimeConsoleUss.ApplyOwnedControl(_input);
            _input.AddToClassList(InputFieldClass);
            var placeholder = new Label("Enter a command, for example: help")
            {
                enableRichText = false,
                pickingMode = PickingMode.Ignore
            };
            placeholder.AddToClassList(RuntimeConsoleUss.SearchPlaceholderClass);
            _input.Add(placeholder);
            _input.style.flexGrow = 1;
            _input.style.flexShrink = 1;
            _input.style.minWidth = 0;
            RuntimeConsoleUi.AttachHelp(_input, "Enter a UnityEvalTool command and press Enter.");
            _input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                Submit();
                _input.Blur();
                evt.PreventDefault();
                evt.StopImmediatePropagation();
            });
            _input.RegisterValueChangedCallback(evt => placeholder.style.display =
                string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None);
            _input.RegisterValueChangedCallback(_ => RefreshActions());
            inputRow.Add(_input);

            _runButton = RuntimeConsoleUi.CreateButton("Run", "Run the current CLI line.", Submit, 64);
            _runButton.AddToClassList(RunButtonClass);
            _runButton.style.minWidth = 64;
            _runButton.style.flexShrink = 0;
            inputRow.Add(_runButton);
            RefreshActions();
        }

        private void Clear()
        {
            _history.Clear();
            _rows.Clear();
            _history.Add(_empty);
            RefreshActions();
        }

        private void RunSuggestedCommand(string command)
        {
            if (_running) return;
            _input.value = command;
            Submit();
        }

        private void Submit()
        {
            if (_running || _input == null) return;

            var line = _input.value;
            if (string.IsNullOrWhiteSpace(line)) return;

            _input.value = string.Empty;
            AddRow("> " + line, RuntimeConsoleDesignTokens.Accent);
            _ = ExecuteAsync(line);
        }

        private async Task ExecuteAsync(string line)
        {
            _running = true;
            RefreshActions();
            try
            {
                var result = await _runner.ExecuteLineAsync(line);
                if (_shutdown) return;
                AddRow(result.Message, GetColor(result.LogType));
                if (!string.IsNullOrWhiteSpace(result.StackTrace))
                    AddRow(result.StackTrace, RuntimeConsoleUi.ErrorColor);
            }
            finally
            {
                _running = false;
                if (!_shutdown)
                    RefreshActions();
            }
        }

        private void AddRow(string text, Color color)
        {
            _empty.RemoveFromHierarchy();
            var row = new Label(text) { enableRichText = false };
            row.AddToClassList(HistoryRowClass);
            row.style.color = color;
            row.style.whiteSpace = WhiteSpace.Normal;
            _history.Add(row);
            _rows.Add(row);

            while (_rows.Count > _maxHistoryRows)
            {
                _rows[0].RemoveFromHierarchy();
                _rows.RemoveAt(0);
            }

            _history.ScrollToEnd();
            RefreshActions();
        }

        private void RefreshActions()
        {
            if (_runButton != null)
                _runButton.SetEnabled(!_running && !string.IsNullOrWhiteSpace(_input?.value));
            if (_clearButton != null)
                _clearButton.SetEnabled(!_running && _rows.Count > 0);
        }

        private static Color GetColor(LogType type)
        {
            return type switch
            {
                LogType.Warning => RuntimeConsoleUi.WarningColor,
                LogType.Error or LogType.Exception or LogType.Assert => RuntimeConsoleUi.ErrorColor,
                _ => RuntimeConsoleDesignTokens.TextSecondary
            };
        }
    }
}
