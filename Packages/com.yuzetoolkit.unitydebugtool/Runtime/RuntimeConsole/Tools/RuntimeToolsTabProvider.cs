#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class RuntimeToolsTabProvider : MonoBehaviour, IRuntimeConsoleTabProvider
    {
        [SerializeField, Tooltip("Required USS used by the Runtime Console Tools tab. Initialization fails when it is missing.")]
        private StyleSheet? styleSheet;

        public IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            context.AddStyleSheet(styleSheet ?? throw new MissingReferenceException(
                $"{nameof(RuntimeToolsTabProvider)} requires a {nameof(StyleSheet)} reference."));
            yield return new RuntimeToolsTab();
        }
    }
}
