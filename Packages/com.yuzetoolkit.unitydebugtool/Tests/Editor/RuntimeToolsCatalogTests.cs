#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace YuzeToolkit.Tests
{
    public sealed class RuntimeToolsCatalogTests
    {
        [Test]
        public void CompleteCatalogContainsNestedLeafTools()
        {
            var rootName = "CatalogTest_" + Guid.NewGuid().ToString("N");
            EvalToolRegistry.RegisterRoot(new TestTool(rootName,
                new TestTool("Group", new TestTool("Leaf"))));

            try
            {
                var paths = RuntimeToolsTab.ReadCompleteCatalog(false).Select(tool => tool.Path).ToArray();
                Assert.That(paths, Does.Contain(rootName));
                Assert.That(paths, Does.Contain(rootName + "/Group"));
                Assert.That(paths, Does.Contain(rootName + "/Group/Leaf"));
            }
            finally
            {
                EvalToolRegistry.UnregisterRoot(rootName);
            }
        }

        [Test]
        public void UnknownSafetyFlagMakesCatalogEntryInvalid()
        {
            var function = new Dictionary<string, object?>
            {
                ["safety"] = new Dictionary<string, object?>
                {
                    ["flags"] = new List<object?> { "FutureDanger" }
                }
            };

            Assert.Throws<InvalidOperationException>(() => RuntimeToolsTab.ParseSafety(function));
        }

        private sealed class TestTool : IEvalTool
        {
            public TestTool(string name, params IEvalTool[] subTools)
            {
                Name = name;
                SubTools = subTools;
            }

            public string Name { get; }
            public string Description => "Catalog test Tool.";
            public IReadOnlyList<EvalToolFunctionDescriptor> Functions => EvalToolFunctionDescriptor.Empty;
            public IReadOnlyList<IEvalTool> SubTools { get; }
        }
    }
}
