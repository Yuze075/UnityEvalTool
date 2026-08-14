#nullable enable
using System;
using System.Collections.Generic;

namespace YuzeToolkit
{
    /// <summary>
    /// Builds an explicit Eval Tool tree for a registered debug window without coupling Tool discovery to visual layout.
    /// </summary>
    public sealed class DebugEvalToolBuilder
    {
        private readonly string _name;
        private readonly string _description;
        private readonly List<IEvalTool> _subTools = new();
        private readonly bool _isRoot;
        private bool _isBuilt;

        public DebugEvalToolBuilder(string name, string description)
            : this(name, description, true)
        {
        }

        private DebugEvalToolBuilder(string name, string description, bool isRoot)
        {
            DebugToolUtility.ValidateRequiredToolMetadata(name, description);
            _name = name;
            _description = description;
            _isRoot = isRoot;
        }

        /// <summary>
        /// Converts a display label or runtime identifier into a valid Eval Tool path segment.
        /// </summary>
        public static string ToToolName(string value) => DebugToolUtility.ToGeneratedToolName(value);

        public DebugEvalToolBuilder AddGroup(string name, string description,
            Action<DebugEvalToolBuilder> configure)
        {
            EnsureMutable();
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var childBuilder = new DebugEvalToolBuilder(name, description, false);
            EnsureUniqueName(name);
            configure(childBuilder);
            _subTools.Add(childBuilder.Build());
            return this;
        }

        public DebugEvalToolBuilder AddReadOnly<TValue>(string name, string description, Func<TValue> getter)
        {
            EnsureMutable();
            DebugToolUtility.ValidateRequiredToolMetadata(name, description);
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            EnsureUniqueName(name);
            _subTools.Add(new DebugReadOnlyFieldTool<TValue>(name, description, getter));
            return this;
        }

        public DebugEvalToolBuilder AddWritable<TValue>(string name, string description, Func<TValue> getter,
            Action<TValue> setter)
        {
            return AddWritable(name, description, getter, setter, EvalToolSafety.MutatesScene);
        }

        public DebugEvalToolBuilder AddWritable<TValue>(string name, string description, Func<TValue> getter,
            Action<TValue> setter, EvalToolSafety safety)
        {
            EnsureMutable();
            DebugToolUtility.ValidateRequiredToolMetadata(name, description);
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            if (setter == null) throw new ArgumentNullException(nameof(setter));
            ValidateMutationSafety(safety, nameof(safety));
            EnsureUniqueName(name);
            _subTools.Add(new DebugWritableFieldTool<TValue>(name, description, getter, setter, safety));
            return this;
        }

        public DebugEvalToolBuilder AddButton(string name, string description, Action action)
        {
            return AddButtonInternal(name, description, action, EvalToolSafety.MutatesScene);
        }

        public DebugEvalToolBuilder AddButton(string name, string description, Action action, EvalToolSafety safety)
        {
            return AddButtonInternal(name, description, action, safety);
        }

        public DebugEvalToolBuilder AddDestructiveButton(string name, string description, Action action)
        {
            return AddButtonInternal(name, description, action,
                EvalToolSafety.Destructive | EvalToolSafety.RequiresConfirmation);
        }

        private DebugEvalToolBuilder AddButtonInternal(string name, string description, Action action,
            EvalToolSafety safety)
        {
            EnsureMutable();
            DebugToolUtility.ValidateRequiredToolMetadata(name, description);
            if (action == null) throw new ArgumentNullException(nameof(action));
            ValidateMutationSafety(safety, nameof(safety));
            EnsureUniqueName(name);
            _subTools.Add(new DebugButtonTool(name, description, action, safety));
            return this;
        }

        internal IEvalTool Build()
        {
            EnsureMutable();
            _isBuilt = true;
            return _isRoot
                ? new DebugPanelEvalTool(_name, _description, _subTools.ToArray())
                : new DebugGroupEvalTool(_name, _description, _subTools.ToArray());
        }

        private void EnsureMutable()
        {
            if (_isBuilt)
                throw new InvalidOperationException($"Debug Eval Tool '{_name}' has already been built.");
        }

        private void EnsureUniqueName(string name)
        {
            for (var i = 0; i < _subTools.Count; i++)
                if (string.Equals(_subTools[i].Name, name, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Debug Eval Tool '{_name}' already contains a child named '{name}'.");
        }

        private static void ValidateMutationSafety(EvalToolSafety safety, string parameterName)
        {
            const EvalToolSafety mutationFlags =
                EvalToolSafety.MutatesScene |
                EvalToolSafety.MutatesProject |
                EvalToolSafety.Destructive |
                EvalToolSafety.TriggersReload |
                EvalToolSafety.ReflectionDangerous |
                EvalToolSafety.NetworkService |
                EvalToolSafety.LongRunning |
                EvalToolSafety.MutatesEditorState |
                EvalToolSafety.PersistsData;
            const EvalToolSafety knownFlags =
                EvalToolSafety.ReadOnly |
                mutationFlags |
                EvalToolSafety.RequiresConfirmation;

            if ((safety & ~knownFlags) != 0 ||
                (safety & EvalToolSafety.ReadOnly) != 0 ||
                (safety & mutationFlags) == 0)
                throw new ArgumentException(
                    "A writable debug field or button must declare a mutation safety flag.", parameterName);
            if ((safety & EvalToolSafety.Destructive) != 0 &&
                (safety & EvalToolSafety.RequiresConfirmation) == 0)
                throw new ArgumentException(
                    "Destructive debug actions must also require confirmation.", parameterName);
        }
    }
}
