#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal abstract class DebugNode
    {
        protected DebugNode(string label)
        {
            Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
        }

        public string Label { get; }
    }

    internal class DebugGroupNode : DebugNode
    {
        public DebugGroupNode(string label, string? toolName, string? description, bool parentToolRooted)
            : base(label)
        {
        }

        protected DebugGroupNode(string? toolName, string? description)
            : base(string.IsNullOrWhiteSpace(toolName) ? "Debug" : toolName!)
        {
        }

        public List<DebugNode> Children { get; } = new();

    }

    internal sealed class DebugWindowNode : DebugGroupNode
    {
        public DebugWindowNode(string? toolName, string? description)
            : base(toolName, description)
        {
            Title = string.IsNullOrWhiteSpace(toolName) ? "Debug" : toolName!;
        }

        public string Title { get; set; }

        public bool Draggable { get; set; } = true;

    }

    internal sealed class DebugInlineGroupNode : DebugGroupNode
    {
        public DebugInlineGroupNode(FlexDirection direction)
            : base(string.Empty, null, null, false)
        {
            Direction = direction;
        }

        public FlexDirection Direction { get; }
    }

    internal sealed class DebugLabelNode : DebugNode
    {
        public DebugLabelNode(string text) : base(text)
        {
        }

    }

    internal sealed class DebugSectionNode : DebugNode
    {
        public DebugSectionNode(string text) : base(text)
        {
        }

    }

    internal sealed class DebugDynamicLabelNode : DebugNode
    {
        public DebugDynamicLabelNode(Func<string> getter) : base(string.Empty)
        {
            Getter = getter ?? throw new ArgumentNullException(nameof(getter));
        }

        public Func<string> Getter { get; }

    }

    internal sealed class DebugTagNode : DebugNode
    {
        public DebugTagNode(string text) : base(text)
        {
        }

    }

    internal sealed class DebugSpaceNode : DebugNode
    {
        public DebugSpaceNode(float height) : base(string.Empty)
        {
            Height = height;
        }

        public float Height { get; }

    }

    internal sealed class DebugImageNode : DebugNode
    {
        public DebugImageNode(string label, Func<Background> backgroundGetter) : base(label)
        {
            BackgroundGetter = backgroundGetter ?? throw new ArgumentNullException(nameof(backgroundGetter));
        }

        public Func<Background> BackgroundGetter { get; }

    }

    internal sealed class DebugButtonNode : DebugNode
    {
        public DebugButtonNode(string label, Action action, string? toolName, string? description)
            : base(label)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public Action Action { get; }


    }

    internal sealed class DebugStateButtonNode : DebugNode
    {
        public DebugStateButtonNode(
            Func<string> labelGetter,
            Func<bool> stateGetter,
            Action action,
            DebugTone tone,
            string? toolName,
            string? description)
            : base(string.Empty)
        {
            LabelGetter = labelGetter ?? throw new ArgumentNullException(nameof(labelGetter));
            StateGetter = stateGetter ?? throw new ArgumentNullException(nameof(stateGetter));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Tone = tone;
        }

        public Func<string> LabelGetter { get; }

        public Func<bool> StateGetter { get; }

        public Action Action { get; }

        public DebugTone Tone { get; }


    }

    internal interface IDebugFieldNode
    {
        Type ValueType { get; }

        bool IsReadOnly { get; }

        object? GetObjectValue();

        void SetObjectValue(object? value);
    }

    internal class DebugFieldNode<TValue> : DebugNode, IDebugFieldNode
    {
        public DebugFieldNode(
            string label,
            Func<TValue> getter,
            Action<TValue>? setter,
            string? toolName,
            string? description)
            : base(label)
        {
            Getter = getter ?? throw new ArgumentNullException(nameof(getter));
            Setter = setter;
        }

        public Func<TValue> Getter { get; }

        public Action<TValue>? Setter { get; }


        public bool IsReadOnly => Setter == null;

        public Type ValueType => typeof(TValue);

        public object? GetObjectValue() => Getter();

        public void SetObjectValue(object? value)
        {
            if (Setter == null) return;
            if (value is TValue typed)
            {
                Setter(typed);
                return;
            }

            if (value == null)
            {
                Setter(default!);
                return;
            }

            var targetType = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
            if (targetType.IsEnum)
            {
                if (value is string text)
                    Setter((TValue)Enum.Parse(targetType, text, true));
                else
                    Setter((TValue)Enum.ToObject(targetType, value));
                return;
            }

            Setter((TValue)Convert.ChangeType(value, targetType));
        }

    }

    internal sealed class DebugStateLabelNode : DebugFieldNode<bool>
    {
        public DebugStateLabelNode(
            string label,
            Func<bool> getter,
            DebugTone tone,
            string? toolName,
            string? description)
            : base(label, getter, null, toolName, description)
        {
            Tone = tone;
        }

        public DebugTone Tone { get; }
    }

    internal sealed class DebugBoolButtonNode : DebugFieldNode<bool>
    {
        public DebugBoolButtonNode(
            string label,
            Func<bool> getter,
            Action<bool> setter,
            DebugTone tone,
            string? toolName,
            string? description)
            : base(label, getter, setter, toolName, description)
        {
            Tone = tone;
        }

        public DebugTone Tone { get; }
    }

    internal sealed class DebugSegmentedIntNode : DebugFieldNode<int>
    {
        public DebugSegmentedIntNode(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            Action<int> setter,
            DebugTone tone,
            string? toolName,
            string? description)
            : base(label, getter, setter, toolName, description)
        {
            LowValue = lowValue;
            HighValue = highValue;
            Tone = tone;
        }

        public int LowValue { get; }

        public int HighValue { get; }

        public DebugTone Tone { get; }
    }

    internal sealed class DebugFloatSliderNode : DebugFieldNode<float>
    {
        public DebugFloatSliderNode(
            string label,
            float lowValue,
            float highValue,
            Func<float> getter,
            Action<float>? setter,
            string format,
            string? toolName,
            string? description)
            : base(label, getter, setter, toolName, description)
        {
            LowValue = lowValue;
            HighValue = highValue;
            Format = string.IsNullOrWhiteSpace(format) ? "0.##" : format;
        }

        public float LowValue { get; }

        public float HighValue { get; }

        public string Format { get; }
    }

    internal sealed class DebugIntSliderNode : DebugFieldNode<int>
    {
        public DebugIntSliderNode(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            Action<int>? setter,
            string format,
            string? toolName,
            string? description)
            : base(label, getter, setter, toolName, description)
        {
            LowValue = lowValue;
            HighValue = highValue;
            Format = string.IsNullOrWhiteSpace(format) ? "0" : format;
        }

        public int LowValue { get; }

        public int HighValue { get; }

        public string Format { get; }
    }

    internal sealed class DebugProgressNode : DebugNode
    {
        public DebugProgressNode(string label, float lowValue, float highValue, Func<float> getter, string format)
            : base(label)
        {
            LowValue = lowValue;
            HighValue = highValue;
            Getter = getter ?? throw new ArgumentNullException(nameof(getter));
            Format = string.IsNullOrWhiteSpace(format) ? "0.##" : format;
        }

        public float LowValue { get; }

        public float HighValue { get; }

        public Func<float> Getter { get; }

        public string Format { get; }

    }
}
