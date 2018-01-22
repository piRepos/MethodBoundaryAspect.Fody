using MethodBoundaryAspect.Fody.Attributes;
using MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly
{
    public class ClassWithStateMachines
    {
        [RecordEverythingAspect]
        public static async Task<string[]> AsyncMethod(string[] args)
        {
            List<string> results = new List<string>();

            foreach (string arg in args)
            {
                results.Insert(0, arg);
                await Task.Delay(20);
            }

            return results.ToArray();
        }

        [RecordEverythingAspect]
        public static IEnumerable<int> YieldMethod()
        {
            int i() => throw new InvalidOperationException("Failed to generate");
            yield return i();
        }

        // Can't run these tests from test project because the return types
        // aren't serializable.
        public static string TestYieldMethod()
        {
            using (var expectation = new Expectation(nameof(YieldMethod)))
            {
                var iterate = YieldMethod();

                var exitLogs = expectation.Logs(l => l.Member == "OnExit");

                if (exitLogs.Length != 1)
                    return String.Format("{0} exit logs found. Should have been one.", exitLogs.Length);

                var exitLog = exitLogs[0];

                if (!ReferenceEquals(exitLog.Args.ReturnValue, iterate))
                    return "Lazy iterator not found in return value.";

                try
                {
                    iterate.GetEnumerator().MoveNext();
                    return "Expected exception to be thrown when iteration starts.";
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }
        }

        public static string TestAsyncMethod()
        {
            using (var expectation = new Expectation(nameof(AsyncMethod)))
            {
                string[] givenArguments = new string[]
                {
                    "one", "two", "three"
                };

                var result = AsyncMethod(givenArguments);

                var entryLogs = expectation.Logs(l => l.Member == "OnEntry");
                if (entryLogs.Length != 1)
                    return String.Format("{0} entry logs recorded. Should have been one.", entryLogs.Length);

                var entryLog = entryLogs[0];
                var givenArgumentArray = entryLog.Args.Arguments;
                if (givenArgumentArray.Length != 1)
                    return String.Format("{0} entry log arguments recorded. Should have been one.", entryLogs.Length);

                var givenArgument = givenArgumentArray[0];
                if (!ReferenceEquals(givenArgument, givenArguments))
                    return "Entry log recorded unexpected argument";

                var exitLogs = expectation.Logs(l => l.Member == "OnExit");
                if (exitLogs.Length != 1)
                    return String.Format("{0} exit logs recorded. Should have been one.", exitLogs.Length);

                var exitLog = exitLogs[0];
                var returnTask = exitLog.Args.ReturnValue as Task<string[]>;

                if (returnTask == null)
                    return "Exit log recorded return value of wrong type.";

                if (returnTask.IsCompleted)
                    return "Return task should have been complete.";

                var exceptionLog = expectation.Logs(l => l.Member == "OnException");
                if (exceptionLog.Length != 0)
                    return "Exception recorded";
            }

            return null;
        }

        class Expectation : IDisposable
        {
            string _methodName;
            List<Log> _logs;
            public class Log
            {
                public MethodExecutionArgs Args { get; set; }
                public string Member { get; set; }

                public Log(string memberName, MethodExecutionArgs args)
                {
                    Member = memberName;
                    Args = new MethodExecutionArgs()
                    {
                        Arguments = args.Arguments.ToArray(),
                        Exception = args.Exception,
                        Instance = args.Instance,
                        Method = args.Method,
                        MethodExecutionTag = args.MethodExecutionTag,
                        ReturnValue = args.ReturnValue
                    };
                }
            }

            public Expectation(string methodName)
            {
                _methodName = methodName;
                _logs = new List<Log>();
                RecordEverythingAspect.Event += RecordEverythingAspect_Event;
            }

            public Log[] Logs(Func<Log, bool> predicate)
            {
                return _logs.Where(predicate).ToArray();
            }

            void RecordEverythingAspect_Event(string name, MethodExecutionArgs arg)
            {
                if (arg.Method.Name == _methodName)
                    _logs.Add(new Log(name, arg));
            }

            void IDisposable.Dispose()
            {
                RecordEverythingAspect.Event -= RecordEverythingAspect_Event;
            }
        }
    }
}
