#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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

    public static class RuntimeConsoleTabRegistry
    {
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<string, Registration>
            Factories = new(StringComparer.Ordinal);

        /// <summary>
        /// Registers a process-wide tab factory. Register before the active <see cref="RuntimeConsoleModule"/>
        /// initializes; the factory is evaluated once for each console host initialization.
        /// </summary>
        public static IDisposable Register(
            string id,
            Func<RuntimeConsoleContext, IEnumerable<IRuntimeConsoleTab>> factory)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Runtime Console factory id is required.", nameof(id));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (SyncRoot)
            {
                if (Factories.ContainsKey(id))
                    throw new InvalidOperationException($"Runtime Console factory '{id}' is already registered.");
                var registration = new Registration(id, factory);
                Factories.Add(id, registration);
                return new RegistrationHandle(registration);
            }
        }

        internal static IReadOnlyList<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            List<Registration> registrations;
            lock (SyncRoot)
                registrations = Factories.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToList();
            return registrations
                .SelectMany(registration => registration.Factory(context) ?? Array.Empty<IRuntimeConsoleTab>())
                .Where(tab => tab != null).ToList();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (SyncRoot) Factories.Clear();
        }

        private static void Unregister(Registration registration)
        {
            lock (SyncRoot)
            {
                if (Factories.TryGetValue(registration.Id, out var current) &&
                    ReferenceEquals(current, registration))
                    Factories.Remove(registration.Id);
            }
        }

        private sealed class Registration
        {
            public Registration(
                string id,
                Func<RuntimeConsoleContext, IEnumerable<IRuntimeConsoleTab>> factory)
            {
                Id = id;
                Factory = factory;
            }

            public string Id { get; }

            public Func<RuntimeConsoleContext, IEnumerable<IRuntimeConsoleTab>> Factory { get; }
        }

        private sealed class RegistrationHandle : IDisposable
        {
            private Registration? _registration;

            public RegistrationHandle(Registration registration)
            {
                _registration = registration;
            }

            public void Dispose()
            {
                if (_registration == null) return;
                Unregister(_registration);
                _registration = null;
            }
        }
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
