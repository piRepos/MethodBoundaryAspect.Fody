using FluentAssertions;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly;
using System;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class StateMachineTests : MethodBoundaryAspectTestBase
    {
        static readonly Type TestClassType = typeof(ClassWithStateMachines);

        [Fact]
        public void IfAsyncMethodDelays_ThenIncompleteTaskIsGiven()
        {
            // Arrange
            const string testMethodName = "AsyncMethod";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            var result = AssemblyLoader.InvokeMethod(TestClassType.FullName, "TestAsyncMethod");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void IfIEnumerableMethodUsesYield_ThenOnExitIsCalledBeforeIteration()
        {
            // Arrange
            const string testMethodName = "YieldMethod";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            var result = AssemblyLoader.InvokeMethod(TestClassType.FullName, "TestYieldMethod");

            // Assert
            result.Should().BeNull();
        }
    }
}
