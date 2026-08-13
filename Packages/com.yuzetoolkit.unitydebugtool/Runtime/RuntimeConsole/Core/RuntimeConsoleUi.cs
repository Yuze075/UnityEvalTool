#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    public static class RuntimeConsoleUi
    {
        public static readonly Color RunningColor = new(0.33f, 0.78f, 0.62f);
        public static readonly Color StoppedColor = new(0.92f, 0.34f, 0.31f);
        public static readonly Color WarningColor = new(0.95f, 0.68f, 0.26f);
        public static readonly Color ErrorColor = new(0.96f, 0.36f, 0.34f);

        public static Button CreateButton(string text, string tooltip, Action clicked, int width = 84)
        {
            var button = new Button(clicked)
            {
                text = text,
                tooltip = tooltip
            };
            button.focusable = false;
            button.tabIndex = -1;
            button.AddToClassList(RuntimeConsoleUss.ButtonClass);
            button.style.width = width;
            return button;
        }

        public static TextField CreateTextField(string label, string value, string tooltip, bool isPassword = false)
        {
            var field = new TextField(label)
            {
                value = value,
                tooltip = tooltip,
                isPasswordField = isPassword
            };
            field.tabIndex = -1;
            return field;
        }

        public static IntegerField CreateIntegerField(string label, int value, string tooltip)
        {
            var field = new IntegerField(label)
            {
                value = Mathf.Max(0, value),
                tooltip = tooltip
            };
            field.focusable = false;
            field.tabIndex = -1;
            return field;
        }

        public static Toggle CreateToggle(string label, bool value, string tooltip)
        {
            var toggle = new Toggle(label)
            {
                value = value,
                tooltip = tooltip
            };
            toggle.focusable = false;
            toggle.tabIndex = -1;
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
            var title = new Label(text);
            title.AddToClassList(RuntimeConsoleUss.CardTitleClass);
            parent.Add(title);
            return title;
        }

        public static Label AddField(VisualElement parent, string labelText)
        {
            var row = new VisualElement();
            row.AddToClassList(RuntimeConsoleUss.FieldRowClass);
            parent.Add(row);

            var label = new Label(labelText);
            label.AddToClassList(RuntimeConsoleUss.FieldLabelClass);
            row.Add(label);

            var value = new Label("-");
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

            var label = new Label(text);
            label.AddToClassList(RuntimeConsoleUss.LabelClass);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);
            return box;
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

            _scrollbar = new VisualElement { tooltip = "Scroll vertically." };
            _scrollbar.AddToClassList(RuntimeConsoleUss.PanViewScrollbarClass);
            _scrollbar.RegisterCallback<PointerDownEvent>(OnScrollbarPointerDown);

            _scrollbarThumb = new VisualElement { tooltip = "Drag to scroll." };
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
