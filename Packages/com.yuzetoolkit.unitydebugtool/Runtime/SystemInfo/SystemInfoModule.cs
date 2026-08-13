#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

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
            _layer?.RemoveFromHierarchy();
            _layer = null;
            _refreshTimer = 0f;
        }

        private void Refresh()
        {
            _refreshTimer = 0f;
            _view?.ApplySnapshot(SystemInfoRegistry.CaptureSnapshot());
        }
    }
}
