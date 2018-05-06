using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MethodBoundaryAspect.Fody.Attributes;
using MethodBoundaryAspect.Fody.Ordering;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Collections.Generic;

namespace MethodBoundaryAspect.Fody
{
    /// <summary>
    /// Used tools: 
    /// - .NET Reflector + Addins "Reflexil" / IL Spy / LinqPad
    /// - PEVerify
    /// - ILDasm
    /// - Mono.Cecil
    /// - Fody
    /// 
    /// TODO:
    /// Fix pdb files -> fixed
    /// Intetgrate with Fody -> ok
    /// Support for class aspects -> ok
    /// Support for assembly aspects -> ok
    /// Implement CompileTimeValidate
    /// Optimize weaving: Dont generate code of "OnXXX()" method is empty or not used -> ok
    /// Optimize weaving: remove runtime dependency on "MethodBoundaryAspect.Attributes" assembly
    /// Optimize weaving: only put arguments in MethodExecutionArgs if they are accessed in "OnXXX()" method
    /// </summary>
    public class ModuleWeaver
    {
        public readonly List<string> AdditionalAssemblyResolveFolders = new List<string>();

        private readonly List<string> _classFilters = new List<string>();
        private readonly List<string> _methodFilters = new List<string>();
        private readonly List<string> _propertyFilter = new List<string>();

        public ModuleWeaver()
        {
            InitLogging();
        }

        public ModuleDefinition ModuleDefinition { get; set; }

        public Action<string> LogDebug { get; set; }
        public Action<string> LogInfo { get; set; }
        public Action<string> LogWarning { get; set; }
        public Action<string, SequencePoint> LogWarningPoint { get; set; }
        public Action<string> LogError { get; set; }
        public Action<string, SequencePoint> LogErrorPoint { get; set; }

        public int TotalWeavedTypes { get; private set; }
        public int TotalWeavedMethods { get; private set; }
        public int TotalWeavedPropertyMethods { get; private set; }
        public byte[] UnweavedAssembly { get; private set; }

        public MethodDefinition LastWeavedMethod { get; private set; }

        public void Execute()
        {
            UnweavedAssembly = File.ReadAllBytes(ModuleDefinition.FileName);
            Execute(ModuleDefinition);
        }

        public string CreateShadowAssemblyPath(string assemblyPath)
        {
            var fileInfoSource = new FileInfo(assemblyPath);
            return
                fileInfoSource.DirectoryName
                + Path.DirectorySeparatorChar
                + Path.GetFileNameWithoutExtension(fileInfoSource.Name)
                + "_Weaved_"
                + fileInfoSource.Extension.ToLower();
        }

        public string WeaveToShadowFile(string assemblyPath)
        {
            var shadowAssemblyPath = CreateShadowAssemblyPath(assemblyPath);
            File.Copy(assemblyPath, shadowAssemblyPath, true);

            var pdbPath = Path.ChangeExtension(assemblyPath, "pdb");
            var shadowPdbPath = CreateShadowAssemblyPath(pdbPath);

            if (File.Exists(pdbPath))
                File.Copy(pdbPath, shadowPdbPath, true);

            Weave(shadowAssemblyPath);
            return shadowAssemblyPath;
        }

