using System;

namespace MethodBoundaryAspect.Attributes
{
    public enum Caching
    {
        None,
        StaticByMethod
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple =false, Inherited =false)]
    public sealed class AspectCachingAttribute : Attribute
    {
        public AspectCachingAttribute(Caching cachingType) { }
    }
}
