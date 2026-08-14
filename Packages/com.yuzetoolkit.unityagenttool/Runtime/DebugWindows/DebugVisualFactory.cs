#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal static class DebugVisualFactory
    {
        public static VisualElement CreateWindow(
            DebugWindowNode node,
            bool allowDragging,
            ICollection<IDebugValueBinding> bindings)
        {
            var window = new VisualElement { name = "unity-debug-tool-window-page" };
            DebugWindowUss.ApplyWindow(window);

            var content = new ScrollView(ScrollViewMode.Vertical);
            DebugWindowUss.ApplyWindowContent(content);
            content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            for (var index = 0; index < node.Children.Count; index++)
            {
                var child = node.Children[index];
                var childVisual = CreateNode(child, bindings);
                if (index == 0 && child is DebugSectionNode)
                    DebugWindowUss.ApplyFirstSection((Label)childVisual);
                content.Add(childVisual);
            }
            window.Add(content);

            SuppressKeyboardInteraction(window);

            return window;
        }

        private static void SuppressKeyboardInteraction(VisualElement root)
        {
            TextField? activeTextField = null;

            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                ReleaseEventSystemSelection();
                var textField = evt.button == 0 ? FindTextField(evt.target as VisualElement, root) : null;
                if (textField != null && textField.enabledInHierarchy)
                {
                    activeTextField = textField;
                    return;
                }

                activeTextField?.Blur();
                activeTextField = null;
                BlurFocusedElement(root);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(evt =>
            {
                ReleaseEventSystemSelection();
                if (FindTextField(evt.target as VisualElement, root) == null)
                    BlurFocusedElement(root);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusInEvent>(evt =>
            {
                if (activeTextField != null && IsDescendantOf(evt.target as VisualElement, activeTextField)) return;
                if (evt.target is VisualElement focused)
                    focused.schedule.Execute(focused.Blur);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (activeTextField != null && IsDescendantOf(evt.target as VisualElement, activeTextField))
                    activeTextField = null;
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (activeTextField != null && IsDescendantOf(evt.target as VisualElement, activeTextField))
                {
                    if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    {
                        evt.PreventDefault();
                        evt.StopImmediatePropagation();
                        var submittedField = activeTextField;
                        activeTextField = null;
                        submittedField.schedule.Execute(submittedField.Blur);
                        ReleaseEventSystemSelection();
                    }

                    return;
                }

                SuppressEvent(evt);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyUpEvent>(evt =>
            {
                if (activeTextField != null && IsDescendantOf(evt.target as VisualElement, activeTextField)) return;
                SuppressEvent(evt);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(SuppressEvent, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(SuppressEvent, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationCancelEvent>(SuppressEvent, TrickleDown.TrickleDown);
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

        private static void BlurFocusedElement(VisualElement root)
        {
            if (root.panel?.focusController.focusedElement is VisualElement focused && IsDescendantOf(focused, root))
                focused.Blur();
        }

        private static void ReleaseEventSystemSelection() => EventSystem.current?.SetSelectedGameObject(null);

        private static void SuppressEvent(KeyDownEvent evt)
        {
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private static void SuppressEvent(KeyUpEvent evt)
        {
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private static void SuppressEvent(NavigationMoveEvent evt)
        {
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private static void SuppressEvent(NavigationSubmitEvent evt)
        {
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private static void SuppressEvent(NavigationCancelEvent evt)
        {
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private static VisualElement CreateNode(DebugNode node, ICollection<IDebugValueBinding> bindings)
        {
            switch (node)
            {
                case DebugInlineGroupNode inlineGroup:
                    return CreateInlineGroup(inlineGroup, bindings);
                case DebugGroupNode group:
                    return CreateGroup(group, bindings);
                case DebugSectionNode section:
                    return CreateSection(section.Label);
                case DebugDynamicLabelNode dynamicLabel:
                    return CreateDynamicLabel(dynamicLabel, bindings);
                case DebugTagNode tag:
                    return CreateTag(tag.Label);
                case DebugLabelNode label:
                    return CreateLabel(label.Label);
                case DebugSpaceNode space:
                    return new VisualElement { style = { height = space.Height } };
                case DebugImageNode image:
                    return CreateImage(image, bindings);
                case DebugButtonNode button:
                    return CreateButton(button);
                case DebugStateButtonNode stateButton:
                    return CreateStateButton(stateButton, bindings);
                case DebugStateLabelNode stateLabel:
                    return CreateStateLabel(stateLabel, bindings);
                case DebugBoolButtonNode boolButton:
                    return CreateBoolButton(boolButton, bindings);
                case DebugSegmentedIntNode segmentedInt:
                    return CreateSegmentedInt(segmentedInt, bindings);
                case DebugFloatSliderNode slider:
                    return CreateFloatSlider(slider, bindings);
                case DebugIntSliderNode slider:
                    return CreateIntSlider(slider, bindings);
                case DebugProgressNode progress:
                    return CreateProgress(progress, bindings);
                case IDebugFieldNode field:
                    return CreateField(field, node.Label, bindings);
                default:
                    return CreateLabel(node.Label);
            }
        }

        private static VisualElement CreateGroup(DebugGroupNode group, ICollection<IDebugValueBinding> bindings)
        {
            var foldout = new Foldout { text = group.Label, value = false };
            DebugWindowUss.ApplyFoldout(foldout);
            foreach (var child in group.Children)
                foldout.Add(CreateNode(child, bindings));
            return foldout;
        }

        private static VisualElement CreateInlineGroup(DebugInlineGroupNode group, ICollection<IDebugValueBinding> bindings)
        {
            var root = new VisualElement();
            DebugWindowUss.ApplyInlineGroup(root);
            DebugWindowUss.ApplyInlineGroupDirection(root, group.Direction);
            for (var index = 0; index < group.Children.Count; index++)
            {
                var child = group.Children[index];
                var visual = CreateNode(child, bindings);
                if (group.Direction == FlexDirection.Row && index == 0 && visual is Label label)
                    DebugWindowUss.ApplyInlineFieldLabel(label);
                root.Add(visual);
            }
            return root;
        }

        private static Label CreateLabel(string text)
        {
            var label = new Label(text);
            DebugWindowUss.ApplyLabel(label);
            return label;
        }

        private static Label CreateSection(string text)
        {
            var label = new Label(text);
            DebugWindowUss.ApplySection(label);
            return label;
        }

        private static Label CreateDynamicLabel(
            DebugDynamicLabelNode node,
            ICollection<IDebugValueBinding> bindings)
        {
            var label = CreateLabel(string.Empty);
            var binding = new FieldBinding<string>(node.Getter, value => label.text = value);
            bindings.Add(binding);
            binding.Refresh();
            return label;
        }

        private static Label CreateTag(string text)
        {
            var label = new Label(text);
            DebugWindowUss.ApplyTag(label);
            return label;
        }

        private static VisualElement CreateImage(DebugImageNode image, ICollection<IDebugValueBinding> bindings)
        {
            var root = new VisualElement();
            DebugWindowUss.ApplyPreview(root);

            if (!string.IsNullOrWhiteSpace(image.Label))
                root.Add(CreateLabel(image.Label));

            var preview = new VisualElement();
            DebugWindowUss.ApplyImage(preview);
            preview.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            root.Add(preview);

            var binding = new FieldBinding<Background>(image.BackgroundGetter, value => preview.style.backgroundImage = value);
            bindings.Add(binding);
            binding.Refresh();
            return root;
        }

        private static VisualElement CreateButton(DebugButtonNode node)
        {
            var iconDirection = GetDirectionIcon(node.Label);
            var button = new Button(() => node.Action()) { text = iconDirection == 0 ? node.Label : string.Empty };
            DebugWindowUss.ApplyButton(button);
            if (iconDirection == 0)
                DebugWindowUss.ApplyPrimaryButton(button);
            else
                DebugWindowUss.ApplyIconButton(button, iconDirection < 0);
            return button;
        }

        private static int GetDirectionIcon(string label)
        {
            if (label.Length != 1) return 0;
            return label[0] switch
            {
                '\u2039' => -1,
                '\u203a' => 1,
                _ => 0
            };
        }

        private static VisualElement CreateStateButton(
            DebugStateButtonNode node,
            ICollection<IDebugValueBinding> bindings)
        {
            Button? button = null;
            button = new Button(() =>
            {
                node.Action();
                ApplyState(button!, node.StateGetter(), node.Tone);
                button!.text = node.LabelGetter();
            });
            DebugWindowUss.ApplyButton(button);
            DebugWindowUss.ApplyStateButton(button);

            var binding = new FieldBinding<bool>(node.StateGetter, value =>
            {
                button.text = node.LabelGetter();
                ApplyState(button!, value, node.Tone);
            });
            bindings.Add(binding);
            binding.Refresh();
            return button;
        }

        private static VisualElement CreateStateLabel(
            DebugStateLabelNode node,
            ICollection<IDebugValueBinding> bindings)
        {
            var label = CreateLabel(string.Empty);
            DebugWindowUss.ApplyStateLabel(label);
            DebugWindowUss.ApplyTone(label, node.Tone);
            var binding = new FieldBinding<bool>(node.Getter, value =>
                label.text = string.IsNullOrWhiteSpace(node.Label)
                    ? value.ToString()
                    : $"{node.Label} [{value}]");
            bindings.Add(binding);
            binding.Refresh();
            return label;
        }

        private static VisualElement CreateBoolButton(
            DebugBoolButtonNode node,
            ICollection<IDebugValueBinding> bindings)
        {
            Button? button = null;
            Label? status = null;
            void Apply(bool value)
            {
                status!.text = value ? "On" : "Off";
                ApplyState(button!, value, node.Tone);
            }

            button = new Button(() =>
            {
                var value = !node.Getter();
                node.Setter?.Invoke(value);
                Apply(node.Getter());
            });
            DebugWindowUss.ApplyButton(button);
            DebugWindowUss.ApplyBoolButton(button);

            var label = new Label(string.IsNullOrWhiteSpace(node.Label) ? "Value" : node.Label)
            {
                pickingMode = PickingMode.Ignore,
                enableRichText = false
            };
            label.AddToClassList(DebugWindowUss.BoolButtonLabelClass);
            button.Add(label);
            status = new Label
            {
                pickingMode = PickingMode.Ignore,
                enableRichText = false
            };
            status.AddToClassList(DebugWindowUss.BoolButtonStatusClass);
            button.Add(status);
            var toggle = new VisualElement { pickingMode = PickingMode.Ignore };
            toggle.AddToClassList(DebugWindowUss.BoolSwitchClass);
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(DebugWindowUss.BoolSwitchThumbClass);
            toggle.Add(thumb);
            button.Add(toggle);

            var binding = new FieldBinding<bool>(node.Getter, Apply);
            bindings.Add(binding);
            binding.Refresh();
            return button;
        }

        private static VisualElement CreateSegmentedInt(
            DebugSegmentedIntNode node,
            ICollection<IDebugValueBinding> bindings)
        {
            var root = new VisualElement();
            DebugWindowUss.ApplySegmentedRow(root);

            if (!string.IsNullOrWhiteSpace(node.Label))
            {
                var label = new Label(node.Label);
                DebugWindowUss.ApplyLabel(label, true);
                root.Add(label);
            }

            var buttons = new List<Button>();
            for (var value = node.LowValue + 1; value <= node.HighValue; value++)
            {
                var targetValue = value;
                var button = new Button(() =>
                {
                    var current = Mathf.Clamp(node.Getter(), node.LowValue, node.HighValue);
                    node.Setter?.Invoke(current == targetValue ? targetValue - 1 : targetValue);
                }) { text = targetValue.ToString() };
                DebugWindowUss.ApplyButton(button);
                DebugWindowUss.ApplySegmentButton(button);
                buttons.Add(button);
                root.Add(button);
            }

            var binding = new FieldBinding<int>(node.Getter, current =>
            {
                current = Mathf.Clamp(current, node.LowValue, node.HighValue);
                for (var index = 0; index < buttons.Count; index++)
                    ApplyState(buttons[index], current >= node.LowValue + index + 1, node.Tone);
            });
            bindings.Add(binding);
            binding.Refresh();
            return root;
        }

        private static void ApplyState(VisualElement element, bool active, DebugTone tone)
        {
            DebugWindowUss.ApplyActiveState(element, active);
            DebugWindowUss.ApplyTone(element, active ? tone : DebugTone.Default);
        }

        private static VisualElement CreateField(
            IDebugFieldNode node,
            string label,
            ICollection<IDebugValueBinding> bindings)
        {
            if (node.IsReadOnly)
                return CreateReadOnlyLabel(node, label, bindings);

            var type = Nullable.GetUnderlyingType(node.ValueType) ?? node.ValueType;
            if (type == typeof(bool)) return CreateBoolField(node, label, bindings);
            if (type == typeof(int)) return CreateTypedField<int, IntegerField>(node, label, new IntegerField(), bindings);
            if (type == typeof(float)) return CreateTypedField<float, FloatField>(node, label, new FloatField(), bindings);
            if (type == typeof(double)) return CreateTypedField<double, DoubleField>(node, label, new DoubleField(), bindings);
            if (type == typeof(string)) return CreateTypedField<string, TextField>(node, label, new TextField(), bindings);
            if (type == typeof(Vector2)) return CreateTypedField<Vector2, Vector2Field>(node, label, new Vector2Field(), bindings);
            if (type == typeof(Vector3)) return CreateTypedField<Vector3, Vector3Field>(node, label, new Vector3Field(), bindings);
            if (type == typeof(Vector4)) return CreateTypedField<Vector4, Vector4Field>(node, label, new Vector4Field(), bindings);
            if (type == typeof(Vector2Int)) return CreateTypedField<Vector2Int, Vector2IntField>(node, label, new Vector2IntField(), bindings);
            if (type == typeof(Vector3Int)) return CreateTypedField<Vector3Int, Vector3IntField>(node, label, new Vector3IntField(), bindings);
            if (type == typeof(Rect)) return CreateTypedField<Rect, RectField>(node, label, new RectField(), bindings);
            if (type == typeof(RectInt)) return CreateTypedField<RectInt, RectIntField>(node, label, new RectIntField(), bindings);
            if (type == typeof(Bounds)) return CreateTypedField<Bounds, BoundsField>(node, label, new BoundsField(), bindings);
            if (type == typeof(BoundsInt)) return CreateTypedField<BoundsInt, BoundsIntField>(node, label, new BoundsIntField(), bindings);
            if (type.IsEnum) return CreateEnumField(node, label, bindings);
            return CreateObjectField(node, label, bindings);
        }

        private static VisualElement CreateReadOnlyLabel(
            IDebugFieldNode node,
            string label,
            ICollection<IDebugValueBinding> bindings)
        {
            var value = CreateLabel(string.Empty);
            DebugWindowUss.ApplyReadOnlyLabel(value);
            var binding = new FieldBinding<object?>(node.GetObjectValue, current =>
            {
                var formatted = DebugToolUtility.FormatValue(current);
                value.text = string.IsNullOrWhiteSpace(label) ? formatted : $"{label} [{formatted}]";
            });
            bindings.Add(binding);
            binding.Refresh();
            return value;
        }

        private static VisualElement CreateTypedField<TValue, TField>(
            IDebugFieldNode node,
            string label,
            TField field,
            ICollection<IDebugValueBinding> bindings)
            where TField : BaseField<TValue>
        {
            field.label = label;
            field.SetEnabled(!node.IsReadOnly);
            DebugWindowUss.ApplyField(field);
            if (string.IsNullOrEmpty(label))
                DebugWindowUss.ApplyFieldWithoutLabel(field);

            var binding = new ObjectFieldBinding<TValue>(node, field);
            bindings.Add(binding);
            binding.Refresh();

            if (!node.IsReadOnly)
            {
                field.RegisterValueChangedCallback(evt =>
                {
                    if (binding.IsRefreshing) return;
                    node.SetObjectValue(evt.newValue);
                    binding.Refresh();
                });
            }

            return field;
        }

        private static VisualElement CreateBoolField(
            IDebugFieldNode node,
            string label,
            ICollection<IDebugValueBinding> bindings)
        {
            var field = new Toggle { label = label };
            field.SetEnabled(!node.IsReadOnly);
            DebugWindowUss.ApplyField(field);
            if (string.IsNullOrEmpty(label))
                DebugWindowUss.ApplyFieldWithoutLabel(field);
            var status = DebugWindowUss.ApplyOwnedToggle(field);
            void Sync(bool value)
            {
                status.text = value ? "On" : "Off";
                DebugWindowUss.ApplyActiveState(field, value);
            }

            var binding = new ObjectFieldBinding<bool>(node, field, Sync);
            bindings.Add(binding);
            binding.Refresh();
            field.RegisterValueChangedCallback(evt =>
            {
                if (binding.IsRefreshing) return;
                node.SetObjectValue(evt.newValue);
                binding.Refresh();
            });
            return field;
        }

        private static VisualElement CreateEnumField(
            IDebugFieldNode node,
            string label,
            ICollection<IDebugValueBinding> bindings)
        {
            var current = node.GetObjectValue() as Enum;
            if (current == null)
            {
                var enumType = Nullable.GetUnderlyingType(node.ValueType) ?? node.ValueType;
                var values = Enum.GetValues(enumType);
                current = values.Length > 0 ? values.GetValue(0) as Enum : null;
                if (current == null)
                    return CreateReadOnlyLabel(node, label, bindings);
            }

            var field = new DebugEnumDropdown(label, current.GetType(), current);
            field.SetEnabled(!node.IsReadOnly);
            if (string.IsNullOrEmpty(label))
                field.AddToClassList(DebugWindowUss.FieldWithoutLabelClass);

            var binding = new EnumFieldBinding(node, field);
            bindings.Add(binding);
            binding.Refresh();

            if (!node.IsReadOnly)
            {
                field.ValueChanged += value =>
                {
                    if (binding.IsRefreshing) return;
                    node.SetObjectValue(value);
                    binding.Refresh();
                };
            }

            return field;
        }

        private static VisualElement CreateObjectField(
            IDebugFieldNode node,
            string label,
            ICollection<IDebugValueBinding> bindings)
        {
            var row = new VisualElement();
            DebugWindowUss.ApplyRow(row);

            var name = new Label(label);
            DebugWindowUss.ApplyLabel(name, true);
            row.Add(name);

            var value = new Label();
            DebugWindowUss.ApplyMiniValue(value);
            row.Add(value);

            var binding = new LabelBinding(node.GetObjectValue, value);
            bindings.Add(binding);
            binding.Refresh();
            return row;
        }

        private static VisualElement CreateFloatSlider(DebugFloatSliderNode node, ICollection<IDebugValueBinding> bindings)
        {
            if (node.IsReadOnly)
                return CreateReadOnlyLabel(node, node.Label, bindings);

            var root = new VisualElement();
            DebugWindowUss.ApplySliderRow(root);

            var slider = new Slider(node.LowValue, node.HighValue)
            {
                label = node.Label
            };
            slider.SetEnabled(!node.IsReadOnly);
            DebugWindowUss.ApplySlider(slider);

            var valueLabel = new Label();
            DebugWindowUss.ApplySliderValue(valueLabel);
            var chrome = CreateSliderChrome(slider);

            void Apply(float value)
            {
                var clamped = Mathf.Clamp(value, node.LowValue, node.HighValue);
                slider.SetValueWithoutNotify(clamped);
                valueLabel.text = DebugToolUtility.FormatNumber(node.Format, clamped);
                ApplySliderChrome(chrome, node.LowValue, node.HighValue, clamped);
            }

            var binding = new FieldBinding<float>(() => node.Getter(), Apply);
            bindings.Add(binding);
            binding.Refresh();

            if (!node.IsReadOnly)
            {
                slider.RegisterValueChangedCallback(evt =>
                {
                    var clamped = Mathf.Clamp(evt.newValue, node.LowValue, node.HighValue);
                    node.Setter?.Invoke(clamped);
                    Apply(clamped);
                });
            }

            root.Add(slider);
            root.Add(valueLabel);
            return root;
        }

        private static VisualElement CreateIntSlider(DebugIntSliderNode node, ICollection<IDebugValueBinding> bindings)
        {
            if (node.IsReadOnly)
                return CreateReadOnlyLabel(node, node.Label, bindings);

            var root = new VisualElement();
            DebugWindowUss.ApplySliderRow(root);

            var slider = new SliderInt(node.LowValue, node.HighValue)
            {
                label = node.Label
            };
            slider.SetEnabled(!node.IsReadOnly);
            DebugWindowUss.ApplySlider(slider);

            var valueLabel = new Label();
            DebugWindowUss.ApplySliderValue(valueLabel);
            var chrome = CreateSliderChrome(slider);

            void Apply(int value)
            {
                var clamped = Mathf.Clamp(value, node.LowValue, node.HighValue);
                slider.SetValueWithoutNotify(clamped);
                valueLabel.text = DebugToolUtility.FormatNumber(node.Format, clamped);
                ApplySliderChrome(chrome, node.LowValue, node.HighValue, clamped);
            }

            var binding = new FieldBinding<int>(() => node.Getter(), Apply);
            bindings.Add(binding);
            binding.Refresh();

            if (!node.IsReadOnly)
            {
                slider.RegisterValueChangedCallback(evt =>
                {
                    var clamped = Mathf.Clamp(evt.newValue, node.LowValue, node.HighValue);
                    node.Setter?.Invoke(clamped);
                    Apply(clamped);
                });
            }

            root.Add(slider);
            root.Add(valueLabel);
            return root;
        }

        private static VisualElement CreateProgress(DebugProgressNode node, ICollection<IDebugValueBinding> bindings)
        {
            var progress = new ProgressBar
            {
                lowValue = node.LowValue,
                highValue = node.HighValue,
                title = node.Label
            };
            DebugWindowUss.ApplyProgress(progress);
            var binding = new FieldBinding<float>(node.Getter, value =>
            {
                progress.value = value;
                progress.title = $"{node.Label} {DebugToolUtility.FormatNumber(node.Format, value)}";
            });
            bindings.Add(binding);
            binding.Refresh();
            return progress;
        }

        private static SliderChrome CreateSliderChrome(VisualElement slider)
        {
            var filler = new VisualElement();
            DebugWindowUss.ApplySliderFiller(filler);
            filler.pickingMode = PickingMode.Ignore;
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            DebugWindowUss.ApplySliderThumb(thumb);

            var tracker = slider.Q<VisualElement>(className: "unity-base-slider__tracker") ??
                          throw new InvalidOperationException("Unity 2022.3 Slider tracker was not created.");
            tracker.Insert(0, filler);
            tracker.Add(thumb);

            return new SliderChrome(filler, thumb);
        }

        private static void ApplySliderChrome(SliderChrome chrome, float lowValue, float highValue, float value)
        {
            var percentage = Mathf.InverseLerp(lowValue, highValue, value) * 100f;
            chrome.Filler.style.width = Length.Percent(percentage);
            chrome.Thumb.style.left = Length.Percent(percentage);
        }

        private readonly struct SliderChrome
        {
            public SliderChrome(VisualElement filler, VisualElement thumb)
            {
                Filler = filler;
                Thumb = thumb;
            }

            public VisualElement Filler { get; }

            public VisualElement Thumb { get; }
        }

        private sealed class ObjectFieldBinding<TValue> : IDebugValueBinding
        {
            private readonly IDebugFieldNode _node;
            private readonly BaseField<TValue> _field;
            private readonly Action<TValue>? _afterRefresh;

            public ObjectFieldBinding(
                IDebugFieldNode node,
                BaseField<TValue> field,
                Action<TValue>? afterRefresh = null)
            {
                _node = node;
                _field = field;
                _afterRefresh = afterRefresh;
            }

            public bool IsRefreshing { get; private set; }

            public void Refresh()
            {
                try
                {
                    IsRefreshing = true;
                    var value = _node.GetObjectValue();
                    if (value is TValue typed)
                    {
                        _field.SetValueWithoutNotify(typed);
                        _afterRefresh?.Invoke(typed);
                    }
                }
                finally
                {
                    IsRefreshing = false;
                }
            }
        }

        private sealed class EnumFieldBinding : IDebugValueBinding
        {
            private readonly IDebugFieldNode _node;
            private readonly DebugEnumDropdown _field;

            public EnumFieldBinding(IDebugFieldNode node, DebugEnumDropdown field)
            {
                _node = node;
                _field = field;
            }

            public bool IsRefreshing { get; private set; }

            public void Refresh()
            {
                try
                {
                    IsRefreshing = true;
                    if (_node.GetObjectValue() is Enum value)
                        _field.SetValueWithoutNotify(value);
                }
                finally
                {
                    IsRefreshing = false;
                }
            }
        }

        private sealed class FieldBinding<TValue> : IDebugValueBinding
        {
            private readonly Func<TValue> _getter;
            private readonly Action<TValue> _apply;

            public FieldBinding(Func<TValue> getter, Action<TValue> apply)
            {
                _getter = getter;
                _apply = apply;
            }

            public void Refresh()
            {
                _apply(_getter());
            }
        }

        private sealed class LabelBinding : IDebugValueBinding
        {
            private readonly Func<object?> _getter;
            private readonly Label _label;

            public LabelBinding(Func<object?> getter, Label label)
            {
                _getter = getter;
                _label = label;
            }

            public void Refresh()
            {
                try
                {
                    _label.text = DebugToolUtility.FormatValue(_getter());
                }
                catch (Exception ex)
                {
                    _label.text = ex.Message;
                }
            }
        }
    }
}
