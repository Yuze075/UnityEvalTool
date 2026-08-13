#nullable enable
using System.Collections.Generic;
using NUnit.Framework;

namespace YuzeToolkit.Tests
{
    public sealed class EvalToolSafetyTests
    {
        [Test]
        public void JsDescriptorParsesPersistsDataAsHighRisk()
        {
            var descriptor = EvalToolRegistry.JsDescriptorFromJson(EvalData.Obj(
                ("name", "Save"),
                ("path", "Save"),
                ("description", "Save data."),
                ("functions", EvalData.Arr(EvalData.Obj(
                    ("name", "write"),
                    ("description", "Write data."),
                    ("safety", EvalData.Arr("PersistsData", "RequiresConfirmation")),
                    ("parameters", new List<object?>())
                )))
            ), "Save");

            var function = descriptor.Functions[0];
            Assert.That(function.Safety, Is.EqualTo(EvalToolSafety.PersistsData |
                                                    EvalToolSafety.RequiresConfirmation));
            Assert.That(function.RiskLevel, Is.EqualTo("high"));
            Assert.That(function.RequiresConfirmation, Is.True);
            var json = EvalToolSafetyUtility.ToJson(function.Safety);
            Assert.That(EvalData.GetBool(json, "persistsData"), Is.True);
        }

        [Test]
        public void JsDescriptorRejectsUnknownSafetyFlag()
        {
            var data = EvalData.Obj(
                ("name", "Bad"),
                ("path", "Bad"),
                ("description", "Invalid safety."),
                ("functions", EvalData.Arr(EvalData.Obj(
                    ("name", "run"),
                    ("description", "Run."),
                    ("parameters", new List<object?>()),
                    ("safety", EvalData.Arr("WritesEverything"))
                )))
            );

            Assert.Throws<System.InvalidOperationException>(() =>
                EvalToolRegistry.JsDescriptorFromJson(data, "Bad"));
        }

        [Test]
        public void JsDescriptorRejectsNumericSafetyFlagAliases()
        {
            var data = EvalData.Obj(
                ("name", "Bad"),
                ("path", "Bad"),
                ("description", "Numeric safety."),
                ("functions", EvalData.Arr(EvalData.Obj(
                    ("name", "run"),
                    ("description", "Run."),
                    ("parameters", new List<object?>()),
                    ("safety", EvalData.Arr("1024"))
                )))
            );

            Assert.Throws<System.InvalidOperationException>(() =>
                EvalToolRegistry.JsDescriptorFromJson(data, "Bad"));
        }

        [Test]
        public void JsDescriptorRejectsReservedExportFunctionName()
        {
            var data = EvalData.Obj(
                ("name", "Bad"),
                ("path", "Bad"),
                ("description", "Reserved export name."),
                ("functions", EvalData.Arr(EvalData.Obj(
                    ("name", "delete"),
                    ("description", "Invalid generated export."),
                    ("parameters", new List<object?>()),
                    ("safety", EvalData.Arr("ReadOnly"))
                )))
            );

            Assert.Throws<System.InvalidOperationException>(() =>
                EvalToolRegistry.JsDescriptorFromJson(data, "Bad"));
        }

        [Test]
        public void JsDescriptorRequiresExplicitSafetyMetadata()
        {
            var data = EvalData.Obj(
                ("name", "Bad"),
                ("path", "Bad"),
                ("description", "Missing safety."),
                ("functions", EvalData.Arr(EvalData.Obj(
                    ("name", "run"),
                    ("description", "Run."),
                    ("parameters", new List<object?>())
                )))
            );

            var exception = Assert.Throws<System.InvalidOperationException>(() =>
                EvalToolRegistry.JsDescriptorFromJson(data, "Bad"));
            StringAssert.Contains("safety array", exception!.Message);
        }

        [Test]
        public void JsDescriptorPreservesDirectChildSummaryForCatalogTraversal()
        {
            var descriptor = EvalToolRegistry.JsDescriptorFromJson(EvalData.Obj(
                ("name", "Debug"),
                ("path", "Debug"),
                ("description", "Debug tools."),
                ("functions", new List<object?>()),
                ("subTools", EvalData.Arr(EvalData.Obj(
                    ("name", "Button"),
                    ("path", "Debug/Button"),
                    ("description", "Button tools."),
                    ("functionCount", 1)
                )))
            ), "Debug");

            Assert.That(descriptor.SubTools, Has.Count.EqualTo(1));
            Assert.That(descriptor.SubTools[0].Name, Is.EqualTo("Button"));
            Assert.That(descriptor.SubTools[0].Path, Is.EqualTo("Debug/Button"));
            Assert.That(descriptor.SubTools[0].FunctionCount, Is.EqualTo(1));
        }

        [Test]
        public void OwnerAwareUnregisterOnlyRemovesExpectedRootInstance()
        {
            var root = new TestTool("OwnerAware" + System.Guid.NewGuid().ToString("N"));
            var stranger = new TestTool(root.Name);
            Assert.That(EvalToolRegistry.TryRegisterRoot(root), Is.True);
            try
            {
                Assert.That(EvalToolRegistry.TryUnregisterRoot(stranger), Is.False);
                Assert.That(EvalToolRegistry.TryResolve(root.Name, out _, out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(root));
                Assert.That(EvalToolRegistry.TryUnregisterRoot(root), Is.True);
                Assert.That(EvalToolRegistry.TryResolve(root.Name, out _, out _), Is.False);
            }
            finally
            {
                EvalToolRegistry.TryUnregisterRoot(root);
            }
        }

        private sealed class TestTool : IEvalTool
        {
            public TestTool(string name) => Name = name;
            public string Name { get; }
            public string Description => "Test tool.";
            public IReadOnlyList<EvalToolFunctionDescriptor> Functions => EvalToolFunctionDescriptor.Empty;
            public IReadOnlyList<IEvalTool> SubTools => System.Array.Empty<IEvalTool>();
        }
    }
}
