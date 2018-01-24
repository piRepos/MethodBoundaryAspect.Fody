using FluentAssertions;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly;
using System;
using Xunit;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class CachedAspectTests : MethodBoundaryAspectTestBase
    {
        static readonly Type TestClassType = typeof(CachedAspectClass);

        public CachedAspectTests()
        {
            WeaveAssemblyClassAndLoad(TestClassType);
        }

        int MethodA(Guid o)
        {
            return (int)AssemblyLoader.InvokeMethod(o, "MethodA");
        }

        int MethodB(Guid o)
        {
            return (int)AssemblyLoader.InvokeMethod(o, "MethodB");
        }

        [Fact]
        public void IfCachedAspectIsCalledForDifferentMethodOnSameInstance_ThenDifferentAspectInstancesAreCalled()
        {
            // Act
            Guid instance = Guid.NewGuid();
            AssemblyLoader.NewInstanceOfClass(TestClassType.FullName, instance);
            int a = MethodA(instance);
            int b = MethodB(instance);

            // Assert
            b.Should().Be(1);
        }

        [Fact]
        public void IfCachedAspectIsCalledForSameMethodOnSameInstance_ThenSameAspectInstanceIsCalled()
        {
            // Act
            Guid instance = Guid.NewGuid();
            AssemblyLoader.NewInstanceOfClass(TestClassType.FullName, instance);
            int a = MethodA(instance);
            a = MethodA(instance);

            // Assert
            a.Should().Be(2);
        }

        [Fact]
        public void IfCachedAspectIsCalledForSameMethodOnDifferentInstances_ThenSameAspectInstanceIsCalled()
        {
            // Act
            Guid instance1 = Guid.NewGuid();
            Guid instance2 = Guid.NewGuid();
            AssemblyLoader.NewInstanceOfClass(TestClassType.FullName, instance1);
            AssemblyLoader.NewInstanceOfClass(TestClassType.FullName, instance2);
            int a = MethodA(instance1);
            a = MethodA(instance2);

            // Assert
            a.Should().Be(2);
        }
    }
}