#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class RuntimeLogTabProvider : MonoBehaviour, IRuntimeConsoleTabProvider
    {
        [SerializeField, Tooltip("USS used by the Runtime Console Log tab.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Maximum number of Unity log entries kept by the Runtime Console Log tab.")]
        private int maxLogEntries = 500;

        public IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            if (styleSheet != null)
                context.AddStyleSheet(styleSheet);
            yield return new RuntimeLogTab(Mathf.Max(1, maxLogEntries));
        }
    }
}
