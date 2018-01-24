using System;
using System.Linq;
using Mono.Cecil;

namespace MethodBoundaryAspect.Fody
{
    public class ReferenceFinder
    {
        public readonly ModuleDefinition Module;

        public ReferenceFinder(ModuleDefinition moduleDefinition)
        {
            Module = moduleDefinition;
        }

        public MethodReference GetMethodReference(Type declaringType, Func<MethodDefinition, bool> predicate)
        {
            return GetMethodReference(GetTypeReference(declaringType), predicate);
        }

        public MethodReference GetMethodReference(TypeReference typeReference, Func<MethodDefinition, bool> predicate)
        {
            var typeDefinition = typeReference.Resolve();

            MethodDefinition methodDefinition;
            do
            {
                methodDefinition = typeDefinition.Methods.FirstOrDefault(predicate);
                typeDefinition = typeDefinition.BaseType == null 
                    ? null 
                    : typeDefinition.BaseType.Resolve();
            } while (methodDefinition == null && typeDefinition != null);

            return Module.ImportReference(methodDefinition);
        }

        public TypeReference GetTypeReference(Type type)
        {
            return Module.ImportReference(type);
        }
    }
}