using MethodBoundaryAspect.Fody.Attributes;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    public class OverwriteReturnValueAspect : OnMethodBoundaryAspect
    {
        object _value;

        public OverwriteReturnValueAspect(object value)
        {
            _value = value;
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            arg.ReturnValue = _value;
        }
    }
}
