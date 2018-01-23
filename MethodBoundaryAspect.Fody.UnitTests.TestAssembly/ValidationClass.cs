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

    public class ValidationClass2
    {
        [ValidatedAspect(true)]
        public void Method() { }

        [ValidatedAspect(InterceptProperty =true)]
        public void Method2() { }

        [ValidatedAspect(InterceptField =true)]
        public void Method3() { }

        [ValidatedAspect(typeof(string), typeof(int))]
        public void Method4() { }

        [ValidatedAspect(typeof(float), null, 42)]
        public void Method5() { }
    }
}
