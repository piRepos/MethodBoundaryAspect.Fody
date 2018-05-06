using System;

namespace MethodBoundaryAspect.Fody
{
    [Flags]
    internal enum AspectMethods
    {
        None = 0,
        OnEntry = (1 << 0),
        OnExit = (1 << 1),
        OnException = (1 << 2),
        CompileTimeValidate = (1 << 3)
    }
}