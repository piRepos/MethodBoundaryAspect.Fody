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
            if (_intercept == true || InterceptProperty == true || InterceptField == true)
                return true;
            if (method.IsSpecialName && method.Name.StartsWith("get_"))
                return true;
            return method.Name.Contains("X");
        }

        public ValidatedAspect() { }

        bool? _intercept;

        public ValidatedAspect(bool intercept)
        {
            _intercept = intercept;
        }

        public bool InterceptProperty { get; set; }

        public bool InterceptField;

        public ValidatedAspect(params object[] args)
        {
            _intercept = args.Length % 2 == 0;
        }
    }
}
