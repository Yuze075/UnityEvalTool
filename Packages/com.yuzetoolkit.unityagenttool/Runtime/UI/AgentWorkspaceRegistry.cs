#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace YuzeToolkit.UnityAgent
{
    /// <summary>A live section contributed to the workbench System Info page by a downstream package.</summary>
    public interface IUnityAgentWorkspaceSection : IDisposable
    {
        VisualElement Root { get; }
        void Tick();
    }

    /// <summary>
    /// Downstream-only composition point. UnityDebugTool contributes its protected System Info and
    /// Performance views here without introducing an Agent -> Debug dependency.
    /// </summary>
    public static class UnityAgentWorkspaceRegistry
    {
        private static readonly List<Registration> SystemInfoRegistrations = new();
        private static int _revision;

        internal static int Revision => _revision;

        public static IDisposable RegisterSystemInfoSection(
            string id,
            int order,
            Func<IUnityAgentWorkspaceSection> factory)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Section id is required.", nameof(id));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (SystemInfoRegistrations.Any(value => string.Equals(value.Id, id, StringComparison.Ordinal)))
                throw new InvalidOperationException($"System Info section '{id}' is already registered.");
            var registration = new Registration(id, order, factory);
            SystemInfoRegistrations.Add(registration);
            _revision++;
            return new RegistrationHandle(registration);
        }

        internal static IReadOnlyList<IUnityAgentWorkspaceSection> CreateSystemInfoSections()
        {
            var sections = new List<IUnityAgentWorkspaceSection>();
            try
            {
                foreach (var registration in SystemInfoRegistrations
                             .OrderBy(value => value.Order).ThenBy(value => value.Id, StringComparer.Ordinal))
                    sections.Add(registration.Factory());
                return sections;
            }
            catch
            {
                for (var index = sections.Count - 1; index >= 0; index--)
                    sections[index].Dispose();
                throw;
            }
        }

        private sealed class Registration
        {
            public Registration(string id, int order, Func<IUnityAgentWorkspaceSection> factory)
            {
                Id = id;
                Order = order;
                Factory = factory;
            }

            public string Id { get; }
            public int Order { get; }
            public Func<IUnityAgentWorkspaceSection> Factory { get; }
        }

        private sealed class RegistrationHandle : IDisposable
        {
            private Registration? _registration;

            public RegistrationHandle(Registration registration) => _registration = registration;

            public void Dispose()
            {
                if (_registration == null) return;
                SystemInfoRegistrations.Remove(_registration);
                _registration = null;
                _revision++;
            }
        }
    }

    /// <summary>Editor-only Eval broker controls exposed without making the runtime assembly depend on Editor code.</summary>
    public static class UnityAgentEvalSettingsBridge
    {
        private static Func<bool>? _getBrokerEnabled;
        private static Action<bool>? _setBrokerEnabled;

        public static bool IsBrokerControlAvailable => _getBrokerEnabled != null && _setBrokerEnabled != null;
        public static bool BrokerEnabled => _getBrokerEnabled?.Invoke() ?? false;

        public static void SetBrokerEnabled(bool enabled)
        {
            if (_setBrokerEnabled == null)
                throw new InvalidOperationException("The Eval Broker control is only available in the Unity Editor.");
            _setBrokerEnabled(enabled);
        }

        public static void ConfigureBrokerControl(Func<bool> getEnabled, Action<bool> setEnabled)
        {
            _getBrokerEnabled = getEnabled ?? throw new ArgumentNullException(nameof(getEnabled));
            _setBrokerEnabled = setEnabled ?? throw new ArgumentNullException(nameof(setEnabled));
        }
    }
}
