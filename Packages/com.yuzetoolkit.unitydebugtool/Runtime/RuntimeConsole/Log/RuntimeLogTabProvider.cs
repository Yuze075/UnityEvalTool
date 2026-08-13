#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class RuntimeLogTabProvider : MonoBehaviour, IRuntimeConsoleTabProvider
    {
        [SerializeField, Tooltip("Required USS used by the Runtime Console Log tab. Initialization fails when it is missing.")]
        private StyleSheet? styleSheet;

        [SerializeField, Tooltip("Maximum number of Unity log entries kept by the Runtime Console Log tab.")]
        private int maxLogEntries = 500;

        public IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            context.AddStyleSheet(styleSheet ?? throw new MissingReferenceException(
                $"{nameof(RuntimeLogTabProvider)} requires a {nameof(StyleSheet)} reference."));
            yield return new RuntimeLogTab(Mathf.Max(1, maxLogEntries));
        }
    }
}
