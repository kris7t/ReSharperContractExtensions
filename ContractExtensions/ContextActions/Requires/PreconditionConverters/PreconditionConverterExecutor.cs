﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Security.Cryptography;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.ReSharper.Feature.Services.CSharp.Bulbs;
using JetBrains.ReSharper.Intentions.CSharp.ContextActions;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Impl;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Tree;
using ReSharper.ContractExtensions.ContextActions.Infrastructure;
using ReSharper.ContractExtensions.ContractsEx.Assertions;
using ReSharper.ContractExtensions.Utilities;

namespace ReSharper.ContractExtensions.ContextActions.Requires
{
    internal sealed class PreconditionConverterExecutor : ContextActionExecutorBase
    {
        private struct Key
        {
            public PreconditionType from;
            public PreconditionType to;

            public Key To(PreconditionType to)
            {
                this.to = to;
                return this;
            }

            public static Key From(PreconditionType from)
            {
                return new Key {from = from};
            }
        }

        private readonly Dictionary<Key, Action<ContractPreconditionAssertion>> _converters = new Dictionary<Key, Action<ContractPreconditionAssertion>>();

        private readonly PreconditionConverterAvailability _availability;
        private readonly PreconditionType _destinationPreconditionType;

        public PreconditionConverterExecutor(PreconditionConverterAvailability availability,
            PreconditionType destinationPreconditionType)
            : base(availability)
        {
            Contract.Requires(availability.SourcePreconditionType != destinationPreconditionType);

            _availability = availability;
            _destinationPreconditionType = destinationPreconditionType;

            InitializeConverters();
        }

        public override void ExecuteTransaction()
        {
            var converter = GetConverter(_availability.SourcePreconditionType, _destinationPreconditionType);
            Contract.Assert(converter != null, 
                string.Format("Converter from {0} to {1} is unavailable", _availability.SourcePreconditionType, _destinationPreconditionType));

            converter(_availability.PreconditionAssertion);
        }

        [CanBeNull]
        private Action<ContractPreconditionAssertion> GetConverter(PreconditionType sourcePreconditionType, PreconditionType destinationPreconditionType)
        {
            var key = Key.From(sourcePreconditionType).To(destinationPreconditionType);
            Action<ContractPreconditionAssertion> result;
            _converters.TryGetValue(key, out result);
            return result;
        }

        private void InitializeConverters()
        {
            _converters[Key.From(PreconditionType.ContractRequires).To(PreconditionType.GenericContractRequires)] =
                FromRequiresToGenericRequires;

            _converters[Key.From(PreconditionType.GenericContractRequires).To(PreconditionType.ContractRequires)] =
                FromGenericRequiresToRequires;

            _converters[Key.From(PreconditionType.IfThrowStatement).To(PreconditionType.ContractRequires)] =
                a => FromIfThrowToRequires(a, isGeneric: false);
            _converters[Key.From(PreconditionType.IfThrowStatement).To(PreconditionType.GenericContractRequires)] =
                a => FromIfThrowToRequires(a, isGeneric: true);
        }

        private void FromIfThrowToRequires(ContractPreconditionAssertion assertion, bool isGeneric)
        {
            // Convertion from if-throw precondition to Contract.Requires
            // contains following steps:
            // 1. Negate condition from the if statement (because if (s == null) throw ANE means that Contract.Requires(s != null))
            // 2. Create Contract.Requires expression (with all optional generic argument and optional message
            // 3. Add required using statements if necessary (for Contract class and Exception type)
            // 4. Replace if-throw statement with newly created contract statement

            var ifThrowAssertion = (IfThrowPreconditionAssertion) assertion;

            ICSharpExpression negatedExpression = 
                CSharpExpressionUtil.CreateLogicallyNegatedExpression(ifThrowAssertion.IfStatement.Condition);
            
            Contract.Assert(negatedExpression != null);

            string predicateCheck = negatedExpression.GetText();

            AddUsingForTheNamespaceIfNecessary(typeof(Contract).Namespace);

            ICSharpStatement newStatement = null;
            if (isGeneric)
            {
                newStatement = CreateGenericContractRequires(ifThrowAssertion.ExceptionType, predicateCheck,
                    ifThrowAssertion.Message);
                AddUsingForTheNamespaceIfNecessary(ifThrowAssertion.ExceptionType.NamespaceNames.First());
            }
            else
            {
                newStatement = CreateNonGenericContractRequires(predicateCheck, ifThrowAssertion.Message);
            }

            ReplaceStatements(ifThrowAssertion.Statement, newStatement);
        }

