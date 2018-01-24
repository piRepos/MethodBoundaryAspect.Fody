using MethodBoundaryAspect.Attributes;
using MethodBoundaryAspect.Fody.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    [AspectCaching(Caching.StaticByMethod)]
    public sealed class CachedAspect : OnMethodBoundaryAspect
    {
        public override bool CompileTimeValidate(MethodBase method)
        {
            return ((MethodInfo)method).ReturnType == typeof(int);
        }

        int m_enterCalled;

        public override void OnEntry(MethodExecutionArgs arg)
        {
            ++m_enterCalled;
        }

        public override void OnExit(MethodExecutionArgs arg)
        {
            arg.ReturnValue = m_enterCalled;
        }
    }
}
