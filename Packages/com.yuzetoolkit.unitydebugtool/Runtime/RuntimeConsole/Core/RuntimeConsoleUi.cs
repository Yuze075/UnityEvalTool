#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    public static class RuntimeConsoleDesignTokens
    {
        public static readonly Color TextPrimary = new Color32(249, 250, 251, 255);
        public static readonly Color TextSecondary = new Color32(207, 211, 214, 255);
        public static readonly Color TextCaption = new Color32(129, 133, 140, 255);
        public static readonly Color Accent = new Color32(96, 165, 250, 255);
        public static readonly Color Error = new Color32(242, 90, 90, 255);
        public static readonly Color Warning = new Color32(221, 134, 41, 255);
        public static readonly Color Success = new Color32(34, 197, 94, 255);
    }

    public static class RuntimeConsoleUi
    {
        public static readonly Color RunningColor = RuntimeConsoleDesignTokens.Success;
        public static readonly Color StoppedColor = RuntimeConsoleDesignTokens.Error;
        public static readonly Color WarningColor = RuntimeConsoleDesignTokens.Warning;
        public static readonly Color ErrorColor = RuntimeConsoleDesignTokens.Error;

        public static Button CreateButton(string text, string tooltip, Action clicked, int width = 84)
        {
            var button = new Button(clicked)
            {
                text = text
            };
            button.focusable = false;
            button.tabIndex = -1;
            RuntimeConsoleUss.ApplyOwnedControl(button);
            button.AddToClassList(RuntimeConsoleUss.ButtonClass);
            AttachHelp(button, tooltip);
            button.style.width = width;
            return button;
        }

        public static TextField CreateTextField(string label, string value, string tooltip, bool isPassword = false)
        {
            var field = new TextField(label)
            {
                value = value,
                isPasswordField = isPassword
            };
            field.tabIndex = -1;
            RuntimeConsoleUss.ApplyOwnedControl(field);
            AttachHelp(field, tooltip);
            return field;
        }

        public static IntegerField CreateIntegerField(string label, int value, string tooltip)
        {
            var field = new IntegerField(label)
            {
                value = Mathf.Max(0, value)
            };
            field.focusable = false;
            field.tabIndex = -1;
            RuntimeConsoleUss.ApplyOwnedControl(field);
            AttachHelp(field, tooltip);
            return field;
        }

        public static Toggle CreateToggle(string label, bool value, string tooltip)
        {
            var toggle = new Toggle(label)
            {
                value = value
            };
            toggle.focusable = false;
            toggle.tabIndex = -1;
            RuntimeConsoleUss.ApplyOwnedControl(toggle);
            RuntimeConsoleUss.ApplyOwnedToggle(toggle);
            AttachHelp(toggle, tooltip);
            return toggle;
        }

        public static VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList(RuntimeConsoleUss.ToolbarClass);
            return toolbar;
        }

        public static VisualElement CreatePage()
        {
            var page = new VisualElement();
            page.AddToClassList(RuntimeConsoleUss.PageClass);
            return page;
        }

        public static RuntimeConsolePanView CreatePanView()
        {
            return new RuntimeConsolePanView();
        }

        public static VisualElement CreateCard()
        {
            var card = new VisualElement();
            card.AddToClassList(RuntimeConsoleUss.CardClass);
            return card;
        }

        public static Label AddTitle(VisualElement parent, string text)
        {
            var title = new Label(text) { enableRichText = false };
            title.AddToClassList(RuntimeConsoleUss.CardTitleClass);
            parent.Add(title);
            return title;
        }

        public static Label AddField(VisualElement parent, string labelText)
        {
            var row = new VisualElement();
            row.AddToClassList(RuntimeConsoleUss.FieldRowClass);
            parent.Add(row);

            var label = new Label(labelText) { enableRichText = false };
            label.AddToClassList(RuntimeConsoleUss.FieldLabelClass);
            row.Add(label);

            var value = new Label("-") { enableRichText = false };
            value.AddToClassList(RuntimeConsoleUss.FieldValueClass);
            row.Add(value);
            return value;
        }

        public static VisualElement CreateMessage(string text, Color accentColor)
        {
            var box = new VisualElement();
            box.AddToClassList(RuntimeConsoleUss.MessageClass);
            box.style.borderLeftColor = accentColor;
            box.style.backgroundColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.12f);

            var label = new Label(text) { enableRichText = false };
            label.AddToClassList(RuntimeConsoleUss.LabelClass);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);
            return box;
        }

        public static void AttachHelp(VisualElement target, string helpText)
        {
            AttachHelp(target, () => helpText);
        }

        public static void AttachHelp(VisualElement target, Func<string> helpTextProvider)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (helpTextProvider == null) throw new ArgumentNullException(nameof(helpTextProvider));

            target.RegisterCallback<TooltipEvent>(evt =>
            {
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerEnterEvent>(evt =>
            {
                var text = helpTextProvider();
                if (!string.IsNullOrWhiteSpace(text))
                    ShowHelp(target, text, evt.position);
            });
            target.RegisterCallback<PointerMoveEvent>(evt => PositionHelp(target, evt.position));
            target.RegisterCallback<PointerLeaveEvent>(_ => HideHelp(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => HideHelp(target));
        }

        private const string HelpPopupName = "yuzu-runtime-console-owned-help-popup";
        private const string HelpTextName = "yuzu-runtime-console-owned-help-text";

        private static void ShowHelp(VisualElement target, string text, Vector2 worldPosition)
        {
            var root = target.panel?.visualTree;
            if (root == null) return;
            var popup = root.Q<VisualElement>(HelpPopupName);
            if (popup == null)
            {
                popup = new VisualElement { name = HelpPopupName, pickingMode = PickingMode.Ignore };
                popup.AddToClassList(RuntimeConsoleUss.HelpPopupClass);
                var label = new Label
                {
                    name = HelpTextName,
                    pickingMode = PickingMode.Ignore,
                    enableRichText = false
                };
                label.AddToClassList(RuntimeConsoleUss.HelpTextClass);
                popup.Add(label);
                root.Add(popup);
            }

            var textLabel = popup.Q<Label>(HelpTextName);
            if (textLabel != null) textLabel.text = text;
            popup.style.display = DisplayStyle.Flex;
            popup.BringToFront();
            PositionHelp(target, worldPosition);
        }

        private static void PositionHelp(VisualElement target, Vector2 worldPosition)
        {
            var root = target.panel?.visualTree;
            var popup = root?.Q<VisualElement>(HelpPopupName);
            if (root == null || popup == null || popup.style.display == DisplayStyle.None) return;

            var local = root.WorldToLocal(worldPosition);
            var width = float.IsNaN(popup.resolvedStyle.width) ? 300f : popup.resolvedStyle.width;
            var height = float.IsNaN(popup.resolvedStyle.height) ? 56f : popup.resolvedStyle.height;
            popup.style.left = Mathf.Clamp(local.x + 12f, 8f,
                Mathf.Max(8f, root.resolvedStyle.width - width - 8f));
            popup.style.top = Mathf.Clamp(local.y + 17f, 8f,
                Mathf.Max(8f, root.resolvedStyle.height - height - 8f));
        }

        private static void HideHelp(VisualElement target)
        {
            var popup = target.panel?.visualTree.Q<VisualElement>(HelpPopupName);
            if (popup != null) popup.style.display = DisplayStyle.None;
        }

        public static string FormatDateTime(DateTime utc)
        {
            return utc == default ? "-" : utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
            if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
            if (duration.TotalMinutes >= 1) return $"{duration.Minutes}m {duration.Seconds}s";
            return $"{Math.Max(0, duration.TotalSeconds):0}s";
        }

        public static string FormatMilliseconds(double milliseconds)
        {
            return milliseconds < 1000
                ? $"{Math.Max(0, milliseconds):0}ms"
                : $"{Math.Max(0, milliseconds / 1000):0.00}s";
        }

        public static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "-";
            return id.Length <= 8 ? id : id[..8];
        }
    }

    public sealed class RuntimeConsolePanView
    {
        private const float WheelStepMultiplier = 24f;
        private const float MinThumbHeight = 30f;

        private readonly VisualElement _root;
        private readonly VisualElement _content;
        private readonly VisualElement _scrollbar;
        private readonly VisualElement _scrollbarThumb;
        private float _offset;
        private float _dragStartY;
        private float _dragStartOffset;
        private bool _isDragging;
        private int _activePointerId = -1;

        public RuntimeConsolePanView()
        {
            _root = new VisualElement();
            _root.AddToClassList(RuntimeConsoleUss.PanViewClass);
            _root.RegisterCallback<WheelEvent>(OnWheel);
            _root.RegisterCallback<GeometryChangedEvent>(_ => ClampOffset());

            _content = new VisualElement();
            _content.AddToClassList(RuntimeConsoleUss.PanViewContentClass);
            _content.RegisterCallback<GeometryChangedEvent>(_ => ClampOffset());
            _root.Add(_content);

            _scrollbar = new VisualElement();
            _scrollbar.AddToClassList(RuntimeConsoleUss.PanViewScrollbarClass);
            _scrollbar.RegisterCallback<PointerDownEvent>(OnScrollbarPointerDown);

            _scrollbarThumb = new VisualElement();
            _scrollbarThumb.AddToClassList(RuntimeConsoleUss.PanViewScrollbarThumbClass);
            _scrollbarThumb.RegisterCallback<PointerDownEvent>(OnThumbPointerDown);
            _scrollbarThumb.RegisterCallback<PointerMoveEvent>(OnThumbPointerMove);
            _scrollbarThumb.RegisterCallback<PointerUpEvent>(OnThumbPointerUp);
            _scrollbarThumb.RegisterCallback<PointerCancelEvent>(OnThumbPointerCancel);
            _scrollbarThumb.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            _scrollbar.Add(_scrollbarThumb);
            _root.Add(_scrollbar);
        }

        public VisualElement Root => _root;

        public VisualElement Content => _content;

        public void Add(VisualElement element)
        {
            _content.Add(element);
            ScheduleClamp();
        }

        public void Clear()
        {
            _content.Clear();
            _offset = 0f;
            ApplyOffset();
        }

        public void ScrollToEnd()
        {
            _root.schedule.Execute(() =>
            {
                _offset = MaxOffset();
                ApplyOffset();
            });
        }

        public void ResetOffset()
        {
            _offset = 0f;
            ApplyOffset();
            ScheduleClamp();
        }

        public void Refresh()
        {
            ScheduleClamp();
        }

        private void OnWheel(WheelEvent evt)
        {
            var max = MaxOffset();
            if (max <= 0f) return;

            _offset = Mathf.Clamp(_offset + evt.delta.y * WheelStepMultiplier, 0f, max);
            ApplyOffset();
            evt.StopPropagation();
        }

        private void OnScrollbarPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            var max = MaxOffset();
            var travel = ThumbTravel();
            if (max <= 0f || travel <= 0f) return;

            var thumbHeight = ThumbHeight();
            _offset = Mathf.Clamp((evt.localPosition.y - thumbHeight * 0.5f) / travel * max, 0f, max);
            ApplyOffset();
            evt.StopPropagation();
        }

        private void OnThumbPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || MaxOffset() <= 0f) return;

            _isDragging = true;
            _activePointerId = evt.pointerId;
            _dragStartY = evt.position.y;
            _dragStartOffset = _offset;
            _scrollbarThumb.CapturePointer(evt.pointerId);
            _scrollbarThumb.AddToClassList(RuntimeConsoleUss.PanViewScrollbarThumbActiveClass);
            evt.StopPropagation();
        }

        private void OnThumbPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || evt.pointerId != _activePointerId) return;

            var max = MaxOffset();
            var travel = ThumbTravel();
            if (max <= 0f || travel <= 0f) return;

            var delta = evt.position.y - _dragStartY;
            _offset = Mathf.Clamp(_dragStartOffset + delta / travel * max, 0f, max);
            ApplyOffset();
            evt.StopPropagation();
        }

        private void OnThumbPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _activePointerId) return;

            if (_scrollbarThumb.HasPointerCapture(evt.pointerId))
                _scrollbarThumb.ReleasePointer(evt.pointerId);
            EndDrag();
            evt.StopPropagation();
        }

        private void OnThumbPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId != _activePointerId) return;

            if (_scrollbarThumb.HasPointerCapture(evt.pointerId))
                _scrollbarThumb.ReleasePointer(evt.pointerId);
            EndDrag();
            evt.StopPropagation();
        }

        private void ClampOffset()
        {
            _offset = Mathf.Clamp(_offset, 0f, MaxOffset());
            ApplyOffset();
        }

        private void ScheduleClamp()
        {
            _root.schedule.Execute(ClampOffset);
        }

        private float MaxOffset()
        {
            var viewportHeight = Mathf.Max(0f, _root.contentRect.height);
            var contentHeight = ContentHeight();
            return Mathf.Max(0f, contentHeight - viewportHeight);
        }

        private void ApplyOffset()
        {
            _content.transform.position = new Vector3(0f, -_offset, 0f);
            UpdateScrollbar();
        }

        private void UpdateScrollbar()
        {
            var max = MaxOffset();
            if (max <= 0f)
            {
                _scrollbar.style.display = DisplayStyle.None;
                return;
            }

            _scrollbar.style.display = DisplayStyle.Flex;

            var thumbHeight = ThumbHeight();
            var travel = ThumbTravel(thumbHeight);
            var thumbTop = travel <= 0f ? 0f : _offset / max * travel;
            _scrollbarThumb.style.height = thumbHeight;
            _scrollbarThumb.style.top = thumbTop;
        }

        private float ThumbHeight()
        {
            var viewportHeight = Mathf.Max(0f, _root.contentRect.height);
            var contentHeight = ContentHeight();
            if (viewportHeight <= 0f || contentHeight <= 0f) return MinThumbHeight;

            var trackHeight = TrackHeight();
            if (trackHeight <= MinThumbHeight) return trackHeight;

            return Mathf.Clamp(viewportHeight / contentHeight * trackHeight, MinThumbHeight, trackHeight);
        }

        private float ThumbTravel()
        {
            return ThumbTravel(ThumbHeight());
        }

        private float ThumbTravel(float thumbHeight)
        {
            return Mathf.Max(0f, TrackHeight() - thumbHeight);
        }

        private float TrackHeight()
        {
            var trackHeight = _scrollbar.contentRect.height;
            return Mathf.Max(0f, trackHeight > 0f ? trackHeight : _root.contentRect.height);
        }

        private float ContentHeight()
        {
            var contentHeight = Mathf.Max(0f, _content.layout.height);
            foreach (var child in _content.Children())
                contentHeight = Mathf.Max(contentHeight, child.layout.yMax + _content.resolvedStyle.paddingBottom);

            return contentHeight;
        }

        private void EndDrag()
        {
            _isDragging = false;
            _activePointerId = -1;
            _scrollbarThumb.RemoveFromClassList(RuntimeConsoleUss.PanViewScrollbarThumbActiveClass);
        }
    }
}
