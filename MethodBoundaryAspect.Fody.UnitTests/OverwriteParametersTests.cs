using FluentAssertions;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class OverwriteParametersTests : MethodBoundaryAspectTestBase
    {
        static readonly Type TestClassType = typeof(OverwriteParametersMethod);

        [Fact]
        public void IfParametersAreOverwritten_ThenNewParametersAreUsed()
        {
            // Arrange
            const string testMethodName = "MethodTakingParameters";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            var result = AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName, 9, "wrong") as object[];

            // Assert
            result[0].Should().Be(3);
            result[1].Should().Be("test");
        }

        [Fact]
        public void IfOverwrittenParameterIsWrongType_ThenInvalidCastExceptionIsThrown()
        {
            // Arrange
            const string testMethodName = "MethodTakingInt";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            Action invoke = () =>
                AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName, 42);

            // Assert
            var exceptions = invoke.ShouldThrow<TargetInvocationException>().Subject;
            var exception = exceptions.Single();
            exception.InnerException.Should().BeOfType<InvalidCastException>();
        }

        [Fact]
        public void IfOverwrittenParamArrayIsShort_ThenIndexOutOfRangeExceptionIsThrown()
        {
            // Arrange
            const string testMethodName = "MethodTakingString";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            Action invoke = () =>
                AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName, "test", "test2");

            // Assert
            var exceptions = invoke.ShouldThrow<TargetInvocationException>().Subject;
            var exception = exceptions.Single();
            exception.InnerException.Should().BeOfType<IndexOutOfRangeException>();
        }
    }
}
