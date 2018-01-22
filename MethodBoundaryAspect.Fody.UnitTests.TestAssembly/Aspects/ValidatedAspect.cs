using MethodBoundaryAspect.Fody.Attributes;
using System.Reflection;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    public class ValidatedAspect : OnMethodBoundaryAspect
    {
        static bool Called = false;

        public static bool WasCalled()
        {
            return Called;
        }

        public override void OnEntry(MethodExecutionArgs arg)
        {
            Called = true;
        }

        public override bool CompileTimeValidate(MethodBase method)
        {
            if (method.IsSpecialName && method.Name.StartsWith("get_"))
                return true;
            return method.Name.Contains("X");
        }
    }
}
