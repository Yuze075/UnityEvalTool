#nullable enable
using System;

namespace YuzeToolkit
{
    /// <summary>
    /// Public registration entry point for Debug Panel pages. Rendering is owned by the unified
    /// Unity Agent workbench, so registrations no longer require a scene component or a DebugPanel host.
    /// </summary>
    public static class DebugWindowModule
    {
        internal static System.Collections.Generic.IReadOnlyList<DebugWindowRegistration> RegisteredWindows =>
            DebugWindowRegistry.RegisteredWindows;

        public static IDisposable RegisterWindow(Action<DebugWindowBuilder> configure) =>
            DebugWindowRegistry.RegisterWindow(null, null, configure);

        [Obsolete("Visual DebugWindow metadata no longer creates an Eval Tool. Build an explicit " +
                  "DebugEvalToolBuilder and use RegisterWindow(DebugEvalToolBuilder, Action<DebugWindowBuilder> expected).")]
        public static IDisposable RegisterWindow(string toolName, string description,
            Action<DebugWindowBuilder> configure) =>
            DebugWindowRegistry.RegisterWindow(toolName, description, configure);

        public static IDisposable RegisterWindow(DebugEvalToolBuilder evalToolBuilder,
            Action<DebugWindowBuilder> configure) =>
            DebugWindowRegistry.RegisterWindow(evalToolBuilder, configure);
    }
}
