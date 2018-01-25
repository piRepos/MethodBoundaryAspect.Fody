﻿using MethodBoundaryAspect.Fody.Attributes;

namespace MethodBoundaryAspect.Fody.UnitTests.TestAssembly.Aspects
{
    public class SetReturnValueAspect : OnMethodBoundaryAspect
    {
		public override void OnEntry(MethodExecutionArgs arg)
		{
			base.OnEntry(arg);
		}

		public override void OnExit(MethodExecutionArgs arg)
        {
            SetReturnValueAspectMethods.Result = arg.ReturnValue;
        }
    }
}