        public void Weave(string assemblyPath)
        {
            var readerParameters = new ReaderParameters
            {
                ReadSymbols = true,
                SymbolReaderProvider = new PdbReaderProvider(),
				ReadWrite = true,
            };

            if (AdditionalAssemblyResolveFolders.Any())
                readerParameters.AssemblyResolver = new FolderAssemblyResolver(AdditionalAssemblyResolveFolders);

            UnweavedAssembly = File.ReadAllBytes(assemblyPath);
			using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters))
			{
				var module = assemblyDefinition.MainModule;
				Execute(module);

				var writerParameters = new WriterParameters
				{
					WriteSymbols = true,
					SymbolWriterProvider = new PdbWriterProvider()
				};
				assemblyDefinition.Write(writerParameters);
			}
        }

        public void AddClassFilter(string classFilter)
        {
            _classFilters.Add(classFilter);
        }

        public void AddMethodFilter(string methodFilter)
        {
            _methodFilters.Add(methodFilter);
        }

        public void AddPropertyFilter(string propertyFilter)
        {
            _propertyFilter.Add(propertyFilter);
        }
        public void AddAdditionalAssemblyResolveFolder(string folderName)
        {
            AdditionalAssemblyResolveFolders.Add(folderName);
        }

        private void InitLogging()
        {
            LogDebug = m => Debug.WriteLine(m);
            LogInfo = LogDebug;
            LogWarning = LogDebug;
            LogWarningPoint = (m, p) => { };
            LogError = LogDebug;
            LogErrorPoint = (m, p) => { };
        }

        private void Execute(ModuleDefinition module)
        {
            var assemblyMethodBoundaryAspects = module.Assembly.CustomAttributes;

            foreach (var type in module.Types)
                WeaveType(module, type, assemblyMethodBoundaryAspects);
        }

        private void WeaveType(ModuleDefinition module, TypeDefinition type, Collection<CustomAttribute> assemblyMethodBoundaryAspects)
        {
            var classMethodBoundaryAspects = type.CustomAttributes;

            var propertyGetters = type.Properties
                .Where(x => x.GetMethod != null)
                .ToDictionary(x => x.GetMethod);

            var propertySetters = type.Properties
                .Where(x => x.SetMethod != null)
                .ToDictionary(x => x.SetMethod);

            var weavedAtLeastOneMethod = false;
            foreach (var method in type.Methods.ToList())
            {
                if (!IsWeavableMethod(method, type))
                    continue;

                Collection<CustomAttribute> methodMethodBoundaryAspects;

                if (method.IsGetter)
                    methodMethodBoundaryAspects = propertyGetters[method].CustomAttributes;
                else if (method.IsSetter)
                    methodMethodBoundaryAspects = propertySetters[method].CustomAttributes;
                else
                    methodMethodBoundaryAspects = method.CustomAttributes;

                var aspectInfos = assemblyMethodBoundaryAspects
                    .Concat(classMethodBoundaryAspects)
                    .Concat(methodMethodBoundaryAspects)
                    .Where(IsMethodBoundaryAspect)
                    .Select(x => new AspectInfo(x))
                    .ToList();
                if (aspectInfos.Count == 0)
                    continue;

                weavedAtLeastOneMethod |= WeaveMethod(
                    module,
                    method,
                    aspectInfos,
                    type);
            }

            var classLevelAspectInfos = assemblyMethodBoundaryAspects
                .Concat(classMethodBoundaryAspects)
                .Where(IsMethodBoundaryAspect)
                .Select(x => new AspectInfo(x))
                .Where(x => x.ForceOverrides)
                .ToList();

            if (classLevelAspectInfos.Count != 0)
                foreach (var baseVirtualMethod in GetPotentiallyOverridableMethods(type))
                {
                    if (!IsWeavableMethod(baseVirtualMethod, type))
                        continue;

                    var @override = new MethodDefinition(baseVirtualMethod.Name, baseVirtualMethod.Attributes, baseVirtualMethod.ReturnType);

                    foreach (var p in baseVirtualMethod.GenericParameters)
                        @override.GenericParameters.Add(new GenericParameter(p));

                    foreach (var p in baseVirtualMethod.Parameters)
                        @override.Parameters.Add(new ParameterDefinition(p.ParameterType));

                    var il = @override.Body.GetILProcessor();
                    il.Emit(OpCodes.Ldarg_0);
                    for (int i = 0; i < baseVirtualMethod.Parameters.Count; ++i)
                        il.Emit(OpCodes.Ldarg, i + 1);
                    il.Emit(OpCodes.Call, baseVirtualMethod);
                    il.Emit(OpCodes.Ret);

                    if (WeaveMethod(module, @override, classLevelAspectInfos, type))
                    {
                        type.Methods.Add(@override);
                        @override.Overrides.Add(baseVirtualMethod);
                        weavedAtLeastOneMethod = true;
                    }
                }

            if (weavedAtLeastOneMethod)
                TotalWeavedTypes++;
        }

        private IEnumerable<MethodDefinition> GetPotentiallyOverridableMethods(TypeReference type)
        {
            for (TypeDefinition typeDef = type.Resolve()?.BaseType?.Resolve(); typeDef != null && typeDef.FullName != typeof(Object).FullName; typeDef = typeDef.BaseType.Resolve())
            {
                foreach (var method in typeDef.Methods)
                {
                    if (method.IsVirtual && !method.IsFinal && !method.HasOverrides)
                        yield return method.Resolve();
                }
            }
        }

        private bool WeaveMethod(
            ModuleDefinition module, 
            MethodDefinition method,
            List<AspectInfo> aspectInfos,
            TypeReference type)
        {
            aspectInfos = AspectOrderer.Order(aspectInfos);
            aspectInfos.Reverse(); // last aspect has to be weaved in first

            using (var methodWeaver = new MethodWeaver())
            {
                foreach (var aspectInfo in aspectInfos)
                {
                    ////var log = string.Format("Weave OnMethodBoundaryAspect '{0}' in method '{1}' from class '{2}'",
                    ////    attributeTypeDefinition.Name,
                    ////    method.Name,
                    ////    method.DeclaringType.FullName);
                    ////LogWarning(log);

                    if (aspectInfo.SkipProperties && (method.IsGetter || method.IsSetter))
                        continue;

                    var aspectTypeDefinition = aspectInfo.AspectAttribute.AttributeType;

                    var overriddenAspectMethods = GetUsedAspectMethods(aspectTypeDefinition);
                    if (overriddenAspectMethods == AspectMethods.None)
                        continue;
                    
                    methodWeaver.Weave(method, aspectInfo.AspectAttribute, overriddenAspectMethods, module, type, UnweavedAssembly);
                }

                if (methodWeaver.WeaveCounter == 0)
                    return false;
            }

            if (method.IsGetter || method.IsSetter)
                TotalWeavedPropertyMethods++;
            else
                TotalWeavedMethods++;

            LastWeavedMethod = method;
            return true;
        }

        private AspectMethods GetUsedAspectMethods(TypeReference aspectTypeDefinition)
        {
            var overloadedMethods = new Dictionary<string, MethodDefinition>();

            var currentType = aspectTypeDefinition;
            do
            {
                var typeDefinition = currentType.Resolve();
                var methods = typeDefinition.Methods
                    .Where(x => x.IsVirtual)
                    .ToList();
                foreach (var method in methods)
                {
                    if (overloadedMethods.ContainsKey(method.Name))
                        continue;

                    overloadedMethods.Add(method.Name, method);
                }

                currentType = typeDefinition.BaseType;
            } while (currentType.FullName != typeof(OnMethodBoundaryAspect).FullName);

            var aspectMethods = AspectMethods.None;
            if (overloadedMethods.ContainsKey("OnEntry"))
                aspectMethods |= AspectMethods.OnEntry;
            if (overloadedMethods.ContainsKey("OnExit"))
                aspectMethods |= AspectMethods.OnExit;
            if (overloadedMethods.ContainsKey("OnException"))
                aspectMethods |= AspectMethods.OnException;
            if (overloadedMethods.ContainsKey(nameof(OnMethodBoundaryAspect.CompileTimeValidate)))
                aspectMethods |= AspectMethods.CompileTimeValidate;
            return aspectMethods;
        }

        private bool IsMethodBoundaryAspect(TypeDefinition attributeTypeDefinition)
        {
            var currentType = attributeTypeDefinition.BaseType;
            do
            {
                if (currentType.FullName == typeof(OnMethodBoundaryAspect).FullName)
                    return true;

                currentType = currentType.Resolve().BaseType;
            } while (currentType != null);

            return false;
        }

        private bool IsMethodBoundaryAspect(CustomAttribute customAttribute)
        {
            return IsMethodBoundaryAspect(customAttribute.AttributeType.Resolve());
        }

        private bool IsWeavableMethod(MethodDefinition method, TypeReference type)
        {
            var fullName = method.DeclaringType.FullName;
            var name = method.Name;

            if (IsIgnoredByWeaving(method))
                return false;

            if (IsUserFiltered(fullName, name, type))
                return false;

            return !(method.IsAbstract // abstract or interface method
                     || method.IsConstructor
                     || name.StartsWith("<") // anonymous
                     || method.IsPInvokeImpl); // extern
        }

        private bool IsUserFiltered(string fullName, string name, TypeReference type)
        {
            if (_classFilters.Any())
            {
                var classFullName = fullName;
                if (!_classFilters.Contains(classFullName) && !_classFilters.Contains(type.FullName))
                    return true;
            }

            if (_methodFilters.Any())
            {
                var methodFullName = string.Format("{0}.{1}", fullName, name);
                var matched = _methodFilters.Contains(methodFullName);
                if (!matched)
                    return true;
            }

            if (_propertyFilter.Any())
            {
                var propertySetterFullName = string.Format("{0}.{1}", fullName, name);
                var propertyGetterFullName = string.Format("{0}.{1}", fullName, name);
                var matched = _propertyFilter.Contains(propertySetterFullName) ||
                              _methodFilters.Contains(propertyGetterFullName);
                if (!matched)
                    return true;
            }

            return false;
        }

        private static bool IsIgnoredByWeaving(ICustomAttributeProvider method)
        {
            return method.CustomAttributes.Any(x => x.AttributeType.FullName == typeof(DisableWeavingAttribute).FullName);
        }
    }
}