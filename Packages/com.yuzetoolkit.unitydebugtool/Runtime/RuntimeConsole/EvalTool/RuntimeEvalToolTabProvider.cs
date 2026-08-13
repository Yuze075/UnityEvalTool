#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class RuntimeEvalToolTabProvider : MonoBehaviour, IRuntimeConsoleTabProvider
    {
        [SerializeField, Tooltip("USS used by the Runtime Console EvalTool tab.")]
        private StyleSheet? styleSheet;

        public IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            if (styleSheet != null)
                context.AddStyleSheet(styleSheet);
            yield return new RuntimeEvalToolTab();
        }
    }
}
