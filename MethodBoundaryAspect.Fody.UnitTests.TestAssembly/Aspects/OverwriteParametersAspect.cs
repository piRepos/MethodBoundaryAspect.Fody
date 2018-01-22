using MethodBoundaryAspect.Fody.Attributes;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    public class OverwriteParametersAspect : OnMethodBoundaryAspect
    {
        object[] _args;

        public OverwriteParametersAspect(params object[] args)
        {
            _args = args;
        }

        public override void OnEntry(MethodExecutionArgs arg)
        {
            arg.Arguments = _args;
        }
    }
}
