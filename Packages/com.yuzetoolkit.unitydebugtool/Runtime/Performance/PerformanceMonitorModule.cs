#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    public sealed class PerformanceMonitorModule : MonoBehaviour, IDebugPanelModule
    {
        [SerializeField, Tooltip("UXML template used by the performance monitor module.")]
        private VisualTreeAsset? template;

        [SerializeField, Tooltip("USS used by the performance monitor module.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Keyboard key used with the DebugPanel modifiers to show or hide the performance monitor module.")]
        private Key toggleKey = Key.F10;

        private readonly PerformanceSampler _sampler = new();
        private PerformanceMonitorView? _view;
        private VisualElement? _layer;

        public int SortOrder => 0;

        public Key ToggleKey => toggleKey;

        public void Initialize(DebugPanelContext context)
        {
            if (template == null || styleSheet == null)
            {
                Debug.LogError($"{nameof(PerformanceMonitorModule)} requires UXML and USS references.", this);
                return;
            }

            context.AddStyleSheet(styleSheet);
            try
            {
                _layer = context.CreateLayer("unity-debug-tool-performance-layer");
                PerformanceMonitorUss.ApplyLayer(_layer);
                _view = new PerformanceMonitorView(template);
                _view.AttachTo(_layer);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"{nameof(PerformanceMonitorModule)} failed to initialize: {exception.Message}", this);
                Shutdown();
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
            _layer?.RemoveFromHierarchy();
            _layer = null;
            _sampler.Reset();
        }
    }
}
