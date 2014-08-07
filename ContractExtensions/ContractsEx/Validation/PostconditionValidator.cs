using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resx.Impl;
using JetBrains.ReSharper.Psi.Util;
using ReSharper.ContractExtensions.ContractsEx;
using ReSharper.ContractExtensions.ContractsEx.Statements;
using ReSharper.ContractExtensions.Utilities;

namespace ReSharper.ContractExtensions.ProblemAnalyzers.PreconditionAnalyzers.MalformContractAnalyzers
{
    internal static class PostconditionValidator
    {
        public static IEnumerable<ValidationRule> GetValidationRules()
        {
            yield return ValidationRule.CheckCodeContractStatement(
                CheckCompatibilityOfContractResultWithMethodReturnType);
        }

        private static ValidationResult CheckCompatibilityOfContractResultWithMethodReturnType(
            CodeContractStatement statement)
        {
            var contractResultTypes =
                statement.CodeContractExpression
                    .Value
                    .With(x => x as ContractEnsuresExpression)
                    .Return(ensures => ensures.ContractResultTypes, Enumerable.Empty<IDeclaredType>().ToList());

            var method = statement.GetDeclaredMethod();

            var methodResult = method
                .With(x => x.DeclaredElement)
                .Return(x => x.ReturnType);

            if (methodResult != null && contractResultTypes.Count != 0)
            {
                if (methodResult.IsVoid())
                {
                    return ValidationResult.CreateError(statement.Statement,
                        MalformedContractError.EnsuresInVoidReturnMethod,
                        ErrorForIncompatibleEnsuresAndReturnType(methodResult, contractResultTypes.First(), method));
                }

                if (!MethodResultIsCompatibleWith(methodResult, contractResultTypes))
                {
                    return ValidationResult.CreateError(statement.Statement,
                        MalformedContractError.ResultTypeInEnsuresIsIncompatibleWithMethodReturnType,
                        ErrorForIncompatibleEnsuresAndReturnType(methodResult, contractResultTypes.First(), method));
                }
            }

            return ValidationResult.CreateNoError(statement.Statement);
        }

        private static string ErrorForIncompatibleEnsuresAndReturnType(IType methodResult, IDeclaredType contractResult, 
            ICSharpFunctionDeclaration method)
        {
            string kind = "method";
            string name = method.DeclaredName;

            var property = method as IAccessorDeclaration;
            if (property != null)
            {
                kind = "property";
                name = name.Replace("get_", "");
            }
            
            //string methodOrProperty = method.
            return string.Format("Detected a call to Contract.Result<{0}>() in {1} '{2}' with return type '{3}'",
                contractResult.GetClrName().ShortName, kind, name, 
                methodResult.GetPresentableName(CSharpLanguage.Instance));
        }

        private static bool MethodResultIsCompatibleWith(IType methodResult, IList<IDeclaredType> contractResultTypes)
        {
            // Method return true for "object" <- "string"
            // but false for "string" <- "object"
            var rule = new CSharpTypeConversionRule(methodResult.Module);
            return
                contractResultTypes
                .All(contractResult =>
                {
                    // Result compatibility should consider tasks as an 
                    var resultType = methodResult.IsGenericTask() ? methodResult.GetTaskUnderlyingType() : methodResult;
                    Contract.Assert(resultType != null);

                    return resultType.IsImplicitlyConvertibleTo(contractResult, rule);
                });
        }

        
    }
}