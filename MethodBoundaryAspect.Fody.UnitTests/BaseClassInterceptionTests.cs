using MethodBoundaryAspect.Fody.UnitTests.TestAssembly;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;
using System;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class BaseClassInterceptionTests : MethodBoundaryAspectTestBase
    {
        static readonly Type TestClassType = typeof(ValidatableDerivedClass);

        bool WasCalled()
        {
            return (bool)AssemblyLoader.InvokeMethod(typeof(ValidatableAspect).FullName, nameof(ValidatableAspect.WasCalled));
        }

        void Weave()
        {
            WeaveAssemblyClassAndLoad(TestClassType);
        }
        
        [Fact]
        public void IfVirtualMethodIsNotOverriddenInDerivedClassAndClassAspectAllowsOverriding_ThenOverrideIsAddedAndIntercepted()
        {
            // Arrange
            const string testMethodName = "DoVirtual";
            Weave();

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void IfVirtualPropertyIsNotOverriddenInDerivedClassAndClassAspectAllowsOverriding_ThenOverrideIsAddedAndIntercepted()
        {
            // Arrange
            const string testMethodName = "get_VirtualStringProperty";
            Weave();

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void IfVirtualMethodIsNotOverriddenInDerivedClassAndClassAspectAllowsOverridingButCompiletimeValidateReturnsFalse_ThenNoOverrideIsAdded()
        {
            // Arrange
            const string testMethodName = "XXXDoVirtual";
            Weave();

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void IfVirtualPropertyIsNotOverriddenInDerivedClassAndClassAspectAllowsOverridingButCompiletimeValidateReturnsFalse_ThenNoOverrideIsAdded()
        {
            // Arrange
            const string testMethodName = "get_XXXVirtualStringProperty";
            Weave();

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void IfSealedPropertyInBaseClassMatchesInterceptionMethod_ThenMethodIsStillNotIntercepted()
        {
            // Arrange
            const string testMethodName = "DoSealed";
            Weave();

            // Act
            AssemblyLoader.InvokeMethod(TestClassType.FullName, testMethodName);
            var result = WasCalled();

            // Assert
            result.Should().Be(false);
        }
    }
}
