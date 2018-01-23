using MethodBoundaryAspect.Fody.Attributes;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MethodBoundaryAspect.Fody
{
    internal class MethodWeaver : IDisposable
    {
        private NamedInstructionBlockChain _createArgumentsArray;
        private MethodBodyPatcher _methodBodyChanger;
        private bool _finished;

        public int WeaveCounter { get; private set; }

        static bool DetermineValidation(string methodName, string className, string aspectName,
            string[] methodParams, byte[] blob, string[] ctorParams)
        {
            return false;
        }

        object[] GetRuntimeAttributeArgs(IEnumerable<CustomAttributeArgument> args)
        {
            return args.Select(GetRuntimeAttributeArg).ToArray();
        }

        object GetRuntimeAttributeArg(CustomAttributeArgument arg)
        {
            switch (arg.Value)
            {
                case CustomAttributeArgument[] array:
                    return array.Select(GetRuntimeAttributeArg).ToArray();
                case CustomAttributeArgument arg2:
                    return GetRuntimeAttributeArg(arg2);
                default:
                    return arg.Value;
            }
        }

        public void Weave(
            MethodDefinition method,
            CustomAttribute aspect,
            AspectMethods overriddenAspectMethods,
            ModuleDefinition moduleDefinition,
            TypeReference type,
            byte[] unweavedAssembly)
        {
            if (overriddenAspectMethods == AspectMethods.None)
                return;

            if (overriddenAspectMethods.HasFlag(AspectMethods.CompileTimeValidate))
            {
                AppDomain.CurrentDomain.Load(unweavedAssembly);
                string className = String.Format("{0}, {1}", type.FullName, type.Module.Assembly.Name.Name);
                string methodName = method.Name;
                string[] methodParams = method.Parameters.Select(p => p.ParameterType.FullName).ToArray();
                byte[] blob = aspect.GetBlob();
                string[] ctorParams = aspect.Constructor.Parameters.Select(p => p.ParameterType.FullName).ToArray();
                string aspectName = String.Format("{0}, {1}", aspect.AttributeType.FullName, aspect.AttributeType.Module.Assembly.Name.Name);
                
                try
                {
                    var typeInfo = Type.GetType(className);
                    if (typeInfo == null)
                        throw new InvalidOperationException(String.Format("Could not find type '{0}'.", className));

                    Type[] parameters = methodParams.Select(Type.GetType).ToArray();
                    var methodInfo = typeInfo.GetMethod(methodName, parameters);

                    Type aspectType = Type.GetType(aspectName);
                    var ctorInfo = aspectType.GetConstructor(ctorParams.Select(Type.GetType).ToArray());
                    if (ctorInfo == null)
                        throw new InvalidOperationException("Could not find constructor for aspect.");
                    
                    object[] ctorArgs = GetRuntimeAttributeArgs(aspect.ConstructorArguments);
                    var aspectInstance = Activator.CreateInstance(aspectType, ctorArgs) as OnMethodBoundaryAspect;
                    if (aspectInstance == null)
                        throw new InvalidOperationException("Could not create aspect.");

                    foreach (var fieldSetter in aspect.Fields)
                    {
                        var field = aspectType.GetField(fieldSetter.Name);
                        if (field == null)
                            throw new InvalidOperationException(String.Format("Could not find field named {0}", fieldSetter.Name));
                        field.SetValue(aspectInstance, fieldSetter.Argument.Value);
                    }

                    foreach (var propSetter in aspect.Properties)
                    {
                        var prop = aspectType.GetProperty(propSetter.Name);
                        if (prop == null)
                            throw new InvalidOperationException(String.Format("Could not find property named {0}", propSetter.Name));
                        prop.SetValue(aspectInstance, propSetter.Argument.Value);
                    }

                    if (!aspectInstance.CompileTimeValidate(methodInfo))
                        return;
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Error while trying to compile-time validate.", e);
                }
            }

            var creator = new InstructionBlockChainCreator(method, aspect.AttributeType, moduleDefinition, WeaveCounter);

            _methodBodyChanger = new MethodBodyPatcher(method.Name, method);
            var saveReturnValue = creator.SaveReturnValue();
            var loadReturnValue = creator.LoadValueOnStack(saveReturnValue);
            _methodBodyChanger.Unify(saveReturnValue, loadReturnValue);

            if (WeaveCounter == 0)
                _createArgumentsArray = creator.CreateMethodArgumentsArray();

            var createMethodExecutionArgsInstance = creator.CreateMethodExecutionArgsInstance(_createArgumentsArray);
            _methodBodyChanger.AddCreateMethodExecutionArgs(createMethodExecutionArgsInstance);

            var createAspectInstance = creator.CreateAspectInstance(aspect);
            if (overriddenAspectMethods.HasFlag(AspectMethods.OnEntry))
            {
                var callAspectOnEntry = creator.CallAspectOnEntry(createAspectInstance,
                    createMethodExecutionArgsInstance);
                _methodBodyChanger.AddOnEntryCall(createAspectInstance, callAspectOnEntry);
            }

            if (overriddenAspectMethods.HasFlag(AspectMethods.OnExit))
            {
                var setMethodExecutionArgsReturnValue =
                    creator.SetMethodExecutionArgsReturnValue(createMethodExecutionArgsInstance, loadReturnValue);
                var callAspectOnExit = creator.CallAspectOnExit(createAspectInstance,
                    createMethodExecutionArgsInstance);

                var readReturnValue = creator.ReadReturnValue(createMethodExecutionArgsInstance, loadReturnValue);

                _methodBodyChanger.AddOnExitCall(createAspectInstance, callAspectOnExit,
                    setMethodExecutionArgsReturnValue, readReturnValue);
            }

            if (overriddenAspectMethods.HasFlag(AspectMethods.OnException))
            {
                var setMethodExecutionArgsExceptionFromStack =
                    creator.SetMethodExecutionArgsExceptionFromStack(createMethodExecutionArgsInstance);

                var callAspectOnException = creator.CallAspectOnException(createAspectInstance,
                    createMethodExecutionArgsInstance);
                _methodBodyChanger.AddOnExceptionCall(createAspectInstance, callAspectOnException,
                    setMethodExecutionArgsExceptionFromStack);
            }

            if (_methodBodyChanger.HasMultipleReturnAndEndsWithThrow)
                _methodBodyChanger.ReplaceThrowAtEndOfRealBodyWithReturn();
            else if (_methodBodyChanger.EndsWithThrow)
            {
                var saveThrownException = creator.SaveThrownException();
                var loadThrownException = creator.LoadValueOnStack(saveThrownException);
                var loadThrownException2 = creator.LoadValueOnStack(saveThrownException);
                _methodBodyChanger.FixThrowAtEndOfRealBody(
                    saveThrownException,
                    loadThrownException,
                    loadThrownException2);
            }

            _methodBodyChanger.OptimizeBody();

            Catel.Fody.CecilExtensions.UpdateDebugInfo(method);

            WeaveCounter++;
        }

        public void Finish()
        {
            if (_finished)
                return;

            if (_methodBodyChanger != null)
                _methodBodyChanger.AddCreateArgumentsArray(_createArgumentsArray);
            _finished = true;
        }

        public void Dispose()
        {
            Finish();
        }
    }
}