#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace YuzeToolkit
{
    internal static class DebugWindowRegistry
    {
        private static readonly List<DebugWindowRegistration> Registrations = new();
        private static readonly HashSet<DebugWindowModule> Hosts = new();

        static DebugWindowRegistry()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        public static IReadOnlyList<DebugWindowRegistration> RegisteredWindows => Registrations;

        public static IDisposable RegisterWindow(string? toolName, string? description, Action<DebugWindowBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            DebugToolUtility.ValidateOptionalToolMetadata(toolName, description);

            var builder = new DebugWindowBuilder(toolName, description);
            configure(builder);

            return Register(builder, null);
        }

        public static IDisposable RegisterWindow(DebugEvalToolBuilder evalToolBuilder,
            Action<DebugWindowBuilder> configure)
        {
            if (evalToolBuilder == null) throw new ArgumentNullException(nameof(evalToolBuilder));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var builder = new DebugWindowBuilder(null, null);
            configure(builder);
            return Register(builder, evalToolBuilder.Build());
        }

        private static IDisposable Register(DebugWindowBuilder builder, IEvalTool? explicitRootTool)
        {
            var registration = DebugWindowRegistration.Create(builder, explicitRootTool);
            if (registration.RootTool != null &&
                Registrations.Any(other => other.RootTool?.Name == registration.RootTool.Name))
                throw new InvalidOperationException(
                    $"Debug root tool '{registration.RootTool.Name}' is already registered.");

            Registrations.Add(registration);
            foreach (var host in Hosts)
                host.AttachRegistration(registration);

            return new Handle(registration);
        }

        public static void AddHost(DebugWindowModule host)
        {
            if (!Hosts.Add(host)) return;

            foreach (var registration in Registrations)
                host.AttachRegistration(registration);
        }

        public static void RemoveHost(DebugWindowModule host)
        {
            if (!Hosts.Remove(host)) return;

            foreach (var registration in Registrations)
                host.DetachRegistration(registration);
        }

        private static void Unregister(DebugWindowRegistration registration)
        {
            if (!Registrations.Remove(registration)) return;

            foreach (var host in Hosts)
                host.DetachRegistration(registration);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            ClearRuntimeHostState();
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.ExitingEditMode or PlayModeStateChange.ExitingPlayMode)
                ClearRuntimeHostState();
        }
#endif

        private static void ClearRuntimeHostState()
        {
            var registrations = Registrations.ToArray();
            var hosts = Hosts.ToArray();

            foreach (var host in hosts)
            {
                foreach (var registration in registrations)
                    host.DetachRegistration(registration);
                host.ClearEvalToolBindings();
            }

            foreach (var registration in registrations)
            {
                if (registration.RootTool != null)
                    EvalToolRegistry.UnregisterRoot(registration.RootTool.Name);
                registration.DisposeVisualElement();
            }

            Hosts.Clear();
        }

        private sealed class Handle : IDisposable
        {
            private DebugWindowRegistration? _registration;

            public Handle(DebugWindowRegistration registration)
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
}
