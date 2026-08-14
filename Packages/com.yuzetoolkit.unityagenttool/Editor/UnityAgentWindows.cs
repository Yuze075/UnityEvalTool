#nullable enable
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit.UnityAgent
{
    internal sealed class UnityAgentWindow : EditorWindow
    {
        private UnityAgentWorkbenchView? _view;
        private IVisualElementScheduledItem? _tickItem;
        private static UnityAgentWorkbenchPage _requestedPage = UnityAgentWorkbenchPage.Chat;

        [MenuItem("YuzeToolkit/Unity Agent/Chat")]
        public static void OpenChat()
        {
            Open(UnityAgentWorkbenchPage.Chat);
        }

        [MenuItem("YuzeToolkit/Unity Agent/Settings")]
        public static void OpenSettings()
        {
            Open(UnityAgentWorkbenchPage.Settings);
        }

        private static void Open(UnityAgentWorkbenchPage page)
        {
            _requestedPage = page;
            var window = GetWindow<UnityAgentWindow>("Unity Agent");
            window.minSize = new Vector2(480, 480);
            window.Show();
            window._view?.ShowPage(page);
        }

        private void CreateGUI()
        {
            _tickItem?.Pause();
            _view?.Dispose();
            rootVisualElement.Clear();
            _view = new UnityAgentWorkbenchView(UnityAgentHost.Default, initialPage: _requestedPage);
            rootVisualElement.Add(_view);
            _tickItem = rootVisualElement.schedule.Execute(() => _view?.Tick()).Every(100);
        }

        private void OnDisable()
        {
            _tickItem?.Pause();
            _tickItem = null;
            _view?.Dispose();
            _view = null;
        }
    }

    [InitializeOnLoad]
    internal static class UnityAgentEditorLifetime
    {
        private const string ProjectSettingsAssetPath = "Assets/Resources/UnityAgentProjectSettings.json";
        private static bool _runtimeDataAvailable;

        static UnityAgentEditorLifetime()
        {
            _runtimeDataAvailable = EditorApplication.isPlaying;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityAgentRuntimeDataBridge.Configure(() => _runtimeDataAvailable);
            UnityAgentEvalSettingsBridge.ConfigureBrokerControl(
                () => EditorBrokerBootstrap.IsEnabled,
                EditorBrokerBootstrap.SetEnabled);
            UnityAgentEvalSettingsBridge.ConfigureEditorActions(
                EditorBrokerBootstrap.Reconnect,
                OpenBrokerFolder,
                SaveProjectSettings);
            AssemblyReloadEvents.beforeAssemblyReload -= UnityAgentHost.DisposeDefault;
            AssemblyReloadEvents.beforeAssemblyReload += UnityAgentHost.DisposeDefault;
            EditorApplication.quitting -= UnityAgentHost.DisposeDefault;
            EditorApplication.quitting += UnityAgentHost.DisposeDefault;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            _runtimeDataAvailable = state == PlayModeStateChange.EnteredPlayMode;
        }

        private static void OpenBrokerFolder()
        {
            var path = Path.Combine(System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.UserProfile), ".unityevaltool");
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static void SaveProjectSettings(string json)
        {
            var path = Path.GetFullPath(Path.Combine(AgentPaths.ProjectRoot, ProjectSettingsAssetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ??
                                      throw new InvalidOperationException("Project Settings path has no directory."));
            File.WriteAllText(path, json + "\n", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ProjectSettingsAssetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
