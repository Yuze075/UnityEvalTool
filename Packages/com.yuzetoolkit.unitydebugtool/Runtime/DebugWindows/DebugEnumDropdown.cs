#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    /// <summary>
    /// Runtime-owned enum selector. It intentionally does not use EnumField/GenericDropdownMenu,
    /// because those popups are rendered with Unity's editor/runtime theme outside the control tree.
    /// </summary>
    internal sealed class DebugEnumDropdown : VisualElement
    {
        private readonly Label _label;
        private readonly Button _button;
        private readonly Type _enumType;
        private VisualElement? _popup;
        private VisualElement? _popupHost;
        private Enum _value;

        public DebugEnumDropdown(string label, Type enumType, Enum value)
        {
            if (enumType == null) throw new ArgumentNullException(nameof(enumType));
            if (!enumType.IsEnum) throw new ArgumentException("Enum type is required.", nameof(enumType));
            if (value == null) throw new ArgumentNullException(nameof(value));

            _enumType = enumType;
            _value = value;

            AddToClassList(DebugWindowUss.EnumFieldClass);

            _label = new Label(label);
            _label.AddToClassList(DebugWindowUss.EnumLabelClass);
            _label.style.display = string.IsNullOrWhiteSpace(label) ? DisplayStyle.None : DisplayStyle.Flex;
            Add(_label);

            _button = new Button(TogglePopup)
            {
                name = "unity-debug-tool-enum-button"
            };
            _button.focusable = false;
            _button.tabIndex = -1;
            _button.AddToClassList(DebugWindowUss.EnumButtonClass);
            Add(_button);

            RefreshButton();
            RegisterCallback<DetachFromPanelEvent>(_ => ClosePopup());
        }

        public event Action<Enum>? ValueChanged;

        public void SetValueWithoutNotify(Enum value)
        {
            if (value == null || value.GetType() != _enumType) return;
            _value = value;
            RefreshButton();
        }

        private void TogglePopup()
        {
            if (_popup != null)
            {
                ClosePopup();
                return;
            }

            if (!enabledInHierarchy) return;

            var host = FindPopupHost();
            if (host == null) return;

            var popup = new VisualElement { name = "unity-debug-tool-enum-popup" };
            popup.AddToClassList(DebugWindowUss.EnumPopupClass);
            popup.pickingMode = PickingMode.Position;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList(DebugWindowUss.EnumPopupScrollClass);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.focusable = false;
            scroll.tabIndex = -1;
            popup.Add(scroll);

            var values = Enum.GetValues(_enumType);
            for (var index = 0; index < values.Length; index++)
            {
                if (values.GetValue(index) is not Enum option) continue;
                var captured = option;
                var item = new Button(() => Select(captured))
                {
                    text = FormatOption(captured)
                };
                item.focusable = false;
                item.tabIndex = -1;
                item.AddToClassList(DebugWindowUss.EnumPopupItemClass);
                item.EnableInClassList(DebugWindowUss.EnumPopupItemSelectedClass, Equals(captured, _value));
                scroll.Add(item);
            }

            _popup = popup;
            _popupHost = host;
            host.Add(popup);
            PositionPopup(host, popup, values.Length);
            host.RegisterCallback<PointerDownEvent>(OnHostPointerDown, TrickleDown.TrickleDown);
            _button.AddToClassList(DebugWindowUss.EnumButtonOpenClass);
        }

        private void Select(Enum value)
        {
            if (!Equals(_value, value))
            {
                _value = value;
                RefreshButton();
                ValueChanged?.Invoke(value);
            }

            ClosePopup();
        }

        private void RefreshButton()
        {
            _button.text = FormatOption(_value) + "   ▾";
        }

        private static string FormatOption(Enum value)
        {
            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text) ? Convert.ToInt64(value).ToString() : text;
        }

        private VisualElement? FindPopupHost()
        {
            for (var current = parent; current != null; current = current.parent)
                if (current.ClassListContains(DebugWindowUss.LayerClass))
                    return current;
            return panel?.visualTree;
        }

        private void PositionPopup(VisualElement host, VisualElement popup, int optionCount)
        {
            var origin = _button.ChangeCoordinatesTo(host, Vector2.zero);
            var width = Mathf.Max(180f, _button.resolvedStyle.width);
            var estimatedHeight = Mathf.Min(280f, Mathf.Max(42f, optionCount * 34f + 10f));
            var availableWidth = Mathf.Max(width, host.resolvedStyle.width);
            var availableHeight = Mathf.Max(estimatedHeight, host.resolvedStyle.height);
            var left = Mathf.Clamp(origin.x, 8f, Mathf.Max(8f, availableWidth - width - 8f));
            var below = origin.y + _button.resolvedStyle.height + 5f;
            var top = below + estimatedHeight <= availableHeight - 8f
                ? below
                : Mathf.Max(8f, origin.y - estimatedHeight - 5f);

            popup.style.left = left;
            popup.style.top = top;
            popup.style.width = width;
            popup.style.maxHeight = estimatedHeight;
        }

        private void OnHostPointerDown(PointerDownEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target != null && (IsDescendantOf(target, _popup) || IsDescendantOf(target, _button))) return;
            ClosePopup();
        }

        private void ClosePopup()
        {
            if (_popupHost != null)
                _popupHost.UnregisterCallback<PointerDownEvent>(OnHostPointerDown, TrickleDown.TrickleDown);

            _popup?.RemoveFromHierarchy();
            _popup = null;
            _popupHost = null;
            _button.RemoveFromClassList(DebugWindowUss.EnumButtonOpenClass);
        }

        private static bool IsDescendantOf(VisualElement target, VisualElement? ancestor)
        {
            if (ancestor == null) return false;
            for (var current = target; current != null; current = current.parent)
                if (current == ancestor)
                    return true;
            return false;
        }
    }
}
