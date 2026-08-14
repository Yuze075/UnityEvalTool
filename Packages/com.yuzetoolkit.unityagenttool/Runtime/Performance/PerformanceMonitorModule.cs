#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using YuzeToolkit.UnityAgent;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class PerformanceMonitorModule : MonoBehaviour, IDebugPanelModule
    {
        [SerializeField, Tooltip("Required UXML template for the performance monitor. Initialization fails when it is missing.")]
        private VisualTreeAsset? template;

        [SerializeField, Tooltip("Required USS for the performance monitor. Initialization fails when it is missing.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Keyboard key used with the DebugPanel modifiers to show or hide the performance monitor module.")]
        private Key toggleKey = Key.F10;

        private readonly PerformanceSampler _sampler = new();
        private PerformanceMonitorView? _view;
        private VisualElement? _layer;
        private System.IDisposable? _workspaceRegistration;

        public int SortOrder => 0;

        public Key ToggleKey => toggleKey;

        public void Initialize(DebugPanelContext context)
        {
            if (template == null || styleSheet == null)
                throw new MissingReferenceException(
                    $"{nameof(PerformanceMonitorModule)} requires both UXML and USS references.");

            context.AddStyleSheet(styleSheet);
            try
            {
                _layer = context.CreateLayer("unity-agent-performance-layer");
                PerformanceMonitorUss.ApplyLayer(_layer);
                _view = new PerformanceMonitorView(template);
                _view.AttachTo(_layer);
                _workspaceRegistration = UnityAgentWorkspaceRegistry.RegisterSystemInfoSection(
                    "unity-agent-performance", 0, () => CreateWorkspaceSection(template, styleSheet));
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
        }

        public void Tick()
        {
            if (_view == null) return;

            var update = _sampler.Tick(Time.unscaledDeltaTime);
            if (update.Metrics != null)
                _view.ApplyMetrics(update.Metrics.Value);
        }

        public void Shutdown()
        {
            _view?.Detach();
            _view = null;
            _workspaceRegistration?.Dispose();
            _workspaceRegistration = null;
            _layer?.RemoveFromHierarchy();
            _layer = null;
            _sampler.Reset();
        }

        public static IUnityAgentWorkspaceSection CreateWorkspaceSection(
            VisualTreeAsset templateAsset, StyleSheet styleSheetAsset)
        {
            if (templateAsset == null) throw new System.ArgumentNullException(nameof(templateAsset));
            if (styleSheetAsset == null) throw new System.ArgumentNullException(nameof(styleSheetAsset));
            return new PerformanceWorkspaceSection(templateAsset, styleSheetAsset);
        }

        private sealed class PerformanceWorkspaceSection : IUnityAgentWorkspaceSection
        {
            private readonly PerformanceSampler _sampler = new();
            private readonly PerformanceMonitorView _view;

            public PerformanceWorkspaceSection(VisualTreeAsset templateAsset, StyleSheet styleSheetAsset)
            {
                Root = new VisualElement();
                PerformanceMonitorUss.ApplyLayer(Root);
                ApplyEmbeddedRootLayout(Root);
                Root.style.flexShrink = 0;
                Root.styleSheets.Add(styleSheetAsset);
                _view = new PerformanceMonitorView(templateAsset);
                _view.AttachTo(Root);
                _view.SetEmbeddedLayout();
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
                var update = _sampler.Tick(Time.unscaledDeltaTime);
                if (update.Metrics != null) _view.ApplyMetrics(update.Metrics.Value);
            }

            public void Dispose()
            {
                _view.Detach();
                _sampler.Reset();
                Root.RemoveFromHierarchy();
            }
        }
    }
}
