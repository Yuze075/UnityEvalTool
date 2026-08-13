#nullable enable
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

        [MenuItem("YuzeToolkit/Unity Agent/Open")]
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
            window.minSize = new Vector2(780, 520);
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
        static UnityAgentEditorLifetime()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= UnityAgentHost.DisposeDefault;
            AssemblyReloadEvents.beforeAssemblyReload += UnityAgentHost.DisposeDefault;
            EditorApplication.quitting -= UnityAgentHost.DisposeDefault;
            EditorApplication.quitting += UnityAgentHost.DisposeDefault;
        }
    }
}
