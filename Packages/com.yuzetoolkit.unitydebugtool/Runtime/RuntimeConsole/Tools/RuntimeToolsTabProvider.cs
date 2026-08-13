#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class RuntimeToolsTabProvider : MonoBehaviour, IRuntimeConsoleTabProvider
    {
        [SerializeField, Tooltip("USS used by the Runtime Console Tools tab.")]
        private StyleSheet? styleSheet;

        public IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            if (styleSheet != null)
                context.AddStyleSheet(styleSheet);
            yield return new RuntimeToolsTab();
        }
    }
}
