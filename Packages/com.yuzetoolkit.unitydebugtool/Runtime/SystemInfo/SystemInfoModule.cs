#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using YuzeToolkit.UnityAgent;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class SystemInfoModule : MonoBehaviour, IDebugPanelModule
    {
        [SerializeField, Tooltip("Required UXML template for system information. Initialization fails when it is missing.")]
        private VisualTreeAsset? template;

        [SerializeField, Tooltip("Required USS for system information. Initialization fails when it is missing.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Keyboard key used with the DebugPanel modifiers to show or hide the system information module.")]
        private Key toggleKey = Key.F10;

        private const float RefreshInterval = 1f;
        private SystemInfoView? _view;
        private VisualElement? _layer;
        private float _refreshTimer;
        private System.IDisposable? _workspaceRegistration;

        public int SortOrder => 1;

        public Key ToggleKey => toggleKey;

        public void Initialize(DebugPanelContext context)
        {
            if (template == null || styleSheet == null)
                throw new MissingReferenceException(
                    $"{nameof(SystemInfoModule)} requires both UXML and USS references.");

            try
            {
                context.AddStyleSheet(styleSheet);
                _layer = context.CreateLayer("unity-debug-tool-system-info-layer");
                SystemInfoUss.ApplyLayer(_layer);
                _view = new SystemInfoView(template);
                _view.AttachTo(_layer);
                _workspaceRegistration = UnityAgentWorkspaceRegistry.RegisterSystemInfoSection(
                    "unity-debug-tool-system-info", 0, () => CreateWorkspaceSection(template, styleSheet));
                Refresh();
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_layer != null)
                _layer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
                Refresh();
        }

        public void Tick()
        {
            if (_view == null) return;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < RefreshInterval) return;

            Refresh();
        }

        public void Shutdown()
        {
            _view?.Detach();
            _view = null;
            _workspaceRegistration?.Dispose();
            _workspaceRegistration = null;
            _layer?.RemoveFromHierarchy();
            _layer = null;
            _refreshTimer = 0f;
        }

        public static IUnityAgentWorkspaceSection CreateWorkspaceSection(
            VisualTreeAsset templateAsset, StyleSheet styleSheetAsset)
        {
            if (templateAsset == null) throw new System.ArgumentNullException(nameof(templateAsset));
            if (styleSheetAsset == null) throw new System.ArgumentNullException(nameof(styleSheetAsset));
            return new SystemInfoWorkspaceSection(templateAsset, styleSheetAsset);
        }

        private sealed class SystemInfoWorkspaceSection : IUnityAgentWorkspaceSection
        {
            private readonly SystemInfoView _view;
            private float _timer;

            public SystemInfoWorkspaceSection(VisualTreeAsset templateAsset, StyleSheet styleSheetAsset)
            {
                Root = new VisualElement();
                SystemInfoUss.ApplyLayer(Root);
                ApplyEmbeddedRootLayout(Root);
                Root.style.flexShrink = 0;
                Root.styleSheets.Add(styleSheetAsset);
                _view = new SystemInfoView(templateAsset);
                _view.AttachTo(Root);
                _view.SetEmbeddedLayout();
                _view.ApplySnapshot(SystemInfoRegistry.CaptureSnapshot());
            }

            private static void ApplyEmbeddedRootLayout(VisualElement root)
            {
                root.style.position = Position.Relative;
                root.style.left = StyleKeyword.Auto;
                root.style.right = StyleKeyword.Auto;
                root.style.top = StyleKeyword.Auto;
                root.style.bottom = StyleKeyword.Auto;
                root.style.width = new Length(100, LengthUnit.Percent);
            }

            public VisualElement Root { get; }

            public void Tick()
            {
                _timer += Time.unscaledDeltaTime;
                if (_timer < RefreshInterval) return;
                _timer = 0f;
                _view.ApplySnapshot(SystemInfoRegistry.CaptureSnapshot());
            }

            public void Dispose()
            {
                _view.Detach();
                Root.RemoveFromHierarchy();
            }
        }

        private void Refresh()
        {
            _refreshTimer = 0f;
            _view?.ApplySnapshot(SystemInfoRegistry.CaptureSnapshot());
        }
    }
}
