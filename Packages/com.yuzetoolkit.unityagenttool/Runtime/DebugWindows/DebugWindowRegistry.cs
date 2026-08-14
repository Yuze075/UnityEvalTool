#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YuzeToolkit
{
    internal static class DebugWindowRegistry
    {
        private static readonly List<DebugWindowRegistration> Registrations = new();
        private static int _revision;

        public static IReadOnlyList<DebugWindowRegistration> RegisteredWindows => Registrations;
        public static int Revision => _revision;

        public static IDisposable RegisterWindow(string? toolName, string? description,
            Action<DebugWindowBuilder> configure)
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

            if (registration.RootTool != null)
                EvalToolRegistry.RegisterRoot(registration.RootTool);
            Registrations.Add(registration);
            _revision++;
            return new Handle(registration);
        }

        private static void Unregister(DebugWindowRegistration registration)
        {
            if (!Registrations.Remove(registration)) return;
            if (registration.RootTool != null)
                EvalToolRegistry.TryUnregisterRoot(registration.RootTool);
            _revision++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            foreach (var registration in Registrations)
            {
                if (registration.RootTool != null)
                    EvalToolRegistry.TryUnregisterRoot(registration.RootTool);
            }
            Registrations.Clear();
            _revision++;
        }

        private sealed class Handle : IDisposable
        {
            private DebugWindowRegistration? _registration;

            public Handle(DebugWindowRegistration registration) => _registration = registration;

            public void Dispose()
            {
                if (_registration == null) return;
                Unregister(_registration);
                _registration = null;
            }
        }
    }
}
