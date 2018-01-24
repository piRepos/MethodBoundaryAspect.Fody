using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MethodBoundaryAspect.Fody.UnitTests
{
    public class AssemblyLoader : MarshalByRefObject
    {
        private Assembly _assembly;

        Dictionary<Guid, object> _instances = new Dictionary<Guid, object>();

        public void Load(string assemblyPath)
        {
            _assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
        }

        public object InvokeMethodWithResultClass(string resultClassName, string className, string methodName, params object[] arguments)
        {
            var type = _assembly.GetType(className, true);
            return InvokeMethodWithResultClass(Activator.CreateInstance(type), resultClassName, methodName, arguments);
        }

        object InvokeMethodWithResultClass(object instance, string resultClassName, string methodName, params object[] arguments)
        {
            var methodInfo = instance.GetType().GetMethod(methodName);
            if (methodInfo == null)
                throw new MissingMethodException(
                    string.Format("Method '{0}' in class '{1}' in assembly '{2}' not found.",
                        methodName,
                        instance.GetType().FullName,
                        _assembly.FullName));
            
            var returnValue = methodInfo.Invoke(instance, arguments);
            //ignore return value of direct call
            //if (returnValue != null)
            //    return returnValue;

            var resultClass = _assembly.GetType(resultClassName, true);
            var resultProperty = resultClass.GetProperty("Result");
            if (resultProperty == null)
                return returnValue;

            var resultValue = resultProperty.GetValue(instance, new object[0]);
            return resultValue;
        }

        public object InvokeMethod(Guid key, string methodName, params object[] arguments)
        {
            var instance = _instances[key];
            return InvokeMethodWithResultClass(instance, instance.GetType().FullName, methodName, arguments);
        }

        public void NewInstanceOfClass(string className, Guid key)
        {
            var type = _assembly.GetType(className, true);
            var o = Activator.CreateInstance(type);
            _instances[key] = o;
        }

        public object InvokeMethod(string className, string methodName, params object[] arguments)
        {
            return InvokeMethodWithResultClass(className, className, methodName, arguments);
        }

        public object GetLastResult(string className)
        {
            var type = _assembly.GetType(className, true);

            var resultProperty = type.GetProperty("Result");
            var resultValue = resultProperty.GetValue(null, new object[0]);
            return resultValue;
        }
    }
}