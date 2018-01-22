using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    public class OverwriteReturnValueMethod
    {
        [OverwriteReturnValueAspect("overridden")]
        public static string MethodReturningString()
        {
            return "not overridden";
        }

        [OverwriteReturnValueAspect(42)]
        public static int MethodReturningValueType()
        {
            return -1;
        }

        public enum Values
        {
            Default,
            Changed
        }

        [OverwriteReturnValueAspect(Values.Changed)]
        public static Values MethodReturningEnum()
        {
            return Values.Default;
        }

        [OverwriteReturnValueAspect("throw")]
        public static int MethodReturningInt()
        {
            return 0;
        }
    }
}
