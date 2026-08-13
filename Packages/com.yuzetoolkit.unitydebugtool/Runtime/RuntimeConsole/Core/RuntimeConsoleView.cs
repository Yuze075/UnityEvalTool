#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal sealed class RuntimeConsoleView
    {
        private readonly IReadOnlyList<IRuntimeConsoleTab> _tabs;
        private readonly GameObject _eventSystemOwner;
        private readonly Dictionary<string, Button> _buttons = new();
        private VisualElement? _layer;
        private VisualElement? _window;
        private VisualElement? _content;
        private IRuntimeConsoleTab? _activeTab;
        private TextField? _activeTextField;

        public RuntimeConsoleView(IReadOnlyList<IRuntimeConsoleTab> tabs, GameObject eventSystemOwner)
        {
            _tabs = tabs;
            _eventSystemOwner = eventSystemOwner;
        }

        public void AttachTo(VisualElement layer)
        {
            if (_tabs.Count == 0) return;

            _layer = layer;
            _window = new VisualElement
            {
                name = "unity-debug-tool-runtime-console",
                pickingMode = PickingMode.Position
            };
            _window.AddToClassList(RuntimeConsoleUss.WindowClass);
            InstallInteractionPolicy(_window);
            layer.Add(_window);
            layer.RegisterCallback<GeometryChangedEvent>(OnLayerGeometryChanged);

            var tabBar = new VisualElement { name = "runtime-console-tab-bar" };
            tabBar.AddToClassList(RuntimeConsoleUss.TabBarClass);
            tabBar.AddManipulator(new RuntimeConsoleDragManipulator(tabBar, _window));
            _window.Add(tabBar);

            _content = new VisualElement { name = "runtime-console-content" };
            _content.AddToClassList(RuntimeConsoleUss.ContentClass);
            _window.Add(_content);

            foreach (var tab in _tabs)
            {
                var captured = tab;
                var button = new Button(() => SetActiveTab(captured))
                {
                    text = tab.Title
                };
                button.AddToClassList(RuntimeConsoleUss.TabButtonClass);
                DisableKeyboardFocus(button);
                RuntimeConsoleUi.AttachHelp(button, $"Show {tab.Title} tab.");
                tabBar.Add(button);
                _buttons[tab.Id] = button;

                tab.SetVisible(false);
                _content.Add(tab.Root);
            }

            var resizeGrip = new VisualElement
            {
                name = "runtime-console-resize-grip"
            };
            resizeGrip.AddToClassList(RuntimeConsoleUss.ResizeGripClass);
            RuntimeConsoleUi.AttachHelp(resizeGrip, "Drag to resize the Runtime Console.");
            resizeGrip.AddManipulator(new RuntimeConsoleResizeManipulator(resizeGrip, _window));
            _window.Add(resizeGrip);

            SetActiveTab(_tabs[0]);
        }

        public void Detach()
        {
            ReleaseInteractionFocus();
            _layer?.UnregisterCallback<GeometryChangedEvent>(OnLayerGeometryChanged);
            _layer = null;
            _window?.RemoveFromHierarchy();
            _window = null;
            _content = null;
            _activeTab = null;
            _activeTextField = null;
            _buttons.Clear();
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
                ReleaseInteractionFocus();
            if (_window != null)
                _window.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _activeTab?.SetVisible(visible);
        }

        public void Tick()
        {
            _activeTab?.Tick();
        }

        private void SetActiveTab(IRuntimeConsoleTab tab)
        {
            ReleaseInteractionFocus();
            _activeTab?.SetVisible(false);
            _activeTab = tab;
            _activeTab.SetVisible(true);

            foreach (var pair in _buttons)
            {
                if (pair.Key == tab.Id)
                    pair.Value.AddToClassList(RuntimeConsoleUss.ActiveTabButtonClass);
                else
                    pair.Value.RemoveFromClassList(RuntimeConsoleUss.ActiveTabButtonClass);
            }
        }

        private void OnLayerGeometryChanged(GeometryChangedEvent evt)
        {
            ClampWindowToLayer();
        }

        private void ClampWindowToLayer()
        {
            if (_layer == null || _window == null) return;

            var parentRect = _layer.contentRect;
            if (parentRect.width <= 0f || parentRect.height <= 0f) return;

            var maxWidth = Mathf.Max(RuntimeConsoleResizeManipulator.MinWidth, parentRect.width - RuntimeConsoleResizeManipulator.EdgePadding);
            var maxHeight = Mathf.Max(RuntimeConsoleResizeManipulator.MinHeight, parentRect.height - RuntimeConsoleResizeManipulator.EdgePadding);
            var width = Mathf.Clamp(_window.resolvedStyle.width, RuntimeConsoleResizeManipulator.MinWidth, maxWidth);
            var height = Mathf.Clamp(_window.resolvedStyle.height, RuntimeConsoleResizeManipulator.MinHeight, maxHeight);
            _window.style.width = width;
            _window.style.height = height;
        }

        private void InstallInteractionPolicy(VisualElement root)
        {
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                ClearEventSystemSelection(force: true);
                var textField = evt.button == 0 ? FindTextField(evt.target as VisualElement, root) : null;
                if (textField != null && textField.enabledInHierarchy)
                {
                    _activeTextField = textField;
                    return;
                }

                ReleaseInteractionFocus();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(evt =>
            {
                ClearEventSystemSelection(force: true);
                if (FindTextField(evt.target as VisualElement, root) == null)
                    ReleaseInteractionFocus();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusInEvent>(evt =>
            {
                if (_activeTextField != null && IsDescendantOf(evt.target as VisualElement, _activeTextField)) return;
                if (evt.target is VisualElement focused)
                    focused.schedule.Execute(focused.Blur);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (_activeTextField != null && IsDescendantOf(evt.target as VisualElement, _activeTextField))
                    _activeTextField = null;
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (_activeTextField != null && IsDescendantOf(evt.target as VisualElement, _activeTextField))
                {
                    if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    {
                        var submittedField = _activeTextField;
                        _activeTextField = null;
                        submittedField.schedule.Execute(() =>
                        {
                            submittedField.Blur();
                            ClearEventSystemSelection(force: false);
                        });
                    }

                    // Keep propagating so a Command Line field can submit at its target callback.
                    return;
                }
                evt.PreventDefault();
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyUpEvent>(evt =>
            {
                if (_activeTextField != null && IsDescendantOf(evt.target as VisualElement, _activeTextField)) return;
                evt.PreventDefault();
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(SuppressNavigation, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(SuppressNavigation, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationCancelEvent>(SuppressNavigation, TrickleDown.TrickleDown);
        }

        private void ReleaseInteractionFocus()
        {
            _activeTextField?.Blur();
            _activeTextField = null;
            if (_window?.panel?.focusController.focusedElement is VisualElement focused &&
                IsDescendantOf(focused, _window))
                focused.Blur();
            ClearEventSystemSelection(force: false);
        }

        private void ClearEventSystemSelection(bool force)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            if (force || eventSystem.currentSelectedGameObject == _eventSystemOwner)
                eventSystem.SetSelectedGameObject(null);
        }

        private static TextField? FindTextField(VisualElement? target, VisualElement root)
        {
            for (var current = target; current != null && current != root; current = current.parent)
                if (current is TextField textField)
                    return textField;
            return null;
        }

        private static bool IsDescendantOf(VisualElement? target, VisualElement ancestor)
        {
            for (var current = target; current != null; current = current.parent)
                if (current == ancestor)
                    return true;
            return false;
        }

        private static void DisableKeyboardFocus(VisualElement element)
        {
            element.focusable = false;
            element.tabIndex = -1;
        }

        private static void SuppressNavigation(EventBase evt)
        {
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }
    }

    internal sealed class RuntimeConsoleResizeManipulator : PointerManipulator
    {
        internal const float MinWidth = 320f;
        internal const float MinHeight = 260f;
        internal const float EdgePadding = 16f;

        private readonly VisualElement _resizeTarget;
        private bool _active;
        private Vector2 _startPointer;
        private Vector2 _startSize;

        public RuntimeConsoleResizeManipulator(VisualElement resizeHandle, VisualElement resizeTarget)
        {
            target = resizeHandle;
            _resizeTarget = resizeTarget;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            _active = true;
            _startPointer = evt.position;
            _startSize = new Vector2(_resizeTarget.resolvedStyle.width, _resizeTarget.resolvedStyle.height);
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_active || !target.HasPointerCapture(evt.pointerId)) return;

            var delta = (Vector2)evt.position - _startPointer;
            var maxSize = GetMaxSize();
            var width = Mathf.Clamp(_startSize.x + delta.x, MinWidth, maxSize.x);
            var height = Mathf.Clamp(_startSize.y - delta.y, MinHeight, maxSize.y);
            _resizeTarget.style.width = width;
            _resizeTarget.style.height = height;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            Finish(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            Finish(evt.pointerId);
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _active = false;
        }

        private void Finish(int pointerId)
        {
            if (!_active) return;
            _active = false;
            if (target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);
        }

        private Vector2 GetMaxSize()
        {
            if (_resizeTarget.parent == null)
                return new Vector2(float.MaxValue, float.MaxValue);

            var parentRect = _resizeTarget.parent.contentRect;
            var layout = _resizeTarget.layout;
            var maxWidth = Mathf.Max(MinWidth, parentRect.width - layout.xMin - EdgePadding);
            var maxHeight = Mathf.Max(MinHeight, layout.yMax - EdgePadding);
            return new Vector2(maxWidth, maxHeight);
        }
    }

    internal sealed class RuntimeConsoleDragManipulator : PointerManipulator
    {
        private const float DragStartThresholdSqr = 9f;

        private readonly VisualElement _dragTarget;
        private bool _active;
        private bool _dragging;
        private int _pointerId;
        private Vector2 _startPointer;
        private float _startLeft;
        private float _startBottom;

        public RuntimeConsoleDragManipulator(VisualElement dragHandle, VisualElement dragTarget)
        {
            target = dragHandle;
            _dragTarget = dragTarget;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || IsInteractiveTarget(evt.target as VisualElement)) return;

            _active = true;
            _dragging = false;
            _pointerId = evt.pointerId;
            _startPointer = evt.position;
            _startLeft = _dragTarget.layout.xMin;
            _startBottom = _dragTarget.parent == null
                ? 0f
                : Mathf.Max(0f, _dragTarget.parent.contentRect.height - _dragTarget.layout.yMax);
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_active || _pointerId != evt.pointerId || !target.HasPointerCapture(evt.pointerId)) return;

            var delta = (Vector2)evt.position - _startPointer;
            if (!_dragging && delta.sqrMagnitude < DragStartThresholdSqr) return;
            _dragging = true;

            var nextLeft = _startLeft + delta.x;
            var nextBottom = _startBottom - delta.y;
            if (_dragTarget.parent != null)
            {
                var parentRect = _dragTarget.parent.contentRect;
                var width = Mathf.Max(24f, _dragTarget.resolvedStyle.width);
                var height = Mathf.Max(24f, _dragTarget.resolvedStyle.height);
                nextLeft = Mathf.Clamp(nextLeft, 0f, Mathf.Max(0f, parentRect.width - width));
                nextBottom = Mathf.Clamp(nextBottom, 0f, Mathf.Max(0f, parentRect.height - height));
            }

            _dragTarget.style.left = nextLeft;
            _dragTarget.style.bottom = nextBottom;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_pointerId != evt.pointerId) return;
            Finish(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (_pointerId != evt.pointerId) return;
            Finish(evt.pointerId);
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _active = false;
            _dragging = false;
        }

        private void Finish(int pointerId)
        {
            _active = false;
            _dragging = false;
            if (target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);
        }

        private static bool IsInteractiveTarget(VisualElement? element)
        {
            while (element != null)
            {
                if (element is Button || element is TextField || element is Toggle)
                    return true;
                element = element.parent;
            }

            return false;
        }
    }
}