        private void FromGenericRequiresToRequires(ContractPreconditionAssertion assertion)
        {
            var requiresAssertion = (ContractRequiresPreconditionAssertion)assertion;
            Contract.Assert(requiresAssertion.IsGeneric);

            string predicateCheck = requiresAssertion.ContractAssertionExpression.PredicateExpression.GetText();
            var newStatement = CreateNonGenericContractRequires(predicateCheck, requiresAssertion.Message);
            
            ReplaceStatements(requiresAssertion.Statement, newStatement);

            RemoveSystemNamespaceUsingIfPossible();
        }

        [System.Diagnostics.Contracts.Pure]
        private ICSharpStatement CreateGenericContractRequires(IClrTypeName exceptionType, string predicateExpression, string message)
        {
            string optionalMessage = message != null ? string.Format(", {0}", message) : null;

            var stringStatement = string.Format("{0}.Requires<{1}>({2}{3});",
                    typeof(Contract).Name, exceptionType.ShortName, predicateExpression,
                    optionalMessage);

            return _factory.CreateStatement(stringStatement);
        }

        [System.Diagnostics.Contracts.Pure]
        public ICSharpStatement CreateNonGenericContractRequires(string predicateExpression, string message)
        {
            string optionalMessage = message != null ? string.Format(", {0}", message) : null;

            var stringStatement = string.Format("{0}.Requires({1}{2});",
                    typeof(Contract).Name, predicateExpression, optionalMessage);

            return _factory.CreateStatement(stringStatement);
        }

        private void FromRequiresToGenericRequires(ContractPreconditionAssertion assertion)
        {
            var requiresAssertion = (ContractRequiresPreconditionAssertion) assertion;
            Contract.Assert(!requiresAssertion.IsGeneric);

            var exceptionType = requiresAssertion.PotentialGenericVersionException();

            string predicate = requiresAssertion.ContractAssertionExpression.PredicateExpression.GetText();
            var newStatement = CreateGenericContractRequires(exceptionType, predicate, requiresAssertion.Message);

            AddUsingForTheNamespaceIfNecessary(exceptionType.NamespaceNames.First());
            ReplaceStatements(requiresAssertion.Statement, newStatement);
        }

        private void ReplaceStatements(ICSharpStatement original, ICSharpStatement @new)
        {
            _currentFile.GetPsiServices().Transactions.Execute("Replace Requires",
                () =>
                {
                    WriteLockCookie.Execute(() => ModificationUtil.ReplaceChild(original, @new));
                });
        }

        private void RemoveSystemNamespaceUsingIfPossible()
        {
            var usingDirective = _factory.CreateUsingDirective("using $0",
                typeof(ArgumentNullException).Namespace);

            var realUsing = _currentFile.Imports.FirstOrDefault(
                ud => ud.ImportedSymbolName.QualifiedName == usingDirective.ImportedSymbolName.QualifiedName);

            if (realUsing == null)
                return;
            
            var usages = UsingUtil.GetUsingDirectiveUsage(realUsing);

            if (usages.Count == 0)
                UsingUtil.RemoveImport(realUsing);
        }

        private void AddUsingForTheNamespaceIfNecessary(string nameSpace)
        {
            var usingDirective = _factory.CreateUsingDirective("using $0",
                nameSpace);

            if (!_currentFile.Imports.ContainsUsing(usingDirective))
            {
                UsingUtil.AddImportAfter(_currentFile, usingDirective);
            }
        }
    }
}