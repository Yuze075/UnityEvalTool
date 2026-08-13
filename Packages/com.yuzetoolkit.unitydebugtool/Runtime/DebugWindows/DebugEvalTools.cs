#nullable enable
using System;
using System.Collections.Generic;

namespace YuzeToolkit
{
    internal sealed class DebugPanelEvalTool : IEvalTool
    {
        public DebugPanelEvalTool(string name, string description, IReadOnlyList<IEvalTool> subTools)
        {
            Name = name;
            Description = description +
                          " Common usage: import a field tool directly with tools://DebugRoot/FieldName, call get() to read it, set(value) to change writable fields, and invoke() to press buttons.";
            SubTools = subTools;
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<EvalToolFunctionDescriptor> Functions => EvalToolFunctionDescriptor.Empty;

        public IReadOnlyList<IEvalTool> SubTools { get; }
    }

    internal sealed class DebugGroupEvalTool : IEvalTool
    {
        public DebugGroupEvalTool(string name, string description, IReadOnlyList<IEvalTool> subTools)
        {
            Name = name;
            Description = description;
            SubTools = subTools;
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<EvalToolFunctionDescriptor> Functions => EvalToolFunctionDescriptor.Empty;

        public IReadOnlyList<IEvalTool> SubTools { get; }
    }

    internal sealed class DebugReadOnlyFieldTool<TValue> : IEvalTool
    {
        private readonly Func<TValue> _getter;

        public DebugReadOnlyFieldTool(string name, string description, Func<TValue> getter)
        {
            Name = name;
            Description = description;
            _getter = getter;
            Functions = new[]
            {
                new EvalToolFunctionDescriptor(
                    "get",
                    "Return the current debug field value.",
                    null,
                    EvalToolSafety.ReadOnly)
            };
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<EvalToolFunctionDescriptor> Functions { get; }

        public IReadOnlyList<IEvalTool> SubTools => Array.Empty<IEvalTool>();

        public TValue get() => _getter();
    }

    internal sealed class DebugWritableFieldTool<TValue> : IEvalTool
    {
        private readonly Func<TValue> _getter;
        private readonly Action<TValue> _setter;

        public DebugWritableFieldTool(string name, string description, Func<TValue> getter, Action<TValue> setter)
        {
            Name = name;
            Description = description;
            _getter = getter;
            _setter = setter;
            Functions = new[]
            {
                new EvalToolFunctionDescriptor(
                    "get",
                    "Return the current debug field value.",
                    null,
                    EvalToolSafety.ReadOnly),
                new EvalToolFunctionDescriptor(
                    "set",
                    "Set the debug field value and return the updated value.",
                    new[]
                    {
                        new EvalToolParameterDescriptor(
                            "value",
                            DebugToolUtility.GetToolTypeName(typeof(TValue)),
                            false,
                            null,
                            "New debug field value.")
                    },
                    EvalToolSafety.MutatesScene)
            };
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<EvalToolFunctionDescriptor> Functions { get; }

        public IReadOnlyList<IEvalTool> SubTools => Array.Empty<IEvalTool>();

        public TValue get() => _getter();

        public TValue set(TValue value)
        {
            _setter(value);
            return _getter();
        }
    }

    internal sealed class DebugButtonTool : IEvalTool
    {
        private readonly Action _action;

        public DebugButtonTool(string name, string description, Action action,
            EvalToolSafety safety = EvalToolSafety.MutatesScene)
        {
            Name = name;
            Description = description;
            _action = action;
            Functions = new[]
            {
                new EvalToolFunctionDescriptor(
                    "invoke",
                    "Invoke this debug button action.",
                    null,
                    safety)
            };
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<EvalToolFunctionDescriptor> Functions { get; }

        public IReadOnlyList<IEvalTool> SubTools => Array.Empty<IEvalTool>();

        public string invoke()
        {
            _action();
            return "invoked";
        }
    }
}
