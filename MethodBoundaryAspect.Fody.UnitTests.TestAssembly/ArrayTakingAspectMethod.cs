using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    public class ArrayTakingAspectMethod
    {
        [ArrayTakingAspect(0, 42)]
        public static int[] Method()
        {
            return new int[0];
        }
    }
}
