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

        private readonly RuntimeCliRunner _runner = new();
        private readonly List<VisualElement> _rows = new();
        private readonly int _maxHistoryRows;
        private RuntimeConsolePanView _history = null!;
        private TextField _input = null!;
        private Button _runButton = null!;
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
            toolbar.Add(RuntimeConsoleUi.CreateButton("Clear", "Clear CLI REPL history.", Clear, 54));

            var hint = new Label("UnityEvalTool command line session");
            hint.AddToClassList(RuntimeConsoleUss.LabelClass);
            hint.AddToClassList(RuntimeConsoleUss.MutedLabelClass);
            hint.style.marginLeft = 8;
            toolbar.Add(hint);

            _history = RuntimeConsoleUi.CreatePanView();
            _history.Root.AddToClassList(HistoryClass);
            Root.Add(_history.Root);

            var inputRow = new VisualElement();
            inputRow.AddToClassList(InputRowClass);
            Root.Add(inputRow);

            _input = new TextField();
            _input.tabIndex = -1;
            RuntimeConsoleUss.ApplyOwnedControl(_input);
            _input.AddToClassList(InputFieldClass);
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
            inputRow.Add(_input);

            _runButton = RuntimeConsoleUi.CreateButton("Run", "Run the current CLI line.", Submit, 64);
            _runButton.AddToClassList(RunButtonClass);
            _runButton.style.minWidth = 64;
            _runButton.style.flexShrink = 0;
            inputRow.Add(_runButton);
        }

        private void Clear()
        {
            _history.Clear();
            _rows.Clear();
        }

        private void Submit()
        {
            if (_running || _input == null) return;

            var line = _input.value;
            if (string.IsNullOrWhiteSpace(line)) return;

            _input.value = string.Empty;
            AddRow("> " + line, new Color(0.4f, 0.72f, 1f, 1f));
            _ = ExecuteAsync(line);
        }

        private async Task ExecuteAsync(string line)
        {
            _running = true;
            _runButton.SetEnabled(false);
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
                    _runButton.SetEnabled(true);
            }
        }

        private void AddRow(string text, Color color)
        {
            var row = new Label(text);
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
        }

        private static Color GetColor(LogType type)
        {
            return type switch
            {
                LogType.Warning => RuntimeConsoleUi.WarningColor,
                LogType.Error or LogType.Exception or LogType.Assert => RuntimeConsoleUi.ErrorColor,
                _ => new Color(0.88f, 0.92f, 0.96f, 1f)
            };
        }
    }
}
