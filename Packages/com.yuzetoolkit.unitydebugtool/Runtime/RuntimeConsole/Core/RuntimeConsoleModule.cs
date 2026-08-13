#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    public sealed class RuntimeConsoleModule : MonoBehaviour, IDebugPanelModule
    {
        [SerializeField, Tooltip("Core USS used by the registered runtime console tabs.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Keyboard key used with the DebugPanel modifiers to show or hide the runtime console module.")]
        private Key toggleKey = Key.F8;

        private readonly List<IRuntimeConsoleTab> _tabs = new();
        private RuntimeConsoleView? _view;
        private VisualElement? _layer;

        public int SortOrder => 20;

        public Key ToggleKey => toggleKey;

        public void Initialize(DebugPanelContext context)
        {
            _tabs.Clear();
            var consoleContext = new RuntimeConsoleContext(context, this);
            foreach (var provider in GetComponents<MonoBehaviour>().OfType<IRuntimeConsoleTabProvider>())
            {
                foreach (var tab in provider.CreateTabs(consoleContext))
                {
                    if (tab != null)
                        _tabs.Add(tab);
                }
            }

            var duplicateId = _tabs.GroupBy(tab => tab.Id).FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateId))
            {
                Debug.LogError($"{nameof(RuntimeConsoleModule)} has duplicate tab id '{duplicateId}'.", this);
                ShutdownTabs();
                return;
            }

            _tabs.Sort((left, right) =>
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0 ? order : string.CompareOrdinal(left.Title, right.Title);
            });

            if (_tabs.Count == 0)
                return;

            if (styleSheet == null)
            {
                Debug.LogError($"{nameof(RuntimeConsoleModule)} requires a core USS reference when tabs are registered.", this);
                ShutdownTabs();
                return;
            }

            context.AddStyleSheet(styleSheet);
            _layer = context.CreateLayer("unity-debug-tool-console-layer");
            RuntimeConsoleUss.ApplyLayer(_layer);
            _view = new RuntimeConsoleView(_tabs);
            _view.AttachTo(_layer);
        }

        public void SetVisible(bool visible)
        {
            if (_layer != null)
                _layer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            _view?.SetVisible(visible);
        }

        public void Tick()
        {
            _view?.Tick();
        }

        public void Shutdown()
        {
            _view?.Detach();
            _view = null;
            _layer?.RemoveFromHierarchy();
            _layer = null;
            ShutdownTabs();
        }

        private void ShutdownTabs()
        {
            for (var i = _tabs.Count - 1; i >= 0; i--)
                _tabs[i].Shutdown();
            _tabs.Clear();
        }
    }
}
