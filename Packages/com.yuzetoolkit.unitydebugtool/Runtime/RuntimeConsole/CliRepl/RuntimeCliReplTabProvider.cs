#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class RuntimeCliReplTabProvider : MonoBehaviour, IRuntimeConsoleTabProvider
    {
        [SerializeField, Tooltip("Required USS used by the Runtime Console Command Line tab. Initialization fails when it is missing.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Maximum number of CLI REPL history rows kept in the tab.")]
        private int maxHistoryRows = 200;

        public IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            context.AddStyleSheet(styleSheet ?? throw new MissingReferenceException(
                $"{nameof(RuntimeCliReplTabProvider)} requires a {nameof(StyleSheet)} reference."));
            yield return new RuntimeCliReplTab(Mathf.Max(1, maxHistoryRows));
        }
    }
}
