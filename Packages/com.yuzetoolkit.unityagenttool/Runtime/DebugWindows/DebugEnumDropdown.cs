#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;
using YuzeToolkit.UnityAgent;

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
        private readonly Label _buttonText;
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
            style.flexGrow = 1;
            style.minWidth = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            _label = new Label(label) { enableRichText = false };
            _label.AddToClassList(DebugWindowUss.EnumLabelClass);
            _label.style.minWidth = 130;
            _label.style.color = AgentUi.TextSecondary;
            _label.style.display = string.IsNullOrWhiteSpace(label) ? DisplayStyle.None : DisplayStyle.Flex;
            Add(_label);

            _button = new Button(TogglePopup)
            {
                name = "unity-debug-tool-enum-button"
            };
            _button.focusable = false;
            _button.tabIndex = -1;
            _button.AddToClassList(DebugWindowUss.EnumButtonClass);
            _button.style.flexGrow = 1;
            _button.style.height = 32;
            _button.style.minWidth = 0;
            _button.style.flexDirection = FlexDirection.Row;
            _button.style.alignItems = Align.Center;
            _button.style.paddingLeft = 10;
            _button.style.paddingRight = 8;
            _button.style.backgroundImage = StyleKeyword.None;
            _button.style.backgroundColor = AgentUi.Input;
            _button.style.borderTopLeftRadius = 8;
            _button.style.borderTopRightRadius = 8;
            _button.style.borderBottomLeftRadius = 8;
            _button.style.borderBottomRightRadius = 8;
            AgentUi.SetBorder(_button, AgentUi.Border, 1);
            _buttonText = new Label { enableRichText = false };
            _buttonText.AddToClassList(DebugWindowUss.EnumButtonTextClass);
            _buttonText.style.flexGrow = 1;
            _buttonText.style.minWidth = 0;
            _buttonText.style.color = AgentUi.Text;
            _button.Add(_buttonText);
            var chevron = new VisualElement { pickingMode = PickingMode.Ignore };
            chevron.AddToClassList(DebugWindowUss.EnumChevronClass);
            chevron.style.width = 8;
            chevron.style.height = 8;
            chevron.style.borderRightWidth = 2;
            chevron.style.borderBottomWidth = 2;
            chevron.style.borderRightColor = AgentUi.TextSecondary;
            chevron.style.borderBottomColor = AgentUi.TextSecondary;
            chevron.style.rotate = new Rotate(new Angle(45, AngleUnit.Degree));
            _button.Add(chevron);
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

            var values = Enum.GetValues(_enumType);
            if (values.Length == 0) return;

            var host = FindPopupHost();
            if (host == null) return;

            var popup = new VisualElement { name = "unity-debug-tool-enum-popup" };
            popup.AddToClassList(DebugWindowUss.EnumPopupClass);
            popup.pickingMode = PickingMode.Position;
            popup.style.position = Position.Absolute;
            popup.style.paddingTop = 4;
            popup.style.paddingBottom = 4;
            popup.style.backgroundColor = AgentUi.Popup;
            popup.style.borderTopLeftRadius = 10;
            popup.style.borderTopRightRadius = 10;
            popup.style.borderBottomLeftRadius = 10;
            popup.style.borderBottomRightRadius = 10;
            AgentUi.SetBorder(popup, AgentUi.BorderStrong, 1);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList(DebugWindowUss.EnumPopupScrollClass);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.focusable = false;
            scroll.tabIndex = -1;
            popup.Add(scroll);

            for (var index = 0; index < values.Length; index++)
            {
                if (values.GetValue(index) is not Enum option) continue;
                var captured = option;
                var item = new Button(() => Select(captured));
                item.focusable = false;
                item.tabIndex = -1;
                item.AddToClassList(DebugWindowUss.EnumPopupItemClass);
                item.EnableInClassList(DebugWindowUss.EnumPopupItemSelectedClass, Equals(captured, _value));
                item.style.height = 36;
                item.style.flexDirection = FlexDirection.Row;
                item.style.alignItems = Align.Center;
                item.style.paddingLeft = 10;
                item.style.paddingRight = 10;
                item.style.backgroundImage = StyleKeyword.None;
                item.style.backgroundColor = Equals(captured, _value) ? AgentUi.Active : AgentUi.Transparent;
                item.style.borderTopWidth = 0;
                item.style.borderRightWidth = 0;
                item.style.borderBottomWidth = 0;
                item.style.borderLeftWidth = 0;
                var itemText = new Label(FormatOption(captured))
                {
                    pickingMode = PickingMode.Ignore,
                    enableRichText = false
                };
                itemText.AddToClassList(DebugWindowUss.EnumPopupItemTextClass);
                itemText.style.flexGrow = 1;
                itemText.style.color = Equals(captured, _value) ? AgentUi.Accent : AgentUi.Text;
                item.Add(itemText);
                var check = new VisualElement { pickingMode = PickingMode.Ignore };
                check.AddToClassList(DebugWindowUss.EnumPopupCheckClass);
                var checkGlyph = new VisualElement { pickingMode = PickingMode.Ignore };
                checkGlyph.AddToClassList(DebugWindowUss.EnumPopupCheckGlyphClass);
                check.Add(checkGlyph);
                item.Add(check);
                scroll.Add(item);
            }

            _popup = popup;
            _popupHost = host;
            host.Add(popup);
            PositionPopup(host, popup, values.Length);
            host.RegisterCallback<PointerDownEvent>(OnHostPointerDown, TrickleDown.TrickleDown);
            host.RegisterCallback<PointerMoveEvent>(OnHostPointerMove, TrickleDown.TrickleDown);
            host.RegisterCallback<KeyDownEvent>(OnHostKeyDown, TrickleDown.TrickleDown);
            host.RegisterCallback<GeometryChangedEvent>(OnHostGeometryChanged);
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
            _buttonText.text = FormatOption(_value);
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
            var availableWidth = Mathf.Max(80f, host.resolvedStyle.width);
            var availableHeight = Mathf.Max(46f, host.resolvedStyle.height);
            var width = Mathf.Min(Mathf.Max(180f, _button.resolvedStyle.width),
                Mathf.Min(320f, Mathf.Max(80f, availableWidth - 16f)));
            var estimatedHeight = Mathf.Min(
                Mathf.Min(320f, Mathf.Max(46f, optionCount * 38f + 8f)),
                Mathf.Max(46f, availableHeight - 16f));
            var left = Mathf.Clamp(origin.x, 8f, Mathf.Max(8f, availableWidth - width - 8f));
            var below = origin.y + _button.resolvedStyle.height + 4f;
            var top = below + estimatedHeight <= availableHeight - 8f
                ? below
                : Mathf.Max(8f, origin.y - estimatedHeight - 4f);

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

        private void OnHostPointerMove(PointerMoveEvent evt)
        {
            if (evt.pressedButtons == 0 || IsDescendantOf(evt.target as VisualElement, _popup)) return;
            ClosePopup();
        }

        private void OnHostKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape) return;
            ClosePopup();
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private void OnHostGeometryChanged(GeometryChangedEvent evt)
        {
            if (evt.oldRect.size == evt.newRect.size) return;
            ClosePopup();
        }

        private void ClosePopup()
        {
            if (_popupHost != null)
            {
                _popupHost.UnregisterCallback<PointerDownEvent>(OnHostPointerDown, TrickleDown.TrickleDown);
                _popupHost.UnregisterCallback<PointerMoveEvent>(OnHostPointerMove, TrickleDown.TrickleDown);
                _popupHost.UnregisterCallback<KeyDownEvent>(OnHostKeyDown, TrickleDown.TrickleDown);
                _popupHost.UnregisterCallback<GeometryChangedEvent>(OnHostGeometryChanged);
            }

            _popup?.RemoveFromHierarchy();
            _popup = null;
            _popupHost = null;
            _button.RemoveFromClassList(DebugWindowUss.EnumButtonOpenClass);
        }

        private static bool IsDescendantOf(VisualElement? target, VisualElement? ancestor)
        {
            if (ancestor == null) return false;
            for (var current = target; current != null; current = current.parent)
                if (current == ancestor)
                    return true;
            return false;
        }
    }
}
