using FluentAssertions;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class OverwriteReturnValueTests : MethodBoundaryAspectTestBase
    {
        private static readonly Type TestClassType = typeof(OverwriteReturnValueMethod);

        [Fact]
        public void IfReturnValueIsOverwritten_ThenNewValueIsReturned()
        {
            // Arrange
            const string testMethodName = "MethodReturningString";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            var result = AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);

            // Assert
            result.Should().Be("overridden");
        }

        [Fact]
        public void IfValueTypeReturnValueIsOverwritten_ThenNewValueIsReturned()
        {
            // Arrange
            const string testMethodName = "MethodReturningValueType";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            var result = AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);

            // Assert
            result.Should().Be(42);
        }

        [Fact]
        public void IfEnumReturnValueIsOverwritten_ThenNewValueIsReturned()
        {
            // Arrange
            const string testMethodName = "MethodReturningEnum";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            var result = AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);

            // Assert
            result.Should().Be(OverwriteReturnValueMethod.Values.Changed);
        }

        [Fact]
        public void IfReturnValueIsWrongType_ThenInvalidCastIsThrown()
        {
            // Arrange
            const string testMethodName = "MethodReturningInt";
            WeaveAssemblyMethodAndLoad(TestClassType, testMethodName);

            // Act
            Action invoke = () =>
                AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);

            // Assert
            var exceptions = invoke.ShouldThrow<TargetInvocationException>().Subject;
            var exception = exceptions.Single();
            exception.InnerException.Should().BeOfType<InvalidCastException>();
        }
    }
}
