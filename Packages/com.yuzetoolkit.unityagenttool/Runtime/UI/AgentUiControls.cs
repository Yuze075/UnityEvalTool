#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit.UnityAgent
{
    /// <summary>
    /// Package-owned button. It intentionally does not derive from UI Toolkit's Button, so no
    /// editor skin, background image, padding, border, or state selector can leak into the Agent UI.
    /// </summary>
    internal sealed class AgentButton : VisualElement
    {
        private readonly Label _label;
        private readonly Action _clicked;
        private string _helpText;
        private Color _surface;
        private Color _foreground;
        private bool _hovered;
        private bool _pressed;

        public AgentButton(string text, string tooltip, Action clicked, Color surface, Color foreground)
        {
            _clicked = clicked ?? throw new ArgumentNullException(nameof(clicked));
            _helpText = tooltip ?? string.Empty;
            AgentTooltip.Attach(this, () => _helpText);
            focusable = true;
            pickingMode = PickingMode.Position;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.flexShrink = 0;
            style.backgroundImage = StyleKeyword.None;
            style.borderTopWidth = 0;
            style.borderRightWidth = 0;
            style.borderBottomWidth = 0;
            style.borderLeftWidth = 0;
            style.paddingTop = 0;
            style.paddingRight = 10;
            style.paddingBottom = 0;
            style.paddingLeft = 10;
            style.opacity = 1;

            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.style.flexShrink = 1;
            _label.style.minWidth = 0;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.whiteSpace = WhiteSpace.NoWrap;
            _label.style.backgroundImage = StyleKeyword.None;
            _label.style.marginTop = 0;
            _label.style.marginRight = 0;
            _label.style.marginBottom = 0;
            _label.style.marginLeft = 0;
            _label.style.paddingTop = 0;
            _label.style.paddingRight = 0;
            _label.style.paddingBottom = 0;
            _label.style.paddingLeft = 0;
            Add(_label);

            SetPalette(surface, foreground);
            this.text = text;

            RegisterCallback<PointerEnterEvent>(_ =>
            {
                _hovered = true;
                RefreshSurface();
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _hovered = false;
                _pressed = false;
                RefreshSurface();
            });
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || !enabledInHierarchy) return;
                _pressed = true;
                Focus();
                RefreshSurface();
                evt.StopPropagation();
            });
            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 0 || !_pressed) return;
                _pressed = false;
                RefreshSurface();
                if (worldBound.Contains(evt.position) && enabledInHierarchy) _clicked();
                evt.StopPropagation();
            });
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!enabledInHierarchy || evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
                _clicked();
                evt.StopPropagation();
            });
            RegisterCallback<FocusInEvent>(_ =>
            {
                style.borderTopWidth = 1;
                style.borderRightWidth = 1;
                style.borderBottomWidth = 1;
                style.borderLeftWidth = 1;
                style.borderTopColor = AgentUi.Focus;
                style.borderRightColor = AgentUi.Focus;
                style.borderBottomColor = AgentUi.Focus;
                style.borderLeftColor = AgentUi.Focus;
            });
            RegisterCallback<FocusOutEvent>(_ =>
            {
                style.borderTopWidth = 0;
                style.borderRightWidth = 0;
                style.borderBottomWidth = 0;
                style.borderLeftWidth = 0;
            });
        }

        public string text
        {
            get => _label.text;
            set => _label.text = value ?? string.Empty;
        }

        public string HelpText
        {
            get => _helpText;
            set => _helpText = value ?? string.Empty;
        }

        public void SetPalette(Color surface, Color foreground)
        {
            _surface = surface;
            _foreground = foreground;
            _label.style.color = foreground;
            style.color = foreground;
            RefreshSurface();
        }

        public void SetBackground(Color surface)
        {
            _surface = surface;
            RefreshSurface();
        }

        public new void SetEnabled(bool value)
        {
            base.SetEnabled(value);
            style.opacity = value ? 1f : 0.42f;
            RefreshSurface();
        }

        private void RefreshSurface()
        {
            var target = _pressed
                ? Color.Lerp(_surface, Color.black, 0.18f)
                : _hovered
                    ? Color.Lerp(_surface, Color.white, 0.10f)
                    : _surface;
            style.backgroundColor = target;
            _label.style.color = enabledInHierarchy ? _foreground : Color.Lerp(_foreground, AgentUi.Muted, 0.55f);
        }
    }

    /// <summary>
    /// TextField with every visual sub-part restyled by the package. Text editing remains native so
    /// IME, selection, clipboard, password fields, and multiline behavior work in Editor and Player.
    /// </summary>
    internal sealed class AgentTextField : TextField
    {
        private VisualElement? _input;
        private readonly Label _placeholder;
        private bool _surface;
        private bool _isFocused;
        private bool _invalid;

        public AgentTextField(string label = "", bool surface = true) : base(label)
        {
            _surface = surface;
            style.minWidth = 0;
            style.flexShrink = 1;
            style.flexDirection = FlexDirection.Column;
            style.backgroundImage = StyleKeyword.None;
            style.backgroundColor = AgentUi.Transparent;
            style.borderTopWidth = 0;
            style.borderRightWidth = 0;
            style.borderBottomWidth = 0;
            style.borderLeftWidth = 0;
            style.marginTop = 0;
            style.marginRight = 0;
            style.marginBottom = 0;
            style.marginLeft = 0;
            style.paddingTop = 0;
            style.paddingRight = 0;
            style.paddingBottom = 0;
            style.paddingLeft = 0;
            style.opacity = 1;

            labelElement.style.width = StyleKeyword.Auto;
            labelElement.style.minWidth = 0;
            labelElement.style.flexGrow = 0;
            labelElement.style.flexShrink = 0;
            labelElement.style.fontSize = 11;
            labelElement.style.color = AgentUi.Muted;
            labelElement.style.marginTop = 0;
            labelElement.style.marginRight = 0;
            labelElement.style.marginBottom = string.IsNullOrEmpty(label) ? 0 : 6;
            labelElement.style.marginLeft = 1;
            labelElement.style.paddingTop = 0;
            labelElement.style.paddingRight = 0;
            labelElement.style.paddingBottom = 0;
            labelElement.style.paddingLeft = 0;
            labelElement.style.backgroundImage = StyleKeyword.None;
            labelElement.style.backgroundColor = AgentUi.Transparent;
            labelElement.style.borderTopWidth = 0;
            labelElement.style.borderRightWidth = 0;
            labelElement.style.borderBottomWidth = 0;
            labelElement.style.borderLeftWidth = 0;
            if (string.IsNullOrEmpty(label)) labelElement.style.display = DisplayStyle.None;

            _placeholder = new Label { pickingMode = PickingMode.Ignore };
            _placeholder.style.position = Position.Absolute;
            _placeholder.style.left = 11;
            _placeholder.style.top = string.IsNullOrEmpty(label) ? 10 : 31;
            _placeholder.style.color = AgentUi.Placeholder;
            _placeholder.style.whiteSpace = WhiteSpace.NoWrap;
            _placeholder.style.display = DisplayStyle.None;
            Add(_placeholder);

            this.RegisterValueChangedCallback(_ => RefreshPlaceholder());
            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(StyleNativeInput));
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_input == null) StyleNativeInput();
            });
            RegisterCallback<PointerEnterEvent>(_ => SetInputBorder(_invalid ? AgentUi.Error : AgentUi.BorderStrong));
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (!_isFocused) SetInputBorder(_invalid ? AgentUi.Error : AgentUi.Border);
            });
            RegisterCallback<FocusInEvent>(_ =>
            {
                _isFocused = true;
                SetInputBorder(_invalid ? AgentUi.Error : AgentUi.Focus);
                RefreshPlaceholder();
            });
            RegisterCallback<FocusOutEvent>(_ =>
            {
                _isFocused = false;
                SetInputBorder(_invalid ? AgentUi.Error : AgentUi.Border);
                RefreshPlaceholder();
            });
            RegisterCallback<ContextualMenuPopulateEvent>(evt => evt.StopImmediatePropagation(),
                TrickleDown.TrickleDown);
            schedule.Execute(StyleNativeInput);
        }

        public string Placeholder
        {
            get => _placeholder.text;
            set
            {
                _placeholder.text = value ?? string.Empty;
                RefreshPlaceholder();
            }
        }

        public void SetSurface(bool enabled)
        {
            _surface = enabled;
            StyleNativeInput();
        }

        public void SetInvalid(bool invalid)
        {
            _invalid = invalid;
            if (_input != null)
                _input.style.backgroundColor = invalid ? AgentUi.ErrorPanel : (_surface ? AgentUi.Input : AgentUi.Transparent);
            SetInputBorder(invalid ? AgentUi.Error : _isFocused ? AgentUi.Focus : AgentUi.Border);
        }

        public new void SetEnabled(bool value)
        {
            base.SetEnabled(value);
            style.opacity = value ? 1f : 0.42f;
        }

        private void StyleNativeInput()
        {
            _input = this.Q<VisualElement>(className: "unity-base-text-field__input")
                     ?? this.Q<VisualElement>(className: "unity-text-field__input")
                     ?? this.Q<VisualElement>(className: "unity-text-input");
            if (_input == null) return;
            var textElement = _input.Q<TextElement>();
            if (textElement != null)
            {
                textElement.style.backgroundImage = StyleKeyword.None;
                textElement.style.backgroundColor = AgentUi.Transparent;
                textElement.style.color = AgentUi.Text;
                StyleTextSelection(textElement);
            }
            _input.style.flexGrow = 1;
            _input.style.minWidth = 0;
            _input.style.minHeight = _surface ? 38 : 32;
            _input.style.backgroundImage = StyleKeyword.None;
            _input.style.backgroundColor = _invalid
                ? AgentUi.ErrorPanel
                : _surface ? AgentUi.Input : AgentUi.Transparent;
            _input.style.color = AgentUi.Text;
            _input.style.marginTop = 0;
            _input.style.marginRight = 0;
            _input.style.marginBottom = 0;
            _input.style.marginLeft = 0;
            _input.style.paddingTop = _surface ? 8 : 5;
            _input.style.paddingRight = _surface ? 10 : 4;
            _input.style.paddingBottom = _surface ? 8 : 5;
            _input.style.paddingLeft = _surface ? 10 : 4;
            _input.style.borderTopLeftRadius = _surface ? 9 : 0;
            _input.style.borderTopRightRadius = _surface ? 9 : 0;
            _input.style.borderBottomLeftRadius = _surface ? 9 : 0;
            _input.style.borderBottomRightRadius = _surface ? 9 : 0;
            SetInputBorder(_invalid ? AgentUi.Error : _surface ? AgentUi.Border : AgentUi.Transparent);

            foreach (var child in _input.Children())
            {
                child.style.backgroundImage = StyleKeyword.None;
                child.style.backgroundColor = AgentUi.Transparent;
                child.style.color = AgentUi.Text;
                child.style.marginTop = 0;
                child.style.marginRight = 0;
                child.style.marginBottom = 0;
                child.style.marginLeft = 0;
            }
            RefreshPlaceholder();
        }

        private static void StyleTextSelection(TextElement textElement)
        {
            // Unity 2022 exposes these through TextElement's explicit ITextSelection implementation
            // in some player profiles, while newer profiles expose public properties. Reflection
            // keeps this Runtime assembly portable without falling back to Unity's skin colors.
            var interfaceType = typeof(TextElement).Assembly.GetType("UnityEngine.UIElements.ITextSelection");
            if (interfaceType == null || !interfaceType.IsInstanceOfType(textElement)) return;
            interfaceType.GetProperty("cursorColor", BindingFlags.Instance | BindingFlags.Public)?
                .SetValue(textElement, AgentUi.Text, null);
            interfaceType.GetProperty("selectionColor", BindingFlags.Instance | BindingFlags.Public)?
                .SetValue(textElement, new Color(1f, 0.36f, 0.13f, 0.42f), null);
        }

        private void SetInputBorder(Color color)
        {
            if (_input == null || !_surface) return;
            _input.style.borderTopWidth = 1;
            _input.style.borderRightWidth = 1;
            _input.style.borderBottomWidth = 1;
            _input.style.borderLeftWidth = 1;
            _input.style.borderTopColor = color;
            _input.style.borderRightColor = color;
            _input.style.borderBottomColor = color;
            _input.style.borderLeftColor = color;
        }

        private void RefreshPlaceholder()
        {
            _placeholder.style.display = !string.IsNullOrEmpty(_placeholder.text) &&
                                         string.IsNullOrEmpty(value)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    /// <summary>Package-owned dropdown whose choices are rendered by the owned overlay layer.</summary>
    internal sealed class AgentChoiceField : VisualElement, INotifyValueChanged<string>
    {
        private readonly Label _caption;
        private readonly Label _valueLabel;
        private readonly Label _arrow;
        private readonly VisualElement _trigger;
        private readonly bool _compact;
        private List<string> _choices = new();
        private string _value = string.Empty;
        private bool _hovered;

        public AgentChoiceField(string label, IEnumerable<string> choices, bool compact = false)
        {
            _compact = compact;
            style.minWidth = 0;
            style.flexShrink = 1;
            style.backgroundImage = StyleKeyword.None;
            style.opacity = 1;
            style.marginTop = compact ? 0 : 4;
            style.marginBottom = compact ? 0 : 4;

            _caption = new Label(label) { pickingMode = PickingMode.Ignore };
            _caption.style.fontSize = 11;
            _caption.style.color = AgentUi.Muted;
            _caption.style.marginLeft = 1;
            _caption.style.marginBottom = 6;
            _caption.style.display = string.IsNullOrEmpty(label) ? DisplayStyle.None : DisplayStyle.Flex;
            Add(_caption);

            _trigger = new VisualElement { focusable = true };
            _trigger.style.height = compact ? 30 : 38;
            _trigger.style.minWidth = 0;
            _trigger.style.flexDirection = FlexDirection.Row;
            _trigger.style.alignItems = Align.Center;
            _trigger.style.backgroundImage = StyleKeyword.None;
            _trigger.style.backgroundColor = compact ? AgentUi.Transparent : AgentUi.Input;
            _trigger.style.borderTopLeftRadius = compact ? 7 : 9;
            _trigger.style.borderTopRightRadius = compact ? 7 : 9;
            _trigger.style.borderBottomLeftRadius = compact ? 7 : 9;
            _trigger.style.borderBottomRightRadius = compact ? 7 : 9;
            _trigger.style.borderTopWidth = compact ? 0 : 1;
            _trigger.style.borderRightWidth = compact ? 0 : 1;
            _trigger.style.borderBottomWidth = compact ? 0 : 1;
            _trigger.style.borderLeftWidth = compact ? 0 : 1;
            _trigger.style.borderTopColor = AgentUi.Border;
            _trigger.style.borderRightColor = AgentUi.Border;
            _trigger.style.borderBottomColor = AgentUi.Border;
            _trigger.style.borderLeftColor = AgentUi.Border;
            _trigger.style.paddingLeft = compact ? 7 : 10;
            _trigger.style.paddingRight = compact ? 6 : 9;
            Add(_trigger);

            _valueLabel = new Label { pickingMode = PickingMode.Ignore };
            _valueLabel.style.flexGrow = 1;
            _valueLabel.style.minWidth = 0;
            _valueLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _valueLabel.style.overflow = Overflow.Hidden;
            _valueLabel.style.textOverflow = TextOverflow.Ellipsis;
            _valueLabel.style.color = AgentUi.Text;
            _trigger.Add(_valueLabel);
            _arrow = new Label("⌄") { pickingMode = PickingMode.Ignore };
            _arrow.style.width = 16;
            _arrow.style.marginLeft = 5;
            _arrow.style.color = AgentUi.Muted;
            _arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            _trigger.Add(_arrow);

            this.choices = choices.ToList();
            if (_choices.Count > 0) SetValueWithoutNotify(_choices[0]);

            _trigger.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _hovered = true;
                RefreshTrigger();
            });
            _trigger.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _hovered = false;
                RefreshTrigger();
            });
            _trigger.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || !enabledInHierarchy) return;
                ShowMenu();
                evt.StopPropagation();
            });
            _trigger.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!enabledInHierarchy || evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
                ShowMenu();
                evt.StopPropagation();
            });
            _trigger.RegisterCallback<FocusInEvent>(_ => SetBorder(AgentUi.Focus));
            _trigger.RegisterCallback<FocusOutEvent>(_ => RefreshTrigger());
        }

        public string label
        {
            get => _caption.text;
            set
            {
                _caption.text = value ?? string.Empty;
                _caption.style.display = string.IsNullOrEmpty(_caption.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public List<string> choices
        {
            get => _choices;
            set
            {
                _choices = value?.Where(item => item != null).ToList() ?? new List<string>();
                RefreshValueLabel();
            }
        }

        public Func<string, string>? ValueFormatter { get; set; }

        public string value
        {
            get => _value;
            set
            {
                value ??= string.Empty;
                if (string.Equals(_value, value, StringComparison.Ordinal)) return;
                var previous = _value;
                SetValueWithoutNotify(value);
                using var evt = ChangeEvent<string>.GetPooled(previous, _value);
                evt.target = this;
                SendEvent(evt);
            }
        }

        public void SetValueWithoutNotify(string newValue)
        {
            _value = newValue ?? string.Empty;
            RefreshValueLabel();
        }

        public void SetForeground(Color color)
        {
            _valueLabel.style.color = color;
            _arrow.style.color = color;
        }

        public new void SetEnabled(bool value)
        {
            base.SetEnabled(value);
            style.opacity = value ? 1f : 0.42f;
            _trigger.pickingMode = value ? PickingMode.Position : PickingMode.Ignore;
        }

        private void ShowMenu()
        {
            var options = _choices.Select(choice => new AgentMenuItem(
                string.IsNullOrEmpty(choice) ? "Default" : choice,
                () => value = choice,
                string.Equals(choice, _value, StringComparison.Ordinal))).ToList();
            if (options.Count == 0)
                options.Add(new AgentMenuItem("No options available", null, false, false, true));
            AgentPopupMenu.Show(_trigger, options, Math.Max(180, Mathf.RoundToInt(worldBound.width)));
        }

        private void RefreshValueLabel()
        {
            var formatted = ValueFormatter?.Invoke(_value) ?? _value;
            _valueLabel.text = string.IsNullOrEmpty(formatted) ? "Default" : formatted;
        }

        private void RefreshTrigger()
        {
            _trigger.style.backgroundColor = _hovered
                ? (_compact ? AgentUi.Hover : AgentUi.InputHover)
                : (_compact ? AgentUi.Transparent : AgentUi.Input);
            SetBorder(_compact ? AgentUi.Transparent : AgentUi.Border);
        }

        private void SetBorder(Color color)
        {
            if (_compact) return;
            _trigger.style.borderTopColor = color;
            _trigger.style.borderRightColor = color;
            _trigger.style.borderBottomColor = color;
            _trigger.style.borderLeftColor = color;
        }
    }

    internal sealed class AgentIntegerField : VisualElement, INotifyValueChanged<int>
    {
        private readonly AgentTextField _field;
        private int _value;
        private bool _invalid;

        public AgentIntegerField(string label)
        {
            style.minWidth = 0;
            style.marginTop = 4;
            style.marginBottom = 4;
            _field = new AgentTextField(label);
            _field.style.flexGrow = 1;
            _field.RegisterValueChangedCallback(evt =>
            {
                if (!int.TryParse(evt.newValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    _invalid = true;
                    _field.SetInvalid(true);
                    return;
                }
                _invalid = false;
                _field.SetInvalid(false);
                value = parsed;
            });
            _field.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!_invalid) return;
                _invalid = false;
                _field.SetInvalid(false);
                _field.SetValueWithoutNotify(_value.ToString(CultureInfo.InvariantCulture));
            });
            Add(_field);
        }

        public int value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                var previous = _value;
                SetValueWithoutNotify(value);
                using var evt = ChangeEvent<int>.GetPooled(previous, _value);
                evt.target = this;
                SendEvent(evt);
            }
        }

        public void SetValueWithoutNotify(int newValue)
        {
            _value = newValue;
            _field.SetValueWithoutNotify(newValue.ToString(CultureInfo.InvariantCulture));
        }

        public new void SetEnabled(bool value)
        {
            base.SetEnabled(value);
            style.opacity = value ? 1f : 0.42f;
        }
    }

    internal sealed class AgentToggle : VisualElement, INotifyValueChanged<bool>
    {
        private readonly VisualElement _track;
        private readonly VisualElement _knob;
        private bool _value;
        private bool _hovered;
        private bool _pressed;

        public AgentToggle(string label)
        {
            focusable = true;
            style.height = 32;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.flexShrink = 0;
            style.paddingLeft = 3;
            style.paddingRight = 3;
            var caption = new Label(label) { pickingMode = PickingMode.Ignore };
            caption.style.color = AgentUi.Muted;
            caption.style.marginRight = 8;
            Add(caption);
            _track = new VisualElement { pickingMode = PickingMode.Ignore };
            _track.style.width = 34;
            _track.style.height = 18;
            _track.style.borderTopLeftRadius = 9;
            _track.style.borderTopRightRadius = 9;
            _track.style.borderBottomLeftRadius = 9;
            _track.style.borderBottomRightRadius = 9;
            _track.style.justifyContent = Justify.Center;
            Add(_track);
            _knob = new VisualElement { pickingMode = PickingMode.Ignore };
            _knob.style.position = Position.Absolute;
            _knob.style.top = 3;
            _knob.style.width = 12;
            _knob.style.height = 12;
            _knob.style.borderTopLeftRadius = 6;
            _knob.style.borderTopRightRadius = 6;
            _knob.style.borderBottomLeftRadius = 6;
            _knob.style.borderBottomRightRadius = 6;
            _track.Add(_knob);

            RegisterCallback<PointerEnterEvent>(_ =>
            {
                _hovered = true;
                RefreshVisual();
            });
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _hovered = false;
                RefreshVisual();
            });
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || !enabledInHierarchy) return;
                _pressed = true;
                Focus();
                RefreshVisual();
                evt.StopPropagation();
            });
            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 0 || !_pressed) return;
                _pressed = false;
                if (worldBound.Contains(evt.position) && enabledInHierarchy) value = !value;
                RefreshVisual();
                evt.StopPropagation();
            });
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!enabledInHierarchy || evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
                value = !value;
                evt.StopPropagation();
            });
            RegisterCallback<FocusInEvent>(_ =>
            {
                _track.style.borderTopWidth = 1;
                _track.style.borderRightWidth = 1;
                _track.style.borderBottomWidth = 1;
                _track.style.borderLeftWidth = 1;
                _track.style.borderTopColor = AgentUi.Focus;
                _track.style.borderRightColor = AgentUi.Focus;
                _track.style.borderBottomColor = AgentUi.Focus;
                _track.style.borderLeftColor = AgentUi.Focus;
            });
            RegisterCallback<FocusOutEvent>(_ =>
            {
                _pressed = false;
                _track.style.borderTopWidth = 0;
                _track.style.borderRightWidth = 0;
                _track.style.borderBottomWidth = 0;
                _track.style.borderLeftWidth = 0;
                RefreshVisual();
            });
            RefreshVisual();
        }

        public bool value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                var previous = _value;
                SetValueWithoutNotify(value);
                using var evt = ChangeEvent<bool>.GetPooled(previous, _value);
                evt.target = this;
                SendEvent(evt);
            }
        }

        public void SetValueWithoutNotify(bool newValue)
        {
            _value = newValue;
            RefreshVisual();
        }

        public new void SetEnabled(bool value)
        {
            base.SetEnabled(value);
            style.opacity = value ? 1f : 0.42f;
            pickingMode = value ? PickingMode.Position : PickingMode.Ignore;
        }

        private void RefreshVisual()
        {
            var baseColor = _value ? AgentUi.Accent : AgentUi.BorderStrong;
            _track.style.backgroundColor = _pressed
                ? Color.Lerp(baseColor, Color.black, 0.18f)
                : _hovered
                    ? Color.Lerp(baseColor, Color.white, 0.12f)
                    : baseColor;
            _knob.style.backgroundColor = _value ? AgentUi.Text : AgentUi.Muted;
            _knob.style.left = _value ? 19 : 3;
        }
    }

    internal sealed class AgentMenuItem
    {
        public AgentMenuItem(string text, Action? action, bool selected = false, bool dangerous = false,
            bool disabled = false, bool separatorBefore = false)
        {
            Text = text;
            Action = action;
            Selected = selected;
            Dangerous = dangerous;
            Disabled = disabled;
            SeparatorBefore = separatorBefore;
        }

        public string Text { get; }
        public Action? Action { get; }
        public bool Selected { get; }
        public bool Dangerous { get; }
        public bool Disabled { get; }
        public bool SeparatorBefore { get; }
    }

    internal static class AgentPopupMenu
    {
        private const string LayerName = "unity-agent-owned-popup-layer";

        public static void Show(VisualElement anchor, IReadOnlyList<AgentMenuItem> items, int minWidth = 220)
        {
            var root = anchor.panel?.visualTree;
            if (root == null) return;
            root.Q<VisualElement>(LayerName)?.RemoveFromHierarchy();

            var layer = new VisualElement { name = LayerName, focusable = true };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.right = 0;
            layer.style.top = 0;
            layer.style.bottom = 0;
            layer.style.backgroundColor = AgentUi.Transparent;
            layer.style.backgroundImage = StyleKeyword.None;
            layer.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == layer) layer.RemoveFromHierarchy();
            });
            layer.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                layer.RemoveFromHierarchy();
                evt.StopPropagation();
            });
            root.Add(layer);
            layer.BringToFront();

            var position = root.WorldToLocal(new Vector2(anchor.worldBound.xMin, anchor.worldBound.yMax + 5));
            var menu = AgentUi.RoundedPanel(11);
            menu.style.position = Position.Absolute;
            menu.style.left = Mathf.Max(8, position.x);
            menu.style.top = Mathf.Max(8, position.y);
            menu.style.width = Mathf.Max(minWidth, anchor.worldBound.width);
            menu.style.maxHeight = 330;
            menu.style.paddingTop = 6;
            menu.style.paddingRight = 6;
            menu.style.paddingBottom = 6;
            menu.style.paddingLeft = 6;
            menu.style.backgroundColor = AgentUi.Popup;
            AgentUi.SetBorder(menu, AgentUi.BorderStrong, 1);
            layer.Add(menu);

            var scroll = AgentUi.Scroll(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 316;
            menu.Add(scroll);
            foreach (var item in items)
            {
                if (item.SeparatorBefore)
                {
                    var separator = new VisualElement();
                    separator.style.height = 1;
                    separator.style.marginTop = 5;
                    separator.style.marginRight = 5;
                    separator.style.marginBottom = 5;
                    separator.style.marginLeft = 5;
                    separator.style.backgroundColor = AgentUi.Border;
                    scroll.Add(separator);
                }

                var marker = item.Selected ? "✓  " : "   ";
                var action = item.Action;
                var option = AgentUi.Button(marker + item.Text, item.Text, () =>
                {
                    if (item.Disabled) return;
                    layer.RemoveFromHierarchy();
                    action?.Invoke();
                }, 0, AgentUi.Transparent, item.Dangerous ? AgentUi.Error : AgentUi.Text);
                option.style.height = 31;
                option.style.marginTop = 1;
                option.style.marginRight = 0;
                option.style.marginBottom = 1;
                option.style.marginLeft = 0;
                option.style.justifyContent = Justify.FlexStart;
                option.style.opacity = item.Disabled ? 0.45f : 1f;
                option.SetEnabled(!item.Disabled);
                scroll.Add(option);
            }

            layer.schedule.Execute(() =>
            {
                var width = menu.resolvedStyle.width;
                var height = menu.resolvedStyle.height;
                var availableWidth = root.resolvedStyle.width;
                var availableHeight = root.resolvedStyle.height;
                if (!float.IsNaN(width) && position.x + width > availableWidth - 8)
                    menu.style.left = Mathf.Max(8, availableWidth - width - 8);
                if (!float.IsNaN(height) && position.y + height > availableHeight - 8)
                {
                    var above = root.WorldToLocal(new Vector2(anchor.worldBound.xMin, anchor.worldBound.yMin - 5)).y;
                    menu.style.top = Mathf.Max(8, above - height);
                }
                layer.Focus();
            });
        }
    }

    /// <summary>One owned tooltip layer per panel. Native UI Toolkit tooltip popups are suppressed.</summary>
    public static class AgentTooltip
    {
        private const string LayerName = "unity-agent-owned-tooltip";

        public static void Attach(VisualElement target, string text) => Attach(target, () => text);

        public static void Attach(VisualElement target, Func<string> textProvider)
        {
            target.RegisterCallback<TooltipEvent>(evt =>
            {
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerEnterEvent>(evt =>
            {
                var text = textProvider();
                if (!string.IsNullOrWhiteSpace(text)) Show(target, text, evt.position);
            });
            target.RegisterCallback<PointerMoveEvent>(evt => Position(target, evt.position));
            target.RegisterCallback<PointerLeaveEvent>(_ => Hide(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => Hide(target));
        }

        private static void Show(VisualElement target, string text, Vector2 worldPosition)
        {
            var root = target.panel?.visualTree;
            if (root == null) return;
            var popup = root.Q<VisualElement>(LayerName);
            if (popup == null)
            {
                popup = AgentUi.RoundedPanel(8);
                popup.name = LayerName;
                popup.pickingMode = PickingMode.Ignore;
                popup.style.position = UnityEngine.UIElements.Position.Absolute;
                popup.style.maxWidth = 360;
                popup.style.paddingTop = 7;
                popup.style.paddingRight = 9;
                popup.style.paddingBottom = 7;
                popup.style.paddingLeft = 9;
                popup.style.backgroundColor = AgentUi.Popup;
                AgentUi.SetBorder(popup, AgentUi.BorderStrong, 1);
                var label = new Label { name = LayerName + "-text", pickingMode = PickingMode.Ignore };
                label.style.color = AgentUi.Text;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.fontSize = 11;
                popup.Add(label);
                root.Add(popup);
            }
            var textLabel = popup.Q<Label>(LayerName + "-text");
            if (textLabel != null) textLabel.text = text;
            popup.style.display = DisplayStyle.Flex;
            popup.BringToFront();
            Position(target, worldPosition);
        }

        private static void Position(VisualElement target, Vector2 worldPosition)
        {
            var root = target.panel?.visualTree;
            var popup = root?.Q<VisualElement>(LayerName);
            if (root == null || popup == null || popup.style.display == DisplayStyle.None) return;
            var local = root.WorldToLocal(worldPosition);
            var width = float.IsNaN(popup.resolvedStyle.width) ? 320f : popup.resolvedStyle.width;
            var height = float.IsNaN(popup.resolvedStyle.height) ? 58f : popup.resolvedStyle.height;
            popup.style.left = Mathf.Clamp(local.x + 12f, 8f,
                Mathf.Max(8f, root.resolvedStyle.width - width - 8f));
            popup.style.top = Mathf.Clamp(local.y + 17f, 8f,
                Mathf.Max(8f, root.resolvedStyle.height - height - 8f));
        }

        private static void Hide(VisualElement target)
        {
            var popup = target.panel?.visualTree.Q<VisualElement>(LayerName);
            if (popup != null) popup.style.display = DisplayStyle.None;
        }
    }
}
