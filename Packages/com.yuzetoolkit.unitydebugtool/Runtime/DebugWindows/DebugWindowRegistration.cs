#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal sealed class DebugWindowRegistration
    {
        private readonly DebugWindowNode _windowNode;
        private readonly List<IDebugValueBinding> _bindings = new();
        private bool _reportedRefreshError;

        private DebugWindowRegistration(DebugWindowNode windowNode, IEvalTool? rootTool)
        {
            _windowNode = windowNode;
            RootTool = rootTool;
        }

        public IEvalTool? RootTool { get; }

        public VisualElement? VisualElement { get; private set; }

        public static DebugWindowRegistration Create(DebugWindowBuilder builder, IEvalTool? explicitRootTool = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var rootTool = explicitRootTool ?? builder.WindowNode.CreateEvalTool();
            return new DebugWindowRegistration(builder.WindowNode, rootTool);
        }

        public void BuildVisualElement(bool allowDragging)
        {
            DisposeVisualElement();
            _bindings.Clear();
            VisualElement = DebugVisualFactory.CreateWindow(_windowNode, allowDragging, _bindings);
        }

        public void Refresh()
        {
            var hasError = false;
            foreach (var binding in _bindings)
            {
                try
                {
                    binding.Refresh();
                }
                catch (Exception exception)
                {
                    hasError = true;
                    if (!_reportedRefreshError)
                        Debug.LogException(exception);
                }
            }

            _reportedRefreshError = hasError;
        }

        public void DisposeVisualElement()
        {
            VisualElement?.RemoveFromHierarchy();
            VisualElement = null;
            _bindings.Clear();
        }
    }

    internal interface IDebugValueBinding
    {
        void Refresh();
    }
}
