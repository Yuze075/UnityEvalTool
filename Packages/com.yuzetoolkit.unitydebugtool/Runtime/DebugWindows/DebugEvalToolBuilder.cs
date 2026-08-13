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
            configure(childBuilder);
            _subTools.Add(childBuilder.Build());
            return this;
        }

        public DebugEvalToolBuilder AddReadOnly<TValue>(string name, string description, Func<TValue> getter)
        {
            EnsureMutable();
            DebugToolUtility.ValidateRequiredToolMetadata(name, description);
            _subTools.Add(new DebugReadOnlyFieldTool<TValue>(name, description, getter));
            return this;
        }

        public DebugEvalToolBuilder AddWritable<TValue>(string name, string description, Func<TValue> getter,
            Action<TValue> setter)
        {
            EnsureMutable();
            DebugToolUtility.ValidateRequiredToolMetadata(name, description);
            _subTools.Add(new DebugWritableFieldTool<TValue>(name, description, getter, setter));
            return this;
        }

        public DebugEvalToolBuilder AddButton(string name, string description, Action action)
        {
            return AddButtonInternal(name, description, action, EvalToolSafety.MutatesScene);
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
    }
}
