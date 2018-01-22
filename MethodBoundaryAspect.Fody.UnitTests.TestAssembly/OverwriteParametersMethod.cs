using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    public class OverwriteParametersMethod
    {
        [OverwriteParametersAspect(3, "test")]
        public static object[] MethodTakingParameters(int i, string name)
        {
            return new object[] { i, name };
        }

        [OverwriteParametersAspect("not an integer")]
        public static void MethodTakingInt(int i)
        {

        }

        [OverwriteParametersAspect("short")]
        public static void MethodTakingString(string s, string t)
        {

        }
    }
}
