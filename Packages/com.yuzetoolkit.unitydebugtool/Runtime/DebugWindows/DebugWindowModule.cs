#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class DebugWindowModule : MonoBehaviour, IDebugPanelModule
    {
        [SerializeField, Tooltip("Required USS for rendered debug windows. Initialization fails when rendering is enabled and this reference is missing.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Whether registered windows render in the top-left area. Explicit Eval Tools still register when rendering is disabled.")]
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

        [Obsolete("Visual DebugWindow metadata no longer creates an Eval Tool. Build an explicit DebugEvalToolBuilder and use RegisterWindow(DebugEvalToolBuilder, Action<DebugWindowBuilder>).")]
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
                throw new MissingReferenceException(
                    $"{nameof(DebugWindowModule)} requires a {nameof(StyleSheet)} reference when window rendering is enabled.");

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
            if (!visible)
                ReleaseInteractionFocus();
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
            ReleaseInteractionFocus();
            DebugWindowRegistry.RemoveHost(this);

            _layer?.RemoveFromHierarchy();
            _layer = null;
        }

        private void ReleaseInteractionFocus()
        {
            if (_layer?.panel?.focusController.focusedElement is VisualElement focused &&
                IsDescendantOf(focused, _layer))
                focused.Blur();

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
                eventSystem.SetSelectedGameObject(null);
        }

        private static bool IsDescendantOf(VisualElement? target, VisualElement ancestor)
        {
            for (var current = target; current != null; current = current.parent)
                if (current == ancestor)
                    return true;
            return false;
        }

        internal void AttachRegistration(DebugWindowRegistration registration)
        {
            var toolRegistered = false;
            try
            {
                if (registration.RootTool != null && !_activeEvalToolRegistrations.Contains(registration))
                {
                    EvalToolRegistry.RegisterRoot(registration.RootTool);
                    _activeEvalToolRegistrations.Add(registration);
                    toolRegistered = true;
                }

                if (_layer == null || !renderDebugWindows) return;

                if (registration.VisualElement == null)
                {
                    registration.BuildVisualElement(allowWindowDragging);
                    if (registration.VisualElement != null)
                        _layer.Add(registration.VisualElement);
                }
            }
            catch
            {
                registration.DisposeVisualElement();
                if (toolRegistered)
                {
                    _activeEvalToolRegistrations.Remove(registration);
                    UnregisterOwnedRoot(registration);
                }

                throw;
            }
        }

        internal void DetachRegistration(DebugWindowRegistration registration)
        {
            if (registration.RootTool != null && _activeEvalToolRegistrations.Remove(registration))
                UnregisterOwnedRoot(registration);

            if (registration.VisualElement != null)
                registration.VisualElement.RemoveFromHierarchy();
            registration.DisposeVisualElement();
        }

        internal void ClearEvalToolBindings()
        {
            foreach (var registration in _activeEvalToolRegistrations)
            {
                if (registration.RootTool != null)
                    UnregisterOwnedRoot(registration);
            }

            _activeEvalToolRegistrations.Clear();
        }

        private static void UnregisterOwnedRoot(DebugWindowRegistration registration)
        {
            var rootTool = registration.RootTool;
            if (rootTool == null) return;
            EvalToolRegistry.TryUnregisterRoot(rootTool);
        }
    }
}
