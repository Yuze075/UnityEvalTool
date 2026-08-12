#nullable enable
using System;
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
        private const string ToolPrefPrefix = nameof(YuzeToolkit) + ".McpTool.Enabled.";
        private Label _connection = null!;
        private Label _phase = null!;
        private Label _instance = null!;
        private Label _installation = null!;

        [MenuItem(nameof(YuzeToolkit) + "/UnityEvalTool")]
        public static void Open()
        {
            var window = GetWindow<UnityEvalToolWindow>("UnityEvalTool");
            window.minSize = new Vector2(620, 420);
            window.Show();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12;
            rootVisualElement.style.paddingBottom = 12;

            var title = new Label("UnityEvalTool Broker 2.0");
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            rootVisualElement.Add(title);
            rootVisualElement.Add(new Label(
                "Unity keeps one authenticated connection to the computer-level Broker. MCP and CLI requests share it."));

            var status = CreateCard("Local registration");
            _connection = AddRow(status, "Broker connection");
            _phase = AddRow(status, "Unity phase");
            _instance = AddRow(status, "Instance ID");
            _installation = AddRow(status, "CLI installation");
            AddRow(status, "MCP endpoint").text = Endpoint;

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            buttons.Add(CreateButton("Copy MCP endpoint", () => EditorGUIUtility.systemCopyBuffer = Endpoint));
            buttons.Add(CreateButton("Reconnect Unity", Reconnect));
            buttons.Add(CreateButton("Open Broker folder", OpenBrokerFolder));
            status.Add(buttons);
            rootVisualElement.Add(status);

            var note = new HelpBox(
                "AI agents must call unity_status, then unity_connect with the returned registryRevision, and only then eval. " +
                "When Unity is compiling or reloading, unity_status can wait by instanceId without calling eval.",
                HelpBoxMessageType.Info);
            note.style.marginTop = 8;
            rootVisualElement.Add(note);

            var toolsCard = CreateCard("Unity eval tools");
            var scroll = new ScrollView { style = { maxHeight = 260 } };
            foreach (var tool in EvalToolRegistry.ListTools(false).OrderBy(value => value.Path, StringComparer.Ordinal))
            {
                var toggle = new Toggle(tool.Path) { value = tool.Enabled, tooltip = tool.Description };
                var path = tool.Path;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(ToolPrefPrefix + path, evt.newValue);
                    EvalToolRegistry.SetEnabled(path, evt.newValue);
                });
                scroll.Add(toggle);
            }
            toolsCard.Add(scroll);
            rootVisualElement.Add(toolsCard);

            rootVisualElement.schedule.Execute(Refresh).Every(500);
            Refresh();
        }

        private static VisualElement CreateCard(string heading)
        {
            var card = new VisualElement();
            card.style.marginTop = 12;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new Color(0.32f, 0.32f, 0.32f);
            card.style.borderBottomColor = new Color(0.32f, 0.32f, 0.32f);
            card.style.borderLeftColor = new Color(0.32f, 0.32f, 0.32f);
            card.style.borderRightColor = new Color(0.32f, 0.32f, 0.32f);
            var label = new Label(heading);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6;
            card.Add(label);
            return card;
        }

        private static Label AddRow(VisualElement parent, string name)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 3 } };
            var key = new Label(name) { style = { width = 140 } };
            var value = new Label { style = { flexGrow = 1 } };
            row.Add(key);
            row.Add(value);
            parent.Add(row);
            return value;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 6;
            return button;
        }

        private void Refresh()
        {
            if (_connection == null) return;
            _connection.text = UnityBrokerClient.Shared.IsConnected ? "Connected" : "Reconnecting";
            _phase.text = ResolvePhase();
            _instance.text = UnityBrokerClient.Shared.Identity.InstanceId;
            _installation.text = GetInstallationStatus();
        }

        private static string ResolvePhase()
        {
            if (EditorApplication.isCompiling) return "Compiling";
            if (EditorApplication.isUpdating) return "Importing";
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
                return "PlayModeTransition";
            return "Ready";
        }

        private static void Reconnect()
        {
            UnityBrokerClient.Shared.Stop();
            UnityBrokerClient.Shared.Start();
        }

        private static void OpenBrokerFolder()
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unityevaltool");
            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory);
        }

        private static string GetInstallationStatus()
        {
            var metadata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unityevaltool", "install.json");
            if (!File.Exists(metadata)) return "Run the npm-installed `unity` command once";
            try
            {
                var root = EvalData.AsObject(LitJson.Parse(File.ReadAllText(metadata)));
                var executable = root == null ? string.Empty : EvalData.GetString(root, "executablePath") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(executable)) return "Invalid install metadata: executablePath is missing";
                return File.Exists(executable) ? executable : "Installed executable is missing: " + executable;
            }
            catch (Exception ex)
            {
                return "Invalid install metadata: " + ex.Message;
            }
        }
    }
}
