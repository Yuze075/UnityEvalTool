#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YuzeToolkit
{
    internal sealed class RuntimeEvalToolTab : RuntimeConsoleTabBase
    {
        private const string Endpoint = "http://127.0.0.1:2347/mcp";
        private static readonly Color DisabledColor = new(0.4f, 0.44f, 0.5f);

        private Button _enabledSwitch = null!;
        private Button _reconnect = null!;
        private Label _connection = null!;
        private Label _phase = null!;
        private Label _canEval = null!;
        private Label _busyReason = null!;
        private Label _playMode = null!;
        private Label _instance = null!;
        private Label _connectionEpoch = null!;
        private Label _vmGeneration = null!;
        private Label _mainThread = null!;

        public RuntimeEvalToolTab() : base("eval-tool", "EvalTool", 20)
        {
            Build();
        }

        public override void Tick()
        {
            var client = UnityBrokerClient.Shared;
            var status = client.LatestStatus;
            var identity = client.Identity;
            var running = client.IsRunning;
            var connected = client.IsConnected;

            _connection.text = !running ? "Disabled" : connected ? "Connected" : "Reconnecting";
            _connection.style.color = connected
                ? RuntimeConsoleUi.RunningColor
                : running ? RuntimeConsoleUi.WarningColor : DisabledColor;
            _phase.text = status.Phase;
            _canEval.text = connected && status.CanEval
                ? string.Equals(status.Phase, "CompilationFailed", StringComparison.Ordinal) ? "Repair" : "Ready"
                : "Unavailable";
            _canEval.style.color = connected && status.CanEval
                ? RuntimeConsoleUi.RunningColor
                : RuntimeConsoleUi.WarningColor;
            _busyReason.text = string.IsNullOrWhiteSpace(status.BusyReason) ? "—" : status.BusyReason;
            _playMode.text = status.IsPlaying
                ? status.IsPaused ? "Play Mode / Paused" : "Play Mode"
                : status.IsUpdating ? "Edit Mode / Importing" : "Edit Mode";
            _instance.text = identity.InstanceId;
            _connectionEpoch.text = identity.ConnectionEpoch.ToString();
            _vmGeneration.text = status.VmGeneration.ToString();
            _mainThread.text = status.MainThreadTickAtUtc == default
                ? "No heartbeat"
                : $"tick {status.MainThreadTick} / {status.MainThreadTickAtUtc.ToLocalTime():HH:mm:ss}";
            _reconnect.SetEnabled(running);
            SetSwitchStyle(_enabledSwitch, running);
        }

        private void Build()
        {
            var toolbar = RuntimeConsoleUi.CreateToolbar();
            Root.Add(toolbar);

            _enabledSwitch = RuntimeConsoleUi.CreateButton(string.Empty,
                "Enable or disable this Unity process registration with the UnityEvalTool Broker.", ToggleEnabled, 112);
            toolbar.Add(_enabledSwitch);
            _reconnect = RuntimeConsoleUi.CreateButton("Reconnect", "Reconnect this Unity process to the Broker.",
                () => UnityBrokerClient.Shared.Reconnect(), 92);
            toolbar.Add(_reconnect);
            toolbar.Add(RuntimeConsoleUi.CreateButton("Copy Endpoint", "Copy the fixed Broker MCP endpoint.",
                () => GUIUtility.systemCopyBuffer = Endpoint, 112));

            var hint = new Label("UnityEvalTool registration and evaluation state");
            hint.AddToClassList(RuntimeConsoleUss.LabelClass);
            hint.AddToClassList(RuntimeConsoleUss.MutedLabelClass);
            hint.style.marginLeft = 8;
            toolbar.Add(hint);

            var page = RuntimeConsoleUi.CreatePage();
            var content = RuntimeConsoleUi.CreatePanView();
            page.Add(content.Root);
            Root.Add(page);

            var stateCard = RuntimeConsoleUi.CreateCard();
            content.Add(stateCard);
            RuntimeConsoleUi.AddTitle(stateCard, "UnityEvalTool");
            _connection = RuntimeConsoleUi.AddField(stateCard, "Connection");
            _phase = RuntimeConsoleUi.AddField(stateCard, "Unity phase");
            _canEval = RuntimeConsoleUi.AddField(stateCard, "Evaluation");
            _busyReason = RuntimeConsoleUi.AddField(stateCard, "Busy reason");
            _playMode = RuntimeConsoleUi.AddField(stateCard, "Runtime state");

            var identityCard = RuntimeConsoleUi.CreateCard();
            content.Add(identityCard);
            RuntimeConsoleUi.AddTitle(identityCard, "Unity identity");
            _instance = RuntimeConsoleUi.AddField(identityCard, "Instance ID");
            _connectionEpoch = RuntimeConsoleUi.AddField(identityCard, "Connection epoch");
            _vmGeneration = RuntimeConsoleUi.AddField(identityCard, "VM generation");
            _mainThread = RuntimeConsoleUi.AddField(identityCard, "Main thread heartbeat");
            RuntimeConsoleUi.AddField(identityCard, "MCP endpoint").text = Endpoint;

            var workflowCard = RuntimeConsoleUi.CreateCard();
            content.Add(workflowCard);
            RuntimeConsoleUi.AddTitle(workflowCard, "Agent workflow");
            workflowCard.Add(RuntimeConsoleUi.CreateMessage(
                "Use unity_status, then unity_connect, then reuse the handle. Wait for compilation through unity_status; CompilationFailed remains available for repair.",
                RuntimeConsoleUi.RunningColor));
            Tick();
        }

        private static void ToggleEnabled()
        {
            var client = UnityBrokerClient.Shared;
            var enabled = !client.IsRunning;
#if UNITY_EDITOR
            EditorPrefs.SetBool(UnityBrokerClient.EditorEnabledPreferenceKey, enabled);
#endif
            if (enabled) client.Start();
            else client.Stop();
        }

        private static void SetSwitchStyle(Button button, bool enabled)
        {
            button.text = enabled ? "●  Enabled" : "○  Disabled";
            button.style.backgroundColor = enabled ? RuntimeConsoleUi.RunningColor : DisabledColor;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 13;
            button.style.borderTopRightRadius = 13;
            button.style.borderBottomLeftRadius = 13;
            button.style.borderBottomRightRadius = 13;
        }
    }
}
