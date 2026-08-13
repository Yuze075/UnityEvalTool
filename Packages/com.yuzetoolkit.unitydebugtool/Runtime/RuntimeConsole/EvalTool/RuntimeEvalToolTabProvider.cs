#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    [DisallowMultipleComponent]
    public sealed class RuntimeEvalToolTabProvider : MonoBehaviour, IRuntimeConsoleTabProvider
    {
        [SerializeField, Tooltip("Required USS used by the Runtime Console EvalTool tab. Initialization fails when it is missing.")]
        private StyleSheet? styleSheet;

        public IEnumerable<IRuntimeConsoleTab> CreateTabs(RuntimeConsoleContext context)
        {
            context.AddStyleSheet(styleSheet ?? throw new MissingReferenceException(
                $"{nameof(RuntimeEvalToolTabProvider)} requires a {nameof(StyleSheet)} reference."));
            yield return new RuntimeEvalToolTab();
        }
    }
}
