#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace YuzeToolkit
{
    internal sealed class RuntimeLogStore : IDisposable
    {
        private readonly ConcurrentQueue<DebugLogEntry> _pendingEntries = new();
        private readonly List<DebugLogEntry> _entries = new();
        private bool _subscribed;

        public int MaxEntries { get; set; } = 500;

        public IReadOnlyList<DebugLogEntry> Entries => _entries;

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
        }

        public void AddInternal(
            string message,
            string stackTrace,
            DebugLogKind kind,
            LogType type = LogType.Log)
        {
            _pendingEntries.Enqueue(new DebugLogEntry(DateTime.Now, message, stackTrace, type, kind));
        }

        public bool Pump()
        {
            var changed = false;
            while (_pendingEntries.TryDequeue(out var entry))
            {
                _entries.Add(entry);
                changed = true;
            }

            while (_entries.Count > Mathf.Max(1, MaxEntries))
            {
                _entries.RemoveAt(0);
                changed = true;
            }

            return changed;
        }

        public void Clear()
        {
            _entries.Clear();
            while (_pendingEntries.TryDequeue(out _))
            {
            }
        }

        public void Dispose()
        {
            if (!_subscribed) return;
            _subscribed = false;
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
        }

        private void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            _pendingEntries.Enqueue(new DebugLogEntry(
                DateTime.Now,
                message ?? string.Empty,
                stackTrace ?? string.Empty,
                type,
                DebugLogKind.Unity));
        }
    }
}
