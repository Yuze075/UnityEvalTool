#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace YuzeToolkit
{
    internal static class EvalToolSafetyUtility
    {
        public static EvalToolFunctionDescriptor Apply(string toolPath, EvalToolFunctionDescriptor function)
        {
            // Safety is intentionally declared at the tool function site.
            // This utility no longer guesses semantics from path, method name, or description text.
            return function;
        }

        public static Dictionary<string, object?> ToJson(EvalToolSafety safety)
        {
            return EvalData.Obj(
                ("flags", GetFlagNames(safety).Cast<object?>().ToList()),
                ("riskLevel", GetRiskLevel(safety)),
                ("readOnly", Has(safety, EvalToolSafety.ReadOnly)),
                ("mutatesScene", Has(safety, EvalToolSafety.MutatesScene)),
                ("mutatesProject", Has(safety, EvalToolSafety.MutatesProject)),
                ("destructive", Has(safety, EvalToolSafety.Destructive)),
                ("requiresConfirmation", Has(safety, EvalToolSafety.RequiresConfirmation)),
                ("triggersReload", Has(safety, EvalToolSafety.TriggersReload)),
                ("reflectionDangerous", Has(safety, EvalToolSafety.ReflectionDangerous)),
                ("networkService", Has(safety, EvalToolSafety.NetworkService)),
                ("longRunning", Has(safety, EvalToolSafety.LongRunning)),
                ("mutatesEditorState", Has(safety, EvalToolSafety.MutatesEditorState)),
                ("persistsData", Has(safety, EvalToolSafety.PersistsData))
            );
        }

        public static string GetRiskLevel(EvalToolSafety safety)
        {
            if (Has(safety, EvalToolSafety.Destructive) ||
                Has(safety, EvalToolSafety.ReflectionDangerous))
                return "dangerous";
            if (Has(safety, EvalToolSafety.MutatesProject) ||
                Has(safety, EvalToolSafety.TriggersReload) ||
                Has(safety, EvalToolSafety.NetworkService) ||
                Has(safety, EvalToolSafety.LongRunning) ||
                Has(safety, EvalToolSafety.PersistsData))
                return "high";
            if (Has(safety, EvalToolSafety.MutatesScene) ||
                Has(safety, EvalToolSafety.MutatesEditorState))
                return "medium";
            return "low";
        }

        private static bool Has(EvalToolSafety safety, EvalToolSafety flag) => (safety & flag) != 0;

        private static IEnumerable<string> GetFlagNames(EvalToolSafety safety)
        {
            foreach (EvalToolSafety flag in Enum.GetValues(typeof(EvalToolSafety)))
            {
                if (flag == EvalToolSafety.Unspecified) continue;
                if (Has(safety, flag)) yield return flag.ToString();
            }
        }

    }
}
