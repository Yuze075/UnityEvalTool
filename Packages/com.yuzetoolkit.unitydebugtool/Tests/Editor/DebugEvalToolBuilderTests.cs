#nullable enable
using System;
using NUnit.Framework;

namespace YuzeToolkit.Tests
{
    public sealed class DebugEvalToolBuilderTests
    {
        [Test]
        public void DelegatesMustBeNonNull()
        {
            var builder = NewBuilder();

            Assert.Throws<ArgumentNullException>(() =>
                builder.AddReadOnly<int>("Read", "Read a value.", null!));
            Assert.Throws<ArgumentNullException>(() =>
                builder.AddWritable("Write", "Write a value.", () => 1, null!));
            Assert.Throws<ArgumentNullException>(() =>
                builder.AddButton("Run", "Run an action.", null!));
        }

        [Test]
        public void WritableAndButtonSafetyMustDescribeMutation()
        {
            var builder = NewBuilder();

            Assert.Throws<ArgumentException>(() => builder.AddWritable(
                "Write", "Write a value.", () => 1, _ => { }, EvalToolSafety.ReadOnly));
            Assert.Throws<ArgumentException>(() => builder.AddWritable(
                "ContradictoryWrite", "Write a value.", () => 1, _ => { },
                EvalToolSafety.ReadOnly | EvalToolSafety.MutatesScene));
            Assert.Throws<ArgumentException>(() => builder.AddButton(
                "Delete", "Delete a value.", () => { }, EvalToolSafety.Destructive));
            Assert.Throws<ArgumentException>(() => builder.AddButton(
                "ConfirmOnly", "Missing mutation effect.", () => { }, EvalToolSafety.RequiresConfirmation));

            Assert.DoesNotThrow(() => builder.AddButton(
                "ConfirmedDelete",
                "Delete a value after confirmation.",
                () => { },
                EvalToolSafety.Destructive | EvalToolSafety.RequiresConfirmation));
        }

        [Test]
        public void ChildNamesMustBeUniqueWithinAGroup()
        {
            var builder = NewBuilder()
                .AddReadOnly("Value", "Read a value.", () => 1);

            Assert.Throws<InvalidOperationException>(() =>
                builder.AddButton("Value", "Duplicate child.", () => { }, EvalToolSafety.MutatesScene));
        }

        private static DebugEvalToolBuilder NewBuilder() => new(
            "Test_" + Guid.NewGuid().ToString("N"),
            "Test debug Tool root.");
    }
}
