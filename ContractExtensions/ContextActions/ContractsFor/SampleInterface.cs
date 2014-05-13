﻿using System.Diagnostics.Contracts;

namespace ReSharper.ContractExtensions.ContextActions.ContractsFor
{
    [ContractClass(typeof(ISomeInterfaceClassContract))]
    internal interface ISomeInterface
    {
        void MethodWithPrecondition(string s);
        string MethodWithPostcondition();
        string PropertyWithPostcondition { get; }
    }

    [ContractClassFor(typeof(ISomeInterface))]
    internal class ISomeInterfaceClassContract : ISomeInterface
    {
        public void MethodWithPrecondition(string s)
        {
            throw new System.NotImplementedException();
        }

        public string MethodWithPostcondition()
        {
            throw new System.NotImplementedException();
        }

        public string PropertyWithPostcondition { get; private set; }
    }
}