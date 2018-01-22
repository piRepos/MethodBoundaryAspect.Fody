using MethodBoundaryAspect.Fody.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    public delegate void InterceptorCalled(string name, MethodExecutionArgs arg);

    public class RecordEverythingAspect: OnMethodBoundaryAspect
    {
        public static event InterceptorCalled Event;

        public override void OnEntry(MethodExecutionArgs arg)
        {
            Event?.DynamicInvoke(nameof(OnEntry), arg);
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            Event?.DynamicInvoke(nameof(OnExit), arg);
        }

        public override void OnException(MethodExecutionArgs arg)
        {
            Event?.DynamicInvoke(nameof(OnException), arg);
        }
    }
}
