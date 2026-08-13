#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    public interface IRuntimeConsoleTab
    {
        string Id { get; }

        string Title { get; }

        int SortOrder { get; }

        VisualElement Root { get; }

        void SetVisible(bool visible);

        void Tick();

        void Shutdown();
    }

    public interface IRuntimeConsoleTabProvider
    {
        IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context);
    }

    public sealed class RuntimeConsoleContext
    {
        internal RuntimeConsoleContext(DebugPanelContext panelContext, MonoBehaviour owner)
        {
            PanelContext = panelContext;
            Owner = owner;
        }

        public DebugPanelContext PanelContext { get; }

        public MonoBehaviour Owner { get; }

        public void AddStyleSheet(StyleSheet styleSheet)
        {
            PanelContext.AddStyleSheet(styleSheet);
        }
    }

    public abstract class RuntimeConsoleTabBase : IRuntimeConsoleTab
    {
        protected RuntimeConsoleTabBase(string id, string title, int sortOrder)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Runtime console tab id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Runtime console tab title is required.", nameof(title));

            Id = id;
            Title = title;
            SortOrder = sortOrder;
            Root = new VisualElement { name = "runtime-console-tab-" + id };
            Root.AddToClassList(RuntimeConsoleUss.TabRootClass);
        }

        public string Id { get; }

        public string Title { get; }

        public int SortOrder { get; }

        public VisualElement Root { get; }

        public virtual void SetVisible(bool visible)
        {
            Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public virtual void Tick()
        {
        }

        public virtual void Shutdown()
        {
            Root.RemoveFromHierarchy();
        }
    }
}
