#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class RuntimeCliReplTabProvider : MonoBehaviour, IRuntimeConsoleTabProvider
    {
        [SerializeField, Tooltip("USS used by the Runtime Console CLI REPL tab.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Maximum number of CLI REPL history rows kept in the tab.")]
        private int maxHistoryRows = 200;

        public IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            if (styleSheet != null)
                context.AddStyleSheet(styleSheet);
            yield return new RuntimeCliReplTab(Mathf.Max(1, maxHistoryRows));
        }
    }
}
