using System.Diagnostics.Contracts;

public class PreconditionWithContractPublicPropertyName
{
    [ContractPublicPropertyName("IsValid")]
    private bool _isValid = false;
    public bool IsValid { get; private set; }
    public void Foo()
    {
        Contract.Requires(_isValid);
    } 
}
