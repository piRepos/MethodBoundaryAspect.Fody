using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    public class ValidatableBaseClass
    {
        public virtual void DoVirtual() { }

        public virtual string VirtualStringProperty { get; set; }

        public void DoSealed() { }

        public virtual void XXXDoVirtual() { }

        public virtual string XXXVirtualStringProperty { get; set; }
    }

    [ValidatableAspect]
    public class ValidatableDerivedClass : ValidatableBaseClass
    {

    }
}
