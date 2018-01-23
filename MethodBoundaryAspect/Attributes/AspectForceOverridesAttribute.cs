using System;

namespace MethodBoundaryAspect.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple =false, Inherited =false)]
    public sealed class AspectForceOverridesAttribute : Attribute
    {
    }
}
