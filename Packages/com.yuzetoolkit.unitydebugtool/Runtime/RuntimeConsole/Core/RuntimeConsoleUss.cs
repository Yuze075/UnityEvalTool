#nullable enable

namespace YuzeToolkit
{
    public static class RuntimeConsoleUss
    {
        public const string LayerClass = "yuzu-debug-console-layer";
        public const string WindowClass = "yuzu-runtime-console";
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
        public const string CardClass = "yuzu-runtime-console-card";
        public const string CardTitleClass = "yuzu-runtime-console-card-title";
        public const string FieldRowClass = "yuzu-runtime-console-field-row";
        public const string FieldLabelClass = "yuzu-runtime-console-field-label";
        public const string FieldValueClass = "yuzu-runtime-console-field-value";
        public const string MessageClass = "yuzu-runtime-console-message";
        public const string ResizeGripClass = "yuzu-runtime-console-resize-grip";
        public const string LabelClass = "yuzu-debug-label";
        public const string MutedLabelClass = "yuzu-debug-label-muted";

        public static void ApplyLayer(UnityEngine.UIElements.VisualElement layer)
        {
            layer.AddToClassList(LayerClass);
            layer.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
        }
    }
}
