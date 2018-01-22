using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    [ValidatedAspect]
    public class ValidationClass
    {
        public string StringProperty { get; set; }

        public void Method() { }

        public void MethodWithRefParam(ref int i) { }

        public void MethodWithRefParamX(ref int i) { }

        public void MethodWithOutParam(out int i) { i = 0; }

        public void MethodWithOutParamX(out int i) { i = 0; }
    }
}
