using MethodBoundaryAspect.Fody.Attributes;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    public class ArrayTakingAspect : OnMethodBoundaryAspect
    {
        int[] _values;

        public ArrayTakingAspect(params int[] values)
        {
            _values = values;
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            arg.ReturnValue = _values;
        }
    }
}
