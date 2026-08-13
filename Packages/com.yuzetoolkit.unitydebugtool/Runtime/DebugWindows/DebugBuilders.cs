#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    public enum DebugTone : byte
    {
        Default,
        Success,
        Danger,
        Red,
        Green,
        Blue,
        Yellow,
        Pink,
        White
    }

    public sealed class DebugWindowBuilder : DebugGroupBuilder
    {
        internal DebugWindowBuilder(string? toolName, string? description)
            : base(new DebugWindowNode(toolName, description))
        {
        }

        internal DebugWindowNode WindowNode => (DebugWindowNode)GroupNode;

        public DebugWindowBuilder SetTitle(string title)
        {
            WindowNode.Title = string.IsNullOrWhiteSpace(title) ? "Debug" : title;
            return this;
        }

        public DebugWindowBuilder SetDraggable(bool draggable)
        {
            WindowNode.Draggable = draggable;
            return this;
        }

        public new DebugWindowBuilder AddLabel(string text)
        {
            base.AddLabel(text);
            return this;
        }

        public new DebugWindowBuilder AddSection(string text)
        {
            base.AddSection(text);
            return this;
        }

        public new DebugWindowBuilder AddDynamicLabel(Func<string> getter)
        {
            base.AddDynamicLabel(getter);
            return this;
        }

        public new DebugWindowBuilder AddTag(string text)
        {
            base.AddTag(text);
            return this;
        }

        public new DebugWindowBuilder AddStateLabel(
            string label,
            Func<bool> getter,
            DebugTone tone = DebugTone.Default,
            string? toolName = null,
            string? description = null)
        {
            base.AddStateLabel(label, getter, tone, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddStateButton(
            Func<string> labelGetter,
            Func<bool> stateGetter,
            Action action,
            DebugTone tone = DebugTone.Default,
            string? toolName = null,
            string? description = null)
        {
            base.AddStateButton(labelGetter, stateGetter, action, tone, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddBoolButton(
            string label,
            Func<bool> getter,
            Action<bool> setter,
            DebugTone tone = DebugTone.Default,
            string? toolName = null,
            string? description = null)
        {
            base.AddBoolButton(label, getter, setter, tone, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddSegmentedInt(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            Action<int> setter,
            DebugTone tone = DebugTone.Danger,
            string? toolName = null,
            string? description = null)
        {
            base.AddSegmentedInt(label, lowValue, highValue, getter, setter, tone, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddSpace(float height = 8f)
        {
            base.AddSpace(height);
            return this;
        }

        public new DebugWindowBuilder AddButton(string label, Action action)
        {
            base.AddButton(label, action);
            return this;
        }

        public new DebugWindowBuilder AddButton(string label, Action action, string? toolName, string? description)
        {
            base.AddButton(label, action, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddReadOnly<TValue>(string label, Func<TValue> getter)
        {
            base.AddReadOnly(label, getter);
            return this;
        }

        public new DebugWindowBuilder AddReadOnly<TValue>(
            string label,
            Func<TValue> getter,
            string? toolName,
            string? description)
        {
            base.AddReadOnly(label, getter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddValue<TValue>(string label, Func<TValue> getter, Action<TValue> setter)
        {
            base.AddValue(label, getter, setter);
            return this;
        }

        public new DebugWindowBuilder AddValue<TValue>(
            string label,
            Func<TValue> getter,
            Action<TValue> setter,
            string? toolName,
            string? description)
        {
            base.AddValue(label, getter, setter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddField<TValue>(string label, Func<TValue> getter)
        {
            base.AddField(label, getter);
            return this;
        }

        public new DebugWindowBuilder AddField<TValue>(string label, Func<TValue> getter, Action<TValue> setter)
        {
            base.AddField(label, getter, setter);
            return this;
        }

        public new DebugWindowBuilder AddReadOnlyBool(string label, Func<bool> getter, string? toolName = null, string? description = null)
        {
            base.AddReadOnly(label, getter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddReadOnlyInt(string label, Func<int> getter, string? toolName = null, string? description = null)
        {
            base.AddReadOnly(label, getter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddReadOnlyFloat(string label, Func<float> getter, string? toolName = null, string? description = null)
        {
            base.AddReadOnly(label, getter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddReadOnlyString(string label, Func<string> getter, string? toolName = null, string? description = null)
        {
            base.AddReadOnly(label, getter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddBool(string label, Func<bool> getter, Action<bool> setter, string? toolName = null, string? description = null)
        {
            base.AddBool(label, getter, setter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddInt(string label, Func<int> getter, Action<int> setter, string? toolName = null, string? description = null)
        {
            base.AddValue(label, getter, setter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddFloat(string label, Func<float> getter, Action<float> setter, string? toolName = null, string? description = null)
        {
            base.AddValue(label, getter, setter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddString(string label, Func<string> getter, Action<string> setter, string? toolName = null, string? description = null)
        {
            base.AddValue(label, getter, setter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddSlider(
            string label,
            float lowValue,
            float highValue,
            Func<float> getter,
            Action<float>? setter = null,
            string format = "0.##",
            string? toolName = null,
            string? description = null)
        {
            base.AddSlider(label, lowValue, highValue, getter, setter, format, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddSlider(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            Action<int>? setter = null,
            string format = "0",
            string? toolName = null,
            string? description = null)
        {
            base.AddSlider(label, lowValue, highValue, getter, setter, format, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddSlider(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            string format)
        {
            base.AddSlider(label, lowValue, highValue, getter, format);
            return this;
        }

        public new DebugWindowBuilder AddSlider(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            Action<int> setter,
            string toolName,
            string description)
        {
            base.AddSlider(label, lowValue, highValue, getter, setter, toolName, description);
            return this;
        }

        public new DebugWindowBuilder AddProgress(
            string label,
            float lowValue,
            float highValue,
            Func<float> getter,
            string format = "0.##")
        {
            base.AddProgress(label, lowValue, highValue, getter, format);
            return this;
        }

        public new DebugWindowBuilder AddProgress(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            string format = "0")
        {
            base.AddProgress(label, lowValue, highValue, getter, format);
            return this;
        }

        public new DebugWindowBuilder AddProgressBar(
            string label,
            float lowValue,
            float highValue,
            Func<float> getter,
            string format = "[{0:F2}]")
        {
            base.AddProgressBar(label, lowValue, highValue, getter, format);
            return this;
        }

        public new DebugWindowBuilder AddProgressBar(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            string format = "[{0}]")
        {
            base.AddProgressBar(label, lowValue, highValue, getter, format);
            return this;
        }

        public new DebugWindowBuilder AddImage(string label, Texture2D texture)
        {
            base.AddImage(label, texture);
            return this;
        }

        public new DebugWindowBuilder AddImage(string label, Sprite sprite)
        {
            base.AddImage(label, sprite);
            return this;
        }

        public new DebugWindowBuilder AddImage(string label, RenderTexture renderTexture)
        {
            base.AddImage(label, renderTexture);
            return this;
        }

        public new DebugWindowBuilder AddImage(string label, VectorImage vectorImage)
        {
            base.AddImage(label, vectorImage);
            return this;
        }

        public new DebugWindowBuilder AddImage(string label, Func<Texture2D> getter)
        {
            base.AddImage(label, getter);
            return this;
        }

        public new DebugWindowBuilder AddImage(string label, Func<Sprite> getter)
        {
            base.AddImage(label, getter);
            return this;
        }

        public new DebugWindowBuilder AddImage(string label, Func<RenderTexture> getter)
        {
            base.AddImage(label, getter);
            return this;
        }

        public new DebugWindowBuilder AddImage(string label, Func<VectorImage> getter)
        {
            base.AddImage(label, getter);
            return this;
        }

        public new DebugWindowBuilder AddGroup(
            string label,
            Action<DebugGroupBuilder> configure,
            bool registerAsTool = true)
        {
            base.AddGroup(label, configure, registerAsTool);
            return this;
        }

        public new DebugWindowBuilder AddGroup(
            string label,
            string toolName,
            string description,
            Action<DebugGroupBuilder> configure)
        {
            base.AddGroup(label, toolName, description, configure);
            return this;
        }

        public new DebugWindowBuilder AddFoldout(string label, Action<DebugGroupBuilder> configure)
        {
            base.AddFoldout(label, configure);
            return this;
        }

        public new DebugWindowBuilder AddHorizontalGroup(Action<DebugGroupBuilder> configure)
        {
            base.AddHorizontalGroup(configure);
            return this;
        }

        public new DebugWindowBuilder AddVerticalGroup(Action<DebugGroupBuilder> configure)
        {
            base.AddVerticalGroup(configure);
            return this;
        }
    }

    public class DebugGroupBuilder
    {
        internal DebugGroupBuilder(DebugGroupNode groupNode)
        {
            GroupNode = groupNode;
        }

        internal DebugGroupNode GroupNode { get; }

        public DebugGroupBuilder AddLabel(string text)
        {
            GroupNode.Children.Add(new DebugLabelNode(text));
            return this;
        }

        public DebugGroupBuilder AddSection(string text)
        {
            GroupNode.Children.Add(new DebugSectionNode(text));
            return this;
        }

        public DebugGroupBuilder AddDynamicLabel(Func<string> getter)
        {
            GroupNode.Children.Add(new DebugDynamicLabelNode(getter));
            return this;
        }

        public DebugGroupBuilder AddTag(string text)
        {
            GroupNode.Children.Add(new DebugTagNode(text));
            return this;
        }

        public DebugGroupBuilder AddStateLabel(
            string label,
            Func<bool> getter,
            DebugTone tone = DebugTone.Default,
            string? toolName = null,
            string? description = null)
        {
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugStateLabelNode(label, getter, tone, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddStateButton(
            Func<string> labelGetter,
            Func<bool> stateGetter,
            Action action,
            DebugTone tone = DebugTone.Default,
            string? toolName = null,
            string? description = null)
        {
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugStateButtonNode(
                labelGetter, stateGetter, action, tone, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddBoolButton(
            string label,
            Func<bool> getter,
            Action<bool> setter,
            DebugTone tone = DebugTone.Default,
            string? toolName = null,
            string? description = null)
        {
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugBoolButtonNode(label, getter, setter, tone, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddSegmentedInt(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            Action<int> setter,
            DebugTone tone = DebugTone.Danger,
            string? toolName = null,
            string? description = null)
        {
            if (highValue <= lowValue) throw new ArgumentOutOfRangeException(nameof(highValue));
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugSegmentedIntNode(
                label, lowValue, highValue, getter, setter, tone, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddSpace(float height = 8f)
        {
            GroupNode.Children.Add(new DebugSpaceNode(Mathf.Max(0f, height)));
            return this;
        }

        public DebugGroupBuilder AddButton(string label, Action action)
        {
            return AddButton(label, action, null, null);
        }

        public DebugGroupBuilder AddButton(string label, Action action, string? toolName, string? description)
        {
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugButtonNode(label, action, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddReadOnly<TValue>(string label, Func<TValue> getter)
        {
            return AddReadOnly(label, getter, null, null);
        }

        public DebugGroupBuilder AddReadOnly<TValue>(
            string label,
            Func<TValue> getter,
            string? toolName,
            string? description)
        {
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugFieldNode<TValue>(label, getter, null, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddValue<TValue>(string label, Func<TValue> getter, Action<TValue> setter)
        {
            return AddValue(label, getter, setter, null, null);
        }

        public DebugGroupBuilder AddValue<TValue>(
            string label,
            Func<TValue> getter,
            Action<TValue> setter,
            string? toolName,
            string? description)
        {
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugFieldNode<TValue>(label, getter, setter, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddField<TValue>(string label, Func<TValue> getter)
        {
            return AddReadOnly(label, getter, null, null);
        }

        public DebugGroupBuilder AddField<TValue>(string label, Func<TValue> getter, Action<TValue> setter)
        {
            return AddValue(label, getter, setter, null, null);
        }

        public DebugGroupBuilder AddReadOnlyBool(string label, Func<bool> getter, string? toolName = null, string? description = null) =>
            AddReadOnly(label, getter, toolName, description);

        public DebugGroupBuilder AddReadOnlyInt(string label, Func<int> getter, string? toolName = null, string? description = null) =>
            AddReadOnly(label, getter, toolName, description);

        public DebugGroupBuilder AddReadOnlyFloat(string label, Func<float> getter, string? toolName = null, string? description = null) =>
            AddReadOnly(label, getter, toolName, description);

        public DebugGroupBuilder AddReadOnlyString(string label, Func<string> getter, string? toolName = null, string? description = null) =>
            AddReadOnly(label, getter, toolName, description);

        public DebugGroupBuilder AddBool(string label, Func<bool> getter, Action<bool> setter, string? toolName = null, string? description = null) =>
            AddBoolButton(label, getter, setter, DebugTone.Success, toolName, description);

        public DebugGroupBuilder AddInt(string label, Func<int> getter, Action<int> setter, string? toolName = null, string? description = null) =>
            AddValue(label, getter, setter, toolName, description);

        public DebugGroupBuilder AddFloat(string label, Func<float> getter, Action<float> setter, string? toolName = null, string? description = null) =>
            AddValue(label, getter, setter, toolName, description);

        public DebugGroupBuilder AddString(string label, Func<string> getter, Action<string> setter, string? toolName = null, string? description = null) =>
            AddValue(label, getter, setter, toolName, description);

        public DebugGroupBuilder AddSlider(
            string label,
            float lowValue,
            float highValue,
            Func<float> getter,
            Action<float>? setter = null,
            string format = "0.##",
            string? toolName = null,
            string? description = null)
        {
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugFloatSliderNode(
                label, lowValue, highValue, getter, setter, format, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddSlider(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            Action<int>? setter = null,
            string format = "0",
            string? toolName = null,
            string? description = null)
        {
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            GroupNode.Children.Add(new DebugIntSliderNode(
                label, lowValue, highValue, getter, setter, format, toolName, description));
            return this;
        }

        public DebugGroupBuilder AddSlider(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            string format)
        {
            GroupNode.Children.Add(new DebugIntSliderNode(
                label, lowValue, highValue, getter, null, format, null, null));
            return this;
        }

        public DebugGroupBuilder AddSlider(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            Action<int> setter,
            string toolName,
            string description)
        {
            return AddSlider(label, lowValue, highValue, getter, setter, "0", toolName, description);
        }

        public DebugGroupBuilder AddProgress(
            string label,
            float lowValue,
            float highValue,
            Func<float> getter,
            string format = "0.##")
        {
            GroupNode.Children.Add(new DebugProgressNode(label, lowValue, highValue, getter, format));
            return this;
        }

        public DebugGroupBuilder AddProgress(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            string format = "0")
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            GroupNode.Children.Add(new DebugProgressNode(label, lowValue, highValue, () => getter(), format));
            return this;
        }

        public DebugGroupBuilder AddProgressBar(
            string label,
            float lowValue,
            float highValue,
            Func<float> getter,
            string format = "[{0:F2}]")
        {
            return AddProgress(label, lowValue, highValue, getter, format);
        }

        public DebugGroupBuilder AddProgressBar(
            string label,
            int lowValue,
            int highValue,
            Func<int> getter,
            string format = "[{0}]")
        {
            return AddProgress(label, lowValue, highValue, getter, format);
        }

        public DebugGroupBuilder AddImage(string label, Texture2D texture)
        {
            GroupNode.Children.Add(new DebugImageNode(label, () => Background.FromTexture2D(texture)));
            return this;
        }

        public DebugGroupBuilder AddImage(string label, Sprite sprite)
        {
            GroupNode.Children.Add(new DebugImageNode(label, () => Background.FromSprite(sprite)));
            return this;
        }

        public DebugGroupBuilder AddImage(string label, RenderTexture renderTexture)
        {
            GroupNode.Children.Add(new DebugImageNode(label, () => Background.FromRenderTexture(renderTexture)));
            return this;
        }

        public DebugGroupBuilder AddImage(string label, VectorImage vectorImage)
        {
            GroupNode.Children.Add(new DebugImageNode(label, () => Background.FromVectorImage(vectorImage)));
            return this;
        }

        public DebugGroupBuilder AddImage(string label, Func<Texture2D> getter)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            GroupNode.Children.Add(new DebugImageNode(label, () => Background.FromTexture2D(getter())));
            return this;
        }

        public DebugGroupBuilder AddImage(string label, Func<Sprite> getter)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            GroupNode.Children.Add(new DebugImageNode(label, () => Background.FromSprite(getter())));
            return this;
        }

        public DebugGroupBuilder AddImage(string label, Func<RenderTexture> getter)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            GroupNode.Children.Add(new DebugImageNode(label, () => Background.FromRenderTexture(getter())));
            return this;
        }

        public DebugGroupBuilder AddImage(string label, Func<VectorImage> getter)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            GroupNode.Children.Add(new DebugImageNode(label, () => Background.FromVectorImage(getter())));
            return this;
        }

        public DebugGroupBuilder AddGroup(string label, Action<DebugGroupBuilder> configure, bool registerAsTool = true)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var toolName = registerAsTool && GroupNode.IsToolRooted
                ? DebugToolUtility.ToGeneratedToolName(label)
                : null;
            var description = toolName == null ? null : $"Debug group for {label}.";
            return AddGroupInternal(label, toolName, description, configure);
        }

        public DebugGroupBuilder AddGroup(
            string label,
            string toolName,
            string description,
            Action<DebugGroupBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);
            return AddGroupInternal(label, toolName, description, configure);
        }

        public DebugGroupBuilder AddFoldout(string label, Action<DebugGroupBuilder> configure)
        {
            return AddGroup(label, configure, false);
        }

        public DebugGroupBuilder AddHorizontalGroup(Action<DebugGroupBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var node = new DebugInlineGroupNode(FlexDirection.Row);
            configure(new DebugGroupBuilder(node));
            GroupNode.Children.Add(node);
            return this;
        }

        public DebugGroupBuilder AddVerticalGroup(Action<DebugGroupBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var node = new DebugInlineGroupNode(FlexDirection.Column);
            configure(new DebugGroupBuilder(node));
            GroupNode.Children.Add(node);
            return this;
        }

        private DebugGroupBuilder AddGroupInternal(
            string label,
            string? toolName,
            string? description,
            Action<DebugGroupBuilder> configure)
        {
            var node = new DebugGroupNode(label, toolName, description, GroupNode.IsToolRooted);
            configure(new DebugGroupBuilder(node));
            GroupNode.Children.Add(node);
            return this;
        }
    }
}
