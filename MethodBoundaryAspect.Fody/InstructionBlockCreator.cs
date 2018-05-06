using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace MethodBoundaryAspect.Fody
{
    public class InstructionBlockCreator
    {
        private const string VoidType = "Void";

        private readonly MethodDefinition _method;
        private readonly ReferenceFinder _referenceFinder;

        private readonly ILProcessor _processor;

        public InstructionBlockCreator(MethodDefinition method, ReferenceFinder referenceFinder)
        {
            _method = method;
            _referenceFinder = referenceFinder;
            _processor = _method.Body.GetILProcessor();
        }

        public bool HasReturnValue()
        {
            return !IsVoid(_method.ReturnType);
        }

        public bool HasThrowAsReturn()
        {
            return _method.Body.Instructions.Last().OpCode.Code == Code.Throw;
        }

        public VariableDefinition CreateVariable(TypeReference variableTypeReference)
        {
            if (IsVoid(variableTypeReference))
                throw new InvalidOperationException("Variable of type 'Void' is not possible!");

            var variableDefinition = new VariableDefinition(variableTypeReference);
            _method.Body.Variables.Add(variableDefinition);
            return variableDefinition;
        }

        public InstructionBlock CreateThisVariable(VariableDefinition instanceVariable,
            TypeReference objectTypeReference)
        {
            if (_method.IsStatic)
                return new InstructionBlock("Static method call: " + _method.Name, _processor.Create(OpCodes.Nop));

            var loadThisInstruction = _processor.Create(OpCodes.Ldarg_0);
            var castInstruction = _processor.Create(OpCodes.Castclass, objectTypeReference);
            var storeThisInstruction = _processor.Create(OpCodes.Stloc, instanceVariable);

            return new InstructionBlock(
                "Static method call: " + _method.Name,
                loadThisInstruction,
                castInstruction,
                storeThisInstruction);
        }

        public InstructionBlock CreateObjectArrayWithMethodArguments(VariableDefinition objectArray,
            TypeReference objectTypeReference)
        {
            var createArrayInstructions = new List<Instruction>
            {
                _processor.Create(OpCodes.Ldc_I4, _method.Parameters.Count), //method.Parameters.Count
                _processor.Create(OpCodes.Newarr, objectTypeReference), // new object[method.Parameters.Count]
                _processor.Create(OpCodes.Stloc, objectArray) // var objArray = new object[method.Parameters.Count]
            };

            foreach (var parameter in _method.Parameters)
            {
                var boxedArguments = BoxArguments(parameter, objectArray).ToList();
                createArrayInstructions.AddRange(boxedArguments);
            }

            return new InstructionBlock("CreateObjectArrayWithMethodArguments", createArrayInstructions);
        }

        public InstructionBlock NewObject(
            VariableDefinition newInstance,
            TypeReference instanceTypeReference,
            ModuleDefinition module,
            int aspectCounter)
        {

            return NewObject(newInstance, instanceTypeReference, module, null, aspectCounter);
        }
        
        public static string CreateVariableName(string name, int aspectCounter, int index)
        {
            return string.Format("__fody${0}${1}${2}", aspectCounter, name, index);
        }

        public InstructionBlock NewObject(
            VariableDefinition newInstance,
            TypeReference instanceTypeReference,
            ModuleDefinition module,
            CustomAttribute aspect,
            int aspectCounter)
        {
            var typeDefinition = instanceTypeReference.Resolve();
            var constructor = typeDefinition
                .Methods
                .SingleOrDefault(x => x.IsConstructor &&
                                !x.IsStatic &&
                                x.Parameters.Select(p => p.ParameterType)
                                    .SequenceEqual(aspect == null ? new TypeReference[0] : aspect.ConstructorArguments.Select(a => a.Type)));

            if (constructor == null)
                throw new InvalidOperationException(string.Format("Didn't found matching constructor on type '{0}'",
                    typeDefinition.FullName));

            var ctorRef = module.ImportReference(constructor);
            var newObjectInstruction = _processor.Create(OpCodes.Newobj, ctorRef);
            var assignToVariableInstruction = _processor.Create(OpCodes.Stloc, newInstance);

            var loadConstValuesOnStack = new List<Instruction>();
            var loadSetConstValuesToAspect = new List<Instruction>();
            if (aspect != null)
            {
                // ctor parameters
                var loadInstructions = aspect.Constructor.Parameters
                    .Zip(aspect.ConstructorArguments, (p, v) => new {Parameter = p, Value = v})
                    .SelectMany(x => LoadValueOnStack(x.Parameter.ParameterType, x.Value.Value, module))
                    .ToList();
                loadConstValuesOnStack.AddRange(loadInstructions);

                // named arguments
                foreach (var property in aspect.Properties)
                {
                    var propertyCopy = property;

                    var loadOnStackInstruction = LoadValueOnStack(propertyCopy.Argument.Type, propertyCopy.Argument.Value, module);
                    var valueVariable = CreateVariable(propertyCopy.Argument.Type);
                    var assignVariableInstructionBlock = AssignValueFromStack(valueVariable);

                    var methodRef = _referenceFinder.GetMethodReference(instanceTypeReference, md => md.Name == "set_" + propertyCopy.Name);
                    var setPropertyInstructionBlock = CallVoidInstanceMethod(newInstance,methodRef, valueVariable);

                    loadSetConstValuesToAspect.AddRange(loadOnStackInstruction);
                    loadSetConstValuesToAspect.AddRange(assignVariableInstructionBlock.Instructions);
                    loadSetConstValuesToAspect.AddRange(setPropertyInstructionBlock.Instructions);
                }

                foreach (var field in aspect.Fields)
                {
                    var fieldCopy = field;

                    var loadInstanceOnStackInstruction = _processor.Create(OpCodes.Ldloc, newInstance);
                    var loadOnStackInstruction = LoadValueOnStack(fieldCopy.Argument.Type, fieldCopy.Argument.Value, module);
                    
                    var fieldRef = instanceTypeReference.Resolve().Fields.First(f => f.Name == fieldCopy.Name);
                    var loadFieldInstructionBlock = _processor.Create(OpCodes.Stfld, fieldRef);

                    loadSetConstValuesToAspect.Add(loadInstanceOnStackInstruction);
                    loadSetConstValuesToAspect.AddRange(loadOnStackInstruction);
                    loadSetConstValuesToAspect.Add(loadFieldInstructionBlock);
                }
            }

            var allInstructions = new List<Instruction>();
            allInstructions.AddRange(loadConstValuesOnStack);
            allInstructions.Add(newObjectInstruction);
            allInstructions.Add(assignToVariableInstruction);
            allInstructions.AddRange(loadSetConstValuesToAspect);

            return new InstructionBlock(
                "NewObject: " + instanceTypeReference.Name,
                allInstructions);
        }

        public InstructionBlock SaveReturnValueFromStack(VariableDefinition returnValue)
        {
            if (IsVoid(_method.ReturnType))
                return new InstructionBlock("SaveReturnValueFromStack - void", _processor.Create(OpCodes.Nop));

            var assignReturnValueToVariableInstruction = _processor.Create(OpCodes.Stloc, returnValue);
            return new InstructionBlock(
                "SaveReturnValueFromStack",
                assignReturnValueToVariableInstruction);
        }

        public InstructionBlock AssignValueFromStack(VariableDefinition variable)
        {
            return new InstructionBlock(
                "AssignValueFromStack",
                _processor.Create(OpCodes.Stloc, variable));
        }

        public InstructionBlock PushValueOnStack(VariableDefinition variable)
        {
            return new InstructionBlock(
                "PushValueOnStack",
                _processor.Create(OpCodes.Ldloc, variable));
        }

		public InstructionBlock JumpIfFalse(Instruction target)
		{
			return new InstructionBlock(
				"JumpIfFalse",
				_processor.Create(OpCodes.Brfalse_S, target));
		}

        public InstructionBlock CallVoidInstanceMethod(
            VariableDefinition callInstanceVariable,
            MethodReference methodReference,
            params VariableDefinition[] argumentVariables)
        {
            var instructions = CreateInstanceMethodCallInstructions(callInstanceVariable, null, methodReference,
                argumentVariables);
            return new InstructionBlock("CallVoidInstanceMethod: " + methodReference.Name, instructions);
        }

        public InstructionBlock CallStaticMethod(
            MethodReference methodReference,
            VariableDefinition resultVariable,
            params VariableDefinition[] argumentVariables)
        {
            var instructions = CreateInstanceMethodCallInstructions(null, resultVariable, methodReference,
                argumentVariables);
            return new InstructionBlock("CallStaticMethod: " + methodReference.Name, instructions);
        }

        public InstructionBlock CallInstanceMethod(
            VariableDefinition callerInstance,
            VariableDefinition returnValue,
            MethodReference methodReference,
            params VariableDefinition[] arguments)
        {
            var instructions = CreateInstanceMethodCallInstructions(callerInstance, returnValue, methodReference,
                arguments);
            return new InstructionBlock("CallInstanceMethod: " + methodReference.Name, instructions);
        }

        private List<Instruction> CreateInstanceMethodCallInstructions(
            VariableDefinition callerInstance,
            VariableDefinition returnValue,
            MethodReference methodReference,
            params VariableDefinition[] arguments)
        {
            Instruction loadVariableInstruction = null;
            if (callerInstance != null)
                loadVariableInstruction = _processor.Create(OpCodes.Ldloc, callerInstance);

            var loadArgumentsInstructions = new List<Instruction>();
            int parameterCount = 0;
            foreach (var argument in arguments)
            {
                loadArgumentsInstructions.Add(_processor.Create(OpCodes.Ldloc, argument));

                var parameter = methodReference.Parameters[parameterCount];
                if (parameter.ParameterType != argument.VariableType)
                {
                    if (argument.VariableType.IsValueType)
                        loadArgumentsInstructions.Add(_processor.Create(OpCodes.Box, argument.VariableType));
                }
                parameterCount++;
            }

            var methodDefinition = methodReference.Resolve();
            var methodCallInstruction = _processor.Create(
                methodDefinition.IsVirtual
                    ? OpCodes.Callvirt
                    : OpCodes.Call,
                methodReference);

            Instruction assignReturnValueToVariableInstruction = null;
            Instruction castReturnValueToCorrectType = null;
            if (returnValue != null)
            {
                if (IsVoid(methodDefinition.ReturnType))
                    throw new InvalidOperationException("Method has no return value");
                
                if (!methodDefinition.ReturnType.IsAssignableTo(returnValue.VariableType))
                    castReturnValueToCorrectType = _processor.Create(OpCodes.Unbox_Any, returnValue.VariableType);

                assignReturnValueToVariableInstruction = _processor.Create(OpCodes.Stloc, returnValue);
            }

            var instructions = new List<Instruction>();
            if (loadVariableInstruction != null)
                instructions.Add(loadVariableInstruction);
            instructions.AddRange(loadArgumentsInstructions);
            instructions.Add(methodCallInstruction);
            if (castReturnValueToCorrectType != null)
                instructions.Add(castReturnValueToCorrectType);
            if (assignReturnValueToVariableInstruction != null)
                instructions.Add(assignReturnValueToVariableInstruction);
            return instructions;
        }

        public InstructionBlock ReadParameterArray(VariableDefinition executionArgs)
        {
            var instructions = new List<Instruction>();

            var argsAsReturned = CreateVariable(_referenceFinder.GetTypeReference(typeof(Object[])));
            instructions.AddRange(CreateInstanceMethodCallInstructions(executionArgs, argsAsReturned,
                _referenceFinder.GetMethodReference(executionArgs.VariableType, md => md.Name == "get_Arguments")));
            
            for (int i = 0; i < _method.Parameters.Count; ++i)
            {
                var p = _method.Parameters[i];
                if (p.ParameterType.IsByReference)
                    instructions.Add(_processor.Create(OpCodes.Ldarg, i + 1));
                instructions.Add(_processor.Create(OpCodes.Ldloc, argsAsReturned));
                instructions.Add(_processor.Create(OpCodes.Ldc_I4, i));
                instructions.Add(_processor.Create(OpCodes.Ldelem_Ref));
                instructions.AddRange(CecilExtensions.StoreTypeInArgument(_processor, p));
            }
            
            return new InstructionBlock("ReadParameterArray: ", instructions);
        }

        private IList<Instruction> LoadValueOnStack(TypeReference parameterType, object value, ModuleDefinition module)
        {
            if (parameterType.IsPrimitive || (parameterType.FullName == "System.String"))
                return new List<Instruction> {LoadPrimitiveConstOnStack(parameterType.MetadataType, value)};

            if (parameterType.IsValueType) // enum
            {
                var enumUnderlyingType = GetEnumUnderlyingType(parameterType.Resolve());
                return new List<Instruction> {LoadPrimitiveConstOnStack(enumUnderlyingType.MetadataType, value)};
            }

            if (parameterType.FullName == "System.Type")
            {
                var typeName = value.ToString();
                var typeReference = module.GetType(typeName, true);

                var typeTypeRef = _referenceFinder.GetTypeReference(typeof (Type));
                var methodReference = _referenceFinder.GetMethodReference(typeTypeRef, md => md.Name == "GetTypeFromHandle");
                
                var instructions = new List<Instruction>
                {
                    _processor.Create(OpCodes.Ldtoken, typeReference),
                    _processor.Create(OpCodes.Call, methodReference)
                };

                return instructions;
            }

            if (parameterType.FullName == typeof(Object).FullName && value is CustomAttributeArgument arg)
            {
                var valueType = _referenceFinder.GetTypeReference(arg.Value.GetType());
                if (arg.Value.GetType() == typeof(TypeReference))
                    valueType = _referenceFinder.GetTypeReference(typeof(Type));
                var instructions = LoadValueOnStack(valueType, arg.Value, module);
                instructions.Add(_processor.Create(OpCodes.Box, valueType));
                return instructions;
            }

            if (parameterType.IsArray && value is CustomAttributeArgument[] args)
            {
                var array = CreateVariable(parameterType);
                var elementType = ((ArrayType)parameterType).ElementType;
                var createArrayInstructions = new List<Instruction>()
                {
                    _processor.Create(OpCodes.Ldc_I4, args.Length),
                    _processor.Create(OpCodes.Newarr, elementType),
                    _processor.Create(OpCodes.Stloc, array)
                };

                OpCode stelem;
                if (elementType.IsValueType)
                    stelem = elementType.MetadataType.GetStElemCode();
                else
                    stelem = OpCodes.Stelem_Ref;
                
                for (int i = 0; i < args.Length; ++i)
                {
                    var parameter = args[i];
                    createArrayInstructions.Add(_processor.Create(OpCodes.Ldloc, array));
                    createArrayInstructions.Add(_processor.Create(OpCodes.Ldc_I4, i));
                    createArrayInstructions.AddRange(LoadValueOnStack(elementType, parameter.Value, module));
                    createArrayInstructions.Add(_processor.Create(stelem));
                }

                createArrayInstructions.Add(_processor.Create(OpCodes.Ldloc, array));

                return createArrayInstructions;
            }
            
            throw new NotSupportedException("Parametertype: " + parameterType);
        }

        private Instruction LoadPrimitiveConstOnStack(MetadataType type, object value)
        {
            switch (type)
            {
                case MetadataType.String:
                    return _processor.Create(OpCodes.Ldstr, (string) value);
                case MetadataType.Int32:
                    return _processor.Create(OpCodes.Ldc_I4, (int) value);
                case MetadataType.Int64:
                    return _processor.Create(OpCodes.Ldc_I8, (long) value);
                case MetadataType.Boolean:
                    return _processor.Create(OpCodes.Ldc_I4, (bool) value ? 1 : 0);
            }

            throw new NotSupportedException("Not a supported primitve parameter type: " + type);
        }

        private static TypeReference GetEnumUnderlyingType(TypeDefinition self)
        {
            var fields = self.Fields;

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field.Name == "value__")
                    return field.FieldType;
            }

            throw new ArgumentException();
        } 

        private static IEnumerable<Instruction> BoxArguments(ParameterDefinition parameterDefinition,
            VariableDefinition paramsArray)
        {

            var paramMetaData = parameterDefinition.ParameterType.MetadataType;
            if (paramMetaData == MetadataType.UIntPtr ||
                paramMetaData == MetadataType.FunctionPointer ||
                paramMetaData == MetadataType.IntPtr ||
                paramMetaData == MetadataType.Pointer)
            {
                yield break;
            }

            yield return Instruction.Create(OpCodes.Ldloc, paramsArray);
            yield return Instruction.Create(OpCodes.Ldc_I4, parameterDefinition.Index);
            yield return Instruction.Create(OpCodes.Ldarg, parameterDefinition);

            // Reset boolean flag variable to false

            // If a parameter is passed by reference then you need to use Ldind
            // ------------------------------------------------------------
            var paramType = parameterDefinition.ParameterType;

            if (paramType.IsByReference)
            {
                var referencedTypeSpec = (TypeSpecification) paramType;

                var pointerToValueTypeVariable = false;
                switch (referencedTypeSpec.ElementType.MetadataType)
                {
                        //Indirect load value of type int8 as int32 on the stack
                    case MetadataType.Boolean:
                    case MetadataType.SByte:
                        yield return Instruction.Create(OpCodes.Ldind_I1);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type int16 as int32 on the stack
                    case MetadataType.Int16:
                        yield return Instruction.Create(OpCodes.Ldind_I2);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type int32 as int32 on the stack
                    case MetadataType.Int32:
                        yield return Instruction.Create(OpCodes.Ldind_I4);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type int64 as int64 on the stack
                        // Indirect load value of type unsigned int64 as int64 on the stack (alias for ldind.i8)
                    case MetadataType.Int64:
                    case MetadataType.UInt64:
                        yield return Instruction.Create(OpCodes.Ldind_I8);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type unsigned int8 as int32 on the stack
                    case MetadataType.Byte:
                        yield return Instruction.Create(OpCodes.Ldind_U1);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type unsigned int16 as int32 on the stack
                    case MetadataType.UInt16:
                    case MetadataType.Char:
                        yield return Instruction.Create(OpCodes.Ldind_U2);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type unsigned int32 as int32 on the stack
                    case MetadataType.UInt32:
                        yield return Instruction.Create(OpCodes.Ldind_U4);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type float32 as F on the stack
                    case MetadataType.Single:
                        yield return Instruction.Create(OpCodes.Ldind_R4);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type float64 as F on the stack
                    case MetadataType.Double:
                        yield return Instruction.Create(OpCodes.Ldind_R8);
                        pointerToValueTypeVariable = true;
                        break;

                        // Indirect load value of type native int as native int on the stack
                    case MetadataType.IntPtr:
                    case MetadataType.UIntPtr:
                        yield return Instruction.Create(OpCodes.Ldind_I);
                        pointerToValueTypeVariable = true;
                        break;

                    default:
                        // Need to check if it is a value type instance, in which case
                        // we use Ldobj instruction to copy the contents of value type
                        // instance to stack and then box it
                        if (referencedTypeSpec.ElementType.IsValueType)
                        {
                            yield return Instruction.Create(OpCodes.Ldobj, referencedTypeSpec.ElementType);
                            pointerToValueTypeVariable = true;
                        }
                        else
                        {
                            // It is a reference type so just use reference the pointer
                            yield return Instruction.Create(OpCodes.Ldind_Ref);
                        }
                        break;
                }

                if (pointerToValueTypeVariable)
                {
                    // Box the de-referenced parameter type
                    yield return Instruction.Create(OpCodes.Box, referencedTypeSpec.ElementType);
                }

            }
            else
            {

                // If it is a value type then you need to box the instance as we are going 
                // to add it to an array which is of type object (reference type)
                // ------------------------------------------------------------
                if (paramType.IsValueType || paramType.IsGenericParameter)
                {
                    // Box the parameter type
                    yield return Instruction.Create(OpCodes.Box, paramType);
                }
                else if (paramType.Name == "String")
                {
                    //var elementType = paramsArray.VariableType.GetElementType();
                    //yield return Instruction.Create(OpCodes.Castclass, elementType);
                }
            }

            // Store parameter in object[] array
            // ------------------------------------------------------------
            yield return Instruction.Create(OpCodes.Stelem_Ref);
        }

        private static bool IsVoid(TypeReference type)
        {
            return type.Name == VoidType;
        }

        public FieldDefinition StoreMethodInfoInStaticField(MethodReference method, TypeReference type)
        {
            var typeDef = type.Resolve();
            int i;
            for (i = 1; typeDef.Fields.FirstOrDefault(f => f.Name == $"<{method.Name}>k_methodField_{i}") != null; ++i)
            { }

            var field = new FieldDefinition($"<{method.Name}>k_methodField_{i}", FieldAttributes.Static | FieldAttributes.Private,
                _referenceFinder.GetTypeReference(typeof(System.Reflection.MethodInfo)));

            var staticCtor = EnsureStaticConstructor(typeDef);

            var processor = staticCtor.Body.GetILProcessor();
            var first = staticCtor.Body.Instructions.First();
            
            var getMethodFromHandle = _referenceFinder.GetMethodReference(typeof(System.Reflection.MethodBase), md => md.Name == "GetMethodFromHandle" && md.Parameters.Count == 2);
            processor.InsertBefore(first, Instruction.Create(OpCodes.Ldtoken, method));
            processor.InsertBefore(first, Instruction.Create(OpCodes.Ldtoken, type));
            processor.InsertBefore(first, Instruction.Create(OpCodes.Call, getMethodFromHandle));
            processor.InsertBefore(first, Instruction.Create(OpCodes.Castclass, _referenceFinder.GetTypeReference(typeof(System.Reflection.MethodInfo))));
            processor.InsertBefore(first, Instruction.Create(OpCodes.Stsfld, field));

            typeDef.Fields.Add(field);

            return field.Resolve();
        }

        public MethodDefinition EnsureStaticConstructor(TypeDefinition type)
        {
            var staticCtor = type.GetStaticConstructor();
            if (staticCtor == null)
            {
                var voidRef = type.Module.TypeSystem.Void;
                staticCtor = new MethodDefinition(".cctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static, voidRef);
                var il = staticCtor.Body.GetILProcessor();
                il.Emit(OpCodes.Ret);
                type.Methods.Add(staticCtor);
            }
            staticCtor.Body.InitLocals = true;
            return staticCtor;
        }
    }
}