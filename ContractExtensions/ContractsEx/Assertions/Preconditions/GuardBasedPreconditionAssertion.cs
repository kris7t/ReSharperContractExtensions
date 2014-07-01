﻿using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace ReSharper.ContractExtensions.ContractsEx.Assertions
{
    /// <summary>
    /// Represents precondition like "Guard.NotNull(s)" or similar clauses.
    /// </summary>
    /// <remarks>
    /// The only "guards" that considered as preconditions by Code Contracts library should be 
    /// decorated with ContractArgumentValidatorAttribute.
    /// </remarks>
    public sealed class GuardBasedPreconditionAssertion : ContractPreconditionAssertion
    {
        public GuardBasedPreconditionAssertion(ICSharpStatement statement, ContractAssertionExpression expression)
            : base(statement, expression)
        {}

        public override bool AssertsArgumentIsNotNull(string name)
        {
            throw new System.NotImplementedException();
        }

        public override PreconditionType PreconditionType
        {
            get { return PreconditionType.GuardClause; }
        }
    }
}