#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit.Tests
{
    public sealed class DebugWindowRegistryTests
    {
        private GameObject? _hostObject;
        private DebugWindowModule? _host;

        [TearDown]
        public void TearDown()
        {
            _host?.Shutdown();
            _host = null;
            if (_hostObject != null)
                UnityEngine.Object.DestroyImmediate(_hostObject);
            _hostObject = null;
        }

        [Test]
        public void RootCollisionRollsBackRegistrationAndPreservesExistingOwner()
        {
            var name = UniqueName();
            var existing = new TestTool(name);
            EvalToolRegistry.RegisterRoot(existing);
            CreateHeadlessHost();
            var countBefore = DebugWindowModule.RegisteredWindows.Count;

            try
            {
                var builder = new DebugEvalToolBuilder(name, "Colliding debug root.")
                    .AddReadOnly("Value", "Read a value.", () => 1);

                Assert.Throws<InvalidOperationException>(() =>
                    DebugWindowModule.RegisterWindow(builder, window => window.SetTitle("Collision")));
                Assert.That(DebugWindowModule.RegisteredWindows.Count, Is.EqualTo(countBefore));
                Assert.That(EvalToolRegistry.TryResolve(name, out var path, out var resolved), Is.True);
                Assert.That(path, Is.EqualTo(name));
                Assert.That(resolved, Is.SameAs(existing));
            }
            finally
            {
                EvalToolRegistry.UnregisterRoot(name);
            }
        }

        [Test]
        public void DisposingHandleDoesNotUnregisterReplacementWithSameName()
        {
            var name = UniqueName();
            CreateHeadlessHost();
            var builder = new DebugEvalToolBuilder(name, "Owned debug root.")
                .AddReadOnly("Value", "Read a value.", () => 1);
            var handle = DebugWindowModule.RegisterWindow(builder, window => window.SetTitle("Owned"));
            var replacement = new TestTool(name);

            try
            {
                Assert.That(EvalToolRegistry.UnregisterRoot(name), Is.True);
                EvalToolRegistry.RegisterRoot(replacement);

                handle.Dispose();

                Assert.That(EvalToolRegistry.TryResolve(name, out _, out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(replacement));
            }
            finally
            {
                handle.Dispose();
                EvalToolRegistry.UnregisterRoot(name);
            }
        }

        [Test]
        public void SecondActiveHostIsRejected()
        {
            CreateHeadlessHost();
            var duplicateObject = new GameObject("UnityDebugTool Duplicate Registry Test Host");
            var duplicate = duplicateObject.AddComponent<DebugWindowModule>();
            typeof(DebugWindowModule)
                .GetField("renderDebugWindows", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(duplicate, false);

            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    duplicate.Initialize(new DebugPanelContext(new VisualElement())));
            }
            finally
            {
                duplicate.Shutdown();
                UnityEngine.Object.DestroyImmediate(duplicateObject);
            }
        }

        private void CreateHeadlessHost()
        {
            _hostObject = new GameObject("UnityDebugTool Registry Test Host");
            _host = _hostObject.AddComponent<DebugWindowModule>();
            typeof(DebugWindowModule)
                .GetField("renderDebugWindows", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_host, false);
            _host.Initialize(new DebugPanelContext(new VisualElement()));
        }

        private static string UniqueName() => "DebugTest_" + Guid.NewGuid().ToString("N");

        private sealed class TestTool : IEvalTool
        {
            public TestTool(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public string Description => "Test Tool.";
            public IReadOnlyList<EvalToolFunctionDescriptor> Functions => EvalToolFunctionDescriptor.Empty;
            public IReadOnlyList<IEvalTool> SubTools => Array.Empty<IEvalTool>();
        }
    }
}
