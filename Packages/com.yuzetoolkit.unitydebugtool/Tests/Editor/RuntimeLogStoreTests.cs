#nullable enable
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace YuzeToolkit.Tests
{
    public sealed class RuntimeLogStoreTests
    {
        [Test]
        public void HiddenCaptureQueueAndVisibleEntriesRemainBounded()
        {
            using var store = new RuntimeLogStore { MaxEntries = 8 };
            for (var index = 0; index < 1000; index++)
                store.AddInternal("entry " + index, string.Empty, DebugLogKind.Internal, LogType.Log);

            Assert.That(store.Pump(), Is.True);
            Assert.That(store.Entries.Count, Is.LessThanOrEqualTo(8));
            Assert.That(store.Entries.Any(entry => entry.Message.Contains("dropped")), Is.True);
            Assert.That(store.Pump(), Is.False);
        }
    }
}
