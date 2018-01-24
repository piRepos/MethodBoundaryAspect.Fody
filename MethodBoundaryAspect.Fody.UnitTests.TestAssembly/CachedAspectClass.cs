using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;
using System;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    [CachedAspect]
    public sealed class CachedAspectClass
    {
        public int MethodA() => 0;

        public int MethodB() => 0;
    }
}
