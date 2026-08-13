#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    public sealed class DebugWindowModule : MonoBehaviour, IDebugPanelModule
    {
        [SerializeField, Tooltip("USS used by the registered debug window module.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Whether registered debug windows are rendered in the top-left debug area.")]
        private bool renderDebugWindows = true;

        [SerializeField, Tooltip("Whether debug windows can be dragged by their headers.")]
        private bool allowWindowDragging = true;

        [SerializeField, Tooltip("Keyboard key used with the DebugPanel modifiers to show or hide registered debug windows.")]
        private Key toggleKey = Key.F9;

        private readonly HashSet<DebugWindowRegistration> _activeEvalToolRegistrations = new();
        private VisualElement? _layer;
        private bool _visible;

        public int SortOrder => 10;

        public Key ToggleKey => toggleKey;

        public bool AllowWindowDragging => allowWindowDragging;

        internal static IReadOnlyList<DebugWindowRegistration> RegisteredWindows => DebugWindowRegistry.RegisteredWindows;

        public static IDisposable RegisterWindow(Action<DebugWindowBuilder> configure)
        {
            return DebugWindowRegistry.RegisterWindow(null, null, configure);
        }

        public static IDisposable RegisterWindow(string toolName, string description, Action<DebugWindowBuilder> configure)
        {
            return DebugWindowRegistry.RegisterWindow(toolName, description, configure);
        }

        public static IDisposable RegisterWindow(DebugEvalToolBuilder evalToolBuilder,
            Action<DebugWindowBuilder> configure)
        {
            return DebugWindowRegistry.RegisterWindow(evalToolBuilder, configure);
        }

        public void Initialize(DebugPanelContext context)
        {
            if (renderDebugWindows && styleSheet == null)
            {
                Debug.LogError($"{nameof(DebugWindowModule)} requires a {nameof(StyleSheet)} reference.", this);
                return;
            }

            if (renderDebugWindows)
            {
                context.AddStyleSheet(styleSheet!);
                _layer = context.CreateLayer("unity-debug-tool-debug-layer");
                DebugWindowUss.ApplyLayer(_layer);
            }

            DebugWindowRegistry.AddHost(this);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_layer != null)
                _layer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Tick()
        {
            if (!_visible || !renderDebugWindows) return;

            foreach (var registration in DebugWindowRegistry.RegisteredWindows)
                registration.Refresh();
        }

        public void Shutdown()
        {
            DebugWindowRegistry.RemoveHost(this);

            _layer?.RemoveFromHierarchy();
            _layer = null;
        }

        internal void AttachRegistration(DebugWindowRegistration registration)
        {
            if (registration.RootTool != null && _activeEvalToolRegistrations.Add(registration))
                EvalToolRegistry.RegisterRoot(registration.RootTool);

            if (_layer == null || !renderDebugWindows) return;

            if (registration.VisualElement == null)
            {
                registration.BuildVisualElement(allowWindowDragging);
                if (registration.VisualElement != null)
                    _layer.Add(registration.VisualElement);
            }
        }

        internal void DetachRegistration(DebugWindowRegistration registration)
        {
            if (registration.RootTool != null && _activeEvalToolRegistrations.Remove(registration))
                EvalToolRegistry.UnregisterRoot(registration.RootTool.Name);

            if (registration.VisualElement != null)
                registration.VisualElement.RemoveFromHierarchy();
            registration.DisposeVisualElement();
        }

        internal void ClearEvalToolBindings()
        {
            foreach (var registration in _activeEvalToolRegistrations)
            {
                if (registration.RootTool != null)
                    EvalToolRegistry.UnregisterRoot(registration.RootTool.Name);
            }

            _activeEvalToolRegistrations.Clear();
        }
    }
}
