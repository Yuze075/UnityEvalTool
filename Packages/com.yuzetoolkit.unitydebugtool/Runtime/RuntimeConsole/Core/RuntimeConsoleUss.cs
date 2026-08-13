#nullable enable
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    public static class RuntimeConsoleUss
    {
        public const string LayerClass = "yuzu-debug-console-layer";
        public const string WindowClass = "yuzu-runtime-console";
        public const string CollapsedWindowClass = "yuzu-runtime-console-collapsed";
        public const string HeaderClass = "yuzu-runtime-console-header";
        public const string TitleClass = "yuzu-runtime-console-title";
        public const string DragHintClass = "yuzu-runtime-console-drag-hint";
        public const string CollapseButtonClass = "yuzu-runtime-console-collapse-button";
        public const string CollapseIconClass = "yuzu-runtime-console-collapse-icon";
        public const string CollapseIconOpenClass = "yuzu-runtime-console-collapse-icon-open";
        public const string HeaderActionSeatClass = "yuzu-runtime-console-header-action-seat";
        public const string TabBarClass = "yuzu-runtime-console-tab-bar";
        public const string TabButtonClass = "yuzu-runtime-console-tab-button";
        public const string ActiveTabButtonClass = "yuzu-runtime-console-tab-button-active";
        public const string ContentClass = "yuzu-runtime-console-content";
        public const string TabRootClass = "yuzu-runtime-console-tab-root";
        public const string PageClass = "yuzu-runtime-console-page";
        public const string PanViewClass = "yuzu-runtime-console-pan-view";
        public const string PanViewContentClass = "yuzu-runtime-console-pan-content";
        public const string PanViewScrollbarClass = "yuzu-runtime-console-pan-scrollbar";
        public const string PanViewScrollbarThumbClass = "yuzu-runtime-console-pan-scrollbar-thumb";
        public const string PanViewScrollbarThumbActiveClass = "yuzu-runtime-console-pan-scrollbar-thumb-active";
        public const string ToolbarClass = "yuzu-runtime-console-toolbar";
        public const string ButtonClass = "yuzu-runtime-console-button";
        public const string ControlClass = "yuzu-runtime-console-control";
        public const string SwitchClass = "yuzu-runtime-console-switch";
        public const string SwitchOnClass = "yuzu-runtime-console-switch-on";
        public const string SwitchOffClass = "yuzu-runtime-console-switch-off";
        public const string SwitchBlockedClass = "yuzu-runtime-console-switch-blocked";
        public const string SwitchIndicatorClass = "yuzu-runtime-console-switch-indicator";
        public const string ToggleClass = "yuzu-runtime-console-toggle";
        public const string ToggleStatusClass = "yuzu-runtime-console-toggle-status";
        public const string ToggleTrackClass = "yuzu-runtime-console-toggle-track";
        public const string ToggleThumbClass = "yuzu-runtime-console-toggle-thumb";
        public const string ToggleOnClass = "yuzu-runtime-console-toggle-on";
        public const string DisclosureClass = "yuzu-runtime-console-disclosure";
        public const string DisclosureOpenClass = "yuzu-runtime-console-disclosure-open";
        public const string CardClass = "yuzu-runtime-console-card";
        public const string CardTitleClass = "yuzu-runtime-console-card-title";
        public const string FieldRowClass = "yuzu-runtime-console-field-row";
        public const string FieldLabelClass = "yuzu-runtime-console-field-label";
        public const string FieldValueClass = "yuzu-runtime-console-field-value";
        public const string MessageClass = "yuzu-runtime-console-message";
        public const string ResizeGripClass = "yuzu-runtime-console-resize-grip";
        public const string ResizeLineClass = "yuzu-runtime-console-resize-line";
        public const string ResizeLineShortClass = "yuzu-runtime-console-resize-line-short";
        public const string SearchIconClass = "yuzu-runtime-console-search-icon";
        public const string SearchIconHandleClass = "yuzu-runtime-console-search-icon-handle";
        public const string SearchPlaceholderClass = "yuzu-runtime-console-search-placeholder";
        public const string HelpPopupClass = "yuzu-runtime-console-help-popup";
        public const string HelpTextClass = "yuzu-runtime-console-help-text";
        public const string LabelClass = "yuzu-debug-label";
        public const string MutedLabelClass = "yuzu-debug-label-muted";

        public static void ApplyOwnedControl(UnityEngine.UIElements.VisualElement control)
        {
            if (control is TextElement textElement)
                textElement.enableRichText = false;
            control.AddToClassList(ControlClass);
        }

        public static void ApplyLayer(UnityEngine.UIElements.VisualElement layer)
        {
            layer.AddToClassList(LayerClass);
            layer.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
        }

        public static void ApplySwitch(UnityEngine.UIElements.Button button, string text, bool enabled, bool blocked = false)
        {
            button.enableRichText = false;
            button.text = text;
            button.AddToClassList(SwitchClass);
            button.EnableInClassList(SwitchOnClass, enabled && !blocked);
            button.EnableInClassList(SwitchOffClass, !enabled && !blocked);
            button.EnableInClassList(SwitchBlockedClass, blocked);

            var indicator = button.Q<VisualElement>(className: SwitchIndicatorClass);
            if (indicator != null) return;
            indicator = new VisualElement
            {
                pickingMode = UnityEngine.UIElements.PickingMode.Ignore
            };
            indicator.AddToClassList(SwitchIndicatorClass);
            button.Insert(0, indicator);
        }

        public static void ApplyDisclosure(Foldout foldout)
        {
            var toggle = foldout.Q<Toggle>();
            if (toggle == null) return;
            var indicator = new VisualElement { pickingMode = PickingMode.Ignore };
            indicator.AddToClassList(DisclosureClass);
            (toggle.Q<VisualElement>(className: "unity-toggle__input") ?? toggle).Insert(0, indicator);
            void Sync(bool open) => indicator.EnableInClassList(DisclosureOpenClass, open);
            Sync(foldout.value);
            foldout.RegisterValueChangedCallback(evt => Sync(evt.newValue));
        }

        public static void ApplyOwnedToggle(Toggle toggle)
        {
            toggle.AddToClassList(ToggleClass);
            var input = toggle.Q<VisualElement>(className: "unity-toggle__input")
                ?? throw new System.InvalidOperationException("Toggle input visual was not created.");
            var status = new Label { pickingMode = PickingMode.Ignore };
            status.enableRichText = false;
            status.AddToClassList(ToggleStatusClass);
            input.Add(status);
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList(ToggleTrackClass);
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(ToggleThumbClass);
            track.Add(thumb);
            input.Add(track);

            void Sync(bool value)
            {
                status.text = value ? "On" : "Off";
                toggle.EnableInClassList(ToggleOnClass, value);
            }
            Sync(toggle.value);
            toggle.RegisterValueChangedCallback(evt => Sync(evt.newValue));
        }
    }
}
